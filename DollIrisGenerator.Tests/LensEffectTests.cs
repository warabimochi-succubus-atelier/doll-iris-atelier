using System;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using Xunit;

namespace DollIrisGenerator.Tests
{
    /// <summary>集光 (SPECIFICATION.md 12.2) and 縁の屈折 (12.3): the dome's light on the side opposite the light.</summary>
    public class LensEffectTests
    {
        private static double Num(Group g) => double.Parse(g.Value, CultureInfo.InvariantCulture);

        /// <summary>The caustic's center relative to the eye's base ellipse center.</summary>
        private static (double Dx, double Dy) CausticShift(string svg, string eye)
        {
            var c = Regex.Match(svg, $"<ellipse id=\"{eye}_base\" cx=\"([0-9.]+)\" cy=\"([0-9.]+)\"");
            var g = Regex.Match(svg, $"<ellipse id=\"{eye}_caustic\" cx=\"([0-9.\\-]+)\" cy=\"([0-9.\\-]+)\"");
            Assert.True(c.Success && g.Success);
            return (Num(g.Groups[1]) - Num(c.Groups[1]), Num(g.Groups[2]) - Num(c.Groups[2]));
        }

        [Fact]
        public void OffByDefault_AndExportsAsBefore()
        {
            var p = new IrisParameters();
            Assert.Equal(0.0, p.CausticIntensity);
            Assert.Equal(0.0, p.DomeEdgeIntensity);
            string svg = SvgExporter.GeneratePairSvg(p);
            Assert.DoesNotContain("_caustic", svg);
            Assert.DoesNotContain("_dome_edge", svg);
        }

        [Fact]
        public void Caustic_SitsOppositeTheLight_MirroredForTheRightEye()
        {
            // Default light from above: the caustic straight below the center, offset × the radius
            var p = new IrisParameters { CausticIntensity = 0.6, CausticOffset = 0.5 };
            string svg = SvgExporter.GeneratePairSvg(p);
            var (dx, dy) = CausticShift(svg, "left_eye");
            Assert.Equal(0.0, dx, 2);
            Assert.Equal(0.5 * p.SizeMm / 2.0, dy, 2);

            // Light from the upper left (225°): the caustic lower right; the right eye's lower left
            p.LightAngle = 225;
            svg = SvgExporter.GeneratePairSvg(p);
            var left = CausticShift(svg, "left_eye");
            var right = CausticShift(svg, "right_eye");
            Assert.True(left.Dx > 0 && left.Dy > 0);
            Assert.Equal(-left.Dx, right.Dx, 2);
            Assert.Equal(left.Dy, right.Dy, 2);

            // 反転しない: both eyes the same
            p.LightRightEyeFlip = RightEyeFlip.None;
            svg = SvgExporter.GeneratePairSvg(p);
            Assert.Equal(CausticShift(svg, "left_eye").Dx, CausticShift(svg, "right_eye").Dx, 2);
            System.Xml.Linq.XDocument.Parse(svg);
        }

        [Fact]
        public void Caustic_KeepsItsScreenDirection_OnATiltedIris()
        {
            var p = new IrisParameters { CausticIntensity = 0.6, CausticOffset = 0.5, TiltAngle = 30 };
            var (dx, dy) = CausticShift(SvgExporter.GeneratePairSvg(p), "left_eye");
            // Inside the group turned by 30°: turned with it, the caustic is straight down on screen
            double a = 30 * Math.PI / 180;
            Assert.Equal(0.0, dx * Math.Cos(a) - dy * Math.Sin(a), 2);
            Assert.True(dx * Math.Sin(a) + dy * Math.Cos(a) > 0);
        }

        [Fact]
        public void Caustic_CrescentIsMasked_RoundIsNot_AndTheBlendIsWritten()
        {
            var p = new IrisParameters { CausticIntensity = 0.6 };
            string svg = SvgExporter.GeneratePairSvg(p);
            string element = Regex.Match(svg, "<ellipse id=\"left_eye_caustic\"[^>]*>").Value;
            Assert.Contains("mask=\"url(#left_eye_caustic_mask)\"", element);
            Assert.Contains("mix-blend-mode:screen", element);
            // Above the patterns, under the limbal ring and the pupil
            int caustic = svg.IndexOf("id=\"left_eye_caustic\"", StringComparison.Ordinal);
            Assert.True(svg.IndexOf("id=\"left_eye_rays\"", StringComparison.Ordinal) < caustic);
            Assert.True(caustic < svg.IndexOf("id=\"left_eye_limbal_ring\"", StringComparison.Ordinal));

            p.CausticShape = CausticShape.Round;
            svg = SvgExporter.GeneratePairSvg(p);
            Assert.DoesNotContain("_caustic_mask", svg);
            System.Xml.Linq.XDocument.Parse(svg);
        }

