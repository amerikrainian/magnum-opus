using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;

namespace MagnumOpus.Names
{
    /// <summary>What the matcher bound, for logs and the update check.</summary>
    public sealed class MatchStats
    {
        public int Types, TypesBound, TypesByOrder;
        public int Fields, FieldsBound;
        public int Methods, MethodsBound;
        public override string ToString() =>
            $"types {TypesBound}/{Types} ({TypesByOrder} by order), fields {FieldsBound}/{Fields}, methods {MethodsBound}/{Methods}";
    }

    /// <summary>
    /// Names a game build that no community mapping covers, from the <see cref="Fingerprint"/> of a
    /// known build: binds each type and member of the new (obfuscated, unrenamed) module to its
    /// counterpart in the fingerprint, then serves the fingerprint's human names as an
    /// <see cref="INameSource"/>. Metadata only, deterministic:
    ///  1. types whose shipped name is real (not obfuscated) bind by name;
    ///  2. rounds of structural signatures (kind, arity, base, field types, method signatures with
    ///     their external-call hashes), written with the CURRENT bindings, unbound game types as "?":
    ///     a signature unique on both sides binds; each binding sharpens the next round;
    ///  3. groups of equal size on both sides pair in metadata order (the obfuscator keeps order);
    ///  4. looser signatures, then the same again, until nothing moves.
    /// Top-level types match globally; a nested type only among the children of its bound parent's
    /// counterpart. Members then bind per bound type pair: elementwise when the lists agree, else by
    /// unique key, then by order within equal groups. What stays unbound keeps its shipping name, and
    /// the module references to it surface as unresolved.
    /// </summary>
    public sealed class Matcher : INameSource
    {
        private readonly ModuleDefinition _module;
        private readonly Fingerprint _fp;
        private readonly List<TypeDefinition> _newTypes;
        private readonly Dictionary<TypeDefinition, int> _newOrder = new Dictionary<TypeDefinition, int>();
        private readonly Dictionary<FpType, int> _knownOrder = new Dictionary<FpType, int>();
        private readonly Dictionary<string, FpType> _knownByName = new Dictionary<string, FpType>(StringComparer.Ordinal);
        private readonly Dictionary<TypeDefinition, FpType> _bind = new Dictionary<TypeDefinition, FpType>();
        private readonly Dictionary<FpType, TypeDefinition> _bindBack = new Dictionary<FpType, TypeDefinition>();
        private readonly Dictionary<FieldDefinition, string> _fieldNames = new Dictionary<FieldDefinition, string>();
        private readonly Dictionary<MethodDefinition, string> _methodNames = new Dictionary<MethodDefinition, string>();

        public MatchStats Stats { get; } = new MatchStats();

        public Matcher(ModuleDefinition module, Fingerprint fp)
        {
            _module = module;
            _fp = fp;
            _newTypes = module.GetTypes().ToList();
            for (int i = 0; i < _newTypes.Count; i++) _newOrder[_newTypes[i]] = i;
            for (int i = 0; i < fp.Types.Count; i++)
            {
                _knownOrder[fp.Types[i]] = i;
                _knownByName[fp.Types[i].Name] = fp.Types[i];
            }
            Run();
        }

        /// <summary>A shipped name the obfuscator made up: Eazfuscator's "#=q..." names, and the junk
        /// types named with whitespace and zero-width characters.</summary>
        public static bool IsObfuscated(string name)
        {
            if (name.StartsWith("#=", StringComparison.Ordinal)) return true;
            foreach (char c in name)
                if (!char.IsWhiteSpace(c) && c != '​' && c != '‌' && c != '‍' && c != '﻿') return false;
            return true;
        }

        // ---- the bindings, as names ----

        public FpType BoundTo(TypeDefinition t) => _bind.TryGetValue(t, out var k) ? k : null;

