using System.IO;
using System.Text.Json;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;

namespace DollIrisGenerator.ViewModels
{
    /// <summary>
    /// The user-facing file flows for a design: pick a file, do the work, report the result.
    /// Covers JSON save / load and PDF / SVG / PNG export; the rendering itself lives in the exporters.
    /// </summary>
    public sealed class DesignFileWorkflow
    {
        private readonly IDialogService _dialogs;
        private readonly Action<string> _reportStatus;

        public DesignFileWorkflow(IDialogService dialogs, Action<string> reportStatus)
        {
            _dialogs = dialogs;
            _reportStatus = reportStatus;
        }

        public static string GetSafeFileName(string? name, string fallback = "DollIris")
        {
            if (string.IsNullOrWhiteSpace(name)) return fallback;
            var invalidChars = Path.GetInvalidFileNameChars();
            var cleaned = string.Join("_", name.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).Trim();
            return string.IsNullOrWhiteSpace(cleaned) ? fallback : cleaned;
        }

        public void SaveJson(IrisParameters iris)
        {
            string? path = _dialogs.PickSaveFile(
                "JSON パラメータファイル (*.json)|*.json",
                $"{GetSafeFileName(iris.Name)}_{iris.SizeMm:F1}mm.json",
                "虹彩パラメータを保存");
            if (path == null) return;

            try
            {
                var opt = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(iris, opt);
                File.WriteAllText(path, json);
                _reportStatus($"パラメータをJSONファイルに保存しました: {Path.GetFileName(path)}");
            }
            catch (Exception ex)
            {
                _dialogs.ShowError($"保存エラー: {ex.Message}", "エラー");
            }
        }

        /// <param name="applyLoaded">Makes the loaded design current. Called only when a design was read.</param>
        public void LoadJson(Action<IrisParameters> applyLoaded)
        {
            string? path = _dialogs.PickOpenFile("JSON パラメータファイル (*.json)|*.json", "虹彩パラメータを読み込み");
            if (path == null) return;

            try
            {
                string json = SafeFileReader.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<IrisParameters>(json);
                if (loaded != null)
                {
                    applyLoaded(loaded);
                    _reportStatus($"パラメータを読み込みました: {Path.GetFileName(path)}");
                }
            }
            catch (Exception ex)
            {
                _dialogs.ShowError($"読み込みエラー: {ex.Message}", "エラー");
            }
        }

