using System.Windows;
using Microsoft.Win32;

namespace DollIrisGenerator.Services
{
    /// <summary>
    /// The file pickers and message boxes the view model needs, behind an interface so its
    /// workflows can run in tests without showing any UI.
    /// </summary>
    public interface IDialogService
    {
        /// <returns>The chosen path, or null when the user cancelled.</returns>
        string? PickSaveFile(string filter, string fileName, string title);

        /// <returns>The chosen path, or null when the user cancelled.</returns>
        string? PickOpenFile(string filter, string title);

        void ShowInfo(string message, string title);
        void ShowWarning(string message, string title);
        void ShowError(string message, string title);

        /// <returns>True when the user answered Yes.</returns>
        bool ConfirmWarning(string message, string title, bool defaultToNo = false);
    }

    public sealed class WpfDialogService : IDialogService
    {
        public string? PickSaveFile(string filter, string fileName, string title)
        {
            var dialog = new SaveFileDialog { Filter = filter, FileName = fileName, Title = title };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public string? PickOpenFile(string filter, string title)
        {
            var dialog = new OpenFileDialog { Filter = filter, Title = title };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public void ShowInfo(string message, string title) =>
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

        public void ShowWarning(string message, string title) =>
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

        public void ShowError(string message, string title) =>
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

        public bool ConfirmWarning(string message, string title, bool defaultToNo = false)
        {
            var answer = defaultToNo
                ? MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
                : MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning);
            return answer == MessageBoxResult.Yes;
        }
    }
}
