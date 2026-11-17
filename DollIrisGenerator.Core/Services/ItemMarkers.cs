using System;
using System.Collections.Generic;
using DollIrisGenerator.Models;

namespace DollIrisGenerator.Services
{
    public enum MarkedItemKind
    {
        Symbol,
        Highlight
    }

    /// <summary>
    /// A ring the preview draws round one decorative symbol / highlight so it can be found in the eye: the ticked
    /// items (選択) and the one whose card is under the pointer or being edited (<see cref="Hovered"/>).
    /// <see cref="X"/> / <see cref="Y"/> are millimetres from the eye's center (Y down), <see cref="Radius"/> in
    /// millimetres; <see cref="Number"/> is the item's 1-based place in its list, as the cards show it.
    /// </summary>
    public readonly record struct ItemMarker(MarkedItemKind Kind, int Number, double X, double Y, double Radius, bool Hovered);

    /// <summary>
    /// Where the selected / hovered items are in each eye, for the preview's markers (screen only: never in the SVG,
    /// PNG or PDF). Uses the same placement as <see cref="SvgExporter"/>: position × the iris's half width / height,
    /// mirrored in the right eye (highlights only with 「ハイライトも左右反転する」), then turned with the iris's tilt.
    /// </summary>
    public static class ItemMarkers
    {
        /// <summary>How much larger than the item the ring is, and the least room it leaves (mm).</summary>
        public const double RingScale = 1.2, RingMinMm = 0.25;

        /// <param name="only">Only this kind (the editor rings the open tab's list); null for both.</param>
        public static IReadOnlyList<ItemMarker> ForEye(IrisParameters p, bool isRightEye, string? hoveredId, MarkedItemKind? only = null)
        {
            var result = new List<ItemMarker>();
            var (rx, ry) = IrisRadiiMm(p);
            double baseR = Math.Min(rx, ry);
            double tilt = isRightEye ? -p.TiltAngle : p.TiltAngle;

            for (int i = 0; only != MarkedItemKind.Highlight && i < p.Symbols.Count; i++)
            {
                var s = p.Symbols[i];
                bool hovered = hoveredId != null && s.Id == hoveredId;
                if (!s.IsSelected && !hovered) continue;
                // A stretched shape (上下左右の長さ) reaches up to its longest length × size from its center;
                // the copies sit on a ring of radius size × 1.4 round the position
                double r = baseR * s.Size * (ShapeStretch.Max(s) + (s.Count > 1 ? 1.4 : 0.0));
                result.Add(Make(MarkedItemKind.Symbol, i + 1, isRightEye ? -s.PositionX : s.PositionX, s.PositionY, r, hovered));
            }

            bool mirrorHighlights = isRightEye && p.MirrorHighlightsWithIris;
            for (int i = 0; only != MarkedItemKind.Symbol && i < p.Highlights.Count; i++)
            {
                var h = p.Highlights[i];
                bool hovered = hoveredId != null && h.Id == hoveredId;
                if (!h.IsSelected && !hovered) continue;
                // As the symbols: up to its longest length from its center, plus the ring of copies
                double r = baseR * h.Size * (ShapeStretch.Max(h) + (h.Count > 1 ? 1.4 : 0.0));
                result.Add(Make(MarkedItemKind.Highlight, i + 1, mirrorHighlights ? -h.PositionX : h.PositionX, h.PositionY, r, hovered));
            }
            return result;

            ItemMarker Make(MarkedItemKind kind, int number, double posX, double posY, double itemRadius, bool hovered)
            {
                double x = posX * rx, y = posY * ry;
                if (Math.Abs(tilt) > 0.05)
                {
                    // SVG rotate(): clockwise on screen (Y down)
                    double a = tilt * Math.PI / 180.0, c = Math.Cos(a), sn = Math.Sin(a);
                    (x, y) = (x * c - y * sn, x * sn + y * c);
                }
                return new ItemMarker(kind, number, x, y, Math.Max(itemRadius * RingScale, itemRadius + RingMinMm), hovered);
            }
        }

        /// <summary>The iris's half width and half height in mm (as <see cref="SvgExporter"/> draws it).</summary>
        public static (double Rx, double Ry) IrisRadiiMm(IrisParameters p)
        {
            double widthMm = p.SizeMm, heightMm = p.SizeMm;
            if (p.Shape == IrisShape.VerticalOval) heightMm *= p.AspectRatio;
            else if (p.Shape == IrisShape.HorizontalOval) widthMm *= p.AspectRatio;
            return (widthMm / 2.0, heightMm / 2.0);
        }
    }
}
