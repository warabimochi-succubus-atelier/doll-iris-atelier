using System;
using System.Linq;
using System.Text.Json;
using DollIrisGenerator.Core.Geometry;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using Xunit;

namespace DollIrisGenerator.Tests
{
    /// <summary>宝石カット面 (SPECIFICATION.md 12.1): brilliant-cut facets over the whole iris.</summary>
    public class IrisFacetTests
    {
        private static IrisParameters Faceted() => new() { IrisFacetIntensity = 0.6 };

        [Fact]
        public void OffByDefault_AndExportsAsBefore()
        {
            var p = new IrisParameters();
            Assert.Equal(0.0, p.IrisFacetIntensity);
            Assert.DoesNotContain("_iris_facets", SvgExporter.GeneratePairSvg(p));
        }

        [Theory]
        [InlineData(6)]
        [InlineData(8)]
        [InlineData(16)]
        public void Geometry_HasTheBrilliantCutFacets(int count)
        {
            var facets = IrisPatterns.IrisFacets(count, 0.45, 270, 0.6, 1, false);
            Assert.Equal(4 * count + 1, facets.Count);
            Assert.Equal(count, facets[0].Polygon.Length); // the table
            Assert.All(facets, f => Assert.InRange(f.Shade, -1.0, 1.0));
            // The table's corners sit on the table radius, the first straight up
            Assert.Equal(0.0, facets[0].Polygon[0].X, 9);
            Assert.Equal(-0.45, facets[0].Polygon[0].Y, 9);
        }

        /// <summary>Every point of the iris (and the corners a shaped outline reaches) lies in exactly one facet.</summary>
        [Fact]
        public void Facets_CoverTheIrisWithoutOverlap()
        {
            var facets = IrisPatterns.IrisFacets(8, 0.45, 270, 0.6, 3, false);
            var rng = new Random(7);
            for (int k = 0; k < 2000; k++)
            {
                var q = new Point(rng.NextDouble() * 2.8 - 1.4, rng.NextDouble() * 2.8 - 1.4);
                if (q.X * q.X + q.Y * q.Y > 1.4 * 1.4) continue;
                int inside = facets.Count(f => Contains(f.Polygon, q));
                Assert.True(inside == 1, $"({q.X:F3}, {q.Y:F3}) is in {inside} facets");
            }
        }

