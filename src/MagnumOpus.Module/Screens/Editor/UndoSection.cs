using System.Collections.Generic;
using System.Linq;
using MagnumOpus.Core.Screens;
using MagnumOpus.Core.Strings;
using MagnumOpus.Core.UI;
using MagnumOpus.Module.Game;
using MagnumOpus.Module.Input;

namespace MagnumOpus.Module.Screens.Editor
{
    /// <summary>
    /// Undo and redo (Ctrl+Z, Ctrl+Y or Ctrl+Shift+Z), from anywhere in the editor (no nodes): the
    /// game's own snapshot buffer, stepped and restored exactly as its NormalInputMode does (a forced
    /// key would need the game to see Ctrl, which an injected chord cannot give it). What changed is
    /// spoken, SpeechChem's way: the new state of each part or cell, three at most, else a count;
    /// "nothing to undo" at the buffer's end.
    /// </summary>
    internal sealed class UndoSection : ScreenSection
    {
        private readonly EditorContext _ctx;

        public UndoSection(EditorContext ctx) { _ctx = ctx; }

        public override IEnumerable<ElementAction> GetActions()
        {
            if (_ctx.Ed == null) yield break;
            yield return new ElementAction(EditorActions.Undo, Strings.Get("bind.ed.undo"), _ => Step(back: true));
            yield return new ElementAction(EditorActions.Redo, Strings.Get("bind.ed.redo"), _ => Step(back: false));
        }

        private void Step(bool back)
        {
            var ed = _ctx.Ed;
            if (ed == null || _ctx.Busy() || !_ctx.CanEdit()) return;
            var solution = ed.GetSolution();
            var before = Describe(solution);
            bool moved = back ? solution.undoRedoBuffer.PreviousSnapshot() : solution.undoRedoBuffer.NextSnapshot();
            if (!moved) { EditorContext.Say(back ? Strings.EditorNothingToUndo : Strings.EditorNothingToRedo); return; }
            solution.RestoreSnapshot(solution.undoRedoBuffer.GetCurrentSnapshot());
            ed.Save();
            ed.ClearPartSelection();
            _ctx.Selecting = false;
            Assets.sounds.click_button.PlaySound(1f);
            EditorContext.Say(Diff(before, Describe(solution)));
        }

        private sealed class State
        {
            public readonly List<string> Parts = new List<string>();     // summaries
            public readonly Dictionary<string, string> Names = new Dictionary<string, string>(); // summary -> name
            public readonly Dictionary<string, string> Cells = new Dictionary<string, string>(); // "row|col" -> label
        }

        // The solution in words: each part's summary, each program cell's label. Arms are ordered by
        // their program index directly (the solution reorders its list only when it saves).
        private State Describe(Solution solution)
        {
            var ed = _ctx.Ed;
            var state = new State();
            foreach (var part in solution.parts)
            {
                string summary = Summary(ed, solution, part);
                state.Parts.Add(summary);
                state.Names[summary] = Board.PartName(ed, part);
            }
            var arms = solution.parts.Where(p => p.GetType().isProgrammable).OrderBy(p => p.programIndex).ToList();
            for (int r = 0; r < arms.Count; r++)
            {
                var program = arms[r].program;
                if (program.Count() == 0) continue;
                for (int c = program.FirstInstructionIndex(); c <= program.LastInstructionIndex(); c++)
                {
                    var ins = program.GetInstruction(c);
                    if (ins.HasValue()) state.Cells[(r + 1) + "|" + (c + 1)] = Instructions.Label(ins.GetValue());
                }
            }
            return state;
        }

        private static string Summary(SolutionEditorScreen ed, Solution solution, Part part)
        {
            string name = Board.PartName(ed, part);
            string where = Hex.Coords(part.GetHexPos());
            var type = part.GetType();
            if (type.isTrack) return Strings.EditorPartAt(name, where) + ", " + Strings.EditorTrackHexes(part.GetTrack().Count);
            string text = Strings.EditorPartAt(name, where);
            if (part.IsRotatable(solution)) text += ", " + Hex.Word(Hex.FromTurns(part.GetRotation().GetNumberOfTurns()));
            if (part.HasArms()) text += ", " + Strings.EditorLength(part.GetArmLength());
            return text;
        }

        private static string Diff(State before, State after)
        {
            var changes = new List<string>();
            var removedParts = Minus(before.Parts, after.Parts);
            var addedParts = Minus(after.Parts, before.Parts);
            changes.AddRange(addedParts);
            // A part moved or turned reads once, as its new state; one that went away says so.
            foreach (var gone in removedParts)
            {
                string name = before.Names[gone];
                if (!addedParts.Any(a => after.Names[a] == name)) changes.Add(Strings.EditorRemoved(gone));
            }
            foreach (var key in after.Cells.Keys.Union(before.Cells.Keys).OrderBy(k => k))
            {
                before.Cells.TryGetValue(key, out var was);
                after.Cells.TryGetValue(key, out var now);
                if (was == now) continue;
                var rc = key.Split('|');
                int row = int.Parse(rc[0]), col = int.Parse(rc[1]);
                changes.Add(now != null ? Strings.EditorCellChange(row, col, now) : Strings.EditorCellRemoved(row, col, was));
            }
            if (changes.Count == 0) return Strings.EditorNoChange;
            return changes.Count > 3 ? Strings.EditorChanges(changes.Count) : string.Join("; ", changes);
        }

        // Multiset difference.
        private static List<string> Minus(List<string> a, List<string> b)
        {
            var rest = new List<string>(b);
            var result = new List<string>();
            foreach (var x in a)
            {
                int i = rest.IndexOf(x);
                if (i >= 0) rest.RemoveAt(i);
                else result.Add(x);
            }
            return result;
        }
    }
}
