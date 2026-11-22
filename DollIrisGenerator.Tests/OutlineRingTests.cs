using System;
using System.Text.Json;
using DollIrisGenerator.Core.Geometry;
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
            Assert.DoesNotContain("_pupil_inner_ring", svg);
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

        // ---- 瞳孔の中の輪 ----

        [Fact]
        public void PupilInnerRing_ShrinksTheShape_KeepingTheLineWidth_AndGoesWithTheOutline()
        {
            var p = new IrisParameters { PupilInnerRingIntensity = 0.7, PupilInnerRingSize = 0.5, PupilSizeRatio = 0.4 };
            double baseR = p.SizeMm / 2.0;
            string svg = SvgExporter.GeneratePairSvg(p);
            string ring = Element(svg, "left_eye_pupil_inner_ring");
            Assert.Contains(" scale(0.50000) ", ring);
            Assert.Contains($"stroke-width=\"{(baseR * p.PupilInnerRingWidth / 0.5).ToFixed(4)}\"", ring);
            Assert.Contains("opacity=\"0.70\"", ring);
            Assert.DoesNotContain("_pupil_outline", svg);

            // Both at once: the ring under the outline, both over the pupil
            p.PupilOutlineIntensity = 0.8;
            p.PupilShape = PupilShape.Heart;
            svg = SvgExporter.GeneratePairSvg(p);
            int pupil = svg.IndexOf("id=\"left_eye_pupil\"", StringComparison.Ordinal);
            int inner = svg.IndexOf("id=\"left_eye_pupil_inner_ring\"", StringComparison.Ordinal);
            int outline = svg.IndexOf("id=\"left_eye_pupil_outline\"", StringComparison.Ordinal);
            Assert.True(pupil < inner && inner < outline);
            Assert.DoesNotContain("scale(", Element(svg, "left_eye_pupil_outline"));
            System.Xml.Linq.XDocument.Parse(svg);

            p.PupilOpacity = 0;
            Assert.DoesNotContain("_pupil_inner_ring", SvgExporter.GeneratePairSvg(p));
        }

        [Theory]
        [InlineData(PupilShape.Cross)]
        [InlineData(PupilShape.Moon)]
        public void PupilInnerRing_CrossAndMoon_StayInsideThePupil(PupilShape shape)
        {
            const double r = 10.0;
            var outer = ShapeOutline.ForPupil(shape, r, r).Flatten(64);
            foreach (double size in new[] { 0.1, 0.25, 0.4, 0.6, 0.8, 0.95 })
            {
                var inner = ShapeOutline.PupilInnerRing(shape, r, size)!;
                Assert.All(inner.Flatten(64), pt => Assert.True(Inside(outer, pt), $"{shape} {size}: {pt}"));
            }
            Assert.Null(ShapeOutline.PupilInnerRing(PupilShape.Heart, r, 0.5));

            // Size 1 is the outline itself
            var full = ShapeOutline.PupilInnerRing(shape, r, 1.0)!.GetBounds();
            var bounds = ShapeOutline.ForPupil(shape, r, r).GetBounds();
            Assert.Equal(bounds.Left, full.Left, 6);
            Assert.Equal(bounds.Top, full.Top, 6);
            Assert.Equal(bounds.Right, full.Right, 6);
            Assert.Equal(bounds.Bottom, full.Bottom, 6);
        }

        [Fact]
        public void PupilInnerRing_CrossAndMoon_AreDrawnWithoutScaling()
        {
            foreach (var shape in new[] { PupilShape.Cross, PupilShape.Moon })
            {
                var p = new IrisParameters { PupilShape = shape, PupilInnerRingIntensity = 0.7, PupilInnerRingSize = 0.5, PupilSizeRatio = 0.4, IsOddEye = false };
                string svg = SvgExporter.GeneratePairSvg(p);
                string ring = Element(svg, "left_eye_pupil_inner_ring");
                Assert.DoesNotContain("scale(", ring);
                Assert.Contains($"stroke-width=\"{(p.SizeMm / 2.0 * p.PupilInnerRingWidth).ToFixed(4)}\"", ring);
                System.Xml.Linq.XDocument.Parse(svg);
            }
        }

        /// <summary>Even-odd point-in-polygon, with the outline's own points counted as inside.</summary>
        private static bool Inside(System.Collections.Generic.List<Point> poly, Point pt)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                var a = poly[i]; var b = poly[j];
                if (Math.Abs(a.X - pt.X) < 1e-6 && Math.Abs(a.Y - pt.Y) < 1e-6) return true;
                if ((a.Y > pt.Y) != (b.Y > pt.Y) && pt.X < (b.X - a.X) * (pt.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
            }
            return inside;
        }

        [Fact]
        public void PupilInnerRings_UpToThree_EachWithItsOwnSettings()
        {
            var p = new IrisParameters { PupilInnerRingIntensity = 0.7, PupilInnerRingSize = 0.7, PupilSizeRatio = 0.4 };
            Assert.Equal(0.0, p.PupilInnerRingIntensity2);
            Assert.Equal(0.0, p.PupilInnerRingIntensity3);
            Assert.Equal(IrisParameters.DefaultPupilInnerRingSize2, p.PupilInnerRingSize2);
            Assert.Equal(IrisParameters.DefaultPupilInnerRingSize3, p.PupilInnerRingSize3);
            string svg = SvgExporter.GeneratePairSvg(p);
            Assert.DoesNotContain("_pupil_inner_ring2", svg);
            Assert.DoesNotContain("_pupil_inner_ring3", svg);

            // Only the third on: drawn with its own settings, the second not
            p.PupilInnerRingIntensity3 = 0.5;
            p.PupilInnerRingSize3 = 0.25;
            p.PupilInnerRingColor3 = "#FF80AB";
            svg = SvgExporter.GeneratePairSvg(p);
            Assert.DoesNotContain("_pupil_inner_ring2", svg);
            string third = Element(svg, "left_eye_pupil_inner_ring3");
            Assert.Contains(" scale(0.25000) ", third);
            Assert.Contains("stroke=\"#FF80AB\"", third);
            Assert.Contains("opacity=\"0.50\"", third);

            // The second with its own width and strength; not evenly spaced
            p.PupilInnerRingIntensity2 = 0.9;
            p.PupilInnerRingSize2 = 0.6;
            p.PupilInnerRingWidth2 = 0.03;
            svg = SvgExporter.GeneratePairSvg(p);
            double baseR = p.SizeMm / 2.0;
            string second = Element(svg, "left_eye_pupil_inner_ring2");
            Assert.Contains(" scale(0.60000) ", second);
            Assert.Contains($"stroke-width=\"{(baseR * 0.03 / 0.6).ToFixed(4)}\"", second);
            Assert.Contains("opacity=\"0.90\"", second);
            Assert.Contains("stroke=\"#FFFFFF\"", second);
            int first = svg.IndexOf("id=\"left_eye_pupil_inner_ring\"", StringComparison.Ordinal);
            Assert.True(first < svg.IndexOf("left_eye_pupil_inner_ring2", StringComparison.Ordinal));
            Assert.True(svg.IndexOf("left_eye_pupil_inner_ring2", StringComparison.Ordinal) < svg.IndexOf("left_eye_pupil_inner_ring3", StringComparison.Ordinal));
            System.Xml.Linq.XDocument.Parse(svg);

            // The first ring off: the others still drawn
            p.PupilInnerRingIntensity = 0;
            svg = SvgExporter.GeneratePairSvg(p);
            Assert.DoesNotContain("id=\"left_eye_pupil_inner_ring\"", svg);
            Assert.Contains("id=\"left_eye_pupil_inner_ring2\"", svg);

            // Clamped like the first ring
            p.PupilInnerRingSize2 = 0.01;
            Assert.Equal(IrisParameters.MinPupilInnerRingSize, p.PupilInnerRingSize2);
            p.PupilInnerRingSize3 = double.NaN;
            Assert.Equal(IrisParameters.DefaultPupilInnerRingSize3, p.PupilInnerRingSize3);
            p.PupilInnerRingWidth3 = 1;
            Assert.Equal(IrisParameters.MaxPupilOutlineWidth, p.PupilInnerRingWidth3);
            p.PupilInnerRingColor2 = null!;
            Assert.Equal(IrisParameters.DefaultPupilInnerRingColor, p.PupilInnerRingColor2);

            // Saved and carried by presets
            var q = new IrisParameters { PupilInnerRingIntensity2 = 0.4, PupilInnerRingSize2 = 0.3, PupilInnerRingColor3 = "#123456" };
            var restored = JsonSerializer.Deserialize<IrisParameters>(JsonSerializer.Serialize(q))!;
            Assert.Equal(0.4, restored.PupilInnerRingIntensity2);
            Assert.Equal(0.3, restored.PupilInnerRingSize2);
            Assert.Equal("#123456", restored.PupilInnerRingColor3);
            var target = new IrisParameters();
            ColorPresets.ApplyPreset(target, ColorPresets.FromDesign(restored, "test", ""));
            Assert.Equal(0.4, target.PupilInnerRingIntensity2);
            Assert.Equal("#123456", target.PupilInnerRingColor3);
            ColorPresets.ApplyPreset(target, ColorPresets.All[0]);
            Assert.Equal(0.0, target.PupilInnerRingIntensity2);
        }

        [Fact]
        public void Randomizer_SometimesAddsSmallerRingsInside()
        {
            int two = 0, three = 0;
            for (int i = 0; i < 1500; i++)
            {
                var p = new IrisParameters();
                IrisRandomizer.RandomizeAttributes(p, new Random(i));
                if (p.PupilInnerRingIntensity2 > 0)
                {
                    two++;
                    Assert.True(p.PupilInnerRingIntensity > 0);
                    Assert.True(p.PupilInnerRingSize2 < p.PupilInnerRingSize);
                }
                if (p.PupilInnerRingIntensity3 > 0)
                {
                    three++;
                    Assert.True(p.PupilInnerRingIntensity2 > 0);
                    Assert.True(p.PupilInnerRingSize3 < p.PupilInnerRingSize2);
                }
            }
            Assert.InRange(two, 40, 140);   // 35% of the 15% with a ring
            Assert.InRange(three, 5, 60);   // 30% of those
        }

        /// <summary>The element of the given id.</summary>
        private static string Element(string svg, string id)
        {
            int at = svg.IndexOf($"id=\"{id}\" ", StringComparison.Ordinal);
            int start = svg.LastIndexOf('<', at);
            return svg[start..(svg.IndexOf("/>", at, StringComparison.Ordinal) + 2)];
        }

        /// <summary>The 扇形の色違い recipe in the manual and DESIGN_JSON_FORMAT.md loads cleanly and draws.</summary>
        [Fact]
        public void SectorColorRecipe_IsValidAndDraws()
        {
            string json = "{\"Name\": \"t\", \"AuroraColor\": \"conic-gradient(from 300deg, #C67C2A 0%, #C67C2A 22%, #00C67C2A 22%, #00C67C2A 100%)\", "
                        + "\"AuroraOpacity\": 0.85, \"AuroraBlend\": 0}";
            var report = DesignFileValidator.Validate(json);
            Assert.DoesNotContain(report.Issues, i => i.Level != DesignIssueLevel.Note);
            var p = Assert.Single(report.Designs).Parameters;
            Assert.True(ColorFill.Parse(p.AuroraColor).IsGradient);
            string svg = SvgExporter.GeneratePairSvg(p);
            Assert.Contains("left_eye_aurora_fill", svg);
            System.Xml.Linq.XDocument.Parse(svg);
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
                PupilInnerRingIntensity = -1, PupilInnerRingSize = 2, PupilInnerRingWidth = 0, PupilInnerRingColor = null!,
                LimbalInnerRingIntensity = -1, LimbalInnerRingWidth = 0, LimbalInnerRingGap = double.NaN, LimbalInnerRingColor = null!
            };
            Assert.Equal(1.0, p.PupilOutlineIntensity);
            Assert.Equal(IrisParameters.MaxPupilOutlineWidth, p.PupilOutlineWidth);
            Assert.Equal(IrisParameters.DefaultPupilOutlineColor, p.PupilOutlineColor);
            Assert.Equal(0.0, p.PupilInnerRingIntensity);
            Assert.Equal(IrisParameters.MaxPupilInnerRingSize, p.PupilInnerRingSize);
            Assert.Equal(IrisParameters.MinPupilOutlineWidth, p.PupilInnerRingWidth);
            Assert.Equal(IrisParameters.DefaultPupilInnerRingColor, p.PupilInnerRingColor);
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
            Assert.Equal(0.0, old.PupilInnerRingIntensity);
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

            int ring = 0;
            for (int i = 0; i < 300; i++)
            {
                var p = new IrisParameters();
                IrisRandomizer.RandomizeAttributes(p, new Random(i));
                if (p.PupilInnerRingIntensity > 0) { ring++; Assert.InRange(p.PupilInnerRingSize, 0.45, 0.75); }
            }
            Assert.InRange(ring, 20, 75); // about 15%

            var keptRing = new IrisParameters { PupilInnerRingIntensity = 0.6, PupilInnerRingColor = "#010203" };
            IrisRandomizer.RandomizeAttributes(keptRing, RandomizeLocks.Pupil, new Random(5));
            Assert.Equal(0.6, keptRing.PupilInnerRingIntensity);
            Assert.Equal("#010203", keptRing.PupilInnerRingColor);

            var kept = new IrisParameters { PupilOutlineIntensity = 0.7, LimbalInnerRingIntensity = 0.4 };
            IrisRandomizer.RandomizeAttributes(kept, RandomizeLocks.Pupil | RandomizeLocks.LimbalRing, new Random(3));
            Assert.Equal(0.7, kept.PupilOutlineIntensity);
            Assert.Equal(0.4, kept.LimbalInnerRingIntensity);
        }
    }
}
