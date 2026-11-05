using MagnumOpus.Core.Text;
using Xunit;

namespace MagnumOpus.Tests
{
    // Directions on the alchemical diagram come from where the game draws the labels (y up). The
    // figures are the stock diagram's label positions relative to Salt's (761, 474).
    public class CompassTests
    {
        [Fact]
        public void TheStockDiagramReadsAsDrawn()
        {
            Assert.Equal(Compass.Dir.Above, Compass.Of(0, 228));      // Air
            Assert.Equal(Compass.Dir.Left, Compass.Of(-124, 2));      // Fire
            Assert.Equal(Compass.Dir.Right, Compass.Of(126, 0));      // Water
            Assert.Equal(Compass.Dir.Below, Compass.Of(2, -130));     // Earth
            Assert.Equal(Compass.Dir.UpperRight, Compass.Of(169, 171)); // Vitae
            Assert.Equal(Compass.Dir.LowerLeft, Compass.Of(-172, -174)); // Mors
            Assert.Equal(Compass.Dir.UpperLeft, Compass.Of(-50, 60));
            Assert.Equal(Compass.Dir.LowerRight, Compass.Of(50, -60));
        }

        [Fact]
        public void SectorsAreCentredOnTheAxes()
        {
            Assert.Equal(Compass.Dir.Right, Compass.Of(100, 41));     // 22.3 degrees
            Assert.Equal(Compass.Dir.UpperRight, Compass.Of(100, 42)); // 22.8 degrees
            Assert.Equal(Compass.Dir.Right, Compass.Of(100, -41));
            Assert.Equal(Compass.Dir.Left, Compass.Of(-100, -1));
            Assert.Null(Compass.Of(0, 0));
        }

        [Fact]
        public void ARunGoesAlongItsMainAxis()
        {
            Assert.Equal(Compass.Dir.Above, Compass.Run(0, 520));     // Lead to Gold: bottom to top
            Assert.Equal(Compass.Dir.Below, Compass.Run(10, -500));
            Assert.Equal(Compass.Dir.Right, Compass.Run(300, 10));
            Assert.Equal(Compass.Dir.Left, Compass.Run(-300, 10));
            Assert.Null(Compass.Run(0, 0));
        }
    }
}
