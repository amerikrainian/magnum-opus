using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace MagnumOpus.Names
{
    /// <summary>
    /// The community's intermediary -> human names (om_mappings named/*.txt). The format: a first
    /// line "Mapping version: X.Y.Z", then "intermediary,human" lines; intermediary names are globally
    /// unique (class_N, method_N, field_N, param_N, ...), so the file is one flat table and its "##"
    /// headings are comments. "enum_N.V,Name" lines re-add enum constants the obfuscator stripped (V
    /// decimal or 0x hex). Only files of the highest version present are used, as OpusMutatum does.
    /// </summary>
    public sealed class NamedMap
    {
        public readonly Dictionary<string, string> Names = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Per intermediary enum name: value -> constant name.</summary>
        public readonly Dictionary<string, SortedDictionary<long, string>> EnumVariants =
            new Dictionary<string, SortedDictionary<long, string>>(StringComparer.Ordinal);

        public Version Version = new Version(0, 0);

        public string Get(string inter)
        {
            if (inter == null) return null;
            return Names.TryGetValue(inter, out var named) && named.Length > 0 ? named : null;
        }

        /// <summary>Load every *.txt under a directory tree (named/ and named/auxiliary/).</summary>
        public static NamedMap LoadDirectory(string dir)
        {
            var files = new List<string>(Directory.GetFiles(dir, "*.txt", SearchOption.AllDirectories));
            files.Sort(StringComparer.Ordinal);
            var texts = new List<KeyValuePair<string, string[]>>();
            foreach (var f in files) texts.Add(new KeyValuePair<string, string[]>(f, File.ReadAllLines(f)));
            return Load(texts);
        }

        public static NamedMap Load(IEnumerable<KeyValuePair<string, string[]>> files)
        {
            var map = new NamedMap();
            var chosen = new List<KeyValuePair<string, string[]>>();
            foreach (var f in files)
            {
                string[] lines = f.Value;
                const string prefix = "Mapping version: ";
                if (lines.Length == 0 || !lines[0].StartsWith(prefix, StringComparison.Ordinal)) continue;
                if (!Version.TryParse(lines[0].Substring(prefix.Length).Trim(), out var v)) continue;
                if (v > map.Version) { chosen.Clear(); map.Version = v; }
                if (v == map.Version) chosen.Add(f);
            }
            foreach (var f in chosen)
            {
                string[] lines = f.Value;
                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0 || line[0] == '#') continue;
                    int comma = line.IndexOf(',');
                    if (comma <= 0) continue;
                    string key = line.Substring(0, comma).Trim();
                    string value = line.Substring(comma + 1).Trim();
                    int dot = key.IndexOf('.');
                    if (dot > 0)
                    {
                        string enumName = key.Substring(0, dot);
                        string v = key.Substring(dot + 1).Trim();
                        long n = v.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                            ? long.Parse(v.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                            : long.Parse(v, CultureInfo.InvariantCulture);
                        if (!map.EnumVariants.TryGetValue(enumName, out var variants))
                            map.EnumVariants[enumName] = variants = new SortedDictionary<long, string>();
                        variants[n] = value;
                    }
                    else
                    {
                        map.Names[key] = value;
                    }
                }
            }
            return map;
        }
    }
}
