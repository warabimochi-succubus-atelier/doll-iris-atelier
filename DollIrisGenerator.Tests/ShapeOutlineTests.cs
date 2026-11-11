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
                if (type != SymbolType.None) yield return new object[] { $"symbol:{type}" };
            }
            foreach (var shape in new[] { PupilShape.CatSlit, PupilShape.Heart, PupilShape.Star })
            {
                yield return new object[] { $"pupil:{shape}" };
            }
        }

        private static ShapeOutline Build(string name)
        {
            var parts = name.Split(':');
            return parts[0] == "symbol"
                ? ShapeOutline.ForSymbol(Enum.Parse<SymbolType>(parts[1]), 50.0)!
                : ShapeOutline.ForPupil(Enum.Parse<PupilShape>(parts[1]), 30.0, 30.0)!;
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
            var p = new IrisParameters { PupilShape = PupilShape.Star, PupilCornerRounding = 0.6 };
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
            var p = new IrisParameters { SizeMm = 10.0, PupilShape = PupilShape.Star, PupilCornerRounding = 0.5 };
            p.Symbols.Clear();
            p.Symbols.Add(new DecorativeSymbolItem { Type = SymbolType.Star4, CornerRounding = 0.8, Enabled = true, Rotation = 0 });

            string sharpSvg = SvgExporter.GenerateSingleEyeSvg(new IrisParameters { SizeMm = 10.0, PupilShape = PupilShape.Star }, isRightEye: false);
            string svg = SvgExporter.GenerateSingleEyeSvg(p, isRightEye: false);
            Assert.True(SvgValidator.IsWellFormed(svg, out _));
            Assert.NotEqual(sharpSvg, svg);

            // The pupil path is the shared outline, rounded, moved to the iris center
            var pupilOutline = ShapeOutline.ForPupil(PupilShape.Star, 1, 1)!; // only checks the command structure
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
