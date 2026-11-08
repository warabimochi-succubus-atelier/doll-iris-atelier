using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DollIrisGenerator.Core.Geometry;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;

namespace DollIrisGenerator.Tests
{
    /// <summary>
    /// The faster paths the browser app relies on (no regex for plain colors, no string per number or color)
    /// must give exactly what the code they replaced gave, so the desktop app's files do not change.
    /// </summary>
    public class FastPathParityTests
    {
        // The pattern IsHexColor used before (on the trimmed value)
        private static readonly Regex OldHexRegex = new(@"^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$");

        [Theory]
        [InlineData("#FFF")]
        [InlineData("#abc")]
        [InlineData("#A1B2C3")]
        [InlineData("#80FF0000")]
        [InlineData("  #123456 \t")]
        [InlineData("#123456\n")]
        [InlineData("#12345")]
        [InlineData("#1234567")]
        [InlineData("#12G456")]
        [InlineData("123456")]
        [InlineData("##12345")]
        [InlineData("#１２３")] // full-width digits are not hex digits
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        [InlineData("radial-gradient(#FFFFFF 0%, #000000 100%)")]
        public void TestIsHexColorMatchesTheOldPattern(string? value)
        {
            bool expected = value != null && OldHexRegex.IsMatch(value.Trim());
            Assert.Equal(expected, ColorFill.IsHexColor(value));
        }

        [Fact]
        public void TestFormatArgbMatchesInterpolatedHex()
        {
            var rng = new Random(5);
            for (int i = 0; i < 2000; i++)
            {
                int a = rng.Next(256), r = rng.Next(256), g = rng.Next(256), b = rng.Next(256);
                Assert.Equal($"#{a:X2}{r:X2}{g:X2}{b:X2}", ColorFill.FormatArgb(a, r, g, b));
            }
            Assert.Equal("#00000000", ColorFill.FormatArgb(0, 0, 0, 0));
            Assert.Equal("#FFFFFFFF", ColorFill.FormatArgb(255, 255, 255, 255));
        }

        [Fact]
        public void TestAppendFixedMatchesToFixed()
        {
            var rng = new Random(9);
            var sb = new StringBuilder();
            for (int i = 0; i < 5000; i++)
            {
                double v = (rng.NextDouble() - 0.5) * Math.Pow(10, rng.Next(-4, 10));
                for (int d = 0; d <= 3; d++)
                {
                    sb.Clear();
                    Assert.Equal(v.ToString("F" + d, CultureInfo.InvariantCulture), sb.AppendFixed(v, d).ToString());
                }
            }
            sb.Clear();
            Assert.Equal(double.NaN.ToString("F3", CultureInfo.InvariantCulture), sb.AppendFixed(double.NaN, 3).ToString());
        }

        [Fact]
        public void TestConicWedgeColorsMatchColorAt()
        {
            var fill = ColorFill.Parse("conic-gradient(from 30deg, #80FF0000 0%, #00FF00 40%, #0000FF 100%)");
            var map = GradientMapping.Create(fill, new Rect(0, 0, 10, 8));
            int i = 0;
            foreach (var (_, _, color) in map.ConicWedges())
            {
                double t = (i + 0.5) / GradientMapping.ConicSegments;
                Assert.Equal(ColorFill.ColorAt(map.Stops, t), color);
                i++;
            }
            Assert.Equal(GradientMapping.ConicSegments, i);
        }

        [Fact]
        public void TestParseWithoutParenthesisIsSolidAsBefore()
        {
            foreach (var value in new[] { "#FF0000", "linear-gradient", "conic-gradient #FFF", " #80D8FF " })
            {
                var fill = ColorFill.Parse(value);
                Assert.False(fill.IsGradient);
                Assert.Equal(value, fill.PrimaryColor);
            }
        }

        [Fact]
        public void TestNebulaPngBase64IsTheEncodedPng()
        {
            var p = new IrisParameters { NebulaOpacity = 0.7, NebulaColor = IrisParameters.DefaultNebulaColor };
            foreach (bool mirror in new[] { false, true })
            {
                string expected = Convert.ToBase64String(NebulaTexture.Png(p, 321, mirror));
                Assert.Equal(expected, NebulaTexture.PngBase64(p, 321, mirror));
                Assert.Same(NebulaTexture.PngBase64(p, 321, mirror), NebulaTexture.PngBase64(p, 321, mirror));
            }
        }
    }
}
