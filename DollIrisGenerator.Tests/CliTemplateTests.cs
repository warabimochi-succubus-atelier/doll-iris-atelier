using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using DollIrisGenerator.Cli;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;

namespace DollIrisGenerator.Tests
{
    /// <summary>DollIrisCli stage 3 (SPECIFICATION.md 23.8): <c>template</c>.</summary>
    public class CliTemplateTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "DollIrisCliTemplateTests-" + Guid.NewGuid().ToString("N"));

        public CliTemplateTests() => Directory.CreateDirectory(_dir);

        public void Dispose()
        {
            try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
        }

        private static (int Code, string Out, string Err) Run(params string[] args)
        {
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            int code = CliApp.Run(args, stdout, stderr);
            return (code, stdout.ToString(), stderr.ToString());
        }

        /// <summary>The design as saved, without the symbols' and highlights' Id (which a file may leave out).</summary>
        private static string WithoutIds(IrisParameters p)
        {
            var node = JsonSerializer.SerializeToNode(p)!.AsObject();
            foreach (var list in new[] { "Symbols", "Highlights" })
                foreach (var item in node[list]!.AsArray()) item!.AsObject().Remove("Id");
            return node.ToJsonString();
        }

        [Fact]
        public void TestDefaultTemplateIsTheAppsNewDesign()
        {
            var result = Run("template");
            Assert.Equal(CliApp.ExitOk, result.Code);

            var report = DesignFileValidator.Validate(result.Out);
            Assert.Empty(report.Issues); // every saved name, every value in range
            Assert.Equal(WithoutIds(new IrisParameters()), WithoutIds(report.Designs.Single().Parameters));

            // No Ids: each file made from the template gets its own when loaded
            var node = JsonNode.Parse(result.Out)!;
            Assert.All(node["Highlights"]!.AsArray(), h => Assert.False(h!.AsObject().ContainsKey("Id")));
            Assert.All(node["Symbols"]!.AsArray(), s => Assert.False(s!.AsObject().ContainsKey("Id")));

            // Japanese stays readable
            Assert.Contains("\"新規虹彩デザイン\"", result.Out);
        }

        [Fact]
        public void TestEveryPresetMakesAValidTemplate()
        {
            foreach (var preset in ColorPresets.All)
            {
                var report = DesignFileValidator.Validate(CliApp.TemplateJson(preset, favorites: false));
                Assert.Empty(report.Issues);

                // The same as 🎨 プリセットを適用 on a new design
                var expected = new IrisParameters();
                ColorPresets.ApplyPreset(expected, preset);
                expected.Name = preset.Name;
                Assert.Equal(WithoutIds(expected), WithoutIds(report.Designs.Single().Parameters));
            }
        }

        [Fact]
        public void TestFavoritesForm()
        {
            var result = Run("template", "--favorites", "--preset", "桜");
            var report = DesignFileValidator.Validate(result.Out);
            Assert.Equal(DesignFileKind.Favorites, report.Kind);
            Assert.Empty(report.Issues);
            Assert.Equal("特殊: 桜", report.Designs.Single().Name);
        }

        [Fact]
        public void TestFindPreset()
        {
            var all = ColorPresets.All;
            Assert.Same(all[0], CliApp.FindPreset("1"));
            Assert.Same(all[^1], CliApp.FindPreset(all.Count.ToString()));
            Assert.Same(all[5], CliApp.FindPreset(all[5].Name));
            Assert.Equal("特殊: キャットアイ", CliApp.FindPreset("キャットアイ").Name);

            Assert.Equal(CliApp.ExitError, Run("template", "--preset", "0").Code);
            Assert.Equal(CliApp.ExitError, Run("template", "--preset", "そんな色").Code);
            var ambiguous = Run("template", "--preset", "雰囲気");
            Assert.Equal(CliApp.ExitError, ambiguous.Code);
            Assert.Contains("複数", ambiguous.Err);
        }

        [Fact]
        public void TestListShowsEveryPresetWithItsNumber()
        {
            var lines = Run("template", "--list").Out.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(ColorPresets.All.Count, lines.Length);
            Assert.StartsWith("  1  " + ColorPresets.All[0].Name, lines[0]);
        }

        [Fact]
        public void TestWritingToAFile()
        {
            string path = Path.Combine(_dir, "mine.json");
            Assert.Equal(CliApp.ExitOk, Run("template", "-o", path).Code);
            Assert.Empty(DesignFileValidator.Validate(File.ReadAllText(path)).Issues);

            Assert.Equal(CliApp.ExitError, Run("template", "-o", path).Code); // already there
            Assert.Equal(CliApp.ExitOk, Run("template", "-o", path, "--preset", "2", "--force").Code);
            Assert.Contains(ColorPresets.All[1].Name, File.ReadAllText(path));

            Assert.Equal(CliApp.ExitOk, Run("template", "--favorites", "-o", _dir + Path.DirectorySeparatorChar).Code);
            Assert.True(File.Exists(Path.Combine(_dir, "template.irisfav.json")));

            // template writes; it reads nothing
            Assert.Equal(CliApp.ExitError, Run("template", "design.json").Code);
        }
    }
}
