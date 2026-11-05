using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml;
using System.Xml.Linq;
using DollIrisGenerator.Controls;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using Microsoft.Win32;

namespace DollIrisGenerator.ViewModels
{
    // File output: JSON save/load, PDF / SVG / PNG export
    public partial class MainViewModel
    {
        private static string GetSafeFileName(string? name, string fallback = "DollIris")
        {
            if (string.IsNullOrWhiteSpace(name)) return fallback;
            var invalidChars = Path.GetInvalidFileNameChars();
            var cleaned = string.Join("_", name.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).Trim();
            return string.IsNullOrWhiteSpace(cleaned) ? fallback : cleaned;
        }

        private void OnSaveJson()
        {
            var sfd = new SaveFileDialog
            {
                Filter = "JSON パラメータファイル (*.json)|*.json",
                FileName = $"{GetSafeFileName(CurrentIris.Name)}_{CurrentIris.SizeMm:F1}mm.json",
                Title = "虹彩パラメータを保存"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    var opt = new JsonSerializerOptions { WriteIndented = true };
                    string json = JsonSerializer.Serialize(CurrentIris, opt);
                    File.WriteAllText(sfd.FileName, json);
                    StatusMessage = $"パラメータをJSONファイルに保存しました: {Path.GetFileName(sfd.FileName)}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"保存エラー: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OnLoadJson()
        {
            var ofd = new OpenFileDialog
            {
                Filter = "JSON パラメータファイル (*.json)|*.json",
                Title = "虹彩パラメータを読み込み"
            };

            if (ofd.ShowDialog() == true)
            {
                try
                {
                    string json = SafeFileReader.ReadAllText(ofd.FileName);
                    var loaded = JsonSerializer.Deserialize<IrisParameters>(json);
                    if (loaded != null)
                    {
                        PushUndo();
                        CurrentIris = loaded;
                        RecordHistory($"JSON読込: {loaded.Name}");
                        StatusMessage = $"パラメータを読み込みました: {Path.GetFileName(ofd.FileName)}";
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"読み込みエラー: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void OnExportPdf()
        {
            if (IsExportingPdf) return;

            // A design larger than the page (e.g. from a hand-edited JSON) or a manual grid that doesn't fit
            // the margins would print off the paper: say so before asking for a file name.
            var checkLayout = new SheetLayout(SheetItems.Count > 0 ? SheetItems : new ObservableCollection<SheetItem> { new(CurrentIris, 1) }, SheetConfig);
            if (!checkLayout.FitsOnPage)
            {
                var answer = MessageBox.Show(
                    $"現在の配置は用紙の印刷範囲に収まりません。はみ出した部分は印刷されません。\n\n" +
                    $"配置の大きさ: {checkLayout.GridWidthMm:F1} × {checkLayout.GridHeightMm:F1} mm\n" +
                    $"印刷範囲 (余白の内側): {checkLayout.UsableWidthMm:F1} × {checkLayout.UsableHeightMm:F1} mm\n\n" +
                    "虹彩サイズ・縦横比、手動の列数・行数、余白・間隔を見直してください。\n\nこのまま PDF を出力しますか？",
                    "用紙からはみ出します", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                if (answer != MessageBoxResult.Yes)
                {
                    StatusMessage = "PDF 出力を中止しました (配置が用紙に収まりません)。";
                    return;
                }
            }

            var sfd = new SaveFileDialog
            {
                Filter = "PDF 印刷用ドキュメント (*.pdf)|*.pdf",
                FileName = $"DollIris_A4Sheet_{CurrentIris.SizeMm:F1}mm.pdf",
                Title = "A4 印刷用 PDF シートを出力"
            };

            if (sfd.ShowDialog() == true)
            {
                // Snapshot everything so edits made while exporting can't change the output mid-way
                var itemsToPrint = SheetItems.Count > 0
                    ? SheetItems.Select(i => new SheetItem(i.Parameters.Clone(), i.PairCount)).ToList()
                    : new List<SheetItem> { new(CurrentIris.Clone(), 1) };
                var configSnapshot = JsonSerializer.Deserialize<PrintSheetConfig>(JsonSerializer.Serialize(SheetConfig))!;
                int pageCount = new SheetLayout(itemsToPrint, configSnapshot).PageCount;

                IsExportingPdf = true;
                StatusMessage = $"PDF を出力中です… ({pageCount} ページ / 1200 DPI 描画のため少し時間がかかります)";
                try
                {
                    // 1200 DPI rendering is slow: run it off the UI thread so the window stays responsive.
                    // WPF rendering needs an STA thread, so use a dedicated one instead of the thread pool.
                    await RunOnStaThreadAsync(() => PdfExporter.ExportA4SheetToPdf(sfd.FileName, itemsToPrint, configSnapshot));
                    StatusMessage = $"高精度 A4 PDF を出力しました (実寸100%印刷推奨): {Path.GetFileName(sfd.FileName)}";
                    MessageBox.Show(
                        $"A4印刷用PDFを正常に出力しました！\n\nファイル: {sfd.FileName}\n\n※印刷時はプリンター設定で「実際のサイズ (100%)」を選択して印刷してください。",
                        "PDF出力完了",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
                catch (Exception ex)
                {
                    StatusMessage = "PDF 出力に失敗しました。";
                    MessageBox.Show($"PDF出力エラー: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    IsExportingPdf = false;
                }
            }
        }

        private static Task RunOnStaThreadAsync(Action work)
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                try
                {
                    work();
                    tcs.SetResult();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            })
            {
                IsBackground = true,
                Name = "PdfExport"
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return tcs.Task;
        }

        private void OnExportSvg()
        {
            var sfd = new SaveFileDialog
            {
                Filter = "SVG ベクター画像 (*.svg)|*.svg",
                FileName = $"{GetSafeFileName(CurrentIris.Name)}_Pair_{CurrentIris.SizeMm:F1}mm.svg",
                Title = "SVG ベクターデータを出力 (Illustrator / Inkscape用)"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    // Save exactly what the SVG editor shows when it was edited by hand
                    bool useEdited = IsSvgManuallyEdited;
                    string svg = useEdited ? SvgSourceText : GenerateSvg();
                    if (useEdited && !IsWellFormedSvg(svg, out string xmlError))
                    {
                        var answer = MessageBox.Show(
                            $"編集した SVG に構文エラーがあります。他のソフトで開けない可能性があります。\n\n{xmlError}\n\nこのまま保存しますか？",
                            "SVG の確認", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        if (answer != MessageBoxResult.Yes) return;
                    }

                    File.WriteAllText(sfd.FileName, svg);
                    StatusMessage = useEdited
                        ? $"手動編集した SVG を保存しました: {Path.GetFileName(sfd.FileName)}"
                        : $"SVG ベクターファイルを出力しました: {Path.GetFileName(sfd.FileName)}";
                    MessageBox.Show(
                        $"SVGベクターファイルを正常に出力しました！{(useEdited ? "（SVG編集タブで手動編集した内容）" : "")}\n\nファイル: {sfd.FileName}\n\nAdobe Illustrator や Inkscape 等で完全にベクター編集・再加工が可能です。",
                        "SVG出力完了",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"SVG出力エラー: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OnExportPng(double dpi)
        {
            var sfd = new SaveFileDialog
            {
                Filter = "PNG 高解像度画像 (*.png)|*.png",
                FileName = $"{GetSafeFileName(CurrentIris.Name)}_Pair_{CurrentIris.SizeMm:F1}mm_{(int)dpi}dpi.png",
                Title = $"高解像度 PNG 画像を出力 ({dpi:F0} DPI)"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    var bmp = RasterExporter.RenderPairToBitmap(
                        CurrentIris,
                        dpi: dpi,
                        pairSpacingMm: SheetConfig.PairSpacingXMm,
                        showCutLines: ShowCutLines,
                        showCenterMarks: ShowCenterMarks
                    );
                    RasterExporter.SaveBitmapToPng(bmp, sfd.FileName);
                    StatusMessage = $"{dpi:F0} DPI 高解像度 PNG を出力しました: {Path.GetFileName(sfd.FileName)}";
                    MessageBox.Show(
                        $"{dpi:F0} DPI の超高解像度PNG画像を出力しました！\n\nファイル: {sfd.FileName}\nサイズ: {bmp.PixelWidth} × {bmp.PixelHeight} ピクセル",
                        "PNG出力完了",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"PNG出力エラー: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
