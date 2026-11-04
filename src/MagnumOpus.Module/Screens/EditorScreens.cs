using System;
using System.Collections.Generic;
using System.Linq;
using MagnumOpus.Core;
using MagnumOpus.Core.Graph;
using MagnumOpus.Core.Strings;
using MagnumOpus.Core.UI;
using MagnumOpus.Module.Game;
using MagnumOpus.Module.Patches;
using MagnumOpus.Module.UI;

namespace MagnumOpus.Module.Screens
{
    /// <summary>
    /// The solution editor (SolutionEditorScreen), read-only stage: three Tab stops.
    ///  1. Board: a hex cursor over the game's unbounded board, on Civ V Access's cluster: Q
    ///     northwest, E northeast, A west, D east, Z southwest, C southeast (numpad 7 9 4 6 1 3; Left
    ///     and Right arrows step west and east), Alt with them skips to the next hex that reads
    ///     differently.
    ///     A hex reads its half-step coordinates, then what is on it (<see cref="Board"/>).
    ///  2. Program: the instruction tape, rows = arms (numbered as the game numbers them, the same
    ///     numbers the board uses), columns = cycles (1-based). Up and Down change the arm, Left and
    ///     Right the column, Home and End the row's first and last instruction.
    ///  3. Run log: the current run's events, per cycle (present once a run has produced one).
    /// Everywhere: digits 1-9 jump to that arm (on the board, to its base; on the tape, to its row),
    /// Space plays or pauses, Ctrl+Space steps a cycle, Alt+Space toggles fast forward (the game's
    /// own buttons, through its own keys), Alt+S the score, Alt+T the tutorial tips. On the board
    /// also: ; and ' move the program cursor a cycle back and forward (on the arm under the board
    /// cursor), [ ] categories, , . their items, S details, W the molecule, X the neighbours.
    /// The letters, digits and punctuation are taken from the game while the editor is up
    /// (<see cref="ClaimsKey"/>): its own instruction hotkeys act on the mouse position. Escape
    /// passes to the game: it stops a run, and a stopped editor closes on a second press.
    /// </summary>
    public sealed class SolutionEditorGameScreen : GameScreen<SolutionEditorScreen>
    {
        public const string StopBoard = "editor.board";
        public const string StopProgram = "editor.program";
        public const string StopLog = "editor.log";

        // Action keys (bound in EditorKeys).
        public const string HexNW = "ed.hex.nw", HexNE = "ed.hex.ne", HexW = "ed.hex.w", HexE = "ed.hex.e", HexSW = "ed.hex.sw", HexSE = "ed.hex.se";
        public const string SkipNW = "ed.skip.nw", SkipNE = "ed.skip.ne", SkipW = "ed.skip.w", SkipE = "ed.skip.e", SkipSW = "ed.skip.sw", SkipSE = "ed.skip.se";
        public const string TapePrev = "ed.tape.prev", TapeNext = "ed.tape.next";
        public const string ArmPrefix = "ed.arm.";
        public const string CatPrev = "ed.cat.prev", CatNext = "ed.cat.next", ItemPrev = "ed.item.prev", ItemNext = "ed.item.next";
        public const string Details = "ed.details", MoleculeKey = "ed.molecule", Neighbours = "ed.neighbours";
        public const string Score = "ed.score", Tips = "ed.tips";
        public const string Play = "ed.play", Step = "ed.step", Fast = "ed.fast";

        private const int SkipLimit = 40;    // hexes a skip looks ahead on the unbounded board
        private const int LogShown = 500;    // newest run log lines declared as nodes
        private const int MaxColumn = 9999;

        // The cursors (positions, not game state: re-read against the live game on every use).
        private HexIndex _cursor;
        private int _row, _col;
        private string _category;
        private int _item = -1;

        // Change watchers for what the editor speaks by itself.
        private SimPlayState _lastState = SimPlayState.Stopped;
        private bool _lastFast;
        private bool _wasPlaying; // the game pauses through a step: "paused" is said when it lands
        private SimError _spokenError;
        private bool _tipsPending;

        public override string Key => "game.editor";

        public override string ScreenName
        {
            get
            {
                var s = Game?.GetSolution();
                return s == null ? null : GameText.Of(s.GetPuzzle().puzzleName);
            }
        }

        public override bool AllowsTypeahead => false;

        public override object InitialFocusStop => StopBoard;

        public override bool ClaimsKey(int k) =>
            (k >= 32 && k <= 126) || k == GameKeys.Delete || (k >= 0x40000054 && k <= 0x40000063);

