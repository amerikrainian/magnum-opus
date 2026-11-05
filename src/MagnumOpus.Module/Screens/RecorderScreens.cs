using MagnumOpus.Core;
using MagnumOpus.Core.Graph;
using MagnumOpus.Core.Strings;
using MagnumOpus.Module.Game;
using MagnumOpus.Module.Patches;
using MagnumOpus.Module.UI;

namespace MagnumOpus.Module.Screens
{
    /// <summary>
    /// The GIF recorder (SolutionRecorderScreen, from the completion screen's Record GIF): the game's
    /// status line, as it draws it ("Recording your solution...", then "An animated GIF has been
    /// saved to your desktop." or "...to the specified folder.", or its error when a collision stops
    /// the recording), spoken again whenever it changes; then Close (the game's close button, which
    /// answers Escape; closing while recording cancels it, as the game does). The recorded animation
    /// itself is not described.
    /// </summary>
    public sealed class SolutionRecorderGameScreen : GameScreen<SolutionRecorderScreen>
    {
        private string _spoken;

        public override string Key => "game.recorder";

        public override string ScreenName => GameText.Translate("Solution Recorder");

        public override void Build(GraphBuilder b)
        {
            if (Game == null) return;
            b.PushContext(null, null);
            b.AddItem(ControlId.Structural("recorder:status"), Nodes.Text(Status));
            b.AddItem(ControlId.Structural("recorder:close"), Nodes.Button(() => Strings.ModClose, () => GameKeys.Forced = GameKeys.Escape));
            b.PopContext();
        }

        public override void OnPush()
        {
            base.OnPush();
            _spoken = Status();
        }

        // The status line changes by itself (recording finishes, or a collision stops it): say it.
        public override void OnUpdate()
        {
            base.OnUpdate();
            try
            {
                string now = Status();
                if (now == _spoken) return;
                _spoken = now;
                if (!string.IsNullOrEmpty(now)) Speech.Say(now, interrupt: false);
            }
            catch (System.Exception e) { CoreLog.Warning("[recorder] status: " + e.Message); }
        }

        // The game's own line (SolutionRecorderScreen.RenderFrame), without its manual-mode key hints.
        private string Status()
        {
            var g = Game;
            if (g == null) return "";
            if (g.solutionEditor != null && g.solutionEditor.HasError())
                return GameText.Translate("Error") + ": " + GameText.Translate("An impending collision or conflict in your solution has stopped the recording process.");
            if (!g.isStopped) return GameText.Translate("Recording your solution...");
            return GameLogic.instance.settingsData.gifOutputDirectory.Get().Trim().Length <= 0
                ? GameText.Translate("An animated GIF has been saved to your desktop.")
                : GameText.Translate("An animated GIF has been saved to the specified folder.");
        }
    }
}
