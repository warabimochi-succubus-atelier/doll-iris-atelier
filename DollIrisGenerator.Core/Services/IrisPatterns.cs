using System;
using System.Collections.Generic;
using DollIrisGenerator.Core.Geometry;
using DollIrisGenerator.Models;

namespace DollIrisGenerator.Services
{
    /// <summary>One radial ray: its direction (radians, already mirrored for the right eye) and where it starts / ends.</summary>
    public readonly record struct RaySpec(double Angle, double InnerR, double OuterR, double WidthFactor);

    /// <summary>
    /// One 斑点: its center (iris-radius units around the iris center, x right, y down), the direction its long axis
    /// points (radians, in the same units), its size (0.4–1 of 大きさ) and its opacity (0.6–1 of 強さ).
    /// </summary>
    public readonly record struct SpotSpec(Point Position, double Angle, double SizeFactor, double AlphaFactor);

    /// <summary>One glitter grain: where it sits, its size (drawing units), and how it is drawn.</summary>
    public readonly record struct GrainSpec(Point Position, double Size, bool IsStar, bool HasFlare);

    /// <summary>
    /// Where the rays and glitter grains go. The screen / PDF / PNG renderer and the SVG exporter both lay
    /// their patterns out from here, so the outputs always agree.
    ///
    /// Each pattern keeps drawing from its own seeded random sequence exactly as earlier versions did; the
    /// newer settings (fiber look, fine rays, flares) take their randomness from separate sequences, so a
    /// saved design looks the same as before as long as those settings are left at 0.
    /// </summary>
    public static class IrisPatterns
    {
        // Seed offsets of the random sequences the newer settings use (the main rays use the seed itself,
        // the grains seed + 1000, as they always did)
        private const int RayWidthSeedOffset = 2000;
        private const int FineRaySeedOffset = 2500;
        private const int FlareSeedOffset = 3000;

        /// <summary>Total bend, in radians, of a ray at full うねり (RayCurve = ±1) from its root to its tip.</summary>
        public const double MaxRayBend = 0.8;

        /// <summary>Width of the fine rays relative to 線幅.</summary>
        public const double FineRayWidthFactor = 0.45;

        public static double InnerRayRadius(IrisParameters p, double baseR) =>
            Math.Max(baseR * p.PupilSizeRatio * 0.85, baseR * p.RayInnerRadiusRatio);

        /// <summary>The main rays, with each one's width factor (1 unless 太さのばらつき is set).</summary>
        public static List<RaySpec> MainRays(IrisParameters p, double baseR, int seed, bool isRightEye)
        {
            var rays = new List<RaySpec>(Math.Max(0, p.RayCount));
            if (p.RayCount <= 0) return rays;

            var rayRng = new Random(seed);
            var widthRng = new Random(seed + RayWidthSeedOffset);
            double innerR = InnerRayRadius(p, baseR);
            double outerR = baseR * p.RayLengthRatio;

            for (int i = 0; i < p.RayCount; i++)
            {
                double baseAngle = (2.0 * Math.PI * i) / p.RayCount;
                double jitter = (rayRng.NextDouble() - 0.5) * (p.RayRandomness * 0.5);
                double angle = baseAngle + jitter;
                if (isRightEye) angle = Math.PI - angle;

                double lenVariation = 1.0 + (rayRng.NextDouble() - 0.5) * p.RayRandomness * 0.6;
                double curOuterR = Math.Min(baseR * 0.96, outerR * lenVariation);
                double curInnerR = innerR * (0.9 + rayRng.NextDouble() * 0.2);

                rays.Add(new RaySpec(angle, curInnerR, curOuterR, WidthFactor(widthRng, p.RayWidthVariation)));
            }
            return rays;
        }

