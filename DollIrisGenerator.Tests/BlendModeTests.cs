using System;
using System.Text.Json;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using Xunit;

namespace DollIrisGenerator.Tests
{
    /// <summary>重ね方 (mix-blend-mode) of the aurora layer, the glitter and the highlights.</summary>
    public class BlendModeTests
    {
        [Fact]
        public void NormalByDefaultAndExportsAsBefore()
        {
            var p = new IrisParameters { AuroraOpacity = 0.5 };
            Assert.Equal(LayerBlend.Normal, p.AuroraBlend);
            Assert.Equal(LayerBlend.Normal, p.GlitterBlend);
            Assert.Equal(LayerBlend.Normal, p.HighlightBlend);
            string svg = SvgExporter.GenerateSingleEyeSvg(p, false);
            Assert.DoesNotContain("mix-blend-mode", svg);
            Assert.DoesNotContain("_glitter_layer", svg);
        }

        [Theory]
        [InlineData(LayerBlend.Screen, "screen")]
        [InlineData(LayerBlend.ColorDodge, "color-dodge")]
        [InlineData(LayerBlend.Overlay, "overlay")]
        [InlineData(LayerBlend.SoftLight, "soft-light")]
        public void EachLayerGetsItsBlendMode(LayerBlend blend, string css)
        {
            var p = new IrisParameters { AuroraOpacity = 0.5, AuroraBlend = blend, GlitterBlend = blend, HighlightBlend = blend };
            string svg = SvgExporter.GenerateSingleEyeSvg(p, false);
            Assert.True(SvgValidator.IsWellFormed(svg, out var error), error);
            string style = $"style=\"mix-blend-mode:{css}\"";
            Assert.Matches($"<ellipse id=\"eye_left_aurora\" [^>]*{style}", svg);
            Assert.Contains($"<g id=\"eye_left_glitter_layer\" {style}>", svg);
            Assert.Contains($"<g id=\"eye_left_highlights\" {style}>", svg);

            // The glow goes inside the blended glitter layer, with the grains
            int layer = svg.IndexOf("eye_left_glitter_layer", StringComparison.Ordinal);
            Assert.True(svg.IndexOf("<use id=\"eye_left_glitter_glow\"", StringComparison.Ordinal) > layer);
        }

        /// <summary>Drawn (resvg): a screen-blended aurora only lightens; a normal one also darkens bright areas.</summary>
        [Fact]
        public void ScreenOnlyLightens()
        {
            byte[] Pixels(IrisParameters p)
            {
                var bmp = RasterExporter.RenderSingleToBitmap(p, false, dpi: 150);
                var px = new byte[bmp.PixelWidth * bmp.PixelHeight * 4];
                bmp.CopyPixels(px, bmp.PixelWidth * 4, 0);
                return px;
            }
            IrisParameters Design(LayerBlend blend, double aurora)
            {
                var d = new IrisParameters { SizeMm = 10, AuroraColor = "#404040", AuroraOpacity = aurora, AuroraBlend = blend, RayCount = 0, ParticleCount = 0 };
                d.Highlights.Clear();
                return d;
            }
            var none = Pixels(Design(LayerBlend.Normal, 0));
            var normal = Pixels(Design(LayerBlend.Normal, 1));
            var screen = Pixels(Design(LayerBlend.Screen, 1));
            // Inside the iris only (12 mm canvas at 150 DPI ≈ 71 px, iris radius ≈ 29.5 px): the anti-aliased rim
            // blends against the transparent outside
            int size = (int)Math.Sqrt(none.Length / 4);
            int darker = 0, screenDarker = 0;
            for (int i = 0; i < none.Length; i += 4)
            {
                int x = (i / 4) % size, y = (i / 4) / size;
                double dx = x - size / 2.0, dy = y - size / 2.0;
                if (dx * dx + dy * dy > 26.0 * 26.0) continue;
                int b0 = none[i] + none[i + 1] + none[i + 2];
                if (normal[i] + normal[i + 1] + normal[i + 2] < b0 - 6) darker++;
                if (screen[i] + screen[i + 1] + screen[i + 2] < b0 - 6) screenDarker++;
            }
            Assert.True(darker > 500, $"a normal dark gray layer should darken the iris ({darker})");
            Assert.Equal(0, screenDarker);
        }

        [Fact]
        public void SavedLoadedAndCheckedLikeOtherSettings()
        {
            var p = new IrisParameters { AuroraBlend = LayerBlend.ColorDodge, GlitterBlend = LayerBlend.Screen, HighlightBlend = LayerBlend.SoftLight };
            var back = JsonSerializer.Deserialize<IrisParameters>(JsonSerializer.Serialize(p))!;
            Assert.Equal(LayerBlend.ColorDodge, back.AuroraBlend);
            Assert.Equal(LayerBlend.Screen, back.GlitterBlend);
            Assert.Equal(LayerBlend.SoftLight, back.HighlightBlend);

            // An unknown number from a hand-edited file
            Assert.Equal(LayerBlend.Normal, JsonSerializer.Deserialize<IrisParameters>("{\"AuroraBlend\":42}")!.AuroraBlend);

            var q = new IrisParameters();
            ColorPresets.ApplyPreset(q, ColorPresets.FromDesign(p, "x", ""));
            Assert.Equal(LayerBlend.ColorDodge, q.AuroraBlend);
            ColorPresets.ApplyPreset(q, ColorPresets.All[0]);
            Assert.Equal(LayerBlend.Normal, q.AuroraBlend);
        }
    }
}
