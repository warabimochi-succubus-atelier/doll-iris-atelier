using System;
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

        /// <summary>Pupil shapes and their weights (out of 100).</summary>
        private static readonly (PupilShape Shape, int Weight)[] PupilShapes =
        {
            (PupilShape.Round, 59),
            (PupilShape.CatSlit, 10),
            (PupilShape.Heart, 8),
            (PupilShape.Star5, 5),
            (PupilShape.Star4, 5),
            (PupilShape.Diamond, 4),
            (PupilShape.SakuraPetal, 3),
            (PupilShape.Butterfly, 4),
            (PupilShape.Moon, 2)
        };

        /// <summary>Decorative symbol types (the cross is left out: in an eye it reads as a religious sign).</summary>
        private static readonly SymbolType[] SymbolTypes =
        {
            SymbolType.Star4, SymbolType.Star5, SymbolType.Heart, SymbolType.Moon,
            SymbolType.Diamond, SymbolType.SakuraPetal, SymbolType.Butterfly
        };

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

            RandomizeIrisColors(p, rng, theme, hue, sat, lit, secondHue, paleSat, colorsLocked);
            if (ColorFree(RandomizeLocks.Rays)) p.RayColor = HslToHex(hue + 10.0, paleSat, 0.86);
            if (ColorFree(RandomizeLocks.Particles)) p.ParticleColor = Chance(50) ? "#FFFFFF" : HslToHex(hue, Math.Min(0.4, sat), 0.94);

            // 外輪: dark, in the theme's hue
            if (ColorFree(RandomizeLocks.LimbalRing)) p.LimbalRingColor = HslToHex(hue - 12.0, Math.Min(0.85, sat + 0.10), Range(0.05, 0.14));
            if (!Locked(RandomizeLocks.LimbalRing))
            {
                p.LimbalRingThickness = Range(0.04, 0.15, 3);
                p.LimbalRingOpacity = Range(0.75, 1.0);
                p.LimbalRingSoftness = Range(0.08, 0.33);
            }

            if (!Locked(RandomizeLocks.Pupil)) RandomizePupil(p, rng);
            // The pupil's color after its shape, so a gem pupil can get a gem color
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
            }

            if (!Locked(RandomizeLocks.Rays)) RandomizeRays(p, rng);
            if (!Locked(RandomizeLocks.Particles)) RandomizeGlitter(p, rng);

            if (!Locked(RandomizeLocks.Lens))
            {
                p.LensPatternEnabled = Chance(30);
                p.LensRingCount = rng.Next(4, 11);
                p.LensRingIntensity = Range(0.30, 0.60);
                p.LensRingWidth = Range(0.6, 1.4, 1);
                p.LensRefractionGlow = Range(0.20, 0.55);
                if (!colorsLocked) p.LensRingColor = p.RayColor;
                p.LensRangeAngle = IrisParameters.DefaultRangeAngle;
                p.LensRangeWidth = IrisParameters.FullRangeWidth;
                p.LensRangeFeather = IrisParameters.DefaultRangeFeather;
            }

            if (!Locked(RandomizeLocks.Highlights))
            {
                // 右目の反転 is how the pair is built, not part of the look: the new highlights keep the first one's
                var flip = p.Highlights.FirstOrDefault()?.RightEyeFlip ?? RightEyeFlip.Mirror;
                var style = HighlightStyles.All[rng.Next(HighlightStyles.All.Count)];
                // Finish: crisp 45%, soft (輪郭のぼかし) 25%, glowing (光のにじみ) 20%, both 10%. On a pale iris a
                // glow washes the color out, so it stays faint there
                int finish = rng.Next(100);
                bool soft = finish is >= 45 and < 70 || finish >= 90;
                bool glowing = finish >= 70;
                double softness = soft ? Range(0.15, 0.40) : 0.0;
                double glow = !glowing ? 0.0 : lit > 0.62 ? Range(0.20, 0.45) : Range(0.35, 0.80);
                p.Highlights.Clear();
                foreach (var h in HighlightStyles.Build(style, rng, flip, softness, glow))
                {
                    // A glowing highlight looks more lit with screen
                    h.Blend = Chance(glowing ? 50 : 70) ? LayerBlend.Normal : LayerBlend.Screen;
                    p.Highlights.Add(h);
                }
            }

            if (!Locked(RandomizeLocks.Symbols)) RandomizeSymbols(p, rng, hue, colorsLocked);
            if (!Locked(RandomizeLocks.IrisFacets)) RandomizeIrisFacets(p, rng);

            if (!Locked(RandomizeLocks.OddEye))
            {
                p.IsOddEye = Chance(15);
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

            // 瞳孔まわりの輪 (drawn after everything else, so the earlier draws stay as they were): 25% of designs, in gold
            // or a light color of the theme
            if (!Locked(RandomizeLocks.Pupil))
            {
                p.CollaretteIntensity = Chance(25) ? Range(0.35, 0.75) : 0.0;
                p.CollaretteWidth = Range(0.12, 0.28);
                p.CollaretteJagged = Range(0.3, 0.8);
                p.CollaretteSpikeCount = rng.Next(14, 29);
                p.CollaretteSoftness = Range(0.25, 0.6);
                p.CollaretteBlend = Chance(40) ? LayerBlend.Screen : LayerBlend.Normal;
            }
            if (ColorFree(RandomizeLocks.Pupil))
            {
                p.CollaretteColor = Chance(50)
                    ? HslToHex(Range(38.0, 50.0, 1), 0.85, 0.62)
                    : HslToHex(hue + 6.0, paleSat, Math.Max(0.70, Math.Min(0.85, lit + 0.30)));
            }

            // 色の段 (after everything else, like the ring above): 20% of designs. Light / dark steps that keep each
            // eye's color, or (not with the odd eye, whose other eye has other colors) steps of the theme's color
            if (!colorsLocked)
            {
                p.ToneBandIntensity = Chance(20) ? Range(0.35, 0.70) : 0.0;
                p.ToneBandCount = rng.Next(2, 5);
                p.ToneBandSoftness = Range(0.0, 0.3);
                bool themed = rng.Next(10) < 4 && !p.IsOddEye;
                p.ToneBandInnerColor = themed ? HslToHex(hue + 6.0, sat, Math.Min(0.92, lit + 0.15)) : IrisParameters.DefaultToneBandInnerColor;
                p.ToneBandOuterColor = themed ? HslToHex(hue - 8.0, sat, Math.Max(0.08, lit - 0.20)) : IrisParameters.DefaultToneBandOuterColor;
                p.ToneBandBlend = themed ? LayerBlend.Normal : LayerBlend.SoftLight;
            }

            // 斑点 (after everything else): 20% of designs, dark spots of the theme's hue
            if (!Locked(RandomizeLocks.Spots))
            {
                p.SpotIntensity = Chance(20) ? Range(0.30, 0.60) : 0.0;
                p.SpotCount = rng.Next(8, 41);
                p.SpotSize = Range(0.025, 0.05, 3);
                p.SpotElongation = Range(0.0, 0.7);
                p.SpotSoftness = Range(0.3, 0.7);
                p.SpotRadiusRatio = Range(0.60, 0.85);
                p.SpotBlend = Chance(40) ? LayerBlend.SoftLight : LayerBlend.Normal;
            }
            if (ColorFree(RandomizeLocks.Spots)) p.SpotColor = HslToHex(hue - 15.0, sat * 0.6, Range(0.12, 0.22));

            // 瞳孔の縁取り線 and 外輪の内側の輪 (after everything else)
            if (!Locked(RandomizeLocks.Pupil))
            {
                p.PupilOutlineIntensity = Chance(15) ? Range(0.5, 0.9) : 0.0;
                p.PupilOutlineWidth = Range(0.01, 0.025, 3);
            }
            if (ColorFree(RandomizeLocks.Pupil)) p.PupilOutlineColor = Chance(70) ? HslToHex(hue, paleSat, 0.85) : HslToHex(42.0, 0.85, 0.62);
            if (!Locked(RandomizeLocks.LimbalRing))
            {
                p.LimbalInnerRingIntensity = Chance(20) ? Range(0.3, 0.7) : 0.0;
                p.LimbalInnerRingWidth = Range(0.008, 0.02, 3);
                p.LimbalInnerRingGap = Range(0.0, 0.03, 3);
            }
            if (ColorFree(RandomizeLocks.LimbalRing)) p.LimbalInnerRingColor = Chance(50) ? "#FFFFFF" : HslToHex(hue, paleSat, 0.85);
        }

        /// <summary>
        /// The 「虹彩カラー設定」 box (kept by the 色 lock): the design name, the iris colors and their ranges, the lower
        /// glow, the aurora and nebula layers, the light and the fiber noise.
        /// </summary>
        private static void RandomizeIrisColors(IrisParameters p, Random rng, ColorTheme theme, double hue, double sat,
            double lit, double secondHue, double paleSat, bool colorsLocked)
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

            // 30%: an aurora ring of the theme's hues and one hue picked at random
            bool aurora = Chance(30);
            double extraHue = rng.NextDouble() * 360.0;
            p.AuroraColor = ColorFill.Create(ColorFillKind.Conic, ColorFill.ConicDefaultAngle, new[]
            {
                new ColorStop(HslToHex(hue, 0.85, 0.70), 0.0),
                new ColorStop(HslToHex(twoTone ? secondHue : hue + 60.0, 0.85, 0.72), 0.33),
                new ColorStop(HslToHex(extraHue, 0.80, 0.74), 0.66),
                new ColorStop(HslToHex(hue, 0.85, 0.70), 1.0)
            }).ToString();
            p.AuroraOpacity = aurora ? Range(0.20, 0.45) : 0.0;
            // Gentle blend modes only (覆い焼き / オーバーレイ easily wash out or darken the iris)
            p.AuroraBlend = rng.Next(10) switch { < 4 => LayerBlend.Normal, < 8 => LayerBlend.Screen, _ => LayerBlend.SoftLight };

            // 20%: nebula clouds, from the second hue through the theme's hue to a neighbour
            bool nebula = Chance(20);
            p.NebulaColor = ColorFill.Create(ColorFillKind.Radial, ColorFill.DefaultAngle, new[]
            {
                new ColorStop(HslToHex(secondHue, 0.85, 0.66), 0.0),
                new ColorStop(HslToHex(hue, 0.85, 0.62), 0.55),
                new ColorStop(HslToHex(hue - 30.0, 0.80, 0.58), 1.0)
            }).ToString();
            p.NebulaOpacity = nebula ? Range(0.45, 0.80) : 0.0;
            p.NebulaDensity = Range(0.35, 0.75);
            p.NebulaScale = Range(0.9, 1.9);
            p.NebulaSwirl = Range(-0.7, 0.7);

            // Light: in 6 designs out of 10, from the upper left where the highlights are
            p.LightAngle = Math.Round(Range(200.0, 270.0));
            p.LightStrength = Chance(60) ? Range(0.15, 0.50) : 0.0;

            // Fiber noise: in half the designs, kept subtle
            p.FiberNoise = Chance(50) ? Range(0.15, 0.45) : 0.0;
            p.FiberNoiseScale = Range(0.8, 1.6);
        }

        private static void RandomizePupil(IrisParameters p, Random rng)
        {
            double Range(double min, double max, int digits = 2) => Math.Round(min + rng.NextDouble() * (max - min), digits);

            int roll = rng.Next(100);
            var shape = PupilShape.Round;
            foreach (var (s, weight) in PupilShapes)
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

            // 宝石カット: mostly on the shapes that read as a cut stone
            bool jewel = shape is PupilShape.Heart or PupilShape.Star5 or PupilShape.Star4 or PupilShape.Diamond;
            p.PupilFacetIntensity = rng.Next(100) < (jewel ? 60 : 8) ? Range(0.45, 0.90) : 0.0;
            p.PupilFacetCount = rng.Next(6, 16);
        }

        private static void RandomizeRays(IrisParameters p, Random rng)
        {
            double Range(double min, double max, int digits = 2) => Math.Round(min + rng.NextDouble() * (max - min), digits);

            int[] counts = { 24, 36, 48, 56, 64, 72, 80, 96 };
            p.RayCount = counts[rng.Next(counts.Length)];
            p.RayThickness = Range(0.8, 2.0, 1);
            p.RayLengthRatio = Range(0.55, 0.80);
            p.RayIntensity = Range(0.40, 0.85);
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
        }

        private static void RandomizeGlitter(IrisParameters p, Random rng)
        {
            double Range(double min, double max, int digits = 2) => Math.Round(min + rng.NextDouble() * (max - min), digits);

            // 25% none, 60% a sprinkle, 15% a starry sky over the whole iris
            int roll = rng.Next(100);
            bool starry = roll >= 85;
            p.ParticleCount = roll < 25 ? 0 : starry ? rng.Next(140, 221) : rng.Next(25, 96);
            p.ParticleRadiusRatio = starry ? Range(0.85, 0.95) : Range(0.68, 0.86);
            p.ParticleMinSize = Range(0.8, 1.4, 1);
            p.ParticleMaxSize = Math.Round(p.ParticleMinSize + Range(0.8, 2.0, 1), 1);
            p.ParticleOpacity = Range(0.65, 0.95);
            p.ParticleGlow = Range(0.5, 0.9);
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
        /// 4 designs in 10 get a symbol, a quarter of them a second, smaller one. Symbols sit away from the highlights
        /// (upper right and lower left of the pupil), so they do not run into each other.
        /// </summary>
        private static void RandomizeSymbols(IrisParameters p, Random rng, double hue, bool colorsLocked)
        {
            double Range(double min, double max, int digits = 2) => Math.Round(min + rng.NextDouble() * (max - min), digits);

            p.Symbols.Clear();
            if (rng.Next(10) >= 4) return;

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

        /// <summary>宝石カット面: 15% of designs, a third of them on the upper side only.</summary>
        private static void RandomizeIrisFacets(IrisParameters p, Random rng)
        {
            double Range(double min, double max, int digits = 2) => Math.Round(min + rng.NextDouble() * (max - min), digits);

            bool on = rng.Next(100) < 15;
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
