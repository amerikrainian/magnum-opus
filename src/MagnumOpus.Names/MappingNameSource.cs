using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace MagnumOpus.Names
{
    /// <summary>
    /// Names from the community mappings: a shipped name is looked up in the build's
    /// <see cref="ObfMap"/> (obfuscated -> intermediary), then in the <see cref="NamedMap"/>
    /// (intermediary -> human). The human name is the first choice, the intermediary name the
    /// fallback when the human name collides; anything unmapped keeps its shipped name (the real-named
    /// types and members, the SDL bindings, the member-less enums the mapping skips). Lookups use
    /// Cecil's own FullName strings, the community files' form, so this source must see the model
    /// unrenamed (the renamer guarantees it).
    /// </summary>
    public sealed class MappingNameSource : INameSource
    {
        private readonly ObfMap _obf;
        private readonly NamedMap _named;
        private readonly Dictionary<TypeDefinition, ObfClass> _classOf = new Dictionary<TypeDefinition, ObfClass>();
        private readonly Dictionary<ObfClass, ILookup<string, ObfMethod>> _methodsByName = new Dictionary<ObfClass, ILookup<string, ObfMethod>>();
        private readonly Dictionary<MethodDefinition, ObfMethod> _methodOf = new Dictionary<MethodDefinition, ObfMethod>();

        public MappingNameSource(ObfMap obf, NamedMap named)
        {
            _obf = obf;
            _named = named;
        }

        /// <summary>The intermediary name a type was given (enum constants re-added by the named
        /// layer are keyed by it), or null.</summary>
        public string InterOf(TypeDefinition type) => Class(type)?.Inter;

        private ObfClass Class(TypeDefinition t)
        {
            if (!_classOf.TryGetValue(t, out var c))
                _classOf[t] = c = _obf.Find(Sig.Full(t));
            return c;
        }

        private IList<string> Choices(string inter)
        {
            if (inter == null) return new string[0];
            string human = _named.Get(inter);
            return human != null && human != inter ? new[] { human, inter } : new[] { inter };
        }

        public IList<string> TypeNames(TypeDefinition type) => Choices(Class(type)?.Inter);

        public IList<string> FieldNames(FieldDefinition field)
        {
            var c = Class(field.DeclaringType);
            var entry = c?.Fields.FirstOrDefault(f => f.Obf == field.Name);
            return Choices(entry?.Inter);
        }

        private ObfMethod Method(MethodDefinition m)
        {
            if (_methodOf.TryGetValue(m, out var hit)) return hit;
            var c = Class(m.DeclaringType);
            if (c != null)
            {
                if (!_methodsByName.TryGetValue(c, out var byName))
                    _methodsByName[c] = byName = c.Methods.ToLookup(x => x.Obf, StringComparer.Ordinal);
                string ret = Sig.CecilFull(m.ReturnType);
                string[] args = Sig.CecilArgs(m);
                hit = byName[m.Name].FirstOrDefault(x => x.Ret == ret && x.Args.SequenceEqual(args));
            }
            _methodOf[m] = hit;
            return hit;
        }

        public IList<string> MethodNames(MethodDefinition method) => Choices(Method(method)?.Inter);

        public string ParamName(MethodDefinition method, int index)
        {
            var entry = Method(method);
            if (entry == null || index >= entry.ParamInters.Length) return null;
            string inter = entry.ParamInters[index];
            return inter == null ? null : _named.Get(inter) ?? inter;
        }
    }
}
