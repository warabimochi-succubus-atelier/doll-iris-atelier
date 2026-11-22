using System;
using System.Collections.Generic;
using System.Linq;
using DollIrisGenerator.Models;

namespace DollIrisGenerator.Services
{
    /// <summary>「属性ランダム生成」 (SPECIFICATION.md 7.4).</summary>
    public static class IrisRandomizer
    {
        /// <summary>
        /// A color theme: the base color's hue / saturation / lightness and, for two-tone themes, the second hue
        /// (lower glow, aurora, nebula).
        /// </summary>
        private record ColorTheme(string Name, double Hue, double Saturation, double Lightness, double? SecondHue = null);

        private static readonly ColorTheme[] Themes =
        {
            // 宝石 (vivid)
            new("サファイアブルー", 215, 0.90, 0.52),
            new("エメラルドグリーン", 145, 0.85, 0.44),
            new("ルビーレッド", 355, 0.88, 0.48),
            new("アメジストパープル", 275, 0.80, 0.52),
            new("アンバーゴールド", 40, 0.92, 0.50),
            new("アクアマリン", 190, 0.85, 0.55),
            new("ターコイズ", 176, 0.75, 0.45),
            new("ペリドット", 82, 0.72, 0.46),
            new("ローズクォーツ", 335, 0.78, 0.60),
            new("サクラブロッサム", 342, 0.80, 0.64),
            // 2 色 (two-tone)
            new("ギャラクシーネイビー", 230, 0.85, 0.40, 185),
            new("トワイライトサンセット", 265, 0.75, 0.48, 30),
            new("ミントシトロン", 160, 0.75, 0.52, 65),
            new("オーロラシアン", 175, 0.85, 0.50, 310),
            new("ピーチソーダ", 15, 0.85, 0.62, 190),
            new("ラベンダーミスト", 262, 0.55, 0.64, 320),
            // ナチュラル (low saturation)
            new("ハニーヘーゼル", 32, 0.70, 0.42),
            new("ショコラブラウン", 22, 0.55, 0.32),
            new("オリーブヘーゼル", 68, 0.45, 0.40, 30),
            new("グレージュ", 35, 0.18, 0.55),
            new("シルバープラチナ", 210, 0.20, 0.60),
            // パステル (light)
            new("パステルミント", 155, 0.55, 0.72),
            new("ベビーブルー", 205, 0.65, 0.72),
            new("ストロベリーミルク", 345, 0.70, 0.74),
            // ダーク (deep)
            new("ワインレッド", 345, 0.70, 0.30),
            new("ミッドナイトブルー", 225, 0.70, 0.28),
            new("ディープフォレスト", 150, 0.60, 0.28)
        };

        /// <summary>Decorative symbol types (the cross is left out: in an eye it reads as a religious sign).</summary>
        private static readonly SymbolType[] SymbolTypes =
        {
            SymbolType.Star4, SymbolType.Star5, SymbolType.Heart, SymbolType.Moon,
            SymbolType.Diamond, SymbolType.SakuraPetal, SymbolType.Butterfly
        };

        /// <summary>画風: what a random design aims at. It picks the pupil shapes, glitter, highlights and extra effects.</summary>
        public enum Look
        {
            Natural,  // ナチュラル: a real glass / resin eye. Fibers, soft light, the dome's light; few decorations
            Anime,    // アニメ: anime painting. Color steps, outlines, rings, lines from the rim
            Fantasy,  // 幻想: aurora, nebula, holographic sheen, starry glitter and symbols
            Gem       // 宝石: cut pupils, gem facets, the dome's light
        }

        /// <summary>The looks and their weights (out of 100), with their names (SPECIFICATION.md 7.4).</summary>
        public static readonly IReadOnlyList<(Look Look, string Name, int Weight)> Looks = new[]
        {
            (Look.Natural, "ナチュラル", 25),
            (Look.Anime, "アニメ", 30),
            (Look.Fantasy, "幻想", 25),
            (Look.Gem, "宝石", 20)
        };

        /// <summary>The extra effects a design may get on top of its base (iris, rays, glitter, pupil, highlights).</summary>
        public enum Extra
        {
            Collarette,       // 瞳孔まわりの輪
            LimbalInnerRing,  // 外輪の内側の輪
            Caustic,          // 集光
            DomeEdge,         // 縁の屈折
            CenterGlow,       // 中心の光
            ToneBands,        // 色の段
            TwoTone,          // 二色分け
            PupilOutline,     // 瞳孔の縁取り線
            PupilInnerRings,  // 瞳孔の中の輪
            RimRays,          // 外周からの線
            Aurora,           // オーロラ層
            Nebula,           // 星雲
            Holo,             // 偏光の虹色
            IrisFacets,       // 宝石カット面
            BlackGlitter,     // 黒いラメ
            GlitterShapes,    // 粒の形
            Symbols,          // 装飾記号
            OddEye            // オッドアイ
        }

