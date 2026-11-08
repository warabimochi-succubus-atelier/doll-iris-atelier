using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Input;
using DollIrisGenerator.Models;

namespace DollIrisGenerator.ViewModels
{
    // 「分割比較」: the pair preview can be split into up to 9 cells, each holding its own design.
    // The selected cell is the one every tab, command, export and undo / redo works on (CurrentIris).
    public partial class MainViewModel
    {
        public const int MinCompareCount = 1;
        public const int MaxCompareCount = 9;
        public const int DefaultCompareCount = 4;

        // Every cell created so far. Lowering the split count only hides cells, so raising it again brings them back.
        private readonly List<CompareSlot> _allSlots = new();
        // Each cell's design as it was when the cell was created, to tell whether closing the app would lose anything
        private readonly Dictionary<CompareSlot, string> _slotStartJson = new();
        private readonly ObservableCollection<CompareSlot> _visibleSlots = new();
        private CompareSlot _selectedSlot = null!; // set in InitializeCompareSlots (constructor)
        private int _compareCount;

        /// <summary>Split counts offered in the toolbar: each one fills its grid with no empty cell.</summary>
        public static IReadOnlyList<int> CompareCountOptions { get; } = new[] { 1, 2, 4, 6, 9 };

        /// <summary>The smallest offered split count that holds <paramref name="requested"/> cells (3 → 4, 7 → 9).</summary>
        public static int SnapCompareCount(int requested)
        {
            int count = Math.Clamp(requested, MinCompareCount, MaxCompareCount);
            return CompareCountOptions.First(option => option >= count);
        }

        /// <summary>Cells shown in the pair preview, in grid order.</summary>
        public ObservableCollection<CompareSlot> VisibleSlots => _visibleSlots;

        public CompareSlot SelectedSlot => _selectedSlot;

        /// <summary>Number of cells in the pair preview: one of <see cref="CompareCountOptions"/> (other values are snapped up).</summary>
        public int CompareCount
        {
            get => _compareCount;
            set
            {
                int count = SnapCompareCount(value);
                if (count == _compareCount) return;

                // The selected cell is about to be hidden: move the selection to the last cell that stays visible
                if (_selectedSlot.Index >= count && !SelectSlot(_allSlots[count - 1]))
                {
                    OnPropertyChanged(); // selection was refused: the UI goes back to the current count
                    return;
                }

                _compareCount = count;
                while (_allSlots.Count < count)
                {
                    // A new cell starts as a copy of the selected design, ready to branch off and compare
                    AddSlot(new CompareSlot(_allSlots.Count, CurrentIris.Clone()));
                }
                _visibleSlots.Clear();
                for (int i = 0; i < count; i++) _visibleSlots.Add(_allSlots[i]);

                OnPropertyChanged();
                OnPropertyChanged(nameof(CompareColumns));
                OnPropertyChanged(nameof(CompareRows));
                OnPropertyChanged(nameof(IsCompareSplit));
            }
        }

        public int CompareRows => (int)Math.Ceiling(_compareCount / (double)CompareColumns);

        /// <summary>
        /// Grid columns for the current split. A pair is about twice as wide as it is tall, so cells are stacked
        /// in more rows than columns: 2 → 1 column × 2 rows, 4 → 2×2, 6 → 2 columns × 3 rows, 9 → 3×3.
        /// </summary>
        public int CompareColumns => GetCompareColumns(_compareCount);

        public static int GetCompareColumns(int count) => Math.Max(1, (int)Math.Ceiling(Math.Sqrt(count / 2.0)));

        public bool IsCompareSplit => _compareCount > 1;

        public ICommand SelectSlotCommand { get; private set; } = null!;

        private void InitializeCompareSlots()
        {
            var first = new CompareSlot(0, _currentIris) { IsSelected = true };
            AddSlot(first);
            _selectedSlot = first;
            _undoStack = first.UndoStack;
            _redoStack = first.RedoStack;
            CompareCount = DefaultCompareCount;
            SelectSlotCommand = new RelayCommand(p => { if (p is CompareSlot slot) SelectSlot(slot); });
        }

        /// <summary>
        /// Makes <paramref name="slot"/> the design being edited. Returns false when <paramref name="slot"/>
        /// is not one of the compare cells.
        /// </summary>
        public bool SelectSlot(CompareSlot slot)
        {
            if (ReferenceEquals(slot, _selectedSlot)) return true;
            if (!_allSlots.Contains(slot)) return false;

            CommitManualEdit(); // a pending slider edit stays an undo step of the cell it was made on

            _selectedSlot.IsSelected = false;
            _selectedSlot = slot;
            slot.IsSelected = true;
            _undoStack = slot.UndoStack;
            _redoStack = slot.RedoStack;

            CurrentIris = slot.Iris;
            OnPropertyChanged(nameof(SelectedSlot));
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            CommandManager.InvalidateRequerySuggested(); // undo / redo buttons follow the new cell's history
            StatusMessage = $"枠 {slot.Number}「{slot.Iris.Name}」を選択しました。設定タブでこのデザインを編集できます。";
            return true;
        }

        private void AddSlot(CompareSlot slot)
        {
            _allSlots.Add(slot);
            _slotStartJson[slot] = JsonSerializer.Serialize(slot.Iris);
        }

        /// <summary>
        /// True when some cell holds a design that would be lost on exit: it was changed since the cell was
        /// created and is not saved as a favorite either. Designs are not saved automatically, so only then
        /// is it worth asking before closing.
        /// </summary>
        public bool HasUnsavedDesigns()
        {
            var favoriteJson = Favorites.Select(f => JsonSerializer.Serialize(f.Parameters)).ToHashSet();
            return _allSlots.Any(slot =>
            {
                string json = JsonSerializer.Serialize(slot.Iris);
                return json != _slotStartJson[slot] && !favoriteJson.Contains(json);
            });
        }

        private void ClearAllUndoHistory()
        {
            foreach (var slot in _allSlots)
            {
                slot.UndoStack.Clear();
                slot.RedoStack.Clear();
            }
        }
    }
}
