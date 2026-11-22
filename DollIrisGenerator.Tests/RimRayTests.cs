using System;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using Xunit;

namespace DollIrisGenerator.Tests
{
    /// <summary>外周からの線 (SPECIFICATION.md 8.2): tapered lines from the rim inward.</summary>
    public class RimRayTests
    {
        private static IrisParameters WithRimRays() => new() { RimRayIntensity = 0.6 };

        [Fact]
        public void OffByDefault_AndExportsAsBefore()
        {
            var p = new IrisParameters();
            Assert.Equal(0.0, p.RimRayIntensity);
            Assert.DoesNotContain("_rim_rays", SvgExporter.GeneratePairSvg(p));
        }

        [Fact]
        public void NoRandomness_SpacesTheLinesEvenly_FromTheTop()
        {
            var rays = IrisPatterns.RimRays(8, 0.0, 1, false);
            Assert.Equal(8, rays.Count);
            for (int i = 0; i < 8; i++)
            {
                Assert.Equal(-Math.PI / 2.0 + i * Math.PI / 4.0, rays[i].Angle, 9);
                Assert.Equal(1.0, rays[i].LengthFactor);
                Assert.Equal(1.0, rays[i].AlphaFactor);
            }
        }

        [Fact]
        public void Randomness_StaysWithinItsLimits_AndMoreLinesKeepTheFirstDraws()
        {
            var rays = IrisPatterns.RimRays(60, 1.0, 7, false);
            double step = 2.0 * Math.PI / 60;
            for (int i = 0; i < rays.Count; i++)
            {
                Assert.InRange(rays[i].Angle, -Math.PI / 2.0 + step * (i - 0.2) - 1e-9, -Math.PI / 2.0 + step * (i + 0.2) + 1e-9);
                Assert.InRange(rays[i].LengthFactor, 0.5, 1.0);
                Assert.InRange(rays[i].AlphaFactor, 0.6, 1.0);
            }
            // The same three draws per line: the length / strength sequence does not depend on the count
            var few = IrisPatterns.RimRays(10, 0.5, 3, false);
            var many = IrisPatterns.RimRays(40, 0.5, 3, false);
            Assert.Equal(few.Select(r => r.LengthFactor), many.Take(10).Select(r => r.LengthFactor));
        }

        [Fact]
        public void RightEye_IsTheMirrorImage()
        {
            var left = IrisPatterns.RimRays(30, 0.6, 9, false);
            var right = IrisPatterns.RimRays(30, 0.6, 9, true);
            for (int i = 0; i < left.Count; i++)
            {
                Assert.Equal(-Math.Cos(left[i].Angle), Math.Cos(right[i].Angle), 9);
                Assert.Equal(Math.Sin(left[i].Angle), Math.Sin(right[i].Angle), 9);
                Assert.Equal(left[i].LengthFactor, right[i].LengthFactor);
            }
        }

        [Fact]
        public void Svg_DrawsOneTaperedPathPerLine_UnderTheRaysAndTheLimbalRing()
        {
            var p = WithRimRays();
            p.RimRayCount = 20;
            p.RimRayColor = "#123456";
            string svg = SvgExporter.GeneratePairSvg(p);
            int group = svg.IndexOf("<g id=\"left_eye_rim_rays\" fill=\"#123456\">", StringComparison.Ordinal);
            Assert.True(group >= 0);
            int end = svg.IndexOf("</g>", group, StringComparison.Ordinal);
            // tip, two points at the rim, two past it
            var paths = Regex.Matches(svg[group..end], "<path d=\"M[^ ]+( L[^ ]+){4} Z\" fill-opacity=\"[0-9.]+\" />");
            Assert.Equal(20, paths.Count);
            Assert.True(group < svg.IndexOf("id=\"left_eye_rays\"", StringComparison.Ordinal));
            Assert.True(group < svg.IndexOf("id=\"left_eye_limbal_ring\"", StringComparison.Ordinal));
            Assert.True(svg.IndexOf("id=\"left_eye_upper_shadow\"", StringComparison.Ordinal) < group);
            System.Xml.Linq.XDocument.Parse(svg);
        }

