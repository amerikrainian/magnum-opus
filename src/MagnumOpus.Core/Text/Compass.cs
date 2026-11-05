using System;

namespace MagnumOpus.Core.Text
{
    /// <summary>
    /// Directions between points drawn on a screen, y growing upward (the game's convention): eight
    /// 45-degree sectors centred on the axes and diagonals, so a label straight above another reads
    /// "above" and one up and to the right at about 45 degrees "upper right". Used to say where things
    /// are on a picture from where the game draws their labels (the alchemical diagram), so a
    /// rearranged picture reads right.
    /// </summary>
    public static class Compass
    {
        public enum Dir { Right, UpperRight, Above, UpperLeft, Left, LowerLeft, Below, LowerRight }

        /// <summary>The sector of the vector (dx, dy); null when it has no length.</summary>
        public static Dir? Of(double dx, double dy)
        {
            if (dx == 0 && dy == 0) return null;
            double deg = Math.Atan2(dy, dx) * 180 / Math.PI; // 0 = right, 90 = up
            int sector = (int)Math.Floor(((deg + 360 + 22.5) % 360) / 45);
            return (Dir)(sector % 8);
        }

        /// <summary>The way a run of points goes from its first to its last, along its main axis
        /// (Above = bottom to top, Right = left to right); null when they coincide.</summary>
        public static Dir? Run(double dx, double dy)
        {
            if (dx == 0 && dy == 0) return null;
            if (Math.Abs(dy) >= Math.Abs(dx)) return dy > 0 ? Dir.Above : Dir.Below;
            return dx > 0 ? Dir.Right : Dir.Left;
        }
    }
}
