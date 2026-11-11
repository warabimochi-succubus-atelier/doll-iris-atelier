using System;
using System.IO;
using DollIrisGenerator.Services;
using DollIrisGenerator.ViewModels;

namespace DollIrisGenerator.Tests
{
    /// <summary>
    /// Creates view models that never read or write the user's real data in %AppData%.
    /// The parameterless <see cref="MainViewModel()"/> loads the saved print sheet, favorites and presets,
    /// so a test that uses it depends on what the person running the tests has saved, and edits to
    /// favorites are written back to their file.
    /// </summary>
    internal static class IsolatedViewModel
    {
        /// <summary>Created only when a test saves favorites; one fixed folder, so runs do not pile up folders.</summary>
        private static readonly string Root = Path.Combine(Path.GetTempPath(), "dolliris_isolated");

        /// <summary>A view model with an empty favorites file of its own, the print sheet and presets kept in memory only, and no real dialogs.</summary>
        public static MainViewModel Create() =>
            new MainViewModel(new FakeDialogService(), new FavoritesStore(Path.Combine(Root, $"{Guid.NewGuid():N}.json")));
    }
}
