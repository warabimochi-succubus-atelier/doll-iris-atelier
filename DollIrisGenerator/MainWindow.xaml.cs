using System;
using System.ComponentModel;
using System.Windows;
using DollIrisGenerator.Controls;
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
            Loaded += (_, _) =>
            {
                if (DataContext is not MainViewModel vm) return;
                vm.ShowFavoritesLoadProblem();
                vm.ShowPrintSheetLoadProblem();
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
            WindowSettingsService.SaveSettings(this, colorInputMode: ColorPickerBox.PreferredInputMode);
        }

        // リセット / 実寸表示 also bring a panned preview back to the centre (zoom is handled by their commands)
        private void OnResetViewClick(object sender, RoutedEventArgs e)
        {
            PreviewCanvas.ResetPan();
        }
    }
}
