using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Media;
using DollIrisGenerator.Models;

namespace DollIrisGenerator.Services
{
    /// <summary>
    /// One piece of a closed outline: a cubic Bezier from the previous point to <see cref="End"/>.
    /// Straight edges keep <see cref="IsLine"/> so they are emitted as lines (C1 / C2 then sit on the line).
    /// </summary>
    public readonly struct OutlineSegment
    {
        public Point C1 { get; }
        public Point C2 { get; }
        public Point End { get; }
        public bool IsLine { get; }

        public OutlineSegment(Point c1, Point c2, Point end, bool isLine = false)
        {
            C1 = c1;
            C2 = c2;
            End = end;
            IsLine = isLine;
        }

        public static OutlineSegment Line(Point from, Point to) =>
            new(Lerp(from, to, 1.0 / 3.0), Lerp(from, to, 2.0 / 3.0), to, isLine: true);

        internal static Point Lerp(Point a, Point b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
    }

    /// <summary>
    /// A closed outline made of lines and cubic Beziers, centered at (0, 0).
    /// The pupil shapes and decorative symbols are built here once and then turned into a WPF geometry
    /// (screen / PNG / PDF) or SVG path data, so every output draws exactly the same outline.
    /// <see cref="Rounded"/> rounds the corners (star tips, heart cusp, moon horns...).
    /// </summary>
    public sealed class ShapeOutline
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Joins whose tangents turn less than this are already smooth and are not rounded.</summary>
        private const double CornerAngleThresholdDeg = 2.0;

        /// <summary>
        /// A corner is never cut further along a curved edge than where that edge has turned this much,
        /// so the rounding stays local to the corner (a thin moon horn does not swallow the whole arc).
        /// </summary>
        private const double MaxTurnAlongCurveDeg = 45.0;

        /// <summary>The rounding curve of one corner never has to turn more than this (a 180° turn has no smooth join).</summary>
        private const double MaxJoinTurnDeg = 165.0;

        /// <summary>How much more than the sharp corner itself the rounding curve may turn (keeps it from growing into a spike).</summary>
        private const double MaxExtraJoinTurnDeg = 40.0;
        public Point Start { get; }

        /// <summary>The segments in order; the last one ends back at <see cref="Start"/>.</summary>
        public IReadOnlyList<OutlineSegment> Segments { get; }

        public ShapeOutline(Point start, IReadOnlyList<OutlineSegment> segments)
        {
            Start = start;
            Segments = segments;
        }

        // ------------------------------------------------------------------
        // Shapes
        // ------------------------------------------------------------------

        /// <summary>Star with <paramref name="points"/> tips, first tip pointing up.</summary>
        public static ShapeOutline Star(int points, double radius, double innerRatio)
        {
            var vertices = new List<Point>();
            for (int i = 0; i < points * 2; i++)
            {
                double angle = i * Math.PI / points - Math.PI / 2.0;
                double r = (i % 2 == 0) ? radius : radius * innerRatio;
                vertices.Add(new Point(r * Math.Cos(angle), r * Math.Sin(angle)));
            }
            return Polygon(vertices);
        }

        public static ShapeOutline Diamond(double radiusX, double radiusY) => Polygon(new[]
        {
            new Point(0, -radiusY), new Point(radiusX, 0), new Point(0, radiusY), new Point(-radiusX, 0)
        });

        public static ShapeOutline Cross(double radius, double barThickness = 0.32)
        {
            double r = radius;
            double w = radius * barThickness;
            return Polygon(new[]
            {
                new Point(-w, -r), new Point(w, -r), new Point(w, -w), new Point(r, -w),
                new Point(r, w), new Point(w, w), new Point(w, r), new Point(-w, r),
                new Point(-w, w), new Point(-r, w), new Point(-r, -w), new Point(-w, -w)
            });
        }

        public static ShapeOutline Heart(double radius)
        {
            double topY = -radius * 0.45;
            double bottomY = radius * 0.85;
            double leftX = -radius * 0.95;
            double rightX = radius * 0.95;

            var bottom = new Point(0, bottomY);
            var top = new Point(0, topY);
            return new ShapeOutline(bottom, new[]
            {
                new OutlineSegment(new Point(leftX * 1.05, bottomY * 0.2), new Point(leftX, topY * 1.3), top),
                new OutlineSegment(new Point(rightX, topY * 1.3), new Point(rightX * 1.05, bottomY * 0.2), bottom)
            });
        }

        /// <summary>
        /// Crescent: the outer half circle (top → right → bottom) and an inner elliptical arc
        /// (radii 0.82r × 1.05r) back to the top, which leaves a crescent bulging to the right.
        /// </summary>
        public static ShapeOutline Moon(double radius)
        {
            var segments = new List<OutlineSegment>();
            AppendArc(segments, new Point(0, 0), radius, radius, -Math.PI / 2.0, Math.PI);

            double irx = radius * 0.82;
            double iry = radius * 1.05;
            // Inner ellipse center sits on the x axis so that the ellipse passes through (0, ±radius)
            double centerOffset = irx * Math.Sqrt(Math.Max(0.0, 1.0 - (radius / iry) * (radius / iry)));
            double startAngle = Math.Atan2(radius / iry, centerOffset / irx);
            AppendArc(segments, new Point(-centerOffset, 0), irx, iry, startAngle, -2.0 * startAngle);

            // Snap the arc ends exactly onto the horn tips (removes rounding noise from the arc math)
            segments[^1] = new OutlineSegment(segments[^1].C1, segments[^1].C2, new Point(0, -radius));
            return new ShapeOutline(new Point(0, -radius), segments);
        }

        /// <summary>Cherry blossom petal: base at the bottom, two lobes and the notch (cleft) at the top.</summary>
        public static ShapeOutline SakuraPetal(double radius)
        {
            double r = radius;
            var basePoint = new Point(0, r * 0.85);
            var leftLobe = new Point(-r * 0.38, -r * 0.95);
            var cleft = new Point(0, -r * 0.68);
            var rightLobe = new Point(r * 0.38, -r * 0.95);

            return new ShapeOutline(basePoint, new[]
            {
                new OutlineSegment(new Point(-r * 0.65, r * 0.35), new Point(-r * 0.70, -r * 0.55), leftLobe),
                new OutlineSegment(new Point(-r * 0.22, -r * 0.98), new Point(-r * 0.08, -r * 0.72), cleft),
                new OutlineSegment(new Point(r * 0.08, -r * 0.72), new Point(r * 0.22, -r * 0.98), rightLobe),
                new OutlineSegment(new Point(r * 0.70, -r * 0.55), new Point(r * 0.65, r * 0.35), basePoint)
            });
        }

        /// <summary>Cat slit pupil: two curves meeting in a point at the top and bottom.</summary>
        public static ShapeOutline CatSlit(double radiusX, double radiusY)
        {
            var top = new Point(0, -radiusY);
            var bottom = new Point(0, radiusY);
            return new ShapeOutline(top, new[]
            {
                new OutlineSegment(new Point(radiusX * 1.4, -radiusY * 0.4), new Point(radiusX * 1.4, radiusY * 0.4), bottom),
                new OutlineSegment(new Point(-radiusX * 1.4, radiusY * 0.4), new Point(-radiusX * 1.4, -radiusY * 0.4), top)
            });
        }

        /// <summary>
        /// Outline of a decorative symbol of the given size (shared by the WPF renderer and the SVG exporter).
        /// Returns null for <see cref="SymbolType.None"/>.
        /// </summary>
        public static ShapeOutline? ForSymbol(SymbolType type, double radius) => type switch
        {
            SymbolType.Star4 => Star(4, radius, 0.28),
            SymbolType.Star5 => Star(5, radius, 0.42),
            SymbolType.Heart => Heart(radius),
            SymbolType.Moon => Moon(radius),
            SymbolType.Cross => Cross(radius),
            SymbolType.Diamond => Diamond(radius * 0.7, radius),
            SymbolType.SakuraPetal => SakuraPetal(radius),
            _ => null
        };

        /// <summary>
        /// Outline of a pupil with corners (cat slit / heart / star), centered at (0, 0).
        /// Returns null for the round and oval pupils, which are drawn as ellipses and have no corners.
        /// </summary>
        public static ShapeOutline? ForPupil(PupilShape shape, double prx, double pry) => shape switch
        {
            PupilShape.CatSlit => CatSlit(prx * 0.45, pry * 1.15),
            PupilShape.Heart => Heart(prx * 0.95),
            PupilShape.Star => Star(5, prx * 1.05, 0.40),
            _ => null
        };

        private static ShapeOutline Polygon(IReadOnlyList<Point> vertices)
        {
            var segments = new List<OutlineSegment>(vertices.Count);
            for (int i = 0; i < vertices.Count; i++)
            {
                segments.Add(OutlineSegment.Line(vertices[i], vertices[(i + 1) % vertices.Count]));
            }
            return new ShapeOutline(vertices[0], segments);
        }

        /// <summary>Appends an axis-aligned elliptical arc as cubic Beziers (at most 90° each).</summary>
        private static void AppendArc(List<OutlineSegment> segments, Point center, double rx, double ry, double startAngle, double sweep)
        {
            int pieces = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweep) / (Math.PI / 2.0) - 1e-9));
            double delta = sweep / pieces;
            double k = 4.0 / 3.0 * Math.Tan(delta / 4.0);
            for (int i = 0; i < pieces; i++)
            {
                double a0 = startAngle + delta * i;
                double a1 = a0 + delta;
                var p0 = new Point(center.X + rx * Math.Cos(a0), center.Y + ry * Math.Sin(a0));
                var p1 = new Point(center.X + rx * Math.Cos(a1), center.Y + ry * Math.Sin(a1));
                var c1 = new Point(p0.X - k * rx * Math.Sin(a0), p0.Y + k * ry * Math.Cos(a0));
                var c2 = new Point(p1.X + k * rx * Math.Sin(a1), p1.Y - k * ry * Math.Cos(a1));
                segments.Add(new OutlineSegment(c1, c2, p1));
            }
        }

        // ------------------------------------------------------------------
        // Corner rounding
        // ------------------------------------------------------------------

        /// <summary>
        /// Returns this outline with its corners rounded. <paramref name="amount"/> runs from 0 (sharp, the
        /// outline is returned unchanged) to 1 (fully rounded). Each corner is cut back along both of its edges
        /// by the same length — at most half of the shorter edge, so neighbouring corners never overlap — and the
        /// cut points are joined by a curve that is tangent to both edges and stays inside the corner's triangle,
        /// so the result never self-intersects or flips.
        /// </summary>
        public ShapeOutline Rounded(double amount)
        {
            if (double.IsNaN(amount) || amount <= 0.0) return this;
            amount = Math.Min(1.0, amount);

            int n = Segments.Count;
            if (n < 2) return this;

            var curves = new Cubic[n];
            for (int i = 0; i < n; i++)
            {
                Point p0 = i == 0 ? Start : Segments[i - 1].End;
                curves[i] = new Cubic(p0, Segments[i]);
            }

            // Corner j sits between curve j (its end) and curve j+1 (its start)
            double cosThreshold = Math.Cos(CornerAngleThresholdDeg * Math.PI / 180.0);
            var cut = new double[n];
            for (int j = 0; j < n; j++)
            {
                var incoming = curves[j];
                var outgoing = curves[(j + 1) % n];
                Vector tIn = incoming.Tangent(1.0);
                Vector tOut = outgoing.Tangent(0.0);
                if (tIn.LengthSquared < 1e-18 || tOut.LengthSquared < 1e-18) continue;
                if (Vector.Multiply(tIn, tOut) >= cosThreshold) continue; // already smooth

                double limit = Math.Min(incoming.Length, outgoing.Length) * 0.5;
                limit = Math.Min(limit, incoming.TurnLimitedLength(fromEnd: true, MaxTurnAlongCurveDeg));
                limit = Math.Min(limit, outgoing.TurnLimitedLength(fromEnd: false, MaxTurnAlongCurveDeg));

                // Where curved edges bend away from the corner (heart tip, cat slit tips), the tangents at the cut
                // points turn further apart the deeper the cut goes, and the joining curve would grow into a long
                // spike (and past 180° no smooth join exists). Shrink the cut until the join turns at most a
                // little more than the corner itself does.
                double cornerTurn = Vector.AngleBetween(tIn, tOut);
                double maxJoinTurn = Math.Min(MaxJoinTurnDeg, Math.Abs(cornerTurn) + MaxExtraJoinTurnDeg);
                for (int guard = 0; guard < 40 && limit > 0; guard++)
                {
                    Vector a = incoming.Tangent(incoming.ParameterAtLength(incoming.Length - limit));
                    Vector b = outgoing.Tangent(outgoing.ParameterAtLength(limit));
                    if (Math.Abs(Vector.AngleBetween(a, b)) <= maxJoinTurn) break;
                    limit *= 0.9;
                }
                cut[j] = amount * limit;
            }

            // Trim every curve by the cuts of the corners at its two ends
            var trimmed = new Cubic[n];
            var u0s = new double[n];
            var u1s = new double[n];
            for (int i = 0; i < n; i++)
            {
                double startCut = cut[(i - 1 + n) % n];
                double endCut = cut[i];
                double u0 = curves[i].ParameterAtLength(startCut);
                double u1 = curves[i].ParameterAtLength(curves[i].Length - endCut);
                if (u1 < u0) u1 = u0 = (u0 + u1) * 0.5;
                u0s[i] = u0;
                u1s[i] = u1;
                trimmed[i] = curves[i].Sub(u0, u1);
            }

            var segments = new List<OutlineSegment>(n * 2);
            for (int i = 0; i < n; i++)
            {
                var cur = trimmed[i];
                if (cur.Length > 1e-9)
                {
                    segments.Add(cur.ToSegment());
                }

                if (cut[i] > 0.0)
                {
                    int k = (i + 1) % n;
                    // Tangents come from the untrimmed curves: a fully consumed edge has no direction of its own
                    segments.Add(CornerCurve(cur.P3, curves[i].Tangent(u1s[i]), trimmed[k].P0, curves[k].Tangent(u0s[k]), curves[i].P3));
                }
            }

            Point start = trimmed[0].P0;
            // The last segment must close the figure exactly on the start point
            var last = segments[^1];
            segments[^1] = new OutlineSegment(last.C1, last.C2, start, last.IsLine);
            return new ShapeOutline(start, segments);
        }

        /// <summary>
        /// Curve from <paramref name="a"/> (leaving along <paramref name="tA"/>) to <paramref name="b"/> (arriving along
        /// <paramref name="tB"/>). Uses the point where both tangent lines meet as the (quadratic) control point, which
        /// keeps the curve inside the triangle a / control / b; for straight edges that point is the original vertex.
        /// </summary>
        private static OutlineSegment CornerCurve(Point a, Vector tA, Point b, Vector tB, Point vertex)
        {
            Point control = vertex;
            double denom = tA.X * tB.Y - tA.Y * tB.X; // cross(tA, tB)
            if (Math.Abs(denom) > 1e-9)
            {
                Vector ab = b - a;
                double s = (ab.X * tB.Y - ab.Y * tB.X) / denom; // distance along tA from a
                double s2 = (tA.X * ab.Y - tA.Y * ab.X) / denom; // distance along tB back from b
                double chord = ab.Length;
                if (s > 0 && s2 > 0 && s < chord * 4.0 && s2 < chord * 4.0)
                {
                    control = a + tA * s;
                }
            }
            return new OutlineSegment(
                OutlineSegment.Lerp(a, control, 2.0 / 3.0),
                OutlineSegment.Lerp(b, control, 2.0 / 3.0),
                b);
        }

        // ------------------------------------------------------------------
        // Output
        // ------------------------------------------------------------------

        /// <summary>WPF geometry of this outline (closed and filled), centered at (0, 0).</summary>
        public PathGeometry ToPathGeometry()
        {
            var figure = new PathFigure { StartPoint = Start, IsClosed = true, IsFilled = true };
            for (int i = 0; i < Segments.Count; i++)
            {
                var seg = Segments[i];
                // A straight closing edge is implied by IsClosed (same as the original polygons)
                if (i == Segments.Count - 1 && seg.IsLine) break;
                figure.Segments.Add(seg.IsLine
                    ? new LineSegment(seg.End, true)
                    : new BezierSegment(seg.C1, seg.C2, seg.End, true));
            }
            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            return geometry;
        }

        /// <summary>
        /// SVG path data ("M … L … C … Z") of this outline moved to (<paramref name="offsetX"/>, <paramref name="offsetY"/>),
        /// optionally mirrored left-right first (right eye moon).
        /// </summary>
        public string ToSvgPathData(double offsetX, double offsetY, bool mirrorX = false)
        {
            double sx = mirrorX ? -1.0 : 1.0;
            string P(Point p) => $"{(offsetX + sx * p.X).ToString("F3", Inv)},{(offsetY + p.Y).ToString("F3", Inv)}";

            var sb = new StringBuilder();
            sb.Append("M ").Append(P(Start));
            for (int i = 0; i < Segments.Count; i++)
            {
                var seg = Segments[i];
                if (i == Segments.Count - 1 && seg.IsLine) break; // closed by Z
                if (seg.IsLine) sb.Append(" L ").Append(P(seg.End));
                else sb.Append(" C ").Append(P(seg.C1)).Append(' ').Append(P(seg.C2)).Append(' ').Append(P(seg.End));
            }
            sb.Append(" Z");
            return sb.ToString();
        }

        /// <summary>Points along the outline (each curve sampled <paramref name="samplesPerSegment"/> times), for tests and hit checks.</summary>
        public List<Point> Flatten(int samplesPerSegment = 24)
        {
            var points = new List<Point> { Start };
            Point p0 = Start;
            foreach (var seg in Segments)
            {
                var c = new Cubic(p0, seg);
                int samples = seg.IsLine ? 1 : samplesPerSegment;
                for (int k = 1; k <= samples; k++) points.Add(c.PointAt((double)k / samples));
                p0 = seg.End;
            }
            points.RemoveAt(points.Count - 1); // last point repeats Start
            return points;
        }

        // ------------------------------------------------------------------
        // Cubic Bezier math
        // ------------------------------------------------------------------

        private readonly struct Cubic
        {
            private const int LengthSamples = 32;

            public Point P0 { get; }
            public Point P1 { get; }
            public Point P2 { get; }
            public Point P3 { get; }
            public bool IsLine { get; }
            public double Length { get; }
            private readonly double[] _cumulative;

            public Cubic(Point p0, OutlineSegment seg) : this(p0, seg.C1, seg.C2, seg.End, seg.IsLine) { }

            public Cubic(Point p0, Point p1, Point p2, Point p3, bool isLine)
            {
                P0 = p0; P1 = p1; P2 = p2; P3 = p3; IsLine = isLine;
                _cumulative = new double[LengthSamples + 1];
                Point prev = p0;
                for (int k = 1; k <= LengthSamples; k++)
                {
                    Point cur = Eval(p0, p1, p2, p3, (double)k / LengthSamples);
                    _cumulative[k] = _cumulative[k - 1] + (cur - prev).Length;
                    prev = cur;
                }
                Length = _cumulative[LengthSamples];
            }

            private static Point Eval(Point p0, Point p1, Point p2, Point p3, double t)
            {
                double u = 1.0 - t;
                double a = u * u * u, b = 3 * u * u * t, c = 3 * u * t * t, d = t * t * t;
                return new Point(a * p0.X + b * p1.X + c * p2.X + d * p3.X, a * p0.Y + b * p1.Y + c * p2.Y + d * p3.Y);
            }

            public Point PointAt(double t) => Eval(P0, P1, P2, P3, t);

            /// <summary>Unit tangent (direction of travel) at parameter t.</summary>
            public Vector Tangent(double t)
            {
                if (IsLine)
                {
                    Vector line = P3 - P0;
                    return line.LengthSquared > 0 ? line / line.Length : line;
                }
                double u = 1.0 - t;
                Vector d = 3 * u * u * (P1 - P0) + 6 * u * t * (P2 - P1) + 3 * t * t * (P3 - P2);
                if (d.LengthSquared < 1e-18)
                {
                    // Degenerate control point at the end: step slightly inside the curve
                    double t2 = t < 0.5 ? t + 1e-3 : t - 1e-3;
                    d = t < 0.5 ? PointAt(t2) - PointAt(t) : PointAt(t) - PointAt(t2);
                }
                return d.LengthSquared > 0 ? d / d.Length : d;
            }

            /// <summary>Parameter at the given arc length from the start (clamped to the curve).</summary>
            public double ParameterAtLength(double length)
            {
                if (length <= 0) return 0.0;
                if (length >= Length) return 1.0;
                if (IsLine) return length / Length;
                for (int k = 1; k <= LengthSamples; k++)
                {
                    if (_cumulative[k] >= length)
                    {
                        double span = _cumulative[k] - _cumulative[k - 1];
                        double f = span > 0 ? (length - _cumulative[k - 1]) / span : 0.0;
                        return (k - 1 + f) / LengthSamples;
                    }
                }
                return 1.0;
            }

            /// <summary>Arc length from one end up to where the tangent has turned by <paramref name="maxTurnDeg"/>.</summary>
            public double TurnLimitedLength(bool fromEnd, double maxTurnDeg)
            {
                if (IsLine) return double.PositiveInfinity;
                double cosLimit = Math.Cos(maxTurnDeg * Math.PI / 180.0);
                Vector reference = Tangent(fromEnd ? 1.0 : 0.0);
                for (int k = 1; k <= LengthSamples; k++)
                {
                    double t = fromEnd ? 1.0 - (double)k / LengthSamples : (double)k / LengthSamples;
                    if (Vector.Multiply(Tangent(t), reference) < cosLimit)
                    {
                        int kPrev = k - 1;
                        return fromEnd ? Length - _cumulative[LengthSamples - kPrev] : _cumulative[kPrev];
                    }
                }
                return double.PositiveInfinity;
            }

            /// <summary>The part of this curve between parameters u0 and u1.</summary>
            public Cubic Sub(double u0, double u1)
            {
                if (u0 <= 0.0 && u1 >= 1.0) return this;
                if (IsLine)
                {
                    Point a = OutlineSegment.Lerp(P0, P3, u0);
                    Point b = OutlineSegment.Lerp(P0, P3, u1);
                    var line = OutlineSegment.Line(a, b);
                    return new Cubic(a, line.C1, line.C2, line.End, true);
                }
                var right = SplitRight(u0);
                double local = u0 >= 1.0 ? 0.0 : (u1 - u0) / (1.0 - u0);
                return right.SplitLeft(local);
            }

            private Cubic SplitLeft(double t)
            {
                Point a = OutlineSegment.Lerp(P0, P1, t), b = OutlineSegment.Lerp(P1, P2, t), c = OutlineSegment.Lerp(P2, P3, t);
                Point d = OutlineSegment.Lerp(a, b, t), e = OutlineSegment.Lerp(b, c, t);
                Point f = OutlineSegment.Lerp(d, e, t);
                return new Cubic(P0, a, d, f, false);
            }

            private Cubic SplitRight(double t)
            {
                Point a = OutlineSegment.Lerp(P0, P1, t), b = OutlineSegment.Lerp(P1, P2, t), c = OutlineSegment.Lerp(P2, P3, t);
                Point d = OutlineSegment.Lerp(a, b, t), e = OutlineSegment.Lerp(b, c, t);
                Point f = OutlineSegment.Lerp(d, e, t);
                return new Cubic(f, e, c, P3, false);
            }

            public OutlineSegment ToSegment() => new(P1, P2, P3, IsLine);
        }
    }
}
