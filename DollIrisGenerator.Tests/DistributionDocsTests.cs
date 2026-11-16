using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DollIrisGenerator.Tests
{
    /// <summary>
    /// The release zips (.github/workflows/release.yml) carry the documents in the same places as the repository,
    /// with docs/README_DIST.md as README.md, so the links between them work offline after unzipping.
    /// A relative link to a file the zips do not have would be broken for the users.
    /// </summary>
    public class DistributionDocsTests
    {
        /// <summary>Path in the zip → path in the repository.</summary>
        private static readonly (string Zip, string Repo)[] ZipDocuments =
        {
            ("README.md", "docs/README_DIST.md"),
            ("USER_MANUAL.md", "USER_MANUAL.md"),
            ("docs/DESIGN_JSON_FORMAT.md", "docs/DESIGN_JSON_FORMAT.md"),
            ("NOTICE.md", "NOTICE.md"),
            ("LICENSE", "LICENSE"),
        };

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SPECIFICATION.md")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return dir!.FullName;
        }

        private static readonly Regex Link = new(@"\]\(([^)\s]+)\)", RegexOptions.Compiled);

        [Fact]
        public void TestRelativeLinksPointToFilesInTheZip()
        {
            string root = RepoRoot();
            var inZip = ZipDocuments.Select(d => d.Zip).ToHashSet(StringComparer.Ordinal);
            var broken = new List<string>();

            foreach (var (zip, repo) in ZipDocuments.Where(d => d.Zip.EndsWith(".md", StringComparison.Ordinal)))
            {
                string text = File.ReadAllText(Path.Combine(root, repo));
                foreach (Match m in Link.Matches(text))
                {
                    string target = m.Groups[1].Value;
                    if (target.StartsWith('#') || target.Contains("://", StringComparison.Ordinal)
                        || target.StartsWith("mailto:", StringComparison.Ordinal))
                        continue;
                    int hash = target.IndexOf('#');
                    if (hash >= 0) target = target[..hash];

                    // Resolve against the document's folder in the zip
                    string folder = Path.GetDirectoryName(zip)!.Replace('\\', '/');
                    var parts = new List<string>(folder.Length == 0 ? Array.Empty<string>() : folder.Split('/'));
                    foreach (string part in target.Split('/'))
                    {
                        if (part == "..") { if (parts.Count > 0) parts.RemoveAt(parts.Count - 1); }
                        else if (part != ".") parts.Add(part);
                    }
                    string resolved = string.Join('/', parts);
                    if (!inZip.Contains(resolved))
                        broken.Add($"{repo}: ({m.Groups[1].Value}) → {resolved}");
                }
            }

            Assert.True(broken.Count == 0, "zip に入っていないファイルへのリンク:\n" + string.Join("\n", broken));
        }

        [Fact]
        public void TestBothZipsCarryTheDocuments()
        {
            string yml = File.ReadAllText(Path.Combine(RepoRoot(), ".github", "workflows", "release.yml"));
            foreach (string package in new[] { "$env:PACKAGE", "$env:CLI_PACKAGE" })
            {
                Assert.Contains($"Copy-Item LICENSE, NOTICE.md, USER_MANUAL.md \"dist/{package}\"", yml);
                Assert.Contains($"Copy-Item docs/README_DIST.md \"dist/{package}/README.md\"", yml);
                Assert.Contains($"Copy-Item docs/DESIGN_JSON_FORMAT.md \"dist/{package}/docs\"", yml);
            }
        }
    }
}
