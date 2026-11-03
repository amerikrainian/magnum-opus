using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagnumOpus.Names;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;

namespace MagnumOpus.Tests
{
    public class NamesTests
    {
        // A tiny "game": obfuscated outer type with a nested type, an overloaded method pair sharing
        // one obfuscated name, a field, and a real-named type with an obfuscated member.
        private static ModuleDefinition MakeGame()
        {
            var m = ModuleDefinition.CreateModule("Lightning", ModuleKind.Dll);
            var obj = m.TypeSystem.Object;
            var outer = new TypeDefinition("", "#=qOuter", TypeAttributes.Public | TypeAttributes.Class, obj);
            var inner = new TypeDefinition("", "#=qInner", TypeAttributes.NestedPrivate | TypeAttributes.Class, obj);
            outer.NestedTypes.Add(inner);
            m.Types.Add(outer);
            var real = new TypeDefinition("", "GameLogic", TypeAttributes.Public | TypeAttributes.Class, obj);
            m.Types.Add(real);
            var other = new TypeDefinition("", "#=qOther", TypeAttributes.Public | TypeAttributes.Class, obj);
            m.Types.Add(other);

            var f = new FieldDefinition("#=qF", FieldAttributes.Private, m.TypeSystem.Int32);
            outer.Fields.Add(f);
            var a = new MethodDefinition("#=qM", MethodAttributes.Public, m.TypeSystem.Void);
            a.Parameters.Add(new ParameterDefinition("#=qp", ParameterAttributes.None, m.TypeSystem.Int32));
            var b = new MethodDefinition("#=qM", MethodAttributes.Public, m.TypeSystem.Void);
            b.Parameters.Add(new ParameterDefinition("#=qp", ParameterAttributes.None, inner));
            outer.Methods.Add(a);
            outer.Methods.Add(b);
            var loop = new MethodDefinition("#=qLoop", MethodAttributes.Public, m.TypeSystem.Void);
            real.Methods.Add(loop);
            return m;
        }

        private static ObfMap MakeObf(ModuleDefinition game)
        {
            var map = new ObfMap { Mvid = game.Mvid };
            var outer = new ObfClass { ObfFullName = "#=qOuter", Inter = "class_1" };
            outer.Fields.Add(new ObfField { Obf = "#=qF", Inter = "field_1" });
            outer.Methods.Add(new ObfMethod { Obf = "#=qM", Inter = "method_1", Ret = "System.Void", Args = new[] { "System.Int32" }, ParamInters = new[] { "param_1" } });
            outer.Methods.Add(new ObfMethod { Obf = "#=qM", Inter = "method_2", Ret = "System.Void", Args = new[] { "#=qOuter/#=qInner" }, ParamInters = new string[] { null } });
            map.Classes.Add(outer);
            map.Classes.Add(new ObfClass { ObfFullName = "#=qOuter/#=qInner", Inter = "class_2" });
            map.Classes.Add(new ObfClass { ObfFullName = "#=qOther", Inter = "class_3" });
            var real = new ObfClass { ObfFullName = "GameLogic" };
            real.Methods.Add(new ObfMethod { Obf = "#=qLoop", Inter = "method_3", Ret = "System.Void", Args = new string[0], ParamInters = new string[0] });
            map.Classes.Add(real);
            return map;
        }

        private static NamedMap MakeNamed()
        {
            // class_3 wants the same human name as class_1: both must fall back to intermediary.
            return NamedMap.Load(new[]
            {
                new KeyValuePair<string, string[]>("a.txt", new[]
                {
                    "Mapping version: 0.1.0",
                    "## Board",
                    "class_1,Board",
                    "class_2,Cell",
                    "class_3,Board",
                    "field_1,width",
                    "method_1,Place",
                    "method_2,Place",
                    "method_3,GameLoop",
                    "param_1,count",
                    "enum_9.0x10,Sixteen",
                }),
                new KeyValuePair<string, string[]>("old.txt", new[] { "Mapping version: 0.0.9", "class_1,Stale" }),
            });
        }

        [Fact]
        public void NamedMap_uses_highest_version_and_parses_enum_constants()
        {
            var named = MakeNamed();
            Assert.Equal(new Version(0, 1, 0), named.Version);
            Assert.Equal("Board", named.Get("class_1"));
            Assert.Equal("Sixteen", named.EnumVariants["enum_9"][16]);
        }

