using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace MagnumOpus.Names
{
    /// <summary>What a remap did, and what it could not resolve.</summary>
    public sealed class RemapResult
    {
        public int Types, Members;
        /// <summary>References with no counterpart in the running build, in human names
        /// ("T Sim", "M Sim::GetParts|..."). They are left as compiled: the code that uses them
        /// throws when the JIT reaches it, and nothing else does.</summary>
        public readonly List<string> Unresolved = new List<string>();
    }

    /// <summary>
    /// The load-time half of the name pipeline: rewrites a module's references into the game assembly
    /// from the human names it was compiled against to the running build's shipping names, through a
    /// <see cref="NameTable"/>. Assembly identity needs no touch ("Lightning" in both). Every key is
    /// computed BEFORE anything is renamed (a nested reference's key includes its declaring type).
    /// Member references are found through method bodies as well as the reference table: Cecil makes a
    /// fresh reference per call site for members of generic instances (the Echopunks lesson), which
    /// the table view never reaches. Custom-attribute arguments of type System.Type are rewritten too,
    /// though module code should target game members through expressions, not attributes.
    /// String literals are never touched: reflection by human name does not remap.
    /// </summary>
    public static class Remapper
    {
        public const string GameAssembly = "Lightning";

        public static RemapResult Remap(ModuleDefinition module, NameTable table)
        {
            var result = new RemapResult();
            var unresolved = new HashSet<string>(StringComparer.Ordinal);

            // Members: collect (reference, new name) with keys taken from the unrenamed model.
            var memberRefs = new List<MemberReference>();
            var seen = new HashSet<MemberReference>();
            void Add(MemberReference mr)
            {
                if (mr is GenericInstanceMethod gim) mr = gim.ElementMethod;
                if (mr is MethodDefinition || mr is FieldDefinition) return;
                if (mr is MethodReference || mr is FieldReference)
                    if (seen.Add(mr)) memberRefs.Add(mr);
            }
            foreach (var mr in module.GetMemberReferences()) Add(mr);
            foreach (var type in module.GetTypes())
                foreach (var method in type.Methods)
                {
                    if (!method.HasBody) continue;
                    foreach (Instruction ins in method.Body.Instructions)
                        if (ins.Operand is MemberReference mr) Add(mr);
                }

            var memberRenames = new List<KeyValuePair<MemberReference, string>>();
            foreach (var mr in memberRefs)
            {
                TypeReference decl = mr.DeclaringType;
                if (decl == null) continue;
                TypeReference element = decl.GetElementType();
                if (!IsGameRef(element)) continue;
                string declFull = Sig.Full(element);
                string shipping;
                bool ok;
                string label;
                if (mr is MethodReference m)
                {
                    string sig = Sig.Method(m);
                    ok = table.TryMethod(declFull, m.Name, sig, out shipping);
                    label = "M " + declFull + "::" + m.Name + "|" + sig;
                }
                else
                {
                    ok = table.TryField(declFull, mr.Name, out shipping);
                    label = "F " + declFull + "::" + mr.Name;
                }
                if (!ok) { unresolved.Add(label); continue; }
                if (shipping != mr.Name) memberRenames.Add(new KeyValuePair<MemberReference, string>(mr, shipping));
            }

            // Types: the reference table plus custom-attribute Type arguments (Cecil materializes
            // those as separate objects).
            var typeRefs = new List<TypeReference>();
            var seenTypes = new HashSet<TypeReference>();
            foreach (var tr in module.GetTypeReferences())
                if (IsGameRef(tr) && seenTypes.Add(tr)) typeRefs.Add(tr);
            foreach (var tr in AttributeTypeArguments(module))
                for (var t = tr; t != null; t = t.DeclaringType)
                    if (IsGameRef(t) && seenTypes.Add(t)) typeRefs.Add(t);

            var typeRenames = new List<KeyValuePair<TypeReference, string>>();
            foreach (var tr in typeRefs)
            {
                string named = Sig.Full(tr);
                if (!table.TryType(named, out string shipping)) { unresolved.Add("T " + named); continue; }
                if (shipping != named) typeRenames.Add(new KeyValuePair<TypeReference, string>(tr, shipping));
            }

            foreach (var kv in memberRenames) { kv.Key.Name = kv.Value; result.Members++; }
            foreach (var kv in typeRenames)
            {
                TypeReference tr = kv.Key;
                string shipping = kv.Value;
                int slash = shipping.LastIndexOf('/');
                string leaf = slash >= 0 ? shipping.Substring(slash + 1) : shipping;
                if (tr.DeclaringType == null)
                {
                    int dot = leaf.LastIndexOf('.');
                    tr.Namespace = dot >= 0 ? leaf.Substring(0, dot) : "";
                    tr.Name = dot >= 0 ? leaf.Substring(dot + 1) : leaf;
                }
                else
                {
                    tr.Name = leaf;
                }
                result.Types++;
            }

            result.Unresolved.AddRange(unresolved.OrderBy(s => s, StringComparer.Ordinal));
            return result;
        }

        /// <summary>A reference into the game assembly: walk a nested reference out to the declarer
        /// whose scope names the assembly.</summary>
        public static bool IsGameRef(TypeReference tr)
        {
            if (tr == null) return false;
            while (tr.DeclaringType != null) tr = tr.DeclaringType;
            return tr.Scope is AssemblyNameReference anr && anr.Name == GameAssembly;
        }

        private static IEnumerable<TypeReference> AttributeTypeArguments(ModuleDefinition module)
        {
            var providers = new List<ICustomAttributeProvider> { module.Assembly, module };
            foreach (var t in module.GetTypes())
            {
                providers.Add(t);
                providers.AddRange(t.Fields);
                providers.AddRange(t.Properties);
                providers.AddRange(t.Events);
                foreach (var m in t.Methods)
                {
                    providers.Add(m);
                    providers.AddRange(m.Parameters);
                }
            }
            foreach (var p in providers)
            {
                if (p == null || !p.HasCustomAttributes) continue;
                foreach (var ca in p.CustomAttributes)
                {
                    foreach (var arg in ca.ConstructorArguments)
                        foreach (var tr in TypesIn(arg)) yield return tr;
                    foreach (var na in ca.Fields.Concat(ca.Properties))
                        foreach (var tr in TypesIn(na.Argument)) yield return tr;
                }
            }
        }

        private static IEnumerable<TypeReference> TypesIn(CustomAttributeArgument arg)
        {
            if (arg.Value is TypeReference tr) yield return tr.GetElementType();
            else if (arg.Value is CustomAttributeArgument inner)
                foreach (var t in TypesIn(inner)) yield return t;
            else if (arg.Value is CustomAttributeArgument[] arr)
                foreach (var a in arr)
                    foreach (var t in TypesIn(a)) yield return t;
        }
    }
}