        [Fact]
        public void DomeEdge_IsABandInsideTheLimbalRing_WeightedAwayFromTheLight()
        {
            var p = new IrisParameters { DomeEdgeIntensity = 0.5 };
            string svg = SvgExporter.GeneratePairSvg(p);
            Assert.Contains("mask=\"url(#left_eye_dome_edge_mask)\"", svg);
            Assert.Contains("<g id=\"left_eye_dome_edge_sided\" mask=\"url(#left_eye_dome_edge_side)\">", svg);
            int edge = svg.IndexOf("id=\"left_eye_dome_edge\"", StringComparison.Ordinal);
            Assert.True(svg.IndexOf("id=\"left_eye_limbal_ring\"", StringComparison.Ordinal) < edge);
            Assert.True(edge < svg.IndexOf("id=\"left_eye_pupil\"", StringComparison.Ordinal));
            System.Xml.Linq.XDocument.Parse(svg);

            p.DomeEdgeBias = 0;
            svg = SvgExporter.GeneratePairSvg(p);
            Assert.DoesNotContain("_dome_edge_side", svg);
            Assert.Contains("left_eye_dome_edge_mask", svg);

            // A shaped outline: the band follows it
            p.OutlineSquareness = 0.5;
            Assert.Contains("<use href=\"#left_eye_outline\" fill=\"none\" stroke=\"#FFFFFF\"", SvgExporter.GeneratePairSvg(p));
        }

        [Fact]
        public void Settings_AreClamped_SavedAndCarriedByPresets()
        {
            var p = new IrisParameters
            {
                CausticIntensity = 2, CausticOffset = 5, CausticSize = 0, CausticShape = (CausticShape)9, CausticBlend = (LayerBlend)9,
                CausticColor = null!, DomeEdgeIntensity = double.NaN, DomeEdgeWidth = 1, DomeEdgeBias = -1, DomeEdgeColor = null!
            };
            Assert.Equal(1.0, p.CausticIntensity);
            Assert.Equal(IrisParameters.MaxCausticOffset, p.CausticOffset);
            Assert.Equal(IrisParameters.MinCausticSize, p.CausticSize);
            Assert.Equal(CausticShape.Crescent, p.CausticShape);
            Assert.Equal(LayerBlend.Screen, p.CausticBlend);
            Assert.Equal(IrisParameters.DefaultCausticColor, p.CausticColor);
            Assert.Equal(0.0, p.DomeEdgeIntensity);
            Assert.Equal(IrisParameters.MaxDomeEdgeWidth, p.DomeEdgeWidth);
            Assert.Equal(0.0, p.DomeEdgeBias);
            Assert.Equal(IrisParameters.DefaultDomeEdgeColor, p.DomeEdgeColor);

            var q = new IrisParameters { CausticIntensity = 0.4, CausticShape = CausticShape.Round, CausticColor = "#FFEEAA", DomeEdgeIntensity = 0.3, DomeEdgeBias = 0.2 };
            var restored = JsonSerializer.Deserialize<IrisParameters>(JsonSerializer.Serialize(q))!;
            Assert.Equal(0.4, restored.CausticIntensity);
            Assert.Equal(CausticShape.Round, restored.CausticShape);
            Assert.Equal("#FFEEAA", restored.CausticColor);
            Assert.Equal(0.2, restored.DomeEdgeBias);
            var old = JsonSerializer.Deserialize<IrisParameters>("{\"LensRefractionGlow\": 0.5}")!;
            Assert.Equal(0.0, old.CausticIntensity);
            Assert.Equal(0.0, old.DomeEdgeIntensity);

            var target = new IrisParameters();
            ColorPresets.ApplyPreset(target, ColorPresets.FromDesign(q, "test", ""));
            Assert.Equal(0.4, target.CausticIntensity);
            Assert.Equal(0.3, target.DomeEdgeIntensity);
            ColorPresets.ApplyPreset(target, ColorPresets.All[0]);
            Assert.Equal(0.0, target.CausticIntensity);
            Assert.Equal(0.0, target.DomeEdgeIntensity);
        }

        [Fact]
        public void Randomizer_SometimesAddsThem_AndTheLensLockKeepsThem()
        {
            int caustic = 0, edge = 0;
            for (int i = 0; i < 400; i++)
            {
                var p = new IrisParameters();
                IrisRandomizer.RandomizeAttributes(p, new Random(i));
                if (p.CausticIntensity > 0) { caustic++; Assert.InRange(p.CausticIntensity, 0.35, 0.70); }
                if (p.DomeEdgeIntensity > 0) { edge++; Assert.InRange(p.DomeEdgeIntensity, 0.30, 0.60); }
            }
            Assert.InRange(caustic, 60, 140); // about 25%
            Assert.InRange(edge, 45, 120);    // about 20%

            var kept = new IrisParameters { CausticIntensity = 0.9, CausticColor = "#010101", DomeEdgeIntensity = 0.8, DomeEdgeWidth = 0.25 };
            IrisRandomizer.RandomizeAttributes(kept, RandomizeLocks.Lens, new Random(3));
            Assert.Equal(0.9, kept.CausticIntensity);
            Assert.Equal("#010101", kept.CausticColor);
            Assert.Equal(0.8, kept.DomeEdgeIntensity);
            Assert.Equal(0.25, kept.DomeEdgeWidth);
        }
    }
}
