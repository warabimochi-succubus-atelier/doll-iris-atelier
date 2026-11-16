using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using DollIrisGenerator.Services;

namespace DollIrisGenerator.Controls
{
    /// <summary>
    /// The decorative symbol / highlight whose card is under the pointer or holds the keyboard focus, so the preview
    /// can ring it (案 C). Set on a card with <c>controls:ItemHover.ItemId="{Binding Id}"</c>. Screen-only state.
    /// Also which list's rings the preview shows (<see cref="ShownKind"/>): only while that list's tab is open, set on
    /// the tab with <c>controls:ItemHover.MarkersFor="Symbol"</c>.
    /// </summary>
    public static class ItemHover
    {
        public static readonly DependencyProperty ItemIdProperty = DependencyProperty.RegisterAttached(
            "ItemId", typeof(string), typeof(ItemHover), new PropertyMetadata(null, OnItemIdChanged));

        public static string? GetItemId(DependencyObject element) => (string?)element.GetValue(ItemIdProperty);
        public static void SetItemId(DependencyObject element, string? value) => element.SetValue(ItemIdProperty, value);

        /// <summary>The Id of the item being pointed at or edited, or null.</summary>
        public static string? CurrentId { get; private set; }

        public static readonly DependencyProperty MarkersForProperty = DependencyProperty.RegisterAttached(
            "MarkersFor", typeof(MarkedItemKind?), typeof(ItemHover), new PropertyMetadata(null, OnMarkersForChanged));

        public static MarkedItemKind? GetMarkersFor(DependencyObject element) => (MarkedItemKind?)element.GetValue(MarkersForProperty);
        public static void SetMarkersFor(DependencyObject element, MarkedItemKind? value) => element.SetValue(MarkersForProperty, value);

        /// <summary>The list whose rings the preview shows (its tab is open), or null for none.</summary>
        public static MarkedItemKind? ShownKind { get; private set; }

        /// <summary>
        /// Sets <see cref="ShownKind"/> (also for tests). The pointed-at card is forgotten: it went away with the
        /// tab, and may not have seen the pointer leave.
        /// </summary>
        public static void SetShownKind(MarkedItemKind? kind)
        {
            if (ShownKind == kind) return;
            ShownKind = kind;
            CurrentId = null;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>Raised when <see cref="CurrentId"/> or <see cref="ShownKind"/> changes.</summary>
        public static event EventHandler? Changed;

        /// <summary>Sets <see cref="CurrentId"/> (also for tests).</summary>
        public static void SetCurrent(string? id)
        {
            if (CurrentId == id) return;
            CurrentId = id;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        private static void OnItemIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not UIElement card) return;
            card.MouseEnter -= OnEnter;
            card.MouseLeave -= OnLeave;
            card.IsKeyboardFocusWithinChanged -= OnFocusChanged;
            if (e.NewValue == null) return;
            card.MouseEnter += OnEnter;
            card.MouseLeave += OnLeave;
            card.IsKeyboardFocusWithinChanged += OnFocusChanged;
        }

        private static void OnMarkersForChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not UIElement tab) return;
            tab.RemoveHandler(Selector.SelectedEvent, (RoutedEventHandler)OnTabSelected);
            tab.RemoveHandler(Selector.UnselectedEvent, (RoutedEventHandler)OnTabUnselected);
            if (e.NewValue == null) return;
            tab.AddHandler(Selector.SelectedEvent, (RoutedEventHandler)OnTabSelected);
            tab.AddHandler(Selector.UnselectedEvent, (RoutedEventHandler)OnTabUnselected);
            if (Selector.GetIsSelected(tab)) SetShownKind((MarkedItemKind?)e.NewValue);
        }

        // Selected / Unselected also bubble up from lists and combo boxes inside the tab: only the tab's own count
        private static void OnTabSelected(object sender, RoutedEventArgs e)
        {
            if (ReferenceEquals(e.OriginalSource, sender)) SetShownKind(GetMarkersFor((DependencyObject)sender));
        }

        private static void OnTabUnselected(object sender, RoutedEventArgs e)
        {
            // The newly opened tab may already have set its own kind
            if (ReferenceEquals(e.OriginalSource, sender) && ShownKind == GetMarkersFor((DependencyObject)sender)) SetShownKind(null);
        }

        private static void OnEnter(object sender, System.Windows.Input.MouseEventArgs e) =>
            SetCurrent(GetItemId((DependencyObject)sender));

        private static void OnLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var card = (UIElement)sender;
            // Still being edited from the keyboard: keep it; otherwise only clear our own mark
            if (card.IsKeyboardFocusWithin) return;
            if (CurrentId == GetItemId(card)) SetCurrent(null);
        }

        private static void OnFocusChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var card = (UIElement)sender;
            if (card.IsKeyboardFocusWithin) SetCurrent(GetItemId(card));
            else if (!card.IsMouseOver && CurrentId == GetItemId(card)) SetCurrent(null);
        }
    }
}
