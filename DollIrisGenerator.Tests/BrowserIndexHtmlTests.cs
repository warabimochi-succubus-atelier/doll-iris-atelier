using System.IO;
using System.Text.RegularExpressions;

namespace DollIrisGenerator.Tests
{
    /// <summary>
    /// The browser app's index.html: published as is (the Service Worker checks its hash), and guarded by a
    /// Content-Security-Policy &lt;meta&gt;.
    /// </summary>
    public class BrowserIndexHtmlTests
    {
        private static string IndexHtml()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SPECIFICATION.md")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return File.ReadAllText(Path.Combine(dir!.FullName, "DollIrisGenerator.Web", "wwwroot", "index.html"));
        }

        /// <summary>Without HTML comments, so notes about the markup are not mistaken for it.</summary>
        private static string Markup() => Regex.Replace(IndexHtml(), "<!--.*?-->", "", RegexOptions.Singleline);

        [Fact]
        public void TestBaseHrefIsRelativeSoNoEditIsNeededForASubfolder()
        {
            var m = Regex.Match(Markup(), @"<base\s+href=""([^""]*)""");
            Assert.True(m.Success, "<base href> がありません");
            Assert.Equal("./", m.Groups[1].Value);
        }

        [Fact]
        public void TestCspComesAfterTheImportMapAndAllowsNoInlineScript()
        {
            string html = Markup();
            int importMap = html.IndexOf(@"<script type=""importmap"">", StringComparison.Ordinal);
            var csp = Regex.Match(html, @"<meta\s+http-equiv=""Content-Security-Policy""\s+content=""([^""]*)""");
            Assert.True(importMap >= 0, "インポートマップがありません");
            Assert.True(csp.Success, "CSP の <meta> がありません");

            // A <meta> policy only covers what follows it; the import map changes every publish, so it stays before
            Assert.True(csp.Index > importMap, "CSP はインポートマップの後に置く");

            var directives = csp.Groups[1].Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            string scriptSrc = Assert.Single(directives, d => d.StartsWith("script-src ", StringComparison.Ordinal));
            Assert.DoesNotContain("'unsafe-inline'", scriptSrc);
            Assert.DoesNotContain("'unsafe-eval'", scriptSrc);
            Assert.Contains("object-src 'none'", directives);

            // Every <script> after the policy is a file of the app's own (inline ones would be refused)
            foreach (Match s in Regex.Matches(html[csp.Index..], @"<script(?<attrs>[^>]*)>(?<body>.*?)</script>", RegexOptions.Singleline))
            {
                Assert.Contains("src=", s.Groups["attrs"].Value);
                Assert.True(string.IsNullOrWhiteSpace(s.Groups["body"].Value), "CSP の後にインラインスクリプトがあります");
            }
        }
    }
}
