using System;
using System.Collections.Generic;
using System.Linq;
using MagnumOpus.Core.Graph;
using MagnumOpus.Core.Screens;
using MagnumOpus.Core.Strings;
using MagnumOpus.Core.UI;
using MagnumOpus.Module.Game;
using MagnumOpus.Module.Input;
using MagnumOpus.Module.UI;

namespace MagnumOpus.Module.Screens.Editor
{
    /// <summary>
    /// The program stop: the instruction tape, rows = arms (the game's numbering, which the board
    /// shares), columns = cycles, 1-based. Up / Down change the arm, Left / Right the column, Home /
    /// End go to the row's first and last instruction; a cell a Reset or Repeat fills reads what runs
    /// there ("clockwise, from reset").
    /// Programming, typed like text (user decision 2026-11-04): an instruction key (the game's own,
    /// on its layout) puts that instruction at the cursor and steps on a cycle, overwriting; with
    /// Shift it inserts, pushing what follows right up to the first gap (the game's insert). Delete
    /// clears the cell (or the selection), Backspace clears the previous cell and steps back onto it.
    /// Shift+arrows select a rectangle of cells, Ctrl+Shift+A every instruction, Escape drops the
    /// selection; Ctrl+C / X / V copy, cut and paste (pasting at the cursor, overwriting).
    /// Ctrl+Shift+Left / Right move the arm's whole program a cycle (the selected cells instead, when
    /// there is a selection), where there is room (<see cref="ShiftInstructions"/>).
    /// Alt+Backspace goes back to the board hex the jump came from (else the row's arm base).
    /// Enter runs the solution to the cell (<see cref="RunSection.RunTo"/>).
    /// The board runs these same edits while Ctrl+Up / Ctrl+Down have the instructions picked
    /// (<see cref="EditorContext.TapePicked"/>).
    /// </summary>
    internal sealed class ProgramSection : ScreenSection
    {
        private readonly EditorContext _ctx;

        public ProgramSection(EditorContext ctx) { _ctx = ctx; ctx.Program = this; }

        public override void Build(GraphBuilder b)
        {
            var ed = _ctx.Ed;
            if (ed == null || ed.GetSolution() == null || Board.Arms(ed).Count == 0) return;
            b.BeginStop(EditorContext.StopProgram);
            b.PushContext(Strings.EditorProgram, null, positions: false);
            b.AddItem(ControlId.Structural("editor:program"), new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new[] { Nodes.LabelPart(() => CellText(_ctx, withArm: true)) },
                OnSecondary = Backspace,
                // Enter: run to this cell (the game's Alt+click on the program, RunSection.RunTo).
                OnActivate = () =>
                {
                    var arm = RowArm(_ctx);
                    if (arm != null) _ctx.Run?.RunTo(arm, _ctx.Col);
                },
            });
            b.PopContext();
        }

        private bool Focused => Equals(Navigation.FocusedStopKey, EditorContext.StopProgram);