        public IList<string> TypeNames(TypeDefinition type)
        {
            var k = BoundTo(type);
            if (k == null) return new string[0];
            return new[] { type.DeclaringType == null ? k.Name : k.Leaf };
        }

        public IList<string> FieldNames(FieldDefinition field) =>
            _fieldNames.TryGetValue(field, out var n) ? new[] { n } : new string[0];

        public IList<string> MethodNames(MethodDefinition method) =>
            _methodNames.TryGetValue(method, out var n) ? new[] { n } : new string[0];

        public string ParamName(MethodDefinition method, int index) => null;

        // ---- type matching ----

        private void Run()
        {
            Stats.Types = _newTypes.Count;
            BindRealNames();
            while (true)
            {
                int before = _bind.Count;
                Rounds(FullSig, byOrder: false);
                Rounds(FullSig, byOrder: true);
                if (_bind.Count == before)
                {
                    Rounds(LooseSig, byOrder: false);
                    Rounds(LooseSig, byOrder: true);
                }
                if (_bind.Count == before)
                {
                    // Members added or removed: kind, arity and base alone, unique within the pool (in
                    // practice among a bound parent's few nested types). Never paired by order.
                    Rounds(LoosestSig, byOrder: false);
                }
                if (_bind.Count == before) break;
            }
            Stats.TypesBound = _bind.Count;
            foreach (var kv in _bind) MatchMembers(kv.Key, kv.Value);
        }

        private void Bind(TypeDefinition t, FpType k)
        {
            _bind[t] = k;
            _bindBack[k] = t;
        }

        private void BindRealNames()
        {
            bool moved = true;
            while (moved)
            {
                moved = false;
                foreach (var t in _newTypes)
                {
                    if (_bind.ContainsKey(t) || IsObfuscated(t.Name)) continue;
                    string target;
                    if (t.DeclaringType == null) target = Sig.Full(t);
                    else
                    {
                        var parent = BoundTo(t.DeclaringType);
                        if (parent == null) continue;
                        target = parent.Name + "/" + t.Name;
                    }
                    if (_knownByName.TryGetValue(target, out var k) && !_bindBack.ContainsKey(k) && k.Kind == Fingerprint.Kind(t))
                    {
                        Bind(t, k);
                        moved = true;
                    }
                }
            }
        }

        // Candidate pools: top-level types together, nested types per bound parent pair.
        private IEnumerable<KeyValuePair<List<TypeDefinition>, List<FpType>>> Pools()
        {
            var pools = new Dictionary<string, KeyValuePair<List<TypeDefinition>, List<FpType>>>(StringComparer.Ordinal);
            KeyValuePair<List<TypeDefinition>, List<FpType>> Pool(string key)
            {
                if (!pools.TryGetValue(key, out var p))
                    pools[key] = p = new KeyValuePair<List<TypeDefinition>, List<FpType>>(new List<TypeDefinition>(), new List<FpType>());
                return p;
            }
            foreach (var t in _newTypes)
            {
                if (_bind.ContainsKey(t)) continue;
                if (t.DeclaringType == null) Pool("").Key.Add(t);
                else if (BoundTo(t.DeclaringType) is FpType parent) Pool(parent.Name).Key.Add(t);
            }
            foreach (var k in _fp.Types)
            {
                if (_bindBack.ContainsKey(k)) continue;
                string parent = k.Parent;
                if (parent == null) Pool("").Value.Add(k);
                else if (_knownByName.TryGetValue(parent, out var pk) && _bindBack.ContainsKey(pk)) Pool(parent).Value.Add(k);
            }
            return pools.Values;
        }

