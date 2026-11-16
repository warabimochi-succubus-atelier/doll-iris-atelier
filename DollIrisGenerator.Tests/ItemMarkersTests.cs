using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using DollIrisGenerator.Controls;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;

namespace DollIrisGenerator.Tests
{
    /// <summary>
    /// The preview's rings round the ticked / pointed-at symbols and highlights (SPECIFICATION 13.2): they must sit
    /// where SvgExporter draws the items, in both eyes.
    /// </summary>
    public class ItemMarkersTests
    {
        private static IrisParameters Design()
        {
            var p = new IrisParameters { SizeMm = 10, Shape = IrisShape.Circle, TiltAngle = 0, MirrorHighlightsWithIris = true };
            p.Symbols.Clear();
            p.Highlights.Clear();
            p.Symbols.Add(new DecorativeSymbolItem { PositionX = 0.5, PositionY = -0.2, Size = 0.1 });
            p.Symbols.Add(new DecorativeSymbolItem { PositionX = -0.4, PositionY = 0.3, Size = 0.2, Count = 3, IsSelected = true });
            p.Highlights.Add(new HighlightItem { PositionX = 0.3, PositionY = 0.4, Size = 0.1, Shape = HighlightShape.Oval, AspectRatio = 2.0, IsSelected = true });
            return p;
        }

        [Fact]
        public void TestOnlyTickedAndPointedAtItemsGetARingWithTheirListNumber()
        {
            var p = Design();
            var left = ItemMarkers.ForEye(p, isRightEye: false, hoveredId: null);
            Assert.Equal(new[] { (MarkedItemKind.Symbol, 2, false), (MarkedItemKind.Highlight, 1, false) },
                left.Select(m => (m.Kind, m.Number, m.Hovered)));

            var hovered = ItemMarkers.ForEye(p, false, p.Symbols[0].Id);
            Assert.Contains(hovered, m => m.Kind == MarkedItemKind.Symbol && m.Number == 1 && m.Hovered);
            Assert.Equal(3, hovered.Count);

            foreach (var s in p.Symbols) s.IsSelected = false;
            foreach (var h in p.Highlights) h.IsSelected = false;
            Assert.Empty(ItemMarkers.ForEye(p, false, null));
        }

        [Fact]
        public void TestRingsSitOnTheItemsAndEncloseThem()
        {
            var p = Design(); // 10 mm circle: half width 5 mm
            var left = ItemMarkers.ForEye(p, false, p.Symbols[0].Id);
            var single = left.Single(m => m.Kind == MarkedItemKind.Symbol && m.Number == 1);
            Assert.Equal(2.5, single.X, 9);
            Assert.Equal(-1.0, single.Y, 9);
            Assert.True(single.Radius >= 0.5 + ItemMarkers.RingMinMm - 1e-9); // item radius 5 mm x 0.1

            // Repeated copies sit on a ring of size x 1.4 round the position: the marker takes them all in
            var ring = left.Single(m => m.Kind == MarkedItemKind.Symbol && m.Number == 2);
            Assert.True(ring.Radius >= 5 * 0.2 * 2.4);

            // An oval highlight is as wide as its aspect ratio
            var oval = left.Single(m => m.Kind == MarkedItemKind.Highlight);
            Assert.True(oval.Radius >= 5 * 0.1 * 2.0);
        }

        [Fact]
        public void TestRightEyeIsMirroredLikeTheExport()
        {
            var p = Design();
            var left = ItemMarkers.ForEye(p, false, null);
            var right = ItemMarkers.ForEye(p, true, null);
            Assert.Equal(left.Select(m => (-m.X, m.Y)), right.Select(m => (m.X, m.Y)));

            // Highlights stay put when 「ハイライトも左右反転する」 is off; symbols are always mirrored
            p.MirrorHighlightsWithIris = false;
            right = ItemMarkers.ForEye(p, true, null);
            Assert.Equal(-left[0].X, right[0].X, 9);
            Assert.Equal(left[1].X, right[1].X, 9);
        }

