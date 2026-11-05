using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using DollIrisGenerator.ViewModels;
using Xunit;

namespace DollIrisGenerator.Tests
{
    /// <summary>Answers dialogs from preset values and records what would have been shown.</summary>
    internal sealed class FakeDialogService : IDialogService
    {
        public string? NextSavePath { get; set; }
        public string? NextOpenPath { get; set; }
        public bool ConfirmAnswer { get; set; }
        public int SavePrompts { get; private set; }
        public int Confirmations { get; private set; }
        public List<string> Infos { get; } = new();
        public List<string> Errors { get; } = new();

        public string? PickSaveFile(string filter, string fileName, string title)
        {
            SavePrompts++;
            return NextSavePath;
        }

        public string? PickOpenFile(string filter, string title) => NextOpenPath;
        public void ShowInfo(string message, string title) => Infos.Add(message);
        public void ShowWarning(string message, string title) => Errors.Add(message);
        public void ShowError(string message, string title) => Errors.Add(message);

        public bool ConfirmWarning(string message, string title, bool defaultToNo = false)
        {
            Confirmations++;
            return ConfirmAnswer;
        }
    }

    public sealed class MainViewModelWorkflowTests : IDisposable
    {
        private readonly string _dir = Directory.CreateTempSubdirectory("dolliris_tests_").FullName;
        private readonly FakeDialogService _dialogs = new();
        private readonly MainViewModel _vm;

        public MainViewModelWorkflowTests()
        {
            _vm = new MainViewModel(_dialogs, new FavoritesStore(FavoritesPath));
        }

        private string FavoritesPath => Path.Combine(_dir, "favorites.json");
        private string PathIn(string fileName) => Path.Combine(_dir, fileName);

        public void Dispose() => Directory.Delete(_dir, recursive: true);

        [Fact]
        public void TestJsonSaveThenLoadRestoresDesignAsOneUndoStep()
        {
            _dialogs.NextSavePath = PathIn("design.json");
            _vm.CurrentIris.Name = "保存したデザイン";
            _vm.SaveJsonCommand.Execute(null);
            Assert.Contains("design.json", _vm.StatusMessage);

            _vm.CurrentIris.Name = "別のデザイン";
            _dialogs.NextOpenPath = PathIn("design.json");
            _vm.LoadJsonCommand.Execute(null);

            Assert.Equal("保存したデザイン", _vm.CurrentIris.Name);
            Assert.Equal("保存したデザイン (20.0mm)", _vm.HistoryItems[0].Title);
            Assert.Contains("読み込みました", _vm.StatusMessage);
            Assert.True(_vm.CanUndo);
        }

        [Fact]
        public void TestCancelledFileDialogsDoNothing()
        {
            string before = _vm.StatusMessage;
            var irisBefore = _vm.CurrentIris;

            _vm.SaveJsonCommand.Execute(null);
            _vm.LoadJsonCommand.Execute(null);
            _vm.ExportSvgCommand.Execute(null);

            Assert.Equal(before, _vm.StatusMessage);
            Assert.Same(irisBefore, _vm.CurrentIris);
            Assert.Empty(_dialogs.Errors);
            Assert.Empty(_dialogs.Infos);
        }

        [Fact]
        public void TestLoadingBrokenJsonReportsErrorAndKeepsDesign()
        {
            File.WriteAllText(PathIn("broken.json"), "{ not json");
            var irisBefore = _vm.CurrentIris;

            _dialogs.NextOpenPath = PathIn("broken.json");
            _vm.LoadJsonCommand.Execute(null);

            Assert.Same(irisBefore, _vm.CurrentIris);
            Assert.StartsWith("読み込みエラー:", Assert.Single(_dialogs.Errors));
        }

