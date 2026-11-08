using System;
using System.IO;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using DollIrisGenerator.ViewModels;
using Xunit;

namespace DollIrisGenerator.Tests
{
    /// <summary>The 生成履歴 titles of hand edits, and when closing the app is worth a confirmation.</summary>
    public sealed class HistoryAndExitTests : IDisposable
    {
        private readonly string _dir = Directory.CreateTempSubdirectory("dolliris_tests_").FullName;
        private readonly MainViewModel _vm;

        public HistoryAndExitTests()
        {
            _vm = new MainViewModel(new FakeDialogService(), new FavoritesStore(Path.Combine(_dir, "favorites.json")));
        }

        public void Dispose() => Directory.Delete(_dir, recursive: true);

        [Fact]
        public void TestDescribeNamesTheEditedSettings()
        {
            Assert.Equal("パラメータ調整: 瞳孔サイズ", ManualEditDescriber.Describe(new[] { nameof(IrisParameters.PupilSizeRatio) }));
            Assert.Equal("パラメータ調整: 瞳孔サイズ ほか 1 項目",
                ManualEditDescriber.Describe(new[] { nameof(IrisParameters.PupilSizeRatio), nameof(IrisParameters.RayCount), nameof(IrisParameters.PupilSizeRatio) }));
            Assert.Equal("パラメータ調整: ハイライト・装飾記号", ManualEditDescriber.Describe(new[] { IrisParameters.NestedItemsChangedName }));
            Assert.Equal("パラメータ調整", ManualEditDescriber.Describe(new string?[] { "Unknown", null }));
        }

        [Fact]
        public void TestRepeatedEditsOfOneSettingShareOneHistoryEntry()
        {
            int start = _vm.HistoryItems.Count;

            // Undo closes the pending hand edit (as the pause timer would) before stepping back
            _vm.CurrentIris.PupilSizeRatio = 0.30;
            _vm.UndoCommand.Execute(null);
            _vm.CurrentIris.PupilSizeRatio = 0.35;
            _vm.UndoCommand.Execute(null);

            Assert.Equal(start + 1, _vm.HistoryItems.Count);
            Assert.StartsWith("パラメータ調整: 瞳孔サイズ", _vm.HistoryItems[0].Details);
            Assert.Equal(0.35, _vm.HistoryItems[0].Snapshot.PupilSizeRatio, 6);

            // A different setting gets its own entry
            _vm.CurrentIris.RayCount = 40;
            _vm.UndoCommand.Execute(null);
            Assert.Equal(start + 2, _vm.HistoryItems.Count);
            Assert.StartsWith("パラメータ調整: ライン本数", _vm.HistoryItems[0].Details);
        }

        [Fact]
        public void TestEditsSeparatedByACommandAreNotMerged()
        {
            int start = _vm.HistoryItems.Count;

            _vm.CurrentIris.PupilSizeRatio = 0.30;
            _vm.RandomizeSeedCommand.Execute(null); // closes the edit, then adds its own entry
            _vm.CurrentIris.PupilSizeRatio = 0.35;
            _vm.UndoCommand.Execute(null);

            Assert.Equal(start + 3, _vm.HistoryItems.Count);
        }

        [Fact]
        public void TestNothingToLoseUntilADesignChanges()
        {
            Assert.False(_vm.HasUnsavedDesigns());

            _vm.CurrentIris.BaseColor = "#123456";
            Assert.True(_vm.HasUnsavedDesigns());

            _vm.AddFavoriteCommand.Execute(null);
            Assert.False(_vm.HasUnsavedDesigns());
        }

        [Fact]
        public void TestAChangedOtherCellCountsAsUnsaved()
        {
            _vm.SelectSlot(_vm.VisibleSlots[1]);
            _vm.CurrentIris.RayCount = 40;
            _vm.SelectSlot(_vm.VisibleSlots[0]);

            Assert.True(_vm.HasUnsavedDesigns());
        }
    }
}
