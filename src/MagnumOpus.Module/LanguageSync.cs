using System;
using System.IO;
using MagnumOpus.Core;
using MagnumOpus.Core.Strings;

namespace MagnumOpus.Module
{
    /// <summary>
    /// The mod's own words follow the GAME's language (Translations.currentLanguage, which the
    /// Options language dropdown sets live), never the system's: MagnumOpus\lang\&lt;code&gt;.txt, the
    /// code being the game's own (Translations.countryCodes: en, de, fr, ru, ...), English when there
    /// is no file. Polled once a frame (one enum compare); a change swaps the whole table
    /// (Strings.LoadTranslation) and logs what the file got wrong.
    /// </summary>
    internal static class LanguageSync
    {
        private static bool _known;
        private static Language _current;
        private static string _dir;

        public static void Start(string pluginDir)
        {
            _dir = Path.Combine(pluginDir, "lang");
            _known = false;
        }

        public static void Tick()
        {
            Language now = Translations.currentLanguage;
            if (_known && now == _current) return;
            _known = true;
            _current = now;
            string code = Translations.countryCodes.TryGetValue(now, out var c) ? c : "en";
            string path = Path.Combine(_dir ?? "", code + ".txt");
            try
            {
                if (code == "en" || !File.Exists(path))
                {
                    Strings.LoadTranslation(null);
                    CoreLog.Info("[lang] game language " + now + " (" + code + "): the mod speaks English" + (code == "en" ? "" : " (no " + path + ")") + ".");
                    return;
                }
                var report = Strings.LoadTranslation(File.ReadAllLines(path));
                CoreLog.Info("[lang] game language " + now + ": loaded " + path + ", " + report.Applied + " strings.");
                foreach (var problem in report.Problems()) CoreLog.Warning("[lang] " + code + ".txt: " + problem);
            }
            catch (Exception e)
            {
                CoreLog.Warning("[lang] loading " + path + " failed: " + e.Message);
                Strings.LoadTranslation(null);
            }
        }
    }
}
