using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace MagnumOpus.Names
{
    /// <summary>Where new names come from. Every call sees the model's ORIGINAL names: the renamer
    /// asks for all candidates before it changes anything.</summary>
    public interface INameSource
    {
        /// <summary>New simple names in preference order (empty: keep the original). When a choice
        /// collides in its scope, the next one is tried; past the last, the original name is kept.</summary>
        IList<string> TypeNames(TypeDefinition type);
        IList<string> FieldNames(FieldDefinition field);
        IList<string> MethodNames(MethodDefinition method);
        /// <summary>A parameter's new name, or null to keep it.</summary>
        string ParamName(MethodDefinition method, int index);
    }

    /// <summary>What a rename did: for every type and member of the module, its name before.</summary>
    public sealed class RenameLog
    {
        public readonly Dictionary<TypeDefinition, string> OldTypeFull = new Dictionary<TypeDefinition, string>();
        public readonly Dictionary<IMemberDefinition, string> OldMemberName = new Dictionary<IMemberDefinition, string>();
        public int TypesRenamed, FieldsRenamed, MethodsRenamed, ParamsRenamed, Collisions;
    }

    /// <summary>
    /// Renames a module's types and members in place from an <see cref="INameSource"/>, keeping every
    /// name unique in its scope: types per namespace (top level) or per declaring type (nested),
    /// fields per type by name, methods per type by name and signature (computed after the type
    /// renames). Collisions are resolved symmetrically: every item of a colliding group moves to its
    /// next candidate, so the outcome never depends on member order. A type that ends in a generic
    /// arity suffix ("`1") keeps it.
    /// </summary>
    public static class Renamer
    {
        public static RenameLog Apply(ModuleDefinition module, INameSource source)
        {
            var log = new RenameLog();
            var types = module.GetTypes().ToList();

            // Snapshot the originals and ask for every candidate before mutating anything.
            foreach (var t in types)
            {
                log.OldTypeFull[t] = Sig.Full(t);
                foreach (var f in t.Fields) log.OldMemberName[f] = f.Name;
                foreach (var m in t.Methods) log.OldMemberName[m] = m.Name;
            }
            var typeItems = types.Select(t => new Item(t, t.Name, WithArity(source.TypeNames(t), t.Name))).ToList();
            var fieldItems = new List<Item>();
            var methodItems = new List<Item>();
            var paramNames = new List<KeyValuePair<ParameterDefinition, string>>();
            foreach (var t in types)
            {
                foreach (var f in t.Fields) fieldItems.Add(new Item(f, f.Name, source.FieldNames(f)));
                foreach (var m in t.Methods)
                {
                    methodItems.Add(new Item(m, m.Name, source.MethodNames(m)));
                    for (int i = 0; i < m.Parameters.Count; i++)
                    {
                        string p = source.ParamName(m, i);
                        if (p != null) paramNames.Add(new KeyValuePair<ParameterDefinition, string>(m.Parameters[i], p));
                    }
                }
            }

            // The module's own member REFERENCES (members of its generic types instantiated, such as
            // Maybe<Puzzle>::GetValue) carry their own copy of the name: resolve them now, while the
            // definitions still have the names they were written against, and copy the new names
            // over once the definitions are renamed.
            var internalRefs = InternalMemberReferences(module);

            // Types first: method signatures are keyed by the new type names.
            log.Collisions += Resolve(typeItems, it => ScopeOf((TypeDefinition)it.Member), it => it.Current);
            foreach (var it in typeItems)
            {
                var t = (TypeDefinition)it.Member;
                if (it.Current == t.Name) continue;
                if (t.DeclaringType == null && it.Current.Contains("."))
                {
                    // A dotted name is a namespace.name pair.
                    int cut = it.Current.LastIndexOf('.');
                    t.Namespace = it.Current.Substring(0, cut);
                    t.Name = it.Current.Substring(cut + 1);
                }
                else
                {
                    t.Name = it.Current;
                }
                log.TypesRenamed++;
            }

            log.Collisions += Resolve(fieldItems, it => ((FieldDefinition)it.Member).DeclaringType, it => it.Current);
            foreach (var it in fieldItems)
            {
                var f = (FieldDefinition)it.Member;
                if (it.Current != f.Name) { f.Name = it.Current; log.FieldsRenamed++; }
            }

            // Signatures do not depend on method names, so compute them once.
            var sigs = methodItems.ToDictionary(it => it, it => Sig.Method((MethodDefinition)it.Member));
            log.Collisions += Resolve(methodItems, it => ((MethodDefinition)it.Member).DeclaringType, it => it.Current + "|" + sigs[it]);
            foreach (var it in methodItems)
            {
                var m = (MethodDefinition)it.Member;
                if (it.Current != m.Name) { m.Name = it.Current; log.MethodsRenamed++; }
            }

            foreach (var kv in paramNames)
            {
                if (kv.Key.Name != kv.Value) { kv.Key.Name = kv.Value; log.ParamsRenamed++; }
            }

            foreach (var kv in internalRefs)
                if (kv.Key.Name != kv.Value.Name) kv.Key.Name = kv.Value.Name;
            return log;
        }

        private static List<KeyValuePair<MemberReference, IMemberDefinition>> InternalMemberReferences(ModuleDefinition module)
        {
            var refs = new List<KeyValuePair<MemberReference, IMemberDefinition>>();
            var seen = new HashSet<MemberReference>();
            void Add(MemberReference mr)
            {
                if (mr is GenericInstanceMethod gim) mr = gim.ElementMethod;
                if (mr == null || mr is IMemberDefinition || !seen.Add(mr)) return;
                if (!(mr.DeclaringType?.GetElementType() is TypeDefinition td) || td.Module != module) return;
                IMemberDefinition def = null;
                try
                {
                    if (mr is MethodReference m) def = m.Resolve();
                    else if (mr is FieldReference f) def = f.Resolve();
                }
                catch (AssemblyResolutionException) { }
                if (def != null) refs.Add(new KeyValuePair<MemberReference, IMemberDefinition>(mr, def));
            }
            foreach (var mr in module.GetMemberReferences()) Add(mr);
            foreach (var t in module.GetTypes())
                foreach (var m in t.Methods)
                    if (m.HasBody)
                        foreach (var ins in m.Body.Instructions)
                            if (ins.Operand is MemberReference mr) Add(mr);
            return refs;
        }

        // The scope a top-level type's name must be unique in is its namespace; a nested type's, its
        // declaring type. Boxed into one object key.
        private static object ScopeOf(TypeDefinition t) => (object)t.DeclaringType ?? ("ns:" + t.Namespace);

        private static IList<string> WithArity(IList<string> candidates, string original)
        {
            if (candidates == null || candidates.Count == 0) return candidates;
            int tick = original.LastIndexOf('`');
            if (tick < 0) return candidates;
            string suffix = original.Substring(tick);
            return candidates.Select(c => c.EndsWith(suffix, StringComparison.Ordinal) ? c : c + suffix).ToList();
        }

        private sealed class Item
        {
            public readonly IMemberDefinition Member;
            public readonly string Original;
            private readonly IList<string> _choices;
            private int _index;

            public Item(IMemberDefinition member, string original, IList<string> choices)
            {
                Member = member;
                Original = original;
                _choices = choices ?? new string[0];
            }

            public string Current => _index < _choices.Count ? _choices[_index] : Original;

            /// <summary>Move to the next choice; false when already at the original.</summary>
            public bool Advance()
            {
                if (_index >= _choices.Count) return false;
                _index++;
                return true;
            }
        }

        // Groups by (scope, key); every member of a group larger than one advances, until no group
        // collides or nothing can move. Returns how many advances happened.
        private static int Resolve(List<Item> items, Func<Item, object> scope, Func<Item, string> key)
        {
            int advances = 0;
            while (true)
            {
                bool moved = false;
                foreach (var group in items.GroupBy(it => new ScopeKey(scope(it), key(it))))
                {
                    if (group.Skip(1).Any())
                        foreach (var it in group)
                            if (it.Advance()) { moved = true; advances++; }
                }
                if (!moved) return advances;
            }
        }

        private struct ScopeKey : IEquatable<ScopeKey>
        {
            private readonly object _scope;
            private readonly string _key;

            public ScopeKey(object scope, string key)
            {
                _scope = scope;
                _key = key;
            }

            public bool Equals(ScopeKey other) => Equals(_scope, other._scope) && _key == other._key;
            public override bool Equals(object obj) => obj is ScopeKey k && Equals(k);
            public override int GetHashCode() => ((_scope?.GetHashCode() ?? 0) * 397) ^ (_key?.GetHashCode() ?? 0);
        }
    }
}