        /// <param name="sheetItems">The sheet contents. When empty, one pair of <paramref name="currentIris"/> is printed.</param>
        /// <param name="setBusy">Called with true while the export runs in the background, then false.</param>
        public async Task ExportPdfAsync(
            IReadOnlyCollection<SheetItem> sheetItems,
            IrisParameters currentIris,
            PrintSheetConfig sheetConfig,
            Action<bool> setBusy)
        {
            // A design larger than the page (e.g. from a hand-edited JSON) or a manual grid that doesn't fit
            // the margins would print off the paper: say so before asking for a file name.
            IEnumerable<SheetItem> itemsToCheck = sheetItems.Count > 0 ? sheetItems : new List<SheetItem> { new(currentIris, 1) };
            var checkLayout = new SheetLayout(itemsToCheck, sheetConfig);
            if (!checkLayout.FitsOnPage)
            {
                bool proceed = _dialogs.ConfirmWarning(
                    $"現在の配置は用紙の印刷範囲に収まりません。はみ出した部分は印刷されません。\n\n" +
                    $"配置の大きさ: {checkLayout.GridWidthMm:F1} × {checkLayout.GridHeightMm:F1} mm\n" +
                    $"印刷範囲 (余白の内側): {checkLayout.UsableWidthMm:F1} × {checkLayout.UsableHeightMm:F1} mm\n\n" +
                    "虹彩サイズ・縦横比、手動の列数・行数、余白・間隔を見直してください。\n\nこのまま PDF を出力しますか？",
                    "用紙からはみ出します",
                    defaultToNo: true);
                if (!proceed)
                {
                    _reportStatus("PDF 出力を中止しました (配置が用紙に収まりません)。");
                    return;
                }
            }

            string? path = _dialogs.PickSaveFile(
                "PDF 印刷用ドキュメント (*.pdf)|*.pdf",
                $"DollIris_A4Sheet_{currentIris.SizeMm:F1}mm.pdf",
                "A4 印刷用 PDF シートを出力");
            if (path == null) return;

            // Snapshot everything so edits made while exporting can't change the output mid-way
            var itemsToPrint = sheetItems.Count > 0
                ? sheetItems.Select(i => new SheetItem(i.Parameters.Clone(), i.PairCount)).ToList()
                : new List<SheetItem> { new(currentIris.Clone(), 1) };
            var configSnapshot = JsonSerializer.Deserialize<PrintSheetConfig>(JsonSerializer.Serialize(sheetConfig))!;
            int pageCount = new SheetLayout(itemsToPrint, configSnapshot).PageCount;

            setBusy(true);
            _reportStatus($"PDF を出力中です… ({pageCount} ページ / 1200 DPI 描画のため少し時間がかかります)");
            try
            {
                // 1200 DPI rendering is slow: run it off the UI thread so the window stays responsive.
                // WPF rendering needs an STA thread, so use a dedicated one instead of the thread pool.
                await RunOnStaThreadAsync(() => PdfExporter.ExportA4SheetToPdf(path, itemsToPrint, configSnapshot));
                _reportStatus($"高精度 A4 PDF を出力しました (実寸100%印刷推奨): {Path.GetFileName(path)}");
                _dialogs.ShowInfo(
                    $"A4印刷用PDFを正常に出力しました！\n\nファイル: {path}\n\n※印刷時はプリンター設定で「実際のサイズ (100%)」を選択して印刷してください。",
                    "PDF出力完了");
            }
            catch (Exception ex)
            {
                _reportStatus("PDF 出力に失敗しました。");
                _dialogs.ShowError($"PDF出力エラー: {ex.Message}", "エラー");
            }
            finally
            {
                setBusy(false);
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

        /// <param name="handEditedSvg">The SVG editor's text when it was edited by hand; otherwise null.</param>
        /// <param name="generateSvg">Produces the SVG from the current parameters when there are no hand edits.</param>
        public void ExportSvg(IrisParameters iris, string? handEditedSvg, Func<string> generateSvg)
        {
            string? path = _dialogs.PickSaveFile(
                "SVG ベクター画像 (*.svg)|*.svg",
                $"{GetSafeFileName(iris.Name)}_Pair_{iris.SizeMm:F1}mm.svg",
                "SVG ベクターデータを出力 (Illustrator / Inkscape用)");
            if (path == null) return;

            try
            {
                // Save exactly what the SVG editor shows when it was edited by hand
                bool useEdited = handEditedSvg != null;
                string svg = handEditedSvg ?? generateSvg();
                if (useEdited && !SvgValidator.IsWellFormed(svg, out string xmlError))
                {
                    bool proceed = _dialogs.ConfirmWarning(
                        $"編集した SVG に構文エラーがあります。他のソフトで開けない可能性があります。\n\n{xmlError}\n\nこのまま保存しますか？",
                        "SVG の確認");
                    if (!proceed) return;
                }

                File.WriteAllText(path, svg);
                _reportStatus(useEdited
                    ? $"手動編集した SVG を保存しました: {Path.GetFileName(path)}"
                    : $"SVG ベクターファイルを出力しました: {Path.GetFileName(path)}");
                _dialogs.ShowInfo(
                    $"SVGベクターファイルを正常に出力しました！{(useEdited ? "（SVG編集タブで手動編集した内容）" : "")}\n\nファイル: {path}\n\nAdobe Illustrator や Inkscape 等で完全にベクター編集・再加工が可能です。",
                    "SVG出力完了");
            }
            catch (Exception ex)
            {
                _dialogs.ShowError($"SVG出力エラー: {ex.Message}", "エラー");
            }
        }

        public void ExportPng(IrisParameters iris, double dpi, double pairSpacingMm, bool showCutLines, bool showCenterMarks)
        {
            string? path = _dialogs.PickSaveFile(
                "PNG 高解像度画像 (*.png)|*.png",
                $"{GetSafeFileName(iris.Name)}_Pair_{iris.SizeMm:F1}mm_{(int)dpi}dpi.png",
                $"高解像度 PNG 画像を出力 ({dpi:F0} DPI)");
            if (path == null) return;

            try
            {
                var bmp = RasterExporter.RenderPairToBitmap(
                    iris,
                    dpi: dpi,
                    pairSpacingMm: pairSpacingMm,
                    showCutLines: showCutLines,
                    showCenterMarks: showCenterMarks
                );
                RasterExporter.SaveBitmapToPng(bmp, path);
                _reportStatus($"{dpi:F0} DPI 高解像度 PNG を出力しました: {Path.GetFileName(path)}");
                _dialogs.ShowInfo(
                    $"{dpi:F0} DPI の超高解像度PNG画像を出力しました！\n\nファイル: {path}\nサイズ: {bmp.PixelWidth} × {bmp.PixelHeight} ピクセル",
                    "PNG出力完了");
            }
            catch (Exception ex)
            {
                _dialogs.ShowError($"PNG出力エラー: {ex.Message}", "エラー");
            }
        }
    }
}
