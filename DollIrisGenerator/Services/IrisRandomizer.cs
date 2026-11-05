using System;
using System.Collections.Generic;
using DollIrisGenerator.Models;

namespace DollIrisGenerator.Services
{
    public static class IrisRandomizer
    {
        private record ColorTheme(string Name, double Hue, double Saturation, double Lightness, bool DualTone = false, double DualHue = 0);

        private static readonly ColorTheme[] Themes = new[]
        {
            new ColorTheme("サファイアブルー", 215, 0.90, 0.52),
            new ColorTheme("エメラルドグリーン", 145, 0.88, 0.46),
            new ColorTheme("ルビーレッド", 355, 0.92, 0.48),
            new ColorTheme("アメジストパープル", 275, 0.88, 0.52),
            new ColorTheme("アンバーゴールド", 42, 0.95, 0.50),
            new ColorTheme("アクアマリン", 192, 0.90, 0.56),
            new ColorTheme("ローズクォーツ", 335, 0.85, 0.58),
            new ColorTheme("ギャラクシーネイビー", 230, 0.92, 0.42, true, 185),
            new ColorTheme("トワイライトサンセット", 265, 0.85, 0.48, true, 35),
            new ColorTheme("ミントシトロン", 160, 0.82, 0.52, true, 65),
            new ColorTheme("シルバープラチナ", 210, 0.20, 0.60),
            new ColorTheme("フォレストオリーブ", 105, 0.75, 0.42),
            new ColorTheme("オーロラシアン", 175, 0.95, 0.50, true, 310),
            new ColorTheme("ハニーヘーゼル", 32, 0.82, 0.44),
            new ColorTheme("サクラブロッサム", 342, 0.86, 0.62)
        };