        /// <summary>
        /// The fine rays (細線レイヤー): one between every two main rays, starting a little further out and
        /// reaching a varied length, so the iris reads as many thin fibers rather than a few spokes.
        /// </summary>
        public static List<RaySpec> FineRays(IrisParameters p, double baseR, int seed, bool isRightEye)
        {
            var rays = new List<RaySpec>(Math.Max(0, p.RayCount));
            if (p.RayCount <= 0 || p.RayFineLayer <= 0.01) return rays;

            var rng = new Random(seed + FineRaySeedOffset);
            double innerR = InnerRayRadius(p, baseR);
            double outerR = baseR * p.RayLengthRatio;
            double halfStep = Math.PI / p.RayCount;

            for (int i = 0; i < p.RayCount; i++)
            {
                double angle = (2.0 * Math.PI * i) / p.RayCount + halfStep
                    + (rng.NextDouble() - 0.5) * (0.2 + p.RayRandomness * 0.5) * halfStep * 2.0;
                if (isRightEye) angle = Math.PI - angle;

                double curInnerR = innerR * (1.0 + rng.NextDouble() * 0.3);
                double curOuterR = Math.Min(baseR * 0.96, outerR * (0.75 + rng.NextDouble() * 0.4));
                if (curOuterR <= curInnerR) curOuterR = Math.Min(baseR * 0.96, curInnerR * 1.1);
                rays.Add(new RaySpec(angle, curInnerR, curOuterR, FineRayWidthFactor * WidthFactor(rng, p.RayWidthVariation)));
            }
            return rays;
        }

        // From about a third (v = 1) to twice (v = 1) the width; one draw per ray whatever v is, so the sequence never shifts
        private static double WidthFactor(Random rng, double variation)
        {
            double r = rng.NextDouble();
            return 1.0 + variation * (r * 1.65 - 0.65);
        }

        /// <summary>Point at radius <paramref name="r"/> in direction <paramref name="angle"/>, stretched onto the iris ellipse.</summary>
        public static Point OnIris(Point center, double rx, double ry, double baseR, double r, double angle) =>
            new(center.X + r * Math.Cos(angle) * (rx / baseR), center.Y + r * Math.Sin(angle) * (ry / baseR));

        /// <summary>
        /// Outline of one fiber-style ray: a band along the ray (bent by うねり), <paramref name="width"/> wide
        /// at its root and narrowing toward the tip by 先細り, with round ends like the plain lines.
        /// </summary>
        public static ShapeOutline FiberOutline(Point center, double rx, double ry, double baseR, RaySpec ray,
            double width, double taper, double curve, bool isRightEye)
        {
            // The swirl turns the other way in the mirrored (right) eye
            double bend = (isRightEye ? -curve : curve) * MaxRayBend;
            int n = Math.Abs(bend) > 1e-4 ? 14 : 1;

            var mid = new Point[n + 1];
            var half = new double[n + 1];
            for (int k = 0; k <= n; k++)
            {
                double t = (double)k / n;
                double r = ray.InnerR + (ray.OuterR - ray.InnerR) * t;
                mid[k] = OnIris(center, rx, ry, baseR, r, ray.Angle + bend * t);
                // Taper 1: about 1.35× wide at the root, down to a fine point at the tip
                half[k] = 0.5 * width * Math.Max(0.06, 1.0 + 0.35 * taper - 1.3 * taper * t);
            }

            var left = new List<Point>(n + 1);
            var right = new List<Point>(n + 1);
            Vector startDir = default, endDir = default;
            for (int k = 0; k <= n; k++)
            {
                var dir = mid[Math.Min(n, k + 1)] - mid[Math.Max(0, k - 1)];
                if (dir.Length < 1e-12) dir = new Vector(Math.Cos(ray.Angle), Math.Sin(ray.Angle));
                dir.Normalize();
                if (k == 0) startDir = dir;
                if (k == n) endDir = dir;
                var normal = new Vector(-dir.Y, dir.X);
                left.Add(mid[k] + normal * half[k]);
                right.Add(mid[k] - normal * half[k]);
            }

            var pts = new List<Point>(2 * n + 16);
            pts.AddRange(left);
            AppendCap(pts, mid[n], endDir, half[n]);
            for (int k = n; k >= 0; k--) pts.Add(right[k]);
            AppendCap(pts, mid[0], -startDir, half[0]);
            return ShapeOutline.Polygon(pts);
        }

        // Half circle around the end of the band, from its left edge round the front to its right edge
        // (seen walking along dir), so the band ends round like a line with round caps
        private static void AppendCap(List<Point> pts, Point c, Vector dir, double radius)
        {
            const int Steps = 6;
            var normal = new Vector(-dir.Y, dir.X);
            for (int s = 1; s < Steps; s++)
            {
                double a = Math.PI * s / Steps;
                pts.Add(c + normal * (radius * Math.Cos(a)) + dir * (radius * Math.Sin(a)));
            }
        }