        [Fact]
        public void ObfMap_tsv_round_trips()
        {
            var game = MakeGame();
            var map = MakeObf(game);
            var sw = new StringWriter();
            map.WriteTsv(sw);
            var back = ObfMap.ReadTsv(new StringReader(sw.ToString()));
            Assert.Equal(map.Mvid, back.Mvid);
            var outer = back.Find("#=qOuter");
            Assert.Equal("class_1", outer.Inter);
            Assert.Equal(2, outer.Methods.Count);
            Assert.Equal(new[] { "#=qOuter/#=qInner" }, outer.Methods[1].Args);
            Assert.Null(outer.Methods[1].ParamInters[0]);
            Assert.Null(back.Find("GameLogic").Inter);
        }

        [Fact]
        public void ObfMap_reads_the_neutral_json_model()
        {
            var model = new Dictionary<string, object>
            {
                ["Classes"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["ClassFullNameA"] = "#=qA", ["ClassNameB"] = "class_7",
                        ["Fields"] = new List<object> { new Dictionary<string, object> { ["FieldNameA"] = "#=qf", ["FieldNameB"] = "field_7" } },
                        ["Methods"] = new List<object>
                        {
                            new Dictionary<string, object>
                            {
                                ["MethodNameA"] = ".ctor", ["MethodNameB"] = null, ["ReturnTypeFullNameA"] = "System.Void",
                                ["ArgumentTypeFullNamesA"] = new List<object> { "#=qA+#=qB" },
                                ["Parameters"] = new List<object> { new Dictionary<string, object> { ["ParameterNameA"] = "x", ["ParameterNameB"] = "param_7" } },
                            },
                        },
                    },
                    // An external reference with nothing mapped is dropped.
                    new Dictionary<string, object> { ["ClassFullNameA"] = "System.Int32", ["ClassNameB"] = null, ["Fields"] = new List<object>(), ["Methods"] = new List<object>() },
                },
            };
            var map = ObfMap.FromJsonModel(model, Guid.NewGuid());
            Assert.Single(map.Classes);
            var m = map.Classes[0].Methods.Single();
            Assert.Null(m.Inter);
            Assert.Equal("#=qA/#=qB", m.Args[0]);
            Assert.Equal("param_7", m.ParamInters[0]);
        }

        [Fact]
        public void Mvid_comes_from_the_community_file_name()
        {
            Assert.True(ObfMap.TryMvidFromFileName("mappings_ce_20261022_42f8deb3-ffe7-44b7-b3ab-037e2df2b2e0.json", out var id));
            Assert.Equal(Guid.Parse("42f8deb3-ffe7-44b7-b3ab-037e2df2b2e0"), id);
            Assert.False(ObfMap.TryMvidFromFileName("readme.json", out _));
        }

        [Fact]
        public void Renamer_applies_human_names_and_falls_back_on_collision()
        {
            var game = MakeGame();
            var log = GameNames.ApplyMappings(game, MakeObf(game), MakeNamed(), out _);
            var names = game.GetTypes().Select(Sig.Full).ToList();
            // class_1 and class_3 both asked for "Board": both fall back to intermediary.
            Assert.Contains("class_1", names);
            Assert.Contains("class_3", names);
            Assert.Contains("class_1/Cell", names);
            var outer = game.Types.Single(t => t.Name == "class_1");
            Assert.Equal("width", outer.Fields[0].Name);
            // Overloads keep one human name: their signatures differ.
            Assert.Equal(new[] { "Place", "Place" }, outer.Methods.Select(m => m.Name).ToArray());
            Assert.Equal("count", outer.Methods[0].Parameters[0].Name);
            Assert.Equal("GameLoop", game.Types.Single(t => t.Name == "GameLogic").Methods[0].Name);
            Assert.True(log.Collisions >= 2);
        }

        [Fact]
        public void NameTable_maps_new_names_back_to_shipping_names()
        {
            var game = MakeGame();
            var table = GameNames.TableFromMappings(game, MakeObf(game), MakeNamed());
            Assert.True(table.TryType("class_1/Cell", out var cell));
            Assert.Equal("#=qOuter/#=qInner", cell);
            Assert.True(table.TryField("class_1", "width", out var f));
            Assert.Equal("#=qF", f);
            Assert.True(table.TryMethod("class_1", "Place", "System.Void(class_1/Cell)`0", out var place));
            Assert.Equal("#=qM", place);
            Assert.Equal(2, table.Methods("class_1", "Place").Count);
            Assert.True(table.TryType("GameLogic", out var gl));
            Assert.Equal("GameLogic", gl);
        }

