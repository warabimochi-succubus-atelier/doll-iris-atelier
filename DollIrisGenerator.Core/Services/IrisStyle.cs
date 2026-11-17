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

        internal static Point PolarPoint(Point c, double r, double degrees)
        {
            double rad = degrees * Math.PI / 180.0;
            return new Point(c.X + r * Math.Cos(rad), c.Y + r * Math.Sin(rad));
        }

        /// <summary>
        /// Local box (around 0, 0) of a highlight shape after its 上下左右の長さ, the area its gradient is laid across.
        /// <paramref name="swapSides"/>: the highlight is mirrored (its left and right lengths trade places).
        /// </summary>
        public static Rect HighlightBox(HighlightItem h, double radius, bool swapSides = false)
        {
            double top = radius * h.StretchTop, bottom = radius * h.StretchBottom;
            double left = radius * (swapSides ? h.StretchRight : h.StretchLeft);
            double right = radius * (swapSides ? h.StretchLeft : h.StretchRight);
            if (h.Shape == HighlightShape.Rhombus) { left *= 0.65; right *= 0.65; }
            return new Rect(-left, -top, left + right, top + bottom);
        }
    }
}
