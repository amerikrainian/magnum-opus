using System;
using System.IO;
using MagnumOpus.Names;

namespace MagnumOpus.Host
{
    /// <summary>The names for one game build, and where they came from.</summary>
    internal sealed class BuildNames
    {
        public NameTable Table;
        public string Source;
    }

    /// <summary>
    /// Finds the names for the running build, best source first:
    ///   1. bundled: MagnumOpus\mappings\obf\&lt;mvid&gt;.tsv, shipped with the release;
    ///   2. downloaded: %LOCALAPPDATA%\MagnumOpus\mappings\&lt;mvid&gt;.tsv, a community mapping fetched
    ///      for a build newer than the release.
    /// Either way the intermediary -> human layer is the shipped MagnumOpus\mappings\named\.
    /// </summary>
    internal static class NameSources
    {
        public static string CacheDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MagnumOpus", "mappings");

        public static BuildNames Load(Guid mvid, string exePath, string dataDir)
        {
            string namedDir = Path.Combine(dataDir, "mappings", "named");
            if (!Directory.Exists(namedDir))
            {
                Log.Error("[names] " + namedDir + " is missing; reinstall the mod.");
                return null;
            }
            NamedMap named = NamedMap.LoadDirectory(namedDir);

            string file = mvid.ToString("D") + ".tsv";
            string bundled = Path.Combine(dataDir, "mappings", "obf", file);
            string cached = Path.Combine(CacheDir, file);
            string source = File.Exists(bundled) ? "bundled" : File.Exists(cached) ? "downloaded" : null;
            if (source == null)
            {
                Log.Warning("[names] no mapping for build " + mvid + " (looked in " + bundled + " and " + cached + ").");
                return null;
            }

            ObfMap obf = ObfMap.ReadTsvFile(source == "bundled" ? bundled : cached);
            using (var module = GameNames.ReadModule(exePath))
            {
                if (module.Mvid != mvid)
                {
                    Log.Error("[names] " + exePath + " is build " + module.Mvid + ", but the running game is " + mvid + ".");
                    return null;
                }
                return new BuildNames { Table = GameNames.TableFromMappings(module, obf, named), Source = source };
            }
        }
    }
}
