using System;
using System.IO;
using System.Linq;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using DollIrisGenerator.ViewModels;
using Xunit;

namespace DollIrisGenerator.Tests
{
    public sealed class PrintSheetPersistenceTests : IDisposable
    {
        private readonly string _dir = Directory.CreateTempSubdirectory("dolliris_sheet_tests_").FullName;
        private readonly FakeDialogService _dialogs = new();

        private string SheetPath => Path.Combine(_dir, "print-sheet.json");

        public void Dispose() => Directory.Delete(_dir, recursive: true);

        private MainViewModel StartApp() =>
            new(_dialogs, new FavoritesStore(Path.Combine(_dir, "favorites.json")), new PrintSheetStore(SheetPath));

        [Fact]
        public void TestSheetItemsSettingsAndTemplateSurviveARestart()
        {
            var first = StartApp();
            first.SelectedPdfTemplate = first.PdfTemplates.First(t => t.Type == PdfTemplateType.Scale1_4_Doll);
            first.SheetConfig.PageMarginTopMm = 22.5;
            first.SheetConfig.RowSpacingMm = 12.0;
            first.SheetConfig.ShowLabels = false;
            first.SheetItems[0].PairCount = 7;
            first.CurrentIris.Name = "二つ目";
            first.CurrentIris.SizeMm = 8.0;
            first.AddCurrentToSheetCommand.Execute(null);
            Assert.True(first.SavePrintSheetBeforeExit());

            var second = StartApp();

            Assert.Equal(PdfTemplateType.Scale1_4_Doll, second.SelectedPdfTemplate!.Type);
            Assert.Equal(22.5, second.SheetConfig.PageMarginTopMm);
            Assert.Equal(12.0, second.SheetConfig.RowSpacingMm);
            Assert.False(second.SheetConfig.ShowLabels);
            Assert.False(second.SheetConfig.AutoCalculateGrid); // from the 1/4 template
            Assert.Equal(2, second.SheetItems.Count);
            Assert.Equal(7, second.SheetItems[0].PairCount);
            Assert.Equal("二つ目", second.SheetItems[1].Parameters.Name);
            Assert.Equal(8.0, second.SheetItems[1].Parameters.SizeMm);
            Assert.Empty(_dialogs.Errors);
        }

        [Fact]
        public void TestRestoringDoesNotReapplyTheTemplateOverSavedSettings()
        {
            var first = StartApp();
            first.SelectedPdfTemplate = first.PdfTemplates.First(t => t.Type == PdfTemplateType.Scale1_3_Doll);
            first.SheetConfig.ManualColumns = 3; // changed after choosing the template
            first.SavePrintSheetBeforeExit();

            var second = StartApp();

            Assert.Equal(PdfTemplateType.Scale1_3_Doll, second.SelectedPdfTemplate!.Type);
            Assert.Equal(3, second.SheetConfig.ManualColumns);
        }

        [Fact]
        public void TestAnEmptiedSheetStaysEmpty()
        {
            var first = StartApp();
            first.ClearSheetCommand.Execute(null);
            first.SavePrintSheetBeforeExit();

            Assert.Empty(StartApp().SheetItems);
        }

        [Fact]
        public void TestNoSavedFileStartsWithTheDefaultSheet()
        {
            var vm = StartApp();

            Assert.Single(vm.SheetItems);
            Assert.Equal(1, vm.SheetItems[0].PairCount);
            Assert.Same(vm.PdfTemplates[0], vm.SelectedPdfTemplate);
            Assert.Equal(15.0, vm.SheetConfig.PageMarginTopMm);
            vm.ShowPrintSheetLoadProblem();
            Assert.Empty(_dialogs.Errors);
        }

        [Fact]
        public void TestABrokenFileIsSetAsideAndTheAppStartsWithDefaults()
        {
            File.WriteAllText(SheetPath, "{ \"Items\": [ { \"Parameters\": ");

            var vm = StartApp();

            Assert.Single(vm.SheetItems);
            Assert.Equal(15.0, vm.SheetConfig.PageMarginTopMm);
            Assert.False(File.Exists(SheetPath));
            var backup = Assert.Single(Directory.GetFiles(_dir, "print-sheet.broken-*.json"));
            Assert.StartsWith("{ \"Items\"", File.ReadAllText(backup));

            vm.ShowPrintSheetLoadProblem();
            Assert.Contains(backup, Assert.Single(_dialogs.Errors));

            // The next save writes a fresh file and leaves the backup alone
            Assert.True(vm.SavePrintSheetBeforeExit());
            Assert.True(File.Exists(SheetPath));
            Assert.True(File.Exists(backup));
        }

