using System;
using System.Linq;
using System.Text.Json;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using Xunit;

namespace DollIrisGenerator.Tests
{
    /// <summary>弧のハイライト (SPECIFICATION.md 14): a thin curved band of light, pointed at both tips.</summary>
    public class ArcHighlightTests
    {
        [Fact]
        public void Arc_SpansItsWidth_RisesByItsCurve_AndIsCentered()
        {
            const double r = 10.0;
            var b = ShapeOutline.Arc(r, 0.3, 0.2).GetBounds();
            Assert.Equal(-r, b.Left, 3);
            Assert.Equal(r, b.Right, 3);
            Assert.Equal((0.3 + 0.1) * r, b.Height, 3); // the outer edge rises 反り + 太さ / 2 above the tips
            Assert.Equal(0.0, (b.Top + b.Bottom) / 2.0, 3);

            // 反り 0: a straight streak, 太さ thick in the middle
            var flat = ShapeOutline.Arc(r, 0.0, 0.2).GetBounds();
            Assert.Equal(0.2 * r, flat.Height, 3);
            Assert.Equal(0.0, (flat.Top + flat.Bottom) / 2.0, 3);

            // Both tips are on the outline: it starts at the left one
            var start = ShapeOutline.Arc(r, 0.5, 0.3).Start;
            Assert.Equal(-r, start.X, 6);
        }

        [Fact]
        public void Arc_BulgesUp_AndTheMiddleIsAsThickAsSet()
        {
            const double r = 10.0, curve = 0.4, thickness = 0.25;
            var points = ShapeOutline.Arc(r, curve, thickness).Flatten(200);
            var middle = points.Where(p => Math.Abs(p.X) < 0.05).Select(p => p.Y).ToList();
            Assert.True(middle.Count >= 2);
            Assert.Equal(thickness * r, middle.Max() - middle.Min(), 1);
            var tipY = points.First().Y;
            Assert.True(middle.Max() < tipY); // the whole middle is above the tips (⌒)
        }

        [Fact]
        public void Svg_DrawsTheArcHighlightAsAPath()
        {
            var p = new IrisParameters();
            p.Highlights.Clear();
            p.Highlights.Add(new HighlightItem { Shape = HighlightShape.Arc, Size = 0.3, Rotation = 135, ArcCurve = 0.5 });
            string svg = SvgExporter.GeneratePairSvg(p);
            Assert.Matches("<g id=\"left_eye_highlights\">\\s*<path d=\"M", svg);
            System.Xml.Linq.XDocument.Parse(svg);

            // The settings change the outline
            p.Highlights[0].ArcThickness = 0.5;
            Assert.NotEqual(svg, SvgExporter.GeneratePairSvg(p));
        }

        [Fact]
        public void Settings_AreClamped_Saved_Cloned_AndCopiedInBulk()
        {
            var h = new HighlightItem { ArcCurve = 5, ArcThickness = 0 };
            Assert.Equal(HighlightItem.MaxArcCurve, h.ArcCurve);
            Assert.Equal(HighlightItem.MinArcThickness, h.ArcThickness);
            h.ArcCurve = double.NaN;
            Assert.Equal(HighlightItem.DefaultArcCurve, h.ArcCurve);

            var q = new HighlightItem { Shape = HighlightShape.Arc, ArcCurve = 0.6, ArcThickness = 0.4 };
            var back = JsonSerializer.Deserialize<HighlightItem>(JsonSerializer.Serialize(q))!;
            Assert.Equal((HighlightShape.Arc, 0.6, 0.4), (back.Shape, back.ArcCurve, back.ArcThickness));
            Assert.Equal((0.6, 0.4), (q.Clone().ArcCurve, q.Clone().ArcThickness));
            var old = JsonSerializer.Deserialize<HighlightItem>("{\"Shape\": 0}")!;
            Assert.Equal((HighlightItem.DefaultArcCurve, HighlightItem.DefaultArcThickness), (old.ArcCurve, old.ArcThickness));

            var target = new HighlightItem();
            Assert.Contains(nameof(HighlightItem.ArcCurve), HighlightItem.BulkSettingNames);
            Assert.True(q.CopySettingTo(target, nameof(HighlightItem.ArcCurve)));
            Assert.True(q.CopySettingTo(target, nameof(HighlightItem.ArcThickness)));
            Assert.Equal((0.6, 0.4), (target.ArcCurve, target.ArcThickness));
        }

        [Fact]
        public void TheArcLayout_HasACircleAndAnArc_AndIsUsedByAPreset()
        {
            var items = HighlightStyles.Build(HighlightStyle.Arc);
            Assert.Equal(new[] { HighlightShape.Circle, HighlightShape.Arc }, items.Select(h => h.Shape));
            Assert.Equal(135.0, items[1].Rotation);
            Assert.True(HighlightStyles.IsSmooth(HighlightShape.Arc));
            Assert.Contains(ColorPresets.All, x => x.Highlights!.Any(h => h.Shape == HighlightShape.Arc));
        }
    }
}
