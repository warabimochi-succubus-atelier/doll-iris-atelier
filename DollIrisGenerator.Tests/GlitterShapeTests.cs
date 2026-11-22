using System;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using DollIrisGenerator.Core.Geometry;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using Xunit;

namespace DollIrisGenerator.Tests
{
    /// <summary>ラメの粒の形 (SPECIFICATION.md 11): round, stars, hearts, diamonds or petals, and a share of them.</summary>
    public class GlitterShapeTests
    {
        private static IrisParameters Glitter(GlitterShape shape, double ratio = 1.0) =>
            new() { ParticleCount = 60, ParticleShape = shape, ParticleShapeRatio = ratio };

        private static System.Collections.Generic.List<GrainSpec> Grains(IrisParameters p, bool right = false) =>
            IrisPatterns.Grains(p, new Point(0, 0), 50, 50, 50, p.Seed, right, 1.0, 0.5);

        private static string GlitterGroup(string svg)
        {
            int start = svg.IndexOf("id=\"left_eye_glitter\"", StringComparison.Ordinal);
            return svg[start..svg.IndexOf("</g>", start, StringComparison.Ordinal)];
        }

        [Fact]
        public void Default_IsTheOldMix_EveryFifthAStar()
        {
            var p = new IrisParameters { ParticleCount = 60 };
            Assert.Equal(GlitterShape.Mixed, p.ParticleShape);
            var grains = Grains(p);
            Assert.All(grains.Select((g, i) => (g, i)), x => Assert.Equal(x.i % 5 == 0, x.g.IsStar));
            Assert.All(grains, g => Assert.Null(g.Shape));
        }

        [Fact]
        public void Shapes_KeepTheGrainsWhereTheyWere()
        {
            var mixed = Grains(new IrisParameters { ParticleCount = 60 });
            foreach (var shape in Enum.GetValues<GlitterShape>())
            {
                var shaped = Grains(Glitter(shape, 0.5));
                Assert.Equal(mixed.Select(g => (g.Position, g.Size, g.HasFlare)), shaped.Select(g => (g.Position, g.Size, g.HasFlare)));
            }
        }

        [Fact]
        public void Circle_IsAllRound_Star4AllStars_AndTheRatioMixesInRoundOnes()
        {
            Assert.All(Grains(Glitter(GlitterShape.Circle, 0.3)), g => Assert.True(!g.IsStar && g.Shape == null));
            Assert.All(Grains(Glitter(GlitterShape.Star4)), g => Assert.True(g.IsStar && g.Shape == null));

            var hearts = Grains(Glitter(GlitterShape.Heart, 0.4));
            int shaped = hearts.Count(g => g.Shape == ShapeKind.Heart);
            Assert.InRange(shaped, 12, 36); // about 40% of 60
            Assert.All(hearts.Where(g => g.Shape == null), g => Assert.False(g.IsStar));
            Assert.All(hearts.Where(g => g.Shape != null), g => Assert.InRange(g.Rotation, -20.0, 20.0));
        }

        [Fact]
        public void RightEye_MirrorsTheTurn()
        {
            var p = Glitter(GlitterShape.SakuraPetal);
            var left = Grains(p);
            var right = Grains(p, right: true);
            for (int i = 0; i < left.Count; i++) Assert.Equal(-left[i].Rotation, right[i].Rotation, 9);
        }

        [Fact]
        public void Svg_DrawsShapedGrainsAsPaths()
        {
            var p = Glitter(GlitterShape.Star5);
            p.ParticleFlareRatio = 0; // flares are paths too
            string group = GlitterGroup(SvgExporter.GeneratePairSvg(p));
            Assert.Equal(60, Regex.Matches(group, "<path d=\"M").Count);
            Assert.Empty(Regex.Matches(group, "<circle "));
            Assert.Contains("transform=\"rotate(", group);

            p.ParticleShapeRatio = 0.5;
            group = GlitterGroup(SvgExporter.GeneratePairSvg(p));
            int paths = Regex.Matches(group, "<path d=\"M").Count, circles = Regex.Matches(group, "<circle ").Count;
            Assert.Equal(60, paths + circles);
            Assert.InRange(paths, 18, 42);
            System.Xml.Linq.XDocument.Parse(SvgExporter.GeneratePairSvg(p));
        }

        [Fact]
        public void Settings_AreClamped_SavedAndCarriedByPresets()
        {
            var p = new IrisParameters { ParticleShape = (GlitterShape)42, ParticleShapeRatio = 0 };
            Assert.Equal(GlitterShape.Mixed, p.ParticleShape);
            Assert.Equal(IrisParameters.MinParticleShapeRatio, p.ParticleShapeRatio);
            p.ParticleShapeRatio = double.NaN;
            Assert.Equal(1.0, p.ParticleShapeRatio);

            var q = Glitter(GlitterShape.Heart, 0.4);
            var restored = JsonSerializer.Deserialize<IrisParameters>(JsonSerializer.Serialize(q))!;
            Assert.Equal(GlitterShape.Heart, restored.ParticleShape);
            Assert.Equal(0.4, restored.ParticleShapeRatio);
            Assert.Equal(GlitterShape.Mixed, JsonSerializer.Deserialize<IrisParameters>("{\"ParticleCount\": 5}")!.ParticleShape);

            var target = new IrisParameters();
            ColorPresets.ApplyPreset(target, ColorPresets.FromDesign(q, "test", ""));
            Assert.Equal(GlitterShape.Heart, target.ParticleShape);
            ColorPresets.ApplyPreset(target, ColorPresets.All[0]);
            Assert.Equal(GlitterShape.Mixed, target.ParticleShape);
        }

        [Fact]
        public void Randomizer_MostlyKeepsTheOldMix_AndTheLockKeepsTheShape()
        {
            int shaped = 0;
            for (int i = 0; i < 400; i++)
            {
                var p = new IrisParameters();
                IrisRandomizer.RandomizeAttributes(p, new Random(i));
                if (p.ParticleShape != GlitterShape.Mixed)
                {
                    shaped++;
                    Assert.InRange(p.ParticleShapeRatio, 0.3, 1.0);
                }
            }
            Assert.InRange(shaped, 25, 80); // about 12% (アニメ・幻想・宝石 designs with glitter, SPECIFICATION.md 7.4)

            var kept = Glitter(GlitterShape.Diamond, 0.6);
            IrisRandomizer.RandomizeAttributes(kept, RandomizeLocks.Particles, new Random(3));
            Assert.Equal(GlitterShape.Diamond, kept.ParticleShape);
            Assert.Equal(0.6, kept.ParticleShapeRatio);
        }
    }
}
