using System.IO;
using System.Text.RegularExpressions;

namespace DollIrisGenerator.Tests
{
    /// <summary>
    /// The browser app shows its own &lt;Version&gt; in the header, so it must match the desktop app's version.
    /// </summary>
    public class VersionSyncTests
    {
        private static string RepoFile(params string[] parts)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SPECIFICATION.md")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir!.FullName, Path.Combine(parts));
        }

        private static string ProjectVersion(string csproj)
        {
            var m = Regex.Match(File.ReadAllText(csproj), @"<Version>([^<]+)</Version>");
            Assert.True(m.Success, $"<Version> がありません: {csproj}");
            return m.Groups[1].Value.Trim();
        }

        [Fact]
        public void TestBrowserAppVersionMatchesDesktopApp()
        {
            string desktop = ProjectVersion(RepoFile("DollIrisGenerator", "DollIrisGenerator.csproj"));
            string web = ProjectVersion(RepoFile("DollIrisGenerator.Web", "DollIrisGenerator.Web.csproj"));
            Assert.Equal(desktop, web);
            Assert.Equal("v" + desktop, AppInfo.VersionLabel);
        }
    }
}
