using System;
using System.Collections.Generic;
using System.Linq;
using MagnumOpus.Core;
using MagnumOpus.Core.Graph;
using MagnumOpus.Core.Screens;
using MagnumOpus.Core.Strings;
using MagnumOpus.Core.UI;
using MagnumOpus.Module.Game;
using MagnumOpus.Module.Input;
using MagnumOpus.Module.Patches;
using MagnumOpus.Module.UI;

namespace MagnumOpus.Module.Screens.Editor
{
    /// <summary>
    /// The engine, from anywhere in the editor (no nodes): Space plays or pauses, Ctrl+Space steps a
    /// cycle, Alt+Space toggles fast forward (the game's own buttons through their own keys, so its
    /// exact path runs; where it would refuse, its reason is spoken). The engine's state changes are
    /// spoken, and an error when the game shows it (message and hexes; the board cursor goes there).
    /// Ctrl+Shift+S reads the score line, Ctrl+Shift+T the puzzle's tips (also spoken on entry), Alt+P
    /// each product's count. Fast forward says nothing; the run log keeps every event.
    /// </summary>
    internal sealed class RunSection : ScreenSection
    {
        private readonly EditorContext _ctx;
        private SimPlayState _lastState = SimPlayState.Stopped;
        private bool _lastFast, _wasPlaying;
        private SimError _spokenError;
        private bool _closeArmed;

        public RunSection(EditorContext ctx) { _ctx = ctx; }

        public void Reset()
        {
            var ed = _ctx.Ed;
            _lastState = ed != null ? ed.GetSimPlayState() : SimPlayState.Stopped;
            _lastFast = ed != null && ed.isFastForward;
            _wasPlaying = false;
            _spokenError = null;
            _closeArmed = false;
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            if (_ctx.Ed == null) yield break;
            yield return new ElementAction(EditorActions.Play, Strings.Get("bind.ed.play"), _ => RunKey(GameKeys.Space));
            yield return new ElementAction(EditorActions.Step, Strings.Get("bind.ed.step"), _ => RunKey(GameKeys.Tab));
            yield return new ElementAction(EditorActions.Fast, Strings.Get("bind.ed.fast"), _ => RunKey(GameKeys.Space));
            yield return new ElementAction(EditorActions.Score, Strings.Get("bind.ed.score"), _ => EditorContext.Say(ScoreText()));
            yield return new ElementAction(EditorActions.Products, Strings.Get("bind.ed.products"), _ => EditorContext.Say(ProductsText()));
            if (TipsText() != null)
                yield return new ElementAction(EditorActions.Tips, Strings.Get("bind.ed.tips"), _ => EditorContext.Say(TipsText()));
            // The toolbar's Show Diagram (also a button atop the parts tray).
            yield return new ElementAction(EditorActions.Diagram, Strings.Get("bind.ed.diagram"), _ =>
            {
                if (EditorTools.DiagramEnabled(_ctx.Ed)) EditorTools.ShowDiagram(_ctx.Ed);
                else EditorContext.Say(Strings.EditorStopFirst);
            });
        }

        public override void OnUpdate()
        {
            var ed = _ctx.Ed;
            if (ed == null) return;
            try { WatchRunState(ed); WatchError(ed); WatchClose(ed); }
            catch (Exception e) { CoreLog.Warning("[editor] update: " + e.Message); }
        }

        // Space, Tab (step): the game's simulation buttons. Not while a part is out of the solution.
        private void RunKey(int gameKey)
        {
            var ed = _ctx.Ed;
            if (ed == null || _ctx.Busy()) return;
            if (ed.GetSimPlayState() == SimPlayState.Stopped)
            {
                string reason = StartRefusal(ed);
                if (reason != null) { EditorContext.Say(reason); return; }
            }
            else if (ed.GetSimulationError().HasValue() && ed.GetCycleTime() >= ed.GetSimulationError().GetValue().errorTime)
            {
                EditorContext.Say(ErrorText(ed.GetSimulationError().GetValue()));
                return;
            }
            GameKeys.Forced = gameKey;
        }

        private static string StartRefusal(SolutionEditorScreen ed)
        {
            var solution = ed.GetSolution();
            if (solution.parts.Count(p => p.GetType().IsOutput()) < solution.GetPuzzle().outputs.Length)
                return GameText.Translate("All products must be placed.");
            if (!Sim.ConstructSimulation(ed).HasValue())
                return GameText.Translate("Instruction errors must be fixed.");
            return null;
        }

        // The game closes a stopped editor on a SECOND Escape: the first slides its close button out
        // for two seconds (closeEditorMeter, also set while the mouse rests on the button) and only
        // sounds it. Infinity marks the editor closing.
        private void WatchClose(SolutionEditorScreen ed)
        {
            float meter = ed.closeEditorMeter;
            bool armed = meter > 0f && !float.IsInfinity(meter);
            if (armed && !_closeArmed) Speech.Say(Strings.EditorCloseArmed);
            _closeArmed = armed;
        }

