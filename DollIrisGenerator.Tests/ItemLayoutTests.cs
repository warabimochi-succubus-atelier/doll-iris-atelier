using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using DollIrisGenerator.ViewModels;

namespace DollIrisGenerator.Tests
{
    /// <summary>
    /// 並べて追加 / 複製 / まとめて変更 for decorative symbols and highlights (SPECIFICATION 13.2): the placement math,
    /// the shared list editor both apps use, and the desktop app's undo / history around it.
    /// </summary>
    public class ItemLayoutTests
    {
        private const double Eps = 1e-9;

        private static void AssertPlace(ItemPlacement p, double x, double y, double angle)
        {
            Assert.Equal(x, p.X, 9);
            Assert.Equal(y, p.Y, 9);
            Assert.Equal(angle, p.Angle, 9);
        }

        [Fact]
        public void TestFullCircleStartsAtTheTopAndGoesClockwiseEvenly()
        {
            var s = new ItemLayoutSettings { Kind = ItemLayoutKind.Circle, Count = 4, Radius = 0.5, CenterX = 0.1, CenterY = -0.2 };
            var places = ItemLayout.Compute(s);
            Assert.Equal(4, places.Count);
            // Y grows downward, so "above the center" is a smaller Y
            AssertPlace(places[0], 0.1, -0.7, 0);
            AssertPlace(places[1], 0.6, -0.2, 90);
            AssertPlace(places[2], 0.1, 0.3, 180);
            AssertPlace(places[3], -0.4, -0.2, -90);
        }

        [Fact]
        public void TestArcPutsItemsOnBothEnds()
        {
            var s = new ItemLayoutSettings { Count = 3, Radius = 0.6, StartAngle = -90, SweepAngle = 180 };
            var places = ItemLayout.Compute(s);
            AssertPlace(places[0], -0.6, 0, -90);
            AssertPlace(places[1], 0, -0.6, 0);
            AssertPlace(places[2], 0.6, 0, 90);
        }

        [Fact]
        public void TestLineSpacesItemsEvenlyAlongItsDirection()
        {
            var s = new ItemLayoutSettings { Kind = ItemLayoutKind.Line, Count = 3, StartX = -0.6, StartY = 0.5, EndX = 0.6, EndY = 0.5 };
            var places = ItemLayout.Compute(s);
            AssertPlace(places[0], -0.6, 0.5, 0);
            AssertPlace(places[1], 0, 0.5, 0);
            AssertPlace(places[2], 0.6, 0.5, 0);

            // Straight down: the items turn a quarter clockwise
            s.StartX = 0; s.StartY = -0.4; s.EndX = 0; s.EndY = 0.4;
            Assert.All(ItemLayout.Compute(s), p => Assert.Equal(90, p.Angle, 9));
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(180, 180)]
        [InlineData(-180, 180)]
        [InlineData(270, -90)]
        [InlineData(-450, -90)]
        [InlineData(370, 10)]
        public void TestAnglesAreKeptInTheRotationSliderRange(double input, double expected)
        {
            Assert.Equal(expected, ItemLayout.NormalizeAngle(input), 9);
        }

        [Fact]
        public void TestLayoutSettingsStayInRange()
        {
            var s = new ItemLayoutSettings { Count = 500, Radius = 9, CenterX = -3, SweepAngle = 0, StartAngle = double.NaN };
            Assert.Equal(ItemLayoutSettings.MaxCount, s.Count);
            Assert.Equal(0.85, s.Radius);
            Assert.Equal(-0.85, s.CenterX);
            Assert.Equal(10, s.SweepAngle);
            Assert.Equal(0, s.StartAngle);
            s.Count = 0;
            Assert.Equal(ItemLayoutSettings.MinCount, s.Count);
        }