        public override IEnumerable<ElementAction> GetActions()
        {
            var ed = _ctx.Ed;
            if (!Focused || ed == null) yield break;
            int arms = Board.Arms(ed).Count;
            if (arms == 0) yield break;
            yield return new ElementAction(UiActions.Up, Strings.Get("bind.ui.up"), _ => MoveRow(-1, select: false));
            yield return new ElementAction(UiActions.Down, Strings.Get("bind.ui.down"), _ => MoveRow(1, select: false));
            yield return new ElementAction(UiActions.Left, Strings.Get("bind.ui.left"), _ => MoveColumn(_ctx.Col - 1, select: false));
            yield return new ElementAction(UiActions.Right, Strings.Get("bind.ui.right"), _ => MoveColumn(_ctx.Col + 1, select: false));
            yield return new ElementAction(UiActions.Home, Strings.Get("bind.ui.home"), _ => Edge(first: true));
            yield return new ElementAction(UiActions.End, Strings.Get("bind.ui.end"), _ => Edge(first: false));
            yield return new ElementAction(EditorActions.SelectUp, Strings.Get("bind.ed.sel.up"), _ => MoveRow(-1, select: true));
            yield return new ElementAction(EditorActions.SelectDown, Strings.Get("bind.ed.sel.down"), _ => MoveRow(1, select: true));
            yield return new ElementAction(EditorActions.SelectLeft, Strings.Get("bind.ed.sel.left"), _ => MoveColumn(_ctx.Col - 1, select: true));
            yield return new ElementAction(EditorActions.SelectRight, Strings.Get("bind.ed.sel.right"), _ => MoveColumn(_ctx.Col + 1, select: true));
            yield return new ElementAction(EditorActions.SelectAll, Strings.Get("bind.ed.sel.all"), _ => SelectAll());
            yield return new ElementAction(EditorActions.ShiftLeft, Strings.Get("bind.ed.shift_left"), _ => ShiftInstructions(-1));
            yield return new ElementAction(EditorActions.ShiftRight, Strings.Get("bind.ed.shift_right"), _ => ShiftInstructions(1));
            for (int n = 1; n <= Math.Min(9, arms); n++)
            {
                int arm = n;
                yield return new ElementAction(EditorActions.ArmPrefix + n, Strings.Get("bind.ed.arm"), _ => JumpToRow(arm - 1));
            }
            yield return new ElementAction(EditorActions.Instruction, Strings.Get("bind.ed.instr"), _ => Type())
            {
                HelpRows = () => ProgramEdits.HelpRows(_ctx, fromBoard: false),
            };
            yield return new ElementAction(EditorActions.Delete, Strings.Get("bind.ed.delete"), _ => Delete());
            yield return new ElementAction(EditorActions.Copy, Strings.Get("bind.ed.copy"), _ => Copy(cut: false));
            yield return new ElementAction(EditorActions.Cut, Strings.Get("bind.ed.cut"), _ => Copy(cut: true));
            yield return new ElementAction(EditorActions.Paste, Strings.Get("bind.ed.paste"), _ => Paste());
            yield return new ElementAction(EditorActions.Jump, Strings.Get("bind.ed.jump"), _ => JumpToBoard());
            if (_ctx.Selecting)
                yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ed.unselect"), _ => { _ctx.Selecting = false; EditorContext.Say(Strings.EditorSelectionCleared); });
        }

        // ---- reading ----

        public static Part RowArm(EditorContext ctx)
        {
            var ed = ctx.Ed;
            var arms = ed == null ? null : Board.Arms(ed);
            if (arms == null || arms.Count == 0) return null;
            ctx.Row = Math.Max(0, Math.Min(arms.Count - 1, ctx.Row));
            return arms[ctx.Row];
        }

        public static InstructionType Instruction(EditorContext ctx)
        {
            var arm = RowArm(ctx);
            if (arm == null) return null;
            var ins = arm.program.GetInstruction(ctx.Col);
            return ins.HasValue() ? ins.GetValue() : null;
        }

        /// <summary>The cell under the program cursor: "4, grab", "5" when empty; with the arm first
        /// when asked ("arm 1, 4, grab"); an instruction error last ("3, reset, overlaps 4",
        /// <see cref="ProgramErrors"/>).</summary>
        public static string CellText(EditorContext ctx, bool withArm)
        {
            var ed = ctx.Ed;
            var arm = RowArm(ctx);
            if (arm == null) return "";
            var ins = Instruction(ctx);
            string label = ins != null ? Instructions.Label(ins) : ExpansionLabel(ed, arm, ctx.Col);
            string cell = label != null ? Strings.EditorCell(ctx.Col + 1, label) : (ctx.Col + 1).ToString();
            string error = ProgramErrors.AtCell(ed, arm, ctx.Col);
            if (error != null) cell += ", " + error;
            return withArm ? Strings.EditorArm(ctx.Row + 1) + ", " + cell : cell;
        }

        // A cell a Reset or Repeat fills (the game draws a line through them): what runs there, and
        // the instruction it comes from. Null for a plain empty cell. From the arm's own expansion,
        // which exists while an instruction error stops the program compiling.
        private static string ExpansionLabel(SolutionEditorScreen ed, Part arm, int col)
        {
            var program = ProgramErrors.Expanded(ed, arm);
            if (program == null) return null;
            foreach (var kv in program)
            {
                if (kv.Key >= col) break;
                var expansion = kv.Value.expansion;
                if (expansion == null || col - kv.Key >= expansion.Length) continue;
                var type = expansion[col - kv.Key];
                if (ReferenceEquals(type, null) || type == InstructionTypes.idle || type == InstructionTypes.placeholderIdle) return null;
                return Strings.EditorExpansion(Instructions.Label(type), Instructions.Label(kv.Value.instruction));
            }
            return null;
        }

