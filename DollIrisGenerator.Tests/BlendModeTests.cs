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
            Assert.All(p.Highlights, h => Assert.Equal(LayerBlend.Normal, h.Blend));
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
            var p = new IrisParameters { AuroraOpacity = 0.5, AuroraBlend = blend, GlitterBlend = blend };
            foreach (var h in p.Highlights) h.Blend = blend;
            string svg = SvgExporter.GenerateSingleEyeSvg(p, false);
            Assert.True(SvgValidator.IsWellFormed(svg, out var error), error);
            string style = $"style=\"mix-blend-mode:{css}\"";
            Assert.Matches($"<ellipse id=\"eye_left_aurora\" [^>]*{style}", svg);
            Assert.Contains($"<g id=\"eye_left_glitter_layer\" {style}>", svg);
            Assert.Contains("<g id=\"eye_left_highlights\">", svg);
            Assert.Matches($"<g id=\"eye_left_highlights\">\\s*<(ellipse|polygon) [^>]*{style}", svg);

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

        /// <summary>Each highlight has its own 重ね方, on its own shape; the others stay as they are.</summary>
        [Fact]
        public void EachHighlightGetsItsOwnBlendMode()
        {
            var p = new IrisParameters();
            p.Highlights.Clear();
            p.Highlights.Add(new HighlightItem { Shape = HighlightShape.Circle, Blend = LayerBlend.Screen });
            p.Highlights.Add(new HighlightItem { Shape = HighlightShape.Oval, AspectRatio = 1.4 });
            p.Highlights.Add(new HighlightItem { Shape = HighlightShape.Star4, Rotation = 20, Blend = LayerBlend.ColorDodge });
            p.Highlights.Add(new HighlightItem { Shape = HighlightShape.Rhombus, Blend = LayerBlend.SoftLight });

            string svg = SvgExporter.GenerateSingleEyeSvg(p, false);
            Assert.True(SvgValidator.IsWellFormed(svg, out var error), error);
            string highlights = svg[svg.IndexOf("<g id=\"eye_left_highlights\">", StringComparison.Ordinal)..];
            var shapes = System.Text.RegularExpressions.Regex.Matches(highlights, "<(ellipse|polygon)[^>]*/>");
            Assert.Equal(4, shapes.Count);
            Assert.Contains("mix-blend-mode:screen", shapes[0].Value);
            Assert.DoesNotContain("mix-blend-mode", shapes[1].Value);
            Assert.Contains("mix-blend-mode:color-dodge", shapes[2].Value);
            Assert.Contains("rotate(", shapes[2].Value);
            Assert.Contains("mix-blend-mode:soft-light", shapes[3].Value);
        }

        [Fact]
        public void SavedLoadedAndCheckedLikeOtherSettings()
        {
            var p = new IrisParameters { AuroraBlend = LayerBlend.ColorDodge, GlitterBlend = LayerBlend.Screen };
            p.Highlights[0].Blend = LayerBlend.SoftLight;
            string json = JsonSerializer.Serialize(p);
            var back = JsonSerializer.Deserialize<IrisParameters>(json)!;
            Assert.Equal(LayerBlend.ColorDodge, back.AuroraBlend);
            Assert.Equal(LayerBlend.Screen, back.GlitterBlend);
            Assert.Equal(LayerBlend.SoftLight, back.Highlights[0].Blend);
            Assert.Equal(LayerBlend.Normal, back.Highlights[1].Blend);
            Assert.Equal(LayerBlend.SoftLight, back.Clone().Highlights[0].Blend);
            Assert.Equal(LayerBlend.SoftLight, back.Highlights[0].Clone().Blend);
            Assert.Equal(LayerBlend.Normal, JsonSerializer.Deserialize<HighlightItem>("{\"Blend\":42}")!.Blend);

            // An unknown number from a hand-edited file
            Assert.Equal(LayerBlend.Normal, JsonSerializer.Deserialize<IrisParameters>("{\"AuroraBlend\":42}")!.AuroraBlend);

            var q = new IrisParameters();
            ColorPresets.ApplyPreset(q, ColorPresets.FromDesign(p, "x", ""));
            Assert.Equal(LayerBlend.ColorDodge, q.AuroraBlend);
            Assert.Equal(LayerBlend.SoftLight, q.Highlights[0].Blend);
            ColorPresets.ApplyPreset(q, ColorPresets.All[0]);
            Assert.Equal(LayerBlend.Normal, q.AuroraBlend);
            Assert.All(q.Highlights, h => Assert.Equal(LayerBlend.Normal, h.Blend));
        }
    }
}