        /// <summary>
        /// Each extra effect: the lock group that keeps it and its chance (percent) in a ナチュラル / アニメ / 幻想 / 宝石
        /// design, before the budget (<see cref="MaxExtras"/>) cuts them down.
        /// </summary>
        public static readonly IReadOnlyDictionary<Extra, (RandomizeLocks Lock, int Natural, int Anime, int Fantasy, int Gem)> Extras =
            new Dictionary<Extra, (RandomizeLocks, int, int, int, int)>
            {
                [Extra.Collarette] = (RandomizeLocks.Pupil, 35, 20, 15, 10),
                [Extra.LimbalInnerRing] = (RandomizeLocks.LimbalRing, 30, 30, 15, 20),
                [Extra.Caustic] = (RandomizeLocks.Lens, 45, 15, 20, 30),
                [Extra.DomeEdge] = (RandomizeLocks.Lens, 35, 10, 15, 40),
                [Extra.CenterGlow] = (RandomizeLocks.Lens, 10, 10, 40, 40),
                [Extra.ToneBands] = (RandomizeLocks.Colors, 0, 40, 10, 0),
                [Extra.TwoTone] = (RandomizeLocks.Colors, 0, 35, 10, 0),
                [Extra.PupilOutline] = (RandomizeLocks.Pupil, 0, 35, 15, 10),
                [Extra.PupilInnerRings] = (RandomizeLocks.Pupil, 0, 35, 15, 15),
                [Extra.RimRays] = (RandomizeLocks.Rays, 15, 40, 10, 5),
                [Extra.Aurora] = (RandomizeLocks.Colors, 0, 10, 45, 15),
                [Extra.Nebula] = (RandomizeLocks.Colors, 0, 0, 35, 0),
                [Extra.Holo] = (RandomizeLocks.Colors, 0, 5, 30, 25),
                [Extra.IrisFacets] = (RandomizeLocks.IrisFacets, 0, 0, 10, 55),
                [Extra.BlackGlitter] = (RandomizeLocks.Particles, 0, 8, 20, 15),
                [Extra.GlitterShapes] = (RandomizeLocks.Particles, 0, 20, 35, 25),
                [Extra.Symbols] = (RandomizeLocks.Symbols, 0, 30, 50, 20),
                [Extra.OddEye] = (RandomizeLocks.OddEye, 5, 15, 25, 15)
            };

        /// <summary>At most this many extra effects in a design (ナチュラル one fewer), so the looks do not pile up.</summary>
        public const int MaxExtras = 4;

        private static int ChanceOf(Extra e, Look look)
        {
            var t = Extras[e];
            return look switch { Look.Natural => t.Natural, Look.Anime => t.Anime, Look.Fantasy => t.Fantasy, _ => t.Gem };
        }

        public static void RandomizeAttributes(IrisParameters p, Random? rng = null) =>
            RandomizeAttributes(p, RandomizeLocks.None, rng);