        /// <summary>
        /// The glitter grains. <paramref name="styleUnit"/> converts grain sizes (style units) into drawing units;
        /// <paramref name="minSpan"/> is the smallest band (drawing units) the grains are spread over.
        /// </summary>
        public static List<GrainSpec> Grains(IrisParameters p, Point center, double rx, double ry, double baseR,
            int seed, bool isRightEye, double styleUnit, double minSpan)
        {
            var grains = new List<GrainSpec>(Math.Max(0, p.ParticleCount));
            if (p.ParticleCount <= 0) return grains;

            var rng = new Random(seed + 1000);
            var flareRng = new Random(seed + FlareSeedOffset);
            double minR = baseR * p.PupilSizeRatio * 0.95;
            double maxR = baseR * p.ParticleRadiusRatio;

            for (int i = 0; i < p.ParticleCount; i++)
            {
                double dist = minR + rng.NextDouble() * Math.Max(minSpan, maxR - minR);
                double angle = rng.NextDouble() * 2.0 * Math.PI;
                if (isRightEye) angle = Math.PI - angle;

                double size = (p.ParticleMinSize + rng.NextDouble() * Math.Max(0.1, p.ParticleMaxSize - p.ParticleMinSize)) * styleUnit;
                bool flare = flareRng.NextDouble() < p.ParticleFlareRatio;
                grains.Add(new GrainSpec(OnIris(center, rx, ry, baseR, dist, angle), size, i % 5 == 0, flare));
            }
            return grains;
        }

        private const int FacetSeedOffset = 5000;

        /// <summary>Strongest facet alpha (at 宝石カット 100 % and a facet's full shade).</summary>
        public const double MaxFacetAlpha = 0.75;

        /// <summary>
        /// Gem-cut facets (宝石カット) of the pupil, around the pupil center (0, 0) with radii <paramref name="prx"/> /
        /// <paramref name="pry"/>. A ring of "table" triangles round a point a little above the center, and around it
        /// three crown facets per step reaching well past the pupil (the renderers clip them to the pupil's outline).
        /// Shade is -1..1: positive facets are lightened (white), negative ones darkened (black).
        /// </summary>
        public static List<(Point[] Polygon, double Shade)> PupilFacets(double prx, double pry, int count, int seed, bool mirror)
        {
            var facets = new List<(Point[], double)>(count * 4);
            if (count <= 0 || prx <= 0 || pry <= 0) return facets;

            var rng = new Random(seed + FacetSeedOffset);
            var apex = new Point(0, -0.08 * pry);
            double inX = 0.5 * prx, inY = 0.5 * pry;
            double reach = 3.0 * Math.Max(prx, pry);

            Point Inner(double a) => new(apex.X + inX * Math.Cos(a), apex.Y + inY * Math.Sin(a));
            Point Outer(double a) => new(apex.X + reach * Math.Cos(a), apex.Y + reach * Math.Sin(a));
            double Shade(double sign) => sign * (0.25 + 0.75 * rng.NextDouble());

            for (int i = 0; i < count; i++)
            {
                double a0 = -Math.PI / 2.0 + 2.0 * Math.PI * i / count;
                double a1 = -Math.PI / 2.0 + 2.0 * Math.PI * (i + 1) / count;
                double am = (a0 + a1) / 2.0;
                double sign = i % 2 == 0 ? 1.0 : -1.0;

                // Table, then the crown: (inner edge → outer middle), and the two triangles either side of it
                facets.Add((new[] { apex, Inner(a0), Inner(a1) }, Shade(sign)));
                facets.Add((new[] { Inner(a0), Inner(a1), Outer(am) }, Shade(-sign)));
                facets.Add((new[] { Inner(a0), Outer(am), Outer(a0) }, Shade(sign)));
                facets.Add((new[] { Inner(a1), Outer(a1), Outer(am) }, Shade(-sign * 0.6)));
            }

            if (mirror)
            {
                for (int k = 0; k < facets.Count; k++)
                {
                    var (poly, shade) = facets[k];
                    var m = new Point[poly.Length];
                    for (int j = 0; j < poly.Length; j++) m[j] = new Point(-poly[j].X, poly[j].Y);
                    facets[k] = (m, shade);
                }
            }
            return facets;
        }

        private const int IrisFacetSeedOffset = 5500;

        /// <summary>Strongest alpha of a 宝石カット面 facet (at strength 100 % and a facet's full shade).</summary>
        public const double MaxIrisFacetAlpha = 0.9;

        /// <summary>Radius (iris radii) the outer facets reach: past the iris, so shaped outlines' corners are covered too.</summary>
        public const double IrisFacetReach = 1.5;