        private static bool Contains(Point[] poly, Point q)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].Y > q.Y) != (poly[j].Y > q.Y)
                    && q.X < (poly[j].X - poly[i].X) * (q.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X)
                    inside = !inside;
            }
            return inside;
        }

        [Fact]
        public void FullBias_DarkensTheShadeSide()
        {
            var facets = IrisPatterns.IrisFacets(8, 0.45, 270, 1.0, 5, false).Skip(1).ToList();
            double CenterY(Point[] poly) => poly.Average(q => q.Y);
            double upper = facets.Where(f => CenterY(f.Polygon) < -0.3).Average(f => f.Shade);
            double lower = facets.Where(f => CenterY(f.Polygon) > 0.3).Average(f => f.Shade);
            Assert.True(upper < -0.3 && lower > 0.3, $"upper {upper}, lower {lower}");
        }

        [Fact]
        public void RightEye_IsTheMirrorImage()
        {
            var left = IrisPatterns.IrisFacets(10, 0.4, 250, 0.7, 9, false);
            var right = IrisPatterns.IrisFacets(10, 0.4, 250, 0.7, 9, true);
            for (int k = 0; k < left.Count; k++)
            {
                Assert.Equal(left[k].Shade, right[k].Shade);
                for (int j = 0; j < left[k].Polygon.Length; j++)
                {
                    Assert.Equal(-left[k].Polygon[j].X, right[k].Polygon[j].X, 9);
                    Assert.Equal(left[k].Polygon[j].Y, right[k].Polygon[j].Y, 9);
                }
            }
        }

        [Fact]
        public void Svg_DrawsTheFacetGroup_WithBlendAndRange()
        {
            var p = Faceted();
            string svg = SvgExporter.GeneratePairSvg(p);
            Assert.Contains("<g id=\"left_eye_iris_facets\" style=\"mix-blend-mode:overlay\">", svg);
            Assert.Contains("<g id=\"right_eye_iris_facets\" style=\"mix-blend-mode:overlay\">", svg);
            // Under the limbal ring and the pupil
            Assert.True(svg.IndexOf("left_eye_iris_facets", StringComparison.Ordinal) < svg.IndexOf("left_eye_limbal_ring", StringComparison.Ordinal));
            Assert.True(svg.IndexOf("left_eye_iris_facets", StringComparison.Ordinal) < svg.IndexOf("left_eye_pupil", StringComparison.Ordinal));

            p.IrisFacetBlend = LayerBlend.Normal;
            PatternRange.Set(p, PatternLayer.IrisFacets, 270, 180, 60);
            svg = SvgExporter.GeneratePairSvg(p);
            Assert.Contains("<g id=\"left_eye_iris_facets\" mask=\"url(#left_eye_iris_facets_range)\">", svg);
            System.Xml.Linq.XDocument.Parse(svg);
        }

        [Fact]
        public void Settings_AreClamped_AndSaved()
        {
            var p = new IrisParameters
            {
                IrisFacetIntensity = 2, IrisFacetCount = 99, IrisFacetTableSize = 0.01, IrisFacetShadeAngle = -45,
                IrisFacetShadeBias = double.NaN, IrisFacetBlend = (LayerBlend)42
            };
            Assert.Equal(1.0, p.IrisFacetIntensity);
            Assert.Equal(IrisParameters.MaxIrisFacetCount, p.IrisFacetCount);
            Assert.Equal(IrisParameters.MinIrisFacetTableSize, p.IrisFacetTableSize);
            Assert.Equal(315.0, p.IrisFacetShadeAngle);
            Assert.Equal(IrisParameters.DefaultIrisFacetShadeBias, p.IrisFacetShadeBias);
            Assert.Equal(LayerBlend.Normal, p.IrisFacetBlend);

            var q = new IrisParameters { IrisFacetIntensity = 0.5, IrisFacetCount = 12, IrisFacetBlend = LayerBlend.SoftLight };
            var restored = JsonSerializer.Deserialize<IrisParameters>(JsonSerializer.Serialize(q))!;
            Assert.Equal(0.5, restored.IrisFacetIntensity);
            Assert.Equal(12, restored.IrisFacetCount);
            Assert.Equal(LayerBlend.SoftLight, restored.IrisFacetBlend);

            var old = JsonSerializer.Deserialize<IrisParameters>("{\"PupilFacetIntensity\": 0.5}")!;
            Assert.Equal(0.0, old.IrisFacetIntensity);
            Assert.Equal(LayerBlend.Overlay, old.IrisFacetBlend);
        }

        [Fact]
        public void Presets_CarryTheFacets_AndBuiltInsTurnThemOff()
        {
            var design = Faceted();
            design.IrisFacetCount = 10;
            design.IrisFacetShadeAngle = 250;
            var preset = ColorPresets.FromDesign(design, "test", "");
            var target = new IrisParameters();
            ColorPresets.ApplyPreset(target, preset);
            Assert.Equal(0.6, target.IrisFacetIntensity);
            Assert.Equal(10, target.IrisFacetCount);
            Assert.Equal(250, target.IrisFacetShadeAngle);

            ColorPresets.ApplyPreset(target, ColorPresets.All[0]);
            Assert.Equal(0.0, target.IrisFacetIntensity);
        }

        [Fact]
        public void Randomizer_SometimesAddsFacets_AndTheLockKeepsThem()
        {
            int on = 0;
            for (int i = 0; i < 200; i++)
            {
                var p = new IrisParameters();
                IrisRandomizer.RandomizeAttributes(p, new Random(i));
                if (p.IrisFacetIntensity > 0) on++;
            }
            Assert.InRange(on, 10, 60); // about 15%

            var kept = Faceted();
            kept.IrisFacetCount = 14;
            IrisRandomizer.RandomizeAttributes(kept, RandomizeLocks.IrisFacets, new Random(3));
            Assert.Equal(0.6, kept.IrisFacetIntensity);
            Assert.Equal(14, kept.IrisFacetCount);
        }
    }
}
