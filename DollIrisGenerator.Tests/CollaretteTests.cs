using System;
using System.Linq;
using System.Text.Json;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using Xunit;

namespace DollIrisGenerator.Tests
{
    /// <summary>瞳孔まわりの輪 (SPECIFICATION.md 9.1): a jagged ring of color round the pupil.</summary>
    public class CollaretteTests
    {
        private static IrisParameters Ringed() => new() { CollaretteIntensity = 0.6 };

        [Fact]
        public void OffByDefault_AndExportsAsBefore()
        {
            var p = new IrisParameters();
            Assert.Equal(0.0, p.CollaretteIntensity);
            Assert.DoesNotContain("_collarette", SvgExporter.GeneratePairSvg(p));
        }

        [Theory]
        [InlineData(8)]
        [InlineData(20)]
        [InlineData(48)]
        public void Edge_GoesRoundOnce_BetweenTheValleysAndThePeaks(int spikes)
        {
            var edge = IrisPatterns.CollaretteEdge(0.3, 0.2, 1.0, spikes, 11, false);
            Assert.True(edge.Length >= IrisPatterns.MinCollarettePoints);
            Assert.Equal(0, edge.Length % (2 * spikes));
            for (int k = 1; k < edge.Length; k++) Assert.True(edge[k].Angle > edge[k - 1].Angle);
            Assert.True(edge[^1].Angle - edge[0].Angle < 2.0 * Math.PI);
            // Peaks reach 80–100% of the width, valleys 20–55%
            Assert.All(edge, e => Assert.InRange(e.Radius, 0.3 + 0.2 * 0.2 - 1e-9, 0.5 + 1e-9));
            Assert.Contains(edge, e => e.Radius > 0.3 + 0.2 * 0.8);
            Assert.Contains(edge, e => e.Radius < 0.3 + 0.2 * 0.55 + 1e-9);
        }

        [Fact]
        public void NoJagged_IsACircle()
        {
            var edge = IrisPatterns.CollaretteEdge(0.25, 0.15, 0.0, 12, 3, false);
            Assert.All(edge, e => Assert.Equal(0.4, e.Radius, 9));
        }

        [Fact]
        public void RightEye_IsTheMirrorImage()
        {
            var left = IrisPatterns.CollaretteEdge(0.3, 0.2, 0.6, 20, 9, false);
            var right = IrisPatterns.CollaretteEdge(0.3, 0.2, 0.6, 20, 9, true);
            for (int k = 0; k < left.Length; k++)
            {
                Assert.Equal(left[k].Radius, right[k].Radius);
                Assert.Equal(-Math.Cos(left[k].Angle), Math.Cos(right[k].Angle), 9);
                Assert.Equal(Math.Sin(left[k].Angle), Math.Sin(right[k].Angle), 9);
            }
        }

        [Fact]
        public void SoftEdge_FadesFromFullToNothing()
        {
            var steps = SvgExporter.NestedFadeSteps(1.0, 0.2, SvgExporter.CollaretteFadeStep,
                SvgExporter.MinCollaretteFadeSteps, SvgExporter.MaxCollaretteFadeSteps);
            Assert.InRange(steps.Length, SvgExporter.MinCollaretteFadeSteps, SvgExporter.MaxCollaretteFadeSteps);
            // Widest first; everything inside all of them is (almost) fully covered
            for (int k = 1; k < steps.Length; k++) Assert.True(steps[k].Reach < steps[k - 1].Reach);
            double covered = 1.0 - steps.Aggregate(1.0, (a, s) => a * (1.0 - s.Opacity));
            Assert.True(covered > 0.99, covered.ToString());
        }

        [Fact]
        public void Svg_DrawsTheRing_UnderTheRaysAndThePupil()
        {
            var p = Ringed();
            string svg = SvgExporter.GeneratePairSvg(p);
            Assert.Contains("<mask id=\"left_eye_collarette_mask\">", svg);
            Assert.Contains("<ellipse id=\"left_eye_collarette\"", svg);
            Assert.Contains("<ellipse id=\"right_eye_collarette\"", svg);
            int ring = svg.IndexOf("left_eye_collarette\"", StringComparison.Ordinal);
            Assert.True(ring < svg.IndexOf("left_eye_upper_shadow", StringComparison.Ordinal));
            Assert.True(ring < svg.IndexOf("left_eye_rays", StringComparison.Ordinal));
            Assert.True(ring < svg.IndexOf("left_eye_pupil", StringComparison.Ordinal));
            System.Xml.Linq.XDocument.Parse(svg);

            // Crisp: the edge itself, with the blend mode
            p.CollaretteSoftness = 0;
            p.CollaretteBlend = LayerBlend.Screen;
            svg = SvgExporter.GeneratePairSvg(p);
            Assert.DoesNotContain("_collarette_mask", svg);
            Assert.Matches("<path id=\"left_eye_collarette\" d=\"M[^\"]+\" fill=\"#FFD54F\" opacity=\"0.60\" style=\"mix-blend-mode:screen\" />", svg);

            // A gradient runs from the pupil's edge to the ring's edge
            p.CollaretteColor = "radial-gradient(#FFFFFF 0%, #FF8800 100%)";
            svg = SvgExporter.GeneratePairSvg(p);
            Assert.Contains("id=\"left_eye_collarette_fill\"", svg);
            System.Xml.Linq.XDocument.Parse(svg);
        }

