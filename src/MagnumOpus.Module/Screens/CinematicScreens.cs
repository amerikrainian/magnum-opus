using MagnumOpus.Core;
using MagnumOpus.Core.Graph;
using MagnumOpus.Module.Game;
using MagnumOpus.Module.Patches;
using MagnumOpus.Module.UI;

namespace MagnumOpus.Module.Screens
{
    /// <summary>
    /// A cutscene (the game's CinematicScreen: portraits and one dialogue line at a time, advanced by
    /// a click, Tab or Space once the line is ready; Escape skips a cutscene already seen). Each line
    /// is spoken as it appears ("Speaker: line") and is the screen's one node, so it can be read
    /// again. Enter advances too (handed to the game as Space); Tab and Space reach the game as they
    /// are; Escape passes.
    /// </summary>
    public sealed class CinematicGameScreen : GameScreen<CinematicScreen>
    {
        public override string Key => "game.cinematic";

        private CinematicScreen _seenScreen;
        private int _seenEvent = -1;

        public override bool PassKeyToGame(int sdlKeycode) => sdlKeycode == GameKeys.Escape || sdlKeycode == GameKeys.Tab;

        public override void Build(GraphBuilder b)
        {
            b.AddItem(ControlId.Structural("cinematic:line"), new NodeVtable
            {
                Announcements = new[] { Nodes.LabelPart(CurrentLine) },
                OnActivate = () => GameKeys.Forced = GameKeys.Space,
            });
        }

        private string CurrentLine()
        {
            var g = Game;
            if (g == null) return "";
            var events = g.vignette.GetVignette().events[0];
            if (g.currentEvent < 0 || g.currentEvent >= events.Count) return "";
            var e = events[g.currentEvent];
            if (!e.IsLineEvent()) return "";
            var line = e.ToLineEvent();
            return GameText.Of(line.portrait.name) + ": " + GameText.Clean(line.line);
        }

        public override void OnUpdate()
        {
            var g = Game;
            if (g == null) return;
            if (!ReferenceEquals(g, _seenScreen)) { _seenScreen = g; _seenEvent = -1; }
            if (g.currentEvent == _seenEvent) return;
            _seenEvent = g.currentEvent;
            string line = CurrentLine();
            if (line.Length > 0) Speech.Say(line, interrupt: true);
        }
    }
}