        public override void OnPush()
        {
            _cursor = new HexIndex(0, 0);
            _row = 0;
            _col = 0;
            _category = null;
            _item = -1;
            _spokenError = null;
            _lastState = Game != null ? Game.GetSimPlayState() : SimPlayState.Stopped;
            _lastFast = Game != null && Game.isFastForward;
            _tipsPending = true;
        }

        public override void OnFocus()
        {
            base.OnFocus();
            if (!_tipsPending) return;
            _tipsPending = false;
            // After the landing is read (the navigator speaks it this frame).
            Later.Frames(2, () => { string t = TipsText(); if (t != null) Speech.Say(t); }, "editor tips");
        }

        public override void OnUpdate()
        {
            var ed = Game;
            if (ed == null) return;
            try { WatchRunState(ed); WatchError(ed); }
            catch (Exception e) { CoreLog.Warning("[editor] update: " + e.Message); }
        }

        // ---- the stops ----

        public override void Build(GraphBuilder b)
        {
            var ed = Game;
            if (ed == null || ed.GetSolution() == null) return;

            b.BeginStop(StopBoard);
            b.PushContext(Strings.EditorBoard, null, positions: false);
            b.AddItem(ControlId.Structural("editor:board"), Nodes.Text(() => Game == null ? "" : Board.Readout(Game, _cursor)));
            b.PopContext();

            if (Board.Arms(ed).Count > 0)
            {
                b.BeginStop(StopProgram);
                b.PushContext(Strings.EditorProgram, null, positions: false);
                b.AddItem(ControlId.Structural("editor:program"), Nodes.Text(() => TapeText(withArm: true)));
                b.PopContext();
            }

            var log = RunLog.Entries;
            if (log.Count > 0)
            {
                b.BeginStop(StopLog);
                b.PushContext(Strings.EditorLog, Strings.RoleList);
                for (int i = Math.Max(0, log.Count - LogShown); i < log.Count; i++)
                {
                    string line = Strings.EditorLogEntry(log[i].Cycle, log[i].Text);
                    b.AddItem(ControlId.Structural("editor:log:" + i), Nodes.Text(() => line));
                }
                b.PopContext();
            }
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            var ed = Game;
            if (ed == null) yield break;
            object stop = Navigation.FocusedStopKey;

            yield return new ElementAction(Play, Strings.Get("bind.ed.play"), _ => RunKey(GameKeys.Space));
            yield return new ElementAction(Step, Strings.Get("bind.ed.step"), _ => RunKey(GameKeys.Tab));
            yield return new ElementAction(Fast, Strings.Get("bind.ed.fast"), _ => RunKey(GameKeys.Space));
            yield return new ElementAction(Score, Strings.Get("bind.ed.score"), _ => Speech.Say(ScoreText(), interrupt: true));
            if (TipsText() != null)
                yield return new ElementAction(Tips, Strings.Get("bind.ed.tips"), _ => Speech.Say(TipsText(), interrupt: true));

            int arms = Board.Arms(ed).Count;
            for (int n = 1; n <= Math.Min(9, arms); n++)
            {
                int arm = n;
                yield return new ElementAction(ArmPrefix + n, Strings.Get("bind.ed.arm"), _ => JumpToArm(arm, stop));
            }

            if (Equals(stop, StopBoard))
            {
                yield return new ElementAction(HexNW, Strings.Get("bind.ed.hex.nw"), _ => Move(HexDir.NorthWest));
                yield return new ElementAction(HexNE, Strings.Get("bind.ed.hex.ne"), _ => Move(HexDir.NorthEast));
                yield return new ElementAction(HexW, Strings.Get("bind.ed.hex.w"), _ => Move(HexDir.West));
                yield return new ElementAction(HexE, Strings.Get("bind.ed.hex.e"), _ => Move(HexDir.East));
                yield return new ElementAction(HexSW, Strings.Get("bind.ed.hex.sw"), _ => Move(HexDir.SouthWest));
                yield return new ElementAction(HexSE, Strings.Get("bind.ed.hex.se"), _ => Move(HexDir.SouthEast));
                yield return new ElementAction(UiActions.Left, Strings.Get("bind.ed.hex.w"), _ => Move(HexDir.West));
                yield return new ElementAction(UiActions.Right, Strings.Get("bind.ed.hex.e"), _ => Move(HexDir.East));
                yield return new ElementAction(SkipNW, Strings.Get("bind.ed.skip.nw"), _ => Skip(HexDir.NorthWest));
                yield return new ElementAction(SkipNE, Strings.Get("bind.ed.skip.ne"), _ => Skip(HexDir.NorthEast));
                yield return new ElementAction(SkipW, Strings.Get("bind.ed.skip.w"), _ => Skip(HexDir.West));
                yield return new ElementAction(SkipE, Strings.Get("bind.ed.skip.e"), _ => Skip(HexDir.East));
                yield return new ElementAction(SkipSW, Strings.Get("bind.ed.skip.sw"), _ => Skip(HexDir.SouthWest));
                yield return new ElementAction(SkipSE, Strings.Get("bind.ed.skip.se"), _ => Skip(HexDir.SouthEast));
                if (arms > 0)
                {
                    yield return new ElementAction(TapePrev, Strings.Get("bind.ed.tape.prev"), _ => TapeFromBoard(-1));
                    yield return new ElementAction(TapeNext, Strings.Get("bind.ed.tape.next"), _ => TapeFromBoard(1));
                }
                yield return new ElementAction(CatPrev, Strings.Get("bind.ed.cat.prev"), _ => Category(-1));
                yield return new ElementAction(CatNext, Strings.Get("bind.ed.cat.next"), _ => Category(1));
                yield return new ElementAction(ItemPrev, Strings.Get("bind.ed.item.prev"), _ => Item(-1));
                yield return new ElementAction(ItemNext, Strings.Get("bind.ed.item.next"), _ => Item(1));
                yield return new ElementAction(Details, Strings.Get("bind.ed.details"), _ => SayIf(string.Join(" ", Board.Details(Game, _cursor))));
                yield return new ElementAction(MoleculeKey, Strings.Get("bind.ed.molecule"), _ => SayIf(Board.MoleculeAround(Game, _cursor)));
                yield return new ElementAction(Neighbours, Strings.Get("bind.ed.neighbours"), _ => SayIf(NeighboursText()));
            }
            else if (Equals(stop, StopProgram) && arms > 0)
            {
                yield return new ElementAction(UiActions.Up, Strings.Get("bind.ui.up"), _ => TapeRow(-1));
                yield return new ElementAction(UiActions.Down, Strings.Get("bind.ui.down"), _ => TapeRow(1));
                yield return new ElementAction(UiActions.Left, Strings.Get("bind.ui.left"), _ => TapeColumn(_col - 1));
                yield return new ElementAction(UiActions.Right, Strings.Get("bind.ui.right"), _ => TapeColumn(_col + 1));
                yield return new ElementAction(UiActions.Home, Strings.Get("bind.ui.home"), _ => TapeEdge(first: true));
                yield return new ElementAction(UiActions.End, Strings.Get("bind.ui.end"), _ => TapeEdge(first: false));
                yield return new ElementAction(Details, Strings.Get("bind.ed.details"), _ =>
                {
                    var ins = CellInstruction();
                    if (ins != null) Speech.Say(Instructions.Details(ins), interrupt: true);
                });
            }
        }

