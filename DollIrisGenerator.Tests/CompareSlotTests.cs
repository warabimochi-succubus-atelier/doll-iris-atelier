using System;
using System.IO;
using System.Linq;
using DollIrisGenerator.Controls;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using DollIrisGenerator.ViewModels;
using Xunit;

namespace DollIrisGenerator.Tests
{
    /// <summary>「分割比較」: the pair preview split into cells, each with its own design and undo history.</summary>
    public sealed class CompareSlotTests : IDisposable
    {
        private readonly string _dir = Directory.CreateTempSubdirectory("dolliris_compare_").FullName;
        private readonly FakeDialogService _dialogs = new();
        private readonly MainViewModel _vm;

        public CompareSlotTests()
        {
            _vm = new MainViewModel(_dialogs, new FavoritesStore(Path.Combine(_dir, "favorites.json")));
        }

        public void Dispose() => Directory.Delete(_dir, recursive: true);

        [Fact]
        public void TestStartsWithFourCellsAndTheFirstSelected()
        {
            Assert.Equal(4, _vm.CompareCount);
            Assert.Equal(4, _vm.VisibleSlots.Count);
            Assert.Same(_vm.VisibleSlots[0], _vm.SelectedSlot);
            Assert.True(_vm.SelectedSlot.IsSelected);
            Assert.Equal(1, _vm.VisibleSlots.Count(s => s.IsSelected));
            Assert.Same(_vm.CurrentIris, _vm.SelectedSlot.Iris);
            // Each cell is its own design, not a shared instance
            Assert.Equal(4, _vm.VisibleSlots.Select(s => s.Iris).Distinct().Count());
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 1)]
        [InlineData(3, 4)]
        [InlineData(5, 6)]
        [InlineData(7, 9)]
        [InlineData(9, 9)]
        [InlineData(12, 9)]
        public void TestSplitCountSnapsToAnOfferedCount(int requested, int expected)
        {
            _vm.CompareCount = requested;
            Assert.Equal(expected, _vm.CompareCount);
            Assert.Equal(expected, _vm.VisibleSlots.Count);
        }

        [Fact]
        public void TestEveryOfferedCountFillsItsGrid()
        {
            Assert.Equal(new[] { 1, 2, 4, 6, 9 }, MainViewModel.CompareCountOptions);
            foreach (int count in MainViewModel.CompareCountOptions)
            {
                _vm.CompareCount = count;
                Assert.Equal(count, _vm.CompareColumns * _vm.CompareRows); // no empty cell
            }
        }

        [Theory]
        [InlineData(1, 1, 1)]
        [InlineData(2, 1, 2)]
        [InlineData(4, 2, 2)]
        [InlineData(6, 2, 3)]
        [InlineData(9, 3, 3)]
        public void TestGridShape(int count, int columns, int rows)
        {
            _vm.CompareCount = count;
            Assert.Equal(columns, _vm.CompareColumns);
            Assert.Equal(rows, _vm.CompareRows);
        }

        [Fact]
        public void TestFitZoomMakesTheWidestPairFillTheCell()
        {
            var small = new IrisParameters { SizeMm = 10.0, Shape = IrisShape.Circle, TiltAngle = 0 };
            var large = new IrisParameters { SizeMm = 14.0, Shape = IrisShape.Circle, TiltAngle = 0 };
            const double spacingMm = 4.0;
            double cellW = 400, cellH = 300;

            double zoom = IrisPreviewControl.FitPairZoom(new[] { small, large }, spacingMm, cellW, cellH);

            // The larger design decides; it fits on both axes and touches at least one of them
            var (wMm, hMm) = SheetLayout.GetEyeFootprintMm(large);
            double pairW = (2 * wMm + spacingMm) * IrisPreviewControl.ScreenDipsPerMm * zoom;
            double pairH = hMm * IrisPreviewControl.ScreenDipsPerMm * zoom;
            double usableW = cellW - 2 * IrisPreviewControl.FitMarginX;
            double usableH = cellH - 2 * IrisPreviewControl.FitMarginY;
            Assert.True(pairW <= usableW + 1e-6 && pairH <= usableH + 1e-6);
            Assert.True(Math.Abs(pairW - usableW) < 1e-6 || Math.Abs(pairH - usableH) < 1e-6);
            Assert.True(zoom < IrisPreviewControl.FitPairZoom(new[] { small }, spacingMm, cellW, cellH));
        }

