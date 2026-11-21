using System;
using System.Linq;
using System.Text.Json;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using Xunit;

namespace DollIrisGenerator.Tests
{
    /// <summary>色の段 (SPECIFICATION.md 7.1.2): concentric bands of color from the pupil's edge to the rim.</summary>
    public class ToneBandTests
    {
        private static IrisParameters Banded() => new() { ToneBandIntensity = 0.6 };

        [Fact]
        public void OffByDefault_AndExportsAsBefore()
        {
            var p = new IrisParameters();
            Assert.Equal(0.0, p.ToneBandIntensity);
            Assert.DoesNotContain("_tone_bands", SvgExporter.GeneratePairSvg(p));
        }

        [Fact]
        public void CrispBands_StepAtEvenlySpacedBoundaries()
        {
            var p = new IrisParameters
            {
                ToneBandCount = 3, ToneBandSoftness = 0, PupilSizeRatio = 0.4,
                ToneBandInnerColor = "#FF0000", ToneBandOuterColor = "#0000FF"
            };
            var stops = SvgExporter.ToneBandStops(p);
            // start, two stops at each of the 2 boundaries, end
            Assert.Equal(6, stops.Count);
            Assert.Equal(new[] { 0.0, 0.6, 0.6, 0.8, 0.8, 1.0 }, stops.Select(s => Math.Round(s.Offset, 9)));
            Assert.Equal(new[] { "#FF0000", "#FF0000", "#800080", "#800080", "#0000FF", "#0000FF" }, stops.Select(s => s.Rgb));
            Assert.All(stops, s => Assert.Equal(1.0, s.Alpha));
        }

        [Fact]
        public void Softness_SpreadsEachBoundary_WithoutCrossingTheNext()
        {
            var p = new IrisParameters { ToneBandCount = 5, ToneBandSoftness = 1, PupilSizeRatio = 0.3 };
            var offsets = SvgExporter.ToneBandStops(p).Select(s => s.Offset).ToList();
            for (int k = 1; k < offsets.Count; k++) Assert.True(offsets[k] >= offsets[k - 1] - 1e-12);
            // Full softness: each boundary spreads over a whole band (half on each side)
            Assert.Equal(0.3 + 0.14 - 0.07, offsets[1], 9);
            Assert.Equal(0.3 + 0.14 + 0.07, offsets[2], 9);
        }

        [Fact]
        public void Svg_DrawsTheBands_OverTheBase_UnderTheNebulaAndShadow()
        {
            var p = Banded();
            string svg = SvgExporter.GeneratePairSvg(p);
            Assert.Contains("<radialGradient id=\"left_eye_tone_bands_grad\" gradientUnits=\"userSpaceOnUse\"", svg);
            Assert.Contains("id=\"left_eye_tone_bands\" cx=", svg);
            Assert.Contains("fill=\"url(#left_eye_tone_bands_grad)\" opacity=\"0.60\" style=\"mix-blend-mode:soft-light\" />", svg);
            int bands = svg.IndexOf("id=\"left_eye_tone_bands\"", StringComparison.Ordinal);
            Assert.True(svg.IndexOf("id=\"left_eye_base\"", StringComparison.Ordinal) < bands);
            Assert.True(bands < svg.IndexOf("id=\"left_eye_upper_shadow\"", StringComparison.Ordinal));
            System.Xml.Linq.XDocument.Parse(svg);

            // A shaped outline is filled through the outline
            p.OutlineSquareness = 0.5;
            svg = SvgExporter.GeneratePairSvg(p);
            Assert.Contains("<use id=\"left_eye_tone_bands\" href=\"#left_eye_outline\"", svg);
        }

        [Fact]
        public void Settings_AreClamped_AndSaved()
        {
            var p = new IrisParameters
            {
                ToneBandIntensity = 3, ToneBandCount = 9, ToneBandSoftness = double.NaN, ToneBandBlend = (LayerBlend)42,
                ToneBandInnerColor = null!
            };
            Assert.Equal(1.0, p.ToneBandIntensity);
            Assert.Equal(IrisParameters.MaxToneBandCount, p.ToneBandCount);
            Assert.Equal(IrisParameters.DefaultToneBandSoftness, p.ToneBandSoftness);
            Assert.Equal(LayerBlend.Normal, p.ToneBandBlend);
            Assert.Equal(IrisParameters.DefaultToneBandInnerColor, p.ToneBandInnerColor);

            var q = new IrisParameters { ToneBandIntensity = 0.5, ToneBandCount = 4, ToneBandOuterColor = "#123456", ToneBandBlend = LayerBlend.Normal };
            var restored = JsonSerializer.Deserialize<IrisParameters>(JsonSerializer.Serialize(q))!;
            Assert.Equal(0.5, restored.ToneBandIntensity);
            Assert.Equal(4, restored.ToneBandCount);
            Assert.Equal("#123456", restored.ToneBandOuterColor);
            Assert.Equal(LayerBlend.Normal, restored.ToneBandBlend);

            var old = JsonSerializer.Deserialize<IrisParameters>("{\"FiberNoise\": 0.3}")!;
            Assert.Equal(0.0, old.ToneBandIntensity);
            Assert.Equal(LayerBlend.SoftLight, old.ToneBandBlend);
        }

        [Fact]
        public void UnreadableColors_FallBackToTheDefaults()
        {
            var p = new IrisParameters { ToneBandCount = 2, ToneBandInnerColor = "blue", ToneBandOuterColor = "linear-gradient(90deg, #00FF00 0%, #FF0000 100%)" };
            var stops = SvgExporter.ToneBandStops(p);
            Assert.Equal("#FFFFFF", stops[0].Rgb);
            Assert.Equal("#00FF00", stops[^1].Rgb); // a gradient gives its first color
        }

        [Fact]
        public void Presets_CarryTheBands_AndBuiltInsTurnThemOff()
        {
            var design = Banded();
            design.ToneBandCount = 4;
            design.ToneBandInnerColor = "#ABCDEF";
            var preset = ColorPresets.FromDesign(design, "test", "");
            var target = new IrisParameters();
            ColorPresets.ApplyPreset(target, preset);
            Assert.Equal(0.6, target.ToneBandIntensity);
            Assert.Equal(4, target.ToneBandCount);
            Assert.Equal("#ABCDEF", target.ToneBandInnerColor);

            ColorPresets.ApplyPreset(target, ColorPresets.All[0]);
            Assert.Equal(0.0, target.ToneBandIntensity);
        }

        [Fact]
        public void Randomizer_SometimesAddsBands_TheOddEyeKeepsItsColors_AndTheColorLockKeepsThem()
        {
            int on = 0;
            for (int i = 0; i < 300; i++)
            {
                var p = new IrisParameters();
                IrisRandomizer.RandomizeAttributes(p, new Random(i));
                if (p.ToneBandIntensity > 0) on++;
                if (p.IsOddEye) Assert.Equal(LayerBlend.SoftLight, p.ToneBandBlend);
            }
            Assert.InRange(on, 30, 90); // about 20%

            var kept = Banded();
            kept.ToneBandCount = 5;
            IrisRandomizer.RandomizeAttributes(kept, RandomizeLocks.Colors, new Random(3));
            Assert.Equal(0.6, kept.ToneBandIntensity);
            Assert.Equal(5, kept.ToneBandCount);
        }
    }
}