        [Fact]
        public void Svg_TipSitsTheLengthInsideTheRim()
        {
            var p = WithRimRays();
            p.RimRayCount = 4;
            p.RimRayRandomness = 0;
            p.RimRayLength = 0.25;
            string svg = SvgExporter.GeneratePairSvg(p);
            // The first line points straight up: its tip is 75% of the iris radius above the center
            var m = Regex.Match(svg, "<g id=\"left_eye_rim_rays\"[^>]*>\\s*<path d=\"M([0-9.\\-]+),([0-9.\\-]+)");
            var c = Regex.Match(svg, "<ellipse id=\"left_eye_base\" cx=\"([0-9.]+)\" cy=\"([0-9.]+)\" rx=\"([0-9.]+)\" ry=\"([0-9.]+)\"");
            double Num(Group g) => double.Parse(g.Value, System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(Num(c.Groups[1]), Num(m.Groups[1]), 2);
            Assert.Equal(Num(c.Groups[2]) - 0.75 * Num(c.Groups[4]), Num(m.Groups[2]), 2);
        }

        [Fact]
        public void Settings_AreClamped_AndSaved()
        {
            var p = new IrisParameters
            {
                RimRayIntensity = 2, RimRayCount = 1, RimRayLength = 1, RimRayWidth = 0, RimRayRandomness = double.NaN, RimRayColor = null!
            };
            Assert.Equal(1.0, p.RimRayIntensity);
            Assert.Equal(IrisParameters.MinRimRayCount, p.RimRayCount);
            Assert.Equal(IrisParameters.MaxRimRayLength, p.RimRayLength);
            Assert.Equal(IrisParameters.MinRimRayWidth, p.RimRayWidth);
            Assert.Equal(IrisParameters.DefaultRimRayRandomness, p.RimRayRandomness);
            Assert.Equal(IrisParameters.DefaultRimRayColor, p.RimRayColor);

            var q = new IrisParameters { RimRayIntensity = 0.4, RimRayCount = 50, RimRayColor = "#102030" };
            var restored = JsonSerializer.Deserialize<IrisParameters>(JsonSerializer.Serialize(q))!;
            Assert.Equal(0.4, restored.RimRayIntensity);
            Assert.Equal(50, restored.RimRayCount);
            Assert.Equal("#102030", restored.RimRayColor);
            Assert.Equal(0.0, JsonSerializer.Deserialize<IrisParameters>("{\"RayCount\": 20}")!.RimRayIntensity);
        }

        [Fact]
        public void Presets_CarryTheLines_AndBuiltInsTurnThemOff()
        {
            var design = WithRimRays();
            design.RimRayCount = 64;
            var target = new IrisParameters();
            ColorPresets.ApplyPreset(target, ColorPresets.FromDesign(design, "test", ""));
            Assert.Equal(0.6, target.RimRayIntensity);
            Assert.Equal(64, target.RimRayCount);

            ColorPresets.ApplyPreset(target, ColorPresets.All[0]);
            Assert.Equal(0.0, target.RimRayIntensity);
        }

        [Fact]
        public void Randomizer_SometimesAddsLines_AndTheRaysLockKeepsThem()
        {
            int on = 0;
            for (int i = 0; i < 300; i++)
            {
                var p = new IrisParameters();
                IrisRandomizer.RandomizeAttributes(p, new Random(i));
                if (p.RimRayIntensity > 0) on++;
            }
            Assert.InRange(on, 30, 90); // about 20%

            var kept = WithRimRays();
            kept.RimRayCount = 99;
            kept.RimRayColor = "#010101";
            IrisRandomizer.RandomizeAttributes(kept, RandomizeLocks.Rays, new Random(3));
            Assert.Equal(0.6, kept.RimRayIntensity);
            Assert.Equal(99, kept.RimRayCount);
            Assert.Equal("#010101", kept.RimRayColor);
        }

        [Fact]
        public void Range_MasksTheLines_WithoutMovingThem()
        {
            var p = WithRimRays();
            Assert.True(PatternRange.IsFull(p, PatternLayer.RimRays));
            string full = SvgExporter.GeneratePairSvg(p);
            Assert.DoesNotContain("_rim_rays_range", full);

            PatternRange.Apply(p, PatternLayer.RimRays, PatternRange.Styles[3]); // 上半分
            Assert.Equal("上半分", PatternRange.Match(p, PatternLayer.RimRays)?.Name);
            string svg = SvgExporter.GeneratePairSvg(p);
            Assert.Contains($"<g id=\"left_eye_rim_rays\" fill=\"{p.RimRayColor}\" mask=\"url(#left_eye_rim_rays_range)\">", svg);
            Assert.Contains("<mask id=\"right_eye_rim_rays_range\"", svg);

            static string Lines(string s)
            {
                int g = s.IndexOf("<g id=\"left_eye_rim_rays\"", StringComparison.Ordinal);
                g = s.IndexOf('>', g);
                return s[g..s.IndexOf("</g>", g, StringComparison.Ordinal)];
            }
            Assert.Equal(Lines(full), Lines(svg));
            System.Xml.Linq.XDocument.Parse(svg);
        }

        [Fact]
        public void Range_IsSaved_AndCarriedByPresets()
        {
            var q = WithRimRays();
            PatternRange.Set(q, PatternLayer.RimRays, 270, 200, 50);
            var restored = JsonSerializer.Deserialize<IrisParameters>(JsonSerializer.Serialize(q))!;
            Assert.Equal((270.0, 200.0, 50.0), PatternRange.Of(restored, PatternLayer.RimRays));
            Assert.True(PatternRange.IsFull(JsonSerializer.Deserialize<IrisParameters>("{\"RimRayIntensity\": 0.5}")!, PatternLayer.RimRays));

            var target = new IrisParameters();
            ColorPresets.ApplyPreset(target, ColorPresets.FromDesign(q, "test", ""));
            Assert.Equal((270.0, 200.0, 50.0), PatternRange.Of(target, PatternLayer.RimRays));
            ColorPresets.ApplyPreset(target, ColorPresets.All[0]);
            Assert.True(PatternRange.IsFull(target, PatternLayer.RimRays));
        }

        [Fact]
        public void Randomizer_MostlyShowsTheWholeRim_AndTheRaysLockKeepsTheRange()
        {
            int upper = 0, lower = 0;
            for (int i = 0; i < 400; i++)
            {
                var p = new IrisParameters();
                IrisRandomizer.RandomizeAttributes(p, new Random(i));
                var name = PatternRange.Match(p, PatternLayer.RimRays)?.Name;
                Assert.Contains(name, new[] { "全体", "上半分", "下半分" });
                if (name == "上半分") upper++;
                if (name == "下半分") lower++;
            }
            Assert.InRange(upper, 50, 120); // about 20%
            Assert.InRange(lower, 20, 70);  // about 10%

            var kept = WithRimRays();
            PatternRange.Set(kept, PatternLayer.RimRays, 10, 20, 5);
            IrisRandomizer.RandomizeAttributes(kept, RandomizeLocks.Rays, new Random(3));
            Assert.Equal((10.0, 20.0, 5.0), PatternRange.Of(kept, PatternLayer.RimRays));
        }
    }
}