        [Fact]
        public void TestArrangedItemsCopyTheModelAndTurnAlongTheCircle()
        {
            var model = new DecorativeSymbolItem
            {
                Type = SymbolType.Heart, Color = "#FF88AA", Size = 0.12, Opacity = 0.6, Rotation = 10, Count = 2,
                CornerRounding = 0.3, Blend = LayerBlend.Screen, Enabled = true, PositionX = 0.4, PositionY = 0.4, IsSelected = true
            };
            var list = new List<DecorativeSymbolItem> { model };
            var s = new ItemLayoutSettings { Count = 4, Radius = 0.5 };

            var added = ItemLayout.AddArranged(list, model, s);

            Assert.Equal(4, added.Count);
            Assert.Equal(5, list.Count);
            Assert.Equal(0.4, model.PositionX); // the model itself is not moved
            Assert.True(model.IsSelected);
            Assert.Equal(5, list.Select(i => i.Id).Distinct().Count());
            double[] rotations = { 10, 100, -170, -80 };
            for (int i = 0; i < 4; i++)
            {
                var item = added[i];
                Assert.False(item.IsSelected);
                Assert.Equal(SymbolType.Heart, item.Type);
                Assert.Equal("#FF88AA", item.Color);
                Assert.Equal(0.12, item.Size);
                Assert.Equal(0.6, item.Opacity);
                Assert.Equal(2, item.Count);
                Assert.Equal(0.3, item.CornerRounding);
                Assert.Equal(LayerBlend.Screen, item.Blend);
                Assert.Equal(rotations[i], item.Rotation, 9);
            }

            s.AlignRotation = false;
            Assert.All(ItemLayout.AddArranged(list, model, s), i => Assert.Equal(10, i.Rotation));
        }

        [Fact]
        public void TestArrangingStopsAtTheItemLimit()
        {
            var list = Enumerable.Range(0, IrisParameters.MaxNestedItems - 3).Select(_ => new HighlightItem()).ToList();
            var added = ItemLayout.AddArranged(list, new HighlightItem(), new ItemLayoutSettings { Count = 10 });
            Assert.Equal(3, added.Count);
            Assert.Equal(IrisParameters.MaxNestedItems, list.Count);
            Assert.Empty(ItemLayout.AddArranged(list, new HighlightItem(), new ItemLayoutSettings { Count = 10 }));
        }

        [Fact]
        public void TestDuplicateGoesRightAfterAndIsMovedInsideTheSliders()
        {
            var a = new HighlightItem { PositionX = 0.2, PositionY = 0.8, Shape = HighlightShape.Star4, Rotation = 30 };
            var b = new HighlightItem();
            var list = new List<HighlightItem> { a, b };

            var copy = ItemLayout.DuplicateAfter(list, a)!;

            Assert.Same(copy, list[1]);
            Assert.Same(b, list[2]);
            Assert.NotEqual(a.Id, copy.Id);
            Assert.Equal(HighlightShape.Star4, copy.Shape);
            Assert.Equal(30, copy.Rotation);
            Assert.Equal(0.3, copy.PositionX, 9);
            Assert.Equal(0.7, copy.PositionY, 9); // 0.9 would leave the -0.85..0.85 sliders: moved up instead

            Assert.Null(ItemLayout.DuplicateAfter(list, new HighlightItem())); // not in the list
            var full = Enumerable.Range(0, IrisParameters.MaxNestedItems).Select(_ => new HighlightItem()).ToList();
            Assert.Null(ItemLayout.DuplicateAfter(full, full[0]));
        }

        private static string[] SavedNames<T>(T item) =>
            JsonNode.Parse(JsonSerializer.Serialize(item))!.AsObject().Select(kv => kv.Key).ToArray();

        /// <summary>まとめて変更 covers every saved setting except the Id and the position, and copies each one.</summary>
        private static void AssertBulkCoversSavedSettings<T>(T source, Func<T> blank) where T : class, IArrangeableItem<T>
        {
            var expected = SavedNames(source).Except(new[] { "Id", "PositionX", "PositionY" }).OrderBy(n => n).ToArray();
            Assert.Equal(expected, T.BulkSettingNames.OrderBy(n => n).ToArray());

            var target = blank();
            foreach (var name in T.BulkSettingNames) Assert.True(source.CopySettingTo(target, name), name);
            var s = JsonNode.Parse(JsonSerializer.Serialize(source))!.AsObject();
            var t = JsonNode.Parse(JsonSerializer.Serialize(target))!.AsObject();
            foreach (var name in expected) Assert.Equal(s[name]!.ToJsonString(), t[name]!.ToJsonString());
            Assert.False(source.CopySettingTo(target, "PositionX"));
            Assert.False(source.CopySettingTo(target, "Id"));
        }

