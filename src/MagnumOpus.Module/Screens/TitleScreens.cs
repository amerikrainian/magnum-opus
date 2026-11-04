using MagnumOpus.Core.Graph;
using MagnumOpus.Core.Strings;
using MagnumOpus.Module.Input;
using MagnumOpus.Module.Patches;
using MagnumOpus.Module.UI;

namespace MagnumOpus.Module.Screens
{
    /// <summary>
    /// The title screen ("Click to Continue"; the game's TitleScreen advances on a left click or
    /// Escape only). Announced as "Press any key to continue", and any key does: the press is handed
    /// to the game as Escape for one frame (<see cref="GameKeys.Forced"/>), so the game runs its own
    /// path (the title sound, the fade to the puzzle select).
    /// </summary>
    public sealed class TitleGateScreen : GameScreen<TitleScreen>
    {
        public override string Key => "game.title";

        // Set by the press that advanced; the title stays on top a few frames around the fade, and
        // must not come back (and re-announce) meanwhile. Cleared once another screen is on top.
        private static bool s_advanced;

        public override bool IsActive()
        {
            bool top = Game != null;
            if (!top) s_advanced = false;
            return top && !s_advanced && base.IsActive();
        }

        public override void Build(GraphBuilder b)
        {
            b.AddItem(ControlId.Structural("title:continue"), Nodes.Text(() => Strings.PressAnyKey));
        }

        public override void OnUpdate()
        {
            // Our global keys (help, mod menu, focus) and modifier chords stay ours.
            if (SdlKeyboard.CtrlHeld || SdlKeyboard.AltHeld || SdlKeyboard.JustPressed((int)Scancode.F1)) return;
            if (!SdlKeyboard.AnyJustPressed) return;
            GameKeys.Forced = GameKeys.Escape;
            s_advanced = true;
        }

        public override bool PassKeyToGame(int sdlKeycode) => true;
    }
}
