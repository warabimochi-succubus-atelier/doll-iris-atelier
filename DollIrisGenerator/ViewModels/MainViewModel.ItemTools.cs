using System;
using System.Windows.Input;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;

namespace DollIrisGenerator.ViewModels
{
    /// <summary>
    /// The 並べて追加 / 複製 / 選択 / まとめて変更 tools of one item list (装飾記号 or ハイライト), as commands for the
    /// settings tabs. The work is done by the core's <see cref="ItemListEditor{T}"/>; this only adds undo, history
    /// and the status line through <paramref name="run"/>.
    /// </summary>
    public sealed class ItemListTools<T> where T : class, IArrangeableItem<T>
    {
        /// <param name="noun">「装飾記号」 / 「ハイライト」, for history entries and messages.</param>
        /// <param name="run">Runs a change of the design as one undo step: (history entry, change returning the status message).</param>
        public ItemListTools(string noun, Func<T> newItem, Action<string, Func<string>> run)
        {
            Editor = new ItemListEditor<T>(newItem);
            string full = $"これ以上追加できません ({noun}は {IrisParameters.MaxNestedItems} 個まで)。";

            ArrangeCommand = new RelayCommand(() =>
            {
                if (Editor.ArrangeableCount == 0) { run("", () => full); return; }
                run($"{noun}を並べて追加", () =>
                {
                    int added = Editor.AddArranged().Count;
                    string shape = Editor.Layout.Kind == ItemLayoutKind.Line ? "直線" : "円周";
                    return added < Editor.Layout.Count
                        ? $"{noun}を{shape}上に {added} 個追加しました ({IrisParameters.MaxNestedItems} 個までのため、{Editor.Layout.Count - added} 個は追加していません)。"
                        : $"{noun}を{shape}上に {added} 個追加しました。";
                });
            });
            DuplicateCommand = new RelayCommand(param =>
            {
                if (param is not T item) return;
                if (Editor.Items.Count >= IrisParameters.MaxNestedItems) { run("", () => full); return; }
                run($"{noun}を複製", () => Editor.Duplicate(item) != null ? $"{noun}を複製しました (すぐ下に追加しました)。" : full);
            });
            SelectAllCommand = new RelayCommand(() => Editor.SetAllSelected(true));
            ClearSelectionCommand = new RelayCommand(() => Editor.SetAllSelected(false));
            RemoveSelectedCommand = new RelayCommand(() =>
            {
                if (!Editor.HasSelection) return;
                run($"選択した{noun}を削除", () => $"選択した{noun}を {Editor.RemoveSelected()} 個削除しました (元に戻せます)。");
            });
        }

        public ItemListEditor<T> Editor { get; }

        public ICommand ArrangeCommand { get; }
        public ICommand DuplicateCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand ClearSelectionCommand { get; }
        public ICommand RemoveSelectedCommand { get; }
    }

    public partial class MainViewModel
    {
        public ItemListTools<DecorativeSymbolItem> SymbolTools { get; }
        public ItemListTools<HighlightItem> HighlightTools { get; }

        /// <summary>What 「➕ 装飾記号追加」 adds (also the model of 並べて追加 when no symbol is ticked).</summary>
        public static DecorativeSymbolItem NewSymbolItem() => new()
        {
            Type = SymbolType.Star4,
            Count = 1,
            Size = 0.18,
            PositionX = 0.25,
            PositionY = -0.15,
            Color = "#FFFFFF",
            Opacity = 0.85,
            Enabled = true
        };

        /// <summary>What 「➕ ハイライト追加」 adds (also the model of 並べて追加 when no highlight is ticked).</summary>
        public static HighlightItem NewHighlightItem() => new()
        {
            Shape = HighlightShape.Circle,
            PositionX = 0.25,
            PositionY = 0.25,
            Size = 0.15,
            Color = "#FFFFFF",
            Opacity = 0.85,
            Enabled = true
        };

        /// <summary>A change made by an item-list tool: one undo step and one history entry (none when the action is empty).</summary>
        private void RunItemListChange(string action, Func<string> change)
        {
            if (string.IsNullOrEmpty(action))
            {
                StatusMessage = change();
                return;
            }
            PushUndo();
            StatusMessage = change();
            RecordHistory(action);
        }

        /// <summary>Points the tools at the current design's lists (after undo, loads, presets... replace them).</summary>
        private void AttachItemListTools()
        {
            if (_currentIris == null) return;
            SymbolTools?.Editor.Attach(_currentIris.Symbols);
            HighlightTools?.Editor.Attach(_currentIris.Highlights);
        }
    }
}