        private void Rounds(Func<object, string> sig, bool byOrder)
        {
            while (true)
            {
                int before = _bind.Count;
                foreach (var pool in Pools().ToList())
                {
                    if (pool.Key.Count == 0 || pool.Value.Count == 0) continue;
                    var newBySig = pool.Key.GroupBy(t => sig(t)).ToDictionary(g => g.Key, g => g.ToList());
                    var knownBySig = pool.Value.GroupBy(k => sig(k)).ToDictionary(g => g.Key, g => g.ToList());
                    foreach (var kv in newBySig)
                    {
                        if (!knownBySig.TryGetValue(kv.Key, out var ks)) continue;
                        var ns = kv.Value;
                        if (ns.Count == 1 && ks.Count == 1) Bind(ns[0], ks[0]);
                        else if (byOrder && ns.Count == ks.Count)
                        {
                            var no = ns.OrderBy(t => _newOrder[t]).ToList();
                            var ko = ks.OrderBy(k => _knownOrder[k]).ToList();
                            for (int i = 0; i < no.Count; i++) { Bind(no[i], ko[i]); Stats.TypesByOrder++; }
                        }
                    }
                }
                if (_bind.Count == before) return;
                if (byOrder) return; // one ordered pass, then let the exact rounds use what it bound
            }
        }

        private string FullSig(object o) => o is TypeDefinition t ? NewFull(t) : KnownFull((FpType)o);
        private string LooseSig(object o) => o is TypeDefinition t ? NewLoose(t) : KnownLoose((FpType)o);

        private string LoosestSig(object o) => o is TypeDefinition t
            ? Fingerprint.Kind(t) + "|" + (t.HasGenericParameters ? t.GenericParameters.Count : 0) + "|" + (t.BaseType == null ? "" : NewType(t.BaseType))
            : ((FpType)o).Kind + "|" + ((FpType)o).Arity + "|" + KnownType(((FpType)o).Base);

        private string NewLoose(TypeDefinition t) =>
            Fingerprint.Kind(t) + "|" + (t.HasGenericParameters ? t.GenericParameters.Count : 0) + "|" + (t.BaseType == null ? "" : NewType(t.BaseType))
            + "|" + t.Fields.Count + "|" + t.Methods.Count;

        private string KnownLoose(FpType k) =>
            k.Kind + "|" + k.Arity + "|" + KnownType(k.Base) + "|" + k.Fields.Count + "|" + k.Methods.Count;

        private string NewFull(TypeDefinition t)
        {
            var sb = new StringBuilder(NewLoose(t));
            sb.Append("|F:");
            foreach (var s in t.Fields.Select(f => (f.IsStatic ? "s" : "i") + NewType(f.FieldType)).OrderBy(s => s, StringComparer.Ordinal)) sb.Append(s).Append(';');
            sb.Append("|M:");
            foreach (var s in t.Methods.Select(NewMethodKey).OrderBy(s => s, StringComparer.Ordinal)) sb.Append(s).Append(';');
            return sb.ToString();
        }

        private string KnownFull(FpType k)
        {
            var sb = new StringBuilder(KnownLoose(k));
            sb.Append("|F:");
            foreach (var s in k.Fields.Select(f => (f.Static ? "s" : "i") + KnownType(f.Type)).OrderBy(s => s, StringComparer.Ordinal)) sb.Append(s).Append(';');
            sb.Append("|M:");
            foreach (var s in k.Methods.Select(KnownMethodKey).OrderBy(s => s, StringComparer.Ordinal)) sb.Append(s).Append(';');
            return sb.ToString();
        }

        private string NewMethodKey(MethodDefinition m) =>
            (m.IsConstructor ? "c" : m.IsStatic ? "s" : "i") + NewMethodSig(m) + "#" + Fingerprint.CallHash(m, _module);

        private string KnownMethodKey(FpMethod m) =>
            (m.Name == ".ctor" || m.Name == ".cctor" ? "c" : m.Static ? "s" : "i") + KnownType(m.Sig) + "#" + m.Calls;

        private string NewMethodSig(MethodDefinition m)
        {
            var sb = new StringBuilder(NewType(m.ReturnType)).Append('(');
            for (int i = 0; i < m.Parameters.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(NewType(m.Parameters[i].ParameterType));
            }
            return sb.Append(")`").Append(m.HasGenericParameters ? m.GenericParameters.Count : 0).ToString();
        }

