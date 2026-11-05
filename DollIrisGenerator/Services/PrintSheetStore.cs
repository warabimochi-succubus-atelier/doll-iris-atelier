using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using DollIrisGenerator.Models;

namespace DollIrisGenerator.Services
{
    /// <summary>What the 「A4印刷・テンプレート」 tab holds, as written to print-sheet.json.</summary>
    public sealed class PrintSheetState
    {
        public int Version { get; set; } = 1;

        /// <summary>The selected template, by <see cref="PdfTemplateType"/> name. Unknown names fall back to the first template.</summary>
        public string? SelectedTemplate { get; set; }

        public PrintSheetConfig? Config { get; set; }

        public List<PrintSheetItemState>? Items { get; set; }
    }

    public sealed class PrintSheetItemState
    {
        public IrisParameters? Parameters { get; set; }
        public int PairCount { get; set; } = 1;
    }

    /// <param name="State">The saved state, already corrected to valid ranges. Null when there was no file or it could not be read.</param>
    /// <param name="Problem">A message for the user when the saved file could not be read; otherwise null.</param>
    public sealed record PrintSheetLoadResult(PrintSheetState? State, string? Problem);

    /// <summary>
    /// Saves the A4 print sheet (designs, quantities, sheet settings, selected template) on exit and
    /// restores it on the next start. Same protection as <see cref="FavoritesStore"/>: writes go through a
    /// temporary file, and an unreadable file is set aside instead of being overwritten.
    /// </summary>
    public sealed class PrintSheetStore
    {
        // Upper limits match the sliders on the tab; the lower limits are the ones PrintSheetConfig enforces
        public const double MaxMarginMm = 35.0;
        public const double MaxPairSpacingMm = 10.0;
        public const double MaxColumnSpacingMm = 20.0;
        public const double MaxRowSpacingMm = 20.0;
        public const double MaxCutLineThicknessMm = 1.0;
        public const double MaxCenterMarkLengthMm = 10.0;

        private static readonly Regex HexColor = new("^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$");

        private readonly string _filePath;

        // Set when an unreadable file could not be set aside: saving would destroy the only copy of the user's data
        private string? _saveBlockedReason;

        /// <param name="filePath">Where to save. Defaults to the user's AppData folder, next to favorites.json.</param>
        public PrintSheetStore(string? filePath = null)
        {
            _filePath = filePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DollIrisAtelier",
                "print-sheet.json");
        }

