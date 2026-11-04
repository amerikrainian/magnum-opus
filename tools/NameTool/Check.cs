using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MagnumOpus.Names;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace MagnumOpus.NameTool
{
    /// <summary>
    /// fingerprint: the shipped fingerprint of a known build (its exe renamed with the mappings).
    /// check: how the fingerprint matcher names a (new) build, and how much to trust it:
    ///   - with a community mapping for that build in mappings/obf: agreement, item by item;
    ///   - with --old (the known build's exe, mapped): method bodies of matched pairs compared
    ///     (opcodes and member operands in human names; integer operands such as the obfuscator's
    ///     string keys ignored): an identical body is strong evidence of a right match;
    ///   - with --module: the module remapped onto the matched names, unresolved references listed.
    /// </summary>
    internal static class Check
    {
        public static int Fingerprint(string[] args)
        {
            string exe = Program.Opt(args, "--exe") ?? throw new ArgumentException("--exe is required");
            string mappings = Program.Opt(args, "--mappings", "mappings");
            string output = Program.Opt(args, "--out") ?? throw new ArgumentException("--out is required");
            var inputs = new List<string> { exe, typeof(Check).Assembly.Location, typeof(GameNames).Assembly.Location };
            inputs.AddRange(Directory.GetFiles(mappings, "*", SearchOption.AllDirectories));
            if (File.Exists(output) && File.GetLastWriteTimeUtc(output) > inputs.Max(File.GetLastWriteTimeUtc))
            {
                Console.WriteLine("fingerprint: up to date (" + output + ")");
                return 0;
            }
            using var m = GameNames.ReadModule(exe);
            var obf = Program.LoadObfFor(mappings, m.Mvid);
            GameNames.ApplyMappings(m, obf, NamedMap.LoadDirectory(Path.Combine(mappings, "named")), out _);
            var fp = MagnumOpus.Names.Fingerprint.FromRenamed(m);
            fp.WriteFile(output);
            Console.WriteLine($"fingerprint: build {m.Mvid}, {fp.Types.Count} types -> {output}");
            return 0;
        }

        public static int Run(string[] args)
        {
            string exe = Program.Opt(args, "--exe") ?? throw new ArgumentException("--exe is required");
            string fpPath = Program.Opt(args, "--fingerprint") ?? throw new ArgumentException("--fingerprint is required");
            string mappings = Program.Opt(args, "--mappings", "mappings");
            string old = Program.Opt(args, "--old");
            string moduleDll = Program.Opt(args, "--module");
            var named = NamedMap.LoadDirectory(Path.Combine(mappings, "named"));
            var fp = MagnumOpus.Names.Fingerprint.ReadFile(fpPath);

            using var fresh = GameNames.ReadModule(exe);
            Console.WriteLine($"build {fresh.Mvid}, fingerprint of build {fp.Mvid}");
            var types = fresh.GetTypes().ToList();
            var matcher = new Matcher(fresh, fp);
            Console.WriteLine("matcher: " + matcher.Stats);

            // Record what the matcher would name each item (by metadata position), before renaming.
            var matched = Names(types, matcher);

            // Where the unbound members cluster.
            var gaps = types
                .Select(t => new { t, k = matcher.BoundTo(t), un = t.Methods.Count(m => matcher.MethodNames(m).Count == 0) + t.Fields.Count(f => matcher.FieldNames(f).Count == 0) })
                .Where(x => x.un > 0).OrderByDescending(x => x.un).ToList();
            Console.WriteLine($"unbound members in {gaps.Count} types; by kind: "
                + string.Join(", ", gaps.GroupBy(x => MagnumOpus.Names.Fingerprint.Kind(x.t)).Select(g => g.Key + " " + g.Sum(x => x.un))));
            foreach (var x in gaps.Take(12))
                Console.WriteLine($"  {x.un,4} unbound in {(x.k?.Name ?? "(unbound type) " + x.t.FullName)} ({x.t.Methods.Count} methods, {x.t.Fields.Count} fields; known {x.k?.Methods.Count} / {x.k?.Fields.Count})");

            // The members the host itself needs, with the shipping names they resolved to.
            using (var probe = GameNames.ReadModule(exe))
            {
                var probeTable = NameTable.FromRenamed(probe, Renamer.Apply(probe, new Matcher(probe, fp)));
                foreach (var name in new[] { "GameInit", "GameLoop", "GetCurrentScreen" })
                    foreach (var o in probeTable.Methods("GameLogic", name))
                        Console.WriteLine($"  GameLogic::{name} -> {o.Value}");
                Console.WriteLine("  GameLogic::instance -> " + (probeTable.TryField("GameLogic", "instance", out var inst) ? inst : "(unbound)"));
            }

            // 1. Against the community mapping, when there is one.
            string obfPath = Path.Combine(mappings, "obf", fresh.Mvid.ToString("D") + ".tsv");
            if (File.Exists(obfPath))
            {
                // Final names on both sides (after the renamer's collision fallbacks), by position.
                using var truthModule = GameNames.ReadModule(exe);
                var truthLog = Renamer.Apply(truthModule, new MappingNameSource(ObfMap.ReadTsvFile(obfPath), named));
                using var matchedModule = GameNames.ReadModule(exe);
                var matchedLog = Renamer.Apply(matchedModule, new Matcher(matchedModule, fp));
                Compare(Renamed(matchedModule, matchedLog), Renamed(truthModule, truthLog));
            }
            else
            {
                Console.WriteLine("no community mapping for this build in " + mappings + "/obf (agreement not measured)");
            }

            // 2. Method bodies against the old build.
            if (old != null)
            {
                using var oldModule = GameNames.ReadModule(old);
                var oldObf = Program.LoadObfFor(mappings, oldModule.Mvid);
                GameNames.ApplyMappings(oldModule, oldObf, named, out _);
                using var newModule = GameNames.ReadModule(exe);
                Renamer.Apply(newModule, new Matcher(newModule, fp));
                CompareBodies(oldModule, newModule);
            }

            // 3. The module's references.
            if (moduleDll != null)
            {
                using var tableModule = GameNames.ReadModule(exe);
                var log = Renamer.Apply(tableModule, new Matcher(tableModule, fp));
                var table = NameTable.FromRenamed(tableModule, log);
                using var mod = ModuleDefinition.ReadModule(new MemoryStream(File.ReadAllBytes(moduleDll)));
                var result = Remapper.Remap(mod, table);
                Console.WriteLine($"module: {result.Types} type and {result.Members} member references bound, {result.Unresolved.Count} unresolved");
                foreach (var u in result.Unresolved) Console.WriteLine("  unresolved: " + u);
            }
            return 0;
        }

        // Item key (kind + metadata position) -> its name after a rename, for items the rename changed.
        private static Dictionary<string, string> Renamed(ModuleDefinition module, RenameLog log)
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            var types = module.GetTypes().ToList();
            for (int i = 0; i < types.Count; i++)
            {
                var t = types[i];
                if (log.OldTypeFull[t] != Sig.Full(t)) d["T" + i] = t.Name;
                for (int f = 0; f < t.Fields.Count; f++)
                    if (log.OldMemberName[t.Fields[f]] != t.Fields[f].Name) d["T" + i + ".F" + f] = t.Fields[f].Name;
                for (int m = 0; m < t.Methods.Count; m++)
                    if (log.OldMemberName[t.Methods[m]] != t.Methods[m].Name) d["T" + i + ".M" + m] = t.Methods[m].Name;
            }
            return d;
        }

        // Item key (kind + metadata position) -> the first-choice name a source gives it.
        private static Dictionary<string, string> Names(List<TypeDefinition> types, INameSource source)
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < types.Count; i++)
            {
                var t = types[i];
                var tn = source.TypeNames(t);
                if (tn.Count > 0) d["T" + i] = tn[0];
                for (int f = 0; f < t.Fields.Count; f++)
                {
                    var n = source.FieldNames(t.Fields[f]);
                    if (n.Count > 0) d["T" + i + ".F" + f] = n[0];
                }
                for (int m = 0; m < t.Methods.Count; m++)
                {
                    var n = source.MethodNames(t.Methods[m]);
                    if (n.Count > 0) d["T" + i + ".M" + m] = n[0];
                }
            }
            return d;
        }

        private static void Compare(Dictionary<string, string> matched, Dictionary<string, string> truth)
        {
            foreach (var kind in new[] { "type", "field", "method" })
            {
                bool Is(string key) => kind == "type" ? !key.Contains('.') : key.Contains(kind == "field" ? ".F" : ".M");
                var keys = truth.Keys.Where(Is).ToList();
                int named = keys.Count(k => matched.ContainsKey(k));
                int agree = keys.Count(k => matched.TryGetValue(k, out var v) && v == truth[k]);
                int extra = matched.Keys.Count(k => Is(k) && !truth.ContainsKey(k));
                Console.WriteLine($"vs community mapping, {kind}s: {keys.Count} mapped, matcher named {named}, agree {agree}, disagree {named - agree}, matcher-only {extra}");
                foreach (var k in keys.Where(k => matched.TryGetValue(k, out var v) && v != truth[k]).Take(10))
                    Console.WriteLine($"  {k}: matcher {matched[k]}, mapping {truth[k]}");
            }
        }

        private static void CompareBodies(ModuleDefinition oldNamed, ModuleDefinition newNamed)
        {
            var oldMethods = new Dictionary<string, MethodDefinition>(StringComparer.Ordinal);
            foreach (var t in oldNamed.GetTypes())
                foreach (var m in t.Methods)
                    oldMethods[Sig.Full(t) + "::" + m.Name + "|" + Sig.Method(m)] = m;
            int pairs = 0, same = 0, bodyless = 0;
            var differ = new List<string>();
            foreach (var t in newNamed.GetTypes())
                foreach (var m in t.Methods)
                {
                    string key = Sig.Full(t) + "::" + m.Name + "|" + Sig.Method(m);
                    if (!oldMethods.TryGetValue(key, out var o)) continue;
                    pairs++;
                    if (!m.HasBody && !o.HasBody) { bodyless++; same++; continue; }
                    if (Body(m) == Body(o)) same++;
                    else if (differ.Count < 15) differ.Add(key);
                }
            Console.WriteLine($"bodies vs old build: {pairs} method pairs by human name and signature, {same} identical "
                + $"({(pairs == 0 ? 0 : 100.0 * same / pairs):F1}%, {bodyless} without bodies)");
            foreach (var d in differ) Console.WriteLine("  differs: " + d);
        }

        private static string Body(MethodDefinition m)
        {
            if (!m.HasBody) return "";
            var sb = new StringBuilder();
            foreach (Instruction ins in m.Body.Instructions)
            {
                sb.Append(ins.OpCode.Name);
                switch (ins.Operand)
                {
                    case MethodReference mr: sb.Append(' ').Append(Sig.Full(mr.DeclaringType.GetElementType())).Append("::").Append(mr.Name); break;
                    case FieldReference fr: sb.Append(' ').Append(Sig.Full(fr.DeclaringType.GetElementType())).Append("::").Append(fr.Name); break;
                    case TypeReference tr: sb.Append(' ').Append(Sig.Type(tr)); break;
                    case string s: sb.Append(" \"").Append(s).Append('"'); break;
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