        /// <summary>
        /// Randomizes the design's attributes, leaving the groups in <paramref name="locks"/> unchanged.
        /// Size, shape, aspect ratio, tilt and outline are always kept, and so are the 右目の反転 settings (how the
        /// pair of eyes is built, not a look). A color is kept when either <see cref="RandomizeLocks.Colors"/> or the
        /// part it belongs to is locked.
        /// </summary>
        public static void RandomizeAttributes(IrisParameters p, RandomizeLocks locks, Random? rng = null)
        {
            rng ??= Random.Shared;
            bool Locked(RandomizeLocks flag) => (locks & flag) != 0;
            bool colorsLocked = Locked(RandomizeLocks.Colors);
            // A part's color changes only when neither the palette nor that part is locked
            bool ColorFree(RandomizeLocks part) => !colorsLocked && !Locked(part);
            double Range(double min, double max, int digits = 2) => Math.Round(min + rng.NextDouble() * (max - min), digits);
            bool Chance(int percent) => rng.Next(100) < percent;

            if (!Locked(RandomizeLocks.Seed)) p.Seed = rng.Next(1, 999999);

            // Palette: always drawn, so a locked palette and a free part still get colors that go together
            var theme = Themes[rng.Next(Themes.Length)];
            double hue = theme.Hue + Range(-8.0, 8.0, 1);
            double sat = Math.Clamp(theme.Saturation + Range(-0.05, 0.05), 0.08, 1.0);
            double lit = Math.Clamp(theme.Lightness + Range(-0.05, 0.05), 0.2, 0.8);
            double secondHue = theme.SecondHue ?? hue + 40.0;
            // Pale colors of the theme stay pale for the grey themes
            double paleSat = Math.Min(0.75, sat + 0.15);

            // 画風: always drawn
            var look = PickLook(rng);

            // The glitter first: the black glitter and the glitter shapes need grains
            if (!Locked(RandomizeLocks.Particles)) RandomizeGlitter(p, rng, look);
            if (ColorFree(RandomizeLocks.Particles)) p.ParticleColor = Chance(50) ? "#FFFFFF" : HslToHex(hue, Math.Min(0.4, sat), 0.94);

            // The extra effects: each one by its look's chance, in a shuffled order, until the budget runs out. A
            // locked effect stays as it is and does not count
            var on = new HashSet<Extra>();
            int budget = (look == Look.Natural ? 1 : 2) + rng.Next(MaxExtras - 1);
            foreach (var e in Enum.GetValues<Extra>().OrderBy(_ => rng.Next()).ToList())
            {
                bool roll = Chance(ChanceOf(e, look));
                bool free = !Locked(Extras[e].Lock)
                    && (e != Extra.BlackGlitter || ColorFree(RandomizeLocks.Particles)) // it changes the grains' color
                    && (e is not (Extra.BlackGlitter or Extra.GlitterShapes) || p.ParticleCount > 0);
                if (roll && free && budget > 0)
                {
                    on.Add(e);
                    budget--;
                }
            }
            // One color split at a time, and none with the odd eye (whose other eye has other colors)
            if (on.Contains(Extra.TwoTone) && (on.Contains(Extra.ToneBands) || on.Contains(Extra.OddEye) || (Locked(RandomizeLocks.OddEye) && p.IsOddEye)))
                on.Remove(Extra.TwoTone);

            RandomizeIrisColors(p, rng, theme, look, hue, sat, lit, secondHue, paleSat, colorsLocked, on);
            if (ColorFree(RandomizeLocks.Rays)) p.RayColor = HslToHex(hue + 10.0, paleSat, 0.86);

            // 外輪: dark, in the theme's hue, and its inner ring
            if (ColorFree(RandomizeLocks.LimbalRing)) p.LimbalRingColor = HslToHex(hue - 12.0, Math.Min(0.85, sat + 0.10), Range(0.05, 0.14));
            if (!Locked(RandomizeLocks.LimbalRing))
            {
                p.LimbalRingThickness = Range(0.04, 0.15, 3);
                p.LimbalRingOpacity = Range(0.75, 1.0);
                p.LimbalRingSoftness = Range(look == Look.Natural ? 0.20 : 0.08, look == Look.Natural ? 0.45 : 0.33);
                p.LimbalInnerRingIntensity = on.Contains(Extra.LimbalInnerRing) ? Range(0.3, 0.7) : 0.0;
                p.LimbalInnerRingWidth = Range(0.008, 0.02, 3);
                p.LimbalInnerRingGap = Range(0.0, 0.03, 3);
            }
            if (ColorFree(RandomizeLocks.LimbalRing)) p.LimbalInnerRingColor = Chance(50) ? "#FFFFFF" : HslToHex(hue, paleSat, 0.85);

            if (!Locked(RandomizeLocks.Pupil)) RandomizePupil(p, rng, look, on);
            // The pupil's colors after its shape, so a gem pupil can get a gem color
            if (ColorFree(RandomizeLocks.Pupil))
            {
                int roll = rng.Next(100);
                bool gem = p.PupilFacetIntensity > 0;
                p.PupilColor = gem && roll < 50
                    // A cut stone in the theme's color (as the preset 特殊: 宝石)
                    ? ColorFill.Create(ColorFillKind.Radial, ColorFill.DefaultAngle, new[]
                    {
                        new ColorStop(HslToHex(hue, paleSat, 0.82), 0.0),
                        new ColorStop(HslToHex(hue, Math.Max(0.5, sat), 0.50), 0.45),
                        new ColorStop(HslToHex(hue - 10.0, Math.Max(0.5, sat), 0.22), 1.0)
                    }).ToString()
                    : roll < 70
                        // A deep pupil that is a little lighter in the middle
                        ? ColorFill.Create(ColorFillKind.Radial, ColorFill.DefaultAngle, new[]
                        {
                            new ColorStop(HslToHex(hue, Math.Min(0.7, sat), 0.20), 0.0),
                            new ColorStop(HslToHex(hue, 0.6, 0.04), 1.0)
                        }).ToString()
                        : HslToHex(hue, 0.60, 0.05);
                // 瞳孔まわりの輪: gold or a light color of the theme; the outline and the inner rings: pale
                p.CollaretteColor = Chance(50)
                    ? HslToHex(Range(38.0, 50.0, 1), 0.85, 0.62)
                    : HslToHex(hue + 6.0, paleSat, Math.Max(0.70, Math.Min(0.85, lit + 0.30)));
                p.PupilOutlineColor = Chance(70) ? HslToHex(hue, paleSat, 0.85) : HslToHex(42.0, 0.85, 0.62);
                p.PupilInnerRingColor = Chance(60) ? "#FFFFFF" : HslToHex(hue, paleSat, 0.85);
                // The other rings: half the time the first one's color, otherwise white or a pale theme color
                string Other() => Chance(50) ? p.PupilInnerRingColor : Chance(50) ? "#FFFFFF" : HslToHex(hue + 20.0, paleSat, 0.85);
                p.PupilInnerRingColor2 = Other();
                p.PupilInnerRingColor3 = Other();
            }

            if (!Locked(RandomizeLocks.Rays)) RandomizeRays(p, rng, look, on);
            if (ColorFree(RandomizeLocks.Rays)) p.RimRayColor = HslToHex(hue - 10.0, sat, 0.12);

            // ラメ: the grain shapes and the black glitter (with a light halo; blended normally, as under スクリーン black
            // grains would vanish)
            if (!Locked(RandomizeLocks.Particles))
            {
                GlitterShape[] shapes = { GlitterShape.Star4, GlitterShape.Star5, GlitterShape.Heart, GlitterShape.Diamond, GlitterShape.SakuraPetal };
                bool shaped = on.Contains(Extra.GlitterShapes);
                var shape = shapes[rng.Next(shapes.Length)];
                double ratio = Range(0.3, 1.0);
                p.ParticleShape = shaped ? shape : GlitterShape.Mixed;
                p.ParticleShapeRatio = shaped ? ratio : 1.0;

                bool black = on.Contains(Extra.BlackGlitter);
                p.ParticleGlowCustomColor = black;
                if (black)
                {
                    p.ParticleGlow = Range(0.6, 0.9);
                    p.GlitterBlend = LayerBlend.Normal;
                }
            }
            if (ColorFree(RandomizeLocks.Particles))
            {
                p.ParticleGlowColor = Chance(50) ? IrisParameters.DefaultParticleGlowColor : HslToHex(hue, paleSat, 0.88);
                if (on.Contains(Extra.BlackGlitter)) p.ParticleColor = HslToHex(hue, sat * 0.5, 0.06);
            }

            // レンズの効果
            if (!Locked(RandomizeLocks.Lens))
            {
                p.CenterGlowIntensity = on.Contains(Extra.CenterGlow) ? Range(0.20, 0.55) : 0.0;
                p.CausticIntensity = on.Contains(Extra.Caustic) ? Range(0.35, 0.70) : 0.0;
                p.CausticOffset = Range(0.35, 0.60);
                p.CausticSize = Range(0.30, 0.50);
                p.CausticShape = Chance(60) ? CausticShape.Crescent : CausticShape.Round;
                p.CausticBlend = LayerBlend.Screen;
                p.DomeEdgeIntensity = on.Contains(Extra.DomeEdge) ? Range(0.30, 0.60) : 0.0;
                p.DomeEdgeWidth = Range(0.08, 0.18);
                p.DomeEdgeBias = Range(0.5, 0.9);
            }
            if (ColorFree(RandomizeLocks.Lens))
            {
                string pale = HslToHex(hue, paleSat, 0.88);
                p.CenterGlowColor = GeometryHelper.SanitizeColorHex(p.RayColor, IrisParameters.DefaultCenterGlowColor);
                p.CausticColor = Chance(50) ? "#FFFFFF" : pale;
                p.DomeEdgeColor = Chance(60) ? "#FFFFFF" : pale;
            }

            if (!Locked(RandomizeLocks.Highlights)) RandomizeHighlights(p, rng, look, lit);
            if (!Locked(RandomizeLocks.Symbols)) RandomizeSymbols(p, rng, hue, colorsLocked, on.Contains(Extra.Symbols));
            if (!Locked(RandomizeLocks.IrisFacets)) RandomizeIrisFacets(p, rng, on.Contains(Extra.IrisFacets));

            if (!Locked(RandomizeLocks.OddEye))
            {
                p.IsOddEye = on.Contains(Extra.OddEye);
                p.OddEyeSeedOffset = rng.Next(500, 5000);
                if (!colorsLocked)
                {
                    // The other eye: another theme (never the same one)
                    var odd = Themes[(Array.IndexOf(Themes, theme) + 1 + rng.Next(Themes.Length - 1)) % Themes.Length];
                    double oddHue = odd.Hue + Range(-8.0, 8.0, 1);
                    double oddPale = Math.Min(0.75, odd.Saturation + 0.15);
                    p.OddEyeBaseColor = HslToHex(oddHue, odd.Saturation, odd.Lightness);
                    p.OddEyeInnerColor = HslToHex(oddHue + 8.0, Math.Max(0.1, odd.Saturation - 0.10), Math.Min(0.92, odd.Lightness + 0.24));
                    p.OddEyeOuterColor = HslToHex(oddHue - 8.0, Math.Min(1.0, odd.Saturation + 0.08), Math.Max(0.12, odd.Lightness - 0.26));
                    p.OddEyeRayColor = HslToHex(oddHue + 10.0, oddPale, 0.86);
                    p.OddEyeBottomHighlightColor = HslToHex(odd.SecondHue ?? oddHue + 12.0, oddPale, 0.86);
                }
            }

            // The color layers of the 「虹彩カラー設定」 box that look at the odd eye (色の段 keeps each eye's colors then)
            if (!colorsLocked)
            {
                p.ToneBandIntensity = on.Contains(Extra.ToneBands) ? Range(0.35, 0.70) : 0.0;
                p.ToneBandCount = rng.Next(2, 5);
                p.ToneBandSoftness = Range(0.0, 0.3);
                bool themed = rng.Next(10) < 4 && !p.IsOddEye;
                p.ToneBandInnerColor = themed ? HslToHex(hue + 6.0, sat, Math.Min(0.92, lit + 0.15)) : IrisParameters.DefaultToneBandInnerColor;
                p.ToneBandOuterColor = themed ? HslToHex(hue - 8.0, sat, Math.Max(0.08, lit - 0.20)) : IrisParameters.DefaultToneBandOuterColor;
                p.ToneBandBlend = themed ? LayerBlend.Normal : LayerBlend.SoftLight;
            }
        }

