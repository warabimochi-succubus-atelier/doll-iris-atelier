using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using Xunit;

namespace DollIrisGenerator.Tests
{
    /// <summary>虹彩の輪郭 (SPECIFICATION.md 6.1): shaped outlines instead of the ellipse.</summary>
    public class IrisOutlineTests
    {
        private static IrisParameters Shield() => new()
        {
            SizeMm = 10, Shape = IrisShape.VerticalOval, AspectRatio = 1.2,
            OutlineTaper = 0.6, OutlineTopFlat = 0.5, OutlineSquareness = 0.3
        };

        [Fact]
        public void AllZero_IsTheEllipse_AndExportsAsBefore()
        {
            var p = new IrisParameters();
            Assert.True(IrisOutline.IsEllipse(p));
            string svg = SvgExporter.GeneratePairSvg(p, true);
            Assert.DoesNotContain("_outline", svg);
            Assert.Contains("<ellipse id=\"left_eye_base\"", svg);
        }

        [Theory]
        [InlineData(0.6, 0.5, 0.0, 0.3)]
        [InlineData(-1.0, 0.0, 1.0, 0.0)]
        [InlineData(0.0, 1.0, 1.0, 1.0)]
        [InlineData(1.0, 0.0, 0.0, 0.0)]
        public void Outline_FillsTheIrisBox_AndIsLeftRightSymmetric(double taper, double top, double bottom, double square)
        {
            double rx = 5.0, ry = 6.0;
            var pts = IrisOutline.Build(taper, top, bottom, square, rx, ry).Flatten();
            Assert.InRange(pts.Min(q => q.X), -rx * 1.005, -rx * 0.995);
            Assert.InRange(pts.Max(q => q.X), rx * 0.995, rx * 1.005);
            Assert.InRange(pts.Min(q => q.Y), -ry * 1.005, -ry * 0.995);
            Assert.InRange(pts.Max(q => q.Y), ry * 0.995, ry * 1.005);

            // Every point has a mirror image on the outline
            foreach (var q in pts.Where((_, i) => i % 50 == 0))
            {
                double nearest = pts.Min(o => Math.Sqrt((o.X + q.X) * (o.X + q.X) + (o.Y - q.Y) * (o.Y - q.Y)));
                Assert.True(nearest < 0.02, $"no mirror point near ({-q.X}, {q.Y}): {nearest}");
            }
        }

        private static double WidthAt(System.Collections.Generic.List<DollIrisGenerator.Core.Geometry.Point> pts, double y) =>
            2.0 * pts.Where(q => Math.Abs(q.Y - y) < 0.05).Max(q => Math.Abs(q.X));

        [Fact]
        public void Taper_WidensTheTop_OrTheBottom()
        {
            var shield = IrisOutline.Build(0.8, 0, 0, 0, 1, 1).Flatten(48);
            Assert.True(WidthAt(shield, -0.6) > WidthAt(shield, 0.6) * 1.3);
            var drop = IrisOutline.Build(-0.8, 0, 0, 0, 1, 1).Flatten(48);
            Assert.True(WidthAt(drop, 0.6) > WidthAt(drop, -0.6) * 1.3);
        }

        [Fact]
        public void TopFlat_FlattensOnlyTheTop()
        {
            var pts = IrisOutline.Build(0, 1.0, 0, 0, 1, 1).Flatten(48);
            // Near the top the outline runs almost straight across; the bottom stays round
            Assert.True(WidthAt(pts, -0.9) > WidthAt(pts, 0.9) * 1.5);
        }

        [Fact]
        public void Svg_ClipRingBaseAndCutLine_UseTheOutline()
        {
            var p = Shield();
            p.LimbalRingSoftness = 0.0;
            string hard = SvgExporter.GeneratePairSvg(p, true);
            foreach (string eye in new[] { "left_eye", "right_eye" })
            {
                Assert.Contains($"<path id=\"{eye}_outline\" d=\"M ", hard);
                Assert.Contains($"<clipPath id=\"{eye}_clip\">", hard);
                Assert.Contains($"<use id=\"{eye}_base\" href=\"#{eye}_outline\"", hard);
                Assert.Contains($"<use id=\"{eye}_limbal_ring\" href=\"#{eye}_outline\" fill=\"none\"", hard);
                Assert.Contains($"<use href=\"#{eye}_outline\" fill=\"none\" stroke=\"#999999\"", hard);
                Assert.DoesNotContain($"<ellipse id=\"{eye}_base\"", hard);
            }

            p.LimbalRingSoftness = 0.5;
            string soft = SvgExporter.GeneratePairSvg(p, false);
            Assert.Contains("<mask id=\"left_eye_limbal_mask\">", soft);
            Assert.Contains("mask=\"url(#left_eye_limbal_mask)\"", soft);
            Assert.DoesNotContain("left_eye_limbal_mask_grad", soft);
            Assert.True(SvgValidatorAccepts(soft));
        }

        private static bool SvgValidatorAccepts(string svg)
        {
            try { System.Xml.Linq.XDocument.Parse(svg); return true; }
            catch { return false; }
        }