        [Fact]
        public void PupilTooLarge_LeavesNoRoomForTheRing()
        {
            var p = Ringed();
            p.PupilSizeRatio = 0.6;
            p.CollaretteWidth = 0.5;
            string svg = SvgExporter.GeneratePairSvg(p);
            Assert.Contains("left_eye_collarette", svg); // 0.35 of room left

            // The pupil + width is held to 0.95: the edge never passes it
            var edge = IrisPatterns.CollaretteEdge(0.6, Math.Min(0.5, 0.95 - 0.6), 0.5, 20, 1, false);
            Assert.All(edge, e => Assert.True(e.Radius <= 0.95 + 1e-9));
        }

        [Fact]
        public void Settings_AreClamped_AndSaved()
        {
            var p = new IrisParameters
            {
                CollaretteIntensity = 2, CollaretteWidth = 0.9, CollaretteJagged = double.NaN, CollaretteSpikeCount = 2,
                CollaretteSoftness = -1, CollaretteBlend = (LayerBlend)42, CollaretteColor = null!
            };
            Assert.Equal(1.0, p.CollaretteIntensity);
            Assert.Equal(IrisParameters.MaxCollaretteWidth, p.CollaretteWidth);
            Assert.Equal(IrisParameters.DefaultCollaretteJagged, p.CollaretteJagged);
            Assert.Equal(IrisParameters.MinCollaretteSpikeCount, p.CollaretteSpikeCount);
            Assert.Equal(0.0, p.CollaretteSoftness);
            Assert.Equal(LayerBlend.Normal, p.CollaretteBlend);
            Assert.Equal(IrisParameters.DefaultCollaretteColor, p.CollaretteColor);

            var q = new IrisParameters { CollaretteIntensity = 0.5, CollaretteSpikeCount = 30, CollaretteColor = "#123456", CollaretteBlend = LayerBlend.Screen };
            var restored = JsonSerializer.Deserialize<IrisParameters>(JsonSerializer.Serialize(q))!;
            Assert.Equal(0.5, restored.CollaretteIntensity);
            Assert.Equal(30, restored.CollaretteSpikeCount);
            Assert.Equal("#123456", restored.CollaretteColor);
            Assert.Equal(LayerBlend.Screen, restored.CollaretteBlend);

            var old = JsonSerializer.Deserialize<IrisParameters>("{\"PupilFacetIntensity\": 0.5}")!;
            Assert.Equal(0.0, old.CollaretteIntensity);
        }

        [Fact]
        public void Presets_CarryTheRing_AndBuiltInsTurnItOff()
        {
            var design = Ringed();
            design.CollaretteColor = "#AA7700";
            design.CollaretteSpikeCount = 33;
            var preset = ColorPresets.FromDesign(design, "test", "");
            var target = new IrisParameters();
            ColorPresets.ApplyPreset(target, preset);
            Assert.Equal(0.6, target.CollaretteIntensity);
            Assert.Equal("#AA7700", target.CollaretteColor);
            Assert.Equal(33, target.CollaretteSpikeCount);

            ColorPresets.ApplyPreset(target, ColorPresets.All[0]);
            Assert.Equal(0.0, target.CollaretteIntensity);
            Assert.Equal(IrisParameters.DefaultCollaretteColor, target.CollaretteColor);
        }

        [Fact]
        public void Randomizer_SometimesAddsTheRing_AndThePupilLockKeepsIt()
        {
            int on = 0;
            for (int i = 0; i < 200; i++)
            {
                var p = new IrisParameters();
                IrisRandomizer.RandomizeAttributes(p, new Random(i));
                if (p.CollaretteIntensity > 0) on++;
            }
            Assert.InRange(on, 25, 75); // about 25%

            var kept = Ringed();
            kept.CollaretteSpikeCount = 41;
            kept.CollaretteColor = "#010203";
            IrisRandomizer.RandomizeAttributes(kept, RandomizeLocks.Pupil, new Random(3));
            Assert.Equal(0.6, kept.CollaretteIntensity);
            Assert.Equal(41, kept.CollaretteSpikeCount);
            Assert.Equal("#010203", kept.CollaretteColor);
        }
    }
}
