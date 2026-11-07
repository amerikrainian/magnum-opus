using System.Collections.Generic;
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
        /// <summary>The instruction of the instruction key pressed this frame: the key's character on
        /// the player's layout matched against the game's hotkeys (which follow its Keyboard Layout
        /// option). Null when the key is no instruction's.</summary>
        public static InstructionType PressedInstruction()
        {
            foreach (var sc in EditorKeys.InstructionKeys)
            {
                if (!SdlKeyboard.JustPressed((int)sc)) continue;
                int key = SdlKeyboard.KeyFor((int)sc);
                foreach (var t in InstructionTypes.instructions)
                    if (!ReferenceEquals(t, null) && (int)t.GetHotkey() == key && key != 0) return t;
            }
            return null;
        }

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

        /// <summary>The key help's rows for the instruction keys (user request 2026-11-07): one per
        /// instruction the puzzle permits, in the tray's order, its name and its key ("pivot counter:
        /// Alt+Q" from the board, "Q" on the tape, where Shift with the key inserts: one more row).
        /// Enter on a row places the instruction as the key would.</summary>
        public static IEnumerable<KeyHelpEntry> HelpRows(EditorContext ctx, bool fromBoard)
        {
            var ed = ctx.Ed;
            var puzzle = ed?.GetSolution()?.GetPuzzle();
            if (puzzle == null) yield break;
            foreach (var type in InstructionTraySection.RowOrder())
            {
                if (!puzzle.HasPermissionForInstr(type)) continue;
                string key = Instructions.Hotkey(type);
                if (key.Length == 0) continue;
                var t = type;
                yield return new KeyHelpEntry
                {
                    ActionKey = EditorActions.Instruction,
                    Label = Instructions.Label(t),
                    Keys = fromBoard ? "Alt+" + key : key,
                    Run = () => { if (ctx.CanEdit()) ProgramSection.PlaceAtCursor(ctx, t, insert: false, withPlace: fromBoard); },
                };
            }
            if (!fromBoard)
                yield return new KeyHelpEntry
                {
                    ActionKey = EditorActions.Instruction,
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