        private static Look PickLook(Random rng)
        {
            int roll = rng.Next(100);
            foreach (var (look, _, weight) in Looks)
            {
                if (roll < weight) return look;
                roll -= weight;
            }
            return Look.Natural;
        }

        /// <summary>The highlight layouts each look picks from (SPECIFICATION.md 7.4); together they use every layout.</summary>
        public static HighlightStyle[] HighlightStylesOf(Look look) => look switch
        {
            Look.Natural => new[] { HighlightStyle.Classic, HighlightStyle.Window, HighlightStyle.Droplet, HighlightStyle.Arc },
            Look.Anime => new[] { HighlightStyle.Classic, HighlightStyle.ThreeDots, HighlightStyle.Arc, HighlightStyle.Crescent, HighlightStyle.Heart },
            Look.Fantasy => new[] { HighlightStyle.Sparkle, HighlightStyle.Heart, HighlightStyle.Crescent, HighlightStyle.Window, HighlightStyle.ThreeDots },
            _ => new[] { HighlightStyle.Diamond, HighlightStyle.Sparkle, HighlightStyle.Classic }
        };

        private static void RandomizeHighlights(IrisParameters p, Random rng, Look look, double lit)
        {
            double Range(double min, double max, int digits = 2) => Math.Round(min + rng.NextDouble() * (max - min), digits);

            // 右目の反転 is how the pair is built, not part of the look: the new highlights keep the first one's
            var flip = p.Highlights.FirstOrDefault()?.RightEyeFlip ?? RightEyeFlip.Mirror;
            var styles = HighlightStylesOf(look);
            var style = styles[rng.Next(styles.Length)];
            // Finish (crisp / soft / glowing / both), out of 100 by look. On a pale iris a glow washes the color out,
            // so it stays faint there
            var (crisp, soft, glow) = look switch
            {
                Look.Natural => (30, 50, 10),
                Look.Anime => (60, 15, 20),
                Look.Fantasy => (30, 15, 40),
                _ => (50, 10, 30)
            };
            int finish = rng.Next(100);
            bool isSoft = finish >= crisp && finish < crisp + soft || finish >= crisp + soft + glow;
            bool isGlowing = finish >= crisp + soft;
            double softness = isSoft ? Range(0.15, 0.40) : 0.0;
            double glowAmount = !isGlowing ? 0.0 : lit > 0.62 ? Range(0.20, 0.45) : Range(0.35, 0.80);
            p.Highlights.Clear();
            foreach (var h in HighlightStyles.Build(style, rng, flip, softness, glowAmount))
            {
                // A glowing highlight looks more lit with screen
                h.Blend = rng.Next(100) < (isGlowing ? 50 : 70) ? LayerBlend.Normal : LayerBlend.Screen;
                p.Highlights.Add(h);
            }
        }

