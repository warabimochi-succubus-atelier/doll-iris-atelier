using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using Xunit;

namespace DollIrisGenerator.Tests
{
    /// <summary>偏光の虹色 (SPECIFICATION.md 7.1.6): rainbow stripes laid out by the light.</summary>
    public class HoloTests
    {
        private static double Num(string s) => double.Parse(s, CultureInfo.InvariantCulture);

        [Fact]
        public void OffByDefault_AndExportsAsBefore()
        {
            var p = new IrisParameters();
            Assert.Equal(0.0, p.HoloIntensity);
            Assert.DoesNotContain("_holo", SvgExporter.GeneratePairSvg(p));
        }

        [Fact]
        public void Stops_RunTheRainbowScaleTimes()
        {
            var stops = SvgExporter.HoloStops(4.0);
            Assert.Equal(4 * SvgExporter.HoloStopsPerRainbow + 1, stops.Length);
            Assert.Equal(0.0, stops[0].Offset);
            Assert.Equal(1.0, stops[^1].Offset);
            Assert.Equal(stops[0].Color, stops[SvgExporter.HoloStopsPerRainbow].Color); // one rainbow later, the same hue
            Assert.True(stops.Zip(stops.Skip(1)).All(x => x.First.Offset < x.Second.Offset));

            // A part of a rainbow is cut off at 100%
            var part = SvgExporter.HoloStops(2.5);
            Assert.Equal(1.0, part[^1].Offset);
        }

        [Fact]
        public void Linear_RunsAlongTheLight_MirroredForTheRightEye()
        {
            // The default light from above: the stripes run up and down (bands lie across)
            var p = new IrisParameters { HoloIntensity = 0.4 };
            string svg = SvgExporter.GeneratePairSvg(p);
            var g = Regex.Match(svg, "<linearGradient id=\"left_eye_holo_grad\" gradientUnits=\"userSpaceOnUse\" x1=\"([0-9.\\-]+)\" y1=\"([0-9.\\-]+)\" x2=\"([0-9.\\-]+)\" y2=\"([0-9.\\-]+)\"");
            Assert.True(g.Success);
            Assert.Equal(Num(g.Groups[1].Value), Num(g.Groups[3].Value), 2);
            Assert.True(Num(g.Groups[2].Value) > Num(g.Groups[4].Value)); // toward the light (up)
            Assert.Contains("mix-blend-mode:screen", Regex.Match(svg, "<ellipse id=\"left_eye_holo\"[^>]*>").Value);

            // Light from the upper left: the right eye's gradient is the mirror image
            p.LightAngle = 225;
            svg = SvgExporter.GeneratePairSvg(p);
            double Dx(string eye)
            {
                var m = Regex.Match(svg, $"<linearGradient id=\"{eye}_holo_grad\" gradientUnits=\"userSpaceOnUse\" x1=\"([0-9.\\-]+)\" y1=\"[0-9.\\-]+\" x2=\"([0-9.\\-]+)\"");
                return Num(m.Groups[2].Value) - Num(m.Groups[1].Value);
            }
            Assert.True(Dx("left_eye") < 0);
            Assert.Equal(-Dx("left_eye"), Dx("right_eye"), 2);
            System.Xml.Linq.XDocument.Parse(svg);
        }

        [Fact]
        public void Concentric_IsCenteredTowardTheLight_AndTheRangeMasksIt()
        {
            var p = new IrisParameters { HoloIntensity = 0.4, HoloShape = HoloShape.Concentric, HoloBlend = LayerBlend.SoftLight };
            string svg = SvgExporter.GeneratePairSvg(p);
            var g = Regex.Match(svg, "<radialGradient id=\"left_eye_holo_grad\" cx=\"([0-9.]+)%\" cy=\"([0-9.]+)%\" r=\"([0-9.]+)%\"");
            Assert.True(g.Success);
            Assert.Equal(50.0, Num(g.Groups[1].Value), 2);
            Assert.Equal(50.0 - SvgExporter.HoloCenterShift / 2.0 * 100.0, Num(g.Groups[2].Value), 2); // light from above
            Assert.Contains("mix-blend-mode:soft-light", svg);

            PatternRange.Apply(p, PatternLayer.Holo, PatternRange.Styles[1]);
            svg = SvgExporter.GeneratePairSvg(p);
            Assert.Contains("mask=\"url(#left_eye_holo_range)\"", Regex.Match(svg, "<ellipse id=\"left_eye_holo\"[^>]*>").Value);
            // Over the gem facets' place, under the limbal ring
            Assert.True(svg.IndexOf("id=\"left_eye_holo\"", StringComparison.Ordinal) < svg.IndexOf("id=\"left_eye_limbal_ring\"", StringComparison.Ordinal));
            System.Xml.Linq.XDocument.Parse(svg);
        }

        [Fact]
        public void Settings_AreClamped_SavedAndCarriedByPresets()
        {
            var p = new IrisParameters { HoloIntensity = 3, HoloScale = 0, HoloShape = (HoloShape)7, HoloBlend = (LayerBlend)9 };
            Assert.Equal(1.0, p.HoloIntensity);
            Assert.Equal(IrisParameters.MinHoloScale, p.HoloScale);
            p.HoloScale = 12;
            Assert.Equal(5.0, p.HoloScale); // at most 5 rainbows
            Assert.Equal(HoloShape.Linear, p.HoloShape);
            Assert.Equal(LayerBlend.Screen, p.HoloBlend);

            var q = new IrisParameters { HoloIntensity = 0.3, HoloScale = 3.5, HoloShape = HoloShape.Concentric };
            var back = JsonSerializer.Deserialize<IrisParameters>(JsonSerializer.Serialize(q))!;
            Assert.Equal((0.3, 3.5, HoloShape.Concentric), (back.HoloIntensity, back.HoloScale, back.HoloShape));
            Assert.Equal(0.0, JsonSerializer.Deserialize<IrisParameters>("{\"TwoToneIntensity\": 0.5}")!.HoloIntensity);

            var target = new IrisParameters();
            ColorPresets.ApplyPreset(target, ColorPresets.FromDesign(q, "test", ""));
            Assert.Equal(0.3, target.HoloIntensity);
            ColorPresets.ApplyPreset(target, ColorPresets.All[0]);
            Assert.Equal(0.0, target.HoloIntensity);
        }

        [Fact]
        public void Randomizer_RarelyAddsIt_AndTheColorLockKeepsIt()
        {
            int on = 0;
            for (int i = 0; i < 500; i++)
            {
                var p = new IrisParameters();
                IrisRandomizer.RandomizeAttributes(p, new Random(i));
                if (p.HoloIntensity > 0) { on++; Assert.InRange(p.HoloIntensity, 0.20, 0.45); }
            }
            Assert.InRange(on, 25, 80); // about 10%

            var kept = new IrisParameters { HoloIntensity = 0.9, HoloScale = 1.2 };
            IrisRandomizer.RandomizeAttributes(kept, RandomizeLocks.Colors, new Random(3));
            Assert.Equal((0.9, 1.2), (kept.HoloIntensity, kept.HoloScale));
        }
    }
}
