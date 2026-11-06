using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using DollIrisGenerator.Models;

namespace DollIrisGenerator.Services
{
    /// <summary>
    /// Where a gradient sits on the element it colors. The screen / PDF renderer (WPF brushes in absolute
    /// coordinates) and the SVG exporter (userSpaceOnUse gradients) both take their coordinates from here,
    /// so the three outputs place every gradient identically.
    ///
    /// Each element supplies a reference box in its own drawing coordinates (before its rotation):
    /// <list type="bullet">
    /// <item>Small shapes (decorative symbols, highlights, pupil): the shape's own bounding box, so the
    /// gradient runs across each star / heart / highlight and turns with it.</item>
    /// <item>Iris-wide elements (limbal ring, rays, Fresnel rings, glitter): the iris ellipse. A linear
    /// gradient runs across the whole iris (each glitter grain takes the color of where it sits), and a
    /// radial one runs from the band's inner edge to its outer edge (<see cref="Band"/>), e.g. from the
    /// inside of the limbal ring to the iris edge.</item>
    /// </list>
    /// </summary>
    public readonly struct GradientMapping
    {
        /// <summary>Linear: start (0 %) and end (100 %) points.</summary>
        public Point Start { get; }
        public Point End { get; }

        /// <summary>Radial: center and radii of the 100 % ellipse.</summary>
        public Point Center { get; }
        public double RadiusX { get; }
        public double RadiusY { get; }

        /// <summary>Stops with their offsets already moved into the band (radial) — use these, not the fill's own stops.</summary>
        public IReadOnlyList<ColorStop> Stops { get; }

        public ColorFillKind Kind { get; }

        private GradientMapping(ColorFillKind kind, Point start, Point end, Point center, double rx, double ry, IReadOnlyList<ColorStop> stops)
        {
            Kind = kind;
            Start = start;
            End = end;
            Center = center;
            RadiusX = rx;
            RadiusY = ry;
            Stops = stops;
        }

        /// <summary>
        /// Maps <paramref name="fill"/> (a gradient) onto <paramref name="box"/>.
        /// Linear: the gradient line goes through the box center at the fill's angle and is just long enough
        /// that 0 % and 100 % touch the box corners (as in CSS), so the whole box is covered for any angle.
        /// Radial: centered in the box, the 100 % ellipse touches the box edges; with a band
        /// (<paramref name="bandInner"/>..<paramref name="bandOuter"/>, fractions of that radius) 0 % is moved
        /// to the band's inner edge and 100 % to its outer edge.
        /// </summary>
        public static GradientMapping Create(ColorFill fill, Rect box, double bandInner = 0.0, double bandOuter = 1.0)
        {
            const double MinExtent = 1e-6;
            double w = Math.Max(box.Width, MinExtent);
            double h = Math.Max(box.Height, MinExtent);
            var c = new Point(box.X + box.Width / 2.0, box.Y + box.Height / 2.0);

            if (fill.Kind == ColorFillKind.Radial)
            {
                double inner = double.IsFinite(bandInner) ? Math.Clamp(bandInner, 0.0, 1.0) : 0.0;
                double outer = double.IsFinite(bandOuter) ? Math.Clamp(bandOuter, inner, 1.0) : 1.0;
                var stops = fill.Stops.Select(s => new ColorStop(s.Color, inner + s.Offset * (outer - inner))).ToArray();
                return new GradientMapping(ColorFillKind.Radial, c, c, c, w / 2.0, h / 2.0, stops);
            }

            double rad = fill.Angle * Math.PI / 180.0;
            double dx = Math.Cos(rad), dy = Math.Sin(rad);
            double half = Math.Abs(w / 2.0 * dx) + Math.Abs(h / 2.0 * dy);
            half = Math.Max(half, MinExtent);
            var start = new Point(c.X - dx * half, c.Y - dy * half);
            var end = new Point(c.X + dx * half, c.Y + dy * half);
            return new GradientMapping(ColorFillKind.Linear, start, end, c, w / 2.0, h / 2.0, fill.Stops.ToArray());
        }

        /// <summary>Box of an ellipse centered at <paramref name="center"/>.</summary>
        public static Rect EllipseBox(Point center, double rx, double ry) =>
            new(center.X - rx, center.Y - ry, rx * 2.0, ry * 2.0);
    }
}
