using MagnumOpus.Core.Graph;
using MagnumOpus.Core.Strings;
using MagnumOpus.Module.Game;
using MagnumOpus.Module.Patches;
using MagnumOpus.Module.UI;

namespace MagnumOpus.Module.Screens
{
    /// <summary>
    /// A puzzle tip (PuzzleTipScreen, over the editor, titled with the tip's name): the tip's text,
    /// the one the game draws (the dragging tip filled with the drag letters of the player's keyboard
    /// layout, as the game does), then Close. The animated demonstration solution beside the text is
    /// not described. Close and Escape (which passes to the game) close it the game's way: its close
    /// button answers Escape; Enter closes it in the game too.
    /// </summary>
    public sealed class PuzzleTipGameScreen : GameScreen<PuzzleTipScreen>
    {
        public override string Key => "game.tip";

        public override string ScreenName => Game?.tip == null ? null : GameText.Of(Game.tip.name);

        public override void Build(GraphBuilder b)
        {
            var g = Game;
            if (g?.tip == null) return;
            b.PushContext(null, null);
            b.AddItem(ControlId.Structural("tip:text"), Nodes.Text(Text));
            b.AddItem(ControlId.Structural("tip:close"), Nodes.Button(() => Strings.ModClose, () => GameKeys.Forced = GameKeys.Escape));
            b.PopContext();
        }

        private string Text()
        {
            var tip = Game?.tip;
            if (tip == null) return "";
            if (tip != Tips.draggingTip) return GameText.Of(tip.description);
            string Key(char c) => ((char)Keybinds.MapKey((SDL2.SDL.SDLKey)c)).ToString().ToUpperInvariant();
            return GameText.Clean(string.Format(Translations.Translate("Remember that you can press the {0}, {1}, {2}, and {3} keys while dragging parts to change their initial rotation and length. This works in any puzzle, but is especially useful in the restricted space of a production-model transmutation engine.").GetTranslated(),
                Key('w'), Key('a'), Key('s'), Key('d')));
        }
    }
}
