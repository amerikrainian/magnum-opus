using System.Text;

namespace MagnumOpus.Module.Game
{
    /// <summary>
    /// Game text for speech. The game's own markup: *bold* and _italic_ (a literal star or underscore
    /// escaped with a backslash, Utils.EscapeFormatting), and '●' marking line-break points in
    /// Japanese. Everything else (tags, whitespace, punctuation) is the speech pipeline's
    /// TextFilter's job.
    /// </summary>
    public static class GameText
    {
        public static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length) { sb.Append(s[++i]); continue; }
                if (c == '*' || c == '_' || c == '●') continue;
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>A cost as the game shows it (UIUtils.FormatMetricScore: "90$"), with the '$' its font
        /// draws as the gold mark spoken as the mod's word for it.</summary>
        public static string Cost(int cost) =>
            Clean(UIUtils.FormatMetricScore(ScoreMetric.Cost, cost)).Replace("$", MagnumOpus.Core.Strings.Strings.EditorGoldMark);

        /// <summary>A LocString in the game's current language, cleaned; empty for null.</summary>
        public static string Of(LocString s) => s == null ? "" : Clean(s.GetTranslated());

        /// <summary>The game's own translation of an English UI string, or the English itself when
        /// the game's table has no row for it.</summary>
        public static string Translate(string english)
        {
            try { return Clean(Translations.Translate(english).GetTranslated()); }
            catch { return english; }
        }
    }
}