        /// <returns>False when the sheet could not be saved; <paramref name="error"/> then says why.</returns>
        public bool TrySave(PrintSheetState state, out string error)
        {
            if (_saveBlockedReason != null)
            {
                error = _saveBlockedReason;
                return false;
            }

            try
            {
                var dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var opt = new JsonSerializerOptions { WriteIndented = true };
                AtomicFileWriter.WriteAllText(_filePath, JsonSerializer.Serialize(state, opt));
                error = "";
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"A4印刷シートの保存に失敗: {ex.Message}");
                error = ex.Message;
                return false;
            }
        }

        public PrintSheetLoadResult Load()
        {
            if (!File.Exists(_filePath)) return new PrintSheetLoadResult(null, null);

            try
            {
                string json = SafeFileReader.ReadAllText(_filePath);
                var state = JsonSerializer.Deserialize<PrintSheetState>(json)
                            ?? throw new InvalidDataException("ファイルの中身が空です。");
                return new PrintSheetLoadResult(Normalize(state), null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"A4印刷シートの読み込みに失敗: {ex.Message}");
                return new PrintSheetLoadResult(null, SetAsideUnreadableFile(ex.Message));
            }
        }

        /// <summary>Corrects out-of-range or missing values so a hand-edited or old file can't break the sheet.</summary>
        public static PrintSheetState Normalize(PrintSheetState state)
        {
            var items = new List<PrintSheetItemState>();
            foreach (var item in state.Items ?? new List<PrintSheetItemState>())
            {
                if (item?.Parameters == null) continue;
                items.Add(new PrintSheetItemState
                {
                    Parameters = item.Parameters,
                    PairCount = Math.Clamp(item.PairCount, 1, SheetItem.MaxPairCount)
                });
            }

            return new PrintSheetState
            {
                Version = state.Version,
                SelectedTemplate = state.SelectedTemplate,
                Config = NormalizeConfig(state.Config),
                Items = items
            };
        }

        private static PrintSheetConfig NormalizeConfig(PrintSheetConfig? source)
        {
            var result = new PrintSheetConfig();
            if (source == null) return result;

            result.PageMarginLeftMm = Clamp(source.PageMarginLeftMm, 5.0, MaxMarginMm, result.PageMarginLeftMm);
            result.PageMarginTopMm = Clamp(source.PageMarginTopMm, 5.0, MaxMarginMm, result.PageMarginTopMm);
            result.PageMarginRightMm = Clamp(source.PageMarginRightMm, 5.0, MaxMarginMm, result.PageMarginRightMm);
            result.PageMarginBottomMm = Clamp(source.PageMarginBottomMm, 5.0, MaxMarginMm, result.PageMarginBottomMm);
            result.PairSpacingXMm = Clamp(source.PairSpacingXMm, 1.0, MaxPairSpacingMm, result.PairSpacingXMm);
            result.ColumnSpacingMm = Clamp(source.ColumnSpacingMm, 2.0, MaxColumnSpacingMm, result.ColumnSpacingMm);
            result.RowSpacingMm = Clamp(source.RowSpacingMm, 4.0, MaxRowSpacingMm, result.RowSpacingMm);
            result.CutLineThicknessMm = Clamp(source.CutLineThicknessMm, 0.05, MaxCutLineThicknessMm, result.CutLineThicknessMm);
            result.CenterMarkLengthMm = Clamp(source.CenterMarkLengthMm, 1.0, MaxCenterMarkLengthMm, result.CenterMarkLengthMm);
            result.CutLineColor = ValidColor(source.CutLineColor, result.CutLineColor);
            result.CenterMarkColor = ValidColor(source.CenterMarkColor, result.CenterMarkColor);

            result.ShowCutLines = source.ShowCutLines;
            result.ShowCenterMarks = source.ShowCenterMarks;
            result.ShowTrimGuides = source.ShowTrimGuides;
            result.ShowScaleMark = source.ShowScaleMark;
            result.ShowLabels = source.ShowLabels;
            result.AutoCalculateGrid = source.AutoCalculateGrid;
            result.ManualColumns = source.ManualColumns; // the setters clamp to the slider ranges
            result.ManualRows = source.ManualRows;
            return result;
        }

        private static double Clamp(double value, double min, double max, double fallback) =>
            double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;

        private static string ValidColor(string? value, string fallback) =>
            value != null && HexColor.IsMatch(value) ? value : fallback;

        /// <summary>Keeps an unreadable file out of the way of the next save, so its contents can still be recovered.</summary>
        /// <returns>The message for the user.</returns>
        private string SetAsideUnreadableFile(string reason)
        {
            try
            {
                string dir = Path.GetDirectoryName(_filePath) ?? "";
                string backupPath = Path.Combine(dir, $"print-sheet.broken-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                File.Move(_filePath, backupPath);
                return "A4印刷シートの保存ファイルを読み込めませんでした。シートは初期状態で起動します。\n\n" +
                       $"元のファイルは次の場所に残しています:\n{backupPath}\n\n理由: {reason}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"A4印刷シートのファイルの退避に失敗: {ex.Message}");
                _saveBlockedReason = $"読み込めなかったファイルを上書きしないよう、保存を停止しています: {_filePath}";
                return "A4印刷シートの保存ファイルを読み込めませんでした。シートは初期状態で起動します。\n\n" +
                       $"元のファイルを退避できなかったため、上書きしないよう、今回はシートを保存しません:\n{_filePath}\n\n理由: {reason}";
            }
        }
    }
}
