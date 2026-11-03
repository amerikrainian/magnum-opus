using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace MagnumOpus.Names
{
    /// <summary>
    /// The shape of a known build, in HUMAN names: every type (kind, base, generic arity, nesting by
    /// name) and every field and method (signature; for methods also the set of external methods the
    /// body calls, hashed, and the IL size). Derived metadata only, no code. Shipped with the mod
    /// (MagnumOpus\mappings\fingerprint.tsv) so the <see cref="Matcher"/> can name a newer build
    /// that no community mapping covers yet: the game's code barely moves between builds (about 99.6
    /// to 100 percent of members keep their position and signature, measured across the 2026 CE
    /// updates), while every obfuscated name changes.
    /// </summary>
    public sealed class Fingerprint
    {
        public Guid Mvid;
        public readonly List<FpType> Types = new List<FpType>();

        public const string Header = "# MagnumOpus fingerprint v1";

        /// <summary>Fingerprint a module that has been renamed into human names (in memory).</summary>
        public static Fingerprint FromRenamed(ModuleDefinition module)
        {
            var fp = new Fingerprint { Mvid = module.Mvid };
            foreach (var t in module.GetTypes())
            {
                var ft = new FpType
                {
                    Name = Sig.Full(t),
                    Kind = Kind(t),
                    Base = t.BaseType == null ? "" : Sig.Type(t.BaseType),
                    Arity = t.HasGenericParameters ? t.GenericParameters.Count : 0,
                };
                foreach (var f in t.Fields)
                    ft.Fields.Add(new FpField { Name = f.Name, Type = Sig.Type(f.FieldType), Static = f.IsStatic });
                foreach (var m in t.Methods)
                    ft.Methods.Add(new FpMethod { Name = m.Name, Sig = Sig.Method(m), Static = m.IsStatic, Calls = CallHash(m, module), Size = m.HasBody ? m.Body.CodeSize : 0 });
                fp.Types.Add(ft);
            }
            return fp;
        }

        public static string Kind(TypeDefinition t)
        {
            if (t.IsInterface) return "interface";
            if (t.IsEnum) return "enum";
            if (t.BaseType != null && (t.BaseType.FullName == "System.MulticastDelegate" || t.BaseType.FullName == "System.Delegate")) return "delegate";
            if (t.IsValueType) return "struct";
            return "class";
        }

        /// <summary>The external methods a body calls (call, callvirt, newobj, ldftn), as a hash of the
        /// sorted distinct "Type::Name" set: build-invariant, since external names are never
        /// obfuscated. "-" for no body, "0" for a body that calls nothing external.</summary>
        public static string CallHash(MethodDefinition m, ModuleDefinition module)
        {
            if (!m.HasBody) return "-";
            var set = new SortedSet<string>(StringComparer.Ordinal);
            foreach (Instruction ins in m.Body.Instructions)
            {
                if (!(ins.Operand is MethodReference mr)) continue;
                // In-module types are TypeDefinitions (Cecil resolves a module's own types); anything
                // else is a reference into another assembly.
                var decl = mr.DeclaringType?.GetElementType();
                if (decl == null || decl is TypeDefinition) continue;
                set.Add(decl.FullName + "::" + mr.Name);
            }
            if (set.Count == 0) return "0";
            ulong h = 14695981039346656037UL;
            foreach (var s in set)
            {
                foreach (char c in s) { h ^= c; h *= 1099511628211UL; }
                h ^= '\n'; h *= 1099511628211UL;
            }
            return h.ToString("x16");
        }

        public void Write(TextWriter w)
        {
            w.WriteLine(Header);
            w.WriteLine("mvid\t" + Mvid.ToString("D"));
            foreach (var t in Types)
            {
                w.WriteLine("T\t" + t.Name + "\t" + t.Kind + "\t" + t.Base + "\t" + t.Arity);
                foreach (var f in t.Fields) w.WriteLine("F\t" + f.Name + "\t" + f.Type + "\t" + (f.Static ? "s" : "i"));
                foreach (var m in t.Methods) w.WriteLine("M\t" + m.Name + "\t" + m.Sig + "\t" + (m.Static ? "s" : "i") + "\t" + m.Calls + "\t" + m.Size);
            }
        }

        public void WriteFile(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            using (var w = new StreamWriter(path, false, new UTF8Encoding(false))) { w.NewLine = "\n"; Write(w); }
        }

        public static Fingerprint Read(TextReader r)
        {
            var fp = new Fingerprint();
            FpType cur = null;
            string line;
            int n = 0;
            while ((line = r.ReadLine()) != null)
            {
                n++;
                if (line.Length == 0 || line[0] == '#') continue;
                var p = line.Split('\t');
                switch (p[0])
                {
                    case "mvid": fp.Mvid = Guid.Parse(p[1]); break;
                    case "T":
                        cur = new FpType { Name = p[1], Kind = p[2], Base = p[3], Arity = int.Parse(p[4]) };
                        fp.Types.Add(cur);
                        break;
                    case "F": cur.Fields.Add(new FpField { Name = p[1], Type = p[2], Static = p[3] == "s" }); break;
                    case "M": cur.Methods.Add(new FpMethod { Name = p[1], Sig = p[2], Static = p[3] == "s", Calls = p[4], Size = int.Parse(p[5]) }); break;
                    default: throw new FormatException("fingerprint line " + n + ": unknown record '" + p[0] + "'.");
                }
            }
            return fp;
        }

        public static Fingerprint ReadFile(string path)
        {
            using (var r = new StreamReader(path, Encoding.UTF8)) return Read(r);
        }
    }

    public sealed class FpType
    {
        public string Name, Kind, Base;
        public int Arity;
        public readonly List<FpField> Fields = new List<FpField>();
        public readonly List<FpMethod> Methods = new List<FpMethod>();
        public string Parent => Name.LastIndexOf('/') is int i && i >= 0 ? Name.Substring(0, i) : null;
        public string Leaf => Name.Substring(Name.LastIndexOf('/') + 1);
    }

    public sealed class FpField
    {
        public string Name, Type;
        public bool Static;
    }

    public sealed class FpMethod
    {
        public string Name, Sig, Calls;
        public bool Static;
        public int Size;
    }
}