        // Alt+Backspace: back to the board hex the jump came from, else the row's arm base.
        private void JumpToBoard()
        {
            var arm = RowArm(_ctx);
            if (_ctx.JumpHex.HasValue) _ctx.Cursor = _ctx.JumpHex.Value;
            else if (arm != null) _ctx.Cursor = Board.BaseHex(_ctx.Ed, arm);
            _ctx.JumpHex = null;
            _ctx.Selecting = false;
            Navigation.JumpToStop(EditorContext.StopBoard);
        }

        // ---- moving and selecting ----

        private void StartOrEndSelection(bool select)
        {
            if (!select) { _ctx.Selecting = false; return; }
            if (_ctx.Selecting) return;
            _ctx.Selecting = true;
            _ctx.AnchorRow = _ctx.Row;
            _ctx.AnchorCol = _ctx.Col;
        }

        private void Spoken(bool withArm, bool select)
        {
            string text = CellText(_ctx, withArm);
            if (select) text += ", " + Strings.EditorSelected(SelectedCells().Count(c => c.Type != null));
            EditorContext.Say(text);
        }

        internal void MoveRow(int step, bool select)
        {
            int count = Board.Arms(_ctx.Ed).Count;
            int row = Math.Max(0, Math.Min(count - 1, _ctx.Row + step));
            if (row == _ctx.Row) return;
            StartOrEndSelection(select);
            _ctx.Row = row;
            Spoken(withArm: true, select: select);
        }

        internal void MoveColumn(int col, bool select)
        {
            col = Math.Max(0, Math.Min(EditorContext.MaxColumn, col));
            if (col == _ctx.Col) return;
            StartOrEndSelection(select);
            _ctx.Col = col;
            Spoken(withArm: false, select: select);
        }

        private void Edge(bool first)
        {
            var arm = RowArm(_ctx);
            if (arm == null) return;
            MoveColumn(EdgeColumn(arm, first), select: false);
        }

        /// <summary>An arm's first or last instruction (cycle 1 on an empty tape).</summary>
        public static int EdgeColumn(Part arm, bool first)
        {
            var program = arm.program;
            return program.Count() == 0 ? 0 : (first ? program.FirstInstructionIndex() : program.LastInstructionIndex());
        }

        private void JumpToRow(int row)
        {
            _ctx.Selecting = false;
            _ctx.Row = row;
            EditorContext.Say(CellText(_ctx, withArm: true));
        }

        internal void SelectAll()
        {
            var arms = Board.Arms(_ctx.Ed);
            var used = arms.Where(a => a.program.Count() > 0).ToList();
            if (used.Count == 0) return;
            _ctx.Selecting = true;
            _ctx.AnchorRow = 0;
            _ctx.AnchorCol = used.Min(a => a.program.FirstInstructionIndex());
            _ctx.Row = arms.Count - 1;
            _ctx.Col = used.Max(a => a.program.LastInstructionIndex());
            EditorContext.Say(Strings.EditorSelected(SelectedCells().Count(c => c.Type != null)));
        }

        private struct Cell { public Part Arm; public int Row, Col; public InstructionType Type; }

        // The selection's cells (or the cursor's one without a selection), row by row.
        private List<Cell> SelectedCells()
        {
            var arms = Board.Arms(_ctx.Ed);
            int r0 = _ctx.Selecting ? Math.Min(_ctx.AnchorRow, _ctx.Row) : _ctx.Row;
            int r1 = _ctx.Selecting ? Math.Max(_ctx.AnchorRow, _ctx.Row) : _ctx.Row;
            int c0 = _ctx.Selecting ? Math.Min(_ctx.AnchorCol, _ctx.Col) : _ctx.Col;
            int c1 = _ctx.Selecting ? Math.Max(_ctx.AnchorCol, _ctx.Col) : _ctx.Col;
            var cells = new List<Cell>();
            for (int r = r0; r <= r1 && r < arms.Count; r++)
                for (int c = c0; c <= c1; c++)
                {
                    var ins = arms[r].program.GetInstruction(c);
                    cells.Add(new Cell { Arm = arms[r], Row = r, Col = c, Type = ins.HasValue() ? ins.GetValue() : null });
                }
            return cells;
        }

