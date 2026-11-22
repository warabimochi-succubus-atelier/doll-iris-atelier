using System;
using System.Linq;
using System.Text.Json;
using DollIrisGenerator.Core.Geometry;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using Xunit;

namespace DollIrisGenerator.Tests
{
    /// <summary>二色分け (SPECIFICATION.md 7.1.4): one side of the iris, past an arched boundary, in another color.</summary>
    public class TwoToneTests
    {
        private static IrisParameters Split() => new() { TwoToneIntensity = 0.8 };

        [Fact]
        public void OffByDefault_AndExportsAsBefore()
        {
            var p = new IrisParameters();
            Assert.Equal(0.0, p.TwoToneIntensity);
            Assert.DoesNotContain("_two_tone", SvgExporter.GeneratePairSvg(p));
        }

        [Fact]
        public void Region_BelowAnArch_ForTheDefaults()
        {
            // Down, height 0.4, curve 0.25: the boundary is at y = 0.2 in the middle and lower at the sides
            var region = IrisPatterns.TwoToneRegion(0.4, 0.25, 90.0);
            Assert.Equal(IrisPatterns.TwoTonePoints + 2, region.Length);
            var middle = region[IrisPatterns.TwoTonePoints / 2];
            Assert.Equal(0.0, middle.X, 9);
            Assert.Equal(0.2, middle.Y, 9);
            // v = ±1: y = 0.2 + 0.25
            Assert.Contains(region, q => Math.Abs(Math.Abs(q.X) - 1.0) < 1e-9 && Math.Abs(q.Y - 0.45) < 1e-9);
            Assert.True(Contains(region, new Point(0, 0.9)));
            Assert.False(Contains(region, new Point(0, 0.1)));
            Assert.False(Contains(region, new Point(0, -0.9)));
        }

        [Fact]
        public void Angle_TurnsTheRegion_AndCurveSignFlipsTheArch()
        {
            var up = IrisPatterns.TwoToneRegion(0.3, 0.0, 270.0);
            Assert.True(Contains(up, new Point(0, -0.8)));
            Assert.False(Contains(up, new Point(0, 0.8)));
            // Straight: the same height everywhere across
            Assert.All(up.Take(IrisPatterns.TwoTonePoints), q => Assert.Equal(-0.4, q.Y, 9));

            // A sag: the sides come in further than the middle
            var sag = IrisPatterns.TwoToneRegion(0.4, -0.3, 90.0);
            Assert.True(sag[0].Y < sag[IrisPatterns.TwoTonePoints / 2].Y);
        }