        [Fact]
        public void TestOvalIrisAndTiltMoveTheRings()
        {
            var p = Design();
            p.Shape = IrisShape.HorizontalOval;
            p.AspectRatio = 1.5; // 15 x 10 mm
            var m = ItemMarkers.ForEye(p, false, p.Symbols[0].Id).Single(x => x.Number == 1 && x.Kind == MarkedItemKind.Symbol);
            Assert.Equal(0.5 * 7.5, m.X, 9);
            Assert.Equal(-0.2 * 5.0, m.Y, 9);

            // Turned clockwise (SVG rotate) by the tilt; the right eye turns the other way
            p.TiltAngle = 30;
            double a = 30 * Math.PI / 180;
            var t = ItemMarkers.ForEye(p, false, p.Symbols[0].Id).Single(x => x.Number == 1 && x.Kind == MarkedItemKind.Symbol);
            Assert.Equal(3.75 * Math.Cos(a) + 1.0 * Math.Sin(a), t.X, 9);
            Assert.Equal(3.75 * Math.Sin(a) - 1.0 * Math.Cos(a), t.Y, 9);
            var r = ItemMarkers.ForEye(p, true, p.Symbols[0].Id).Single(x => x.Number == 1 && x.Kind == MarkedItemKind.Symbol);
            Assert.Equal(-t.X, r.X, 9);
            Assert.Equal(t.Y, r.Y, 9);
        }

        private static double Attr(string svg, string pattern) =>
            double.Parse(Regex.Match(svg, pattern).Groups[1].Value, CultureInfo.InvariantCulture);

        [Theory]
        [InlineData(IrisShape.Circle, 1.0, 0.0)]
        [InlineData(IrisShape.VerticalOval, 1.3, 20.0)]
        public void TestPairLayoutIsWhereThePairSvgPutsTheEyes(IrisShape shape, double aspect, double tilt)
        {
            var p = new IrisParameters { SizeMm = 12, Shape = shape, AspectRatio = aspect, TiltAngle = tilt };
            string svg = SvgExporter.GeneratePairSvg(p, includePrintGuides: false, pairSpacingMm: 4.0);
            var layout = SvgExporter.PairLayoutMm(p, 4.0, SvgExporter.PairPaddingMm);

            var viewBox = Regex.Match(svg, @"viewBox=""0 0 ([\d.]+) ([\d.]+)""");
            Assert.Equal(double.Parse(viewBox.Groups[1].Value, CultureInfo.InvariantCulture), layout.Width, 2);
            Assert.Equal(double.Parse(viewBox.Groups[2].Value, CultureInfo.InvariantCulture), layout.Height, 2);
            Assert.Equal(Attr(svg, @"left_eye_clip"">\s*<ellipse cx=""([\d.]+)"""), layout.LeftCx, 3);
            Assert.Equal(Attr(svg, @"right_eye_clip"">\s*<ellipse cx=""([\d.]+)"""), layout.RightCx, 3);
            Assert.Equal(Attr(svg, @"left_eye_clip"">\s*<ellipse cx=""[\d.]+"" cy=""([\d.]+)"""), layout.Cy, 3);
        }

        [Fact]
        public void TestTickingTellsThePreviewWithoutChangingTheDesign()
        {
            var p = Design();
            int selectionEvents = 0, designChanges = 0;
            p.ItemSelectionChanged += (_, _) => selectionEvents++;
            p.PropertyChanged += (_, _) => designChanges++;

            p.Symbols[0].IsSelected = true;
            p.Highlights[0].IsSelected = false;

            Assert.Equal(2, selectionEvents);
            Assert.Equal(0, designChanges);
        }

        [Fact]
        public void TestPointedAtCardIsTrackedForThePreview()
        {
            int changes = 0;
            void OnChanged(object? s, EventArgs e) => changes++;
            ItemHover.Changed += OnChanged;
            try
            {
                ItemHover.SetCurrent("abc");
                ItemHover.SetCurrent("abc"); // no change, no event
                Assert.Equal("abc", ItemHover.CurrentId);
                ItemHover.SetCurrent(null);
                Assert.Null(ItemHover.CurrentId);
                Assert.Equal(2, changes);
            }
            finally
            {
                ItemHover.Changed -= OnChanged;
                ItemHover.SetCurrent(null);
            }
        }
    }
}
