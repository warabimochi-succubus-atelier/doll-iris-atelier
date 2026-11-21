using System;
using System.Collections.Generic;
using DollIrisGenerator.Models;

namespace DollIrisGenerator.Services
{
    /// <summary>
    /// The highlight layouts 「属性ランダム生成」 picks from and the built-in presets use (SPECIFICATION.md 7.4). Every
    /// layout has its main light at the upper left and a smaller one at the lower right, where the light of
    /// <see cref="IrisParameters.LightAngle"/> (200–270°) puts them.
    /// </summary>
    public enum HighlightStyle
    {
        /// <summary>二つ玉: a large circle and a small one (the app's default highlights).</summary>
        Classic,
        /// <summary>キラッ: a four-pointed sparkle and a small circle.</summary>
        Sparkle,
        /// <summary>三つ玉: a large circle, a dot beside it and a tilted oval below.</summary>
        ThreeDots,
        /// <summary>しずく: a tall tilted oval and a small circle.</summary>
        Droplet,
        /// <summary>ハート添え: a large circle and a small heart.</summary>
        Heart,
        /// <summary>窓: a wide soft oval, a small sparkle above it and a dot below.</summary>
        Window,
        /// <summary>ダイヤ: a diamond-shaped glint and a small circle.</summary>
        Diamond,
        /// <summary>三日月: a large circle and a crescent of reflected light along the lower edge.</summary>
        Crescent
    }

    public static class HighlightStyles
    {
        public static IReadOnlyList<HighlightStyle> All { get; } = (HighlightStyle[])Enum.GetValues(typeof(HighlightStyle));

        /// <summary>
        /// The highlights of <paramref name="style"/>. With <paramref name="jitter"/>, positions move by up to ±0.03,
        /// sizes by up to ±10% and sparkles turn a little, so two random designs with the same layout still differ.
        /// <paramref name="softness"/> (輪郭のぼかし) goes on the smooth shapes only (circles and the crescent): a blurred
        /// sparkle, heart or diamond loses the points that make it one. <paramref name="glow"/> (光のにじみ) goes on all.
        /// </summary>
        public static List<HighlightItem> Build(HighlightStyle style, Random? jitter = null, RightEyeFlip flip = RightEyeFlip.Mirror,
            double softness = 0.0, double glow = 0.0)
        {
            double Pos(double v) => jitter == null ? v : Math.Round(v + (jitter.NextDouble() - 0.5) * 0.06, 2);
            double Size(double v) => jitter == null ? v : Math.Round(v * (0.9 + jitter.NextDouble() * 0.2), 2);
            double Turn(double v, double range) => jitter == null ? v : Math.Round(v + (jitter.NextDouble() - 0.5) * range, 1);

            HighlightItem Item(HighlightShape shape, double x, double y, double size, double opacity,
                double rotation = 0.0, double stretchX = 1.0) => new()
            {
                Shape = shape,
                PositionX = Pos(x),
                PositionY = Pos(y),
                Size = Size(size),
                Rotation = rotation,
                StretchLeft = stretchX,
                StretchRight = stretchX,
                Color = "#FFFFFF",
                Opacity = opacity,
                Softness = IsSmooth(shape) ? softness : 0.0,
                Glow = glow,
                Enabled = true,
                RightEyeFlip = flip
            };

            return style switch
            {
                HighlightStyle.Sparkle => new()
                {
                    Item(HighlightShape.Star4, -0.30, -0.30, 0.26, 0.95, rotation: Turn(0.0, 30.0)),
                    Item(HighlightShape.Circle, 0.30, 0.32, 0.13, 0.70)
                },
                HighlightStyle.ThreeDots => new()
                {
                    Item(HighlightShape.Circle, -0.34, -0.30, 0.25, 0.96),
                    Item(HighlightShape.Circle, -0.13, -0.44, 0.09, 0.85),
                    Item(HighlightShape.Circle, 0.32, 0.28, 0.15, 0.72, rotation: 25.0, stretchX: 1.35)
                },
                HighlightStyle.Droplet => new()
                {
                    Item(HighlightShape.Circle, -0.30, -0.32, 0.26, 0.95, rotation: Turn(-15.0, 10.0), stretchX: 0.8),
                    Item(HighlightShape.Circle, 0.28, 0.27, 0.12, 0.78)
                },
                HighlightStyle.Heart => new()
                {
                    Item(HighlightShape.Circle, -0.32, -0.31, 0.24, 0.95),
                    Item(HighlightShape.Heart, 0.31, 0.29, 0.13, 0.85, rotation: Turn(-12.0, 16.0))
                },
                HighlightStyle.Window => new()
                {
                    Item(HighlightShape.Circle, -0.28, -0.30, 0.25, 0.85, rotation: -30.0, stretchX: 1.3),
                    Item(HighlightShape.Star4, 0.05, -0.45, 0.10, 0.90, rotation: Turn(0.0, 30.0)),
                    Item(HighlightShape.Circle, 0.32, 0.30, 0.10, 0.70)
                },
                HighlightStyle.Diamond => new()
                {
                    Item(HighlightShape.Diamond, -0.31, -0.31, 0.25, 0.92, rotation: Turn(-20.0, 16.0)),
                    Item(HighlightShape.Circle, 0.30, 0.30, 0.12, 0.75)
                },
                HighlightStyle.Crescent => new()
                {
                    Item(HighlightShape.Circle, -0.32, -0.32, 0.24, 0.95),
                    // A moon bulging toward the lower right: a thin reflection following that edge
                    Item(HighlightShape.Moon, 0.36, 0.38, 0.20, 0.55, rotation: Turn(45.0, 16.0))
                },
                _ => new()
                {
                    Item(HighlightShape.Circle, -0.32, -0.32, 0.26, 0.95),
                    Item(HighlightShape.Circle, 0.35, 0.30, 0.14, 0.75)
                }
            };
        }

        /// <summary>Shapes without points, which 輪郭のぼかし softens without losing what they are.</summary>
        public static bool IsSmooth(HighlightShape shape) => shape is HighlightShape.Circle or HighlightShape.Moon;
    }
}
