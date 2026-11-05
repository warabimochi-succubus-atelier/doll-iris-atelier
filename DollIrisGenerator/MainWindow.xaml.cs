using System;
using System.ComponentModel;
using System.Windows;
using DollIrisGenerator.Services;
using DollIrisGenerator.ViewModels;

namespace DollIrisGenerator
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            WindowSettingsService.ApplySettings(this);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // The design being edited is not saved automatically (only favorites are), so confirm first
            bool exporting = DataContext is MainViewModel { IsExportingPdf: true };
            string message = exporting
                ? "PDF を出力中です。今終了すると PDF は正しく保存されません。\n\nそれでも Doll Iris Atelier を終了しますか？"
                : "Doll Iris Atelier を終了しますか？\n\n編集中のデザインは自動保存されません。残したい場合は「💾 JSON保存」または「⭐ お気に入り追加」で保存してください。";
            var answer = MessageBox.Show(this, message, "終了の確認",
                MessageBoxButton.YesNo, exporting ? MessageBoxImage.Warning : MessageBoxImage.Question, MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            base.OnClosing(e);
            WindowSettingsService.SaveSettings(this);
        }

        // リセット / 実寸表示 also bring a panned preview back to the centre (zoom is handled by their commands)
        private void OnResetViewClick(object sender, RoutedEventArgs e)
        {
            PreviewCanvas.ResetPan();
        }
    }
}
