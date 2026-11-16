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

        public static TheoryData<double, double, double, string, int> NebulaCases => new()
        {
            { 1.2, 0.5, 0.0, IrisParameters.DefaultNebulaColor, 12345 },
            { IrisParameters.MinNebulaScale, 0.0, -1.0, "#80FF00AA", 1 },
            { IrisParameters.MaxNebulaScale, 1.0, 1.0, "#B388FF", 987654 },
            { 1.68, 0.52, -0.19, "radial-gradient(#FFFFFF 0%, #3050FF 100%)", 264374 },
            { 2.3, 0.8, 0.45, "linear-gradient(30deg, #FF80AB 0%, #40C4FF 100%)", -7 },
        };

        [Theory]
        [MemberData(nameof(NebulaCases))]
        public void TestNebulaPixelsMatchTheStraightforwardCode(double scale, double density, double swirl, string color, int seed)
        {
            var p = new IrisParameters { NebulaOpacity = 0.8, NebulaScale = scale, NebulaDensity = density, NebulaSwirl = swirl, NebulaColor = color };
            var reference = ReferenceNebula(p, seed);
            Assert.Equal(reference, NebulaTexture.Rgba(p, seed, false));

            // The right eye: the same pixels, each row the other way round
            int n = NebulaTexture.Resolution;
            var mirrored = new byte[reference.Length];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    Array.Copy(reference, (y * n + n - 1 - x) * 4, mirrored, (y * n + x) * 4, 4);
            Assert.Equal(mirrored, NebulaTexture.Rgba(p, seed, true));

            // Another amount and color over the same clouds (the cloud shape is reused for these)
            p.NebulaDensity = 1.0 - density;
            p.NebulaColor = "#4000FFCC";
            Assert.Equal(ReferenceNebula(p, seed), NebulaTexture.Rgba(p, seed, false));
        }

        // ---- NebulaTexture.Generate as it was written first (one call per noise sample) ----

        private static byte[] ReferenceNebula(IrisParameters p, int seed)
        {
            int n = NebulaTexture.Resolution;
            var px = new byte[n * n * 4];
            uint s = unchecked((uint)(seed + 4000));
            var fill = ColorFill.Parse(p.NebulaColor);
            var colorAt = fill.IsGradient ? GradientMapping.Create(fill, new Rect(-1, -1, 2, 2)).ArgbSampler() : null;
            (int A, int R, int G, int B) solid = GeometryHelper.ParseHexArgb(GeometryHelper.SanitizeColorHex(p.NebulaColor, "#B388FF"));
            double freq = 2.2 * p.NebulaScale;
            double center = 0.66 - 0.32 * p.NebulaDensity;
            const double Soft = 0.2;
            double twist = p.NebulaSwirl * 2.6;
            for (int y = 0; y < n; y++)
            {
                double v = (y + 0.5) / n * 2.0 - 1.0;
                for (int x = 0; x < n; x++)
                {
                    double u = (x + 0.5) / n * 2.0 - 1.0;
                    double r = Math.Sqrt(u * u + v * v);
                    double ang = Math.Atan2(v, u) + twist * r;
                    double su = r * Math.Cos(ang), sv = r * Math.Sin(ang);
                    double wx = RefFbm(su * freq + 5.2, sv * freq + 1.3, s ^ 0x9E3779B9u);
                    double wy = RefFbm(su * freq + 1.7, sv * freq + 9.2, s ^ 0x85EBCA6Bu);
                    double cloud = RefFbm(su * freq + 1.6 * wx, sv * freq + 1.6 * wy, s);
                    double t = RefSmoothStep(center - Soft, center + Soft, cloud);
                    t *= 1.0 - RefSmoothStep(0.88, 1.0, r);
                    var c = colorAt != null ? colorAt(new Point(u, v)) : solid;
                    int i = (y * n + x) * 4;
                    px[i] = (byte)c.R;
                    px[i + 1] = (byte)c.G;
                    px[i + 2] = (byte)c.B;
                    px[i + 3] = (byte)Math.Clamp((int)Math.Round(c.A * t), 0, 255);
                }
            }
            return px;
        }

        private static double RefSmoothStep(double a, double b, double x)
        {
            double t = Math.Clamp((x - a) / (b - a), 0.0, 1.0);
            return t * t * (3.0 - 2.0 * t);
        }

        private static double RefFbm(double x, double y, uint seed)
        {
            double sum = 0, amp = 0.5, norm = 0;
            for (int o = 0; o < 5; o++)
            {
                sum += amp * RefValueNoise(x, y, seed + (uint)o * 0x632BE5ABu);
                norm += amp;
                amp *= 0.5;
                x *= 2.03;
                y *= 2.03;
            }
            return sum / norm;
        }

        private static double RefValueNoise(double x, double y, uint seed)
        {
            int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y);
            double fx = x - xi, fy = y - yi;
            double ux = fx * fx * (3 - 2 * fx), uy = fy * fy * (3 - 2 * fy);
            double a = RefHash(xi, yi, seed), b = RefHash(xi + 1, yi, seed);
            double c = RefHash(xi, yi + 1, seed), d = RefHash(xi + 1, yi + 1, seed);
            return a + (b - a) * ux + (c - a) * uy + (a - b - c + d) * ux * uy;
        }

        private static double RefHash(int x, int y, uint seed)
        {
            unchecked
            {
                uint h = (uint)x * 0x8DA6B343u ^ (uint)y * 0xD8163841u ^ seed * 0xCB1AB31Fu;
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                h *= 0x297A2D39u;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / 16777215.0;
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
