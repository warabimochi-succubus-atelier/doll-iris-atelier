using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DollIrisGenerator.Controls
{
    /// <summary>
    /// For a scrolling list placed inside a scrolling page: once the list cannot scroll any further in the
    /// wheel's direction (or has nothing to scroll), the mouse wheel scrolls the page instead.
    /// Without this, WPF's inner ScrollViewer always swallows the wheel, so the page stops scrolling
    /// whenever the pointer is over the list.
    /// </summary>
    public static class ScrollChaining
    {
        public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
            "IsEnabled", typeof(bool), typeof(ScrollChaining), new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not UIElement element) return;
            element.PreviewMouseWheel -= OnPreviewMouseWheel;
            if ((bool)e.NewValue) element.PreviewMouseWheel += OnPreviewMouseWheel;
        }

        /// <summary>
        /// True when an inner list at <paramref name="verticalOffset"/> (of <paramref name="scrollableHeight"/>)
        /// cannot move in the wheel's direction, so the wheel should go to the page around it.
        /// A positive <paramref name="wheelDelta"/> scrolls up.
        /// </summary>
        public static bool ShouldPassToParent(int wheelDelta, double verticalOffset, double scrollableHeight)
        {
            const double epsilon = 0.5;
            if (wheelDelta == 0) return false;
            return wheelDelta > 0
                ? verticalOffset <= epsilon
                : verticalOffset >= scrollableHeight - epsilon;
        }

        private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled || sender is not DependencyObject element) return;

            var inner = FindScrollViewer(element);
            if (inner != null && !ShouldPassToParent(e.Delta, inner.VerticalOffset, inner.ScrollableHeight)) return;

            if (VisualTreeHelper.GetParent(element) is not UIElement parent) return;
            e.Handled = true;
            parent.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = element
            });
        }

        private static ScrollViewer? FindScrollViewer(DependencyObject root)
        {
            if (root is ScrollViewer viewer) return viewer;
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var found = FindScrollViewer(VisualTreeHelper.GetChild(root, i));
                if (found != null) return found;
            }
            return null;
        }
    }
}
