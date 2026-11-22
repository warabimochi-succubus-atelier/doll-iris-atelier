using System;
using System.Collections.Generic;
using System.Linq;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using Xunit;

namespace DollIrisGenerator.Tests
{
    /// <summary>What 「属性ランダム生成」 makes (SPECIFICATION.md 7.4): the shapes, the highlight layouts and where symbols go.</summary>
    public class RandomizerVarietyTests
    {
        private static IEnumerable<IrisParameters> Designs(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var p = new IrisParameters();
                IrisRandomizer.RandomizeAttributes(p, new Random(i));
                yield return p;
            }
        }

        /// <summary>The extra effects a design has on (IrisRandomizer.Extra).</summary>
        private static int ExtrasOn(IrisParameters p) => new[]
        {
            p.CollaretteIntensity > 0, p.LimbalInnerRingIntensity > 0, p.CausticIntensity > 0, p.DomeEdgeIntensity > 0,
            p.CenterGlowIntensity > 0, p.ToneBandIntensity > 0, p.TwoToneIntensity > 0, p.PupilOutlineIntensity > 0,
            p.PupilInnerRingIntensity > 0, p.RimRayIntensity > 0, p.AuroraOpacity > 0, p.NebulaOpacity > 0, p.HoloIntensity > 0,
            p.IrisFacetIntensity > 0, p.ParticleGlowCustomColor, p.ParticleShape != GlitterShape.Mixed, p.Symbols.Count > 0, p.IsOddEye
        }.Count(x => x);

        [Fact]
        public void Tables_AreComplete_AndAddUpTo100()
        {
            Assert.Equal(100, IrisRandomizer.Looks.Sum(x => x.Weight));
            Assert.Equal(Enum.GetValues<IrisRandomizer.Extra>().OrderBy(e => e), IrisRandomizer.Extras.Keys.OrderBy(e => e));
            foreach (var (look, _, _) in IrisRandomizer.Looks)
            {
                Assert.Equal(100, IrisRandomizer.PupilShapesOf(look).Sum(x => x.Weight));
                Assert.DoesNotContain(IrisRandomizer.PupilShapesOf(look), x => x.Shape == PupilShape.Cross);
            }
            // Together the looks use every highlight layout
            Assert.Equal(HighlightStyles.All.OrderBy(s => s),
                IrisRandomizer.Looks.SelectMany(x => IrisRandomizer.HighlightStylesOf(x.Look)).Distinct().OrderBy(s => s));
        }

        [Fact]
        public void Extras_StayWithinTheBudget_AndDoNotPileUp()
        {
            var counts = Designs(600).Select(ExtrasOn).ToList();
            Assert.All(counts, c => Assert.InRange(c, 0, IrisRandomizer.MaxExtras));
            Assert.InRange(counts.Average(), 1.5, 3.0);
            Assert.Contains(counts, c => c == IrisRandomizer.MaxExtras);
            // One color split at a time, and no two-tone split with the odd eye
            Assert.All(Designs(600), p => Assert.False(p.TwoToneIntensity > 0 && (p.ToneBandIntensity > 0 || p.IsOddEye)));
            // The black glitter and the grain shapes only where there are grains
            Assert.All(Designs(600), p => Assert.True(p.ParticleCount > 0 || (!p.ParticleGlowCustomColor && p.ParticleShape == GlitterShape.Mixed)));
        }

        [Fact]
        public void TheLooks_AllShowUp_PlainAndDecorated()
        {
            var designs = Designs(600).ToList();
            // ナチュラル: fibers without glitter or symbols; 幻想: aurora, nebula or holographic sheen; 宝石: gem facets
            Assert.Contains(designs, p => p.FiberNoise >= 0.25 && p.ParticleCount == 0 && p.Symbols.Count == 0);
            Assert.Contains(designs, p => p.NebulaOpacity > 0);
            Assert.Contains(designs, p => p.HoloIntensity > 0 && p.AuroraOpacity == 0);
            Assert.Contains(designs, p => p.IrisFacetIntensity > 0 && p.PupilFacetIntensity > 0);
            Assert.Contains(designs, p => p.TwoToneIntensity > 0);
        }

        [Fact]
        public void PupilShapes_ComeFromTheTable_WithoutTheCross()
        {
            var shapes = Designs(600).Select(p => p.PupilShape).ToList();
            var expected = new[]
            {
                PupilShape.Round, PupilShape.CatSlit, PupilShape.Heart, PupilShape.Star5, PupilShape.Star4,
                PupilShape.Diamond, PupilShape.SakuraPetal, PupilShape.Butterfly, PupilShape.Moon
            };
            Assert.Equal(expected.OrderBy(s => s), shapes.Distinct().OrderBy(s => s));
            // The round pupil stays the most common one
            Assert.True(shapes.Count(s => s == PupilShape.Round) > shapes.Count / 2);
        }

        [Fact]
        public void OnlyRoundPupils_AreStretchedToAnOval()
        {
            foreach (var p in Designs(300))
            {
                if (p.PupilShape != PupilShape.Round) Assert.Equal(1.0, p.PupilStretchTop);
            }
            Assert.Contains(Designs(300), p => p.PupilStretchTop == 1.25 && p.PupilStretchLeft == 0.75);
        }