        private static bool Contains(Point[] poly, Point q)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].Y > q.Y) != (poly[j].Y > q.Y)
                    && q.X < (poly[j].X - poly[i].X) * (q.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X)
                    inside = !inside;
            }
            return inside;
        }

        [Fact]
        public void Svg_Crisp_FillsTheRegion_UnderTheRaysLimbalRingAndPupil()
        {
            var p = Split();
            string svg = SvgExporter.GeneratePairSvg(p);
            Assert.Contains("<path id=\"left_eye_two_tone\" d=\"M", svg);
            Assert.Contains("fill=\"#82A8FF\" opacity=\"0.80\" />", svg);
            int split = svg.IndexOf("id=\"left_eye_two_tone\"", StringComparison.Ordinal);
            Assert.True(svg.IndexOf("id=\"left_eye_base\"", StringComparison.Ordinal) < split);
            foreach (string over in new[] { "left_eye_upper_shadow", "left_eye_rays", "left_eye_limbal_ring", "left_eye_pupil" })
                Assert.True(split < svg.IndexOf($"id=\"{over}\"", StringComparison.Ordinal), over);
            System.Xml.Linq.XDocument.Parse(svg);
        }

        [Fact]
        public void Svg_Soft_UsesAMaskOverTheIris_AndGradientsWork()
        {
            var p = Split();
            p.TwoToneSoftness = 0.4;
            p.TwoToneBlend = LayerBlend.SoftLight;
            p.TwoToneColor = "linear-gradient(90deg, #FFFFFF 0%, #82A8FF 100%)";
            string svg = SvgExporter.GeneratePairSvg(p);
            Assert.Contains("<mask id=\"left_eye_two_tone_mask\">", svg);
            Assert.Contains("<ellipse id=\"left_eye_two_tone\"", svg);
            Assert.Contains("fill=\"url(#left_eye_two_tone_fill)\" opacity=\"0.80\" style=\"mix-blend-mode:soft-light\" mask=\"url(#left_eye_two_tone_mask)\" />", svg);
            System.Xml.Linq.XDocument.Parse(svg);

            // A shaped outline: the body is the outline
            p.OutlineTopFlat = 0.4;
            Assert.Contains("<use id=\"left_eye_two_tone\" href=\"#left_eye_outline\"", SvgExporter.GeneratePairSvg(p));
        }

        [Fact]
        public void RightEye_MirrorsTheSide()
        {
            var p = Split();
            p.TwoToneAngle = 0; // the right-hand side on the left eye
            string svg = SvgExporter.GeneratePairSvg(p, includePrintGuides: false);
            // The right eye's region is turned to 180° (its left-hand side): compare the regions' first points
            var left = IrisPatterns.TwoToneRegion(p.TwoToneHeight, p.TwoToneCurve, 0.0);
            var right = IrisPatterns.TwoToneRegion(p.TwoToneHeight, p.TwoToneCurve, 180.0);
            int n = IrisPatterns.TwoTonePoints;
            for (int i = 0; i < n; i++)
            {
                // The boundary is symmetric across, so the mirror image is the 180° boundary read backwards
                Assert.Equal(-left[i].X, right[n - 1 - i].X, 9);
                Assert.Equal(left[i].Y, right[n - 1 - i].Y, 9);
            }
            Assert.Contains("id=\"right_eye_two_tone\"", svg);
        }

        [Fact]
        public void Settings_AreClamped_AndSaved()
        {
            var p = new IrisParameters
            {
                TwoToneIntensity = 2, TwoToneHeight = 0, TwoToneCurve = 5, TwoToneSoftness = -1, TwoToneAngle = -90,
                TwoToneBlend = (LayerBlend)42, TwoToneColor = null!
            };
            Assert.Equal(1.0, p.TwoToneIntensity);
            Assert.Equal(IrisParameters.MinTwoToneHeight, p.TwoToneHeight);
            Assert.Equal(IrisParameters.MaxTwoToneCurve, p.TwoToneCurve);
            Assert.Equal(0.0, p.TwoToneSoftness);
            Assert.Equal(270.0, p.TwoToneAngle);
            Assert.Equal(LayerBlend.Normal, p.TwoToneBlend);
            Assert.Equal(IrisParameters.DefaultTwoToneColor, p.TwoToneColor);

            var q = new IrisParameters { TwoToneIntensity = 0.5, TwoToneCurve = -0.3, TwoToneColor = "#102030" };
            var restored = JsonSerializer.Deserialize<IrisParameters>(JsonSerializer.Serialize(q))!;
            Assert.Equal(0.5, restored.TwoToneIntensity);
            Assert.Equal(-0.3, restored.TwoToneCurve);
            Assert.Equal("#102030", restored.TwoToneColor);
            Assert.Equal(0.0, JsonSerializer.Deserialize<IrisParameters>("{\"FiberNoise\": 0.2}")!.TwoToneIntensity);
        }

        [Fact]
        public void Presets_CarryTheSplit_AndBuiltInsTurnItOff()
        {
            var design = Split();
            design.TwoToneAngle = 270;
            var target = new IrisParameters();
            ColorPresets.ApplyPreset(target, ColorPresets.FromDesign(design, "test", ""));
            Assert.Equal(0.8, target.TwoToneIntensity);
            Assert.Equal(270, target.TwoToneAngle);

            ColorPresets.ApplyPreset(target, ColorPresets.All[0]);
            Assert.Equal(0.0, target.TwoToneIntensity);
        }

        [Fact]
        public void Randomizer_SometimesSplits_NeverForTheOddEye_AndTheColorLockKeepsIt()
        {
            int on = 0;
            for (int i = 0; i < 400; i++)
            {
                var p = new IrisParameters();
                IrisRandomizer.RandomizeAttributes(p, new Random(i));
                if (p.TwoToneIntensity > 0)
                {
                    on++;
                    Assert.False(p.IsOddEye);
                    Assert.Equal(90.0, p.TwoToneAngle);
                }
            }
            Assert.InRange(on, 25, 100); // about 15% of the non-odd-eye designs

            var kept = Split();
            kept.TwoToneColor = "#010203";
            IrisRandomizer.RandomizeAttributes(kept, RandomizeLocks.Colors, new Random(3));
            Assert.Equal(0.8, kept.TwoToneIntensity);
            Assert.Equal("#010203", kept.TwoToneColor);
        }
    }
}
