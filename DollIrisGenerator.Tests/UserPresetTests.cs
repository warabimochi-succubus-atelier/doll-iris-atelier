using System;
using System.IO;
using System.Linq;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using DollIrisGenerator.ViewModels;
using Xunit;

namespace DollIrisGenerator.Tests
{
    /// <summary>Presets the user adds: saved between runs, renamed and removed; the built-in ones stay as they are.</summary>
    public sealed class UserPresetTests : IDisposable
    {
        private readonly string _dir = Directory.CreateTempSubdirectory("dolliris_presets_").FullName;
        private readonly FakeDialogService _dialogs = new();

        private string PresetsPath => Path.Combine(_dir, "user-presets.json");

        private MainViewModel NewViewModel() =>
            new(_dialogs, new FavoritesStore(Path.Combine(_dir, "favorites.json")), null, new UserPresetStore(PresetsPath));

        public void Dispose() => Directory.Delete(_dir, recursive: true);

        [Fact]
        public void TestAddedPresetIsThereOnTheNextStart()
        {
            var vm = NewViewModel();
            vm.CurrentIris.Name = "あお";
            vm.CurrentIris.RayCount = 33;
            vm.AddCurrentAsPresetCommand.Execute(null);

            var reopened = NewViewModel();
            Assert.Equal(ColorPresets.All.Count + 1, reopened.Presets.Count);
            var preset = reopened.Presets.Last();
            Assert.Equal("カスタム: あお", preset.Name);
            Assert.Equal(33, preset.RayCount);
            // Only the added ones go into the file
            Assert.Single(ColorPresets.ImportPalettesFromFile(PresetsPath)!);
        }

        [Fact]
        public void TestRegisteringTheSameDesignTwiceGivesDifferentNames()
        {
            var vm = NewViewModel();
            vm.CurrentIris.Name = "あお";
            vm.AddCurrentAsPresetCommand.Execute(null);
            vm.AddCurrentAsPresetCommand.Execute(null);
            Assert.Equal(new[] { "カスタム: あお", "カスタム: あお (2)" }, vm.Presets.Skip(ColorPresets.All.Count).Select(p => p.Name));
        }

        [Fact]
        public void TestRenameIsSavedAndRefusesEmptyOrTakenNames()
        {
            var vm = NewViewModel();
            vm.CurrentIris.Name = "あお";
            vm.AddCurrentAsPresetCommand.Execute(null);
            var preset = vm.SelectedPreset!;
            Assert.True(vm.IsSelectedPresetUserMade);
            Assert.Equal("カスタム: あお", vm.PresetNameDraft);

            vm.PresetNameDraft = "  空色  ";
            vm.RenamePresetCommand.Execute(null);
            Assert.Equal("空色", preset.Name);
            Assert.Equal("空色", NewViewModel().Presets.Last().Name);

            vm.PresetNameDraft = "   ";
            vm.RenamePresetCommand.Execute(null);
            Assert.Equal("空色", preset.Name);

            vm.PresetNameDraft = ColorPresets.All[0].Name; // a built-in name is taken
            vm.RenamePresetCommand.Execute(null);
            Assert.Equal("空色", preset.Name);
            Assert.Single(_dialogs.Errors);
        }

        [Fact]
        public void TestRemoveAsksFirstAndLeavesTheDesignAsItIs()
        {
            var vm = NewViewModel();
            vm.AddCurrentAsPresetCommand.Execute(null);
            var preset = vm.SelectedPreset!;
            vm.CurrentIris.RayCount = 77; // edited after the preset was applied

            _dialogs.ConfirmAnswer = false;
            vm.RemovePresetCommand.Execute(null);
            Assert.Contains(preset, vm.Presets);

            _dialogs.ConfirmAnswer = true;
            vm.RemovePresetCommand.Execute(null);
            Assert.DoesNotContain(preset, vm.Presets);
            Assert.Null(vm.SelectedPreset);
            Assert.Equal(77, vm.CurrentIris.RayCount);
            Assert.Equal(2, _dialogs.Confirmations);
            Assert.Equal(ColorPresets.All.Count, NewViewModel().Presets.Count);
        }

        [Fact]
        public void TestBuiltInPresetsCannotBeRenamedOrRemoved()
        {
            var vm = NewViewModel();
            var builtIn = vm.Presets[0];
            vm.SelectedPreset = builtIn;
            Assert.False(vm.IsSelectedPresetUserMade);
            Assert.False(vm.RenamePresetCommand.CanExecute(null));
            Assert.False(vm.RemovePresetCommand.CanExecute(null));

            _dialogs.ConfirmAnswer = true;
            vm.PresetNameDraft = "別名";
            vm.RenamePresetCommand.Execute(null);
            vm.RemovePresetCommand.Execute(null);
            Assert.Equal("ベーシック: クリアブルー", builtIn.Name);
            Assert.Contains(builtIn, vm.Presets);
        }

        [Fact]
        public void TestImportedPresetsAreSaved()
        {
            string shared = Path.Combine(_dir, "shared.irispalette.json");
            ColorPresets.ExportPalettesToFile(shared, new[] { ColorPresets.FromDesign(new IrisParameters(), "もらったプリセット", "") });

            var vm = NewViewModel();
            _dialogs.NextOpenPath = shared;
            vm.ImportPresetsCommand.Execute(null);
            Assert.Equal("もらったプリセット", NewViewModel().Presets.Last().Name);
        }

        [Fact]
        public void TestUnreadableFileIsSetAsideAndReported()
        {
            File.WriteAllText(PresetsPath, "{ broken");
            var vm = NewViewModel();
            Assert.Equal(ColorPresets.All.Count, vm.Presets.Count);
            vm.ShowUserPresetsLoadProblem();
            Assert.Single(_dialogs.Errors);
            Assert.Single(Directory.GetFiles(_dir, "user-presets.broken-*.json"));
            Assert.False(File.Exists(PresetsPath));
        }
    }
}