        // A type in the new module, written as the fingerprint writes it: game types by their bound
        // human name, unbound game types as "?", external types by name, generic parameters by position.
        private string NewType(TypeReference t)
        {
            switch (t)
            {
                case GenericParameter gp: return (gp.Type == GenericParameterType.Method ? "!!" : "!") + gp.Position;
                case GenericInstanceType git: return NewType(git.ElementType) + "<" + string.Join(",", git.GenericArguments.Select(NewType)) + ">";
                case ArrayType at: return NewType(at.ElementType) + "[" + new string(',', at.Rank - 1) + "]";
                case ByReferenceType br: return NewType(br.ElementType) + "&";
                case PointerType pt: return NewType(pt.ElementType) + "*";
                case RequiredModifierType rm: return NewType(rm.ElementType);
                case OptionalModifierType om: return NewType(om.ElementType);
                case PinnedType pin: return NewType(pin.ElementType);
                case SentinelType st: return NewType(st.ElementType);
                case FunctionPointerType _: return t.FullName;
                case TypeDefinition td: return BoundTo(td)?.Name ?? "?";
                default: return Sig.Full(t);
            }
        }

        // A fingerprint type string with every game type not yet bound replaced by "?". The format is
        // Sig's: names separated by < > , ( ) [ ] & * and the arity suffix after ")".
        private readonly Dictionary<string, string> _knownCache = new Dictionary<string, string>(StringComparer.Ordinal);
        private int _knownCacheAt = -1;

        private string KnownType(string s)
        {
            if (_knownCacheAt != _bind.Count) { _knownCache.Clear(); _knownCacheAt = _bind.Count; }
            if (_knownCache.TryGetValue(s, out var hit)) return hit;
            var sb = new StringBuilder(s.Length);
            int start = 0;
            for (int i = 0; i <= s.Length; i++)
            {
                if (i < s.Length && "<>,()[]&*".IndexOf(s[i]) < 0) continue;
                if (i > start)
                {
                    string token = s.Substring(start, i - start);
                    sb.Append(_knownByName.TryGetValue(token, out var k) && !_bindBack.ContainsKey(k) ? "?" : token);
                }
                if (i < s.Length) sb.Append(s[i]);
                start = i + 1;
            }
            return _knownCache[s] = sb.ToString();
        }

        // ---- member matching ----

        private void MatchMembers(TypeDefinition t, FpType k)
        {
            Stats.Fields += t.Fields.Count;
            Stats.Methods += t.Methods.Count;

            var fields = Pair(t.Fields.ToList(), k.Fields,
                f => (f.IsStatic ? "s" : "i") + NewType(f.FieldType), f => (f.Static ? "s" : "i") + KnownType(f.Type),
                f => f.Name, f => f.Name, null, null);
            foreach (var kv in fields) _fieldNames[kv.Key] = kv.Value.Name;
            Stats.FieldsBound += fields.Count;

            var methods = Pair(t.Methods.ToList(), k.Methods,
                NewMethodKey, KnownMethodKey,
                m => m.Name, m => m.Name,
                m => (m.IsConstructor ? "c" : m.IsStatic ? "s" : "i") + NewMethodSig(m),
                m => (m.Name == ".ctor" || m.Name == ".cctor" ? "c" : m.Static ? "s" : "i") + KnownType(m.Sig));
            foreach (var kv in methods) _methodNames[kv.Key] = kv.Value.Name;
            Stats.MethodsBound += methods.Count;
        }

