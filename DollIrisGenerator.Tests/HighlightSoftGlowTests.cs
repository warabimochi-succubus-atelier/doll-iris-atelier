using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using Xunit;

namespace DollIrisGenerator.Tests
{
    /// <summary>輪郭のぼかし (Softness) and 光のにじみ (Glow) of a highlight (SPECIFICATION.md 14).</summary>
    public class HighlightSoftGlowTests
    {
        private static IrisParameters OneHighlight(Action<HighlightItem> setUp)
        {
            var p = new IrisParameters();
            p.Highlights.Clear();
            var h = new HighlightItem { Size = 0.25, Opacity = 0.8, Blend = LayerBlend.Screen };
            setUp(h);
            p.Highlights.Add(h);
            return p;
        }

        private static string Highlights(IrisParameters p)
        {
            string svg = SvgExporter.GenerateSingleEyeSvg(p, false);
            int start = svg.IndexOf("<g id=\"eye_left_highlights\">", StringComparison.Ordinal);
            Assert.True(start >= 0);
            return svg[start..];
        }

        private static double StdDeviation(string svg, string filterId)
        {
            var m = Regex.Match(svg, $"<filter id=\"{filterId}\"[^>]*>\\s*<feGaussianBlur stdDeviation=\"([0-9.]+)\"");
            Assert.True(m.Success, filterId);
            return double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        [Fact]
        public void ByDefault_TheHighlightIsDrawnAsBefore_WithoutFilters()
        {
            var h = new HighlightItem();
            Assert.Equal(0.0, h.Softness);
            Assert.Equal(0.0, h.Glow);
            string svg = Highlights(OneHighlight(_ => { }));
            Assert.DoesNotContain("filter", svg.Split("</g>")[0]);
            Assert.Matches("<ellipse [^>]*opacity=\"0.80\"[^>]*mix-blend-mode", svg);
        }

        [Fact]
        public void Softness_BlursTheShape_InAGroupThatCarriesOpacityAndBlend()
        {
            var p = OneHighlight(h => h.Softness = 0.5);
            string svg = Highlights(p);
            Assert.Matches("<g id=\"eye_left_highlight0_soft\" filter=\"url\\(#eye_left_highlight0_soft_filter\\)\" opacity=\"0.80\"[^>]*mix-blend-mode", svg);
            // The shape inside is fully opaque and has no blend of its own
            Assert.Matches("_soft\"[^>]*>\\s*<ellipse [^>]*opacity=\"1.00\" />", svg);
            // The blur grows with the softness (half the softness, half the deviation)
            double sigma = StdDeviation(svg, "eye_left_highlight0_soft_filter");
            Assert.True(sigma > 0);
            Assert.Equal(sigma, StdDeviation(Highlights(OneHighlight(h => h.Softness = 1.0)), "eye_left_highlight0_soft_filter") / 2.0, 3);
        }

        [Fact]
        public void Glow_IsABlurredCopyUnderTheHighlight()
        {
            string svg = Highlights(OneHighlight(h => h.Glow = 0.25));
            int glow = svg.IndexOf("<g id=\"eye_left_highlight0_glow\"", StringComparison.Ordinal);
            int shape = svg.IndexOf("opacity=\"0.80\"", StringComparison.Ordinal);
            Assert.True(glow >= 0 && shape > glow, "the glow comes first, under the highlight");
            // Opacity √にじみ × 不透明度, with the highlight's 重ね方 and a strengthened alpha
            Assert.Matches("<g id=\"eye_left_highlight0_glow\" [^>]*opacity=\"0.40\"[^>]*mix-blend-mode", svg);
            Assert.Matches("<filter id=\"eye_left_highlight0_glow_filter\"[\\s\\S]*?<feFuncA type=\"linear\" slope=\"2.00\"", svg);
            // More glow spreads further
            Assert.True(StdDeviation(Highlights(OneHighlight(h => h.Glow = 1.0)), "eye_left_highlight0_glow_filter")
                > StdDeviation(svg, "eye_left_highlight0_glow_filter"));
        }

        [Fact]
        public void RepeatedCopies_EachGetTheirOwnFilters()
        {
            string svg = Highlights(OneHighlight(h => { h.Count = 3; h.Softness = 0.3; h.Glow = 0.5; }));
            for (int c = 0; c < 3; c++)
            {
                Assert.Contains($"id=\"eye_left_highlight0_{c}_glow\"", svg);
                Assert.Contains($"id=\"eye_left_highlight0_{c}_soft\"", svg);
            }
            var ids = Regex.Matches(svg, "id=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
        }

        [Fact]
        public void EveryShape_CanBeSoftAndGlow()
        {
            foreach (HighlightShape shape in Enum.GetValues(typeof(HighlightShape)))
            {
                string svg = Highlights(OneHighlight(h => { h.Shape = shape; h.Rotation = 30; h.Softness = 0.4; h.Glow = 0.6; }));
                Assert.Contains("_glow_filter", svg);
                Assert.Matches("_soft\"[^>]*>\\s*<(path|ellipse|polygon) [^>]*transform=\"rotate", svg);
            }
        }

        [Fact]
        public void Values_AreClamped_AndCopiedWithTheHighlight()
        {
            var h = new HighlightItem { Softness = 2.0, Glow = -1.0 };
            Assert.Equal(1.0, h.Softness);
            Assert.Equal(0.0, h.Glow);

            var source = new HighlightItem { Softness = 0.3, Glow = 0.7 };
            var clone = source.Clone();
            Assert.Equal((0.3, 0.7), (clone.Softness, clone.Glow));

            Assert.Contains(nameof(HighlightItem.Softness), HighlightItem.BulkSettingNames);
            Assert.Contains(nameof(HighlightItem.Glow), HighlightItem.BulkSettingNames);
            var target = new HighlightItem();
            Assert.True(source.CopySettingTo(target, nameof(HighlightItem.Softness)));
            Assert.True(source.CopySettingTo(target, nameof(HighlightItem.Glow)));
            Assert.Equal((0.3, 0.7), (target.Softness, target.Glow));
        }
    }
}