        [Fact]
        public void TestBulkSettingsAreEverySavedSettingButIdAndPosition()
        {
            AssertBulkCoversSavedSettings(new DecorativeSymbolItem
            {
                Type = SymbolType.Moon, Count = 5, Size = 0.3, Color = "linear-gradient(90deg, #FF0000 0%, #0000FF 100%)",
                Opacity = 0.4, Rotation = -45, CornerRounding = 0.7, Enabled = true, Blend = LayerBlend.Overlay
            }, () => new DecorativeSymbolItem());
            AssertBulkCoversSavedSettings(new HighlightItem
            {
                Shape = HighlightShape.Oval, Size = 0.3, AspectRatio = 1.8, Rotation = 20, Color = "#FFEEAA",
                Opacity = 0.5, Enabled = false, Blend = LayerBlend.ColorDodge
            }, () => new HighlightItem());
        }

        [Fact]
        public void TestSelectionIsNeitherSavedNorADesignChange()
        {
            var p = new IrisParameters();
            p.Symbols.Add(new DecorativeSymbolItem());
            p.Highlights.Add(new HighlightItem());
            string before = JsonSerializer.Serialize(p);
            var changes = new List<string?>();
            p.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

            p.Symbols[0].IsSelected = true;
            p.Highlights[0].IsSelected = true;

            Assert.Empty(changes);
            Assert.Equal(before, JsonSerializer.Serialize(p));
            Assert.False(p.Clone().Symbols[0].IsSelected);
            Assert.DoesNotContain("IsSelected", SavedNames(p.Symbols[0]));

            p.Symbols[0].Size = 0.3; // a real edit still reaches the design
            Assert.Contains(IrisParameters.NestedItemsChangedName, changes);
        }

        [Fact]
        public void TestBulkEditSetsOnlyTheChangedSettingOnTheTickedItems()
        {
            var items = new ObservableCollection<DecorativeSymbolItem>
            {
                new() { Color = "#111111", Size = 0.1, PositionX = 0.1, Type = SymbolType.Star5 },
                new() { Color = "#222222", Size = 0.2, PositionX = 0.2, Type = SymbolType.Heart },
                new() { Color = "#333333", Size = 0.3, PositionX = 0.3, Type = SymbolType.Cross },
            };
            var editor = new ItemListEditor<DecorativeSymbolItem>(() => new DecorativeSymbolItem());
            editor.Attach(items);
            Assert.False(editor.HasSelection);

            items[2].IsSelected = true;
            items[1].IsSelected = true;
            Assert.Equal(2, editor.SelectedCount);
            // The form starts from the first ticked item in the list
            Assert.Equal("#222222", editor.Bulk.Color);
            Assert.Equal(SymbolType.Heart, editor.Bulk.Type);

            var changed = new List<string>();
            editor.BulkSettingChanged += changed.Add;
            editor.Bulk.Color = "#ABCDEF";

            Assert.Equal(new[] { "Color" }, changed);
            Assert.Equal(new[] { "#111111", "#ABCDEF", "#ABCDEF" }, items.Select(i => i.Color));
            Assert.Equal(new[] { 0.1, 0.2, 0.3 }, items.Select(i => i.Size)); // other settings are left as they were
            Assert.Equal(SymbolType.Cross, items[2].Type);

            editor.Bulk.PositionX = -0.5; // not a bulk setting
            Assert.Equal(new[] { 0.1, 0.2, 0.3 }, items.Select(i => i.PositionX));

            Assert.Equal(2, editor.RemoveSelected());
            Assert.Single(items);
            Assert.False(editor.HasSelection);
        }