        private static void SayIf(string text)
        {
            if (!string.IsNullOrEmpty(text)) Speech.Say(text, interrupt: true);
        }

        // ---- board ----

        private void SayBoard() { var ed = Game; if (ed != null) Speech.Say(Board.Readout(ed, _cursor), interrupt: true); }

        private void Move(HexDir dir)
        {
            _cursor = Hex.Step(_cursor, dir);
            SayBoard();
        }

        // To the next hex that way whose contents read differently; nothing within reach: silent.
        private void Skip(HexDir dir)
        {
            var ed = Game;
            if (ed == null) return;
            var world = Board.WorldAtoms(ed);
            string here = string.Join("|", Board.Contents(ed, _cursor, world));
            for (int i = 1; i <= SkipLimit; i++)
            {
                var h = Hex.Step(_cursor, dir, i);
                if (string.Join("|", Board.Contents(ed, h, world)) == here) continue;
                _cursor = h;
                SayBoard();
                return;
            }
        }

        private string NeighboursText()
        {
            var ed = Game;
            if (ed == null) return null;
            var world = Board.WorldAtoms(ed);
            var parts = new List<string>();
            foreach (var dir in Hex.All)
            {
                var c = Board.Contents(ed, Hex.Step(_cursor, dir), world);
                if (c.Count > 0) parts.Add(Strings.EditorNeighbour(Hex.Word(dir), string.Join(", ", c)));
            }
            return string.Join("; ", parts);
        }

        private void JumpToArm(int n, object stop)
        {
            var ed = Game;
            if (ed == null) return;
            var arms = Board.Arms(ed);
            if (n < 1 || n > arms.Count) return;
            _row = n - 1;
            if (Equals(stop, StopProgram)) { Speech.Say(TapeText(withArm: true), interrupt: true); return; }
            _cursor = Board.BaseHex(ed, arms[n - 1]);
            if (Equals(stop, StopBoard)) SayBoard();
        }

