using System;
using System.Linq;
using System.Text.Json;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using Xunit;

namespace DollIrisGenerator.Tests
{
    /// <summary>見える範囲 (SPECIFICATION.md 8.1): rays, rim rays, glitter and gem facets shown only inside a fan.</summary>
    public class PatternRangeTests
    {
        private static IrisParameters Busy() => new()
        {
            ParticleCount = 80, RayCount = 48, RayIntensity = 0.7
        };

        [Fact]
        public void Defaults_AreFull_AndExportAsBefore()
        {
            var p = Busy();
            foreach (PatternLayer layer in Enum.GetValues<PatternLayer>())
                Assert.True(PatternRange.IsFull(p, layer));
            string svg = SvgExporter.GeneratePairSvg(p);
            Assert.DoesNotContain("_range", svg);
            Assert.DoesNotContain("_ranged", svg);
        }

        [Theory]
        [InlineData(PatternLayer.Rays, "rays")]
        [InlineData(PatternLayer.Glitter, "glitter")]
        public void ARange_MasksOnlyThatLayer(PatternLayer layer, string name)
        {
            var p = Busy();
            PatternRange.Set(p, layer, 90, 180, 40);
            string svg = SvgExporter.GeneratePairSvg(p);
            foreach (string eye in new[] { "left_eye", "right_eye" })
            {
                Assert.Contains($"<mask id=\"{eye}_{name}_range\" maskUnits=\"userSpaceOnUse\"", svg);
                Assert.Contains($"mask=\"url(#{eye}_{name}_range)\"", svg);
            }
            foreach (string other in new[] { "rays", "glitter", "lens" }.Where(o => o != name))
                Assert.DoesNotContain($"_{other}_range", svg);
            System.Xml.Linq.XDocument.Parse(svg);
        }

        [Fact]
        public void Range_DoesNotMoveThePatterns()
        {
            var full = Busy();
            var ranged = Busy();
            PatternRange.Set(ranged, PatternLayer.Rays, 90, 120, 30);
            Assert.Equal(IrisPatterns.MainRays(full, 5, 42, false), IrisPatterns.MainRays(ranged, 5, 42, false));
            string a = SvgExporter.GeneratePairSvg(full), b = SvgExporter.GeneratePairSvg(ranged);
            // The same ray lines are drawn; only the mask is added around them
            var lines = (string s) => s.Split('\n').Where(l => l.Contains("<path d=\"M") && !l.Contains("fill=\"#FFFFFF\"")).ToList();
            Assert.Equal(lines(a), lines(b));
        }

        [Fact]
        public void GlitterBlend_PutsTheMaskOnTheBlendedGroup()
        {
            var p = Busy();
            p.GlitterBlend = LayerBlend.Screen;
            PatternRange.Set(p, PatternLayer.Glitter, 90, 180, 40);
            string svg = SvgExporter.GeneratePairSvg(p);
            Assert.Contains("<g id=\"left_eye_glitter_layer\" style=\"mix-blend-mode:screen\" mask=\"url(#left_eye_glitter_range)\">", svg);
            Assert.DoesNotContain("left_eye_glitter_ranged", svg);
        }

        [Theory]
        [InlineData(180, 40)]
        [InlineData(120, 90)]
        [InlineData(330, 60)]
        [InlineData(40, 80)]
        [InlineData(200, 0)]
        public void Sectors_StackUpToTheWeight(double width, double feather)
        {
            var sectors = PatternRange.Sectors(width, feather);
            Assert.True(sectors.Zip(sectors.Skip(1)).All(z => z.First.HalfDeg >= z.Second.HalfDeg), "widest first");
            for (double d = 0.25; d < 180; d += 0.5)
            {
                double clear = 1.0;
                foreach (var (half, opacity) in sectors)
                    if (d < half) clear *= 1.0 - opacity;
                double expected = PatternRange.Weight(d, 0, width, feather);
                Assert.True(Math.Abs((1.0 - clear) - expected) < 0.06, $"d = {d}: {1.0 - clear} vs {expected}");
            }
        }

        [Fact]
        public void Weight_IsOneInsideZeroOutside_AndWrapsAround()
        {
            Assert.Equal(1.0, PatternRange.Weight(90, 90, 180, 40));
            Assert.Equal(0.0, PatternRange.Weight(270, 90, 180, 40));
            Assert.Equal(0.5, PatternRange.Weight(180, 90, 180, 40), 6);
            Assert.Equal(1.0, PatternRange.Weight(350, 10, 60, 0));
            Assert.Equal(0.0, PatternRange.Weight(330, 10, 60, 0));
            Assert.Equal(1.0, PatternRange.Weight(123, 0, 360, 30));
        }

        [Fact]
        public void RightEye_IsMirrored()
        {
            Assert.Equal(90.0, PatternRange.CenterFor(90, true));
            Assert.Equal(180.0, PatternRange.CenterFor(0, true));
            Assert.Equal(135.0, PatternRange.CenterFor(45, true));
            Assert.Equal(45.0, PatternRange.CenterFor(45, false));
        }

        [Fact]
        public void Settings_AreClamped_Normalized_AndSaved()
        {
            var p = new IrisParameters { RayRangeAngle = -90, RayRangeWidth = 999, RayRangeFeather = -5, ParticleRangeAngle = double.NaN, RimRayRangeFeather = 200 };
            Assert.Equal(270.0, p.RayRangeAngle);
            Assert.Equal(360.0, p.RayRangeWidth);
            Assert.Equal(0.0, p.RayRangeFeather);
            Assert.Equal(IrisParameters.DefaultRangeAngle, p.ParticleRangeAngle);
            Assert.Equal(IrisParameters.MaxRangeFeather, p.RimRayRangeFeather);

            var q = Busy();
            PatternRange.Set(q, PatternLayer.Glitter, 45, 150, 20);
            var restored = JsonSerializer.Deserialize<IrisParameters>(JsonSerializer.Serialize(q))!;
            Assert.Equal((45.0, 150.0, 20.0), PatternRange.Of(restored, PatternLayer.Glitter));

            var old = JsonSerializer.Deserialize<IrisParameters>("{\"RayCount\": 40}")!;
            Assert.All(Enum.GetValues<PatternLayer>(), l => Assert.True(PatternRange.IsFull(old, l)));
        }

        [Fact]
        public void QuickStyles_ApplyAndMatch()
        {
            var p = new IrisParameters();
            foreach (PatternLayer layer in Enum.GetValues<PatternLayer>())
            {
                Assert.Equal("全体", PatternRange.Match(p, layer)?.Name);
                foreach (var s in PatternRange.Styles)
                {
                    PatternRange.Apply(p, layer, s);
                    Assert.Equal(s.Name, PatternRange.Match(p, layer)?.Name);
                }
                PatternRange.Set(p, layer, 33, 100, 10);
                Assert.Null(PatternRange.Match(p, layer));
            }
        }

        [Fact]
        public void Presets_CarryTheRanges()
        {
            var design = Busy();
            PatternRange.Set(design, PatternLayer.Rays, 90, 180, 40);
            var preset = ColorPresets.FromDesign(design, "test", "");
            var target = new IrisParameters();
            ColorPresets.ApplyPreset(target, preset);
            Assert.Equal((90.0, 180.0, 40.0), PatternRange.Of(target, PatternLayer.Rays));

            // A preset without ranges resets them to the defaults
            PatternRange.Set(target, PatternLayer.Glitter, 10, 50, 5);
            ColorPresets.ApplyPreset(target, ColorPresets.All[0]);
            Assert.True(PatternRange.IsFull(target, PatternLayer.Glitter));
        }
    }
}
