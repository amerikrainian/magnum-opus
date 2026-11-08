using System;
using MagnumOpus.Core.Strings;
using Xunit;

namespace MagnumOpus.Tests
{
    /// <summary>Direction words follow the Abbreviate directions setting, wherever a string names one.</summary>
    public class DirectionWordsTests : IDisposable
    {
        public void Dispose()
        {
            Strings.AbbreviateDirections = () => false;
            Strings.LoadTranslation(null);
        }

        [Fact]
        public void Directions_are_full_words_by_default()
        {
            Assert.Equal("northwest", Strings.HexDirection(2));
            Assert.Equal("Cursor northwest", Strings.Get("bind.ed.hex.nw"));
        }

        [Theory]
        [InlineData(0, "e")]
        [InlineData(1, "ne")]
        [InlineData(2, "nw")]
        [InlineData(3, "w")]
        [InlineData(4, "sw")]
        [InlineData(5, "se")]
        public void The_setting_shortens_each_direction(int dir, string expected)
        {
            Strings.AbbreviateDirections = () => true;
            Assert.Equal(expected, Strings.HexDirection(dir));
        }

        [Fact]
        public void The_setting_reaches_labels_and_filled_templates()
        {
            Strings.AbbreviateDirections = () => true;
            Assert.Equal("Skip se", Strings.Get("bind.ed.skip.se"));
            string text = Strings.F("tut.intro.move", "a", "b", "Q", "E", "A", "D", "Z", "C");
            Assert.Contains("Q nw, E ne, A w, D e, Z sw and C se.", text);
        }

        [Fact]
        public void A_translation_supplies_its_own_words()
        {
            Strings.LoadTranslation(new[] { "hex.dir.3=ouest", "hex.short.3=o", "bind.ed.hex.w=Curseur <dir:3>" });
            Assert.Equal("Curseur ouest", Strings.Get("bind.ed.hex.w"));
            Strings.AbbreviateDirections = () => true;
            Assert.Equal("Curseur o", Strings.Get("bind.ed.hex.w"));
        }
    }
}
