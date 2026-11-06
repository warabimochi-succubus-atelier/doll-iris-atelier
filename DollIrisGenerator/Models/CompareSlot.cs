using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DollIrisGenerator.Models
{
    /// <summary>
    /// One cell of the split pair preview (「分割比較」). Each cell holds its own design and its own undo / redo
    /// history, so switching cells never mixes the edits of two designs.
    /// </summary>
    public sealed class CompareSlot : INotifyPropertyChanged
    {
        private IrisParameters _iris;
        private bool _isSelected;

        public CompareSlot(int index, IrisParameters iris)
        {
            Index = index;
            _iris = iris;
        }

        /// <summary>Zero-based position in the grid.</summary>
        public int Index { get; }

        /// <summary>1-based number shown on the cell.</summary>
        public int Number => Index + 1;

        public IrisParameters Iris
        {
            get => _iris;
            set { if (ReferenceEquals(_iris, value)) return; _iris = value; OnPropertyChanged(); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected == value) return; _isSelected = value; OnPropertyChanged(); }
        }

        // Owned by MainViewModel: the selected cell's lists are the active undo / redo stacks
        internal List<IrisParameters> UndoStack { get; } = new();
        internal List<IrisParameters> RedoStack { get; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
