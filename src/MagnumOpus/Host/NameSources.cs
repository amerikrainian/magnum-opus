using System;
using System.IO;
using MagnumOpus.Names;

namespace MagnumOpus.Host
{
    /// <summary>The names for one game build, and where they came from.</summary>
    internal sealed class BuildNames
    {
        public NameTable Table;
        /// <summary>"bundled", "downloaded" or "matched".</summary>
        public string Source;
        public string Detail;
    }

    /// <summary>
    /// Finds the names for the running build, best source first (CLAUDE.md "Surviving game updates"):
    ///   1. bundled: MagnumOpus\mappings\obf\&lt;mvid&gt;.tsv, shipped with the release;
    ///   2. downloaded: %LOCALAPPDATA%\MagnumOpus\mappings\&lt;mvid&gt;.tsv, the community mapping
    ///      fetched for a build newer than the release (<see cref="MappingDownloader"/>);
    ///   3. matched: the running exe bound to MagnumOpus\mappings\fingerprint.tsv (the shape of the
    ///      build the release was made against) by <see cref="Matcher"/>.
    /// 1 and 2 use the shipped intermediary -> human layer (MagnumOpus\mappings\named\).
    /// </summary>
    internal static class NameSources
    {
        public static string CacheDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MagnumOpus", "mappings");

        public static string CachedPath(Guid mvid) => Path.Combine(CacheDir, mvid.ToString("D") + ".tsv");

        /// <summary>True when a community mapping (bundled or downloaded) exists for the build.</summary>
        public static bool HasMapping(Guid mvid, string dataDir) =>
            File.Exists(BundledPath(mvid, dataDir)) || File.Exists(CachedPath(mvid));

        private static string BundledPath(Guid mvid, string dataDir) =>
            Path.Combine(dataDir, "mappings", "obf", mvid.ToString("D") + ".tsv");

        public static BuildNames Load(Guid mvid, string exePath, string dataDir)
        {
            string bundled = BundledPath(mvid, dataDir);
            string cached = CachedPath(mvid);
            string mapPath = File.Exists(bundled) ? bundled : File.Exists(cached) ? cached : null;

            using (var module = GameNames.ReadModule(exePath))
            {
                if (module.Mvid != mvid)
                {
                    Log.Error("[names] " + exePath + " is build " + module.Mvid + ", but the running game is " + mvid + ".");
                    return null;
                }

                if (mapPath != null)
                {
                    string namedDir = Path.Combine(dataDir, "mappings", "named");
                    if (!Directory.Exists(namedDir))
                    {
                        Log.Error("[names] " + namedDir + " is missing; reinstall the mod.");
                        return null;
                    }
                    var table = GameNames.TableFromMappings(module, ObfMap.ReadTsvFile(mapPath), NamedMap.LoadDirectory(namedDir));
                    return new BuildNames { Table = table, Source = mapPath == bundled ? "bundled" : "downloaded" };
                }

                string fpPath = Path.Combine(dataDir, "mappings", "fingerprint.tsv");
                if (!File.Exists(fpPath))
                {
                    Log.Error("[names] no mapping for build " + mvid + " and no fingerprint at " + fpPath + ".");
                    return null;
                }
                var fp = Fingerprint.ReadFile(fpPath);
                var matcher = new Matcher(module, fp);
                var log = Renamer.Apply(module, matcher);
                Log.Warning("[names] build " + mvid + " has no community mapping yet; matched against the fingerprint of build "
                    + fp.Mvid + ": " + matcher.Stats + ".");
                return new BuildNames { Table = NameTable.FromRenamed(module, log), Source = "matched", Detail = matcher.Stats.ToString() };
            }
        }
    }
}
