using System;
using System.Linq;
using System.Text.Json;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using Xunit;

namespace DollIrisGenerator.Tests
{
    /// <summary>蝶々 (SPECIFICATION.md 9・13・14): the wings-only butterfly shape for pupils, symbols and highlights.</summary>
    public class ButterflyShapeTests
    {
        [Fact]
        public void Numbers_AreAddedAtTheEnd()
        {
            Assert.Equal(10, (int)SymbolType.Butterfly);
            Assert.Equal(10, (int)HighlightShape.Butterfly);
            Assert.Equal(10, (int)PupilShape.Butterfly);
            Assert.Equal(ShapeKind.Butterfly, ShapeKinds.Of(SymbolType.Butterfly));
            Assert.Equal(ShapeKind.Butterfly, ShapeKinds.Of(HighlightShape.Butterfly));
            Assert.Equal(ShapeKind.Butterfly, ShapeKinds.Of(PupilShape.Butterfly));
        }

        [Fact]
        public void Outline_FitsTheRadius_AndIsLeftRightSymmetric()
        {
            var pts = ShapeOutline.Butterfly(10).Flatten(48);
            Assert.InRange(pts.Max(p => Math.Abs(p.X)), 9.5, 10.5);
            Assert.InRange(pts.Min(p => p.Y), -9.0, -7.5);
            Assert.InRange(pts.Max(p => p.Y), 7.5, 9.0);
            foreach (var q in pts.Where((_, i) => i % 7 == 0))
            {
                double nearest = pts.Min(o => Math.Sqrt((o.X + q.X) * (o.X + q.X) + (o.Y - q.Y) * (o.Y - q.Y)));
                Assert.True(nearest < 0.05, $"no mirror point near ({-q.X:F2}, {q.Y:F2})");
            }
            // Wings on both sides, joined in the middle (the outline crosses the center line only at the top and bottom)
            Assert.Contains(pts, p => p.X > 8 && p.Y < -3);  // upper right wing
            Assert.Contains(pts, p => p.X > 5 && p.Y > 5);   // lower right wing
        }

        [Fact]
        public void PupilButterfly_IsALittleLarger()
        {
            var pupil = ShapeOutline.ForPupil(PupilShape.Butterfly, 10, 10).Flatten(48);
            Assert.InRange(pupil.Max(p => Math.Abs(p.X)), 10.5, 11.5);
        }

        [Fact]
        public void EveryItem_DrawsIt_AndFilesKeepIt()
        {
            var p = new IrisParameters { PupilShape = PupilShape.Butterfly };
            p.Symbols.Add(new DecorativeSymbolItem { Type = SymbolType.Butterfly, Enabled = true });
            p.Highlights.Clear();
            p.Highlights.Add(new HighlightItem { Shape = HighlightShape.Butterfly });
            string svg = SvgExporter.GeneratePairSvg(p);
            Assert.Contains("left_eye_symbol_", svg);
            System.Xml.Linq.XDocument.Parse(svg);

            var restored = JsonSerializer.Deserialize<IrisParameters>(JsonSerializer.Serialize(p))!;
            Assert.Equal(PupilShape.Butterfly, restored.PupilShape);
            Assert.Equal(SymbolType.Butterfly, restored.Symbols.Last().Type);
            Assert.Equal(HighlightShape.Butterfly, restored.Highlights.Single().Shape);
        }
    }
}
