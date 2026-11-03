using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace MagnumOpus.Names
{
    /// <summary>
    /// The reverse of a rename: for every type, field and method of the renamed model, its new name
    /// -> its name before (the shipping name). Keys use <see cref="Sig"/> over the NEW names, which is
    /// exactly how a module compiled against the renamed reference spells its references, so the
    /// remap is a dictionary lookup. Built at runtime from the running exe (renamed in memory, never
    /// written) and at build time for the checks.
    /// </summary>
    public sealed class NameTable
    {
        private readonly Dictionary<string, string> _types = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _fields = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _methods = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<KeyValuePair<string, string>>> _methodsByName =
            new Dictionary<string, List<KeyValuePair<string, string>>>(StringComparer.Ordinal);

        public Guid Mvid { get; private set; }
        public int TypeCount => _types.Count;

        public static NameTable FromRenamed(ModuleDefinition renamed, RenameLog log)
        {
            var table = new NameTable { Mvid = renamed.Mvid };
            foreach (var t in renamed.GetTypes())
            {
                string full = Sig.Full(t);
                table._types[full] = log.OldTypeFull.TryGetValue(t, out var oldFull) ? oldFull : full;
                foreach (var f in t.Fields)
                    table._fields[full + "::" + f.Name] = Old(log, f);
                foreach (var m in t.Methods)
                {
                    string sig = Sig.Method(m);
                    string old = Old(log, m);
                    table._methods[MethodKey(full, m.Name, sig)] = old;
                    string byName = full + "::" + m.Name;
                    if (!table._methodsByName.TryGetValue(byName, out var list))
                        table._methodsByName[byName] = list = new List<KeyValuePair<string, string>>();
                    list.Add(new KeyValuePair<string, string>(sig, old));
                }
            }
            return table;
        }

        private static string Old(RenameLog log, IMemberDefinition m) =>
            log.OldMemberName.TryGetValue(m, out var old) ? old : m.Name;

        internal static string MethodKey(string declFull, string name, string sig) => declFull + "::" + name + "|" + sig;

        /// <summary>Shipping full name (Outer/Inner) of a type by its new full name.</summary>
        public bool TryType(string namedFull, out string shippingFull) => _types.TryGetValue(namedFull, out shippingFull);

        public bool TryField(string namedDeclFull, string namedName, out string shippingName) =>
            _fields.TryGetValue(namedDeclFull + "::" + namedName, out shippingName);

        public bool TryMethod(string namedDeclFull, string namedName, string namedSig, out string shippingName) =>
            _methods.TryGetValue(MethodKey(namedDeclFull, namedName, namedSig), out shippingName);

        /// <summary>Every overload of a method by new name: (new signature, shipping name).</summary>
        public IList<KeyValuePair<string, string>> Methods(string namedDeclFull, string namedName) =>
            _methodsByName.TryGetValue(namedDeclFull + "::" + namedName, out var list)
                ? (IList<KeyValuePair<string, string>>)list
                : new KeyValuePair<string, string>[0];

        /// <summary>Reflection's form of a shipping type name: nested types joined with '+'.</summary>
        public static string ReflectionName(string shippingFull) => shippingFull.Replace('/', '+');
    }
}
