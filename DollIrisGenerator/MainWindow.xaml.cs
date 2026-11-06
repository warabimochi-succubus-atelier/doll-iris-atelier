using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DollIrisGenerator.Controls;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using DollIrisGenerator.ViewModels;

namespace DollIrisGenerator
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            var settings = WindowSettingsService.LoadSettings();
            ColorPickerBox.PreferredInputMode = settings.ColorInputMode; // before the color boxes are created
            InitializeComponent();
            WindowSettingsService.ApplySettings(this, settings);
            if (DataContext is MainViewModel startVm)
            {
                startVm.RandomizeLocks = settings.RandomizeLocks; // 「✨ 属性ランダム生成」 locks from the last session
            }
            Loaded += (_, _) =>
            {
                if (DataContext is not MainViewModel vm) return;
                vm.ShowFavoritesLoadProblem();
                vm.ShowPrintSheetLoadProblem();
                // A different view starts centred
                vm.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(MainViewModel.PreviewMode)) ResetAllPans();
                    // More cells → smaller cells: shrink the zoom when the pairs no longer fit (never enlarges)
                    if (args.PropertyName == nameof(MainViewModel.CompareCount)) FitCompareZoom(shrinkOnly: true);
                };
                // A small window may not fit the default 4 cells at the default zoom (after the first layout pass)
                Dispatcher.BeginInvoke(() => FitCompareZoom(shrinkOnly: true), System.Windows.Threading.DispatcherPriority.Loaded);
            };
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // The design being edited is not saved automatically (only favorites and the A4 sheet are), so confirm first
            bool exporting = DataContext is MainViewModel { IsExportingPdf: true };
            string message = exporting
                ? "PDF を出力中です。今終了すると PDF は正しく保存されません。\n\nそれでも Doll Iris Atelier を終了しますか？"
                : "Doll Iris Atelier を終了しますか？\n\n編集中のデザインは自動保存されません。残したい場合は「⭐ お気に入りに追加」で保存してください。\n" +
                  "（A4印刷シートの中身と設定は保存され、次回起動時に復元されます）";
            var answer = MessageBox.Show(this, message, "終了の確認",
                MessageBoxButton.YesNo, exporting ? MessageBoxImage.Warning : MessageBoxImage.Question, MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            if (DataContext is MainViewModel vm && !vm.SavePrintSheetBeforeExit())
            {
                e.Cancel = true;
                return;
            }

            base.OnClosing(e);
            WindowSettingsService.SaveSettings(this, colorInputMode: ColorPickerBox.PreferredInputMode,
                randomizeLocks: (DataContext as MainViewModel)?.RandomizeLocks ?? Models.RandomizeLocks.None);
        }

        // リセット / 実寸表示 also bring a panned preview back to the centre (zoom is handled by their commands)
        private void OnResetViewClick(object sender, RoutedEventArgs e) => ResetAllPans();

        /// <summary>Re-centres the A4 preview and every cell of the split pair preview.</summary>
        private void ResetAllPans()
        {
            PreviewCanvas.ResetPan();
            foreach (var cell in FindDescendants<IrisPreviewControl>(CompareGrid)) cell.ResetPan();
        }

        private static IEnumerable<T> FindDescendants<T>(DependencyObject root) where T : DependencyObject
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T match) yield return match;
                foreach (var nested in FindDescendants<T>(child)) yield return nested;
            }
        }

        private void OnFitCompareZoomClick(object sender, RoutedEventArgs e) => FitCompareZoom(shrinkOnly: false);

        // Cell border (2 DIP each side) + margin (1 DIP each side) of the split preview's cell template
        private const double CompareCellChrome = 6.0;

        /// <summary>
        /// Sets the pair view's zoom so every visible cell's pair fits in its cell and re-centres the cells.
        /// With <paramref name="shrinkOnly"/>, the zoom is only lowered (and only when the pairs overflow).
        /// </summary>
        private void FitCompareZoom(bool shrinkOnly)
        {
            if (DataContext is not MainViewModel vm || CompareGrid.ActualWidth <= 0 || CompareGrid.ActualHeight <= 0) return;

            double cellW = CompareGrid.ActualWidth / vm.CompareColumns - CompareCellChrome;
            double cellH = CompareGrid.ActualHeight / vm.CompareRows - CompareCellChrome;
            double fit = IrisPreviewControl.FitPairZoom(vm.VisibleSlots.Select(s => s.Iris), vm.SheetConfig.PairSpacingXMm, cellW, cellH);
            if (shrinkOnly && fit >= vm.PairZoomScale) return;

            vm.PairZoomScale = fit;
            ResetAllPans();
            vm.StatusMessage = $"アイが枠に収まる拡大率 ({vm.PairZoomScale * 100:F0}%) にしました。";
        }

        // Clicking anywhere in a cell of the split preview makes that cell's design the one being edited.
        // Preview (tunnelling) event: the cell still pans / zooms as before with the same click.
        private void OnCompareCellMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: CompareSlot slot } && DataContext is MainViewModel vm)
            {
                vm.SelectSlot(slot);
            }
        }
    }
}
