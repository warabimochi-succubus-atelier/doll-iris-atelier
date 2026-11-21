using System;
using System.Text.Json;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using Xunit;

namespace DollIrisGenerator.Tests
{
    /// <summary>瞳孔の縁取り線 (SPECIFICATION.md 9.2) and 外輪の内側の輪 (10.1).</summary>
    public class OutlineRingTests
    {
        [Fact]
        public void BothOffByDefault_AndExportAsBefore()
        {
            var p = new IrisParameters();
            Assert.Equal(0.0, p.PupilOutlineIntensity);
            Assert.Equal(0.0, p.LimbalInnerRingIntensity);
            string svg = SvgExporter.GeneratePairSvg(p);
            Assert.DoesNotContain("_pupil_outline", svg);
            Assert.DoesNotContain("_limbal_inner", svg);
        }

        // ---- 瞳孔の縁取り線 ----

        [Fact]
        public void PupilOutline_StrokesThePupilShape_AboveThePupil()
        {
            var p = new IrisParameters { PupilOutlineIntensity = 0.8, PupilOutlineColor = "#FFD54F", PupilSizeRatio = 0.3 };
            string svg = SvgExporter.GeneratePairSvg(p);
            double baseR = p.SizeMm / 2.0;
            string width = (baseR * p.PupilOutlineWidth).ToFixed(4);
            // A round pupil: an ellipse with the pupil's radii
            Assert.Contains($"<ellipse cx=", svg);
            Assert.Contains($" id=\"left_eye_pupil_outline\" fill=\"none\" stroke=\"#FFD54F\" stroke-width=\"{width}\" stroke-linejoin=\"round\" opacity=\"0.80\" />", svg);
            Assert.True(svg.IndexOf("id=\"left_eye_pupil\"", StringComparison.Ordinal) < svg.IndexOf("left_eye_pupil_outline", StringComparison.Ordinal));

            // A shaped pupil: the same outline path
            p.PupilShape = PupilShape.Star5;
            p.PupilFacetIntensity = 0.5;
            svg = SvgExporter.GeneratePairSvg(p);
            Assert.Contains("<path d=\"M", svg[svg.LastIndexOf('<', svg.IndexOf("id=\"left_eye_pupil_outline\"", StringComparison.Ordinal))..]);
            Assert.True(svg.IndexOf("left_eye_pupil_facets", StringComparison.Ordinal) < svg.IndexOf("left_eye_pupil_outline", StringComparison.Ordinal));
            System.Xml.Linq.XDocument.Parse(svg);

            // No pupil, no line
            p.PupilOpacity = 0;
            Assert.DoesNotContain("_pupil_outline", SvgExporter.GeneratePairSvg(p));
        }

        // ---- 外輪の内側の輪 ----

        [Fact]
        public void InnerRing_IsABandAtTheSameDistanceInsideTheRim()
        {
            var p = new IrisParameters { LimbalInnerRingIntensity = 0.6, LimbalRingThickness = 0.08, LimbalInnerRingGap = 0.02, LimbalInnerRingWidth = 0.015 };
            double baseR = p.SizeMm / 2.0;
            double inset = baseR * 0.10, width = baseR * 0.015;
            string svg = SvgExporter.GeneratePairSvg(p);
            Assert.Contains("<mask id=\"left_eye_limbal_inner_mask\">", svg);
            Assert.Contains($"stroke=\"#FFFFFF\" stroke-width=\"{(2 * (inset + width)).ToFixed(4)}\"", svg);
            Assert.Contains($"stroke=\"#000000\" stroke-width=\"{(2 * inset).ToFixed(4)}\"", svg);
            Assert.Contains("id=\"left_eye_limbal_inner_ring\"", svg);
            Assert.Contains("fill=\"#FFFFFF\" opacity=\"0.60\" mask=\"url(#left_eye_limbal_inner_mask)\" />", svg);
            // Over the limbal ring, under the pupil
            int ring = svg.IndexOf("id=\"left_eye_limbal_inner_ring\"", StringComparison.Ordinal);
            Assert.True(svg.IndexOf("id=\"left_eye_limbal_ring\"", StringComparison.Ordinal) < ring);
            Assert.True(ring < svg.IndexOf("id=\"left_eye_pupil\"", StringComparison.Ordinal));
            System.Xml.Linq.XDocument.Parse(svg);

            // No limbal ring and no gap: right at the rim, so no black stroke
            p.LimbalRingThickness = 0;
            p.LimbalInnerRingGap = 0;
            Assert.DoesNotContain("stroke=\"#000000\"", SvgExporter.GeneratePairSvg(p));

            // A shaped outline: strokes of the outline itself
            p.OutlineTopFlat = 0.4;
            svg = SvgExporter.GeneratePairSvg(p);
            Assert.Contains("<use href=\"#left_eye_outline\" fill=\"none\" stroke=\"#FFFFFF\"", svg);
            Assert.Contains("<use id=\"left_eye_limbal_inner_ring\" href=\"#left_eye_outline\"", svg);
        }