        [Fact]
        public void TestEditorFollowsAReplacedListAndUsesTheTickedItemAsModel()
        {
            var editor = new ItemListEditor<HighlightItem>(() => new HighlightItem { Shape = HighlightShape.Circle, Color = "#FFFFFF" });
            var first = new ObservableCollection<HighlightItem> { new() { IsSelected = true } };
            editor.Attach(first);
            Assert.True(editor.HasSelection);

            var second = new ObservableCollection<HighlightItem>
            {
                new() { Shape = HighlightShape.Rhombus, Color = "#AACCFF" },
                new() { Shape = HighlightShape.Star4, Color = "#FFAA00" },
            };
            editor.Attach(second);
            Assert.False(editor.HasSelection);

            // Nothing ticked: the new-item defaults
            editor.Layout.Count = 2;
            Assert.All(editor.AddArranged(), h => Assert.Equal(HighlightShape.Circle, h.Shape));

            second[1].IsSelected = true;
            var added = editor.AddArranged();
            Assert.All(added, h => { Assert.Equal(HighlightShape.Star4, h.Shape); Assert.Equal("#FFAA00", h.Color); });
            Assert.Equal(6, second.Count);
            Assert.Single(first);
        }

        [Fact]
        public void TestDesktopArrangeAndDuplicateAreOneUndoStepEach()
        {
            var vm = IsolatedViewModel.Create();
            int symbols = vm.CurrentIris.Symbols.Count;
            int history = vm.HistoryItems.Count;

            vm.SymbolTools.Editor.Layout.Count = 6;
            vm.SymbolTools.ArrangeCommand.Execute(null);
            Assert.Equal(symbols + 6, vm.CurrentIris.Symbols.Count);
            Assert.StartsWith("装飾記号を並べて追加 ", vm.HistoryItems[0].Details);
            Assert.Equal(history + 1, vm.HistoryItems.Count);
            Assert.Contains("6 個追加しました", vm.StatusMessage);

            var last = vm.CurrentIris.Symbols[^1];
            vm.SymbolTools.DuplicateCommand.Execute(last);
            Assert.Equal(symbols + 7, vm.CurrentIris.Symbols.Count);

            vm.UndoCommand.Execute(null);
            Assert.Equal(symbols + 6, vm.CurrentIris.Symbols.Count);
            vm.UndoCommand.Execute(null);
            Assert.Equal(symbols, vm.CurrentIris.Symbols.Count);
            // Undo replaced the design: the tools work on the restored list
            Assert.Same(vm.CurrentIris.Symbols, vm.SymbolTools.Editor.Items);
            Assert.Same(vm.CurrentIris.Highlights, vm.HighlightTools.Editor.Items);
        }

        [Fact]
        public void TestDesktopTickingIsNotAnEditButBulkChangesAre()
        {
            var vm = IsolatedViewModel.Create();
            vm.HighlightTools.Editor.Layout.Count = 3;
            vm.HighlightTools.ArrangeCommand.Execute(null);
            var highlights = vm.CurrentIris.Highlights;
            int history = vm.HistoryItems.Count;
            int undoBefore = CountUndo(vm);

            vm.HighlightTools.SelectAllCommand.Execute(null);
            Assert.All(highlights, h => Assert.True(h.IsSelected));
            Assert.Equal(undoBefore, CountUndo(vm));

            vm.HighlightTools.Editor.Bulk.Opacity = 0.25;
            Assert.All(highlights, h => Assert.Equal(0.25, h.Opacity));
            Assert.Equal(undoBefore + 1, CountUndo(vm)); // one step for the whole change (drag), like any slider

            vm.HighlightTools.RemoveSelectedCommand.Execute(null);
            Assert.Empty(vm.CurrentIris.Highlights);
            Assert.StartsWith("選択したハイライトを削除 ", vm.HistoryItems[0].Details);
            Assert.True(vm.HistoryItems.Count > history);
        }

        /// <summary>How many undo steps the view model holds.</summary>
        private static int CountUndo(MainViewModel vm)
        {
            var field = typeof(MainViewModel).GetField("_undoStack", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            return ((System.Collections.ICollection)field.GetValue(vm)!).Count;
        }
    }
}
