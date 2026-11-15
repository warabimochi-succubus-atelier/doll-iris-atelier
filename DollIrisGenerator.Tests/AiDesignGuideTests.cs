using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;

namespace DollIrisGenerator.Tests
{
    /// <summary>
    /// docs/AI_DESIGN_GUIDE.md gives AI chats a template design to copy. It must load as it is and list every
    /// saved property, so a design an AI writes from it has no missing fields (which would fall back to defaults).
    /// </summary>
    public class AiDesignGuideTests
    {
        private static string RepoFile(params string[] parts)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SPECIFICATION.md")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir!.FullName, Path.Combine(parts));
        }

        /// <summary>The JSON object that follows the 「# テンプレート」 heading in the copy-paste block.</summary>
        private static string TemplateJson()
        {
            string text = File.ReadAllText(RepoFile("docs", "AI_DESIGN_GUIDE.md")).Replace("\r\n", "\n");
            int heading = text.IndexOf("\n# テンプレート", StringComparison.Ordinal);
            Assert.True(heading >= 0, "「# テンプレート」の見出しがありません");
            int start = text.IndexOf("\n{\n", heading, StringComparison.Ordinal);
            int end = text.IndexOf("\n}\n", start, StringComparison.Ordinal);
            Assert.True(start >= 0 && end > start, "テンプレートの JSON が見つかりません");
            return text.Substring(start + 1, end - start + 1);
        }

        private static string[] Keys(JsonNode? node) =>
            node!.AsObject().Select(kv => kv.Key).OrderBy(k => k, StringComparer.Ordinal).ToArray();

        [Fact]
        public void TestTemplateLoadsAsDesignImport()
        {
            var items = FavoritesStore.ParseImportText(TemplateJson(), out string? single);
            Assert.NotNull(single);
            var p = Assert.Single(items!).Parameters;
            Assert.Equal("サファイアブルー", p.Name);
            Assert.Single(p.Symbols);
            Assert.Single(p.Highlights);
            Assert.Contains("<svg", SvgExporter.GeneratePairSvg(p));
        }

        [Fact]
        public void TestSeveralDesignsLoadAsFavoritesList()
        {
            // The guide's 「複数作るとき」 shape: Name, Note and Parameters only (no Id or CreatedAt).
            string design = TemplateJson();
            string json = $"[{{\"Name\": \"案1\", \"Note\": \"\", \"Parameters\": {design}}}," +
                          $" {{\"Name\": \"案2\", \"Note\": \"\", \"Parameters\": {design}}}]";
            var items = FavoritesStore.ParseImportText(json, out string? single);
            Assert.Null(single);
            Assert.Equal(new[] { "案1", "案2" }, items!.Select(f => f.Name));
            Assert.NotEqual(items[0].Id, items[1].Id);
            Assert.Equal("#2979FF", items[1].Parameters.BaseColor);
        }

        [Fact]
        public void TestTemplateListsEverySavedProperty()
        {
            var template = JsonNode.Parse(TemplateJson())!;
            var saved = JsonNode.Parse(JsonSerializer.Serialize(new IrisParameters()));
            Assert.Equal(Keys(saved), Keys(template));

            // Symbols and highlights get a new Id when the file has none, so the template leaves it out.
            string[] ItemKeys(object item) =>
                Keys(JsonNode.Parse(JsonSerializer.Serialize(item))).Where(k => k != "Id").ToArray();
            Assert.Equal(ItemKeys(new DecorativeSymbolItem()), Keys(template["Symbols"]![0]));
            Assert.Equal(ItemKeys(new HighlightItem()), Keys(template["Highlights"]![0]));
        }
    }
}
