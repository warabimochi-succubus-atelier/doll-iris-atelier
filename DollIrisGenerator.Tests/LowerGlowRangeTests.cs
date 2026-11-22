using System;
using System.Text.Json;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using Xunit;

namespace DollIrisGenerator.Tests
{
    /// <summary>下部反射光の範囲 (SPECIFICATION.md 7.1.5): where the lower glow starts.</summary>
    public class LowerGlowRangeTests
    {
        [Fact]
        public void Default_WritesTheOldFixedStart()
        {
            var p = new IrisParameters();
            Assert.Equal(0.45, p.BottomHighlightStart);
            Assert.Contains("<stop offset=\"45%\" stop-color=\"#B3E5FC\" stop-opacity=\"0\" />", SvgExporter.GeneratePairSvg(p));

            // A file saved before the setting existed starts at 45% too
            Assert.Equal(0.45, JsonSerializer.Deserialize<IrisParameters>("{\"BottomHighlightIntensity\": 0.5}")!.BottomHighlightStart);
        }

        [Fact]
        public void Start_MovesTheClearStop_ForBothEyes()
        {
            var p = new IrisParameters { BottomHighlightStart = 0.2, IsOddEye = true };
            string svg = SvgExporter.GeneratePairSvg(p);
            Assert.Contains($"<stop offset=\"20%\" stop-color=\"{p.BottomHighlightColor}\" stop-opacity=\"0\" />", svg);
            Assert.Contains($"<stop offset=\"20%\" stop-color=\"{p.OddEyeBottomHighlightColor}\" stop-opacity=\"0\" />", svg);
            System.Xml.Linq.XDocument.Parse(svg);
        }

        [Fact]
        public void Settings_AreClamped_SavedAndCarriedByPresets()
        {
            var p = new IrisParameters { BottomHighlightStart = 2 };
            Assert.Equal(IrisParameters.MaxBottomHighlightStart, p.BottomHighlightStart);
            p.BottomHighlightStart = double.NaN;
            Assert.Equal(IrisParameters.DefaultBottomHighlightStart, p.BottomHighlightStart);
            p.BottomHighlightStart = -1;
            Assert.Equal(0.0, p.BottomHighlightStart);

            var q = new IrisParameters { BottomHighlightStart = 0.6 };
            Assert.Equal(0.6, JsonSerializer.Deserialize<IrisParameters>(JsonSerializer.Serialize(q))!.BottomHighlightStart);
            var target = new IrisParameters();
            ColorPresets.ApplyPreset(target, ColorPresets.FromDesign(q, "test", ""));
            Assert.Equal(0.6, target.BottomHighlightStart);
            ColorPresets.ApplyPreset(target, ColorPresets.All[0]);
            Assert.Equal(IrisParameters.DefaultBottomHighlightStart, target.BottomHighlightStart);
        }

        [Fact]
        public void Randomizer_MostlyKeepsTheUsualStart_AndTheColorLockKeepsIt()
        {
            int moved = 0;
            for (int i = 0; i < 400; i++)
            {
                var p = new IrisParameters();
                IrisRandomizer.RandomizeAttributes(p, new Random(i));
                if (p.BottomHighlightStart != IrisParameters.DefaultBottomHighlightStart)
                {
                    moved++;
                    Assert.InRange(p.BottomHighlightStart, 0.30, 0.70);
                }
            }
            Assert.InRange(moved, 80, 160); // about 30%

            var kept = new IrisParameters { BottomHighlightStart = 0.85 };
            IrisRandomizer.RandomizeAttributes(kept, RandomizeLocks.Colors, new Random(3));
            Assert.Equal(0.85, kept.BottomHighlightStart);
        }
    }
}
