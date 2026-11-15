using System;
using System.Collections.Generic;
using System.Linq;
using DollIrisGenerator.Models;

namespace DollIrisGenerator.Services
{
    /// <summary>One place computed by 並べて追加: position (iris-radius units) and rotation (degrees).</summary>
    public readonly record struct ItemPlacement(double X, double Y, double Rotation);

    /// <summary>
    /// 並べて追加 and 複製 for the design's decorative symbols and highlights. Only positions and rotations are
    /// computed here; the items are ordinary list entries afterwards (saved as before, drawn as before).
    /// </summary>
    public static class ItemLayout
    {
        /// <summary>How far 複製 moves the copy (right and down, or the other way near the edge).</summary>
        public const double DuplicateOffset = 0.1;

        /// <summary>Random candidates tried per item; the one farthest from everything already placed wins.</summary>
        public const int CandidatesPerItem = 30;

        /// <summary>
        /// Scatters <see cref="ItemLayoutSettings.Count"/> places at random in the ring between the inner and outer
        /// distance from the iris center (evenly over its area). Each place is the best of
        /// <see cref="CandidatesPerItem"/> random tries: the one farthest from the places already taken and from
        /// <paramref name="occupied"/> (the items already in the list), so the items spread out instead of piling up.
        /// The result is in reading order (see <see cref="ReadingOrder"/>); positions are rounded to 0.001 and
        /// rotations to whole degrees.
        /// </summary>
        /// <param name="itemSize">The new items' size (radius ratio): the height of a row for the reading order.</param>
        public static IReadOnlyList<ItemPlacement> Compute(ItemLayoutSettings s, Random random, double itemSize,
            IEnumerable<(double X, double Y)>? occupied = null)
        {
            double inner = Math.Min(s.InnerRadius, s.OuterRadius);
            double outer = Math.Max(s.InnerRadius, s.OuterRadius);
            var taken = occupied?.ToList() ?? new List<(double X, double Y)>();
            var result = new List<ItemPlacement>(s.Count);

            for (int i = 0; i < s.Count; i++)
            {
                (double X, double Y) best = default;
                double bestDistance = double.NegativeInfinity;
                for (int c = 0; c < CandidatesPerItem; c++)
                {
                    // Uniform over the ring's area: the radius grows with the square root
                    double r = Math.Sqrt(inner * inner + random.NextDouble() * (outer * outer - inner * inner));
                    double a = random.NextDouble() * 2.0 * Math.PI;
                    var candidate = (X: Math.Round(r * Math.Cos(a), 3), Y: Math.Round(r * Math.Sin(a), 3));
                    double nearest = taken.Count == 0
                        ? double.PositiveInfinity
                        : taken.Min(t => (t.X - candidate.X) * (t.X - candidate.X) + (t.Y - candidate.Y) * (t.Y - candidate.Y));
                    if (nearest > bestDistance)
                    {
                        bestDistance = nearest;
                        best = candidate;
                    }
                }
                taken.Add(best);
                double rotation = s.RandomRotation ? NormalizeAngle(Math.Round(random.NextDouble() * 360.0 - 180.0)) : 0.0;
                result.Add(new ItemPlacement(best.X, best.Y, rotation));
            }
            return ReadingOrder(result, itemSize);
        }

        /// <summary>
        /// Top left to bottom right, as text is read: the places are cut into rows as high as an item
        /// (2 × <paramref name="itemSize"/>, at least 0.1), rows from the top, and left to right within a row.
        /// </summary>
        public static IReadOnlyList<ItemPlacement> ReadingOrder(IEnumerable<ItemPlacement> places, double itemSize)
        {
            double row = Math.Max(2.0 * itemSize, 0.1);
            return places
                .OrderBy(p => Math.Floor((p.Y + 2.0) / row))
                .ThenBy(p => p.X)
                .ThenBy(p => p.Y)
                .ToList();
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
        /// Appends copies of <paramref name="template"/> at random places (its own position and rotation are not used;
        /// its size sets the rows of the reading order).
        /// Stops at <paramref name="max"/> items in the list. Returns the new items, in the order they were added.
        /// </summary>
        public static List<T> AddArranged<T>(IList<T> items, T template, ItemLayoutSettings settings, Random random,
            int max = IrisParameters.MaxNestedItems)
            where T : IArrangeableItem<T>
        {
            var added = new List<T>();
            int room = Math.Max(0, max - items.Count);
            if (room == 0) return added;
            var occupied = items.Select(i => (i.PositionX, i.PositionY)).ToList();
            foreach (var place in Compute(settings, random, template.Size, occupied).Take(room))
            {
                var item = template.Clone();
                item.IsSelected = false;
                item.PositionX = place.X;
                item.PositionY = place.Y;
                item.Rotation = place.Rotation;
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
            v + DuplicateOffset <= ItemLayoutSettings.MaxRadius + 1e-9 ? v + DuplicateOffset : v - DuplicateOffset;
    }
}