        [Fact]
        public void TestSvgExportWritesGeneratedThenHandEditedText()
        {
            _dialogs.NextSavePath = PathIn("generated.svg");
            _vm.ExportSvgCommand.Execute(null);
            Assert.Equal(_vm.SvgSourceText, File.ReadAllText(PathIn("generated.svg")));
            Assert.Contains("出力しました", _vm.StatusMessage);

            const string edited = "<svg xmlns=\"http://www.w3.org/2000/svg\"><!-- mine --></svg>";
            _vm.SvgSourceText = edited;
            _dialogs.NextSavePath = PathIn("edited.svg");
            _vm.ExportSvgCommand.Execute(null);

            Assert.Equal(edited, File.ReadAllText(PathIn("edited.svg")));
            Assert.Contains("手動編集した SVG", _vm.StatusMessage);
            Assert.Equal(0, _dialogs.Confirmations);
        }

        [Fact]
        public void TestMalformedHandEditedSvgIsOnlySavedAfterConfirmation()
        {
            _vm.SvgSourceText = "<svg><g></svg>";
            _dialogs.NextSavePath = PathIn("bad.svg");

            _dialogs.ConfirmAnswer = false;
            _vm.ExportSvgCommand.Execute(null);
            Assert.False(File.Exists(PathIn("bad.svg")));

            _dialogs.ConfirmAnswer = true;
            _vm.ExportSvgCommand.Execute(null);
            Assert.True(File.Exists(PathIn("bad.svg")));
            Assert.Equal(2, _dialogs.Confirmations);
        }

        [Fact]
        public void TestPdfExportStopsBeforeFilePromptWhenLayoutOverflowAndDeclined()
        {
            _vm.SheetConfig.AutoCalculateGrid = false;
            _vm.SheetConfig.ManualColumns = 10;
            _vm.SheetConfig.ManualRows = 10;
            _vm.SheetItems.Clear();
            _vm.SheetItems.Add(new SheetItem(_vm.CurrentIris.Clone(), 30));

            _dialogs.ConfirmAnswer = false;
            _vm.ExportPdfCommand.Execute(null);

            Assert.Equal(1, _dialogs.Confirmations);
            Assert.Equal(0, _dialogs.SavePrompts);
            Assert.Contains("中止", _vm.StatusMessage);
            Assert.False(_vm.IsExportingPdf);
        }

        [Fact]
        public void TestFavoritesPersistToInjectedStoreAndReloadIntoNewViewModel()
        {
            _vm.CurrentIris.Name = "残すデザイン";
            _vm.AddFavoriteCommand.Execute(null);
            Assert.True(File.Exists(FavoritesPath));

            var reopened = new MainViewModel(_dialogs, new FavoritesStore(FavoritesPath));
            Assert.Equal("残すデザイン", Assert.Single(reopened.Favorites).Name);

            reopened.RemoveFavoriteCommand.Execute(reopened.Favorites[0]);
            Assert.Empty(new FavoritesStore(FavoritesPath).LoadPersisted());
        }

        [Fact]
        public void TestFavoritesImportSkipsExistingIdsAndExportRoundTrips()
        {
            _vm.AddFavoriteCommand.Execute(null);
            _dialogs.NextSavePath = PathIn("export.irisfav.json");
            _vm.ExportFavoritesCommand.Execute(null);

            _dialogs.NextOpenPath = PathIn("export.irisfav.json");
            _vm.ImportFavoritesCommand.Execute(null);

            Assert.Single(_vm.Favorites);
            Assert.Contains("0 件", _vm.StatusMessage);
        }

        [Fact]
        public void TestFavoritesStoreToleratesMissingAndCorruptFiles()
        {
            Assert.Empty(new FavoritesStore(PathIn("missing.json")).LoadPersisted());

            File.WriteAllText(PathIn("corrupt.json"), "{ not json");
            Assert.Empty(new FavoritesStore(PathIn("corrupt.json")).LoadPersisted());

            File.WriteAllText(PathIn("partial.json"), "[null, {\"Name\":\"no parameters\",\"Parameters\":null}]");
            Assert.Empty(new FavoritesStore(PathIn("partial.json")).LoadPersisted());
        }

        [Theory]
        [InlineData(null, "DollIris")]
        [InlineData("   ", "DollIris")]
        [InlineData("a/b:c", "a_b_c")]
        [InlineData("ふつうの名前", "ふつうの名前")]
        public void TestSafeFileNameReplacesInvalidCharacters(string? name, string expected)
        {
            Assert.Equal(expected, DesignFileWorkflow.GetSafeFileName(name));
        }
    }
}