        [Fact]
        public void TestAFileOfTheWrongShapeIsTreatedAsBroken()
        {
            File.WriteAllText(SheetPath, "[1, 2, 3]");

            var vm = StartApp();

            Assert.Single(vm.SheetItems);
            Assert.Single(Directory.GetFiles(_dir, "print-sheet.broken-*.json"));
        }

        [Fact]
        public void TestOutOfRangeAndMissingValuesAreCorrected()
        {
            File.WriteAllText(SheetPath, """
                {
                  "SelectedTemplate": "NoSuchTemplate",
                  "Config": {
                    "PageMarginTopMm": 500, "PageMarginLeftMm": -3,
                    "PairSpacingXMm": 99, "ColumnSpacingMm": 0, "RowSpacingMm": 1000,
                    "CutLineThicknessMm": 50, "CenterMarkLengthMm": 0,
                    "CutLineColor": "not a color", "CenterMarkColor": "#123456",
                    "ManualColumns": 99, "ManualRows": -5
                  },
                  "Items": [
                    { "Parameters": { "Name": "大きすぎ", "SizeMm": 24.0 }, "PairCount": 5000 },
                    { "Parameters": null, "PairCount": 3 },
                    null,
                    { "Parameters": { "Name": "ゼロ個" }, "PairCount": 0 }
                  ]
                }
                """);

            var vm = StartApp();

            Assert.Same(vm.PdfTemplates[0], vm.SelectedPdfTemplate);
            var c = vm.SheetConfig;
            Assert.Equal(PrintSheetStore.MaxMarginMm, c.PageMarginTopMm);
            Assert.Equal(5.0, c.PageMarginLeftMm);
            Assert.Equal(PrintSheetStore.MaxPairSpacingMm, c.PairSpacingXMm);
            Assert.Equal(2.0, c.ColumnSpacingMm);
            Assert.Equal(PrintSheetStore.MaxRowSpacingMm, c.RowSpacingMm);
            Assert.Equal(PrintSheetStore.MaxCutLineThicknessMm, c.CutLineThicknessMm);
            Assert.Equal(1.0, c.CenterMarkLengthMm);
            Assert.Equal("#A0A0A0", c.CutLineColor);
            Assert.Equal("#123456", c.CenterMarkColor);
            Assert.Equal(12, c.ManualColumns);
            Assert.Equal(1, c.ManualRows);

            Assert.Equal(2, vm.SheetItems.Count);
            Assert.Equal(SheetItem.MaxPairCount, vm.SheetItems[0].PairCount);
            Assert.Equal(IrisParameters.MaxSizeMm, vm.SheetItems[0].Parameters.SizeMm);
            Assert.Equal(1, vm.SheetItems[1].PairCount);
            vm.ShowPrintSheetLoadProblem();
            Assert.Empty(_dialogs.Errors);
        }

        [Fact]
        public void TestAMissingConfigUsesDefaults()
        {
            File.WriteAllText(SheetPath, "{ \"Items\": [] }");

            var vm = StartApp();

            Assert.Empty(vm.SheetItems);
            Assert.Equal(15.0, vm.SheetConfig.PageMarginTopMm);
            Assert.True(vm.SheetConfig.AutoCalculateGrid);
        }

        [Fact]
        public void TestASaveFailureAsksBeforeExitingAndRespectsTheAnswer()
        {
            // A directory where the file should be makes the write fail
            Directory.CreateDirectory(SheetPath);
            var vm = StartApp();
            Assert.Empty(_dialogs.Errors); // a directory is not an existing file, so the start is clean

            _dialogs.ConfirmAnswer = false;
            Assert.False(vm.SavePrintSheetBeforeExit());
            _dialogs.ConfirmAnswer = true;
            Assert.True(vm.SavePrintSheetBeforeExit());
            Assert.Equal(2, _dialogs.Confirmations);
        }

        [Fact]
        public void TestSavingLeavesNoTemporaryFiles()
        {
            var vm = StartApp();
            vm.SavePrintSheetBeforeExit();
            vm.SavePrintSheetBeforeExit();

            Assert.Equal(new[] { "print-sheet.json" }, Directory.GetFiles(_dir).Select(Path.GetFileName));
        }

        [Fact]
        public void TestWithoutAStoreTheSheetIsNotPersisted()
        {
            var vm = new MainViewModel(_dialogs, new FavoritesStore(Path.Combine(_dir, "favorites.json")));

            Assert.True(vm.SavePrintSheetBeforeExit());
            Assert.False(File.Exists(SheetPath));
        }
    }
}
