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
    /// The instruction tray (SolutionEditorProgramPanel's grid beside the tape) as three rows mirroring
    /// the keyboard (Q W E R T, A S D F G, X C V B), built as five columns so Up / Down keep the column
    /// and Left / Right the row. The game draws its Delete button under A (it is the Delete key, left
    /// out here), so the bottom row is shifted left to fill its place: Q A X, W S C, E D V, R F B, T G
    /// (user request 2026-11-14); Right from the bottom row's last tile goes nowhere. Each tile reads its name and the game's hotkey
    /// letter (which follows its Keyboard Layout option); one the puzzle does not permit reads "not
    /// available" (the game draws a blank slot there), the period override "limit reached" once the
    /// solution has one (the game greys it). Enter puts the instruction at the program cursor and
    /// steps it on, as typing its key on the tape does; S reads the game's description.
    /// </summary>
    internal sealed class InstructionTraySection : ScreenSection
    {
        public const string Stop = "editor.instructions";

        private readonly EditorContext _ctx;

        public InstructionTraySection(EditorContext ctx) { _ctx = ctx; }

        /// <summary>The tray's instructions row by row, left to right (Q W E R T, A S D F G, X C V B
        /// on the game's default layout).</summary>
        public static IEnumerable<InstructionType> RowOrder()
        {
            var columns = Columns();
            int rows = columns.Max(c => c.Length);
            for (int r = 0; r < rows; r++)
                foreach (var column in columns)
                    if (r < column.Length && !ReferenceEquals(column[r], null)) yield return column[r];
        }

        // Columns left to right, each top to bottom; the bottom row left-aligned (see the summary).
        private static InstructionType[][] Columns() => new[]
        {
            new[] { InstructionTypes.pivotLeft, InstructionTypes.rotateLeft, InstructionTypes.periodOverride },
            new[] { InstructionTypes.extend, InstructionTypes.retract, InstructionTypes.reset },
            new[] { InstructionTypes.pivotRight, InstructionTypes.rotateRight, InstructionTypes.repeat },
            new[] { InstructionTypes.drop, InstructionTypes.grab, InstructionTypes.halt },
            new[] { InstructionTypes.moveMinus, InstructionTypes.movePlus },
        };

        private const string IdPrefix = "editor:instr:";

        public override void Build(GraphBuilder b)
        {
            var ed = _ctx.Ed;
            if (ed == null || ed.GetSolution() == null || Board.Arms(ed).Count == 0) return;
            b.BeginStop(Stop);
            b.PushContext(Strings.EditorInstructionTray, null, positions: false);
            foreach (var column in Columns())
            {
                b.StartColumn();
                foreach (var type in column)
                {
                    if (ReferenceEquals(type, null)) continue;
                    var t = type;
                    b.AddItem(ControlId.Structural(IdPrefix + t.id), new NodeVtable
                    {
                        // No role word: a tile reads "grab, F" (user rule 2026-11-04).
                        ControlType = ControlTypes.Text,
                        Announcements = new List<NodeAnnouncement>
                        {
                            Nodes.LabelPart(() => Instructions.Label(t)),
                            Nodes.ValuePart(() => Instructions.Hotkey(t)),
                            new NodeAnnouncement(() => State(t), live: false, kind: AnnouncementKinds.Enabled),
                        },
                        SearchText = () => Instructions.Label(t),
                        OnActivate = () => Place(t),
                    });
                }
                b.EndColumn();
            }
            BlockRaggedRight(b);
            b.PopContext();
        }

        // Core's columns step Right into a shorter column's last tile (from B up to G here): a
        // self-edge on every tile with no tile to its right in the same row makes Right go nowhere.
        private static void BlockRaggedRight(GraphBuilder b)
        {
            var columns = Columns().Select(c => c.Where(t => !ReferenceEquals(t, null)).ToArray()).ToArray();
            for (int c = 0; c + 1 < columns.Length; c++)
                for (int r = columns[c + 1].Length; r < columns[c].Length; r++)
                {
                    var id = ControlId.Structural(IdPrefix + columns[c][r].id);
                    b.Connect(id, GraphDir.Right, id);
                }
        }

        private bool Focused => Equals(Navigation.FocusedStopKey, Stop);

        public override IEnumerable<ElementAction> GetActions()
        {
            if (!Focused || _ctx.Ed == null) yield break;
            yield return new ElementAction(EditorActions.Details, Strings.Get("bind.ed.details"), _ => SayDetails());
        }

        private static InstructionType FocusedType()
        {
            var key = Navigation.FocusedNode?.Id?.StructuralKey as string;
            if (key == null || !key.StartsWith(IdPrefix)) return null;
            string id = key.Substring(IdPrefix.Length);
            return Columns().SelectMany(c => c).FirstOrDefault(t => !ReferenceEquals(t, null) && t.id.ToString() == id);
        }

        private static void SayDetails()
        {
            var t = FocusedType();
            if (t != null) EditorContext.Say(Instructions.Details(t));
        }

        private string State(InstructionType t)
        {
            var solution = _ctx.Ed?.GetSolution();
            if (solution == null) return null;
            if (!solution.GetPuzzle().HasPermissionForInstr(t)) return Strings.EditorNotAvailable;
            if (LimitReached(solution, t)) return Strings.EditorLimitReached;
            return null;
        }

        // The game allows one period override in a solution.
        private static bool LimitReached(Solution solution, InstructionType t) =>
            t.isPeriodOverride && solution.parts.Any(p => p.program.HasInstruction(t));

        private void Place(InstructionType t)
        {
            if (_ctx.FinishDrawing()) return;
            var ed = _ctx.Ed;
            if (ed == null || !_ctx.CanEdit()) return;
            if (LimitReached(ed.GetSolution(), t)) { EditorContext.Say(Strings.EditorLimitReached); return; }
            ProgramSection.PlaceAtCursor(_ctx, t, insert: false, withPlace: true);
        }
    }
}
