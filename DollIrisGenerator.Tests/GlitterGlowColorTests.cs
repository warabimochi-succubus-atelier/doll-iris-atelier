using System;
using System.Text.Json;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using Xunit;

namespace DollIrisGenerator.Tests
{
    /// <summary>ラメのにじみの色 (SPECIFICATION.md 11): a glow in its own color, e.g. a light halo round black glitter.</summary>
    public class GlitterGlowColorTests
    {
        private static IrisParameters Glitter() => new() { ParticleCount = 40, ParticleGlow = 0.7 };

        private static string GlowFilter(string svg)
        {
            int start = svg.IndexOf("<filter id=\"left_eye_glitter_glow_filter\"", StringComparison.Ordinal);
            return svg[start..(svg.IndexOf("</filter>", start, StringComparison.Ordinal) + 9)];
        }

        [Fact]
        public void Off_TheGlowKeepsTheGrainsColor_AsBefore()
        {
            var p = Glitter();
            Assert.False(p.ParticleGlowCustomColor);
            string filter = GlowFilter(SvgExporter.GeneratePairSvg(p));
            Assert.DoesNotContain("feFlood", filter);
            Assert.DoesNotContain("result=", filter);
        }

        [Fact]
        public void On_TheBlurredShapeIsFilledWithTheGlowColor()
        {
            var p = Glitter();
            p.ParticleColor = "#000000";
            p.ParticleGlowCustomColor = true;
            p.ParticleGlowColor = "#E1F5FE";
            string svg = SvgExporter.GeneratePairSvg(p);
            string filter = GlowFilter(svg);
            Assert.Contains("<feComponentTransfer result=\"shape\">", filter);
            Assert.Contains($"slope=\"{SvgExporter.HaloAlphaSlope:0.00}\"", filter);
            Assert.Contains("<feFlood flood-color=\"#E1F5FE\" />", filter);
            Assert.Contains("<feComposite in2=\"shape\" operator=\"in\" />", filter);
            Assert.True(filter.IndexOf("feComponentTransfer", StringComparison.Ordinal) < filter.IndexOf("feFlood", StringComparison.Ordinal));
            System.Xml.Linq.XDocument.Parse(svg);

            // A color's own alpha goes to the flood
            p.ParticleGlowColor = "#80FFFFFF";
            Assert.Contains("<feFlood flood-color=\"#FFFFFF\" flood-opacity=\"0.502\" />", GlowFilter(SvgExporter.GeneratePairSvg(p)));

            // No glow, no filter at all
            p.ParticleGlow = 0;
            Assert.DoesNotContain("_glitter_glow", SvgExporter.GeneratePairSvg(p));
        }

        [Fact]
        public void Settings_AreSaved_AndCarriedByPresets()
        {
            var p = new IrisParameters { ParticleGlowCustomColor = true, ParticleGlowColor = "#123456" };
            var restored = JsonSerializer.Deserialize<IrisParameters>(JsonSerializer.Serialize(p))!;
            Assert.True(restored.ParticleGlowCustomColor);
            Assert.Equal("#123456", restored.ParticleGlowColor);
            Assert.False(JsonSerializer.Deserialize<IrisParameters>("{\"ParticleGlow\": 0.5}")!.ParticleGlowCustomColor);
            p.ParticleGlowColor = null!;
            Assert.Equal(IrisParameters.DefaultParticleGlowColor, p.ParticleGlowColor);

            var target = new IrisParameters();
            ColorPresets.ApplyPreset(target, ColorPresets.FromDesign(restored, "test", ""));
            Assert.True(target.ParticleGlowCustomColor);
            Assert.Equal("#123456", target.ParticleGlowColor);
            ColorPresets.ApplyPreset(target, ColorPresets.All[0]);
            Assert.False(target.ParticleGlowCustomColor);
        }

        [Fact]
        public void Randomizer_SometimesMakesBlackGlitterWithAHalo()
        {
            int black = 0;
            for (int i = 0; i < 600; i++)
            {
                var p = new IrisParameters();
                IrisRandomizer.RandomizeAttributes(p, new Random(i));
                if (!p.ParticleGlowCustomColor) continue;
                black++;
                Assert.True(p.ParticleCount > 0);
                Assert.Equal(LayerBlend.Normal, p.GlitterBlend);
                Assert.InRange(p.ParticleGlow, 0.6, 0.9);
                var (_, r, g, b) = GeometryHelper.ParseHexArgb(p.ParticleColor);
                Assert.True(r + g + b < 3 * 40, p.ParticleColor); // nearly black
            }
            Assert.InRange(black, 20, 90); // 10% of the designs with glitter

            // The color lock keeps the glitter as it was
            var kept = Glitter();
            kept.ParticleColor = "#FFFFFF";
            IrisRandomizer.RandomizeAttributes(kept, RandomizeLocks.Colors, new Random(1));
            Assert.Equal("#FFFFFF", kept.ParticleColor);
            Assert.False(kept.ParticleGlowCustomColor);
        }
    }
}