        /// <summary>
        /// 宝石カット面: a brilliant cut seen from above, over the whole iris, in iris-radius units around (0, 0)
        /// (x right, y down; the caller stretches it onto the iris like the other patterns). <paramref name="count"/>
        /// table corners, the first at the top. From the center out: the table (N-gon, radius
        /// <paramref name="tableSize"/>), a star facet on each table edge (tip halfway to the rim), a kite from each
        /// table corner out to the girdle, and two upper-girdle triangles between neighbouring kites. The outer points
        /// lie at <see cref="IrisFacetReach"/> (the iris clip cuts them at the rim).
        ///
        /// Shade (-1 = black … 1 = white): <paramref name="bias"/> × the facet's direction (darkest when it points at
        /// <paramref name="shadeAngle"/>) + (1 − bias / 2) × a light / dark alternation by facet kind, plus a small seeded
        /// jitter. The table stays faintly light. <paramref name="mirror"/> mirrors the result left-right (right eye:
        /// the dark side is mirrored with it).
        /// </summary>
        public static List<(Point[] Polygon, double Shade)> IrisFacets(int count, double tableSize, double shadeAngle, double bias,
            int seed, bool mirror)
        {
            var facets = new List<(Point[], double)>(count * 4 + 1);
            if (count < 3) return facets;

            var rng = new Random(seed + IrisFacetSeedOffset);
            double t = tableSize, s = tableSize + 0.5 * (1.0 - tableSize), g = IrisFacetReach;
            double step = 2.0 * Math.PI / count;
            double dark = shadeAngle * Math.PI / 180.0;

            Point At(double r, double a) => new(r * Math.Cos(a), r * Math.Sin(a));
            double Vertex(int i) => -Math.PI / 2.0 + step * i;
            double Mid(int i) => Vertex(i) + step / 2.0;
            double Shade(Point[] poly, double alternate)
            {
                double cx = 0, cy = 0;
                foreach (var q in poly) { cx += q.X; cy += q.Y; }
                double direction = -Math.Cos(Math.Atan2(cy, cx) - dark); // -1 on the dark side
                double jitter = (rng.NextDouble() - 0.5) * 0.3;
                // The alternation keeps neighbouring facets apart even when the direction dominates
                return Math.Clamp(bias * direction + (1.0 - 0.5 * bias) * alternate + jitter, -1.0, 1.0);
            }

            var table = new Point[count];
            for (int i = 0; i < count; i++) table[i] = At(t, Vertex(i));
            facets.Add((table, 0.2 + (rng.NextDouble() - 0.5) * 0.1));

            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                double sign = i % 2 == 0 ? 1.0 : -1.0;
                var star = new[] { At(t, Vertex(i)), At(t, Vertex(next)), At(s, Mid(i)) };
                facets.Add((star, Shade(star, 0.7 * sign)));
                var kite = new[] { At(t, Vertex(i)), At(s, Mid(i - 1 + count)), At(g, Vertex(i)), At(s, Mid(i)) };
                facets.Add((kite, Shade(kite, -0.5 * sign)));
                var girdleA = new[] { At(s, Mid(i)), At(g, Vertex(i)), At(g, Mid(i)) };
                facets.Add((girdleA, Shade(girdleA, 0.4)));
                var girdleB = new[] { At(s, Mid(i)), At(g, Mid(i)), At(g, Vertex(next)) };
                facets.Add((girdleB, Shade(girdleB, -0.4)));
            }

            if (mirror)
            {
                for (int k = 0; k < facets.Count; k++)
                {
                    var (poly, shade) = facets[k];
                    var m = new Point[poly.Length];
                    for (int j = 0; j < poly.Length; j++) m[j] = new Point(-poly[j].X, poly[j].Y);
                    facets[k] = (m, shade);
                }
            }
            return facets;
        }

        private const int CollaretteSeedOffset = 6500;

        /// <summary>Fewest points round a 瞳孔まわりの輪 edge (so a smooth edge, ギザギザ 0, still reads as a circle).</summary>
        public const int MinCollarettePoints = 96;