        [Fact]
        public void Remapper_rewrites_a_module_compiled_against_human_names()
        {
            // The "reference" the module compiled against: the game, renamed.
            var reference = MakeGame();
            GameNames.ApplyMappings(reference, MakeObf(reference), MakeNamed(), out _);

            // The module: one method calling Place(Cell), reading width, calling GameLoop, and
            // touching a member the running build lacks.
            var mod = ModuleDefinition.CreateModule("MagnumOpus.Module", ModuleKind.Dll);
            var gameRef = new AssemblyNameReference("Lightning", new Version(1, 0, 0, 0));
            mod.AssemblyReferences.Add(gameRef);
            var boardRef = new TypeReference("", "class_1", mod, gameRef);
            var cellRef = new TypeReference("", "Cell", mod, gameRef) { DeclaringType = boardRef };
            var logicRef = new TypeReference("", "GameLogic", mod, gameRef);
            var place = new MethodReference("Place", mod.TypeSystem.Void, boardRef) { HasThis = true };
            place.Parameters.Add(new ParameterDefinition(cellRef));
            var width = new FieldReference("width", mod.TypeSystem.Int32, boardRef);
            var loop = new MethodReference("GameLoop", mod.TypeSystem.Void, logicRef) { HasThis = true };
            var gone = new MethodReference("Vanished", mod.TypeSystem.Void, logicRef) { HasThis = true };

            var host = new TypeDefinition("", "Feature", TypeAttributes.Public | TypeAttributes.Class, mod.TypeSystem.Object);
            mod.Types.Add(host);
            var body = new MethodDefinition("Run", MethodAttributes.Public | MethodAttributes.Static, mod.TypeSystem.Void);
            host.Methods.Add(body);
            var il = body.Body.GetILProcessor();
            il.Append(il.Create(OpCodes.Ldnull));
            il.Append(il.Create(OpCodes.Ldnull));
            il.Append(il.Create(OpCodes.Callvirt, place));
            il.Append(il.Create(OpCodes.Ldnull));
            il.Append(il.Create(OpCodes.Ldfld, width));
            il.Append(il.Create(OpCodes.Pop));
            il.Append(il.Create(OpCodes.Ldnull));
            il.Append(il.Create(OpCodes.Callvirt, loop));
            il.Append(il.Create(OpCodes.Ldnull));
            il.Append(il.Create(OpCodes.Callvirt, gone));
            il.Append(il.Create(OpCodes.Ret));

            // Real modules are read from bytes (an in-memory model has no reference table).
            var bytes = new MemoryStream();
            mod.Write(bytes);
            bytes.Position = 0;
            var loaded = ModuleDefinition.ReadModule(bytes);

            // The running build: the original exe, renamed in memory to build the table.
            var running = MakeGame();
            var table = GameNames.TableFromMappings(running, MakeObf(running), MakeNamed());

            var result = Remapper.Remap(loaded, table);
            var calls = loaded.Types.Single(t => t.Name == "Feature").Methods[0].Body.Instructions
                .Select(i => i.Operand).OfType<MemberReference>().ToList();
            var placeAfter = (MethodReference)calls[0];
            Assert.Equal("#=qM", placeAfter.Name);
            Assert.Equal("#=qF", calls[1].Name);
            Assert.Equal("#=qLoop", calls[2].Name);
            Assert.Equal("#=qOuter", placeAfter.DeclaringType.Name);
            Assert.Equal("#=qInner", placeAfter.Parameters[0].ParameterType.Name);
            Assert.Equal("GameLogic", calls[2].DeclaringType.Name);
            Assert.Single(result.Unresolved);
            Assert.StartsWith("M GameLogic::Vanished", result.Unresolved[0]);
        }

        private static string RepoFile(params string[] parts)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Directory.Build.props"))) dir = dir.Parent;
            return dir == null ? null : Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray());
        }

        [Fact]
        public void Real_game_resolves_the_host_hooks_when_the_workspace_exists()
        {
            string exe = RepoFile("game", "Lightning.orig.exe");
            string mappings = RepoFile("mappings");
            if (exe == null || !File.Exists(exe)) return; // no analysis workspace on this machine
            using var module = GameNames.ReadModule(exe);
            string obfPath = Path.Combine(mappings, "obf", module.Mvid.ToString("D") + ".tsv");
            if (!File.Exists(obfPath)) return; // a newer game build than the committed mappings
            var table = GameNames.TableFromMappings(module, ObfMap.ReadTsvFile(obfPath), NamedMap.LoadDirectory(Path.Combine(mappings, "named")));
            Assert.Single(table.Methods("GameLogic", "GameLoop"));
            Assert.Single(table.Methods("GameLogic", "GameInit"));
            Assert.Single(table.Methods("GameLogic", "GetCurrentScreen"));
            Assert.True(table.TryField("GameLogic", "instance", out var inst));
            Assert.StartsWith("#=q", inst);
        }
    }
}
