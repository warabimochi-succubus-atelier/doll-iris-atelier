using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using DollIrisGenerator.ViewModels;
using Xunit;
using Point = DollIrisGenerator.Core.Geometry.Point;
using Rect = DollIrisGenerator.Core.Geometry.Rect;
using Vector = DollIrisGenerator.Core.Geometry.Vector;

namespace DollIrisGenerator.Tests
{
    /// <summary>Corner rounding of the pupil shapes and decorative symbols (角の丸み).</summary>
    public class ShapeOutlineTests
    {
        private static readonly double[] Amounts = Enumerable.Range(0, 21).Select(i => i / 20.0).ToArray();

        public static IEnumerable<object[]> AllCorneredShapes()
        {
            foreach (SymbolType type in Enum.GetValues<SymbolType>())
            {
                // Every shape with corners (the circle has none)
                if (type != SymbolType.None && type != SymbolType.Circle) yield return new object[] { $"symbol:{type}" };
            }
            foreach (var shape in new[] { PupilShape.CatSlit, PupilShape.Heart, PupilShape.Star5 })
            {
                yield return new object[] { $"pupil:{shape}" };
            }
            // Highlights with corners (角の丸み); the circle has none
            foreach (var shape in new[] { HighlightShape.Star4, HighlightShape.Diamond }) // the shapes highlights draw differently
            {
                yield return new object[] { $"highlight:{shape}" };
            }
        }

        private static ShapeOutline Build(string name)
        {
            var parts = name.Split(':');
            return parts[0] switch
            {
                "symbol" => ShapeOutline.ForSymbol(Enum.Parse<SymbolType>(parts[1]), 50.0)!,
                "highlight" => ShapeOutline.ForHighlight(Enum.Parse<HighlightShape>(parts[1]), 50.0),
                _ => ShapeOutline.ForPupil(Enum.Parse<PupilShape>(parts[1]), 30.0, 30.0)!
            };
        }

