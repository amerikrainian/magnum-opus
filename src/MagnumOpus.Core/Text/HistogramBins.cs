using System;
using System.Collections.Generic;

namespace MagnumOpus.Core.Text
{
    /// <summary>One bar of a score histogram, as spoken.</summary>
    public sealed class HistogramBin
    {
        public int Low, High;  // the integer scores the bar holds, inclusive
        public int Percent;    // its share of the players (rounded)
        public bool Drawn;     // a bar is drawn (non-zero height; may still round to 0%)
        public bool Marked;    // the first score marker stands over it
        public bool Marked2;   // the second one does (puzzle info draws this and best)
    }

    /// <summary>
    /// A score histogram as the game draws it (UIUtils.RenderHistogram): equal-width bars over
    /// 0 .. maxScore, one per bucket, rising from zero. The game stores each bucket's player count
    /// divided by the largest one, so heights are proportional to counts and a bar's share of the
    /// total height is its share of the players, the same reading as SpeechChem's (share of the
    /// chart's filled area, never "percent of the tallest bar"). The marker line stands over bucket
    /// clamp((score - 1) * n / maxScore, 0, n - 1) (LineRenderer.GetLineOffset), so bucket k holds the
    /// scores ceil(k * maxScore / n) + 1 .. ceil((k + 1) * maxScore / n), the first from 0.
    /// BCL-pure; unit-tested.
    /// </summary>
    public static class HistogramBins
    {
        /// <summary>The bucket the game's marker for this score stands over.</summary>
        public static int MarkerBucket(int score, int buckets, int maxScore)
        {
            if (buckets <= 0 || maxScore <= 0) return -1;
            long k = (long)(score - 1) * buckets / maxScore;
            return (int)Math.Max(0, Math.Min(buckets - 1, k));
        }

        /// <summary>The bars as rows: every drawn bar, plus a marker's bucket even when empty.</summary>
        public static List<HistogramBin> Bins(IReadOnlyList<float> heights, int maxScore, int? marker, int? marker2 = null)
        {
            var bins = new List<HistogramBin>();
            if (heights == null || heights.Count == 0 || maxScore <= 0) return bins;
            int n = heights.Count;
            int mark = marker.HasValue ? MarkerBucket(marker.Value, n, maxScore) : -1;
            int mark2 = marker2.HasValue ? MarkerBucket(marker2.Value, n, maxScore) : -1;
            double total = 0;
            foreach (var h in heights) total += Math.Max(0f, h);
            for (int k = 0; k < n; k++)
            {
                double height = Math.Max(0f, heights[k]);
                if (height <= 0 && k != mark && k != mark2) continue;
                int low = k == 0 ? 0 : CeilDiv((long)k * maxScore, n) + 1;
                int high = CeilDiv((long)(k + 1) * maxScore, n);
                bins.Add(new HistogramBin
                {
                    Low = low,
                    High = Math.Max(low, high),
                    Percent = total > 0 ? (int)Math.Round(100 * height / total) : 0,
                    Drawn = height > 0,
                    Marked = k == mark,
                    Marked2 = k == mark2,
                });
            }
            return bins;
        }

        private static int CeilDiv(long a, long b) => (int)((a + b - 1) / b);
    }
}
