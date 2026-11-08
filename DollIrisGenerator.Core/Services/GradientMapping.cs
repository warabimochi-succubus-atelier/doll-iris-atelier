using System;
using System.Collections.Generic;
using System.Linq;
using DollIrisGenerator.Core.Geometry;
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

        /// <summary>Radial: center and radii of the 100 % ellipse. Conic: the center it turns around, and half the box size.</summary>
        public Point Center { get; }
        public double RadiusX { get; }
        public double RadiusY { get; }

        /// <summary>Conic: direction of 0 % in degrees (0° = right, 90° = down); the colors go round clockwise.</summary>
        public double StartAngle { get; }

        /// <summary>Stops with their offsets already moved into the band (radial) — use these, not the fill's own stops.</summary>
        public IReadOnlyList<ColorStop> Stops { get; }

        public ColorFillKind Kind { get; }

        private GradientMapping(ColorFillKind kind, Point start, Point end, Point center, double rx, double ry, IReadOnlyList<ColorStop> stops, double startAngle = 0.0)
        {
            Kind = kind;
            Start = start;
            End = end;
            Center = center;
            RadiusX = rx;
            RadiusY = ry;
            Stops = stops;
            StartAngle = startAngle;
        }

        /// <summary>
        /// Number of wedges a conic gradient is drawn with (on screen as in SVG). 2° each: neighbouring wedges
        /// differ by well under one color step for any sensible gradient, so no banding shows at iris size.
        /// </summary>
        public const int ConicSegments = 180;

        /// <summary>Position (0..1) along the gradient at <paramref name="p"/>, as the renderers lay it out.</summary>
        public double OffsetAt(Point p)
        {
            switch (Kind)
            {
                case ColorFillKind.Radial:
                {
                    double dx = (p.X - Center.X) / Math.Max(RadiusX, 1e-9);
                    double dy = (p.Y - Center.Y) / Math.Max(RadiusY, 1e-9);
                    return Math.Sqrt(dx * dx + dy * dy);
                }
                case ColorFillKind.Conic:
                {
                    double deg = Math.Atan2(p.Y - Center.Y, p.X - Center.X) * 180.0 / Math.PI;
                    double rel = (deg - StartAngle) % 360.0;
                    if (rel < 0) rel += 360.0;
                    return rel / 360.0;
                }
                case ColorFillKind.Linear:
                {
                    double vx = End.X - Start.X, vy = End.Y - Start.Y;
                    double len2 = vx * vx + vy * vy;
                    if (len2 < 1e-18) return 0.0;
                    return ((p.X - Start.X) * vx + (p.Y - Start.Y) * vy) / len2;
                }
                default:
                    return 0.0;
            }
        }

        /// <summary>Color ("#AARRGGBB") the gradient has at <paramref name="p"/>, e.g. for the halo of a glitter grain sitting there.</summary>
        public string ColorAt(Point p) => ColorFill.ColorAt(Stops, OffsetAt(p));

        /// <summary>
        /// <see cref="ColorAt"/> as channels (same values), for coloring many points: the stop colors are read once.
        /// </summary>
        public Func<Point, (int A, int R, int G, int B)> ArgbSampler()
        {
            var sampler = new ColorFill.StopSampler(Stops);
            var map = this;
            return p => sampler.ArgbAt(map.OffsetAt(p));
        }

        /// <summary>
        /// Wedges a conic gradient is drawn with: for each, its start / end angle (degrees, as <see cref="StartAngle"/>)
        /// and its color, taken at the middle of the wedge.
        /// </summary>
        public IEnumerable<(double From, double To, string Color)> ConicWedges()
        {
            double step = 360.0 / ConicSegments;
            for (int i = 0; i < ConicSegments; i++)
            {
                double t = (i + 0.5) / ConicSegments;
                yield return (StartAngle + i * step, StartAngle + (i + 1) * step, ColorFill.ColorAt(Stops, t));
            }
        }

        /// <summary>
        /// Area a conic gradient's wedges cover: the box grown by half its size on every side, so strokes that
        /// reach past the element's box (a wide limbal ring, a ray's round cap) are still colored and never see a
        /// repeated tile.
        /// </summary>
        public Rect ConicArea()
        {
            double w = RadiusX * 2.0, h = RadiusY * 2.0;
            return new Rect(Center.X - w, Center.Y - h, w * 2.0, h * 2.0);
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

            if (fill.Kind == ColorFillKind.Conic)
            {
                // Turns around the box center; the band does not apply (it runs around, not outwards)
                return new GradientMapping(ColorFillKind.Conic, c, c, c, w / 2.0, h / 2.0, fill.Stops.ToArray(), fill.Angle);
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
