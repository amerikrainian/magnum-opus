using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MagnumOpus.Names;
using Mono.Cecil;

namespace MagnumOpus.NameTool
{
    /// <summary>
    /// Verbs:
    ///   import  &lt;om_mappings dir&gt; [--out mappings] [--source TEXT]
    ///       Every intermediary JSON becomes mappings/obf/&lt;mvid&gt;.tsv; named/**/*.txt is copied to
    ///       mappings/named/. --source is recorded in mappings/SOURCE.txt (the om_mappings commit).
    ///   mvid &lt;exe&gt;   print a build's module version id (the key every mapping is filed under)
    ///   refgen  --exe game/Lightning.orig.exe [--mappings mappings] --out game/Lightning.named.ref.exe
    ///           [--analysis game/Lightning.named.exe]
    ///       The compile reference: the exe renamed into human names, enum constants re-added, every
    ///       member public. --analysis also writes the renamed exe unpublicized, for de4dot and the
    ///       decompile. Skips work when the outputs are newer than every input.
    /// Exit code 0 on success, 1 on any failure (the build treats it as an error).
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                if (args.Length == 0) return Usage();
                switch (args[0])
                {
                    case "import": return Import(args.Skip(1).ToArray());
                    case "refgen": return RefGen(args.Skip(1).ToArray());
                    case "mvid":
                        if (args.Length < 2) return Usage();
                        using (var m = GameNames.ReadModule(args[1])) Console.WriteLine(m.Mvid.ToString("D"));
                        return 0;
                    default: return Usage();
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("NameTool: " + ex);
                return 1;
            }
        }

        private static int Usage()
        {
            Console.Error.WriteLine("usage: NameTool import <om_mappings dir> [--out mappings] [--source TEXT]");
            Console.Error.WriteLine("       NameTool mvid <exe>");
            Console.Error.WriteLine("       NameTool refgen --exe <Lightning.exe> [--mappings mappings] --out <ref.exe> [--analysis <named.exe>]");
            return 1;
        }

        internal static string Opt(string[] args, string name, string fallback = null)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }

        private static int Import(string[] args)
        {
            if (args.Length == 0) return Usage();
            string src = args[0];
            string outDir = Opt(args, "--out", "mappings");
            string source = Opt(args, "--source", src);

            string interDir = Path.Combine(src, "intermediary");
            string namedDir = Path.Combine(src, "named");
            int maps = 0;
            // Community-edition builds only (the mod targets that branch); the "previous" branch's files
            // share the intermediary namespace and would override CE names in the flat named table.
            foreach (var json in Directory.GetFiles(interDir, "mappings_ce_*.json").OrderBy(f => f, StringComparer.Ordinal))
            {
                if (!ObfMap.TryMvidFromFileName(json, out Guid mvid))
                {
                    Console.WriteLine("skipped (no MVID in the name): " + Path.GetFileName(json));
                    continue;
                }
                var map = ObfMap.FromJsonModel(ReadJsonModel(json), mvid);
                string target = Path.Combine(outDir, "obf", mvid.ToString("D") + ".tsv");
                map.WriteTsvFile(target);
                Console.WriteLine($"obf: {Path.GetFileName(json)} -> {target} ({map.Classes.Count} classes)");
                maps++;
            }

            string namedOut = Path.Combine(outDir, "named");
            if (Directory.Exists(namedOut)) Directory.Delete(namedOut, true);
            foreach (var txt in Directory.GetFiles(namedDir, "*.txt", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(txt).Contains("previous", StringComparison.OrdinalIgnoreCase)) continue;
                string rel = Path.GetRelativePath(namedDir, txt);
                string target = Path.Combine(namedOut, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(txt, target, true);
            }
            var named = NamedMap.LoadDirectory(namedOut);
            Console.WriteLine($"named: version {named.Version}, {named.Names.Count} names, {named.EnumVariants.Count} enums with constants");

            File.WriteAllText(Path.Combine(outDir, "SOURCE.txt"),
                "Imported from QuintessentialOM/om_mappings (https://github.com/QuintessentialOM/om_mappings)\n" +
                "source: " + source + "\n" +
                "imported: " + DateTime.UtcNow.ToString("yyyy-MM-dd") + "\n" +
                "CE obf maps imported: " + maps + " (only the current build is kept in the repo), named version " + named.Version + "\n");
            return 0;
        }

        /// <summary>System.Text.Json into the neutral model ObfMap reads (dictionaries, lists, strings).</summary>
        internal static object ReadJsonModel(string path)
        {
            using var doc = JsonDocument.Parse(File.ReadAllBytes(path), new JsonDocumentOptions { MaxDepth = 256 });
            return ToModel(doc.RootElement);
        }

        private static object ToModel(JsonElement e)
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.Object:
                    var d = new Dictionary<string, object>(StringComparer.Ordinal);
                    foreach (var p in e.EnumerateObject()) d[p.Name] = ToModel(p.Value);
                    return d;
                case JsonValueKind.Array:
                    return e.EnumerateArray().Select(ToModel).ToList();
                case JsonValueKind.String: return e.GetString();
                case JsonValueKind.Number: return e.GetDouble();
                case JsonValueKind.True: return true;
                case JsonValueKind.False: return false;
                default: return null;
            }
        }

        internal static ObfMap LoadObfFor(string mappingsDir, Guid mvid)
        {
            string path = Path.Combine(mappingsDir, "obf", mvid.ToString("D") + ".tsv");
            if (!File.Exists(path))
                throw new FileNotFoundException("no obfuscation map for build " + mvid + " in " + mappingsDir + "/obf. "
                    + "Import a newer om_mappings (tools/import-mappings.ps1), or wait for the community to publish one.", path);
            return ObfMap.ReadTsvFile(path);
        }

        private static int RefGen(string[] args)
        {
            string exe = Opt(args, "--exe") ?? throw new ArgumentException("--exe is required");
            string mappings = Opt(args, "--mappings", "mappings");
            string output = Opt(args, "--out") ?? throw new ArgumentException("--out is required");
            string analysis = Opt(args, "--analysis");

            var inputs = new List<string> { exe, typeof(Program).Assembly.Location, typeof(GameNames).Assembly.Location };
            inputs.AddRange(Directory.GetFiles(mappings, "*", SearchOption.AllDirectories));
            DateTime newestInput = inputs.Max(File.GetLastWriteTimeUtc);
            bool fresh = File.Exists(output) && File.GetLastWriteTimeUtc(output) > newestInput
                && (analysis == null || (File.Exists(analysis) && File.GetLastWriteTimeUtc(analysis) > newestInput));
            if (fresh)
            {
                Console.WriteLine("refgen: up to date (" + output + ")");
                return 0;
            }

            var named = NamedMap.LoadDirectory(Path.Combine(mappings, "named"));
            Guid mvid;
            using (var probe = GameNames.ReadModule(exe)) mvid = probe.Mvid;
            var obf = LoadObfFor(mappings, mvid);

            if (analysis != null)
            {
                using var a = GameNames.ReadModule(exe);
                var alog = GameNames.ApplyMappings(a, obf, named, out var asrc);
                GameNames.AddEnumVariants(a, named, asrc);
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(analysis)));
                a.Write(analysis);
                Console.WriteLine("refgen: analysis exe -> " + analysis);
            }

            using var m = GameNames.ReadModule(exe);
            var log = GameNames.ApplyMappings(m, obf, named, out var source);
            int enums = GameNames.AddEnumVariants(m, named, source);
            GameNames.Publicize(m);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            m.Write(output);
            Console.WriteLine($"refgen: build {mvid}: renamed {log.TypesRenamed} types, {log.FieldsRenamed} fields, "
                + $"{log.MethodsRenamed} methods, {log.ParamsRenamed} parameters ({log.Collisions} collision fallbacks); "
                + $"{enums} enum constants re-added -> {output}");
            return 0;
        }
    }
}
