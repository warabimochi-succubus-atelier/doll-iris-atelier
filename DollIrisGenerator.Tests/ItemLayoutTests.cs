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
    /// 並べて追加 / 複製 / まとめて変更 for decorative symbols and highlights (SPECIFICATION 13.2): the random placement,
    /// the shared list editor both apps use, and the desktop app's undo / history around it.
    /// </summary>
    public class ItemLayoutTests
    {
        private static double Distance(ItemPlacement p) => Math.Sqrt(p.X * p.X + p.Y * p.Y);

        [Fact]
        public void TestRandomPlacesStayInTheRingAndAreRepeatableWithASeed()
        {
            var s = new ItemLayoutSettings { Count = 24, InnerRadius = 0.3, OuterRadius = 0.7 };
            var places = ItemLayout.Compute(s, new Random(7), 0.1);

            Assert.Equal(24, places.Count);
            Assert.All(places, p =>
            {
                Assert.InRange(Distance(p), 0.3 - 0.001, 0.7 + 0.001); // positions are rounded to 0.001
                Assert.Equal(Math.Round(p.X, 3), p.X);
                Assert.Equal(Math.Round(p.Y, 3), p.Y);
            });
            Assert.Equal(places, ItemLayout.Compute(s, new Random(7), 0.1));
            Assert.NotEqual(places, ItemLayout.Compute(s, new Random(8), 0.1));

            // Inner and outer the wrong way round still mean the ring between them
            var swapped = new ItemLayoutSettings { Count = 10, InnerRadius = 0.7, OuterRadius = 0.3 };
            Assert.All(ItemLayout.Compute(swapped, new Random(1), 0.1), p => Assert.InRange(Distance(p), 0.3 - 0.001, 0.7 + 0.001));
        }

        [Fact]
        public void TestRandomPlacesSpreadOutInsteadOfPilingUp()
        {
            var s = new ItemLayoutSettings { Count = 8 }; // the form's defaults: 0.3 to 0.75 from the center
            for (int seed = 1; seed <= 30; seed++)
            {
                var places = ItemLayout.Compute(s, new Random(seed), 0.18);
                double closest = places
                    .SelectMany((p, i) => places.Skip(i + 1).Select(q => Math.Sqrt((p.X - q.X) * (p.X - q.X) + (p.Y - q.Y) * (p.Y - q.Y))))
                    .Min();
                Assert.True(closest > 0.15, $"seed {seed}: two items only {closest:F3} apart");
            }

            // Items already in the list are kept clear of too: one new item next to a crowd goes to the far side
            var crowd = Enumerable.Range(0, 12).Select(i => (X: 0.6 + 0.01 * i, Y: 0.0)).ToList();
            var single = ItemLayout.Compute(new ItemLayoutSettings { Count = 1, InnerRadius = 0.5, OuterRadius = 0.7 }, new Random(3), 0.1, crowd);
            Assert.True(single[0].X < 0, $"placed at {single[0].X:F3}");
        }

        [Fact]
        public void TestRotationIsRandomOnlyWhenAsked()
        {
            var s = new ItemLayoutSettings { Count = 20, RandomRotation = true };
            var turned = ItemLayout.Compute(s, new Random(5), 0.1);
            Assert.All(turned, p =>
            {
                Assert.InRange(p.Rotation, -179.0, 180.0);
                Assert.Equal(Math.Round(p.Rotation), p.Rotation);
            });
            Assert.True(turned.Select(p => p.Rotation).Distinct().Count() > 10);

            s.RandomRotation = false;
            Assert.All(ItemLayout.Compute(s, new Random(5), 0.1), p => Assert.Equal(0.0, p.Rotation));
        }

        [Fact]
        public void TestPlacesComeTopLeftToBottomRight()
        {
            // Rows as high as an item (2 x size = 0.2): top row first, left to right inside a row
            var places = new[]
            {
                new ItemPlacement(0.5, 0.42, 0), new ItemPlacement(-0.5, -0.5, 0), new ItemPlacement(0.4, -0.45, 0),
                new ItemPlacement(-0.3, 0.5, 0), new ItemPlacement(0.0, 0.05, 0),
            };
            var ordered = ItemLayout.ReadingOrder(places, 0.1);
            Assert.Equal(new[] { (-0.5, -0.5), (0.4, -0.45), (0.0, 0.05), (-0.3, 0.5), (0.5, 0.42) },
                ordered.Select(p => (p.X, p.Y)));

            // Whatever the random places, the list reads row by row
            var computed = ItemLayout.Compute(new ItemLayoutSettings { Count = 24 }, new Random(11), 0.12);
            var rows = computed.Select(p => Math.Floor((p.Y + 2.0) / 0.24)).ToList();
            for (int i = 1; i < computed.Count; i++)
            {
                Assert.True(rows[i] >= rows[i - 1]);
                if (rows[i] == rows[i - 1]) Assert.True(computed[i].X >= computed[i - 1].X);
            }
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
            var s = new ItemLayoutSettings { Count = 500, InnerRadius = -1, OuterRadius = 9 };
            Assert.Equal(ItemLayoutSettings.MaxCount, s.Count);
            Assert.Equal(0.0, s.InnerRadius);
            Assert.Equal(0.85, s.OuterRadius);
            s.Count = 0;
            s.OuterRadius = double.NaN;
            Assert.Equal(ItemLayoutSettings.MinCount, s.Count);
            Assert.Equal(0.75, s.OuterRadius);
        }

        [Fact]
        public void TestFormStartsWithThreeSymbolsOrTwoHighlights()
        {
            var vm = IsolatedViewModel.Create();
            Assert.Equal(3, vm.SymbolTools.Editor.Layout.Count);
            Assert.Equal(2, vm.HighlightTools.Editor.Layout.Count);
            // The browser app builds its editors with the same constants
            Assert.Equal(3, new ItemListEditor<DecorativeSymbolItem>(() => new DecorativeSymbolItem(), ItemLayoutSettings.DefaultSymbolCount).Layout.Count);
            Assert.Equal(2, new ItemListEditor<HighlightItem>(() => new HighlightItem(), ItemLayoutSettings.DefaultHighlightCount).Layout.Count);
        }

        [Fact]
        public void TestArrangedItemsCopyTheTemplateLook()
        {
            var template = new DecorativeSymbolItem
            {
                Type = SymbolType.Heart, Color = "#FF88AA", Size = 0.12, Opacity = 0.6, Rotation = 45, Count = 1,
                CornerRounding = 0.3, Blend = LayerBlend.Screen, Enabled = true, PositionX = 0.4, PositionY = 0.4
            };
            var existing = new DecorativeSymbolItem { IsSelected = true };
            var list = new List<DecorativeSymbolItem> { existing };
            var s = new ItemLayoutSettings { Count = 5, RandomRotation = false };

            var added = ItemLayout.AddArranged(list, template, s, new Random(2));

            Assert.Equal(5, added.Count);
            Assert.Equal(6, list.Count);
            Assert.Equal(added, list.Skip(1));
            Assert.Equal(0.4, template.PositionX); // the template itself stays as it is
            Assert.Equal(6, list.Select(i => i.Id).Distinct().Count());
            Assert.All(added, item =>
            {
                Assert.False(item.IsSelected);
                Assert.Equal(SymbolType.Heart, item.Type);
                Assert.Equal("#FF88AA", item.Color);
                Assert.Equal(0.12, item.Size);
                Assert.Equal(0.6, item.Opacity);
                Assert.Equal(0.3, item.CornerRounding);
                Assert.Equal(LayerBlend.Screen, item.Blend);
                Assert.True(item.Enabled);
                Assert.Equal(0.0, item.Rotation); // not random: upright, not the template's 45°
            });
            // In the order Compute gives (reading order), keeping clear of the item already there
            Assert.Equal(ItemLayout.Compute(s, new Random(2), 0.12, new[] { (existing.PositionX, existing.PositionY) })
                    .Select(p => (p.X, p.Y)),
                added.Select(i => (i.PositionX, i.PositionY)));
        }

        [Fact]
        public void TestArrangingStopsAtTheItemLimit()
        {
            var list = Enumerable.Range(0, IrisParameters.MaxNestedItems - 3).Select(_ => new HighlightItem()).ToList();
            var added = ItemLayout.AddArranged(list, new HighlightItem(), new ItemLayoutSettings { Count = 10 }, new Random(1));
            Assert.Equal(3, added.Count);
            Assert.Equal(IrisParameters.MaxNestedItems, list.Count);
            Assert.Empty(ItemLayout.AddArranged(list, new HighlightItem(), new ItemLayoutSettings { Count = 10 }, new Random(1)));
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
                Shape = HighlightShape.Diamond, Size = 0.3, Count = 4, CornerRounding = 0.6, StretchTop = 1.2, StretchBottom = 0.8, StretchLeft = 1.8, StretchRight = 0.6, Rotation = 20, Color = "#FFEEAA",
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
        public void TestEditorFollowsAReplacedListAndArrangesItsTemplate()
        {
            var editor = new ItemListEditor<HighlightItem>(() => new HighlightItem { Shape = HighlightShape.Circle, Color = "#FFFFFF" })
            {
                Random = new Random(4)
            };
            var first = new ObservableCollection<HighlightItem> { new() { IsSelected = true } };
            editor.Attach(first);
            Assert.True(editor.HasSelection);

            var second = new ObservableCollection<HighlightItem>
            {
                new() { Shape = HighlightShape.Diamond, Color = "#AACCFF", IsSelected = true },
            };
            editor.Attach(second);
            Assert.True(editor.HasSelection);

            // The form's own look, whatever is ticked
            Assert.Equal(HighlightShape.Circle, editor.Template.Shape);
            editor.Template.Shape = HighlightShape.Star4;
            editor.Template.Color = "#FFAA00";
            editor.Layout.Count = 3;
            var added = editor.AddArranged();
            Assert.Equal(3, added.Count);
            Assert.All(added, h => { Assert.Equal(HighlightShape.Star4, h.Shape); Assert.Equal("#FFAA00", h.Color); });
            Assert.Equal(4, second.Count);
            Assert.Single(first);
            Assert.Equal(HighlightShape.Diamond, second[0].Shape); // the ticked item is not touched
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
