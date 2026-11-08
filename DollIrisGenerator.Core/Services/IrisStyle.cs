using System;
using DollIrisGenerator.Core.Geometry;
using DollIrisGenerator.Models;

namespace DollIrisGenerator.Services
{
    /// <summary>
    /// Drawing rules shared by every renderer (the WPF screen / PDF / PNG renderer in the app and the SVG export
    /// here), so they size and place things the same way.
    /// </summary>
    public static class IrisStyle
    {
        /// <summary>
        /// Preview zoom the style unit is defined at (the app's default preview zoom, 360%, which shows the
        /// default 10 mm iris about 136 DIP across).
        /// </summary>
        public const double ReferenceZoom = 3.6;

        /// <summary>
        /// Physical size of one "style unit" (ray thickness, lens ring width, glitter size, guide lines):
        /// one screen DIP at the default preview zoom, i.e. 25.4 / (96 * 3.6) = 0.0735 mm.
        /// Tying these to millimetres makes the preview (at any zoom), PDF, PNG and SVG all match the
        /// default-zoom preview the design was made with. It moved together with the default zoom when the
        /// size range changed from eye-sized (around 20 mm) to iris-sized (around 10 mm) values, so the
        /// built-in presets keep the same proportions on the smaller iris.
        /// </summary>
        public const double MmPerStyleUnit = 25.4 / (96.0 * ReferenceZoom);

        /// <summary>
        /// Color ("#AARRGGBB" or plain hex) a glitter grain has where it sits: the solid color, or the iris-wide
        /// gradient's color at that point (placed exactly like the grains' brush).
        /// </summary>
        internal static Func<Point, string> GrainColorSampler(string? value, Rect irisBox, bool mirror, double bandInner, double bandOuter)
        {
            var fill = ColorFill.Parse(value);
            if (!fill.IsGradient)
            {
                string solid = GeometryHelper.SanitizeColorHex(value, "#FFFFFF");
                return _ => solid;
            }
            if (mirror) fill = fill.MirroredX();
            var map = GradientMapping.Create(fill, irisBox, bandInner, bandOuter);
            // Same text as map.ColorAt, with the stop colors read once for all the grains
            var argbAt = map.ArgbSampler();
            return pt =>
            {
                var c = argbAt(pt);
                return ColorFill.FormatArgb(c.A, c.R, c.G, c.B);
            };
        }

        internal static Point PolarPoint(Point c, double r, double degrees)
        {
            double rad = degrees * Math.PI / 180.0;
            return new Point(c.X + r * Math.Cos(rad), c.Y + r * Math.Sin(rad));
        }

        /// <summary>Local box (around 0, 0) of a highlight shape, the area its gradient is laid across.</summary>
        public static Rect HighlightBox(HighlightItem h, double radius) => h.Shape switch
        {
            HighlightShape.Oval => new Rect(-radius * HighlightAspect(h), -radius, 2.0 * radius * HighlightAspect(h), 2.0 * radius),
            HighlightShape.Rhombus => new Rect(-radius * 0.65, -radius, 1.3 * radius, 2.0 * radius),
            _ => new Rect(-radius, -radius, 2.0 * radius, 2.0 * radius)
        };

        private static double HighlightAspect(HighlightItem h) => h.AspectRatio > 0 ? h.AspectRatio : 1.0;
    }
}