        /// <summary>
        /// The 「虹彩カラー設定」 box (kept by the 色 lock): the design name, the iris colors and their ranges, the lower
        /// glow and where it starts, the eyelid shadow, the aurora and nebula layers, the two-tone split, the holographic
        /// sheen, the light and the fiber noise. (The tone bands, which look at the odd eye, come later.)
        /// </summary>
        private static void RandomizeIrisColors(IrisParameters p, Random rng, ColorTheme theme, Look look, double hue, double sat,
            double lit, double secondHue, double paleSat, bool colorsLocked, HashSet<Extra> on)
        {
            if (colorsLocked) return;
            double Range(double min, double max, int digits = 2) => Math.Round(min + rng.NextDouble() * (max - min), digits);
            bool Chance(int percent) => rng.Next(100) < percent;
            bool twoTone = theme.SecondHue != null;

            p.Name = $"{theme.Name} #{rng.Next(100, 1000)}";
            p.BaseColor = HslToHex(hue, sat, lit);
            p.InnerColor = HslToHex(hue + (twoTone ? 20.0 : 8.0), Math.Max(0.1, sat - 0.10), Math.Min(0.92, lit + 0.26));
            p.OuterColor = HslToHex(hue - 8.0, Math.Min(1.0, sat + 0.08), Math.Max(0.10, lit - 0.28));
            p.BottomHighlightColor = twoTone ? HslToHex(secondHue, paleSat, 0.80) : HslToHex(hue + 12.0, paleSat, 0.88);
            p.InnerGradientRadius = Range(0.35, 0.70);
            p.OuterGradientRadius = Range(0.80, 1.10);
            p.BottomHighlightIntensity = Range(0.25, 0.80);
            // 下部反射光の始まる高さ: mostly the usual 45%, sometimes higher or only at the bottom
            bool moved = Chance(30);
            double start = Range(0.30, 0.70);
            p.BottomHighlightStart = moved ? start : IrisParameters.DefaultBottomHighlightStart;

            // まぶたの影: mostly the usual shadow, sometimes light or deep, sometimes tinted
            int shadowRoll = rng.Next(100);
            double strength = rng.NextDouble(), depth = rng.NextDouble();
            bool tinted = Chance(30);
            p.UpperShadowIntensity = shadowRoll < 70 ? IrisParameters.DefaultUpperShadowIntensity
                : shadowRoll < 85 ? Math.Round(0.25 + strength * 0.25, 2) : Math.Round(0.70 + strength * 0.15, 2);
            p.UpperShadowDepth = shadowRoll < 70 ? IrisParameters.DefaultUpperShadowDepth
                : shadowRoll < 85 ? Math.Round(0.45 + depth * 0.15, 2) : Math.Round(0.70 + depth * 0.15, 2);
            p.UpperShadowColor = tinted ? HslToHex(hue, sat, 0.10) : IrisParameters.DefaultUpperShadowColor;

            // オーロラ層: a ring of the theme's hues and one hue picked at random
            double extraHue = rng.NextDouble() * 360.0;
            p.AuroraColor = ColorFill.Create(ColorFillKind.Conic, ColorFill.ConicDefaultAngle, new[]
            {
                new ColorStop(HslToHex(hue, 0.85, 0.70), 0.0),
                new ColorStop(HslToHex(twoTone ? secondHue : hue + 60.0, 0.85, 0.72), 0.33),
                new ColorStop(HslToHex(extraHue, 0.80, 0.74), 0.66),
                new ColorStop(HslToHex(hue, 0.85, 0.70), 1.0)
            }).ToString();
            p.AuroraOpacity = on.Contains(Extra.Aurora) ? Range(0.20, 0.45) : 0.0;
            // Gentle blend modes only (覆い焼き / オーバーレイ easily wash out or darken the iris)
            p.AuroraBlend = rng.Next(10) switch { < 4 => LayerBlend.Normal, < 8 => LayerBlend.Screen, _ => LayerBlend.SoftLight };

            // 星雲: clouds from the second hue through the theme's hue to a neighbour
            p.NebulaColor = ColorFill.Create(ColorFillKind.Radial, ColorFill.DefaultAngle, new[]
            {
                new ColorStop(HslToHex(secondHue, 0.85, 0.66), 0.0),
                new ColorStop(HslToHex(hue, 0.85, 0.62), 0.55),
                new ColorStop(HslToHex(hue - 30.0, 0.80, 0.58), 1.0)
            }).ToString();
            p.NebulaOpacity = on.Contains(Extra.Nebula) ? Range(0.45, 0.80) : 0.0;
            p.NebulaDensity = Range(0.35, 0.75);
            p.NebulaScale = Range(0.9, 1.9);
            p.NebulaSwirl = Range(-0.7, 0.7);

            // 二色分け: a lighter lower side
            p.TwoToneIntensity = on.Contains(Extra.TwoTone) ? Range(0.50, 0.85) : 0.0;
            p.TwoToneColor = HslToHex(hue + 8.0, paleSat, Math.Min(0.85, lit + 0.25));
            p.TwoToneHeight = Range(0.25, 0.45);
            p.TwoToneCurve = Range(0.10, 0.40);
            p.TwoToneSoftness = Range(0.0, 0.40);
            p.TwoToneAngle = IrisParameters.DefaultTwoToneAngle;
            p.TwoToneBlend = LayerBlend.Normal;

            // 偏光の虹色: sometimes on the lower half only
            p.HoloIntensity = on.Contains(Extra.Holo) ? Range(0.20, 0.45) : 0.0;
            p.HoloScale = Range(2.0, 5.0, 1);
            p.HoloShape = Chance(40) ? HoloShape.Concentric : HoloShape.Linear;
            int blendRoll = rng.Next(10);
            p.HoloBlend = blendRoll < 5 ? LayerBlend.Screen : blendRoll < 8 ? LayerBlend.SoftLight : LayerBlend.Overlay;
            PatternRange.Apply(p, PatternLayer.Holo, Chance(30) ? PatternRange.Styles[1] : PatternRange.Styles[0]);

            // Light: from the upper left where the highlights are; a natural eye is lit more often
            p.LightAngle = Math.Round(Range(200.0, 270.0));
            p.LightStrength = Chance(look == Look.Natural ? 80 : 50) ? Range(0.15, 0.50) : 0.0;

            // Fiber noise: the natural look's fibers, a little elsewhere
            var (fiberChance, fiberMin, fiberMax) = look == Look.Natural ? (90, 0.25, 0.50) : (25, 0.10, 0.30);
            p.FiberNoise = Chance(fiberChance) ? Range(fiberMin, fiberMax) : 0.0;
            p.FiberNoiseScale = Range(0.8, 1.6);
        }

