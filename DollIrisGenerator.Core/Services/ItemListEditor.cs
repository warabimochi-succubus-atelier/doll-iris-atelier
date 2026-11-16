using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using DollIrisGenerator.Models;

namespace DollIrisGenerator.Services
{
    /// <summary>
    /// The editing tools of one item list of the design (decorative symbols or highlights), shared by the desktop
    /// and browser apps: the 並べて追加 form, 複製, and まとめて変更 of the ticked items.
    ///
    /// まとめて変更 works through <see cref="Bulk"/>, an item that is not in the design: it shows the first ticked
    /// item's settings, and changing one of its settings sets that one setting (only) on every ticked item.
    /// The screens bind <see cref="Bulk"/> to the same controls as a list item.
    /// </summary>
    public sealed class ItemListEditor<T> : INotifyPropertyChanged where T : class, IArrangeableItem<T>
    {
        private readonly Func<T> _newItem;
        private ObservableCollection<T> _items = new();
        private bool _loadingBulk;

        /// <param name="newItem">What 「➕ 追加」 adds; also the starting look of the 並べて追加 form.</param>
        /// <param name="arrangeCount">The 並べて追加 form's starting 個数.</param>
        public ItemListEditor(Func<T> newItem, int arrangeCount = ItemLayoutSettings.DefaultSymbolCount)
        {
            _newItem = newItem;
            Layout.Count = arrangeCount;
            Template = newItem();
            Bulk = newItem();
            Bulk.PropertyChanged += OnBulkPropertyChanged;
            Layout.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ItemLayoutSettings.Count)) OnPropertyChanged(nameof(ArrangeableCount));
            };
        }

        public ItemLayoutSettings Layout { get; } = new();

        /// <summary>
        /// The look of the items 並べて追加 makes (type / shape, color, size, corner rounding, opacity, blend...),
        /// edited in the form itself. Not in the design; its position and rotation are not used.
        /// </summary>
        public T Template { get; }

        /// <summary>Where 並べて追加 draws its random places from (tests set a seeded one).</summary>
        public Random Random { get; set; } = Random.Shared;

        /// <summary>The まとめて変更 values (see the class summary).</summary>
        public T Bulk { get; }

        public ObservableCollection<T> Items => _items;

        public IReadOnlyList<T> Selected => _items.Where(i => i.IsSelected).ToList();

        public int SelectedCount => _items.Count(i => i.IsSelected);

        public bool HasSelection => _items.Any(i => i.IsSelected);

        /// <summary>Raised after a まとめて変更 change was set on the ticked items (with the setting's name).</summary>
        public event Action<string>? BulkSettingChanged;

        /// <summary>Raised when items are ticked / unticked, added or removed.</summary>
        public event Action? SelectionChanged;

        /// <summary>
        /// Points the editor at the design's list (call again when the design or the list is replaced: undo, preset...).
        /// Ticks live on the items, so a list restored from a snapshot comes back with nothing ticked.
        /// </summary>
        public void Attach(ObservableCollection<T> items)
        {
            if (ReferenceEquals(items, _items)) return;
            _items.CollectionChanged -= OnCollectionChanged;
            foreach (var item in _items) item.PropertyChanged -= OnItemPropertyChanged;
            _items = items;
            _items.CollectionChanged += OnCollectionChanged;
            foreach (var item in _items) item.PropertyChanged += OnItemPropertyChanged;
            OnSelectionChanged();
        }

        public T NewItem() => _newItem();

        public void SetAllSelected(bool selected)
        {
            foreach (var item in _items.ToList()) item.IsSelected = selected;
        }

        /// <summary>
        /// 並べて追加: adds <see cref="Template"/> copies at random places (in reading order, see <see cref="ItemLayout.Compute"/>).
        /// Stops when the list holds <see cref="IrisParameters.MaxNestedItems"/>.
        /// </summary>
        public List<T> AddArranged() => ItemLayout.AddArranged(_items, Template, Layout, Random);

        /// <summary>How many 並べて追加 would add now (fewer than asked near the limit).</summary>
        public int ArrangeableCount => Math.Max(0, Math.Min(Layout.Count, IrisParameters.MaxNestedItems - _items.Count));

        public T? Duplicate(T item) => ItemLayout.DuplicateAfter(_items, item);

        /// <summary>Removes the ticked items; returns how many.</summary>
        public int RemoveSelected()
        {
            var selected = Selected;
            foreach (var item in selected) _items.Remove(item);
            return selected.Count;
        }

        private void OnBulkPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_loadingBulk || e.PropertyName == null) return;
            if (!T.BulkSettingNames.Contains(e.PropertyName)) return;
            var selected = Selected;
            if (selected.Count == 0) return;
            foreach (var item in selected) Bulk.CopySettingTo(item, e.PropertyName);
            BulkSettingChanged?.Invoke(e.PropertyName);
        }

        private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IArrangeableItem.IsSelected)) OnSelectionChanged();
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null) foreach (T item in e.OldItems) item.PropertyChanged -= OnItemPropertyChanged;
            if (e.NewItems != null) foreach (T item in e.NewItems) item.PropertyChanged += OnItemPropertyChanged;
            if (e.Action == NotifyCollectionChangedAction.Reset)
                foreach (var item in _items) { item.PropertyChanged -= OnItemPropertyChanged; item.PropertyChanged += OnItemPropertyChanged; }
            OnSelectionChanged();
        }

        private void OnSelectionChanged()
        {
            // まとめて変更 starts from the first ticked item's settings
            var first = _items.FirstOrDefault(i => i.IsSelected);
            if (first != null)
            {
                _loadingBulk = true;
                try
                {
                    foreach (var name in T.BulkSettingNames) first.CopySettingTo(Bulk, name);
                }
                finally
                {
                    _loadingBulk = false;
                }
            }
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(ArrangeableCount));
            SelectionChanged?.Invoke();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
