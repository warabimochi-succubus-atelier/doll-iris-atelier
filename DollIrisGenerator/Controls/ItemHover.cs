using System;
using System.Windows;

namespace DollIrisGenerator.Controls
{
    /// <summary>
    /// The decorative symbol / highlight whose card is under the pointer or holds the keyboard focus, so the preview
    /// can ring it (案 C). Set on a card with <c>controls:ItemHover.ItemId="{Binding Id}"</c>. Screen-only state.
    /// </summary>
    public static class ItemHover
    {
        public static readonly DependencyProperty ItemIdProperty = DependencyProperty.RegisterAttached(
            "ItemId", typeof(string), typeof(ItemHover), new PropertyMetadata(null, OnItemIdChanged));

        public static string? GetItemId(DependencyObject element) => (string?)element.GetValue(ItemIdProperty);
        public static void SetItemId(DependencyObject element, string? value) => element.SetValue(ItemIdProperty, value);

        /// <summary>The Id of the item being pointed at or edited, or null.</summary>
        public static string? CurrentId { get; private set; }

        /// <summary>Raised when <see cref="CurrentId"/> changes.</summary>
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
