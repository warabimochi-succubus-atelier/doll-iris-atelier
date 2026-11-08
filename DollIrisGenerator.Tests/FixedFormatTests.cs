using System;
using System.Globalization;
using DollIrisGenerator.Services;

namespace DollIrisGenerator.Tests
{
    /// <summary>
    /// FixedFormat.ToFixed must write exactly what ToString("F1" / "F2" / "F3") writes: the SVG export uses it,
    /// and the desktop app and the browser app must keep producing byte-identical files.
    /// </summary>
    public class FixedFormatTests
    {
        private static void Check(double value)
        {
            for (int d = 0; d <= 3; d++)
            {
                string expected = value.ToString("F" + d, CultureInfo.InvariantCulture);
                Assert.True(expected == value.ToFixed(d), $"{value:R} F{d}: expected {expected}, got {value.ToFixed(d)}");
            }
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-0.0)]
        [InlineData(0.0004)]
        [InlineData(-0.0004)]
        [InlineData(0.0005)]
        [InlineData(-0.0005)]
        [InlineData(0.0015)]
        [InlineData(1.0005)]
        [InlineData(2.675)]
        [InlineData(-2.675)]
        [InlineData(0.125)]
        [InlineData(0.375)]
        [InlineData(999.9995)]
        [InlineData(123456.789)]
        [InlineData(1e300)]
        [InlineData(-1e-300)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        [InlineData(double.Epsilon)]
        public void TestEdgeCasesMatchTheFramework(double value) => Check(value);

        [Fact]
        public void TestRandomValuesMatchTheFramework()
        {
            var rng = new Random(20261108);
            for (int i = 0; i < 300_000; i++)
            {
                // Mostly drawing-sized numbers (mm, -50..50), plus every magnitude
                double value = i % 3 == 0
                    ? (rng.NextDouble() - 0.5) * Math.Pow(10, rng.Next(-8, 12))
                    : (rng.NextDouble() - 0.5) * 100.0;
                Check(value);
            }
        }

        [Fact]
        public void TestValuesNextToRoundingTiesMatchTheFramework()
        {
            var rng = new Random(7);
            for (int i = 0; i < 100_000; i++)
            {
                // k.xxx5 and its neighbouring doubles, where rounding is decided
                double tie = (rng.Next(-200_000, 200_000) + 0.5) / 1000.0;
                Check(tie);
                Check(Math.BitIncrement(tie));
                Check(Math.BitDecrement(tie));
                double tie2 = (rng.Next(-20_000, 20_000) + 0.5) / 100.0;
                Check(tie2);
                Check(Math.BitIncrement(tie2));
                Check(Math.BitDecrement(tie2));
            }
        }
    }
}
