using System.Collections.Generic;
using System.Linq;
using MagnumOpus.Core.Strings;
using MagnumOpus.Core.UI;
using MagnumOpus.Module.Game;
using MagnumOpus.Module.Input;

namespace MagnumOpus.Module.Screens.Editor
{
    /// <summary>
    /// Instruction edits through the game's own program operations (SetInstruction,
    /// InsertInstruction, RemoveInstruction as a modification): each marks the program modified, and
    /// the editor takes its undo snapshot and saves at the end of the frame, as for a mouse edit.
    /// </summary>
    internal static class ProgramEdits
    {
        /// <summary>Whether this arm's program may take this instruction; says why not.</summary>
        public static bool Allowed(SolutionEditorScreen ed, Part arm, int armNumber, InstructionType type)
        {
            if (arm.GetIsFixed()) { EditorContext.Say(Strings.EditorFixed(Strings.EditorArm(armNumber))); return false; }
            if (type != null && !ed.GetSolution().GetPuzzle().HasPermissionForInstr(type))
            {
                EditorContext.Say(Strings.EditorUnavailable(Instructions.Label(type)));
                return false;
            }
            return true;
        }

        /// <summary>The game allows one period override in a solution: once there is one, its tile
        /// greys and its key places nothing (SolutionEditorProgramPanel.IsLimitReached).</summary>
        public static bool LimitReached(Solution solution, InstructionType type) =>
            type != null && type.isPeriodOverride && solution.parts.Any(p => p.program.HasInstruction(type));

        /// <summary>Remove every period override in the solution as a modification (paste moves the
        /// override, as the game's Ctrl+drag of one does); false, removing nothing, when one sits on
        /// a fixed arm (the game cannot pick its instructions up).</summary>
        public static bool TakeOverride(Solution solution)
        {
            var found = new List<KeyValuePair<Part, int>>();
            foreach (var p in solution.parts)
            {
                var program = p.program;
                if (program.Count() == 0) continue;
                for (int c = program.FirstInstructionIndex(); c <= program.LastInstructionIndex(); c++)
                {
                    var ins = program.GetInstruction(c);
                    if (!ins.HasValue() || !ins.GetValue().isPeriodOverride) continue;
                    if (p.GetIsFixed()) return false;
                    found.Add(new KeyValuePair<Part, int>(p, c));
                }
            }
            foreach (var f in found) f.Key.program.RemoveInstruction(f.Value, asModification: true);
            return true;
        }

        public static void Place(Part arm, int col, InstructionType type, bool insert)
        {
            if (insert) arm.program.InsertInstruction(col, type);
            else arm.program.SetInstruction(col, type);
            Assets.sounds.instruction_place.PlaySound(1f);
        }

        /// <summary>Clear a cell; the instruction that was there, or null for an empty cell.</summary>
        public static InstructionType Clear(Part arm, int col)
        {
            var ins = arm.program.GetInstruction(col);
            if (!ins.HasValue()) return null;
            arm.program.RemoveInstruction(col, asModification: true);
            Assets.sounds.instruction_remove.PlaySound(1f);
            return ins.GetValue();
        }

        /// <summary>The key help's row for an instruction key (user request 2026-11-07), when the
        /// puzzle permits it and it has a key: its name and its keys ("pivot counter: Alt+Q" from the
        /// board, "Q" on the tape, where Shift with the key inserts: one more row, after the last
        /// instruction's). Enter on a row places the instruction as the key would.</summary>
        public static IEnumerable<KeyHelpEntry> HelpRows(EditorContext ctx, InstructionSlot slot, bool fromBoard)
        {
            var puzzle = ctx.Ed?.GetSolution()?.GetPuzzle();
            if (puzzle == null) yield break;
            var t = slot.Type;
            string keys = InstructionSlots.Keys(slot, alt: fromBoard);
            if (puzzle.HasPermissionForInstr(t) && keys.Length > 0)
                yield return new KeyHelpEntry
                {
                    ActionKey = slot.ActionKey,
                    Label = Instructions.Label(t),
                    Keys = keys,
                    Run = () => { if (ctx.CanEdit()) ProgramSection.PlaceAtCursor(ctx, t, insert: false, withPlace: fromBoard); },
                };
            if (!fromBoard && ReferenceEquals(slot, InstructionSlots.All[InstructionSlots.All.Length - 1]))
                yield return new KeyHelpEntry
                {
                    ActionKey = slot.ActionKey,
                    Label = Strings.Get("bind.ed.instr_insert"),
                    Keys = Strings.Get("help.ed.instr_insert_keys"),
                    Run = () => { },
                };
        }

        /// <summary>A short list of instructions for speech: up to three by name, more by count.</summary>
        public static string Summary(IList<InstructionType> types)
        {
            if (types.Count > 3) return Strings.EditorInstructionCount(types.Count);
            var names = new List<string>();
            foreach (var t in types) names.Add(Instructions.Label(t));
            return string.Join(", ", names);
        }
    }
}