        // ---- editing ----

        private void Type()
        {
            if (SdlKeyboard.CtrlHeld) return; // Ctrl+letters are the clipboard here; Alt types as plain
            var type = ProgramEdits.PressedInstruction();
            if (type == null || !_ctx.CanEdit()) return;
            PlaceAtCursor(_ctx, type, insert: SdlKeyboard.ShiftHeld, withPlace: false);
        }

        /// <summary>An instruction at the program cursor (inserting or overwriting), the cursor stepped
        /// on; said as the instruction, or with its cycle when asked from away from the tape ("4,
        /// grab"; no arm, user rule 2026-11-04).</summary>
        public static void PlaceAtCursor(EditorContext ctx, InstructionType type, bool insert, bool withPlace)
        {
            var arm = RowArm(ctx);
            if (arm == null || !ProgramEdits.Allowed(ctx.Ed, arm, ctx.Row + 1, type)) return;
            ctx.Selecting = false;
            int col = ctx.Col;
            ProgramEdits.Place(arm, col, type, insert);
            ctx.Col = Math.Min(EditorContext.MaxColumn, col + 1);
            string label = Instructions.Label(type);
            string text = withPlace ? Strings.EditorCell(col + 1, label) : label;
            // An overlap this made is said at once (the game marks it red).
            string error = ProgramErrors.AtCell(ctx.Ed, arm, col);
            EditorContext.Say(error == null ? text : text + ", " + error);
        }

        private struct Moved { public Part Arm; public int Row, Col; public InstructionType Type; }

        /// <summary>Ctrl+Shift+Left / Right (user request 2026-11-07), on the tape and the board: the
        /// program cursor's arm's instructions, all of them, move a cycle that way; with a selection,
        /// the selected cells move instead, the selection and the cursor with them. Only where there
        /// is room: nothing moves before cycle 1 or onto an instruction that stays. Said as the
        /// cursor's cell afterwards ("2, grab": without a selection the cursor stays, so it reads what
        /// moved under it). One undo step (the game snapshots at the frame's end).</summary>
        internal void ShiftInstructions(int step)
        {
            var ed = _ctx.Ed;
            if (ed == null || RowArm(_ctx) == null || !_ctx.CanEdit()) return;
            var arms = Board.Arms(ed);
            bool selection = _ctx.Selecting;
            int r0 = selection ? Math.Min(_ctx.AnchorRow, _ctx.Row) : _ctx.Row;
            int r1 = selection ? Math.Max(_ctx.AnchorRow, _ctx.Row) : _ctx.Row;
            int c0 = selection ? Math.Min(_ctx.AnchorCol, _ctx.Col) : 0;
            int c1 = selection ? Math.Max(_ctx.AnchorCol, _ctx.Col) : EditorContext.MaxColumn;
            var moved = new List<Moved>();
            for (int r = r0; r <= r1 && r < arms.Count; r++)
            {
                var program = arms[r].program;
                if (program.Count() == 0) continue;
                for (int c = Math.Max(c0, program.FirstInstructionIndex()); c <= Math.Min(c1, program.LastInstructionIndex()); c++)
                {
                    var ins = program.GetInstruction(c);
                    if (ins.HasValue()) moved.Add(new Moved { Arm = arms[r], Row = r, Col = c, Type = ins.GetValue() });
                }
            }
            if (moved.Count == 0) return;
            var fixedArm = moved.FirstOrDefault(m => m.Arm.GetIsFixed());
            if (fixedArm.Arm != null) { EditorContext.Say(Strings.EditorFixed(Strings.EditorArm(fixedArm.Row + 1))); return; }
            // Room: every instruction lands on the tape, on a cell that is empty or moving too.
            foreach (var m in moved)
            {
                int to = m.Col + step;
                if (to < 0 || to > EditorContext.MaxColumn) return;
                bool alsoMoving = m.Row >= r0 && m.Row <= r1 && to >= c0 && to <= c1;
                if (!alsoMoving && m.Arm.program.GetInstruction(to).HasValue()) return;
            }
            foreach (var m in moved) m.Arm.program.RemoveInstruction(m.Col, asModification: true);
            foreach (var m in moved) m.Arm.program.SetInstruction(m.Col + step, m.Type);
            Assets.sounds.instruction_place.PlaySound(1f);
            if (selection)
            {
                _ctx.AnchorCol += step;
                _ctx.Col += step;
            }
            EditorContext.Say(CellText(_ctx, withArm: false));
        }