        private static void RunOnSta(Action body)
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try { body(); }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) throw new Exception(failure.Message, failure);
        }

        private static double SignedArea(IReadOnlyList<Point> pts)
        {
            double area = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % pts.Count];
                area += a.X * b.Y - b.X * a.Y;
            }
            return area * 0.5;
        }

        private static double Cross(Point o, Point a, Point b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);

        private static bool SegmentsCross(Point a, Point b, Point c, Point d)
        {
            double d1 = Cross(c, d, a), d2 = Cross(c, d, b), d3 = Cross(a, b, c), d4 = Cross(a, b, d);
            return ((d1 > 1e-9 && d2 < -1e-9) || (d1 < -1e-9 && d2 > 1e-9))
                && ((d3 > 1e-9 && d4 < -1e-9) || (d3 < -1e-9 && d4 > 1e-9));
        }

        /// <summary>Index pair of the first two non-neighbouring edges that cross, or null for a simple polygon.</summary>
        private static (int, int)? FindSelfIntersection(IReadOnlyList<Point> pts)
        {
            int n = pts.Count;
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 2; j < n; j++)
                {
                    if (i == 0 && j == n - 1) continue; // neighbours through the closing edge
                    if (SegmentsCross(pts[i], pts[(i + 1) % n], pts[j], pts[(j + 1) % n])) return (i, j);
                }
            }
            return null;
        }

        /// <summary>Largest change of direction between consecutive edges of the flattened outline, in degrees.</summary>
        private static double MaxTurnDeg(IReadOnlyList<Point> pts)
        {
            double max = 0;
            int n = pts.Count;
            for (int i = 0; i < n; i++)
            {
                Vector a = pts[(i + 1) % n] - pts[i];
                Vector b = pts[(i + 2) % n] - pts[(i + 1) % n];
                if (a.LengthSquared < 1e-12 || b.LengthSquared < 1e-12) continue;
                max = Math.Max(max, Math.Abs(Vector.AngleBetween(a, b)));
            }
            return max;
        }

        [Fact]
        public void TestZeroRoundingKeepsTheOutlineUnchanged()
        {
            foreach (var row in AllCorneredShapes())
            {
                var outline = Build((string)row[0]);
                Assert.Same(outline, outline.Rounded(0.0));
                Assert.Same(outline, outline.Rounded(double.NaN));
            }
        }

        [Fact]
        public void TestZeroRoundingStarMatchesTheOriginalPolygon()
        {
            // The sharp star must keep exactly the vertices the renderer always drew
            var pts = ShapeOutline.Star(5, 50.0, 0.42).Flatten();
            Assert.Equal(10, pts.Count);
            for (int i = 0; i < 10; i++)
            {
                double angle = i * Math.PI / 5.0 - Math.PI / 2.0;
                double r = i % 2 == 0 ? 50.0 : 21.0;
                Assert.Equal(r * Math.Cos(angle), pts[i].X, 9);
                Assert.Equal(r * Math.Sin(angle), pts[i].Y, 9);
            }
        }

        [Theory]
        [MemberData(nameof(AllCorneredShapes))]
        public void TestRoundedOutlinesNeverSelfIntersectOrFlip(string name)
        {
            var outline = Build(name);
            double originalArea = SignedArea(outline.Flatten(48));
            Assert.True(Math.Abs(originalArea) > 1.0, $"{name}: degenerate original");

            foreach (double amount in Amounts)
            {
                var rounded = outline.Rounded(amount);
                Assert.Equal(rounded.Start.X, rounded.Segments[^1].End.X, 9);
                Assert.Equal(rounded.Start.Y, rounded.Segments[^1].End.Y, 9);

                var pts = rounded.Flatten(48);
                Assert.Null(FindSelfIntersection(pts));

                double area = SignedArea(pts);
                Assert.True(Math.Sign(area) == Math.Sign(originalArea), $"{name} @ {amount}: outline flipped");
                // Rounding only shaves corners off; the shape keeps most of its area
                Assert.True(Math.Abs(area) > Math.Abs(originalArea) * 0.4, $"{name} @ {amount}: collapsed ({area} vs {originalArea})");
            }
        }

        [Theory]
        [MemberData(nameof(AllCorneredShapes))]
        public void TestFullRoundingLeavesNoSharpCorner(string name)
        {
            var sharp = Build(name);
            var rounded = sharp.Rounded(1.0);
            double sharpTurn = MaxTurnDeg(sharp.Flatten(64));
            double roundedTurn = MaxTurnDeg(rounded.Flatten(64));
            Assert.True(roundedTurn < 20.0, $"{name}: still a {roundedTurn:F1}° corner when fully rounded (sharp: {sharpTurn:F1}°)");
        }

        [Theory]
        [MemberData(nameof(AllCorneredShapes))]
        public void TestEveryCornerIsVisiblyRoundedAtFullRounding(string name)
        {
            var sharp = Build(name);
            var rounded = sharp.Rounded(1.0).Flatten(64);
            double size = sharp.Flatten(64).Max(p => Math.Max(Math.Abs(p.X), Math.Abs(p.Y)));

            int corners = 0;
            for (int i = 0; i < sharp.Segments.Count; i++)
            {
                var seg = sharp.Segments[i];
                var next = sharp.Segments[(i + 1) % sharp.Segments.Count];
                Vector tIn = seg.End - seg.C2;
                Vector tOut = next.C1 - seg.End;
                if (Math.Abs(Vector.AngleBetween(tIn, tOut)) < 2.0) continue; // smooth join (moon arc pieces)

                corners++;
                double nearest = rounded.Min(p => (p - seg.End).Length);
                Assert.True(nearest > size * 0.01, $"{name}: corner {seg.End} only moved {nearest:F3} (size {size:F1})");
            }
            Assert.True(corners >= 2, $"{name}: no corners found");
        }

        [Theory]
        [MemberData(nameof(AllCorneredShapes))]
        public void TestRoundingChangesTheShapeGraduallyWithoutJumps(string name)
        {
            // A small slider step must give a small change (no sudden spike near 100%)
            var outline = Build(name);
            double size = outline.Flatten(64).Max(p => Math.Max(Math.Abs(p.X), Math.Abs(p.Y)));
            Rect? previous = null;
            foreach (double amount in Amounts)
            {
                var pts = outline.Rounded(amount).Flatten(64);
                double minX = pts.Min(p => p.X), minY = pts.Min(p => p.Y);
                var box = new Rect(minX, minY, pts.Max(p => p.X) - minX, pts.Max(p => p.Y) - minY);
                if (previous is Rect prev)
                {
                    double change = new[] { box.Left - prev.Left, box.Top - prev.Top, box.Right - prev.Right, box.Bottom - prev.Bottom }.Max(Math.Abs);
                    Assert.True(change < size * 0.06, $"{name}: jumped by {change:F2} at {amount} (size {size:F1})");
                }
                previous = box;
            }
        }

        [Fact]
        public void TestHeartHasRoundLobesAVisibleCleftAndIsCentered()
        {
            var heart = ShapeOutline.Heart(50.0);
            var pts = heart.Flatten(64);
            var box = heart.GetBounds();

            // Centered on the symbol position, ±0.8r across and ±0.656r up and down
            Assert.Equal(-40.0, box.Left, 0);
            Assert.Equal(40.0, box.Right, 0);
            Assert.Equal(-32.8, box.Top, 0);
            Assert.Equal(32.8, box.Bottom, 0);

            // The cleft sits clearly below the lobe tops (not a flat top)
            double cleftY = pts.Where(p => Math.Abs(p.X) < 0.5).Min(p => p.Y);
            Assert.True(cleftY - box.Top > 50.0 * 0.15, $"cleft only {cleftY - box.Top:F2} below the lobes");

            // Only the tip and the cleft are corners; the lobes join the sides smoothly
            int corners = 0;
            for (int i = 0; i < heart.Segments.Count; i++)
            {
                var seg = heart.Segments[i];
                var next = heart.Segments[(i + 1) % heart.Segments.Count];
                if (Math.Abs(Vector.AngleBetween(seg.End - seg.C2, next.C1 - seg.End)) >= 2.0) corners++;
            }
            Assert.Equal(2, corners);
        }

        [Fact]
        public void TestCrossIsALatinCrossWithTheLongArmBelow()
        {
            var cross = ShapeOutline.Cross(50.0);
            var box = cross.GetBounds();
            Assert.Equal(-32.5, box.Left, 6);
            Assert.Equal(32.5, box.Right, 6);
            Assert.Equal(-50.0, box.Top, 6);
            Assert.Equal(50.0, box.Bottom, 6);

            // The crossbar is the widest part; it sits above the center, so the lower arm is the long one
            var bar = cross.Flatten().Where(p => Math.Abs(Math.Abs(p.X) - 32.5) < 1e-9).ToList();
            Assert.Equal(4, bar.Count);
            double barCenter = bar.Average(p => p.Y);
            Assert.Equal(-20.0, barCenter, 6);
            Assert.True(box.Bottom - barCenter > 2.0 * (barCenter - box.Top));
        }

        public static IEnumerable<object[]> StretchCases()
        {
            foreach (var row in AllCorneredShapes())
            {
                yield return new object[] { row[0], 0.7, 1.3, 0.6, 0.6 };
                yield return new object[] { row[0], 2.0, 0.5, 1.5, 0.5 };
            }
        }

        [Theory]
        [MemberData(nameof(StretchCases))]
        public void TestStretchedOutlinesStayClosedAndSimple(string name, double top, double bottom, double left, double right)
        {
            foreach (double rounding in new[] { 0.0, 0.5, 1.0 })
            {
                var rounded = Build(name).Rounded(rounding);
                var stretched = rounded.Stretched(top, bottom, left, right);
                Assert.Equal(stretched.Start.X, stretched.Segments[^1].End.X, 9);
                Assert.Equal(stretched.Start.Y, stretched.Segments[^1].End.Y, 9);

                var pts = stretched.Flatten(48);
                Assert.Null(FindSelfIntersection(pts));
                Assert.Equal(Math.Sign(SignedArea(rounded.Flatten(48))), Math.Sign(SignedArea(pts)));

                // Each side of the box moves out by its own length (the box is sampled: allow 1% of the size)
                var before = rounded.GetBounds();
                var after = stretched.GetBounds();
                // An edge on the far side of the center (rounded moon horns) moves by that side's length
                double tolerance = 0.01 * 50.0;
                double X(double edge) => edge * (edge < 0 ? left : right);
                double Y(double edge) => edge * (edge < 0 ? top : bottom);
                Assert.Equal(Y(before.Top), after.Top, tolerance);
                Assert.Equal(Y(before.Bottom), after.Bottom, tolerance);
                Assert.Equal(X(before.Left), after.Left, tolerance);
                Assert.Equal(X(before.Right), after.Right, tolerance);
            }
        }

        [Fact]
        public void TestStretchingByOneKeepsTheOutlineAndPointsMoveExactly()
        {
            var star = ShapeOutline.Star(4, 50.0, 0.28);
            Assert.Same(star, star.Stretched(1, 1, 1, 1));

            // 4芒星 → とがった十字: the tips land exactly on the stretched lengths
            var pts = star.Stretched(0.7, 1.3, 0.6, 0.6).Flatten();
            Assert.Contains(pts, p => Math.Abs(p.X) < 1e-9 && Math.Abs(p.Y + 35.0) < 1e-9);
            Assert.Contains(pts, p => Math.Abs(p.X) < 1e-9 && Math.Abs(p.Y - 65.0) < 1e-9);
            Assert.Contains(pts, p => Math.Abs(p.X + 30.0) < 1e-9 && Math.Abs(p.Y) < 1e-9);
            Assert.Contains(pts, p => Math.Abs(p.X - 30.0) < 1e-9 && Math.Abs(p.Y) < 1e-9);
        }

        /// <summary>Box of the first path inside the symbol group, relative to the iris center (the symbol sits there).</summary>
        private static Rect SymbolPathBox(string svg, string groupId)
        {
            int group = svg.IndexOf($"<g id=\"{groupId}\"", StringComparison.Ordinal);
            Assert.True(group >= 0, groupId);
            int at = svg.IndexOf("<path d=\"", group, StringComparison.Ordinal) + 9;
            string d = svg.Substring(at, svg.IndexOf('"', at) - at);
            var numbers = System.Text.RegularExpressions.Regex.Matches(d, @"-?\d+(\.\d+)?")
                .Select(m => double.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture)).ToList();
            var xs = numbers.Where((_, i) => i % 2 == 0).ToList();
            var ys = numbers.Where((_, i) => i % 2 == 1).ToList();
            return new Rect(xs.Min(), ys.Min(), xs.Max() - xs.Min(), ys.Max() - ys.Min());
        }

        [Fact]
        public void TestRightEyeSymbolIsTheMirrorImageOfTheStretchedLeftOne()
        {
            var p = new IrisParameters { SizeMm = 10.0 };
            p.Symbols.Clear();
            p.Symbols.Add(new DecorativeSymbolItem
            {
                Type = SymbolType.Star4, Size = 0.3, PositionX = 0.1, PositionY = 0, Enabled = true,
                StretchLeft = 2.0, StretchRight = 0.5
            });
            // An unstretched diamond at the same place marks the symbol's center
            p.Symbols.Add(new DecorativeSymbolItem { Type = SymbolType.Diamond, Size = 0.1, PositionX = 0.1, PositionY = 0, Enabled = true });

            foreach (bool isRightEye in new[] { false, true })
            {
                string svg = SvgExporter.GenerateSingleEyeSvg(p, isRightEye);
                string eye = isRightEye ? "eye_right" : "eye_left";
                var star = SymbolPathBox(svg, $"{eye}_symbol_Star4");
                var mark = SymbolPathBox(svg, $"{eye}_symbol_Diamond");
                double center = (mark.Left + mark.Right) / 2.0;
                double toLeft = center - star.Left, toRight = star.Right - center;
                // Left eye: 4 times as far to the left as to the right; the right eye is its mirror image
                Assert.Equal(isRightEye ? 0.25 : 4.0, toLeft / toRight, 1);
            }
        }

        [Fact]
        public void TestStretchIsSavedClonedClampedAndCoveredByTheMarker()
        {
            var s = new DecorativeSymbolItem { StretchTop = 0.7, StretchBottom = 1.3, StretchLeft = 0.6, StretchRight = 9.0 };
            Assert.Equal(3.0, s.StretchRight); // pulled into range
            s.StretchRight = double.NaN;
            Assert.Equal(1.0, s.StretchRight);

            var reloaded = JsonSerializer.Deserialize<DecorativeSymbolItem>(JsonSerializer.Serialize(s))!;
            Assert.Equal((0.7, 1.3, 0.6, 1.0), (reloaded.StretchTop, reloaded.StretchBottom, reloaded.StretchLeft, reloaded.StretchRight));
            var clone = s.Clone();
            Assert.Equal((0.7, 1.3, 0.6, 1.0), (clone.StretchTop, clone.StretchBottom, clone.StretchLeft, clone.StretchRight));
            Assert.DoesNotContain("MaxStretch", JsonSerializer.Serialize(s));

            // Missing in a file: as drawn
            var old = JsonSerializer.Deserialize<DecorativeSymbolItem>("{\"Type\":1}")!;
            Assert.Equal((1.0, 1.0, 1.0, 1.0), (old.StretchTop, old.StretchBottom, old.StretchLeft, old.StretchRight));

            // The selection ring reaches as far as the longest length
            var p = new IrisParameters();
            p.Symbols.Clear();
            p.Symbols.Add(new DecorativeSymbolItem { Size = 0.2, Enabled = true, IsSelected = true });
            double plain = ItemMarkers.ForEye(p, false, null)[0].Radius;
            p.Symbols[0].StretchBottom = 2.0;
            Assert.True(ItemMarkers.ForEye(p, false, null)[0].Radius > plain * 1.8);
        }

        public static IEnumerable<object[]> HighlightStretchCases()
        {
            foreach (var shape in Enum.GetValues<HighlightShape>())
            {
                yield return new object[] { shape, 1.0, 1.0, 1.35, 1.35 }; // the former oval
                yield return new object[] { shape, 0.5, 2.0, 0.7, 1.6 };
            }
        }

        [Theory]
        [MemberData(nameof(HighlightStretchCases))]
        public void TestStretchedHighlightOutlinesAreSimpleAndFillTheirGradientBox(HighlightShape shape, double top, double bottom, double left, double right)
        {
            var h = new HighlightItem { Shape = shape, StretchTop = top, StretchBottom = bottom, StretchLeft = left, StretchRight = right };
            foreach (bool swap in new[] { false, true })
            {
                var outline = ShapeOutline.ForHighlight(shape, 20.0).Stretched(h, swap);
                Assert.Equal(outline.Start.X, outline.Segments[^1].End.X, 9);
                Assert.Equal(outline.Start.Y, outline.Segments[^1].End.Y, 9);
                Assert.Null(FindSelfIntersection(outline.Flatten(48)));

                // The quick box of the shapes written as an ellipse / polygon is exactly the drawn shape
                // (the SVG measures the outline of every other shape)
                if (shape is not (HighlightShape.Circle or HighlightShape.Star4 or HighlightShape.Diamond)) continue;
                var drawn = outline.GetBounds();
                var box = IrisStyle.HighlightBox(h, 20.0, swap);
                Assert.Equal(drawn.Left, box.Left, 6);
                Assert.Equal(drawn.Right, box.Right, 6);
                Assert.Equal(drawn.Top, box.Top, 6);
                Assert.Equal(drawn.Bottom, box.Bottom, 6);
            }
        }

        /// <summary>The drawing elements inside the left eye's highlight group.</summary>
        private static List<string> HighlightElements(IrisParameters p, bool isRightEye = false)
        {
            string svg = SvgExporter.GenerateSingleEyeSvg(p, isRightEye);
            Assert.True(SvgValidator.IsWellFormed(svg, out var error), error);
            string eye = isRightEye ? "eye_right" : "eye_left";
            int start = svg.IndexOf($"<g id=\"{eye}_highlights\">", StringComparison.Ordinal);
            string group = svg.Substring(start, svg.IndexOf("</g>", start, StringComparison.Ordinal) - start);
            return System.Text.RegularExpressions.Regex.Matches(group, "<(ellipse|polygon|path)[^>]*/>").Select(m => m.Value).ToList();
        }

        private static double Attr(string element, string name) => double.Parse(
            System.Text.RegularExpressions.Regex.Match(element, $" {name}=\"([-0-9.]+)\"").Groups[1].Value,
            System.Globalization.CultureInfo.InvariantCulture);

        [Fact]
        public void TestHighlightsAreDrawnAsBeforeUntilStretchedAndAnEvenCircleStaysAnEllipse()
        {
            var p = new IrisParameters { SizeMm = 10.0 };
            p.Highlights.Clear();
            p.Highlights.Add(new HighlightItem { Shape = HighlightShape.Circle, Size = 0.2 });
            p.Highlights.Add(new HighlightItem { Shape = HighlightShape.Star4 });
            p.Highlights.Add(new HighlightItem { Shape = HighlightShape.Diamond });
            p.Highlights.Add(new HighlightItem { Shape = HighlightShape.Circle, Size = 0.2, StretchLeft = 1.35, StretchRight = 1.35 }); // oval
            p.Highlights.Add(new HighlightItem { Shape = HighlightShape.Circle, StretchBottom = 1.6 });  // egg: no longer an ellipse
            p.Highlights.Add(new HighlightItem { Shape = HighlightShape.Star4, StretchBottom = 1.6 });

            var e = HighlightElements(p);
            Assert.Equal(6, e.Count);
            Assert.StartsWith("<ellipse", e[0]);
            Assert.Equal(Attr(e[0], "rx"), Attr(e[0], "ry"), 6);
            Assert.StartsWith("<polygon", e[1]);
            Assert.StartsWith("<polygon", e[2]);
            Assert.StartsWith("<ellipse", e[3]);
            Assert.Equal(Attr(e[0], "rx") * 1.35, Attr(e[3], "rx"), 2);
            Assert.Equal(Attr(e[0], "ry"), Attr(e[3], "ry"), 6);
            Assert.StartsWith("<path", e[4]);
            Assert.StartsWith("<path", e[5]);
            Assert.All(e, x => Assert.Contains("opacity=", x));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TestRightEyeHighlightLengthsSwapOnlyWhenTheHighlightsAreMirrored(bool mirror)
        {
            var p = new IrisParameters { SizeMm = 10.0, MirrorHighlightsWithIris = mirror };
            p.Highlights.Clear();
            p.Highlights.Add(new HighlightItem { Shape = HighlightShape.Circle, PositionX = 0, PositionY = 0, Size = 0.2, StretchLeft = 2.0, StretchRight = 0.5, StretchBottom = 1.3 });

            double LeftOverRight(bool isRightEye)
            {
                string d = System.Text.RegularExpressions.Regex.Match(HighlightElements(p, isRightEye)[0], "d=\"([^\"]*)\"").Groups[1].Value;
                var n = System.Text.RegularExpressions.Regex.Matches(d, @"-?\d+(\.\d+)?")
                    .Select(m => double.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture)).ToList();
                var xs = n.Where((_, i) => i % 2 == 0).ToList();
                double cx = xs[0]; // the outline starts at the top, straight above the center
                return (cx - xs.Min()) / (xs.Max() - cx);
            }

            Assert.Equal(4.0, LeftOverRight(false), 1);
            Assert.Equal(mirror ? 0.25 : 4.0, LeftOverRight(true), 1);
        }

        [Fact]
        public void TestHighlightLengthsAreSavedAndTheOldOvalReadsAsACircle()
        {
            var h = new HighlightItem { Shape = HighlightShape.Star4, StretchTop = 0.7, StretchBottom = 1.3, StretchLeft = 9.0, StretchRight = double.NaN };
            Assert.Equal((3.0, 1.0), (h.StretchLeft, h.StretchRight)); // pulled into range
            string json = JsonSerializer.Serialize(h);
            Assert.DoesNotContain("AspectRatio", json);
            var back = JsonSerializer.Deserialize<HighlightItem>(json)!;
            Assert.Equal((HighlightShape.Star4, 0.7, 1.3, 3.0, 1.0), (back.Shape, back.StretchTop, back.StretchBottom, back.StretchLeft, back.StretchRight));
            var clone = h.Clone();
            Assert.Equal((0.7, 1.3, 3.0, 1.0), (clone.StretchTop, clone.StretchBottom, clone.StretchLeft, clone.StretchRight));

            // The file numbers of the other shapes stay; 1 (the oval) and unknown numbers become a circle
            Assert.Equal(new[] { 0, 2, 3, 4, 5, 6, 7, 8, 9 }, Enum.GetValues<HighlightShape>().Select(s => (int)s).OrderBy(n => n));
            foreach (string shape in new[] { "1", "99" })
            {
                var old = JsonSerializer.Deserialize<HighlightItem>($"{{\"Shape\":{shape},\"AspectRatio\":1.6}}")!;
                Assert.Equal(HighlightShape.Circle, old.Shape);
                Assert.Equal((1.0, 1.0, 1.0, 1.0), (old.StretchTop, old.StretchBottom, old.StretchLeft, old.StretchRight));
            }

            // The selection ring reaches as far as the longest length
            var p = new IrisParameters();
            p.Highlights.Clear();
            p.Highlights.Add(new HighlightItem { Size = 0.2, IsSelected = true });
            double plain = ItemMarkers.ForEye(p, false, null)[0].Radius;
            p.Highlights[0].StretchTop = 2.0;
            Assert.True(ItemMarkers.ForEye(p, false, null)[0].Radius > plain * 1.8);
        }

        /// <summary>Center of a drawn highlight element: the ellipse's (cx, cy), or the middle of the path / polygon's points.</summary>
        private static (double X, double Y) ElementCenter(string element)
        {
            if (element.StartsWith("<ellipse", StringComparison.Ordinal)) return (Attr(element, "cx"), Attr(element, "cy"));
            string pts = System.Text.RegularExpressions.Regex.Match(element, "(?:d|points)=\"([^\"]*)\"").Groups[1].Value;
            var n = System.Text.RegularExpressions.Regex.Matches(pts, @"-?\d+(\.\d+)?")
                .Select(m => double.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture)).ToList();
            var xs = n.Where((_, i) => i % 2 == 0).ToList();
            var ys = n.Where((_, i) => i % 2 == 1).ToList();
            return ((xs.Min() + xs.Max()) / 2.0, (ys.Min() + ys.Max()) / 2.0);
        }

        [Theory]
        [InlineData(HighlightShape.Circle, true)]
        [InlineData(HighlightShape.Star4, true)]
        [InlineData(HighlightShape.Diamond, false)]
        public void TestHighlightCopiesSitOnARingAndFollowTheMirrorSetting(HighlightShape shape, bool mirror)
        {
            var p = new IrisParameters { SizeMm = 10.0, MirrorHighlightsWithIris = mirror };
            p.Highlights.Clear();
            p.Highlights.Add(new HighlightItem { Shape = shape, Count = 5, Size = 0.1, PositionX = 0.3, PositionY = -0.2, Blend = LayerBlend.Screen });
            // The same highlight with one copy marks the ring's center
            p.Highlights.Add(new HighlightItem { Shape = HighlightShape.Circle, Count = 1, Size = 0.02, PositionX = 0.3, PositionY = -0.2 });

            foreach (bool isRightEye in new[] { false, true })
            {
                var e = HighlightElements(p, isRightEye);
                Assert.Equal(6, e.Count);
                var center = ElementCenter(e[5]);
                double r = Attr(e[5], "rx") / 0.02 * 0.1; // the copies' radius (size 0.1), from the marker's 0.02
                var offsets = e.Take(5).Select(ElementCenter).Select(c => (c.X - center.X, c.Y - center.Y)).ToList();
                foreach (var (dx, dy) in offsets) Assert.Equal(r * 1.4, Math.Sqrt(dx * dx + dy * dy), 2);
                // Each copy carries the highlight's own 重ね方 (no group of its own)
                Assert.All(e.Take(5), x => Assert.Contains("mix-blend-mode:screen", x));

                // The first copy is to the right of the position; in a mirrored right eye it is to the left
                double expectedSide = isRightEye && mirror ? -1.0 : 1.0;
                Assert.Equal(expectedSide * r * 1.4, offsets[0].Item1, 2);
                Assert.Equal(0.0, offsets[0].Item2, 2);
            }
        }

        [Fact]
        public void TestHighlightCornerRoundingRoundsTheSparkleAndRhombusButNotTheCircle()
        {
            var p = new IrisParameters { SizeMm = 10.0 };
            p.Highlights.Clear();
            p.Highlights.Add(new HighlightItem { Shape = HighlightShape.Circle, CornerRounding = 0.8 });
            p.Highlights.Add(new HighlightItem { Shape = HighlightShape.Star4, CornerRounding = 0.8 });
            p.Highlights.Add(new HighlightItem { Shape = HighlightShape.Diamond, CornerRounding = 0.8 });
            var rounded = HighlightElements(p);

            foreach (var h in p.Highlights) h.CornerRounding = 0.0;
            var sharp = HighlightElements(p);

            Assert.Equal(sharp[0], rounded[0]);            // a circle has no corners: the same ellipse
            Assert.StartsWith("<polygon", sharp[1]);
            Assert.StartsWith("<path", rounded[1]);
            Assert.Contains(" C ", rounded[1]);             // the tips became curves
            Assert.StartsWith("<path", rounded[2]);
            Assert.Contains(" C ", rounded[2]);
            Assert.All(rounded, x => Assert.Contains("opacity=", x));
        }

        [Fact]
        public void TestHighlightCountAndRoundingAreSavedClonedClampedAndRinged()
        {
            var h = new HighlightItem { Count = 99, CornerRounding = 5.0 };
            Assert.Equal((12, 1.0), (h.Count, h.CornerRounding)); // pulled into range
            h.Count = 3;
            h.CornerRounding = 0.4;
            var back = JsonSerializer.Deserialize<HighlightItem>(JsonSerializer.Serialize(h))!;
            Assert.Equal((3, 0.4), (back.Count, back.CornerRounding));
            Assert.Equal((3, 0.4), (h.Clone().Count, h.Clone().CornerRounding));
            var old = JsonSerializer.Deserialize<HighlightItem>("{\"Shape\":2}")!;
            Assert.Equal((1, 0.0), (old.Count, old.CornerRounding));

            // The selection ring takes in the ring of copies (as the symbols' does)
            var p = new IrisParameters();
            p.Highlights.Clear();
            p.Highlights.Add(new HighlightItem { Size = 0.2, IsSelected = true });
            double single = ItemMarkers.ForEye(p, false, null)[0].Radius;
            p.Highlights[0].Count = 6;
            double ring = ItemMarkers.ForEye(p, false, null)[0].Radius;
            Assert.True(ring > single * 2.2, $"{ring} vs {single}");
        }

        [Fact]
        public void TestSymbolsAndHighlightsOfferTheSameShapes()
        {
            var all = Enum.GetValues<ShapeKind>().OrderBy(k => k).ToArray();
            var fromSymbols = Enum.GetValues<SymbolType>().Select(ShapeKinds.Of).Where(k => k != null).Select(k => k!.Value).OrderBy(k => k).ToArray();
            var fromHighlights = Enum.GetValues<HighlightShape>().Select(ShapeKinds.Of).OrderBy(k => k).ToArray();
            Assert.Equal(all, fromSymbols);     // every shape once
            Assert.Equal(all, fromHighlights);

            // The numbers the files have always stored keep their meaning; new shapes come after them
            Assert.Equal((1, 2, 3, 4, 5, 6, 7, 8, 9), ((int)SymbolType.Star4, (int)SymbolType.Star5, (int)SymbolType.Heart, (int)SymbolType.Moon,
                (int)SymbolType.Cross, (int)SymbolType.Diamond, (int)SymbolType.SakuraPetal, (int)SymbolType.Circle, (int)SymbolType.CatSlit));
            Assert.Equal((0, 2, 3), ((int)HighlightShape.Circle, (int)HighlightShape.Star4, (int)HighlightShape.Diamond));
            Assert.Equal(SymbolType.None, JsonSerializer.Deserialize<DecorativeSymbolItem>("{\"Type\":42}")!.Type);

            // The same outline for both, except the highlight's thinner sparkle and narrower diamond (as before)
            foreach (var kind in all)
            {
                var symbol = ShapeOutline.ForShape(kind, 10.0).GetBounds();
                var highlight = ShapeOutline.ForShape(kind, 10.0, highlight: true).GetBounds();
                if (kind == ShapeKind.Diamond) Assert.Equal(13.0, highlight.Width, 6); // ±0.65r
                else Assert.Equal(symbol, highlight);
            }
            Assert.Equal(14.0, ShapeOutline.ForShape(ShapeKind.Diamond, 10.0).GetBounds().Width, 6); // ±0.7r
        }

        /// <summary>Distance of the star's valley points from its center.</summary>
        private static double StarCoreRadius(ShapeOutline star) => star.Flatten().Min(p => Math.Sqrt(p.X * p.X + p.Y * p.Y));

        [Fact]
        public void TestStarCoreScaleSetsTheStarsInnerRadius()
        {
            Assert.Equal(2.8, StarCoreRadius(ShapeOutline.ForShape(ShapeKind.Star4, 10.0)), 6);
            Assert.Equal(2.2, StarCoreRadius(ShapeOutline.ForShape(ShapeKind.Star4, 10.0, highlight: true)), 6);
            Assert.Equal(1.4, StarCoreRadius(ShapeOutline.ForShape(ShapeKind.Star4, 10.0, starCore: 0.5)), 6);
            Assert.Equal(8.4, StarCoreRadius(ShapeOutline.ForShape(ShapeKind.Star5, 10.0, starCore: 2.0)), 6);
            Assert.Equal(9.5, StarCoreRadius(ShapeOutline.ForShape(ShapeKind.Star5, 10.0, starCore: 3.0)), 6); // kept inside the tips
            // Other shapes ignore it
            Assert.Equal(ShapeOutline.ForShape(ShapeKind.Heart, 10.0).GetBounds(), ShapeOutline.ForShape(ShapeKind.Heart, 10.0, starCore: 2.0).GetBounds());

            var s = new DecorativeSymbolItem { StarCoreScale = 9.0 };
            Assert.Equal(3.0, s.StarCoreScale);
            s.StarCoreScale = 0.6;
            Assert.Equal(0.6, JsonSerializer.Deserialize<DecorativeSymbolItem>(JsonSerializer.Serialize(s))!.StarCoreScale);
            Assert.Equal(0.6, s.Clone().StarCoreScale);
            var h = new HighlightItem { StarCoreScale = double.NaN };
            Assert.Equal(1.0, h.StarCoreScale);
            h.StarCoreScale = 1.7;
            Assert.Equal(1.7, JsonSerializer.Deserialize<HighlightItem>(JsonSerializer.Serialize(h))!.StarCoreScale);
            Assert.Equal(1.7, h.Clone().StarCoreScale);
            Assert.Equal(1.0, JsonSerializer.Deserialize<HighlightItem>("{\"Shape\":2}")!.StarCoreScale);
        }

        [Fact]
        public void TestEveryShapeDrawsAsASymbolAndAsAHighlight()
        {
            foreach (var kind in Enum.GetValues<ShapeKind>())
            {
                var p = new IrisParameters { SizeMm = 10.0 };
                p.Symbols.Clear();
                p.Highlights.Clear();
                var type = Enum.GetValues<SymbolType>().Single(t => ShapeKinds.Of(t) == kind);
                var shape = Enum.GetValues<HighlightShape>().Single(x => ShapeKinds.Of(x) == kind);
                p.Symbols.Add(new DecorativeSymbolItem { Type = type, Enabled = true, Opacity = 0.9 });
                p.Highlights.Add(new HighlightItem { Shape = shape });

                string svg = SvgExporter.GenerateSingleEyeSvg(p, false);
                Assert.True(SvgValidator.IsWellFormed(svg, out var error), $"{kind}: {error}");
                Assert.Contains($"<g id=\"eye_left_symbol_{type}\"", svg);
                Assert.Single(HighlightElements(p));
            }

            // Unchanged settings keep the old SVG elements: the plain sparkle is still the polygon
            var q = new IrisParameters { SizeMm = 10.0 };
            q.Highlights.Clear();
            q.Highlights.Add(new HighlightItem { Shape = HighlightShape.Star4 });
            Assert.StartsWith("<polygon", HighlightElements(q)[0]);
            q.Highlights[0].StarCoreScale = 0.6;
            Assert.StartsWith("<path", HighlightElements(q)[0]);
        }

        [Fact]
        public void TestMirroredMoonHighlightFacesTheOtherWayInTheRightEye()
        {
            var p = new IrisParameters { SizeMm = 10.0, MirrorHighlightsWithIris = true };
            p.Highlights.Clear();
            p.Highlights.Add(new HighlightItem { Shape = HighlightShape.Moon, PositionX = 0, PositionY = 0, Size = 0.3 });
            // The moon's horns point left (its outer arc is on the right); the start point is the upper horn tip
            double HornX(bool isRightEye)
            {
                string d = System.Text.RegularExpressions.Regex.Match(HighlightElements(p, isRightEye)[0], "d=\"M ([-0-9.]+),").Groups[1].Value;
                return double.Parse(d, System.Globalization.CultureInfo.InvariantCulture);
            }
            double CenterX(bool isRightEye) => ElementCenter(HighlightElements(p, isRightEye)[0]).X;

            Assert.True(CenterX(false) > HornX(false)); // left eye: bulge to the right of the horns
            Assert.True(CenterX(true) < HornX(true));   // right eye: mirrored, bulge to the left
            p.MirrorHighlightsWithIris = false;
            Assert.True(CenterX(true) > HornX(true));   // not mirrored: the same moon in both eyes
        }

        /// <summary>The pupil's own shape element (inside its group), for one eye.</summary>
        private static string PupilElement(IrisParameters p, bool isRightEye = false)
        {
            string svg = SvgExporter.GenerateSingleEyeSvg(p, isRightEye);
            Assert.True(SvgValidator.IsWellFormed(svg, out var error), error);
            string eye = isRightEye ? "eye_right" : "eye_left";
            int start = svg.IndexOf($"<g id=\"{eye}_pupil\"", StringComparison.Ordinal);
            Assert.True(start >= 0);
            return System.Text.RegularExpressions.Regex.Match(svg[start..], "<(ellipse|path)[^>]*/>").Value;
        }

        [Fact]
        public void TestPupilsOfferTheSameShapesAndKeepTheirFileNumbers()
        {
            var all = Enum.GetValues<ShapeKind>().OrderBy(k => k).ToArray();
            Assert.Equal(all, Enum.GetValues<PupilShape>().Select(ShapeKinds.Of).OrderBy(k => k).ToArray());
            Assert.Equal((0, 2, 3, 4), ((int)PupilShape.Round, (int)PupilShape.CatSlit, (int)PupilShape.Heart, (int)PupilShape.Star5));
            foreach (string number in new[] { "1", "42" }) // 1 was the vertical oval
                Assert.Equal(PupilShape.Round, JsonSerializer.Deserialize<IrisParameters>($"{{\"PupilShape\":{number}}}")!.PupilShape);

            // Every shape draws as a pupil, also blurred (the sharp core) and with gem-cut facets (clipped to it), in both eyes
            foreach (var shape in Enum.GetValues<PupilShape>())
            {
                var p = new IrisParameters { SizeMm = 10.0, PupilShape = shape, PupilSoftness = 0.2, PupilFacetIntensity = 0.7, PupilStretchBottom = 1.4 };
                foreach (bool isRightEye in new[] { false, true })
                {
                    string svg = SvgExporter.GenerateSingleEyeSvg(p, isRightEye);
                    Assert.True(SvgValidator.IsWellFormed(svg, out var error), $"{shape}: {error}");
                    Assert.Contains("_pupil_core_clip", svg);
                    Assert.Contains("_pupil_facet_clip", svg);
                }
            }
        }

        [Fact]
        public void TestRoundPupilStaysAnEllipseAndTheOldOvalIsAStretchedCircle()
        {
            var p = new IrisParameters { SizeMm = 10.0, PupilShape = PupilShape.Round };
            string round = PupilElement(p);
            Assert.StartsWith("<ellipse", round);
            Assert.Equal(Attr(round, "rx"), Attr(round, "ry"), 6);

            // The former vertical oval: 0.75 as wide, 1.25 as tall
            p.PupilStretchLeft = p.PupilStretchRight = 0.75;
            p.PupilStretchTop = p.PupilStretchBottom = 1.25;
            string oval = PupilElement(p);
            Assert.StartsWith("<ellipse", oval);
            Assert.Equal(Attr(round, "rx") * 0.75, Attr(oval, "rx"), 2);
            Assert.Equal(Attr(round, "ry") * 1.25, Attr(oval, "ry"), 2);

            // Up and down different: no longer an ellipse
            p.PupilStretchBottom = 1.6;
            Assert.StartsWith("<path", PupilElement(p));

            // The 雰囲気 preset that used the vertical oval keeps the same shape
            var mood = ColorPresets.All.Single(x => x.Name.StartsWith("雰囲気: D.", StringComparison.Ordinal));
            var q = new IrisParameters();
            ColorPresets.ApplyPreset(q, mood);
            Assert.Equal((PupilShape.Round, 0.75, 0.75, 1.25, 1.25), (q.PupilShape, q.PupilStretchLeft, q.PupilStretchRight, q.PupilStretchTop, q.PupilStretchBottom));
            // ... and the next preset puts the pupil back to as drawn
            ColorPresets.ApplyPreset(q, ColorPresets.All.First(x => x != mood && x.PupilStretchLeft == null));
            Assert.Equal((1.0, 1.0, 1.0, 1.0, 1.0), (q.PupilStretchLeft, q.PupilStretchRight, q.PupilStretchTop, q.PupilStretchBottom, q.PupilStarCoreScale));
        }

        [Fact]
        public void TestRightEyePupilIsTheMirrorImage()
        {
            // A heart pupil stretched to the left: the right eye stretches it to the right
            var p = new IrisParameters { SizeMm = 10.0, PupilShape = PupilShape.Heart, PupilStretchLeft = 2.0, PupilStretchRight = 0.5 };
            double LeftOverRight(bool isRightEye)
            {
                var e = PupilElement(p, isRightEye);
                string d = System.Text.RegularExpressions.Regex.Match(e, "d=\"([^\"]*)\"").Groups[1].Value;
                var xs = System.Text.RegularExpressions.Regex.Matches(d, @"-?\d+(\.\d+)?")
                    .Select(m => double.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture)).Where((_, i) => i % 2 == 0).ToList();
                double cx = xs[0]; // the heart starts at its tip, straight below the center
                return (cx - xs.Min()) / (xs.Max() - cx);
            }
            Assert.Equal(4.0, LeftOverRight(false), 1);
            Assert.Equal(0.25, LeftOverRight(true), 1);

            // The moon pupil faces the other way in the right eye
            var m = new IrisParameters { SizeMm = 10.0, PupilShape = PupilShape.Moon };
            double HornX(bool isRightEye) => double.Parse(
                System.Text.RegularExpressions.Regex.Match(PupilElement(m, isRightEye), "d=\"M ([-0-9.]+),").Groups[1].Value,
                System.Globalization.CultureInfo.InvariantCulture);
            Assert.True(ElementCenter(PupilElement(m, false)).X > HornX(false));
            Assert.True(ElementCenter(PupilElement(m, true)).X < HornX(true));
        }

        [Fact]
        public void TestPupilStarCoreAndLengthsAreSavedClonedAndClamped()
        {
            Assert.Equal(4.0, StarCoreRadius(ShapeOutline.ForPupil(PupilShape.Star5, 10.0, 10.0)) / 1.05, 6); // 0.40 of 10.5
            Assert.Equal(2.0 * 4.0, StarCoreRadius(ShapeOutline.ForPupil(PupilShape.Star5, 10.0, 10.0, 2.0)) / 1.05, 6);

            var p = new IrisParameters { PupilStarCoreScale = 0.7, PupilStretchTop = 1.3, PupilStretchBottom = 9.0, PupilStretchLeft = 0.6, PupilStretchRight = double.NaN };
            Assert.Equal((3.0, 1.0), (p.PupilStretchBottom, p.PupilStretchRight));
            var back = JsonSerializer.Deserialize<IrisParameters>(JsonSerializer.Serialize(p))!;
            var clone = p.Clone();
            foreach (var x in new[] { back, clone })
                Assert.Equal((0.7, 1.3, 3.0, 0.6, 1.0), (x.PupilStarCoreScale, x.PupilStretchTop, x.PupilStretchBottom, x.PupilStretchLeft, x.PupilStretchRight));
            var old = JsonSerializer.Deserialize<IrisParameters>("{\"PupilShape\":3}")!;
            Assert.Equal((1.0, 1.0, 1.0, 1.0, 1.0), (old.PupilStarCoreScale, old.PupilStretchTop, old.PupilStretchBottom, old.PupilStretchLeft, old.PupilStretchRight));
        }

        [Fact]
        public void TestMoreRoundingPullsTheStarTipsInSteadily()
        {
            double previousTip = double.MaxValue;
            foreach (double amount in Amounts)
            {
                var pts = ShapeOutline.Star(5, 50.0, 0.42).Rounded(amount).Flatten(48);
                double tip = pts.Max(p => Math.Sqrt(p.X * p.X + p.Y * p.Y));
                if (amount == 0) Assert.Equal(50.0, tip, 6);
                else Assert.True(tip < previousTip, $"tip did not move in at {amount}: {tip} vs {previousTip}");
                previousTip = tip;
            }
            Assert.True(previousTip < 45.0);
        }

        [Fact]
        public void TestOldSaveFilesLoadWithSharpCornersAndValuesAreClamped()
        {
            // Saved before the parameter existed: no keys at all
            var old = JsonSerializer.Deserialize<IrisParameters>(
                "{\"Name\":\"旧\",\"PupilShape\":4,\"Symbols\":[{\"Type\":2,\"Enabled\":true}]}")!;
            Assert.Equal(0.0, old.PupilCornerRounding);
            Assert.Equal(0.0, old.Symbols[0].CornerRounding);

            var outOfRange = JsonSerializer.Deserialize<IrisParameters>(
                "{\"PupilCornerRounding\":5.0,\"Symbols\":[{\"CornerRounding\":-2.0}]}")!;
            Assert.Equal(1.0, outOfRange.PupilCornerRounding);
            Assert.Equal(0.0, outOfRange.Symbols[0].CornerRounding);
        }

        [Fact]
        public void TestRoundingSurvivesSaveCloneAndSymbolClone()
        {
            var p = new IrisParameters { PupilShape = PupilShape.Star5, PupilCornerRounding = 0.6 };
            p.Symbols.Clear();
            p.Symbols.Add(new DecorativeSymbolItem { Type = SymbolType.Star5, CornerRounding = 0.35, Enabled = true });

            var reloaded = JsonSerializer.Deserialize<IrisParameters>(JsonSerializer.Serialize(p))!;
            Assert.Equal(0.6, reloaded.PupilCornerRounding, 9);
            Assert.Equal(0.35, reloaded.Symbols[0].CornerRounding, 9);

            var clone = p.Clone();
            Assert.Equal(0.6, clone.PupilCornerRounding, 9);
            Assert.Equal(0.35, clone.Symbols[0].CornerRounding, 9);
            Assert.Equal(0.35, p.Symbols[0].Clone().CornerRounding, 9);
        }

        [Fact]
        public void TestRoundingEditsAreUndoable()
        {
            var vm = IsolatedViewModel.Create();
            // Slider drag on the pupil and then on a symbol card: one undo step for the whole edit burst
            vm.CurrentIris.PupilCornerRounding = 0.3;
            vm.CurrentIris.PupilCornerRounding = 0.7;
            vm.CurrentIris.Symbols.First().CornerRounding = 0.4;

            Assert.True(vm.CanUndo);
            vm.UndoCommand.Execute(null);
            Assert.Equal(0.0, vm.CurrentIris.PupilCornerRounding, 9);
            Assert.Equal(0.0, vm.CurrentIris.Symbols.First().CornerRounding, 9);
            vm.RedoCommand.Execute(null);
            Assert.Equal(0.7, vm.CurrentIris.PupilCornerRounding, 9);
            Assert.Equal(0.4, vm.CurrentIris.Symbols.First().CornerRounding, 9);
        }

        [Fact]
        public void TestSvgUsesTheSameRoundedOutlineAsTheScreen()
        {
            var p = new IrisParameters { SizeMm = 10.0, PupilShape = PupilShape.Star5, PupilCornerRounding = 0.5 };
            p.Symbols.Clear();
            p.Symbols.Add(new DecorativeSymbolItem { Type = SymbolType.Star4, CornerRounding = 0.8, Enabled = true, Rotation = 0 });

            string sharpSvg = SvgExporter.GenerateSingleEyeSvg(new IrisParameters { SizeMm = 10.0, PupilShape = PupilShape.Star5 }, isRightEye: false);
            string svg = SvgExporter.GenerateSingleEyeSvg(p, isRightEye: false);
            Assert.True(SvgValidator.IsWellFormed(svg, out _));
            Assert.NotEqual(sharpSvg, svg);

            // The pupil path is the shared outline, rounded, moved to the iris center
            var pupilOutline = ShapeOutline.ForPupil(PupilShape.Star5, 1, 1)!; // only checks the command structure
            string pupilPathStart = "<path d=\"M ";
            int pupilAt = svg.IndexOf("_pupil", StringComparison.Ordinal);
            Assert.True(pupilAt >= 0);
            int pathAt = svg.IndexOf(pupilPathStart, pupilAt, StringComparison.Ordinal);
            string pupilD = svg.Substring(pathAt + 9, svg.IndexOf('"', pathAt + 9) - pathAt - 9);
            Assert.Contains(" C ", pupilD); // rounded corners are curves
            Assert.Equal(pupilOutline.Rounded(0.5).Segments.Count(s => !s.IsLine), pupilD.Split(" C ").Length - 1);

            RunOnSta(() =>
            {
                // Screen geometry and SVG come from the same outline: same number of curved corners
                var geo = ShapeOutline.Star(4, 20.0, 0.28).Rounded(0.8).ToPathGeometry();
                Assert.Equal(8, geo.Figures[0].Segments.OfType<BezierSegment>().Count());
                var bmp = RasterExporter.RenderSingleToBitmap(p, isRightEye: true, dpi: 300.0, showCutLines: false, showCenterMarks: false);
                Assert.True(bmp.PixelWidth > 0);
            });
        }
    }
}
