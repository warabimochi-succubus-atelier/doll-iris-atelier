using System;
using System.Collections.Generic;
using DollIrisGenerator.Models;

namespace DollIrisGenerator.Services
{
    /// <summary>One place computed by 並べて追加: position (iris-radius units) and the angle along the layout (degrees).</summary>
    public readonly record struct ItemPlacement(double X, double Y, double Angle);

    /// <summary>
    /// 並べて追加 and 複製 for the design's decorative symbols and highlights. Only positions and rotations are
    /// computed here; the items are ordinary list entries afterwards (saved as before, drawn as before).
    /// </summary>
    public static class ItemLayout
    {
        /// <summary>How far 複製 moves the copy (right and down, or the other way near the edge).</summary>
        public const double DuplicateOffset = 0.1;

        public static IReadOnlyList<ItemPlacement> Compute(ItemLayoutSettings s)
        {
            int n = s.Count;
            var result = new List<ItemPlacement>(n);
            if (s.Kind == ItemLayoutKind.Line)
            {
                double dx = s.EndX - s.StartX, dy = s.EndY - s.StartY;
                double angle = (dx == 0 && dy == 0) ? 0.0 : Math.Atan2(dy, dx) * 180.0 / Math.PI;
                for (int i = 0; i < n; i++)
                {
                    double t = n == 1 ? 0.0 : (double)i / (n - 1);
                    result.Add(new ItemPlacement(s.StartX + dx * t, s.StartY + dy * t, NormalizeAngle(angle)));
                }
                return result;
            }

            // A full circle spaces the items evenly; an arc puts the first and the last at its two ends
            bool full = s.SweepAngle >= 360.0 - 1e-9;
            double step = full ? 360.0 / n : (n == 1 ? 0.0 : s.SweepAngle / (n - 1));
            for (int i = 0; i < n; i++)
            {
                double deg = s.StartAngle + step * i;
                double rad = deg * Math.PI / 180.0;
                // 0° above the center, clockwise (Y grows downward)
                double x = s.CenterX + s.Radius * Math.Sin(rad);
                double y = s.CenterY - s.Radius * Math.Cos(rad);
                result.Add(new ItemPlacement(x, y, NormalizeAngle(deg)));
            }
            return result;
        }

        /// <summary>The same angle in (−180°, 180°], the range of the rotation sliders.</summary>
        public static double NormalizeAngle(double degrees)
        {
            if (!double.IsFinite(degrees)) return 0.0;
            double a = Math.IEEERemainder(degrees, 360.0); // [-180, 180]
            if (a <= -180.0) a += 360.0;
            return Math.Abs(a) < 1e-9 ? 0.0 : a;
        }

        /// <summary>
        /// Appends copies of <paramref name="model"/> at the layout's places (its own position is not used; its rotation
        /// is the base the layout's angle is added to when <see cref="ItemLayoutSettings.AlignRotation"/> is on).
        /// Stops at <paramref name="max"/> items in the list. Returns the new items.
        /// </summary>
        public static List<T> AddArranged<T>(IList<T> items, T model, ItemLayoutSettings settings, int max = IrisParameters.MaxNestedItems)
            where T : IArrangeableItem<T>
        {
            var added = new List<T>();
            foreach (var place in Compute(settings))
            {
                if (items.Count >= max) break;
                var item = model.Clone();
                item.IsSelected = false;
                item.PositionX = place.X;
                item.PositionY = place.Y;
                item.Rotation = settings.AlignRotation ? NormalizeAngle(model.Rotation + place.Angle) : model.Rotation;
                items.Add(item);
                added.Add(item);
            }
            return added;
        }

        /// <summary>
        /// Inserts a copy of <paramref name="item"/> right after it, moved by <see cref="DuplicateOffset"/> right and down
        /// (left / up where that would leave the position sliders' range). Null when the list is full or the item is not in it.
        /// </summary>
        public static T? DuplicateAfter<T>(IList<T> items, T item, int max = IrisParameters.MaxNestedItems)
            where T : class, IArrangeableItem<T>
        {
            int index = items.IndexOf(item);
            if (index < 0 || items.Count >= max) return null;
            var copy = item.Clone();
            copy.IsSelected = false;
            copy.PositionX = Shift(item.PositionX);
            copy.PositionY = Shift(item.PositionY);
            items.Insert(index + 1, copy);
            return copy;
        }

        private static double Shift(double v) =>
            v + DuplicateOffset <= ItemLayoutSettings.PositionLimit + 1e-9 ? v + DuplicateOffset : v - DuplicateOffset;
    }
}
