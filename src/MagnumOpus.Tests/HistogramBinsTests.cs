using System.Linq;
using MagnumOpus.Core.Text;
using Xunit;

namespace MagnumOpus.Tests
{
    public class HistogramBinsTests
    {
        [Fact]
        public void SharesAreOfTheTotalNotTheTallestBar()
        {
            // Normalized to the tallest bar, as the game stores them: counts 10, 20, 10.
            var bins = HistogramBins.Bins(new[] { 0.5f, 1f, 0.5f }, 30, null);
            Assert.Equal(new[] { 25, 50, 25 }, bins.Select(b => b.Percent).ToArray());
            Assert.Equal(100, bins.Sum(b => b.Percent));
        }

        [Fact]
        public void RangesFollowTheGamesMarkerMath()
        {
            // 4 buckets over 0..100: marker bucket = clamp((s - 1) * 4 / 100).
            var bins = HistogramBins.Bins(new[] { 1f, 1f, 1f, 1f }, 100, null);
            Assert.Equal(new[] { 0, 26, 51, 76 }, bins.Select(b => b.Low).ToArray());
            Assert.Equal(new[] { 25, 50, 75, 100 }, bins.Select(b => b.High).ToArray());
            Assert.Equal(0, HistogramBins.MarkerBucket(25, 4, 100));
            Assert.Equal(1, HistogramBins.MarkerBucket(26, 4, 100));
            Assert.Equal(3, HistogramBins.MarkerBucket(500, 4, 100));
            Assert.Equal(0, HistogramBins.MarkerBucket(0, 4, 100));
        }

        [Fact]
        public void EmptyBarsAreSkippedUnlessMarked()
        {
            var bins = HistogramBins.Bins(new[] { 1f, 0f, 0.5f }, 30, 15);
            Assert.Equal(3, bins.Count);
            Assert.True(bins[1].Marked);
            Assert.False(bins[1].Drawn);
            Assert.Equal(0, bins[1].Percent);
            var unmarked = HistogramBins.Bins(new[] { 1f, 0f, 0.5f }, 30, null);
            Assert.Equal(2, unmarked.Count);
        }

        [Fact]
        public void NoDataNoBins()
        {
            Assert.Empty(HistogramBins.Bins(new float[0], 30, 5));
            Assert.Empty(HistogramBins.Bins(new[] { 1f }, 0, 5));
        }
    }
}
