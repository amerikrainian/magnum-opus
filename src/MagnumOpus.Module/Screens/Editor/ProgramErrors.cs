using System.Collections.Generic;
using System.Linq;
using MagnumOpus.Core.Strings;
using MagnumOpus.Module.Game;

namespace MagnumOpus.Module.Screens.Editor
{
    /// <summary>
    /// The game's instruction errors: a Reset or Repeat fills the cells after it with the
    /// instructions it stands for, and a filled cell that already holds an instruction is an error.
    /// That overlap is what makes the program fail to compile (CompiledProgramGrid.FromSolution
    /// returns nothing, so "Instruction errors must be fixed." and no run), and what the game marks:
    /// it draws the Reset's or Repeat's extent line red (SolutionEditorProgramPanel, the
    /// instruction_extent drawn in 0x880202). Read from each arm's own expansion
    /// (EditableProgram.GetReferenceForPart), which exists with or without errors, live.
    /// </summary>
    internal static class ProgramErrors
    {
        public struct Overlap
        {
            public Part Arm;
            public int Row;          // 0-based arm row
            public int Col;          // the Reset's or Repeat's cell
            public InstructionType Type;
            public List<int> Into;   // the filled cells that hold an instruction
        }

        /// <summary>An arm's program expanded: each instruction with the cells it fills.</summary>
        public static SortedDictionary<int, ExpandedInstruction> Expanded(SolutionEditorScreen ed, Part arm)
        {
            var solution = ed?.GetSolution();
            if (solution == null || arm == null) return null;
            return arm.program.GetReferenceForPart(arm, solution.GetTrackUnderPart(arm)).program;
        }

        /// <summary>Every overlap on an arm, in cell order.</summary>
        public static List<Overlap> On(SolutionEditorScreen ed, Part arm, int row)
        {
            var result = new List<Overlap>();
            var program = Expanded(ed, arm);
            if (program == null) return result;
            foreach (var kv in program)
            {
                var expansion = kv.Value.expansion;
                if (expansion == null || expansion.Length <= 1) continue;
                var into = new List<int>();
                for (int i = 1; i < expansion.Length; i++)
                    if (program.ContainsKey(kv.Key + i)) into.Add(kv.Key + i);
                if (into.Count > 0)
                    result.Add(new Overlap { Arm = arm, Row = row, Col = kv.Key, Type = kv.Value.instruction, Into = into });
            }
            return result;
        }

        /// <summary>Every overlap in the solution, arm by arm.</summary>
        public static List<Overlap> All(SolutionEditorScreen ed)
        {
            var result = new List<Overlap>();
            if (ed == null) return result;
            var arms = Board.Arms(ed);
            for (int r = 0; r < arms.Count; r++) result.AddRange(On(ed, arms[r], r));
            return result;
        }

        private static string Cycles(IEnumerable<int> cols) => string.Join(", ", cols.Select(c => (c + 1).ToString()));

        /// <summary>What is wrong with a cell, or null: a Reset or Repeat there runs into instructions
        /// ("overlaps 4"), or the instruction there sits in one's filled cells ("overlaps reset at
        /// 3"); both where both hold.</summary>
        public static string AtCell(SolutionEditorScreen ed, Part arm, int col)
        {
            var overlaps = On(ed, arm, 0);
            var parts = new List<string>();
            foreach (var o in overlaps)
                if (o.Col == col) parts.Add(Strings.ProgramOverlaps(Cycles(o.Into)));
            foreach (var o in overlaps)
                if (o.Into.Contains(col)) parts.Add(Strings.ProgramOverlapsAt(Instructions.Label(o.Type), o.Col + 1));
            return parts.Count == 0 ? null : string.Join(", ", parts);
        }

        /// <summary>The first overlap as a sentence ("arm 2, reset at 3 overlaps 4"), or null.</summary>
        public static string Describe(Overlap o) =>
            Strings.ProgramOverlapLine(Strings.EditorArm(o.Row + 1), Instructions.Label(o.Type), o.Col + 1, Cycles(o.Into));
    }
}