        [Fact]
        public void Symbols_StayOutOfTheHighlightCorners()
        {
            int seen = 0;
            foreach (var p in Designs(400))
            {
                foreach (var s in p.Symbols)
                {
                    seen++;
                    Assert.True(s.Enabled);
                    Assert.NotEqual(SymbolType.Cross, s.Type);
                    // Upper right (x > 0, y < 0) or lower left (x < 0, y > 0): the highlights are upper left / lower right
                    Assert.True(s.PositionX * s.PositionY < 0, $"symbol at ({s.PositionX}, {s.PositionY})");
                    double r = Math.Sqrt(s.PositionX * s.PositionX + s.PositionY * s.PositionY);
                    Assert.InRange(r, 0.46, 0.64);
                }
            }
            Assert.True(seen > 50);
            Assert.Contains(Designs(400), p => p.Symbols.Count == 2);
            Assert.Contains(Designs(400), p => p.Symbols.Any(s => s.Type == SymbolType.Butterfly));
        }

        [Fact]
        public void Highlights_UseEveryLayout_WithTheMainLightUpperLeft()
        {
            var layouts = new HashSet<string>();
            foreach (var p in Designs(300))
            {
                Assert.InRange(p.Highlights.Count, 2, 3);
                var main = p.Highlights[0];
                Assert.True(main.PositionX < 0 && main.PositionY < 0);
                layouts.Add(string.Join(",", p.Highlights.Select(h => h.Shape)) + p.Highlights.Count);
            }
            // Circle-only layouts share their shapes, so count the distinct shape lists the layouts make
            int distinct = HighlightStyles.All.Select(s => string.Join(",", HighlightStyles.Build(s).Select(h => h.Shape)) + HighlightStyles.Build(s).Count).Distinct().Count();
            Assert.Equal(distinct, layouts.Count);
        }

        [Fact]
        public void Highlights_AreCrispSoftOrGlowing_AndOnlySmoothShapesAreBlurred()
        {
            int crisp = 0, soft = 0, glowing = 0;
            foreach (var p in Designs(400))
            {
                // One finish for the whole layout
                Assert.Single(p.Highlights.Select(h => h.Glow).Distinct());
                Assert.True(p.Highlights.Where(h => h.Softness > 0).Select(h => h.Softness).Distinct().Count() <= 1);
                foreach (var h in p.Highlights)
                {
                    if (!HighlightStyles.IsSmooth(h.Shape)) Assert.Equal(0.0, h.Softness);
                    if (h.Softness > 0) Assert.InRange(h.Softness, 0.15, 0.40);
                    if (h.Glow > 0) Assert.InRange(h.Glow, 0.20, 0.80);
                }
                bool s = p.Highlights.Any(h => h.Softness > 0), g = p.Highlights[0].Glow > 0;
                if (!s && !g) crisp++;
                if (s) soft++;
                if (g) glowing++;
            }
            // Crisp is the most common finish; soft and glowing both show up often
            Assert.True(crisp > 120 && soft > 60 && glowing > 60, $"crisp {crisp}, soft {soft}, glowing {glowing}");
        }

        [Fact]
        public void SomePresets_HaveSoftOrGlowingHighlights()
        {
            var presets = ColorPresets.All;
            Assert.Contains(presets, x => x.Highlights!.Any(h => h.Glow > 0));
            Assert.Contains(presets, x => x.Highlights!.Any(h => h.Softness > 0));
            Assert.Contains(presets, x => x.Highlights!.All(h => h.Glow == 0 && h.Softness == 0));
            foreach (var h in presets.SelectMany(x => x.Highlights!))
                if (!HighlightStyles.IsSmooth(h.Shape)) Assert.Equal(0.0, h.Softness);
        }

        [Fact]
        public void HighlightStyles_WithoutJitter_AreFixed_AndJitterStaysClose()
        {
            foreach (var style in HighlightStyles.All)
            {
                var fixedOnes = HighlightStyles.Build(style);
                Assert.Equal(fixedOnes.Select(h => (h.PositionX, h.PositionY, h.Size)), HighlightStyles.Build(style).Select(h => (h.PositionX, h.PositionY, h.Size)));
                var moved = HighlightStyles.Build(style, new Random(1), RightEyeFlip.None);
                for (int i = 0; i < fixedOnes.Count; i++)
                {
                    Assert.InRange(moved[i].PositionX - fixedOnes[i].PositionX, -0.031, 0.031);
                    Assert.InRange(moved[i].PositionY - fixedOnes[i].PositionY, -0.031, 0.031);
                    Assert.InRange(moved[i].Size / fixedOnes[i].Size, 0.88, 1.12);
                    Assert.Equal(RightEyeFlip.None, moved[i].RightEyeFlip);
                }
            }
        }

        [Fact]
        public void Themes_IncludeMutedPaleAndDarkColors()
        {
            var lightness = Designs(400).Select(p => Lightness(p.BaseColor)).ToList();
            Assert.Contains(lightness, l => l < 0.33);  // ダーク
            Assert.Contains(lightness, l => l > 0.68);  // パステル
            var names = Designs(400).Select(p => p.Name.Split(" #")[0]).Distinct().ToList();
            Assert.Contains("グレージュ", names);
            Assert.Contains("ショコラブラウン", names);
        }

        [Fact]
        public void GemPupils_SometimesGetAGemColor()
        {
            var gems = Designs(600).Where(p => p.PupilFacetIntensity > 0).ToList();
            Assert.NotEmpty(gems);
            Assert.Contains(gems, p => ColorFill.Parse(p.PupilColor).IsGradient);
            Assert.Contains(gems, p => !ColorFill.Parse(p.PupilColor).IsGradient);
        }

        private static double Lightness(string hex)
        {
            int r = Convert.ToInt32(hex.Substring(1, 2), 16), g = Convert.ToInt32(hex.Substring(3, 2), 16), b = Convert.ToInt32(hex.Substring(5, 2), 16);
            return (Math.Max(r, Math.Max(g, b)) + Math.Min(r, Math.Min(g, b))) / 510.0;
        }
    }
}
