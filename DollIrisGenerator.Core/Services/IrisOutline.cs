using System;
using System.Collections.Generic;
using DollIrisGenerator.Core.Geometry;
using DollIrisGenerator.Models;

namespace DollIrisGenerator.Services
{
    /// <summary>
    /// The iris outline (虹彩の輪郭) when it is not a plain ellipse: an egg / shield (上下の幅の差), a flat top or
    /// bottom (上・下の平ら) and a rounded-square look (四角さ). All four at 0 is the ellipse, which the exporter keeps
    /// drawing as an <c>&lt;ellipse&gt;</c> (unchanged output); see <see cref="IsEllipse"/>.
    ///
    /// The shape is a superellipse whose exponents differ per side: |x|^nx + |y|^m = 1, with nx for the sides and m
    /// for the top half (y &lt; 0) and the bottom half separately. A large m flattens that end and leaves rounded
    /// corners. The width is then scaled with the height (x × (1 − 0.4 × taper × y)), and the result is fitted into
    /// the iris box again, so the outline always touches the box's four sides: the size (直径) and aspect stay the
    /// width and height of the outline, and the eye's center (pupil, rays...) stays the box center.
    /// The shape is left-right symmetric, so the right eye needs no mirroring.
    /// </summary>
    /// <summary>A named set of the four outline settings, for the quick-select buttons (簡単選択).</summary>
    public sealed record OutlineStyle(string Name, double Taper, double TopFlat, double BottomFlat, double Squareness);

    public static class IrisOutline
    {
        /// <summary>The quick-select outlines, in button order. The first is the plain ellipse.</summary>
        public static readonly IReadOnlyList<OutlineStyle> Styles = new[]
        {
            new OutlineStyle("楕円", 0.0, 0.0, 0.0, 0.0),
            new OutlineStyle("上が平ら", 0.0, 0.6, 0.0, 0.2),
            new OutlineStyle("卵形", 0.35, 0.0, 0.0, 0.0),
            new OutlineStyle("盾形", 0.6, 0.5, 0.0, 0.3),
            new OutlineStyle("角丸", 0.0, 0.0, 0.0, 0.8),
        };

        /// <summary>Sets the design's four outline settings to <paramref name="style"/>'s.</summary>
        public static void Apply(IrisParameters p, OutlineStyle style)
        {
            p.OutlineTaper = style.Taper;
            p.OutlineTopFlat = style.TopFlat;
            p.OutlineBottomFlat = style.BottomFlat;
            p.OutlineSquareness = style.Squareness;
        }

        /// <summary>The quick-select outline the design's settings match (to 0.005), or null.</summary>
        public static OutlineStyle? Match(IrisParameters p)
        {
            foreach (var s in Styles)
            {
                if (Math.Abs(p.OutlineTaper - s.Taper) < Epsilon && Math.Abs(p.OutlineTopFlat - s.TopFlat) < Epsilon
                    && Math.Abs(p.OutlineBottomFlat - s.BottomFlat) < Epsilon && Math.Abs(p.OutlineSquareness - s.Squareness) < Epsilon)
                    return s;
            }
            return null;
        }

        /// <summary>Points the outline is drawn through (joined by smooth curves). Enough for ±0.01 mm on a 20 mm iris.</summary>
        public const int PointCount = 128;

        /// <summary>Samples taken along the exact curve before the points are spaced evenly by length.</summary>
        private const int DenseSamples = 4096;

        /// <summary>Below this every setting counts as 0 (an ellipse).</summary>
        private const double Epsilon = 0.005;

        /// <summary>How much 上下の幅の差 = ±1 widens one end and narrows the other (fraction of the half width).</summary>
        private const double TaperAmount = 0.4;

        /// <summary>Exponent added at 上・下の平ら = 1 (2 is an ellipse; 12 is nearly a straight edge with round corners).</summary>
        private const double FlatExponent = 10.0;

        /// <summary>Exponent added at 四角さ = 1, on every side.</summary>
        private const double SquareExponent = 2.0;

        public static bool IsEllipse(IrisParameters p) =>
            Math.Abs(p.OutlineTaper) < Epsilon && p.OutlineTopFlat < Epsilon && p.OutlineBottomFlat < Epsilon
            && p.OutlineSquareness < Epsilon;

