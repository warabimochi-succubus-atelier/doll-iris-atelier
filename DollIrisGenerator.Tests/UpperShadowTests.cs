using System;
using System.Text.Json;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using Xunit;

namespace DollIrisGenerator.Tests
{
    /// <summary>まぶたの影 (SPECIFICATION.md 7.1.3): the upper eyelid shadow's strength, depth and color.</summary>
    public class UpperShadowTests
    {
        /// <summary>The defaults write exactly the fixed shadow earlier versions always drew.</summary>
        [Fact]
        public void Defaults_WriteTheOldFixedShadow()
        {
            var p = new IrisParameters();
            Assert.Equal(0.65, p.UpperShadowIntensity);
            Assert.Equal(0.65, p.UpperShadowDepth);
            Assert.Equal("#000000", p.UpperShadowColor);
            string svg = SvgExporter.GeneratePairSvg(p);
            Assert.Contains(
                "<linearGradient id=\"left_eye_shadow_grad\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\">\n" +
                "      <stop offset=\"0%\" stop-color=\"#000000\" stop-opacity=\"0.65\" />\n" +
                "      <stop offset=\"65%\" stop-color=\"#000000\" stop-opacity=\"0\" />\n" +
                "    </linearGradient>", svg.Replace("\r\n", "\n"));
            Assert.Contains("id=\"left_eye_upper_shadow\"", svg);

            // A file saved before the setting existed loads as the old shadow
            var old = JsonSerializer.Deserialize<IrisParameters>("{\"FiberNoise\": 0.2}")!;
            Assert.Equal(0.65, old.UpperShadowIntensity);
            Assert.Equal(0.65, old.UpperShadowDepth);
        }

        [Fact]
        public void Settings_ChangeTheGradient()
        {
            var p = new IrisParameters { UpperShadowIntensity = 0.8, UpperShadowDepth = 0.4, UpperShadowColor = "#1A237E" };
            string svg = SvgExporter.GeneratePairSvg(p);
            Assert.Contains("<stop offset=\"0%\" stop-color=\"#1A237E\" stop-opacity=\"0.8\" />", svg);
            Assert.Contains("<stop offset=\"40%\" stop-color=\"#1A237E\" stop-opacity=\"0\" />", svg);

            // A color's own alpha multiplies the strength
            p.UpperShadowColor = "#80000000";
            Assert.Contains("stop-color=\"#000000\" stop-opacity=\"0.402\"", SvgExporter.GeneratePairSvg(p));
            System.Xml.Linq.XDocument.Parse(svg);
        }

        [Fact]
        public void ZeroStrength_DrawsNoShadow()
        {
            var p = new IrisParameters { UpperShadowIntensity = 0 };
            string svg = SvgExporter.GeneratePairSvg(p);
            Assert.DoesNotContain("_shadow_grad", svg);
            Assert.DoesNotContain("_upper_shadow", svg);
            System.Xml.Linq.XDocument.Parse(svg);
        }

        [Fact]
        public void Settings_AreClamped_AndSaved()
        {
            var p = new IrisParameters { UpperShadowIntensity = 3, UpperShadowDepth = 0, UpperShadowColor = null! };
            Assert.Equal(1.0, p.UpperShadowIntensity);
            Assert.Equal(IrisParameters.MinUpperShadowDepth, p.UpperShadowDepth);
            Assert.Equal(IrisParameters.DefaultUpperShadowColor, p.UpperShadowColor);
            p.UpperShadowIntensity = double.NaN;
            Assert.Equal(IrisParameters.DefaultUpperShadowIntensity, p.UpperShadowIntensity);

            var q = new IrisParameters { UpperShadowIntensity = 0.3, UpperShadowDepth = 0.9, UpperShadowColor = "#123456" };
            var restored = JsonSerializer.Deserialize<IrisParameters>(JsonSerializer.Serialize(q))!;
            Assert.Equal(0.3, restored.UpperShadowIntensity);
            Assert.Equal(0.9, restored.UpperShadowDepth);
            Assert.Equal("#123456", restored.UpperShadowColor);
        }

        [Fact]
        public void Presets_CarryTheShadow_AndBuiltInsUseTheUsualOne()
        {
            var design = new IrisParameters { UpperShadowIntensity = 0.2, UpperShadowColor = "#3E2723" };
            var target = new IrisParameters();
            ColorPresets.ApplyPreset(target, ColorPresets.FromDesign(design, "test", ""));
            Assert.Equal(0.2, target.UpperShadowIntensity);
            Assert.Equal("#3E2723", target.UpperShadowColor);

            ColorPresets.ApplyPreset(target, ColorPresets.All[0]);
            Assert.Equal(IrisParameters.DefaultUpperShadowIntensity, target.UpperShadowIntensity);
            Assert.Equal(IrisParameters.DefaultUpperShadowColor, target.UpperShadowColor);
        }

        [Fact]
        public void Randomizer_MostlyKeepsTheUsualShadow_AndTheColorLockKeepsIt()
        {
            int usual = 0, light = 0, deep = 0;
            for (int i = 0; i < 400; i++)
            {
                var p = new IrisParameters();
                IrisRandomizer.RandomizeAttributes(p, new Random(i));
                if (p.UpperShadowIntensity == IrisParameters.DefaultUpperShadowIntensity) usual++;
                else if (p.UpperShadowIntensity < 0.6) light++;
                else deep++;
                Assert.InRange(p.UpperShadowIntensity, 0.25, 0.85);
            }
            Assert.InRange(usual, 240, 320); // about 70%
            Assert.InRange(light, 30, 100);
            Assert.InRange(deep, 30, 100);

            var kept = new IrisParameters { UpperShadowIntensity = 0.1, UpperShadowColor = "#010203" };
            IrisRandomizer.RandomizeAttributes(kept, RandomizeLocks.Colors, new Random(3));
            Assert.Equal(0.1, kept.UpperShadowIntensity);
            Assert.Equal("#010203", kept.UpperShadowColor);
        }
    }
}
