using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using MagnumOpus.Names;

namespace MagnumOpus.Host
{
    /// <summary>
    /// Binds the mod to the running game build. When the CLR loads Lightning (before Main), this finds
    /// the name source for the build's MVID (see <see cref="NameSources"/>), renames a Cecil copy of
    /// the exe in memory to build the <see cref="NameTable"/> (human name -> shipping name), attaches
    /// the two permanent hooks and loads the module. Also the host's reflection-by-human-name helpers:
    /// the hooks and the dev tools reach game members by the names the module source uses.
    /// </summary>
    internal static class GameBinding
    {
        public static Assembly Game { get; private set; }
        public static NameTable Table { get; private set; }
        public static Guid Mvid { get; private set; }
        /// <summary>Where the table's names came from ("bundled", "downloaded", "matched").</summary>
        public static string Source { get; private set; }
        public static string GameDir { get; private set; }
        /// <summary>The mod's data folder in the game folder (mappings, the module, lang).</summary>
        public static string DataDir { get; private set; }
        public static bool Bound => Table != null;

        public static void OnGameAssembly(Assembly game)
        {
            Game = game;
            Mvid = game.ManifestModule.ModuleVersionId;
            GameDir = AppDomain.CurrentDomain.BaseDirectory;
            DataDir = Path.Combine(GameDir, "MagnumOpus");
            Log.Info("Game assembly: " + game.FullName + ", build " + Mvid);

            var sw = Stopwatch.StartNew();
            var names = NameSources.Load(Mvid, ExePath, DataDir);

            // A build the release does not bundle: fetch the community's mapping in the background.
            // When it lands, the module rebinds onto it (or, without a running module, the next launch
            // uses the cache).
            if ((names == null || names.Source == "matched") && MappingDownloader.Enabled)
                MappingDownloader.Start(Mvid, path => Runtime.Post(Rebind));

            if (names == null)
            {
                Log.Error("[names] no names for build " + Mvid + "; the mod cannot bind to this game build.");
                SpeechOutput.Tts.Speak("Magnum Opus does not support this game build yet.", interrupt: true);
                return;
            }
            Table = names.Table;
            Source = names.Source;
            Log.Info("[names] build " + Mvid + ": " + Table.TypeCount + " types named from the " + Source
                + " source in " + sw.ElapsedMilliseconds + " ms.");
            if (Source == "matched") ModHostImpl.Notices.Enqueue(HostNotice.NamesMatched);

            if (!GameHooks.Attach())
            {
                Log.Error("[hooks] the game hooks did not attach; the game runs without the mod.");
                return;
            }
            Runtime.Start();
        }

        private static string ExePath => Path.Combine(GameDir, "Lightning.exe");

        /// <summary>A community mapping arrived for this build: rebuild the table from it and reload
        /// the module onto the new names. Main thread (posted from the download thread).</summary>
        private static void Rebind()
        {
            try
            {
                var names = NameSources.Load(Mvid, ExePath, DataDir);
                if (names == null || names.Source == "matched") return;
                Table = names.Table;
                Source = names.Source;
                Log.Info("[names] rebound build " + Mvid + " to the " + Source + " mapping; reloading the module.");
                ModHostImpl.Notices.Enqueue(HostNotice.MappingUpdated);
                Runtime.ReloadModule();
            }
            catch (Exception ex)
            {
                Log.Error("[names] rebinding to the downloaded mapping failed; the matched names stay in use", ex);
            }
        }

        /// <summary>A game type by human full name (Outer/Inner), or null.</summary>
        public static Type Type(string humanFull)
        {
            if (Table == null || !Table.TryType(humanFull, out var shipping)) return null;
            return Game.GetType(NameTable.ReflectionName(shipping), false);
        }

        public const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            | BindingFlags.Static | BindingFlags.DeclaredOnly;

        /// <summary>A game method by human names. With several overloads, <paramref name="paramCount"/>
        /// picks; null when absent or still ambiguous.</summary>
        public static MethodInfo Method(string humanType, string humanName, int paramCount = -1)
        {
            var type = Type(humanType);
            if (type == null) return null;
            var overloads = Table.Methods(humanType, humanName);
            var candidates = type.GetMethods(All)
                .Where(m => overloads.Any(o => o.Value == m.Name && (paramCount < 0 || ParamCount(o.Key) == paramCount)))
                .Where(m => paramCount < 0 || m.GetParameters().Length == paramCount)
                .ToList();
            return candidates.Count == 1 ? candidates[0] : null;
        }

        /// <summary>A game field by human names, or null.</summary>
        public static FieldInfo Field(string humanType, string humanName)
        {
            var type = Type(humanType);
            if (type == null || !Table.TryField(humanType, humanName, out var shipping)) return null;
            return type.GetField(shipping, All);
        }

        /// <summary>The human full name of a runtime type (game types renamed, others as they are).</summary>
        public static string HumanName(System.Type t)
        {
            if (t == null) return "null";
            if (Table == null || t.Assembly != Game) return t.Name;
            return Table.HumanType(t.FullName.Replace('+', '/'));
        }

        // Parameters in a Sig.Method string "Ret(A,B<C,D>)`0": top-level commas between the parens.
        internal static int ParamCount(string sig)
        {
            int open = sig.IndexOf('(');
            int close = sig.LastIndexOf(')');
            if (open < 0 || close <= open + 1) return 0;
            int depth = 0, count = 1;
            for (int i = open + 1; i < close; i++)
            {
                char c = sig[i];
                if (c == '<' || c == '[') depth++;
                else if (c == '>' || c == ']') depth--;
                else if (c == ',' && depth == 0) count++;
            }
            return count;
        }
    }
}