        public static void RandomizeAttributes(IrisParameters p, Random? rng = null)
        {
            rng ??= Random.Shared;

            // 1. Strictly capture and preserve size and dimensions
            double preservedSizeMm = p.SizeMm;
            IrisShape preservedShape = p.Shape;
            double preservedAspectRatio = p.AspectRatio;
            double preservedTiltAngle = p.TiltAngle;

            // 2. Random Seed
            p.Seed = rng.Next(1, 999999);

            // 3. Harmonious Color Palette
            var theme = Themes[rng.Next(Themes.Length)];
            p.Name = $"{theme.Name} #{rng.Next(100, 999)}";

            double baseHue = theme.Hue + (rng.NextDouble() * 16.0 - 8.0);
            double baseSat = Math.Clamp(theme.Saturation + (rng.NextDouble() * 0.10 - 0.05), 0.1, 1.0);
            double baseLit = Math.Clamp(theme.Lightness + (rng.NextDouble() * 0.10 - 0.05), 0.2, 0.8);

            p.BaseColor = HslToHex(baseHue, baseSat, baseLit);
            p.InnerColor = HslToHex(baseHue + (theme.DualTone ? 20.0 : 8.0), Math.Max(0.1, baseSat - 0.10), Math.Min(0.92, baseLit + 0.26));
            p.OuterColor = HslToHex(baseHue - 8.0, Math.Min(1.0, baseSat + 0.08), Math.Max(0.12, baseLit - 0.28));

            if (theme.DualTone)
            {
                p.BottomHighlightColor = HslToHex(theme.DualHue, 0.85, 0.78);
            }
            else
            {
                p.BottomHighlightColor = HslToHex(baseHue + 12.0, 0.75, 0.88);
            }

            p.LimbalRingColor = HslToHex(baseHue - 12.0, Math.Min(0.85, baseSat + 0.10), 0.08);
            p.PupilColor = HslToHex(baseHue, 0.60, 0.05);
            p.RayColor = HslToHex(baseHue + 10.0, 0.70, 0.86);
            p.ParticleColor = rng.Next(2) == 0 ? "#FFFFFF" : HslToHex(baseHue, 0.40, 0.94);

            // 4. Gradient Radii & Glow
            p.InnerGradientRadius = Math.Round(0.35 + rng.NextDouble() * 0.35, 2);
            p.OuterGradientRadius = Math.Round(0.80 + rng.NextDouble() * 0.30, 2);
            p.BottomHighlightIntensity = Math.Round(0.25 + rng.NextDouble() * 0.55, 2);

            // 5. Limbal Ring (外輪)
            p.LimbalRingThickness = Math.Round(0.04 + rng.NextDouble() * 0.11, 3);
            p.LimbalRingOpacity = Math.Round(0.75 + rng.NextDouble() * 0.25, 2);
            p.LimbalRingSoftness = Math.Round(0.08 + rng.NextDouble() * 0.25, 2);

            // 6. Pupil (瞳孔)
            int pupilRoll = rng.Next(10);
            p.PupilShape = pupilRoll switch
            {
                < 6 => PupilShape.Round,
                6 => PupilShape.VerticalOval,
                7 => PupilShape.CatSlit,
                8 => PupilShape.Heart,
                _ => PupilShape.Star
            };
            p.PupilSizeRatio = Math.Round(0.24 + rng.NextDouble() * 0.18, 2);
            p.PupilOpacity = Math.Round(0.92 + rng.NextDouble() * 0.08, 2);
            p.PupilSoftness = Math.Round(0.03 + rng.NextDouble() * 0.12, 2);

            // 7. Radial pattern (光彩・放射ライン)
            int[] rayCounts = { 24, 36, 48, 56, 64, 72, 80 };
            p.RayCount = rayCounts[rng.Next(rayCounts.Length)];
            p.RayThickness = Math.Round(0.8 + rng.NextDouble() * 1.2, 1);
            p.RayLengthRatio = Math.Round(0.55 + rng.NextDouble() * 0.25, 2);
            p.RayIntensity = Math.Round(0.40 + rng.NextDouble() * 0.45, 2);
            p.RayRandomness = Math.Round(0.25 + rng.NextDouble() * 0.35, 2);
            p.RayInnerRadiusRatio = Math.Round(0.26 + rng.NextDouble() * 0.14, 2);

            // 8. Lens-Style Optical Pattern (フレネルリング)
            p.LensPatternEnabled = rng.Next(10) < 4; // 40% chance
            if (p.LensPatternEnabled)
            {
                p.LensRingCount = rng.Next(4, 11);
                p.LensRingIntensity = Math.Round(0.35 + rng.NextDouble() * 0.35, 2);
                p.LensRingWidth = Math.Round(0.6 + rng.NextDouble() * 0.8, 1);
                p.LensRefractionGlow = Math.Round(0.20 + rng.NextDouble() * 0.35, 2);
                p.LensRingColor = p.RayColor;
            }

            // 9. Particles / Glitter (ラメ)
            p.ParticleCount = rng.Next(10) < 7 ? rng.Next(25, 95) : 0;
            p.ParticleMinSize = Math.Round(0.8 + rng.NextDouble() * 0.6, 1);
            p.ParticleMaxSize = Math.Round(p.ParticleMinSize + 0.8 + rng.NextDouble() * 1.2, 1);
            p.ParticleOpacity = Math.Round(0.65 + rng.NextDouble() * 0.30, 2);
            p.ParticleRadiusRatio = Math.Round(0.68 + rng.NextDouble() * 0.18, 2);

            // 10. Highlights (アニメ調ハイライト配置)
            GenerateRandomHighlights(p, rng);

            // 11. Decorative Symbols (装飾記号)
            p.Symbols.Clear();
            if (rng.Next(10) < 4) // 40% chance of symbol
            {
                SymbolType[] symbolTypes = {
                    SymbolType.Star4, SymbolType.Star5, SymbolType.Heart,
                    SymbolType.Moon, SymbolType.Diamond, SymbolType.SakuraPetal
                };
                var symType = symbolTypes[rng.Next(symbolTypes.Length)];
                p.Symbols.Add(new DecorativeSymbolItem
                {
                    Type = symType,
                    Count = rng.Next(1, 3),
                    Size = Math.Round(0.14 + rng.NextDouble() * 0.08, 2),
                    PositionX = Math.Round(0.18 + rng.NextDouble() * 0.22, 2),
                    PositionY = Math.Round(-0.25 + rng.NextDouble() * 0.45, 2),
                    Color = p.ParticleColor,
                    Opacity = Math.Round(0.75 + rng.NextDouble() * 0.20, 2),
                    Rotation = Math.Round(rng.NextDouble() * 60.0 - 30.0, 1),
                    Enabled = true
                });
            }

            // 12. Odd-Eye Mode (オッドアイ)
            p.IsOddEye = rng.Next(100) < 15; // 15% chance
            if (p.IsOddEye)
            {
                var oddTheme = Themes[rng.Next(Themes.Length)];
                double oddHue = oddTheme.Hue + (rng.NextDouble() * 16.0 - 8.0);
                p.OddEyeBaseColor = HslToHex(oddHue, oddTheme.Saturation, oddTheme.Lightness);
                p.OddEyeInnerColor = HslToHex(oddHue + 8.0, Math.Max(0.1, oddTheme.Saturation - 0.10), Math.Min(0.92, oddTheme.Lightness + 0.24));
                p.OddEyeOuterColor = HslToHex(oddHue - 8.0, Math.Min(1.0, oddTheme.Saturation + 0.08), Math.Max(0.12, oddTheme.Lightness - 0.26));
                p.OddEyeRayColor = HslToHex(oddHue + 10.0, 0.70, 0.86);
                p.OddEyeBottomHighlightColor = HslToHex(oddHue + 12.0, 0.75, 0.88);
                p.OddEyeSeedOffset = rng.Next(500, 5000);
            }

            // 13. RESTORE SIZE AND DIMENSIONS (GUARANTEE NON-MODIFICATION)
            p.SizeMm = preservedSizeMm;
            p.Shape = preservedShape;
            p.AspectRatio = preservedAspectRatio;
            p.TiltAngle = preservedTiltAngle;
        }