        // ---- shared ----

        [Fact]
        public void Settings_AreClamped_AndSaved()
        {
            var p = new IrisParameters
            {
                PupilOutlineIntensity = 2, PupilOutlineWidth = 1, PupilOutlineColor = null!,
                LimbalInnerRingIntensity = -1, LimbalInnerRingWidth = 0, LimbalInnerRingGap = double.NaN, LimbalInnerRingColor = null!
            };
            Assert.Equal(1.0, p.PupilOutlineIntensity);
            Assert.Equal(IrisParameters.MaxPupilOutlineWidth, p.PupilOutlineWidth);
            Assert.Equal(IrisParameters.DefaultPupilOutlineColor, p.PupilOutlineColor);
            Assert.Equal(0.0, p.LimbalInnerRingIntensity);
            Assert.Equal(IrisParameters.MinLimbalInnerRingWidth, p.LimbalInnerRingWidth);
            Assert.Equal(IrisParameters.DefaultLimbalInnerRingGap, p.LimbalInnerRingGap);
            Assert.Equal(IrisParameters.DefaultLimbalInnerRingColor, p.LimbalInnerRingColor);

            var q = new IrisParameters { PupilOutlineIntensity = 0.7, PupilOutlineColor = "#123456", LimbalInnerRingIntensity = 0.4, LimbalInnerRingGap = 0.05 };
            var restored = JsonSerializer.Deserialize<IrisParameters>(JsonSerializer.Serialize(q))!;
            Assert.Equal(0.7, restored.PupilOutlineIntensity);
            Assert.Equal("#123456", restored.PupilOutlineColor);
            Assert.Equal(0.4, restored.LimbalInnerRingIntensity);
            Assert.Equal(0.05, restored.LimbalInnerRingGap);

            var old = JsonSerializer.Deserialize<IrisParameters>("{\"LimbalRingSoftness\": 0.3}")!;
            Assert.Equal(0.0, old.PupilOutlineIntensity);
            Assert.Equal(0.0, old.LimbalInnerRingIntensity);
        }

        [Fact]
        public void Presets_CarryBoth_AndBuiltInsTurnThemOff()
        {
            var design = new IrisParameters { PupilOutlineIntensity = 0.7, LimbalInnerRingIntensity = 0.4, LimbalInnerRingColor = "#ABCDEF" };
            var target = new IrisParameters();
            ColorPresets.ApplyPreset(target, ColorPresets.FromDesign(design, "test", ""));
            Assert.Equal(0.7, target.PupilOutlineIntensity);
            Assert.Equal(0.4, target.LimbalInnerRingIntensity);
            Assert.Equal("#ABCDEF", target.LimbalInnerRingColor);

            ColorPresets.ApplyPreset(target, ColorPresets.All[0]);
            Assert.Equal(0.0, target.PupilOutlineIntensity);
            Assert.Equal(0.0, target.LimbalInnerRingIntensity);
        }

        [Fact]
        public void Randomizer_SometimesAddsThem_AndTheLocksKeepThem()
        {
            int outline = 0, inner = 0;
            for (int i = 0; i < 300; i++)
            {
                var p = new IrisParameters();
                IrisRandomizer.RandomizeAttributes(p, new Random(i));
                if (p.PupilOutlineIntensity > 0) outline++;
                if (p.LimbalInnerRingIntensity > 0) inner++;
            }
            Assert.InRange(outline, 20, 75); // about 15%
            Assert.InRange(inner, 30, 90);   // about 20%

            var kept = new IrisParameters { PupilOutlineIntensity = 0.7, LimbalInnerRingIntensity = 0.4 };
            IrisRandomizer.RandomizeAttributes(kept, RandomizeLocks.Pupil | RandomizeLocks.LimbalRing, new Random(3));
            Assert.Equal(0.7, kept.PupilOutlineIntensity);
            Assert.Equal(0.4, kept.LimbalInnerRingIntensity);
        }
    }
}
