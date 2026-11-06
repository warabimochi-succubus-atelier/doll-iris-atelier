using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DollIrisGenerator.Controls
{
    /// <summary>
    /// Items host for a TabControl that keeps every tab in the row given by <see cref="RowProperty"/>
    /// (0 = top). Unlike the built-in TabPanel, rows never swap places when a tab is selected.
    /// Each line is stretched to the full width: tabs get as equal a width as their labels allow.
    /// A row that does not fit the width wraps onto further lines, still above the next row.
    /// </summary>
    public class TabRowsPanel : Panel
    {
        public static readonly DependencyProperty RowProperty = DependencyProperty.RegisterAttached(
            "Row", typeof(int), typeof(TabRowsPanel),
            new FrameworkPropertyMetadata(0,
                FrameworkPropertyMetadataOptions.AffectsParentMeasure | FrameworkPropertyMetadataOptions.AffectsParentArrange));

        public static int GetRow(DependencyObject element) => (int)element.GetValue(RowProperty);
        public static void SetRow(DependencyObject element, int value) => element.SetValue(RowProperty, value);

        protected override Size MeasureOverride(Size availableSize)
        {
            foreach (UIElement child in InternalChildren)
                child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            return Layout(availableSize.Width, null);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            Layout(finalSize.Width, (child, rect) => child.Arrange(rect));
            return finalSize;
        }

        /// <summary>Places the children line by line (in child order within a row); returns the size used.</summary>
        private Size Layout(double maxWidth, Action<UIElement, Rect>? place)
        {
            var rows = new SortedDictionary<int, List<UIElement>>();
            foreach (UIElement child in InternalChildren)
            {
                if (child.Visibility == Visibility.Collapsed) continue;
                int row = GetRow(child);
                if (!rows.TryGetValue(row, out var list)) rows[row] = list = new List<UIElement>();
                list.Add(child);
            }

            double y = 0, usedWidth = 0;
            foreach (var row in rows.Values)
            {
                foreach (var line in SplitIntoLines(row, maxWidth))
                {
                    double[] widths = StretchWidths(line.Select(c => c.DesiredSize.Width).ToArray(), maxWidth);
                    double x = 0, lineHeight = line.Max(c => c.DesiredSize.Height);
                    for (int i = 0; i < line.Count; i++)
                    {
                        place?.Invoke(line[i], new Rect(x, y, widths[i], lineHeight));
                        x += widths[i];
                    }
                    usedWidth = Math.Max(usedWidth, x);
                    y += lineHeight;
                }
            }

            return new Size(double.IsInfinity(maxWidth) ? usedWidth : Math.Min(usedWidth, maxWidth), y);
        }

        private static List<List<UIElement>> SplitIntoLines(List<UIElement> row, double maxWidth)
        {
            var lines = new List<List<UIElement>> { new() };
            double x = 0;
            foreach (var child in row)
            {
                double w = child.DesiredSize.Width;
                if (x > 0 && x + w > maxWidth)
                {
                    lines.Add(new List<UIElement>());
                    x = 0;
                }
                lines[^1].Add(child);
                x += w;
            }
            return lines;
        }

        /// <summary>
        /// Widens the tabs of one line to fill <paramref name="lineWidth"/>: short labels are raised to a common width,
        /// labels already wider than that keep their own width (no tab becomes narrower than its label).
        /// </summary>
        public static double[] StretchWidths(double[] desired, double lineWidth)
        {
            var result = (double[])desired.Clone();
            if (double.IsInfinity(lineWidth) || double.IsNaN(lineWidth) || desired.Sum() >= lineWidth) return result;

            var flexible = Enumerable.Range(0, desired.Length).ToList();
            double fixedWidth = 0, common;
            while (true)
            {
                common = (lineWidth - fixedWidth) / flexible.Count;
                var tooWide = flexible.Where(i => desired[i] > common).ToList();
                if (tooWide.Count == 0) break;
                foreach (int i in tooWide)
                {
                    flexible.Remove(i);
                    fixedWidth += desired[i];
                }
            }
            foreach (int i in flexible) result[i] = common;
            return result;
        }
    }
}