        // The arm under the board cursor (its base or a gripper), 0-based, or -1.
        private int ArmUnderCursor(SolutionEditorScreen ed)
        {
            var arms = Board.Arms(ed);
            for (int i = 0; i < arms.Count; i++)
                if (Board.BaseHex(ed, arms[i]) == _cursor || Board.Grippers(ed, arms[i]).Any(g => g.Key == _cursor)) return i;
            return -1;
        }

        private void TapeFromBoard(int step)
        {
            var ed = Game;
            if (ed == null) return;
            int arm = ArmUnderCursor(ed);
            if (arm >= 0) _row = arm;
            int col = Math.Max(0, Math.Min(MaxColumn, _col + step));
            if (col == _col && arm < 0) return;
            _col = col;
            Speech.Say(TapeText(withArm: true), interrupt: true);
        }

        // ---- categories ----

        private struct CatItem { public HexIndex Hex; }

        private List<KeyValuePair<string, List<CatItem>>> Categories(SolutionEditorScreen ed)
        {
            var cats = new List<KeyValuePair<string, List<CatItem>>>();
            var solution = ed.GetSolution();
            void Add(string key, IEnumerable<HexIndex> hexes)
            {
                var items = hexes.Select(h => new CatItem { Hex = h }).ToList();
                if (items.Count > 0) cats.Add(new KeyValuePair<string, List<CatItem>>(key, items));
            }
            Add("reagents", solution.parts.Where(p => p.GetType().isInput).Select(p => p.GetHexPos()));
            Add("products", solution.parts.Where(p => p.GetType().IsOutput()).Select(p => p.GetHexPos()));
            Add("arms", Board.Arms(ed).Select(p => Board.BaseHex(ed, p)));
            Add("glyphs", solution.parts.Where(p => p.GetType().glyphHexes.Length > 0).Select(p => p.GetHexPos()));
            Add("tracks", solution.parts.Where(p => p.GetType().isTrack).Select(p => p.GetHexPos()));
            if (Board.Running(ed))
                Add("atoms", ed.GetSimulation().GetMolecules().Where(m => m.GetAtoms().Count > 0)
                    .Select(m => m.GetAtoms().Keys.OrderBy(h => h.R).ThenBy(h => h.Q).First()));
            return cats;
        }

        private void Category(int step)
        {
            var ed = Game;
            if (ed == null) return;
            var cats = Categories(ed);
            if (cats.Count == 0) return;
            int at = cats.FindIndex(c => c.Key == _category);
            at = at < 0 ? (step > 0 ? 0 : cats.Count - 1) : ((at + step) % cats.Count + cats.Count) % cats.Count;
            _category = cats[at].Key;
            _item = -1;
            Speech.Say(Strings.EditorCategory(Strings.EditorCategoryName(_category), cats[at].Value.Count), interrupt: true);
        }

        private void Item(int step)
        {
            var ed = Game;
            if (ed == null) return;
            var cats = Categories(ed);
            if (cats.Count == 0) return;
            int at = cats.FindIndex(c => c.Key == _category);
            if (at < 0) { at = 0; _category = cats[0].Key; _item = -1; }
            var items = cats[at].Value;
            _item = _item < 0 ? (step > 0 ? 0 : items.Count - 1) : ((_item + step) % items.Count + items.Count) % items.Count;
            _cursor = items[_item].Hex;
            SayBoard();
        }

        // ---- program ----

        private Part RowArm(SolutionEditorScreen ed)
        {
            var arms = Board.Arms(ed);
            if (arms.Count == 0) return null;
            _row = Math.Max(0, Math.Min(arms.Count - 1, _row));
            return arms[_row];
        }

        private InstructionType CellInstruction()
        {
            var ed = Game;
            var arm = ed == null ? null : RowArm(ed);
            if (arm == null) return null;
            var ins = arm.program.GetInstruction(_col);
            return ins.HasValue() ? ins.GetValue() : null;
        }

        private string TapeText(bool withArm)
        {
            var ed = Game;
            var arm = ed == null ? null : RowArm(ed);
            if (arm == null) return "";
            var ins = CellInstruction();
            string label = ins != null ? Instructions.Label(ins) : ExpansionLabel(ed, arm, _col);
            string cell = label != null ? Strings.EditorCell(_col + 1, label) : (_col + 1).ToString();
            return withArm ? Strings.EditorArm(_row + 1) + ", " + cell : cell;
        }