        private void WatchRunState(SolutionEditorScreen ed)
        {
            var state = ed.GetSimPlayState();
            bool fast = ed.isFastForward;
            if (state == _lastState && fast == _lastFast) return;
            if (state == SimPlayState.Playing && (_lastState != SimPlayState.Playing || fast != _lastFast))
            {
                Speech.Say(fast ? Strings.RunFast : Strings.RunRunning);
                _wasPlaying = true;
            }
            else if (state == SimPlayState.Stopped && _lastState != SimPlayState.Stopped)
            {
                Speech.Say(Strings.RunStopped);
                _wasPlaying = false;
            }
            else if (state == SimPlayState.Paused && _wasPlaying)
            {
                // Space pauses through a step (the cycle in flight finishes first).
                Speech.Say(Strings.RunPaused);
                _wasPlaying = false;
            }
            _lastState = state;
            _lastFast = fast;
        }

        private void WatchError(SolutionEditorScreen ed)
        {
            var error = ed.GetSimulationError();
            if (!error.HasValue()) { _spokenError = null; return; }
            var e = error.GetValue();
            if (ReferenceEquals(e, _spokenError) || ed.GetCycleTime() < e.errorTime) return;
            _spokenError = e;
            string text = ErrorText(e);
            Speech.Say(text, interrupt: true);
            RunLog.Add(ed.GetCycle() + 1, text);
            var hexes = ErrorHexes(e);
            if (hexes.Count > 0 && hexes[0] != _ctx.Cursor)
            {
                // The board cursor goes to the error; when it actually moves and the board is where
                // the player is, the new hex is read after the message.
                _ctx.Cursor = hexes[0];
                if (Equals(Navigation.FocusedStopKey, EditorContext.StopBoard))
                    Speech.Say(Board.Readout(ed, _ctx.Cursor), interrupt: false);
            }
        }

        private static List<HexIndex> ErrorHexes(SimError e)
        {
            var hexes = new List<HexIndex>();
            if (e.locations == null) return hexes;
            foreach (var p in e.locations)
            {
                var h = HexGrid.standardGrid.GetHexOffset(p, Vector2.Zero);
                if (!hexes.Contains(h)) hexes.Add(h);
            }
            return hexes;
        }

        internal static string ErrorText(SimError e)
        {
            string message = GameText.Clean(e.errorMessage);
            var hexes = ErrorHexes(e);
            if (hexes.Count == 0) return message;
            string where = Hex.Coords(hexes[0]);
            for (int i = 1; i < hexes.Count; i++) where = Strings.RunErrorPlaces(where, Hex.Coords(hexes[i]));
            return Strings.RunError(message, where);
        }

        // Each product with where it is (two can share a name) and how many of the puzzle's count
        // are done ("Product (Elemental Salt) at 4, 0: 6 of 6"); before a run every count is 0.
        private string ProductsText()
        {
            var ed = _ctx.Ed;
            var solution = ed.GetSolution();
            var lines = new List<string>();
            foreach (var p in solution.parts.Where(p => p.GetType().IsOutput()).OrderBy(p => p.GetInputOutputIndex()))
            {
                int done = Board.Running(ed) ? Math.Min(ed.GetSimulation().GetSimState(p).outputDone, p.GetOutputCount()) : 0;
                string name = Strings.EditorPartAt(Board.PartName(ed, p), Hex.Coords(p.GetHexPos()));
                lines.Add(Strings.EventProduct(name, done, p.GetOutputCount()));
            }
            return lines.Count == 0 ? Strings.EditorNoProducts : string.Join(". ", lines);
        }

        // The figures above the program, with the game's labels; dashes (before a run) left out.
        private string ScoreText()
        {
            var ed = _ctx.Ed;
            var solution = ed.GetSolution();
            var parts = new List<string>();
            bool running = Board.Running(ed);
            if (running)
            {
                int done = 0, total = 0;
                foreach (var p in solution.parts.Where(p => p.GetType().IsOutput()))
                {
                    done += Math.Min(ed.GetSimulation().GetSimState(p).outputDone, p.GetOutputCount());
                    total += p.GetOutputCount();
                }
                parts.Add(Strings.EditorScoreItem(GameText.Translate("Products"), done + "/" + total));
            }
            string cost = GameText.Cost(solution.GetTotalCost());
            parts.Add(Strings.EditorScoreItem(GameText.Translate("Cost"), cost));
            if (running)
            {
                int cycles = ed.cycleLength.HasValue() ? ed.cycleLength.GetValue() : ed.GetCycle();
                parts.Add(Strings.EditorScoreItem(GameText.Translate("Cycles"), cycles.ToString()));
            }
            if (solution.GetPuzzle().productionInfo.HasValue())
            {
                var sim = Sim.ConstructSimulation(ed);
                if (sim.HasValue())
                    parts.Add(Strings.EditorScoreItem(GameText.Translate("Instrs"), sim.GetValue().GetProgram().TotalInstructions().ToString()));
            }
            else if (running)
            {
                int area = ed.footprint.HasValue() ? ed.footprint.GetValue() : ed.GetOccupiedHexes();
                parts.Add(Strings.EditorScoreItem(GameText.Translate("Area"), area.ToString()));
            }
            return string.Join(", ", parts);
        }

