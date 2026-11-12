using System.Diagnostics;

namespace DollIrisGenerator
{
    /// <summary>
    /// The app's name, version, author and links, shared by the header and the 「バージョン情報」 dialog.
    /// </summary>
    public static class AppInfo
    {
        public const string Name = "Doll Iris Atelier";
        // Keep in sync with <Version> in DollIrisGenerator.csproj
        public const string VersionLabel = "v0.13.0";
        public const string Author = "わらびもちサキュバス工房";
        public const string Copyright = "© 2026 わらびもちサキュバス工房";
        public const string License = "MIT License (ロゴとアイコンを除く)";

        public const string RepositoryUrl = "https://github.com/warabimochi-succubus-atelier/doll-iris-atelier";
        public const string ManualUrl = RepositoryUrl + "/blob/main/USER_MANUAL.md";
        public const string IssuesUrl = RepositoryUrl + "/issues";
        public const string LicenseUrl = RepositoryUrl + "/blob/main/LICENSE";

        // Hyperlink.NavigateUri needs a Uri; an x:Static string is not converted
        public static readonly Uri RepositoryUri = new(RepositoryUrl);
        public static readonly Uri ManualUri = new(ManualUrl);
        public static readonly Uri IssuesUri = new(IssuesUrl);
        public static readonly Uri LicenseUri = new(LicenseUrl);

        /// <summary>Opens <paramref name="url"/> in the default browser.</summary>
        /// <returns>False when no browser could be started.</returns>
        public static bool OpenInBrowser(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                return true;
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                return false;
            }
        }
    }
}