        // A cell a Reset or Repeat fills when the program is compiled (the game draws a line through
        // them): what runs there, and the instruction it comes from. Null for a plain empty cell.
        private static string ExpansionLabel(SolutionEditorScreen ed, Part arm, int col)
        {
            var grid = CompiledProgramGrid.FromSolution(ed.GetSolution());
            if (!grid.HasValue()) return null;
            var compiled = grid.GetValue().GetProgramForPart(arm);
            if (compiled == null || compiled.instructions == null) return null;
            int i = col - compiled.startIndex;
            if (i < 0 || i >= compiled.instructions.Length || !compiled.instructions[i].isExpansion) return null;
            var type = compiled.instructions[i].type;
            if (ReferenceEquals(type, null) || type == InstructionTypes.idle || type == InstructionTypes.placeholderIdle) return null;
            for (int c = col - 1; c >= arm.program.FirstInstructionIndex(); c--)
            {
                var source = arm.program.GetInstruction(c);
                if (source.HasValue()) return Strings.EditorExpansion(Instructions.Label(type), Instructions.Label(source.GetValue()));
            }
            return Instructions.Label(type);
        }

        private void TapeRow(int step)
        {
            var ed = Game;
            if (ed == null) return;
            int count = Board.Arms(ed).Count;
            int row = Math.Max(0, Math.Min(count - 1, _row + step));
            if (row == _row) return;
            _row = row;
            Speech.Say(TapeText(withArm: true), interrupt: true);
        }

        private void TapeColumn(int col)
        {
            col = Math.Max(0, Math.Min(MaxColumn, col));
            if (col == _col) return;
            _col = col;
            Speech.Say(TapeText(withArm: false), interrupt: true);
        }

        private void TapeEdge(bool first)
        {
            var ed = Game;
            var arm = ed == null ? null : RowArm(ed);
            if (arm == null) return;
            var program = arm.program;
            int col = program.Count() == 0 ? 0 : (first ? program.FirstInstructionIndex() : program.LastInstructionIndex());
            TapeColumn(col);
        }

        // ---- the engine ----

        // Space, Tab (step): the game's own simulation buttons, through their own keys. Where the
        // game would refuse, its reason is spoken instead of nothing happening.
        private void RunKey(int gameKey)
        {
            var ed = Game;
            if (ed == null) return;
            if (ed.GetSimPlayState() == SimPlayState.Stopped)
            {
                string reason = StartRefusal(ed);
                if (reason != null) { Speech.Say(reason, interrupt: true); return; }
            }
            else if (ed.GetSimulationError().HasValue() && ed.GetCycleTime() >= ed.GetSimulationError().GetValue().errorTime)
            {
                Speech.Say(ErrorText(ed, ed.GetSimulationError().GetValue()), interrupt: true);
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

        // The game shows a run's error once the animation reaches it: said then, logged, and the
        // board cursor put on its first hex.
        private void WatchError(SolutionEditorScreen ed)
        {
            var error = ed.GetSimulationError();
            if (!error.HasValue()) { _spokenError = null; return; }
            var e = error.GetValue();
            if (ReferenceEquals(e, _spokenError) || ed.GetCycleTime() < e.errorTime) return;
            _spokenError = e;
            string text = ErrorText(ed, e);
            Speech.Say(text, interrupt: true);
            RunLog.Add(ed.GetCycle() + 1, text);
            var hexes = ErrorHexes(e);
            if (hexes.Count > 0) _cursor = hexes[0];
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

        private static string ErrorText(SolutionEditorScreen ed, SimError e)
        {
            string message = GameText.Clean(e.errorMessage);
            var hexes = ErrorHexes(e);
            if (hexes.Count == 0) return message;
            string where = Hex.Coords(hexes[0]);
            for (int i = 1; i < hexes.Count; i++) where = Strings.RunErrorPlaces(where, Hex.Coords(hexes[i]));
            return Strings.RunError(message, where);
        }

        // ---- score and tips ----

        // The figures the game shows above the program, with its own labels; the ones it shows as
        // dashes (before a run) are left out.
        private string ScoreText()
        {
            var ed = Game;
            if (ed == null) return "";
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
            // The game's font draws '$' as its gold mark.
            string cost = GameText.Clean(UIUtils.FormatMetricScore(ScoreMetric.Cost, solution.GetTotalCost())).Replace("$", Strings.EditorGoldMark);
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

        // The puzzle's sticky notes the game is showing (the tutorial's tips), or null.
        private string TipsText()
        {
            var ed = Game;
            var solution = ed?.GetSolution();
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
}