        [Theory]
        [InlineData(0.4, 0.04)]
        [InlineData(0.8, 0.29)]
        [InlineData(0.2, 0.01)]
        public void SoftRingStrokes_FollowTheGaussianEdge(double width, double sigma)
        {
            var strokes = SvgExporter.OutlineRingStrokes(width, sigma);
            Assert.InRange(strokes.Length, SvgExporter.MinOutlineRingSteps, SvgExporter.MaxOutlineRingSteps);
            Assert.True(strokes.Zip(strokes.Skip(1)).All(z => z.First.HalfWidthMm > z.Second.HalfWidthMm), "widest first");
            // Mask value at distance d from the rim: white strokes stacked over each other
            for (double d = 0.0; d < width + 3 * sigma; d += width / 37.0)
            {
                double clear = 1.0;
                foreach (var (half, opacity) in strokes)
                    if (d < half) clear *= 1.0 - opacity;
                double expected = SvgExporter.NormalCdf((width - d) / sigma);
                Assert.True(Math.Abs((1.0 - clear) - expected) < 0.12, $"d = {d}: {1.0 - clear} vs {expected}");
            }
        }

        [Fact]
        public void Footprint_CoversTheTiltedOutline()
        {
            var p = new IrisParameters { SizeMm = 10, Shape = IrisShape.HorizontalOval, AspectRatio = 1.5, TiltAngle = 30, OutlineSquareness = 1.0 };
            var (w, h) = SheetLayout.GetEyeFootprintMm(p);
            var ellipse = SheetLayout.GetEyeFootprintMm(new IrisParameters { SizeMm = 10, Shape = IrisShape.HorizontalOval, AspectRatio = 1.5, TiltAngle = 30 });
            Assert.True(w > ellipse.W && h > ellipse.H, "a rounded square reaches further than the ellipse when tilted");

            double t = 30 * Math.PI / 180;
            foreach (var q in IrisOutline.Build(p, 7.5, 5.0).Flatten())
            {
                Assert.True(Math.Abs(q.X * Math.Cos(t) - q.Y * Math.Sin(t)) <= w / 2 + 0.005);
                Assert.True(Math.Abs(q.X * Math.Sin(t) + q.Y * Math.Cos(t)) <= h / 2 + 0.005);
            }

            // Untilted: the box itself
            p.TiltAngle = 0;
            Assert.Equal((15.0, 10.0), SheetLayout.GetEyeFootprintMm(p));
        }

        [Fact]
        public void PdfCutLine_FollowsTheOutline()
        {
            var sb = new StringBuilder();
            PdfExporter.AppendCutOutline(sb, 20, 30, IrisOutline.Build(Shield(), 5, 6), 10);
            string ops = sb.ToString();
            Assert.Contains(" cm", ops);
            Assert.Equal(IrisOutline.PointCount, ops.Split('\n').Count(l => l.TrimEnd().EndsWith(" c")));
            Assert.Contains("h S", ops);
        }

        [Fact]
        public void Settings_AreClamped_AndSavedInJson()
        {
            var p = new IrisParameters { OutlineTaper = 3, OutlineTopFlat = -1, OutlineBottomFlat = double.NaN, OutlineSquareness = 2 };
            Assert.Equal(1.0, p.OutlineTaper);
            Assert.Equal(0.0, p.OutlineTopFlat);
            Assert.Equal(0.0, p.OutlineBottomFlat);
            Assert.Equal(1.0, p.OutlineSquareness);

            var restored = JsonSerializer.Deserialize<IrisParameters>(JsonSerializer.Serialize(Shield()))!;
            Assert.Equal(0.6, restored.OutlineTaper);
            Assert.Equal(0.5, restored.OutlineTopFlat);
            Assert.Equal(0.3, restored.OutlineSquareness);

            // A file saved before the outline existed reads as the ellipse
            var old = JsonSerializer.Deserialize<IrisParameters>("{\"SizeMm\": 10.0}")!;
            Assert.True(IrisOutline.IsEllipse(old));
        }

        [Fact]
        public void QuickStyles_ApplyAndMatch()
        {
            var p = new IrisParameters();
            Assert.Equal("楕円", IrisOutline.Match(p)?.Name);
            foreach (var style in IrisOutline.Styles)
            {
                IrisOutline.Apply(p, style);
                Assert.Equal(style.Name, IrisOutline.Match(p)?.Name);
            }
            p.OutlineTaper = 0.11;
            Assert.Null(IrisOutline.Match(p));
        }

        [Fact]
        public void PresetsAndRandomizer_KeepTheOutline()
        {
            var p = Shield();
            ColorPresets.ApplyPreset(p, ColorPresets.All[3]);
            IrisRandomizer.RandomizeAttributes(p, new Random(1));
            Assert.Equal(0.6, p.OutlineTaper);
            Assert.Equal(0.5, p.OutlineTopFlat);
            Assert.Equal(0.3, p.OutlineSquareness);
        }
    }
}
