using System.Collections.Generic;
using System.Text;
using MagnumOpus.Core.Graph;
using MagnumOpus.Module.Game;
using MagnumOpus.Module.Patches;
using MagnumOpus.Module.UI;

namespace MagnumOpus.Module.Screens
{
    /// <summary>
    /// Any game screen the mod has no model for yet: announced by its name (the human type name,
    /// "SolutionEditorScreen" read "Solution Editor"), with the text it draws as lines, top to
    /// bottom (<see cref="TextCapture"/>), and every labeled button the game draws on
    /// it as a button of ours, pressed through the game's own press (<see cref="ButtonPress"/>). Never
    /// silent, and operable as far as the screen's text buttons go. Below every modeled screen
    /// (they register their game screen types in GameScreen&lt;T&gt;).
    /// </summary>
    public sealed class FallbackScreen : GameScreen
    {
        public override string Key => "game.unmodeled";
        public override int Layer => -10;

        public override bool IsActive()
        {
            var top = GameScreens.Top;
            bool active = top != null && !GameScreens.InTransition && !IsModeled(top.GetType());
            TextCapture.Enabled = active; // capture costs nothing while a modeled screen is up
            return active;
        }

        public override string ScreenName => Friendly(GameScreens.Name(GameScreens.Top));

        public override void Build(GraphBuilder b)
        {
            var labels = ButtonPress.LastFrame;
            var buttonText = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var l in labels) buttonText.Add(GameText.Clean(l));
            var seen = new Dictionary<string, int>();
            b.PushContext(null, null);
            // The screen's drawn text, top to bottom, minus the button captions offered below.
            var lines = new List<string>();
            foreach (var d in TextCapture.LastFrame)
            {
                string t = GameText.Clean(d.Text);
                if (string.IsNullOrWhiteSpace(t) || buttonText.Contains(t) || lines.Exists(x => string.Equals(x, t, System.StringComparison.OrdinalIgnoreCase))) continue;
                lines.Add(t);
            }
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                b.AddItem(ControlId.Structural("fallback-text:" + i), Nodes.Text(() => line));
            }
            foreach (var raw in labels)
            {
                string label = GameText.Clean(raw);
                if (string.IsNullOrWhiteSpace(label)) continue;
                seen.TryGetValue(label, out int n);
                seen[label] = n + 1;
                string pressed = raw;
                b.AddItem(ControlId.Structural("fallback:" + label + "#" + n), Nodes.Button(() => label, () => ButtonPress.Press(pressed)));
            }
            if (seen.Count == 0 && lines.Count == 0)
                b.AddItem(ControlId.Structural("fallback:name"), Nodes.Text(() => ScreenName));
            b.PopContext();
        }

        /// <summary>"SolutionEditorScreen" -> "Solution Editor"; nested and obfuscated names as they are.</summary>
        public static string Friendly(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return "";
            int slash = typeName.LastIndexOf('/');
            string n = slash >= 0 ? typeName.Substring(slash + 1) : typeName;
            if (n.EndsWith("Screen") && n.Length > 6) n = n.Substring(0, n.Length - 6);
            var sb = new StringBuilder();
            for (int i = 0; i < n.Length; i++)
            {
                if (i > 0 && char.IsUpper(n[i]) && !char.IsUpper(n[i - 1])) sb.Append(' ');
                sb.Append(n[i]);
            }
            return sb.ToString();
        }
    }
}
