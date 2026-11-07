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
    /// The instruction tray (SolutionEditorProgramPanel's grid beside the tape), laid out as the game
    /// draws it: three rows of five mirroring the keyboard (Q W E R T, A S D F G, Z X C V B), built as
    /// five columns so Up / Down keep the key column and Left / Right the row; the bottom row's Delete
    /// button is left out (it is the Delete key). Each tile reads its name and the game's hotkey
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

        // Columns left to right, each top to bottom (the game's tile positions).
        private static InstructionType[][] Columns() => new[]
        {
            new[] { InstructionTypes.pivotLeft, InstructionTypes.rotateLeft },
            new[] { InstructionTypes.extend, InstructionTypes.retract, InstructionTypes.periodOverride },
            new[] { InstructionTypes.pivotRight, InstructionTypes.rotateRight, InstructionTypes.reset },
            new[] { InstructionTypes.drop, InstructionTypes.grab, InstructionTypes.repeat },
            new[] { InstructionTypes.moveMinus, InstructionTypes.movePlus, InstructionTypes.halt },
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
                            Nodes.ValuePart(() => Letter(t)),
                            new NodeAnnouncement(() => State(t), live: false, kind: AnnouncementKinds.Enabled),
                        },
                        SearchText = () => Instructions.Label(t),
                        OnActivate = () => Place(t),
                    });
                }
                b.EndColumn();
            }
            b.PopContext();
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

        // The game's letter for the hotkey (its Keyboard Layout option decides it).
        private static string Letter(InstructionType t)
        {
            try { return GameText.Clean(SolutionEditorScreen.GetCharacterFromKey(t.GetHotkey())).ToUpperInvariant(); }
            catch (System.Exception e) { MagnumOpus.Core.CoreLog.Warning("[editor] hotkey letter: " + e.Message); return ""; }
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
            var ed = _ctx.Ed;
            if (ed == null || !_ctx.CanEdit()) return;
            if (LimitReached(ed.GetSolution(), t)) { EditorContext.Say(Strings.EditorLimitReached); return; }
            ProgramSection.PlaceAtCursor(_ctx, t, insert: false, withPlace: true);
        }
    }
}