        private static void GenerateRandomHighlights(IrisParameters p, Random rng)
        {
            p.Highlights.Clear();
            int style = rng.Next(4);

            switch (style)
            {
                case 0:
                    // Classic 2-point anime highlight
                    p.Highlights.Add(new HighlightItem
                    {
                        Shape = HighlightShape.Circle,
                        PositionX = -0.32,
                        PositionY = -0.32,
                        Size = Math.Round(0.24 + rng.NextDouble() * 0.08, 2),
                        Color = "#FFFFFF",
                        Opacity = 0.95,
                        Enabled = true
                    });
                    p.Highlights.Add(new HighlightItem
                    {
                        Shape = HighlightShape.Circle,
                        PositionX = 0.34,
                        PositionY = 0.30,
                        Size = Math.Round(0.12 + rng.NextDouble() * 0.06, 2),
                        Color = "#FFFFFF",
                        Opacity = 0.75,
                        Enabled = true
                    });
                    break;

                case 1:
                    // Sparkle Star4 + Soft lower highlight
                    p.Highlights.Add(new HighlightItem
                    {
                        Shape = HighlightShape.Star4,
                        PositionX = -0.30,
                        PositionY = -0.30,
                        Size = Math.Round(0.22 + rng.NextDouble() * 0.06, 2),
                        Rotation = Math.Round(rng.NextDouble() * 45.0, 1),
                        Color = "#FFFFFF",
                        Opacity = 0.95,
                        Enabled = true
                    });
                    p.Highlights.Add(new HighlightItem
                    {
                        Shape = HighlightShape.Circle,
                        PositionX = 0.30,
                        PositionY = 0.32,
                        Size = 0.14,
                        Color = "#FFFFFF",
                        Opacity = 0.70,
                        Enabled = true
                    });
                    break;

                case 2:
                    // 3-point dynamic luminous highlight
                    p.Highlights.Add(new HighlightItem
                    {
                        Shape = HighlightShape.Circle,
                        PositionX = -0.34,
                        PositionY = -0.30,
                        Size = 0.25,
                        Color = "#FFFFFF",
                        Opacity = 0.96,
                        Enabled = true
                    });
                    p.Highlights.Add(new HighlightItem
                    {
                        Shape = HighlightShape.Circle,
                        PositionX = -0.15,
                        PositionY = -0.42,
                        Size = 0.10,
                        Color = "#FFFFFF",
                        Opacity = 0.85,
                        Enabled = true
                    });
                    p.Highlights.Add(new HighlightItem
                    {
                        Shape = HighlightShape.Oval,
                        PositionX = 0.32,
                        PositionY = 0.28,
                        Size = 0.16,
                        AspectRatio = 1.35,
                        Rotation = 25.0,
                        Color = "#FFFFFF",
                        Opacity = 0.75,
                        Enabled = true
                    });
                    break;

                case 3:
                default:
                    // Oval stylized anime eye
                    p.Highlights.Add(new HighlightItem
                    {
                        Shape = HighlightShape.Oval,
                        PositionX = -0.30,
                        PositionY = -0.32,
                        Size = 0.26,
                        AspectRatio = 0.85,
                        Rotation = -15.0,
                        Color = "#FFFFFF",
                        Opacity = 0.95,
                        Enabled = true
                    });
                    p.Highlights.Add(new HighlightItem
                    {
                        Shape = HighlightShape.Circle,
                        PositionX = 0.28,
                        PositionY = 0.26,
                        Size = 0.13,
                        Color = "#FFFFFF",
                        Opacity = 0.80,
                        Enabled = true
                    });
                    break;
            }
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
