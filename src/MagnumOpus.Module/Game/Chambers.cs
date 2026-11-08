using System.Linq;

namespace MagnumOpus.Module.Game
{
    /// <summary>
    /// A production puzzle's board: only the hexes inside its chambers exist (the game places nothing
    /// anywhere else). Every other puzzle's board is unbounded, so these answer "everything exists"
    /// there. Read live from the puzzle.
    /// </summary>
    internal static class Chambers
    {
        private static readonly PlacedChamber[] None = new PlacedChamber[0];

        /// <summary>The puzzle's chambers in the game's order; empty on a puzzle without any.</summary>
        public static PlacedChamber[] All(SolutionEditorScreen ed)
        {
            var solution = ed?.GetSolution();
            if (solution == null) return None;
            return solution.GetPuzzle().productionInfo.GetOrDefault(out var info) ? info.chambers : None;
        }

        /// <summary>Whether the board has this hex.</summary>
        public static bool Exists(SolutionEditorScreen ed, HexIndex hex)
        {
            var all = All(ed);
            return all.Length == 0 || all.Any(c => c.GetWorldIndices().Contains(hex));
        }

        /// <summary>The index of the chamber holding this hex, or -1.</summary>
        public static int IndexOf(SolutionEditorScreen ed, HexIndex hex)
        {
            var all = All(ed);
            for (int i = 0; i < all.Length; i++)
                if (all[i].GetWorldIndices().Contains(hex)) return i;
            return -1;
        }

        /// <summary>The board's home: the origin, or the first chamber's centre where the origin is
        /// not a hex of this puzzle.</summary>
        public static HexIndex Home(SolutionEditorScreen ed)
        {
            var origin = new HexIndex(0, 0);
            return Exists(ed, origin) ? origin : All(ed)[0].hexPos;
        }

        /// <summary>This hex if the board has it, else the closest one that it has (an atom that hit
        /// a chamber's wall is reported on the wall).</summary>
        public static HexIndex Nearest(SolutionEditorScreen ed, HexIndex hex)
        {
            if (Exists(ed, hex)) return hex;
            return All(ed).SelectMany(c => c.GetWorldIndices())
                .OrderBy(h => Hex.Distance(h, hex)).ThenBy(h => h.R).ThenBy(h => h.Q).First();
        }
    }
}
