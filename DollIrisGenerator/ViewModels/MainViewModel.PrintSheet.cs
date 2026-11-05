using DollIrisGenerator.Models;
using DollIrisGenerator.Services;

namespace DollIrisGenerator.ViewModels
{
    // A4 print sheet persistence: the 「A4印刷・テンプレート」 tab is saved on exit and restored on the next start
    public partial class MainViewModel
    {
        /// <summary>The current sheet: designs and quantities, sheet settings, and the selected template.</summary>
        public PrintSheetState CapturePrintSheetState() => new()
        {
            SelectedTemplate = SelectedPdfTemplate?.Type.ToString(),
            Config = SheetConfig,
            Items = SheetItems
                .Select(i => new PrintSheetItemState { Parameters = i.Parameters, PairCount = i.PairCount })
                .ToList()
        };

        /// <summary>Restores a saved sheet. The template is only re-selected, not re-applied, so the saved settings stay as they were.</summary>
        private void ApplyPrintSheetState(PrintSheetState state)
        {
            _selectedPdfTemplate = PdfTemplates.FirstOrDefault(t => t.Type.ToString() == state.SelectedTemplate) ?? PdfTemplates[0];
            _sheetConfig = state.Config ?? new PrintSheetConfig();
            _sheetItems.Clear();
            foreach (var item in state.Items ?? new List<PrintSheetItemState>())
            {
                if (item.Parameters != null) _sheetItems.Add(new SheetItem(item.Parameters, item.PairCount));
            }
        }

        /// <summary>Tells the user if the saved print sheet could not be read. Call once the main window is showing.</summary>
        public void ShowPrintSheetLoadProblem()
        {
            if (_printSheetLoadProblem == null) return;
            string problem = _printSheetLoadProblem;
            _printSheetLoadProblem = null;
            _dialogs.ShowWarning(problem, "A4印刷シートの読み込み");
        }

        /// <summary>Saves the print sheet as the app closes.</summary>
        /// <returns>False when saving failed and the user chose not to exit, so the sheet is not lost.</returns>
        public bool SavePrintSheetBeforeExit()
        {
            if (_printSheetStore == null) return true;
            if (_printSheetStore.TrySave(CapturePrintSheetState(), out string error)) return true;

            return _dialogs.ConfirmWarning(
                $"A4印刷シートを保存できませんでした。このまま終了すると、シートの内容と設定は次回に復元されません。\n\n" +
                $"理由: {error}\n\nそれでも終了しますか？",
                "A4印刷シートの保存",
                defaultToNo: true);
        }
    }
}
