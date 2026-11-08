using System.Windows;
using DollIrisGenerator.Services;

namespace DollIrisGenerator;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // PNG / PDF images are drawn by resvg.dll (inside the exe); say so at once if it cannot be used,
        // rather than failing at the first export
        if (!ResvgRasterizer.TryCheck(out string? error))
        {
            MessageBox.Show(
                "画像を描く部品 (resvg.dll) を読み込めませんでした。PNG と PDF の書き出しはできません。\n" +
                "アプリを入れ直してください。\n\n" + error,
                "resvg.dll を読み込めません", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