        /// <summary>
        /// The outline of an iris <paramref name="rx"/> × <paramref name="ry"/> (half width / height) centered at (0, 0),
        /// y down (screen / SVG), starting at the top and going clockwise. Not tilted.
        /// </summary>
        public static ShapeOutline Build(IrisParameters p, double rx, double ry) =>
            Build(p.OutlineTaper, p.OutlineTopFlat, p.OutlineBottomFlat, p.OutlineSquareness, rx, ry);

        public static ShapeOutline Build(double taper, double topFlat, double bottomFlat, double squareness, double rx, double ry)
        {
            var unit = UnitPoints(taper, topFlat, bottomFlat, squareness);
            var pts = new Point[unit.Count];
            for (int i = 0; i < unit.Count; i++) pts[i] = new Point(unit[i].X * rx, unit[i].Y * ry);
            return CatmullRom(pts);
        }

        /// <summary><see cref="PointCount"/> points evenly spaced along the outline, fitted into [-1, 1] × [-1, 1].</summary>
        internal static List<Point> UnitPoints(double taper, double topFlat, double bottomFlat, double squareness)
        {
            taper = Math.Clamp(taper, -1.0, 1.0);
            double nx = 2.0 + SquareExponent * Math.Clamp(squareness, 0.0, 1.0);
            double mTop = nx + FlatExponent * Math.Clamp(topFlat, 0.0, 1.0);
            double mBottom = nx + FlatExponent * Math.Clamp(bottomFlat, 0.0, 1.0);
            double a = TaperAmount * taper;

            // Dense samples of the exact curve, from the top (t = −90°) clockwise on screen
            var dense = new Point[DenseSamples];
            double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
            for (int i = 0; i < DenseSamples; i++)
            {
                double t = -Math.PI / 2.0 + 2.0 * Math.PI * i / DenseSamples;
                double c = Math.Cos(t), s = Math.Sin(t);
                double y = Math.Sign(s) * Math.Pow(Math.Abs(s), 2.0 / (s < 0 ? mTop : mBottom));
                double x = Math.Sign(c) * Math.Pow(Math.Abs(c), 2.0 / nx) * (1.0 - a * y);
                dense[i] = new Point(x, y);
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
            }

            // Fit into the unit box (the taper widens one end beyond ±1)
            double cxBox = (minX + maxX) / 2.0, cyBox = (minY + maxY) / 2.0;
            double sx = 2.0 / (maxX - minX), sy = 2.0 / (maxY - minY);
            for (int i = 0; i < DenseSamples; i++)
                dense[i] = new Point((dense[i].X - cxBox) * sx, (dense[i].Y - cyBox) * sy);

            // Even spacing by length: the superellipse parameter crowds samples near some points and thins them
            // near others (a flat edge's ends)
            var cumulative = new double[DenseSamples + 1];
            for (int i = 0; i < DenseSamples; i++)
                cumulative[i + 1] = cumulative[i] + Distance(dense[i], dense[(i + 1) % DenseSamples]);
            double total = cumulative[DenseSamples];

            var result = new List<Point>(PointCount);
            int j = 0;
            for (int k = 0; k < PointCount; k++)
            {
                double target = total * k / PointCount;
                while (j < DenseSamples - 1 && cumulative[j + 1] < target) j++;
                double seg = cumulative[j + 1] - cumulative[j];
                double f = seg > 1e-12 ? (target - cumulative[j]) / seg : 0.0;
                var p0 = dense[j];
                var p1 = dense[(j + 1) % DenseSamples];
                result.Add(new Point(p0.X + (p1.X - p0.X) * f, p0.Y + (p1.Y - p0.Y) * f));
            }
            return result;
        }

        /// <summary>A closed smooth curve through the points (Catmull-Rom, written as cubic Beziers).</summary>
        private static ShapeOutline CatmullRom(IReadOnlyList<Point> pts)
        {
            int n = pts.Count;
            var segments = new List<OutlineSegment>(n);
            for (int i = 0; i < n; i++)
            {
                Point prev = pts[(i - 1 + n) % n], p0 = pts[i], p1 = pts[(i + 1) % n], next = pts[(i + 2) % n];
                var c1 = new Point(p0.X + (p1.X - prev.X) / 6.0, p0.Y + (p1.Y - prev.Y) / 6.0);
                var c2 = new Point(p1.X - (next.X - p0.X) / 6.0, p1.Y - (next.Y - p0.Y) / 6.0);
                segments.Add(new OutlineSegment(c1, c2, p1));
            }
            return new ShapeOutline(pts[0], segments);
        }

        private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    }
}
