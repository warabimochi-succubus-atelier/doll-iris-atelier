using System;
using System.IO;
using System.Linq;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using DollIrisGenerator.ViewModels;
using Xunit;

namespace DollIrisGenerator.Tests
{
    /// <summary>
    /// Favorite names: adding a design whose name is already in the list gives it a name of its own, and a favorite
    /// can be renamed in the list (desktop: <see cref="MainViewModel.RenameFavorite"/>; the browser app uses the same
    /// <see cref="FavoriteItem.Rename"/> and <see cref="FavoriteNames"/>).
    /// </summary>
    public sealed class FavoriteNameTests : IDisposable
    {
        private readonly string _dir = Directory.CreateTempSubdirectory("dolliris_tests_").FullName;
        private readonly MainViewModel _vm;

        public FavoriteNameTests()
        {
            _vm = new MainViewModel(new FakeDialogService(), new FavoritesStore(FavoritesPath));
        }

        private string FavoritesPath => Path.Combine(_dir, "favorites.json");

        public void Dispose() => Directory.Delete(_dir, recursive: true);

        [Fact]
        public void TestMakeUniqueNumbersNamesAlreadyInTheList()
        {
            var favorites = new[] { Favorite("ルビー"), Favorite("ルビー (2)"), Favorite("サファイア のコピー") };

            Assert.Equal("エメラルド", FavoriteNames.MakeUnique("エメラルド", favorites));
            Assert.Equal("ルビー (3)", FavoriteNames.MakeUnique("ルビー", favorites));
            Assert.Equal("サファイア のコピー (2)", FavoriteNames.MakeCopyName("サファイア", favorites));
            Assert.Equal("ルビー のコピー", FavoriteNames.MakeCopyName("ルビー", favorites));
        }

        [Fact]
        public void TestCreateFavoriteNamesTheCopyButNotTheEditedDesign()
        {
            var design = new IrisParameters { Name = "ルビー" };
            var favorite = FavoriteNames.CreateFavorite(design, new[] { Favorite("ルビー") });

            Assert.Equal("ルビー (2)", favorite.Name);
            Assert.Equal("ルビー (2)", favorite.Parameters.Name);
            Assert.Equal("ルビー", design.Name);
        }

        [Fact]
        public void TestRenameChangesTheEntryAndItsDesignAndIgnoresEmptyNames()
        {
            var favorite = Favorite("ルビー");

            Assert.True(favorite.Rename("  左目用ルビー  "));
            Assert.Equal("左目用ルビー", favorite.Name);
            Assert.Equal("左目用ルビー", favorite.Parameters.Name);

            Assert.False(favorite.Rename("   "));
            Assert.False(favorite.Rename(null));
            Assert.False(favorite.Rename("左目用ルビー"));
            Assert.Equal("左目用ルビー", favorite.Name);
        }

        [Fact]
        public void TestAddingTheDefaultNameTwiceGivesDifferentNames()
        {
            string defaultName = new IrisParameters().Name;
            _vm.AddFavoriteCommand.Execute(null);
            _vm.CurrentIris.BaseColor = "#123456";
            _vm.AddFavoriteCommand.Execute(null);
            _vm.CurrentIris.BaseColor = "#654321";
            _vm.AddFavoriteCommand.Execute(null);

            Assert.Equal(new[] { $"{defaultName} (3)", $"{defaultName} (2)", defaultName }, _vm.Favorites.Select(f => f.Name));
            Assert.Equal(_vm.Favorites.Select(f => f.Name), _vm.Favorites.Select(f => f.Parameters.Name));
            Assert.Equal(defaultName, _vm.CurrentIris.Name); // the design being edited keeps its name
            Assert.Contains($"{defaultName} (3)", _vm.StatusMessage);
            Assert.False(_vm.HasUnsavedDesigns()); // the favorite counts although its name differs
        }

        [Fact]
        public void TestRenamingIsSavedAndRestoredButIsNotAnUndoStep()
        {
            _vm.CurrentIris.BaseColor = "#123456";
            _vm.AddFavoriteCommand.Execute(null);
            var favorite = _vm.Favorites[0];
            bool couldUndo = _vm.CanUndo;

            Assert.True(_vm.RenameFavorite(favorite, "青い瞳"));
            Assert.Equal(couldUndo, _vm.CanUndo);
            Assert.Equal(new IrisParameters().Name, _vm.CurrentIris.Name);
            Assert.False(_vm.HasUnsavedDesigns());

            // Saved right away
            var saved = Assert.Single(new FavoritesStore(FavoritesPath).LoadPersisted().Favorites);
            Assert.Equal("青い瞳", saved.Name);
            Assert.Equal("青い瞳", saved.Parameters.Name);

            // Restoring gives the design the new name, and 「元に戻す」 goes back to the design before restoring
            _vm.CurrentIris.BaseColor = "#ABCDEF";
            _vm.LoadFavoriteCommand.Execute(favorite);
            Assert.Equal("青い瞳", _vm.CurrentIris.Name);
            Assert.Equal("#123456", _vm.CurrentIris.BaseColor);
            _vm.UndoCommand.Execute(null);
            Assert.Equal(new IrisParameters().Name, _vm.CurrentIris.Name);
            Assert.Equal("#ABCDEF", _vm.CurrentIris.BaseColor);
            Assert.Equal("青い瞳", favorite.Name);
        }

        [Fact]
        public void TestAnEmptyNameIsNotTaken()
        {
            _vm.AddFavoriteCommand.Execute(null);
            var favorite = _vm.Favorites[0];
            string name = favorite.Name;

            Assert.False(_vm.RenameFavorite(favorite, ""));
            Assert.Equal(name, favorite.Name);
        }

        [Fact]
        public void TestDuplicateAfterRenameUsesTheNewName()
        {
            _vm.AddFavoriteCommand.Execute(null);
            _vm.RenameFavorite(_vm.Favorites[0], "金色の瞳");
            _vm.DuplicateFavoriteCommand.Execute(_vm.Favorites[0]);

            Assert.Equal(new[] { "金色の瞳", "金色の瞳 のコピー" }, _vm.Favorites.Select(f => f.Name));
        }

        private static FavoriteItem Favorite(string name) => new(new IrisParameters { Name = name });
    }
}
