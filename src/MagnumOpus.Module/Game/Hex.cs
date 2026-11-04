using System.Collections.Generic;
using System.Text;
using MagnumOpus.Core.Strings;

namespace MagnumOpus.Module.Game
{
    /// <summary>The six neighbours of a hex on the game's pointy-top board, in the game's rotation
    /// order: turn k of a HexRotation points direction k (turns count counter-clockwise from east).</summary>
    public enum HexDir { East = 0, NorthEast = 1, NorthWest = 2, West = 3, SouthWest = 4, SouthEast = 5 }

    /// <summary>
    /// The board's geometry for speech. The game's HexIndex is axial (Q, R): Q grows east, R grows up
    /// the screen, rows are horizontal (pointy-top hexes, the Civ V orientation). Neighbours: east
    /// (1, 0), northeast (0, 1), northwest (-1, 1), west (-1, 0), southwest (0, -1), southeast (1, -1).
    /// Spoken coordinates are Civ's half-step x, y (user decision 2026-11-04): x = Q + R/2 and y = R,
    /// so x is the horizontal position in hex widths and a step northeast from (0, 0) reads "0.5, 1".
    /// Origin is the game's own (0, 0).
    /// </summary>
    public static class Hex
    {
        public static readonly HexDir[] All = { HexDir.East, HexDir.NorthEast, HexDir.NorthWest, HexDir.West, HexDir.SouthWest, HexDir.SouthEast };

        private static readonly int[] DQ = { 1, 0, -1, -1, 0, 1 };
        private static readonly int[] DR = { 0, 1, 1, 0, -1, -1 };

        public static void Offset(HexDir dir, out int dq, out int dr)
        {
            dq = DQ[(int)dir];
            dr = DR[(int)dir];
        }

        public static HexIndex Step(HexIndex h, HexDir dir, int n = 1) => new HexIndex(h.Q + DQ[(int)dir] * n, h.R + DR[(int)dir] * n);

        /// <summary>Hex steps between two hexes.</summary>
        public static int Distance(int dq, int dr) => (System.Math.Abs(dq) + System.Math.Abs(dr) + System.Math.Abs(dq + dr)) / 2;

        public static int Distance(HexIndex a, HexIndex b) => Distance(b.Q - a.Q, b.R - a.R);

        /// <summary>The direction a game rotation points (turns modulo six).</summary>
        public static HexDir FromTurns(int turns) => (HexDir)(((turns % 6) + 6) % 6);

        /// <summary>The direction of a unit offset, or null when it is not a neighbour.</summary>
        public static HexDir? DirOf(int dq, int dr)
        {
            for (int i = 0; i < 6; i++)
                if (DQ[i] == dq && DR[i] == dr) return (HexDir)i;
            return null;
        }

        /// <summary>"0.5, 1": the half-step coordinates of a hex.</summary>
        public static string Coords(HexIndex h) => Coords(h.Q, h.R);

        public static string Coords(int q, int r) => Strings.HexCoords(Half(2 * q + r), r.ToString(System.Globalization.CultureInfo.InvariantCulture));

        // A number given in halves: 3 -> "1.5", -1 -> "-0.5", 4 -> "2".
        private static string Half(int twice)
        {
            string sign = twice < 0 ? "-" : "";
            int a = System.Math.Abs(twice);
            return sign + (a / 2).ToString(System.Globalization.CultureInfo.InvariantCulture) + (a % 2 == 1 ? ".5" : "");
        }

        public static string Word(HexDir dir) => Strings.HexDirection((int)dir);

        /// <summary>
        /// An offset as at most two neighbouring directions, in the game's rotation order from east:
        /// (3, 1) reads "3 east, 1 northeast"; zero reads "here". Any offset lies in one 60-degree
        /// sector, spanned by the two directions bounding it with non-negative counts.
        /// </summary>
        public static string Describe(int dq, int dr)
        {
            if (dq == 0 && dr == 0) return Strings.HexHere;
            for (int i = 0; i < 6; i++)
            {
                int j = (i + 1) % 6;
                int det = DQ[i] * DR[j] - DQ[j] * DR[i];
                int a = (dq * DR[j] - DQ[j] * dr) / det;
                int b = (DQ[i] * dr - dq * DR[i]) / det;
                if (a < 0 || b < 0 || a * DQ[i] + b * DQ[j] != dq || a * DR[i] + b * DR[j] != dr) continue;
                var parts = new List<string>();
                // Rotation order from east: the lower index first, except the sector between
                // southeast (5) and east (0), which reads east first too.
                if (j == 0)
                {
                    if (b > 0) parts.Add(Strings.HexSteps(b, Word((HexDir)j)));
                    if (a > 0) parts.Add(Strings.HexSteps(a, Word((HexDir)i)));
                }
                else
                {
                    if (a > 0) parts.Add(Strings.HexSteps(a, Word((HexDir)i)));
                    if (b > 0) parts.Add(Strings.HexSteps(b, Word((HexDir)j)));
                }
                return string.Join(", ", parts);
            }
            return Strings.HexHere; // unreachable: the six sectors cover the plane
        }

        public static string Describe(HexIndex from, HexIndex to) => Describe(to.Q - from.Q, to.R - from.R);
    }
}
