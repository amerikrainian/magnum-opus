using MagnumOpus.Core.Graph;
using MagnumOpus.Core.Strings;
using MagnumOpus.Module.Game;
using MagnumOpus.Module.Patches;
using MagnumOpus.Module.UI;

namespace MagnumOpus.Module.Screens
{
    /// <summary>
    /// A letter or document (the game's DocumentScreen: the vignette's text split on '#' into
    /// sections drawn over a paper texture). Each section is a line, the first one landed on; Close
    /// (or Escape, which passes to the game) closes it the game's way: a click or Escape there marks
    /// the vignette seen and pops the screen.
    /// </summary>
    public sealed class DocumentGameScreen : GameScreen<DocumentScreen>
    {
        public override string Key => "game.document";

        public override void Build(GraphBuilder b)
        {
            var g = Game;
            if (g == null) return;
            var sections = g.sections;
            b.PushContext(null, null);
            for (int i = 0; i < sections.Length; i++)
            {
                int index = i;
                if (string.IsNullOrWhiteSpace(sections[i])) continue;
                b.AddItem(ControlId.Structural("doc:" + i), Nodes.Text(() => Section(index)));
            }
            b.AddItem(ControlId.Structural("doc:close"), Nodes.Button(() => Strings.ModClose, Close));
            b.PopContext();
        }

        private string Section(int i)
        {
            var g = Game;
            return g == null || i >= g.sections.Length ? "" : GameText.Clean(g.sections[i]);
        }

        // The game's own close: Escape for one frame.
        private static void Close() => GameKeys.Forced = GameKeys.Escape;
    }
}
