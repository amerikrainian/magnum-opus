using MagnumOpus.Core;
using MagnumOpus.Core.Graph;
using MagnumOpus.Core.Screens;
using MagnumOpus.Core.UI;
using MagnumOpus.Module.Game;
using MagnumOpus.Module.Input;
using MagnumOpus.Module.Patches;
using MagnumOpus.Module.UI;

namespace MagnumOpus.Module.Screens
{
    /// <summary>
    /// A cutscene (the game's CinematicScreen: portraits and one dialogue line at a time; Escape skips
    /// a cutscene already seen). Each line is spoken in full as it appears ("Speaker: line") and is
    /// the screen's one node, so it can be read again.
    ///
    /// ONE PRESS = THE NEXT LINE (Echopunks' rule, its CutsceneAdvance): the game types each line out
    /// a character at a time and takes an advance (click, Tab, Space) only once the whole line shows
    /// plus 0.1 s, and not while a portrait slides in, out or changes. Since the line is already
    /// heard, Enter or Space here finishes the typing and the slide (the game's own clocks,
    /// timeSinceEvent and cinematicsEventTime) and hands the game Space for the frame, so its own
    /// advance runs (sounds, the seen flag, the end of the scene). Escape passes.
    /// </summary>
    public sealed class CinematicGameScreen : GameScreen<CinematicScreen>
    {
        public override string Key => "game.cinematic";

        private CinematicScreen _seenScreen;
        private int _seenEvent = -1;

        public override bool PassKeyToGame(int sdlKeycode) => sdlKeycode == GameKeys.Escape;

        // Space is ours (the advance above); Tab is a navigation key, already kept from the game.
        public override bool ClaimsKey(int sdlKeycode) => sdlKeycode == GameKeys.Space;

        public override void Build(GraphBuilder b)
        {
            b.AddItem(ControlId.Structural("cinematic:line"), new NodeVtable
            {
                Announcements = new[] { Nodes.LabelPart(CurrentLine) },
                OnActivate = Advance,
            });
        }

        public override System.Collections.Generic.IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(EditorActions.Play, MagnumOpus.Core.Strings.Strings.Get("bind.cinematic.next"), _ => Advance());
        }

        private void Advance()
        {
            var g = Game;
            if (g == null) return;
            try
            {
                g.timeSinceEvent = 1000f;       // the typewriter is past the line's end
                if (g.cinematicsEventLength.HasValue())
                    g.cinematicsEventTime = g.cinematicsEventLength.GetValue(); // the slide is done
            }
            catch (System.Exception e) { CoreLog.Warning("[cinematic] advance: " + e.Message); }
            GameKeys.Forced = GameKeys.Space;
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

        // Each new line is spoken as it appears; a scene's first line is not (landing on the node
        // reads it, which said it twice).
        public override void OnUpdate()
        {
            var g = Game;
            if (g == null) return;
            if (!ReferenceEquals(g, _seenScreen)) { _seenScreen = g; _seenEvent = g.currentEvent; return; }
            if (g.currentEvent == _seenEvent) return;
            _seenEvent = g.currentEvent;
            string line = CurrentLine();
            if (line.Length > 0) Speech.Say(line, interrupt: true);
        }
    }
}