        /// <summary>The puzzle's sticky notes the game is showing (the tutorial's tips), or null.</summary>
        public string TipsText()
        {
            var solution = _ctx.Ed?.GetSolution();
            if (solution == null) return null;
            var notes = solution.GetPuzzle().notes;
            if (notes == null || notes.Length == 0) return null;
            int inputs = solution.parts.Count(p => p.GetType().isInput);
            int outputs = solution.parts.Count(p => p.GetType().IsOutput());
            var lines = new List<string>();
            foreach (var note in notes)
            {
                if (note.visibilityMode == NoteVisibilityMode.NoInputOutput && inputs >= 1 && outputs >= 1) continue;
                foreach (var point in note.points) lines.Add(GameText.Of(point));
            }
            return lines.Count == 0 ? null : string.Join(" ", lines);
        }
    }

    /// <summary>
    /// The error panel (SolutionEditorScreen.RenderErrorPanel), a stop while the game shows it: the
    /// game's word "Error" with its message and the hexes, then Replay (the failing cycle played
    /// again up to the error: cycleTime runs within a cycle and the error time is a fraction of one;
    /// disabled when the error is at the cycle's start, as the game has it) and Okay (stops the
    /// engine). The game hides the panel while the replay runs; the stop stays, so focus does not
    /// drop to the board. The game draws those two buttons through a private builder the button
    /// patch does not see, so they run the game's own click code (ResetCycleTime / StopSim and their
    /// sounds).
    /// </summary>
    internal sealed class ErrorSection : ScreenSection
    {
        public const string Stop = "editor.error";
        private readonly EditorContext _ctx;

        public ErrorSection(EditorContext ctx) { _ctx = ctx; }

        private SimError _reached; // the error the run reached (kept through a replay of its cycle)

        private SimError Shown()
        {
            var ed = _ctx.Ed;
            if (ed == null || ed.GetSimPlayState() == SimPlayState.Stopped) { _reached = null; return null; }
            var error = ed.GetSimulationError();
            if (!error.HasValue()) { _reached = null; return null; }
            var e = error.GetValue();
            if (ed.GetCycleTime() >= e.errorTime) _reached = e;
            return ReferenceEquals(e, _reached) ? e : null;
        }

        public override void Build(GraphBuilder b)
        {
            if (Shown() == null) return;
            b.BeginStop(Stop);
            b.PushContext(GameText.Translate("Error"), null, positions: false);
            b.AddItem(ControlId.Structural("editor:error:text"), Nodes.Text(() => { var e = Shown(); return e == null ? "" : RunSection.ErrorText(e); }));
            b.AddItem(ControlId.Structural("editor:error:replay"), Nodes.Button(() => GameText.Translate("Replay"), Replay,
                enabled: () => { var e = Shown(); return e != null && e.errorTime > 0f; }));
            b.AddItem(ControlId.Structural("editor:error:okay"), Nodes.Button(() => GameText.Translate("Okay"), Okay));
            b.PopContext();
        }

        private void Replay()
        {
            var ed = _ctx.Ed;
            if (Shown() == null) return;
            ed.ResetCycleTime();
            Assets.sounds.click_button.PlaySound(1f);
        }

        private void Okay()
        {
            var ed = _ctx.Ed;
            if (Shown() == null) return;
            ed.StopSim();
            Assets.sounds.sim_stop.PlaySound(1f);
        }
    }

    /// <summary>The run log stop: the current run's events, oldest first (the newest
    /// <see cref="Shown"/> as nodes).</summary>
    internal sealed class LogSection : ScreenSection
    {
        private const int Shown = 500;

        public override void Build(GraphBuilder b)
        {
            var log = RunLog.Entries;
            if (log.Count == 0) return;
            b.BeginStop(EditorContext.StopLog);
            b.PushContext(Strings.EditorLog, Strings.RoleList);
            for (int i = Math.Max(0, log.Count - Shown); i < log.Count; i++)
            {
                string line = Strings.EditorLogEntry(log[i].Cycle, log[i].Text);
                b.AddItem(ControlId.Structural("editor:log:" + i), Nodes.Text(() => line));
            }
            b.PopContext();
        }
    }
}