        /// <summary>Pupil shapes and their weights (out of 100) in each look.</summary>
        public static (PupilShape Shape, int Weight)[] PupilShapesOf(Look look) => look switch
        {
            Look.Natural => new[] { (PupilShape.Round, 88), (PupilShape.CatSlit, 12) },
            Look.Anime => new[]
            {
                (PupilShape.Round, 55), (PupilShape.CatSlit, 10), (PupilShape.Heart, 10), (PupilShape.Star5, 6), (PupilShape.Star4, 6),
                (PupilShape.Butterfly, 5), (PupilShape.Moon, 4), (PupilShape.SakuraPetal, 4)
            },
            Look.Fantasy => new[]
            {
                (PupilShape.Round, 45), (PupilShape.Star5, 10), (PupilShape.Star4, 10), (PupilShape.Heart, 8), (PupilShape.Butterfly, 9),
                (PupilShape.Moon, 7), (PupilShape.SakuraPetal, 7), (PupilShape.CatSlit, 4)
            },
            _ => new[]
            {
                (PupilShape.Round, 25), (PupilShape.Diamond, 25), (PupilShape.Heart, 18), (PupilShape.Star5, 13), (PupilShape.Star4, 14),
                (PupilShape.CatSlit, 5)
            }
        };

        /// <summary>The pupil (kept by the 瞳孔 lock) with its 瞳孔まわりの輪, 縁取り線 and 中の輪.</summary>
        private static void RandomizePupil(IrisParameters p, Random rng, Look look, HashSet<Extra> on)
        {
            double Range(double min, double max, int digits = 2) => Math.Round(min + rng.NextDouble() * (max - min), digits);

            int roll = rng.Next(100);
            var shape = PupilShape.Round;
            foreach (var (s, weight) in PupilShapesOf(look))
            {
                if (roll < weight) { shape = s; break; }
                roll -= weight;
            }
            p.PupilShape = shape;

            // A round pupil is sometimes a tall oval (左右 75%・上下 125%)
            bool oval = shape == PupilShape.Round && rng.Next(100) < 15;
            p.PupilStretchLeft = p.PupilStretchRight = oval ? 0.75 : 1.0;
            p.PupilStretchTop = p.PupilStretchBottom = oval ? 1.25 : 1.0;

            // Shapes with a thin outline need more room to read
            bool figure = shape is not (PupilShape.Round or PupilShape.CatSlit);
            p.PupilSizeRatio = figure ? Range(0.34, 0.46) : Range(0.24, 0.42);
            p.PupilOpacity = Range(0.92, 1.0);
            p.PupilSoftness = Range(0.03, 0.15);
            p.PupilCornerRounding = Range(0.0, 0.5);
            p.PupilStarCoreScale = Range(0.85, 1.15);

            // 宝石カット: mostly on the shapes that read as a cut stone, and on most of the 宝石 look's pupils
            bool jewel = shape is PupilShape.Heart or PupilShape.Star5 or PupilShape.Star4 or PupilShape.Diamond;
            int facetChance = look == Look.Gem ? (jewel ? 85 : 30) : jewel ? 60 : 8;
            p.PupilFacetIntensity = rng.Next(100) < facetChance ? Range(0.45, 0.90) : 0.0;
            p.PupilFacetCount = rng.Next(6, 16);

            // 瞳孔まわりの輪
            p.CollaretteIntensity = on.Contains(Extra.Collarette) ? Range(0.35, 0.75) : 0.0;
            p.CollaretteWidth = Range(0.12, 0.28);
            p.CollaretteJagged = Range(0.3, 0.8);
            p.CollaretteSpikeCount = rng.Next(14, 29);
            p.CollaretteSoftness = Range(0.25, 0.6);
            p.CollaretteBlend = rng.Next(100) < 40 ? LayerBlend.Screen : LayerBlend.Normal;

            // 縁取り線
            p.PupilOutlineIntensity = on.Contains(Extra.PupilOutline) ? Range(0.5, 0.9) : 0.0;
            p.PupilOutlineWidth = Range(0.01, 0.025, 3);

            // 中の輪: the first one, and 35% of those a second (30% of which a third), smaller and not evenly spaced
            bool first = on.Contains(Extra.PupilInnerRings);
            bool second = first && rng.Next(100) < 35;
            bool third = second && rng.Next(100) < 30;
            p.PupilInnerRingIntensity = first ? Range(0.5, 0.9) : 0.0;
            p.PupilInnerRingSize = Range(0.45, 0.75);
            p.PupilInnerRingWidth = Range(0.008, 0.02, 3);
            double f2 = Range(0.45, 0.75), f3 = Range(0.4, 0.7);
            p.PupilInnerRingSize2 = Math.Max(IrisParameters.MinPupilInnerRingSize, Math.Round(p.PupilInnerRingSize * f2, 2));
            p.PupilInnerRingSize3 = Math.Max(IrisParameters.MinPupilInnerRingSize, Math.Round(p.PupilInnerRingSize2 * f3, 2));
            // A third ring only when it fits inside the second
            if (p.PupilInnerRingSize3 >= p.PupilInnerRingSize2) third = false;
            p.PupilInnerRingIntensity2 = second ? Range(0.4, 0.9) : 0.0;
            p.PupilInnerRingIntensity3 = third ? Range(0.3, 0.8) : 0.0;
            p.PupilInnerRingWidth2 = Range(0.006, 0.018, 3);
            p.PupilInnerRingWidth3 = Range(0.005, 0.015, 3);
        }

