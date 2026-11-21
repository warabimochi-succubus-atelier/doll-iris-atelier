using System;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using Xunit;

namespace DollIrisGenerator.Tests
{
    /// <summary>斑点 (SPECIFICATION.md 11.1): dark freckles / crypts scattered over the iris.</summary>
    public class SpotTests
    {
        private static IrisParameters Spotted() => new() { SpotIntensity = 0.5 };

        [Fact]
        public void OffByDefault_AndExportsAsBefore()
        {
            var p = new IrisParameters();
            Assert.Equal(0.0, p.SpotIntensity);
            Assert.DoesNotContain("_spots", SvgExporter.GeneratePairSvg(p));
        }

        [Fact]
        public void Spots_SitBetweenThePupilAndTheSpread_PointingOutward()
        {
            var spots = IrisPatterns.Spots(200, 0.3, 0.8, 42, false);
            Assert.Equal(200, spots.Count);
            foreach (var s in spots)
            {
                double r = Math.Sqrt(s.Position.X * s.Position.X + s.Position.Y * s.Position.Y);
                Assert.InRange(r, 0.33 - 1e-9, 0.8 + 1e-9);
                Assert.InRange(s.SizeFactor, 0.4, 1.0);
                Assert.InRange(s.AlphaFactor, 0.6, 1.0);
                // The long axis is within 15° of the radius
                double off = Math.Abs(Math.IEEERemainder(s.Angle - Math.Atan2(s.Position.Y, s.Position.X), 2.0 * Math.PI));
                Assert.True(off <= Math.PI / 12.0 + 1e-9, off.ToString());
            }
        }

        [Fact]
        public void MoreSpots_KeepTheFirstOnesInPlace()
        {
            var few = IrisPatterns.Spots(10, 0.3, 0.8, 5, false);
            var many = IrisPatterns.Spots(40, 0.3, 0.8, 5, false);
            Assert.Equal(few, many.Take(10));
        }

        [Fact]
        public void RightEye_IsTheMirrorImage()
        {
            var left = IrisPatterns.Spots(30, 0.3, 0.8, 9, false);
            var right = IrisPatterns.Spots(30, 0.3, 0.8, 9, true);
            for (int i = 0; i < left.Count; i++)
            {
                Assert.Equal(-left[i].Position.X, right[i].Position.X, 9);
                Assert.Equal(left[i].Position.Y, right[i].Position.Y, 9);
                Assert.Equal(-Math.Cos(left[i].Angle), Math.Cos(right[i].Angle), 9);
                Assert.Equal(Math.Sin(left[i].Angle), Math.Sin(right[i].Angle), 9);
            }
        }

        [Fact]
        public void Svg_DrawsTheSpots_UnderTheRingAndTheRays()
        {
            var p = Spotted();
            p.SpotCount = 12;
            p.CollaretteIntensity = 0.5;
            string svg = SvgExporter.GeneratePairSvg(p);
            Assert.Contains("<radialGradient id=\"left_eye_spots_grad\">", svg);
            Assert.Contains("<g id=\"left_eye_spots\" fill=\"url(#left_eye_spots_grad)\">", svg);
            int group = svg.IndexOf("<g id=\"left_eye_spots\"", StringComparison.Ordinal);
            int end = svg.IndexOf("</g>", group, StringComparison.Ordinal);
            Assert.Equal(12, Regex.Matches(svg[group..end], "<ellipse ").Count);
            Assert.True(group < svg.IndexOf("left_eye_collarette", StringComparison.Ordinal));
            Assert.True(group < svg.IndexOf("left_eye_upper_shadow", StringComparison.Ordinal));
            Assert.True(group < svg.IndexOf("left_eye_rays", StringComparison.Ordinal));
            System.Xml.Linq.XDocument.Parse(svg);

            // Crisp: the color itself, and the blend mode on the group
            p.SpotSoftness = 0;
            p.SpotBlend = LayerBlend.SoftLight;
            p.SpotColor = "#80112233";
            svg = SvgExporter.GeneratePairSvg(p);
            Assert.DoesNotContain("_spots_grad", svg);
            Assert.Contains("<g id=\"left_eye_spots\" fill=\"#112233\" style=\"mix-blend-mode:soft-light\">", svg);
            // The color's own alpha (0x80) multiplies each spot's opacity: at most 0.5 × 0.502
            group = svg.IndexOf("<g id=\"left_eye_spots\"", StringComparison.Ordinal);
            end = svg.IndexOf("</g>", group, StringComparison.Ordinal);
            foreach (Match m in Regex.Matches(svg[group..end], "fill-opacity=\"([0-9.]+)\""))
                Assert.True(double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) <= 0.252);
        }