        [Fact]
        public void TestFitZoomStaysInThePreviewZoomRange()
        {
            var design = new IrisParameters();
            Assert.Equal(WpfIrisRenderer.MinPreviewZoom, IrisPreviewControl.FitPairZoom(new[] { design }, 4.0, 10, 10));
            Assert.Equal(WpfIrisRenderer.MaxPreviewZoom, IrisPreviewControl.FitPairZoom(new[] { design }, 4.0, 100000, 100000));
        }

        [Fact]
        public void TestEditsGoToTheSelectedCellOnly()
        {
            var first = _vm.VisibleSlots[0];
            var second = _vm.VisibleSlots[1];

            Assert.True(_vm.SelectSlot(second));
            Assert.Same(second.Iris, _vm.CurrentIris);
            Assert.False(first.IsSelected);
            Assert.True(second.IsSelected);

            _vm.CurrentIris.Name = "枠2のデザイン";
            _vm.CurrentIris.SizeMm = 14.0;

            Assert.Equal("枠2のデザイン", second.Iris.Name);
            Assert.NotEqual("枠2のデザイン", first.Iris.Name);
            Assert.NotEqual(14.0, first.Iris.SizeMm);
        }

        [Fact]
        public void TestEachCellHasItsOwnUndoHistory()
        {
            var first = _vm.VisibleSlots[0];
            var second = _vm.VisibleSlots[1];
            string firstSeedName = first.Iris.Name;

            _vm.CurrentIris.Name = "枠1を編集";
            _vm.SelectSlot(second); // commits the pending edit as an undo step of cell 1
            Assert.False(_vm.CanUndo);

            _vm.CurrentIris.Name = "枠2を編集";
            _vm.SelectSlot(first);
            Assert.True(_vm.CanUndo);

            _vm.UndoCommand.Execute(null);
            Assert.Equal(firstSeedName, first.Iris.Name);
            Assert.Same(first.Iris, _vm.CurrentIris);
            Assert.Equal("枠2を編集", second.Iris.Name);

            _vm.SelectSlot(second);
            _vm.UndoCommand.Execute(null);
            Assert.Same(second.Iris, _vm.CurrentIris);
            Assert.NotEqual("枠2を編集", second.Iris.Name);
        }

        [Fact]
        public void TestCommandsThatReplaceTheDesignStayInTheSelectedCell()
        {
            var third = _vm.VisibleSlots[2];
            _vm.SelectSlot(third);

            _vm.NewDesignCommand.Execute(null);

            Assert.Same(_vm.CurrentIris, third.Iris);
            Assert.NotSame(_vm.CurrentIris, _vm.VisibleSlots[0].Iris);
        }

        [Fact]
        public void TestShrinkingTheSplitMovesSelectionAndKeepsHiddenDesigns()
        {
            var fourth = _vm.VisibleSlots[3];
            _vm.SelectSlot(fourth);
            _vm.CurrentIris.Name = "隠れる枠";

            _vm.CompareCount = 2;

            Assert.Same(_vm.VisibleSlots[1], _vm.SelectedSlot);
            Assert.Same(_vm.SelectedSlot.Iris, _vm.CurrentIris);
            Assert.False(fourth.IsSelected);

            _vm.CompareCount = 4;
            Assert.Same(fourth, _vm.VisibleSlots[3]);
            Assert.Equal("隠れる枠", fourth.Iris.Name);
        }

        [Fact]
        public void TestNewCellsStartAsCopiesOfTheSelectedDesign()
        {
            _vm.CurrentIris.Name = "元のデザイン";
            _vm.CompareCount = 6;

            Assert.Equal("元のデザイン", _vm.VisibleSlots[5].Iris.Name);
            Assert.NotSame(_vm.CurrentIris, _vm.VisibleSlots[5].Iris);
        }

        [Fact]
        public void TestSwitchingAwayFromHandEditedSvgAsksFirst()
        {
            var first = _vm.VisibleSlots[0];
            var second = _vm.VisibleSlots[1];
            _vm.SvgSourceText = "<svg xmlns=\"http://www.w3.org/2000/svg\"/>";

            _dialogs.ConfirmAnswer = false;
            Assert.False(_vm.SelectSlot(second));
            Assert.Same(first, _vm.SelectedSlot);
            Assert.True(_vm.IsSvgManuallyEdited);

            _dialogs.ConfirmAnswer = true;
            Assert.True(_vm.SelectSlot(second));
            Assert.Same(second, _vm.SelectedSlot);
            Assert.False(_vm.IsSvgManuallyEdited);
            Assert.Equal(2, _dialogs.Confirmations);
        }
    }
}
