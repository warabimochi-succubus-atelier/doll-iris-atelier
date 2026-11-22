using System;
using System.Collections.Generic;
using DollIrisGenerator.Models;

namespace DollIrisGenerator.Services
{
    /// <summary>Which pattern layer a 見える範囲 belongs to.</summary>
    public enum PatternLayer
    {
        Rays,       // 放射ライン (main and fine rays)
        Glitter,    // ラメ (grains, flares and their glow)
        IrisFacets, // 宝石カット面
        RimRays     // 外周からの線
    }

    /// <summary>A named 見える範囲 for the quick-select buttons.</summary>
    public sealed record PatternRangeStyle(string Name, double Angle, double Width, double Feather);

    /// <summary>
    /// 見える範囲: a pattern layer is shown only inside a fan seen from the iris center, pointing at
    /// <c>Angle</c> (degrees, 0 = right, 90 = down, on the untilted iris), <c>Width</c> degrees wide, its two edges
    /// fading out over <c>Feather</c> degrees (half inside, half outside the nominal edge). Angles are those the
    /// patterns are laid out with (<see cref="IrisPatterns.OnIris"/>): on an oval the fan is stretched with the iris.
    /// The right eye's fan is mirrored (θ → 180° − θ), like the patterns themselves.
    ///
    /// The exporter draws the fan as a mask of nested sectors (<see cref="Sectors"/>), so the patterns' random
    /// layout never changes with the range: the range only hides what is outside it.
    /// </summary>
    public static class PatternRange
    {
        /// <summary>A range at least this wide (degrees) covers everything and is not drawn.</summary>
        public const double FullThreshold = 359.95;

        /// <summary>Longest angular step (degrees) between the nested sectors of a feathered edge.</summary>
        public const double FeatherStepDeg = 1.0;
        public const int MinFeatherSteps = 4, MaxFeatherSteps = 48;

        /// <summary>The quick-select ranges (簡単選択), in button order. The first covers everything.</summary>
        public static readonly IReadOnlyList<PatternRangeStyle> Styles = new[]
        {
            new PatternRangeStyle("全体", IrisParameters.DefaultRangeAngle, IrisParameters.FullRangeWidth, IrisParameters.DefaultRangeFeather),
            new PatternRangeStyle("下半分", 90.0, 180.0, 40.0),
            new PatternRangeStyle("下 1/3", 90.0, 120.0, 40.0),
            new PatternRangeStyle("上半分", 270.0, 180.0, 40.0),
        };

        public static void Apply(IrisParameters p, PatternLayer layer, PatternRangeStyle s) => Set(p, layer, s.Angle, s.Width, s.Feather);

        public static void Set(IrisParameters p, PatternLayer layer, double angle, double width, double feather)
        {
            switch (layer)
            {
                case PatternLayer.Rays: p.RayRangeAngle = angle; p.RayRangeWidth = width; p.RayRangeFeather = feather; break;
                case PatternLayer.Glitter: p.ParticleRangeAngle = angle; p.ParticleRangeWidth = width; p.ParticleRangeFeather = feather; break;
                case PatternLayer.IrisFacets: p.IrisFacetRangeAngle = angle; p.IrisFacetRangeWidth = width; p.IrisFacetRangeFeather = feather; break;
                case PatternLayer.RimRays: p.RimRayRangeAngle = angle; p.RimRayRangeWidth = width; p.RimRayRangeFeather = feather; break;
                default: throw new ArgumentOutOfRangeException(nameof(layer));
            }
        }

        /// <summary>
        /// The quick-select range the layer's settings match, or null. Any full range is 全体 (its direction and
        /// edge do nothing).
        /// </summary>
        public static PatternRangeStyle? Match(IrisParameters p, PatternLayer layer)
        {
            var (angle, width, feather) = Of(p, layer);
            if (IsFull(width)) return Styles[0];
            foreach (var s in Styles)
            {
                if (Math.Abs(angle - s.Angle) < 0.05 && Math.Abs(width - s.Width) < 0.05 && Math.Abs(feather - s.Feather) < 0.05)
                    return s;
            }
            return null;
        }

