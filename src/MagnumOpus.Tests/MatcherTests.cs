using System;
using System.IO;
using System.Linq;
using MagnumOpus.Names;
using Mono.Cecil;
using Xunit;

namespace MagnumOpus.Tests
{
    public class MatcherTests
    {
        // A build of the tiny game with a given obfuscation salt (every shipped name changes) and
        // optionally one method inserted, as an update would.
        private static ModuleDefinition Build(string salt, bool extraMethod)
        {
            var m = ModuleDefinition.CreateModule("Lightning", ModuleKind.Dll);
            var obj = m.TypeSystem.Object;
            var board = new TypeDefinition("", "#=q" + salt + "Board", TypeAttributes.Public, obj);
            var cell = new TypeDefinition("", "#=q" + salt + "Cell", TypeAttributes.NestedPublic, obj);
            board.NestedTypes.Add(cell);
            m.Types.Add(board);
            var logic = new TypeDefinition("", "GameLogic", TypeAttributes.Public, obj);
            m.Types.Add(logic);

            board.Fields.Add(new FieldDefinition("#=q" + salt + "w", FieldAttributes.Public, m.TypeSystem.Int32));
            board.Fields.Add(new FieldDefinition("#=q" + salt + "c", FieldAttributes.Public, cell));
            var place = new MethodDefinition("#=q" + salt + "p", MethodAttributes.Public, m.TypeSystem.Void);
            place.Parameters.Add(new ParameterDefinition(cell));
            board.Methods.Add(place);
            if (extraMethod)
                board.Methods.Add(new MethodDefinition("#=q" + salt + "x", MethodAttributes.Public, m.TypeSystem.String));
            var count = new MethodDefinition("#=q" + salt + "n", MethodAttributes.Public, m.TypeSystem.Int32);
            board.Methods.Add(count);
            logic.Methods.Add(new MethodDefinition("#=q" + salt + "loop", MethodAttributes.Public, m.TypeSystem.Void));
            logic.Fields.Add(new FieldDefinition("#=q" + salt + "inst", FieldAttributes.Public | FieldAttributes.Static, logic));
            return m;
        }

        // Human names for the known build, as the community mapping would give them.
        private sealed class KnownNames : INameSource
        {
            public System.Collections.Generic.IList<string> TypeNames(TypeDefinition t) =>
                t.Name.EndsWith("Board") ? new[] { "Board" } : t.Name.EndsWith("Cell") ? new[] { "Cell" } : new string[0];
            public System.Collections.Generic.IList<string> FieldNames(FieldDefinition f) =>
                f.Name.EndsWith("w") ? new[] { "width" } : f.Name.EndsWith("c") ? new[] { "cursor" } : f.Name.EndsWith("inst") ? new[] { "instance" } : new string[0];
            public System.Collections.Generic.IList<string> MethodNames(MethodDefinition m) =>
                m.Name.EndsWith("loop") ? new[] { "GameLoop" } : m.Name.EndsWith("p") ? new[] { "Place" } : m.Name.EndsWith("n") ? new[] { "Count" } : new string[0];
            public string ParamName(MethodDefinition m, int i) => null;
        }

        private static Fingerprint KnownFingerprint()
        {
            var known = Build("A", extraMethod: false);
            Renamer.Apply(known, new KnownNames());
            return Fingerprint.FromRenamed(known);
        }

        [Fact]
        public void Fingerprint_round_trips()
        {
            var fp = KnownFingerprint();
            var sw = new StringWriter();
            fp.Write(sw);
            var back = Fingerprint.Read(new StringReader(sw.ToString()));
            Assert.Equal(fp.Types.Count, back.Types.Count);
            Assert.Equal(fp.Types.Select(t => t.Name), back.Types.Select(t => t.Name));
            Assert.Equal(fp.Types[0].Methods.Select(m => m.Sig), back.Types[0].Methods.Select(m => m.Sig));
        }

        [Fact]
        public void A_rebuilt_game_with_new_names_and_an_inserted_method_is_named_from_the_fingerprint()
        {
            var fresh = Build("B", extraMethod: true);
            var matcher = new Matcher(fresh, KnownFingerprint());
            var table = NameTable.FromRenamed(fresh, Renamer.Apply(fresh, matcher));

            Assert.True(table.TryType("Board/Cell", out var cell));
            Assert.Equal("#=qBBoard/#=qBCell", cell);
            Assert.True(table.TryMethod("Board", "Place", "System.Void(Board/Cell)`0", out var place));
            Assert.Equal("#=qBp", place);
            Assert.True(table.TryMethod("Board", "Count", "System.Int32()`0", out var count));
            Assert.Equal("#=qBn", count);
            Assert.True(table.TryField("Board", "cursor", out var cursor));
            Assert.Equal("#=qBc", cursor);
            Assert.True(table.TryMethod("GameLogic", "GameLoop", "System.Void()`0", out var loop));
            Assert.Equal("#=qBloop", loop);
            Assert.True(table.TryField("GameLogic", "instance", out var inst));
            Assert.Equal("#=qBinst", inst);
            // The inserted method has no counterpart and keeps its shipping name.
            Assert.True(table.TryMethod("Board", "#=qBx", "System.String()`0", out _));
        }

        [Fact]
        public void Obfuscated_names_are_recognized()
        {
            Assert.True(Matcher.IsObfuscated("#=qAbc=="));
            Assert.True(Matcher.IsObfuscated("   ​ "));
            Assert.False(Matcher.IsObfuscated("GameLogic"));
            Assert.False(Matcher.IsObfuscated(".ctor"));
        }

        private static string RepoFile(params string[] parts)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Directory.Build.props"))) dir = dir.Parent;
            return dir == null ? null : Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray());
        }

        /// <summary>The public build (2026-10-21) against the CE build's fingerprint: different code,
        /// every name different, no community mapping. The two members were verified live on the
        /// public build on 2026-11-03 (the per-frame tick and the top-screen getter).</summary>
        [Fact]
        public void Real_public_build_is_matched_from_the_CE_fingerprint_when_the_workspace_has_both()
        {
            string ce = RepoFile("game", "Lightning.orig.exe");
            string pub = RepoFile("game", "builds", "public-26115378", "Lightning.exe");
            string mappings = RepoFile("mappings");
            if (ce == null || !File.Exists(ce) || !File.Exists(pub)) return;
            using var known = GameNames.ReadModule(ce);
            string obf = Path.Combine(mappings, "obf", known.Mvid.ToString("D") + ".tsv");
            if (!File.Exists(obf)) return;
            GameNames.ApplyMappings(known, ObfMap.ReadTsvFile(obf), NamedMap.LoadDirectory(Path.Combine(mappings, "named")), out _);
            var fp = Fingerprint.FromRenamed(known);

            using var fresh = GameNames.ReadModule(pub);
            var matcher = new Matcher(fresh, fp);
            var table = NameTable.FromRenamed(fresh, Renamer.Apply(fresh, matcher));
            Assert.Equal("#=qiyXpaFmc0auKLHgZwaAUuA==", table.Methods("GameLogic", "GameLoop").Single().Value);
            Assert.Equal("#=qF2wiDUnsVxT3Bg48FSQHqcuN1CZ9VqhfeGlUfR7jfO8=", table.Methods("GameLogic", "GetCurrentScreen").Single().Value);
            Assert.True(matcher.Stats.MethodsBound > 0.95 * matcher.Stats.Methods, matcher.Stats.ToString());
        }
    }
}
