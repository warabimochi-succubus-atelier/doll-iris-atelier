using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DollIrisGenerator.Controls
{
    /// <summary>
    /// Makes a (pixel-scrolling) ScrollViewer move by as much as the wheel or touchpad actually turned.
    ///
    /// WPF's ScrollViewer ignores the size of a wheel event: every event scrolls a whole notch (3 lines = 48 px).
    /// A precision touchpad sends a stream of small events for one two-finger swipe, so each tiny movement became a
    /// full notch and long pages (many symbols / highlights) flew past. With this, one mouse-wheel notch (120)
    /// still scrolls the same 48 px, and a touchpad scrolls in proportion to the finger movement.
    ///
    /// A scrollable control inside the page (a list, a multi-line text box) keeps the wheel while it can still
    /// scroll that way, as before; at its end the page scrolls.
    /// </summary>
    public static class ProportionalWheelScrolling
    {
        /// <summary>Size of one mouse-wheel notch in <see cref="MouseWheelEventArgs.Delta"/>.</summary>
        public const int NotchDelta = 120;

        /// <summary>Height of one "line" of wheel scrolling in WPF, in pixels.</summary>
        public const double LineHeight = 16.0;

        public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
            "IsEnabled", typeof(bool), typeof(ProportionalWheelScrolling), new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

        /// <summary>
        /// How far (pixels, positive = down) a wheel event of <paramref name="wheelDelta"/> scrolls, with the system's
        /// "lines per notch" setting. A negative setting means a page per notch (Windows' "one screen at a time").
        /// A positive delta scrolls up.
        /// </summary>
        public static double ScrollPixels(int wheelDelta, int wheelScrollLines, double viewportHeight)
        {
            double perNotch = wheelScrollLines < 0 ? viewportHeight : wheelScrollLines * LineHeight;
            return -wheelDelta * perNotch / NotchDelta;
        }

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ScrollViewer viewer) return;
            viewer.PreviewMouseWheel -= OnPreviewMouseWheel;
            if ((bool)e.NewValue) viewer.PreviewMouseWheel += OnPreviewMouseWheel;
        }

        private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled || sender is not ScrollViewer viewer || viewer.CanContentScroll || e.Delta == 0) return;
            if (InnerCanScroll(viewer, e.OriginalSource as DependencyObject, e.Delta)) return;
            if (viewer.ScrollableHeight <= 0) return;

            double target = viewer.VerticalOffset + ScrollPixels(e.Delta, SystemParameters.WheelScrollLines, viewer.ViewportHeight);
            viewer.ScrollToVerticalOffset(Math.Clamp(target, 0, viewer.ScrollableHeight));
            e.Handled = true;
        }

        /// <summary>True when a scroll viewer between the pointer and <paramref name="page"/> can still scroll this way.</summary>
        private static bool InnerCanScroll(ScrollViewer page, DependencyObject? source, int delta)
        {
            for (var node = source; node != null && !ReferenceEquals(node, page); node = Parent(node))
            {
                if (node is ScrollViewer inner && inner.ScrollableHeight > 0 &&
                    !ScrollChaining.ShouldPassToParent(delta, inner.VerticalOffset, inner.ScrollableHeight))
                    return true;
            }
            return false;
        }

        private static DependencyObject? Parent(DependencyObject node) =>
            node is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(node)
                : LogicalTreeHelper.GetParent(node); // text elements (Run...) are not in the visual tree
    }
}