        [Fact]
        public void Settings_AreClamped_AndSaved()
        {
            var p = new IrisParameters
            {
                SpotIntensity = 2, SpotCount = 500, SpotSize = 0, SpotElongation = double.NaN, SpotSoftness = -1,
                SpotRadiusRatio = 2, SpotBlend = (LayerBlend)42, SpotColor = null!
            };
            Assert.Equal(1.0, p.SpotIntensity);
            Assert.Equal(IrisParameters.MaxSpotCount, p.SpotCount);
            Assert.Equal(IrisParameters.MinSpotSize, p.SpotSize);
            Assert.Equal(IrisParameters.DefaultSpotElongation, p.SpotElongation);
            Assert.Equal(0.0, p.SpotSoftness);
            Assert.Equal(IrisParameters.MaxSpotRadiusRatio, p.SpotRadiusRatio);
            Assert.Equal(LayerBlend.Normal, p.SpotBlend);
            Assert.Equal(IrisParameters.DefaultSpotColor, p.SpotColor);

            var q = new IrisParameters { SpotIntensity = 0.4, SpotCount = 60, SpotColor = "#102030", SpotBlend = LayerBlend.Overlay };
            var restored = JsonSerializer.Deserialize<IrisParameters>(JsonSerializer.Serialize(q))!;
            Assert.Equal(0.4, restored.SpotIntensity);
            Assert.Equal(60, restored.SpotCount);
            Assert.Equal("#102030", restored.SpotColor);
            Assert.Equal(LayerBlend.Overlay, restored.SpotBlend);

            Assert.Equal(0.0, JsonSerializer.Deserialize<IrisParameters>("{\"ParticleCount\": 10}")!.SpotIntensity);
        }

        [Fact]
        public void Presets_CarryTheSpots_AndBuiltInsTurnThemOff()
        {
            var design = Spotted();
            design.SpotCount = 77;
            var preset = ColorPresets.FromDesign(design, "test", "");
            var target = new IrisParameters();
            ColorPresets.ApplyPreset(target, preset);
            Assert.Equal(0.5, target.SpotIntensity);
            Assert.Equal(77, target.SpotCount);

            ColorPresets.ApplyPreset(target, ColorPresets.All[0]);
            Assert.Equal(0.0, target.SpotIntensity);
        }

        [Fact]
        public void Randomizer_SometimesAddsSpots_AndTheLockKeepsThem()
        {
            int on = 0;
            for (int i = 0; i < 300; i++)
            {
                var p = new IrisParameters();
                IrisRandomizer.RandomizeAttributes(p, new Random(i));
                if (p.SpotIntensity > 0) on++;
            }
            Assert.InRange(on, 30, 90); // about 20%

            var kept = Spotted();
            kept.SpotCount = 99;
            kept.SpotColor = "#010101";
            IrisRandomizer.RandomizeAttributes(kept, RandomizeLocks.Spots, new Random(3));
            Assert.Equal(0.5, kept.SpotIntensity);
            Assert.Equal(99, kept.SpotCount);
            Assert.Equal("#010101", kept.SpotColor);
            Assert.Equal("斑点", RandomizeLocksInfo.Describe(RandomizeLocks.Spots));
        }
    }
}
