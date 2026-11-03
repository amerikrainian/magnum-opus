using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MagnumOpus.Names
{
    /// <summary>One game build's obfuscated -> intermediary names: the community's om_mappings
    /// "intermediary" layer for one MVID, read from its JSON or from our compact TSV form
    /// (mappings/obf/&lt;mvid&gt;.tsv: the same facts at a twentieth of the size).</summary>
    public sealed class ObfMap
    {
        public Guid Mvid;
        public readonly List<ObfClass> Classes = new List<ObfClass>();

        private Dictionary<string, ObfClass> _byName;

        /// <summary>The class entry for a type by its obfuscated full name (namespace.Name, nested
        /// types as Outer/Inner), or null.</summary>
        public ObfClass Find(string obfFullName)
        {
            if (_byName == null)
            {
                _byName = new Dictionary<string, ObfClass>(StringComparer.Ordinal);
                foreach (var c in Classes) _byName[c.ObfFullName] = c;
            }
            _byName.TryGetValue(obfFullName, out var hit);
            return hit;
        }

        public const string TsvHeader = "# MagnumOpus obfuscated->intermediary map v1";

        /// <summary>
        /// Build from an om_mappings intermediary JSON (mappings_&lt;branch&gt;_&lt;date&gt;_&lt;mvid&gt;.json)
        /// already parsed into a neutral object model: objects as string-keyed
        /// <see cref="System.Collections.IDictionary"/>, arrays as <see cref="System.Collections.IEnumerable"/>,
        /// strings, null. This library ships no JSON parser: the host parses with the framework's own
        /// JavaScriptSerializer (in-box on .NET Framework), the build tool with System.Text.Json; both
        /// produce this shape. The MVID comes from the file name, as the community keys it.
        /// </summary>
        public static ObfMap FromJsonModel(object root, Guid mvid)
        {
            var map = new ObfMap { Mvid = mvid };
            foreach (var c in Seq(Obj(root)["Classes"]).Select(Obj))
            {
                var cls = new ObfClass { ObfFullName = Norm(Str(c["ClassFullNameA"])), Inter = Str(c["ClassNameB"]) };
                foreach (var f in Seq(c["Fields"]).Select(Obj))
                {
                    string fb = Str(f["FieldNameB"]);
                    if (fb != null) cls.Fields.Add(new ObfField { Obf = Str(f["FieldNameA"]), Inter = fb });
                }
                foreach (var m in Seq(c["Methods"]).Select(Obj))
                {
                    var method = new ObfMethod
                    {
                        Obf = Str(m["MethodNameA"]),
                        Inter = Str(m["MethodNameB"]),
                        Ret = Norm(Str(m["ReturnTypeFullNameA"])),
                        Args = Seq(m["ArgumentTypeFullNamesA"]).Select(a => Norm(Str(a))).ToArray(),
                        ParamInters = Seq(m["Parameters"]).Select(p => Str(Obj(p)["ParameterNameB"])).ToArray(),
                    };
                    if (method.Inter != null || method.ParamInters.Any(p => p != null))
                        cls.Methods.Add(method);
                }
                if (cls.Inter != null || cls.Fields.Count > 0 || cls.Methods.Count > 0)
                    map.Classes.Add(cls);
            }
            return map;
        }

        private static System.Collections.IDictionary Obj(object o) =>
            o as System.Collections.IDictionary ?? throw new FormatException("mapping JSON: expected an object.");

        private static IEnumerable<object> Seq(object o) =>
            (o as System.Collections.IEnumerable ?? throw new FormatException("mapping JSON: expected an array.")).Cast<object>();

        private static string Str(object o) => o == null ? null : o as string ?? throw new FormatException("mapping JSON: expected a string.");

        /// <summary>Pull the MVID out of a community mapping file name.</summary>
        public static bool TryMvidFromFileName(string fileName, out Guid mvid)
        {
            mvid = Guid.Empty;
            string stem = Path.GetFileNameWithoutExtension(fileName);
            int cut = stem.LastIndexOf('_');
            return cut >= 0 && Guid.TryParse(stem.Substring(cut + 1), out mvid);
        }

        // Nested types are written Outer/Inner throughout (Cecil's FullName form); reflection's '+'
        // never appears in the community files, but normalize anyway.
        private static string Norm(string s) => s?.Replace('+', '/');

        public void WriteTsv(TextWriter w)
        {
            w.WriteLine(TsvHeader);
            w.WriteLine("mvid\t" + Mvid.ToString("D"));
            foreach (var c in Classes)
            {
                w.WriteLine("C\t" + c.ObfFullName + "\t" + (c.Inter ?? ""));
                foreach (var f in c.Fields) w.WriteLine("F\t" + f.Obf + "\t" + f.Inter);
                foreach (var m in c.Methods)
                {
                    w.WriteLine("M\t" + m.Obf + "\t" + (m.Inter ?? "") + "\t" + m.Ret + "\t" + string.Join("|", m.Args)
                        + "\t" + string.Join(",", m.ParamInters.Select(p => p ?? "-")));
                }
            }
        }

        public static ObfMap ReadTsv(TextReader r)
        {
            var map = new ObfMap();
            ObfClass cls = null;
            string line;
            int n = 0;
            while ((line = r.ReadLine()) != null)
            {
                n++;
                if (line.Length == 0 || line[0] == '#') continue;
                string[] p = line.Split('\t');
                switch (p[0])
                {
                    case "mvid": map.Mvid = Guid.Parse(p[1]); break;
                    case "C":
                        cls = new ObfClass { ObfFullName = p[1], Inter = Empty(p[2]) };
                        map.Classes.Add(cls);
                        break;
                    case "F":
                        Need(cls, n).Fields.Add(new ObfField { Obf = p[1], Inter = p[2] });
                        break;
                    case "M":
                        Need(cls, n).Methods.Add(new ObfMethod
                        {
                            Obf = p[1],
                            Inter = Empty(p[2]),
                            Ret = p[3],
                            Args = p[4].Length == 0 ? new string[0] : SplitArgs(p[4]),
                            ParamInters = p[5].Length == 0 ? new string[0] : p[5].Split(',').Select(x => x == "-" ? null : x).ToArray(),
                        });
                        break;
                    default: throw new FormatException("obf map line " + n + ": unknown record '" + p[0] + "'.");
                }
            }
            if (map.Mvid == Guid.Empty) throw new FormatException("obf map: no mvid line.");
            return map;
        }

        // Argument type names are joined with '|', which no type name contains; a generic argument
        // list's commas stay inside its own name.
        private static string[] SplitArgs(string s) => s.Split('|');

        private static ObfClass Need(ObfClass c, int line) =>
            c ?? throw new FormatException("obf map line " + line + ": member before any class.");

        private static string Empty(string s) => s.Length == 0 ? null : s;

        public static ObfMap ReadTsvFile(string path)
        {
            using (var r = new StreamReader(path, Encoding.UTF8)) return ReadTsv(r);
        }

        public void WriteTsvFile(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            using (var w = new StreamWriter(path, false, new UTF8Encoding(false))) { w.NewLine = "\n"; WriteTsv(w); }
        }
    }

    public sealed class ObfClass
    {
        /// <summary>Obfuscated full name (namespace.Name; nested Outer/Inner, every segment as shipped).</summary>
        public string ObfFullName;
        /// <summary>Intermediary name (class_N, enum_N, struct_N, delegate_N, interface_N), or null for
        /// a type that ships under its real name (its members may still be mapped).</summary>
        public string Inter;
        public readonly List<ObfField> Fields = new List<ObfField>();
        public readonly List<ObfMethod> Methods = new List<ObfMethod>();
    }

    public sealed class ObfField
    {
        public string Obf;
        public string Inter;
    }

    public sealed class ObfMethod
    {
        public string Obf;
        /// <summary>method_N, or null (constructors, real-named methods) when only parameters map.</summary>
        public string Inter;
        /// <summary>Return and argument types as Cecil full names in SHIPPING (obfuscated) terms; with
        /// the name they pick the one method among overloads that share an obfuscated name.</summary>
        public string Ret;
        public string[] Args;
        /// <summary>param_N per parameter position, null where unmapped.</summary>
        public string[] ParamInters;
    }
}