        /// <summary>The rays (kept by the 放射模様 lock) and the 外周からの線.</summary>
        private static void RandomizeRays(IrisParameters p, Random rng, Look look, HashSet<Extra> on)
        {
            double Range(double min, double max, int digits = 2) => Math.Round(min + rng.NextDouble() * (max - min), digits);

            // ナチュラル: many fine, faint fibers; アニメ: fewer, bolder lines
            int[] counts = look switch
            {
                Look.Natural => new[] { 64, 72, 80, 96 },
                Look.Anime => new[] { 24, 36, 48, 56, 64 },
                _ => new[] { 24, 36, 48, 56, 64, 72, 80, 96 }
            };
            p.RayCount = counts[rng.Next(counts.Length)];
            p.RayThickness = look == Look.Natural ? Range(0.8, 1.2, 1) : Range(0.8, 2.0, 1);
            p.RayLengthRatio = Range(0.55, 0.80);
            p.RayIntensity = look switch { Look.Natural => Range(0.30, 0.60), Look.Anime => Range(0.50, 0.85), _ => Range(0.40, 0.85) };
            p.RayRandomness = Range(0.25, 0.60);
            p.RayInnerRadiusRatio = Range(0.26, 0.40);
            p.RayTaper = Range(0.5, 0.9);
            p.RayWidthVariation = Range(0.3, 0.7);
            p.RayCurve = Range(-0.35, 0.35); // a gentle swirl, either way
            p.RayFineLayer = Range(0.4, 0.8);

            // 見える範囲: 2 designs in 10 have the rays on the lower side only
            bool lower = rng.Next(10) < 2;
            p.RayRangeAngle = IrisParameters.DefaultRangeAngle;
            p.RayRangeWidth = lower ? Math.Round(Range(160.0, 220.0)) : IrisParameters.FullRangeWidth;
            p.RayRangeFeather = lower ? Math.Round(Range(30.0, 60.0)) : IrisParameters.DefaultRangeFeather;

            // 外周からの線: dark lines of the theme's hue, mostly all round, sometimes the upper or the lower half
            p.RimRayIntensity = on.Contains(Extra.RimRays) ? Range(0.35, 0.70) : 0.0;
            p.RimRayCount = rng.Next(24, 73);
            p.RimRayLength = Range(0.12, 0.30);
            p.RimRayWidth = Range(0.8, 1.6, 1);
            p.RimRayRandomness = Range(0.3, 0.6);
            int rangeRoll = rng.Next(10);
            PatternRange.Apply(p, PatternLayer.RimRays, rangeRoll < 7 ? PatternRange.Styles[0] : rangeRoll < 9 ? PatternRange.Styles[3] : PatternRange.Styles[1]);
        }

        /// <summary>The glitter (kept by the ラメ lock), by look: none, a sprinkle or a starry sky.</summary>
        private static void RandomizeGlitter(IrisParameters p, Random rng, Look look)
        {
            double Range(double min, double max, int digits = 2) => Math.Round(min + rng.NextDouble() * (max - min), digits);

            // Out of 100: none / sprinkle / starry
            var (none, sprinkle) = look switch
            {
                Look.Natural => (45, 55),
                Look.Anime => (25, 75),
                Look.Fantasy => (5, 45),
                _ => (15, 65)
            };
            int roll = rng.Next(100);
            bool starry = roll >= none + sprinkle;
            p.ParticleCount = roll < none ? 0
                : starry ? rng.Next(140, 221)
                : look == Look.Natural ? rng.Next(15, 51) : rng.Next(25, 96);
            p.ParticleRadiusRatio = starry ? Range(0.85, 0.95) : Range(0.68, 0.86);
            p.ParticleMinSize = Range(0.8, 1.4, 1);
            p.ParticleMaxSize = Math.Round(p.ParticleMinSize + Range(0.8, 2.0, 1), 1);
            p.ParticleOpacity = Range(0.65, 0.95);
            p.ParticleGlow = look == Look.Natural ? Range(0.3, 0.6) : Range(0.5, 0.9);
            p.ParticleFlareRatio = Range(0.08, 0.25);
            p.ParticleFlareLength = Range(3.0, 5.5, 1);
            p.GlitterBlend = rng.Next(10) switch { < 5 => LayerBlend.Normal, < 9 => LayerBlend.Screen, _ => LayerBlend.ColorDodge };

            // 見える範囲: 1 design in 10 has the glitter on the lower side only
            bool lower = rng.Next(10) < 1;
            p.ParticleRangeAngle = IrisParameters.DefaultRangeAngle;
            p.ParticleRangeWidth = lower ? Math.Round(Range(200.0, 260.0)) : IrisParameters.FullRangeWidth;
            p.ParticleRangeFeather = lower ? Math.Round(Range(40.0, 70.0)) : IrisParameters.DefaultRangeFeather;
        }