        internal void Delete()
        {
            if (!_ctx.CanEdit()) return;
            var removed = new List<InstructionType>();
            foreach (var cell in SelectedCells())
            {
                if (cell.Type == null) continue;
                if (cell.Arm.GetIsFixed()) { EditorContext.Say(Strings.EditorFixed(Strings.EditorArm(cell.Row + 1))); return; }
            }
            foreach (var cell in SelectedCells())
            {
                var t = ProgramEdits.Clear(cell.Arm, cell.Col);
                if (t != null) removed.Add(t);
            }
            _ctx.Selecting = false;
            if (removed.Count > 0) EditorContext.Say(Strings.EditorDeleted(ProgramEdits.Summary(removed)));
        }

        internal void Backspace()
        {
            if (_ctx.Selecting) { Delete(); return; }
            if (_ctx.Col == 0 || !_ctx.CanEdit()) return;
            var arm = RowArm(_ctx);
            if (arm == null) return;
            _ctx.Col--;
            if (arm.program.GetInstruction(_ctx.Col).HasValue())
            {
                if (!ProgramEdits.Allowed(_ctx.Ed, arm, _ctx.Row + 1, null)) { _ctx.Col++; return; }
                var t = ProgramEdits.Clear(arm, _ctx.Col);
                EditorContext.Say(Strings.EditorDeleted(Instructions.Label(t)));
            }
            else EditorContext.Say(CellText(_ctx, withArm: false));
        }

        internal void Copy(bool cut)
        {
            if (cut && !_ctx.CanEdit()) return;
            var cells = SelectedCells().Where(c => c.Type != null).ToList();
            if (cells.Count == 0) return;
            if (cut && cells.Any(c => c.Arm.GetIsFixed()))
            {
                EditorContext.Say(Strings.EditorFixed(Strings.EditorArm(cells.First(c => c.Arm.GetIsFixed()).Row + 1)));
                return;
            }
            int r0 = cells.Min(c => c.Row), c0 = cells.Min(c => c.Col);
            _ctx.Clipboard.Clear();
            foreach (var c in cells) _ctx.Clipboard.Add(new EditorContext.ClipCell { Row = c.Row - r0, Col = c.Col - c0, Type = c.Type });
            var types = cells.Select(c => c.Type).ToList();
            if (cut) foreach (var c in cells) ProgramEdits.Clear(c.Arm, c.Col);
            _ctx.Selecting = false;
            _ctx.PartsOnClipboard = false;
            string summary = ProgramEdits.Summary(types);
            EditorContext.Say(cut ? Strings.EditorCut(summary) : Strings.EditorCopied(summary));
        }

        // Parts copied last have no place on the tape: nothing (silent, user decision 2026-11-07).
        internal void Paste()
        {
            if (_ctx.PartsOnClipboard || _ctx.Clipboard.Count == 0 || !_ctx.CanEdit()) return;
            var ed = _ctx.Ed;
            var arms = Board.Arms(ed);
            var placed = new List<InstructionType>();
            foreach (var clip in _ctx.Clipboard)
            {
                int r = _ctx.Row + clip.Row;
                if (r >= arms.Count) continue;
                if (arms[r].GetIsFixed() || !ed.GetSolution().GetPuzzle().HasPermissionForInstr(clip.Type)) continue;
                arms[r].program.SetInstruction(_ctx.Col + clip.Col, clip.Type);
                placed.Add(clip.Type);
            }
            _ctx.Selecting = false;
            if (placed.Count == 0) return;
            Assets.sounds.instruction_place.PlaySound(1f);
            string text = Strings.EditorPasted(ProgramEdits.Summary(placed));
            if (placed.Count < _ctx.Clipboard.Count) text += ", " + Strings.EditorNotPasted(_ctx.Clipboard.Count - placed.Count);
            EditorContext.Say(text);
        }
    }
}
