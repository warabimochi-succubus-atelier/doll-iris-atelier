using System.Windows;
using System.Windows.Navigation;

namespace DollIrisGenerator.Views
{
    /// <summary>「バージョン情報」: app name, version, author, and links to the manual and GitHub.</summary>
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
        }

        private void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            e.Handled = true;
            if (!AppInfo.OpenInBrowser(e.Uri.AbsoluteUri))
            {
                MessageBox.Show(this, $"ブラウザを開けませんでした。\n\n{e.Uri.AbsoluteUri}", "バージョン情報",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnClose(object sender, RoutedEventArgs e) => Close();
    }
}