        /// <summary>
        /// The symbols (when <paramref name="on"/>): one, and a quarter of the time a second, smaller one. Symbols sit
        /// away from the highlights (upper right and lower left of the pupil), so they do not run into each other.
        /// </summary>
        private static void RandomizeSymbols(IrisParameters p, Random rng, double hue, bool colorsLocked, bool on)
        {
            double Range(double min, double max, int digits = 2) => Math.Round(min + rng.NextDouble() * (max - min), digits);

            p.Symbols.Clear();
            if (!on) return;

            // The symbols' color: the glitter's (always, when the palette is locked), a pale tint of the theme or a
            // pale complementary accent
            int colorRoll = rng.Next(10);
            string color = colorsLocked || colorRoll < 5 ? p.ParticleColor
                : colorRoll < 8 ? HslToHex(hue + 30.0, 0.70, 0.88)
                : HslToHex(hue + 180.0, 0.60, 0.85);

            bool upperRight = rng.Next(2) == 0;
            int count = rng.Next(10) < 7 ? 1 : rng.Next(2, 4);
            var first = SymbolTypes[rng.Next(SymbolTypes.Length)];
            p.Symbols.Add(NewSymbol(first, upperRight, count > 1 ? Range(0.10, 0.14) : Range(0.13, 0.20), count));

            if (rng.Next(4) == 0)
            {
                var second = SymbolTypes[(Array.IndexOf(SymbolTypes, first) + 1 + rng.Next(SymbolTypes.Length - 1)) % SymbolTypes.Length];
                p.Symbols.Add(NewSymbol(second, !upperRight, Range(0.08, 0.12), 1));
            }

            DecorativeSymbolItem NewSymbol(SymbolType type, bool upper, double size, int copies)
            {
                // Upper right: 290–340°, lower left: 110–160° (0° = right, 90° = down), 0.48–0.62 of the radius out
                double angle = (upper ? Range(290.0, 340.0) : Range(110.0, 160.0)) * Math.PI / 180.0;
                double radius = Range(0.48, 0.62);
                return new DecorativeSymbolItem
                {
                    Type = type,
                    Count = copies,
                    Size = size,
                    PositionX = Math.Round(Math.Cos(angle) * radius, 2),
                    PositionY = Math.Round(Math.Sin(angle) * radius, 2),
                    Color = color,
                    Opacity = Range(0.75, 0.95),
                    Rotation = Range(-25.0, 25.0, 1),
                    Blend = rng.Next(10) < 6 ? LayerBlend.Normal : LayerBlend.Screen,
                    Enabled = true
                };
            }
        }

        /// <summary>宝石カット面 (when <paramref name="on"/>), a third of them on the upper side only.</summary>
        private static void RandomizeIrisFacets(IrisParameters p, Random rng, bool on)
        {
            double Range(double min, double max, int digits = 2) => Math.Round(min + rng.NextDouble() * (max - min), digits);

            int[] counts = { 6, 8, 8, 10, 12 };
            bool upper = rng.Next(10) < 3;
            p.IrisFacetIntensity = on ? Range(0.35, 0.70) : 0.0;
            p.IrisFacetCount = counts[rng.Next(counts.Length)];
            p.IrisFacetTableSize = Range(0.35, 0.55);
            p.IrisFacetShadeAngle = Math.Round(Range(225.0, 315.0)); // the dark side somewhere above
            p.IrisFacetShadeBias = Range(0.4, 0.8);
            p.IrisFacetBlend = rng.Next(10) < 3 ? LayerBlend.SoftLight : LayerBlend.Overlay;
            p.IrisFacetRangeAngle = upper ? 270.0 : IrisParameters.DefaultRangeAngle;
            p.IrisFacetRangeWidth = upper ? 200.0 : IrisParameters.FullRangeWidth;
            p.IrisFacetRangeFeather = upper ? 60.0 : IrisParameters.DefaultRangeFeather;
        }

        public static string HslToHex(double h, double s, double l)
        {
            h = (h % 360.0 + 360.0) % 360.0;
            s = Math.Clamp(s, 0.0, 1.0);
            l = Math.Clamp(l, 0.0, 1.0);

            double c = (1.0 - Math.Abs(2.0 * l - 1.0)) * s;
            double x = c * (1.0 - Math.Abs((h / 60.0) % 2.0 - 1.0));
            double m = l - c / 2.0;

            double r1 = 0, g1 = 0, b1 = 0;
            if (h < 60) { r1 = c; g1 = x; b1 = 0; }
            else if (h < 120) { r1 = x; g1 = c; b1 = 0; }
            else if (h < 180) { r1 = 0; g1 = c; b1 = x; }
            else if (h < 240) { r1 = 0; g1 = x; b1 = c; }
            else if (h < 300) { r1 = x; g1 = 0; b1 = c; }
            else { r1 = c; g1 = 0; b1 = x; }

            byte r = (byte)Math.Clamp((int)Math.Round((r1 + m) * 255.0), 0, 255);
            byte g = (byte)Math.Clamp((int)Math.Round((g1 + m) * 255.0), 0, 255);
            byte b = (byte)Math.Clamp((int)Math.Round((b1 + m) * 255.0), 0, 255);

            return $"#{r:X2}{g:X2}{b:X2}";
        }
    }
}