        // Pair two ordered member lists: elementwise when every key agrees; else real names first,
        // then unique keys, then equal groups by order; then the same on the secondary key.
        private static Dictionary<TN, TK> Pair<TN, TK>(List<TN> news, List<TK> knowns,
            Func<TN, string> newKey, Func<TK, string> knownKey,
            Func<TN, string> newName, Func<TK, string> knownName,
            Func<TN, string> newKey2, Func<TK, string> knownKey2)
        {
            var result = new Dictionary<TN, TK>();
            if (news.Count == knowns.Count && news.Select(newKey).SequenceEqual(knowns.Select(knownKey)))
            {
                for (int i = 0; i < news.Count; i++) result[news[i]] = knowns[i];
                return result;
            }
            var usedKnown = new HashSet<TK>();

            // Real (unobfuscated) names: the same member in both builds. With the same key first (an
            // overload set shares the name), then by name alone where the name is unique on both sides
            // (a signature can differ only through a game type not bound yet).
            foreach (var n in news)
            {
                string name = newName(n);
                if (IsObfuscated(name)) continue;
                var hit = knowns.Where(k => !usedKnown.Contains(k) && knownName(k) == name && knownKey(k) == newKey(n)).ToList();
                if (hit.Count == 1) { result[n] = hit[0]; usedKnown.Add(hit[0]); }
            }
            foreach (var n in news)
            {
                if (result.ContainsKey(n)) continue;
                string name = newName(n);
                if (IsObfuscated(name) || news.Count(x => newName(x) == name) != 1) continue;
                var hit = knowns.Where(k => !usedKnown.Contains(k) && knownName(k) == name).ToList();
                if (hit.Count == 1) { result[n] = hit[0]; usedKnown.Add(hit[0]); }
            }

            void ByKey(Func<TN, string> nk, Func<TK, string> kk)
            {
                var ng = news.Where(n => !result.ContainsKey(n)).GroupBy(nk).ToDictionary(g => g.Key, g => g.ToList());
                var kg = knowns.Where(k => !usedKnown.Contains(k)).GroupBy(kk).ToDictionary(g => g.Key, g => g.ToList());
                foreach (var kv in ng)
                {
                    if (!kg.TryGetValue(kv.Key, out var ks) || ks.Count != kv.Value.Count) continue;
                    // Unique, or equal groups paired in order (both lists are in metadata order).
                    for (int i = 0; i < ks.Count; i++) { result[kv.Value[i]] = ks[i]; usedKnown.Add(ks[i]); }
                }
            }
            ByKey(newKey, knownKey);
            if (newKey2 != null) ByKey(newKey2, knownKey2);

            // What is left, aligned in order: the longest common subsequence of the remaining keys
            // (secondary key when there is one). Survives members inserted or removed between builds,
            // which defeats the elementwise and unique-key steps for everything around them.
            var restNew = news.Where(n => !result.ContainsKey(n)).ToList();
            var restKnown = knowns.Where(k => !usedKnown.Contains(k)).ToList();
            if (restNew.Count > 0 && restKnown.Count > 0)
            {
                var nk = restNew.Select(newKey2 ?? newKey).ToArray();
                var kk = restKnown.Select(knownKey2 ?? knownKey).ToArray();
                foreach (var (i, j) in Lcs(nk, kk))
                {
                    result[restNew[i]] = restKnown[j];
                    usedKnown.Add(restKnown[j]);
                }
            }
            return result;
        }

        private static List<(int, int)> Lcs(string[] a, string[] b)
        {
            var len = new int[a.Length + 1, b.Length + 1];
            for (int i = a.Length - 1; i >= 0; i--)
                for (int j = b.Length - 1; j >= 0; j--)
                    len[i, j] = a[i] == b[j] ? len[i + 1, j + 1] + 1 : Math.Max(len[i + 1, j], len[i, j + 1]);
            var pairs = new List<(int, int)>();
            int x = 0, y = 0;
            while (x < a.Length && y < b.Length)
            {
                if (a[x] == b[y]) { pairs.Add((x, y)); x++; y++; }
                else if (len[x + 1, y] >= len[x, y + 1]) x++;
                else y++;
            }
            return pairs;
        }
    }
}