        /// <summary>Human-readable names of the layers (UI, history).</summary>
        public static string LayerName(PatternLayer layer) => layer switch
        {
            PatternLayer.Rays => "放射ライン",
            PatternLayer.Glitter => "ラメ",
            PatternLayer.IrisFacets => "宝石カット面",
            PatternLayer.RimRays => "外周からの線",
            _ => layer.ToString()
        };

        public static (double Angle, double Width, double Feather) Of(IrisParameters p, PatternLayer layer) => layer switch
        {
            PatternLayer.Rays => (p.RayRangeAngle, p.RayRangeWidth, p.RayRangeFeather),
            PatternLayer.Glitter => (p.ParticleRangeAngle, p.ParticleRangeWidth, p.ParticleRangeFeather),
            PatternLayer.IrisFacets => (p.IrisFacetRangeAngle, p.IrisFacetRangeWidth, p.IrisFacetRangeFeather),
            PatternLayer.RimRays => (p.RimRayRangeAngle, p.RimRayRangeWidth, p.RimRayRangeFeather),
            _ => throw new ArgumentOutOfRangeException(nameof(layer))
        };

        public static bool IsFull(double width) => width >= FullThreshold;

        public static bool IsFull(IrisParameters p, PatternLayer layer) => IsFull(Of(p, layer).Width);

        /// <summary>The fan's center direction for one eye (mirrored for the right eye).</summary>
        public static double CenterFor(double angle, bool isRightEye)
        {
            double a = isRightEye ? 180.0 - angle : angle;
            a %= 360.0;
            return a < 0 ? a + 360.0 : a;
        }

        /// <summary>How visible (0–1) the pattern is at <paramref name="angleDeg"/> (smoothstep across the edges).</summary>
        public static double Weight(double angleDeg, double center, double width, double feather)
        {
            if (IsFull(width)) return 1.0;
            double d = Math.Abs(((angleDeg - center) % 360.0 + 540.0) % 360.0 - 180.0);
            return WeightAtDistance(d, width, feather);
        }

        /// <summary><see cref="Weight"/> at an angular distance <paramref name="d"/> from the center (not wrapped round).</summary>
        private static double WeightAtDistance(double d, double width, double feather)
        {
            double edge = width / 2.0;
            if (feather < 0.01) return d <= edge ? 1.0 : 0.0;
            double t = Math.Clamp((edge + feather / 2.0 - d) / feather, 0.0, 1.0);
            return t * t * (3.0 - 2.0 * t);
        }

        /// <summary>
        /// The nested sectors that make the mask, widest first: half-angle (degrees from the center, at most 180 =
        /// the whole turn) and opacity. A point at angular distance d from the center is covered by every sector
        /// whose half-angle is larger, so drawn white one over another their opacities stack up to
        /// <see cref="Weight"/> (in steps of at most <see cref="FeatherStepDeg"/> across a feathered edge). Each
        /// sector is one piece, so there are no seams between neighbouring wedges.
        /// </summary>
        public static (double HalfDeg, double Opacity)[] Sectors(double width, double feather)
        {
            double edge = width / 2.0;
            if (feather < 0.01) return new[] { (Math.Clamp(edge, 0.0, 180.0), 1.0) };

            double lo = edge - feather / 2.0, hi = edge + feather / 2.0;
            int count = Math.Clamp((int)Math.Ceiling(feather / FeatherStepDeg), MinFeatherSteps, MaxFeatherSteps);
            double step = (hi - lo) / count;
            var target = new double[count];
            for (int k = 0; k < count; k++)
                target[k] = WeightAtDistance(lo + (k + 0.5) * step, width, feather); // past 180° too: kept decreasing

            var result = new List<(double, double)>(count);
            for (int k = count - 1; k >= 0; k--)
            {
                double outer = k == count - 1 ? 1.0 : 1.0 - target[k + 1];
                double opacity = outer > 1e-9 ? Math.Clamp(1.0 - (1.0 - target[k]) / outer, 0.0, 1.0) : 0.0;
                double half = Math.Clamp(lo + (k + 1) * step, 0.0, 180.0);
                if (opacity > 0.0005 && half > 0.0) result.Add((half, opacity));
            }
            return result.ToArray();
        }
    }
}