        /// <summary>
        /// 瞳孔まわりの輪: the outer edge, in iris-radius units around (0, 0) (x right, y down; the caller stretches it
        /// onto the iris), as (direction, radius) pairs going round once. <paramref name="spikes"/> peaks alternate with
        /// valleys joined by straight lines, each line split into equal steps so the edge has at least
        /// <see cref="MinCollarettePoints"/> points. Peak i points at −90° + 360° / N × (i ± 0.15), the valley sits halfway
        /// to the next peak. Peaks lie at <paramref name="pupil"/> + <paramref name="width"/> × (1 − jagged × 0.2 × r),
        /// valleys at pupil + width × (1 − jagged × (0.45 + 0.35 × r)), r from the seed.
        /// <paramref name="mirror"/> mirrors the edge left-right (right eye).
        /// </summary>
        public static (double Angle, double Radius)[] CollaretteEdge(double pupil, double width, double jagged, int spikes,
            int seed, bool mirror)
        {
            spikes = Math.Max(3, spikes);
            var rng = new Random(seed + CollaretteSeedOffset);
            double step = 2.0 * Math.PI / spikes;
            var peakAngle = new double[spikes];
            var peakR = new double[spikes];
            var valleyR = new double[spikes];
            for (int i = 0; i < spikes; i++)
            {
                peakAngle[i] = -Math.PI / 2.0 + step * (i + (rng.NextDouble() - 0.5) * 0.3);
                peakR[i] = pupil + width * (1.0 - jagged * 0.2 * rng.NextDouble());
                valleyR[i] = pupil + width * (1.0 - jagged * (0.45 + 0.35 * rng.NextDouble()));
            }

            int perLine = Math.Max(1, (int)Math.Ceiling(MinCollarettePoints / (2.0 * spikes)));
            var edge = new (double, double)[spikes * 2 * perLine];
            int n = 0;
            for (int i = 0; i < spikes; i++)
            {
                double next = i + 1 < spikes ? peakAngle[i + 1] : peakAngle[0] + 2.0 * Math.PI;
                double valleyAngle = (peakAngle[i] + next) / 2.0;
                double nextR = peakR[(i + 1) % spikes];
                // peak i → valley i, then valley i → peak i + 1 (its point is the next line's first)
                for (int k = 0; k < perLine; k++)
                {
                    double t = (double)k / perLine;
                    edge[n++] = (peakAngle[i] + (valleyAngle - peakAngle[i]) * t, peakR[i] + (valleyR[i] - peakR[i]) * t);
                }
                for (int k = 0; k < perLine; k++)
                {
                    double t = (double)k / perLine;
                    edge[n++] = (valleyAngle + (next - valleyAngle) * t, valleyR[i] + (nextR - valleyR[i]) * t);
                }
            }
            if (mirror)
            {
                for (int k = 0; k < edge.Length; k++) edge[k] = (Math.PI - edge[k].Item1, edge[k].Item2);
            }
            return edge;
        }

        private const int SpotSeedOffset = 7500;

        /// <summary>
        /// 斑点: <paramref name="count"/> spots between 1.1 × the pupil's radius and <paramref name="outer"/>, spread
        /// evenly over the area (r = √(a² + (b² − a²) × random)), each pointing along its radius ± 15°. Every spot takes
        /// the same five draws, so changing the count keeps the first spots where they were. <paramref name="mirror"/>
        /// mirrors them left-right (right eye).
        /// </summary>
        public static List<SpotSpec> Spots(int count, double pupil, double outer, int seed, bool mirror)
        {
            var spots = new List<SpotSpec>(Math.Max(0, count));
            var rng = new Random(seed + SpotSeedOffset);
            double a = pupil * 1.1, b = Math.Max(a, outer);
            for (int i = 0; i < count; i++)
            {
                double angle = rng.NextDouble() * 2.0 * Math.PI;
                double r = Math.Sqrt(a * a + (b * b - a * a) * rng.NextDouble());
                double size = 0.4 + 0.6 * rng.NextDouble();
                double alpha = 0.6 + 0.4 * rng.NextDouble();
                double tilt = (rng.NextDouble() - 0.5) * (Math.PI / 6.0);
                double axis = angle + tilt;
                var position = new Point(r * Math.Cos(angle), r * Math.Sin(angle));
                if (mirror)
                {
                    position = new Point(-position.X, position.Y);
                    axis = Math.PI - axis;
                }
                spots.Add(new SpotSpec(position, axis, size, alpha));
            }
            return spots;
        }

        /// <summary>Cross-shaped flare (a thin four-point star) of a grain, around (0, 0).</summary>
        public static ShapeOutline Flare(double size, double length) => ShapeOutline.Star(4, size * length * 0.6, 0.07);
    }
}
