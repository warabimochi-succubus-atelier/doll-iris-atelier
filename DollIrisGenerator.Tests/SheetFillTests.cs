using System;
using System.IO;
using System.Linq;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using DollIrisGenerator.ViewModels;
using Xunit;

namespace DollIrisGenerator.Tests
{
    public sealed class SheetFillTests : IDisposable
    {
        private readonly string _dir = Directory.CreateTempSubdirectory("dolliris_fill_").FullName;
        private readonly MainViewModel _vm;

        public SheetFillTests()
        {
            _vm = new MainViewModel(new FakeDialogService(), new FavoritesStore(Path.Combine(_dir, "favorites.json")));
            _vm.SheetItems.Clear();
        }

        public void Dispose() => Directory.Delete(_dir, recursive: true);

        private IrisParameters Design(string name, double sizeMm)
        {
            var p = _vm.CurrentIris.Clone();
            p.Name = name;
            p.SizeMm = sizeMm;
            return p;
        }

        private int Capacity() => new SheetLayout(_vm.SheetItems, _vm.SheetConfig).PairsPerPage;
        private int Total() => _vm.SheetItems.Sum(i => i.PairCount);

        [Fact]
        public void TestDistributeKeepsRatioAndNeverShrinks()
        {
            Assert.Equal(new[] { 8, 4 }, SheetFill.Distribute(new[] { 2, 1 }, 12, 100));
            Assert.Equal(new[] { 4, 3, 3 }, SheetFill.Distribute(new[] { 1, 1, 1 }, 10, 100));
            Assert.Equal(new[] { 8, 2 }, SheetFill.Distribute(new[] { 5, 1 }, 10, 100));
            Assert.Equal(new[] { 5, 1 }, SheetFill.Distribute(new[] { 5, 1 }, 6, 100));
        }

        [Fact]
        public void TestDistributeStopsAtPerItemCap()
        {
            Assert.Equal(new[] { 3, 3 }, SheetFill.Distribute(new[] { 1, 2 }, 10, 3));
        }

        [Fact]
        public void TestTargetPairsRoundsUpToWholePages()
        {
            Assert.Equal(12, SheetFill.TargetPairs(0, 12));
            Assert.Equal(12, SheetFill.TargetPairs(5, 12));
            Assert.Equal(12, SheetFill.TargetPairs(12, 12));
            Assert.Equal(24, SheetFill.TargetPairs(13, 12));
        }

        [Fact]
        public void TestFillOnEmptySheetUsesOnlyCurrentDesign()
        {
            _vm.FillSheetWithCurrentCommand.Execute(null);

            var item = Assert.Single(_vm.SheetItems);
            Assert.Equal(Capacity(), item.PairCount);
        }

        [Fact]
        public void TestFillKeepsExistingDesignsInTheirRatio()
        {
            var a = Design("A", 12);
            var b = Design("B", 12);
            _vm.SheetItems.Add(new SheetItem(a, 2));
            _vm.SheetItems.Add(new SheetItem(b, 1));
            _vm.CurrentIris = b.Clone();

            _vm.FillSheetWithCurrentCommand.Execute(null);

            Assert.Equal(2, _vm.SheetItems.Count);
            Assert.Same(a, _vm.SheetItems[0].Parameters);
            Assert.Same(b, _vm.SheetItems[1].Parameters);
            int capacity = Capacity();
            Assert.Equal(capacity, Total());
            Assert.InRange(_vm.SheetItems[0].PairCount - 2 * _vm.SheetItems[1].PairCount, -2, 2);
            Assert.Contains("満杯", _vm.StatusMessage);
        }

        [Fact]
        public void TestFillAddsCurrentDesignWhenNotOnSheet()
        {
            _vm.SheetItems.Add(new SheetItem(Design("A", 12), 1));
            _vm.CurrentIris = Design("C", 12);

            _vm.FillSheetWithCurrentCommand.Execute(null);

            Assert.Equal(2, _vm.SheetItems.Count);
            Assert.Equal("C", _vm.SheetItems[1].Parameters.Name);
            Assert.Equal(Capacity(), Total());
            Assert.True(Math.Abs(_vm.SheetItems[0].PairCount - _vm.SheetItems[1].PairCount) <= 1);
            Assert.Contains("「C」を加え", _vm.StatusMessage);
        }

        [Fact]
        public void TestFillCapacityFollowsLargestDesign()
        {
            var small = Design("小", 8);
            _vm.SheetItems.Add(new SheetItem(small, 1));
            int smallOnly = Capacity();
            _vm.CurrentIris = Design("大", 20);

            _vm.FillSheetWithCurrentCommand.Execute(null);

            // The big design enlarges every cell, so the mixed sheet holds fewer pairs than the small one alone
            int mixed = Capacity();
            Assert.True(mixed < smallOnly);
            Assert.Equal(mixed, Total());
            Assert.Equal(1, new SheetLayout(_vm.SheetItems, _vm.SheetConfig).PageCount);
        }

        [Fact]
        public void TestFillCompletesLastPageWhenSheetSpansPages()
        {
            var a = Design("A", 12);
            _vm.SheetItems.Add(new SheetItem(a, 1));
            _vm.CurrentIris = a.Clone();
            int capacity = Capacity();
            _vm.SheetItems[0].PairCount = capacity + 1;
            if (capacity + 1 > SheetItem.MaxPairCount) return; // cannot set up a two-page sheet with one row on this layout

            _vm.FillSheetWithCurrentCommand.Execute(null);

            Assert.Equal(2 * capacity, Total());
            Assert.Contains("× 2 ページ", _vm.StatusMessage);
        }

        [Fact]
        public void TestFillLeavesFullSheetUnchanged()
        {
            _vm.SheetItems.Add(new SheetItem(Design("A", 12), 1));
            int capacity = Capacity();
            _vm.SheetItems[0].PairCount = capacity;
            _vm.CurrentIris = Design("別", 12);

            _vm.FillSheetWithCurrentCommand.Execute(null);

            var item = Assert.Single(_vm.SheetItems);
            Assert.Equal(capacity, item.PairCount);
            Assert.Contains("すでに満杯", _vm.StatusMessage);
        }
    }
}
