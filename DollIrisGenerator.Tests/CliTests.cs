using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using DollIrisGenerator.Cli;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;

namespace DollIrisGenerator.Tests
{
    /// <summary>DesignFileValidator and the DollIrisCli commands (SPECIFICATION.md 23).</summary>
    public class CliTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "DollIrisCliTests-" + Guid.NewGuid().ToString("N"));

        public CliTests() => Directory.CreateDirectory(_dir);

        public void Dispose()
        {
            try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
        }

        /// <summary>A full design as the app saves it, with <paramref name="edit"/> applied to its JSON.</summary>
        private static string Design(Action<JsonObject>? edit = null)
        {
            var node = JsonSerializer.SerializeToNode(new IrisParameters { Name = "テスト" })!.AsObject();
            // Saved items carry an Id; a hand-written file usually has none
            foreach (var list in new[] { "Symbols", "Highlights" })
                foreach (var item in node[list]!.AsArray()) item!.AsObject().Remove("Id");
            edit?.Invoke(node);
            return node.ToJsonString();
        }

        private static DesignIssue Single(DesignFileReport report, DesignIssueLevel level = DesignIssueLevel.Warning) =>
            Assert.Single(report.Issues, i => i.Level == level);

        private string WriteFile(string name, string text)
        {
            string path = Path.Combine(_dir, name);
            File.WriteAllText(path, text);
            return path;
        }

        private static (int Code, string Out, string Err) Run(params string[] args)
        {
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            int code = CliApp.Run(args, stdout, stderr);
            return (code, stdout.ToString(), stderr.ToString());
        }

        // ---- validator ----

        [Fact]
        public void TestSavedDesignHasNoIssues()
        {
            var report = DesignFileValidator.Validate(Design());
            Assert.Empty(report.Issues);
            Assert.False(report.IsList);
            Assert.Equal("テスト", Assert.Single(report.Designs).Name);
        }

        [Fact]
        public void TestOutOfRangeNumberIsReportedWithTheLoadedValue()
        {
            var issue = Single(DesignFileValidator.Validate(Design(d => d["SizeMm"] = 30)));
            Assert.Equal("SizeMm", issue.Path);
            Assert.Contains("30", issue.Message);
            Assert.Contains("20 として読み込まれます", issue.Message);
        }

        [Fact]
        public void TestUnknownNameSuggestsTheRightCase()
        {
            var report = DesignFileValidator.Validate(Design(d => d["raycount"] = 10));
            var issue = Single(report);
            Assert.Equal("raycount", issue.Path);
            Assert.Contains("「RayCount」", issue.Message);

            issue = Single(DesignFileValidator.Validate(Design(d => d["Sparkle"] = 1)));
            Assert.DoesNotContain("「", issue.Message);
        }

        [Fact]
        public void TestMissingFieldsAreANoteNotAWarning()
        {
            var report = DesignFileValidator.Validate("{ \"Name\": \"少し\", \"SizeMm\": 9 }");
            var note = Single(report, DesignIssueLevel.Note);
            Assert.Contains("RayCount", note.Message);
            Assert.Equal(0, report.WarningCount);
            Assert.Equal(9.0, report.Designs[0].Parameters.SizeMm);
        }

        [Fact]
        public void TestChoicesOutsideTheListAreReported()
        {
            // Blends fall back to 通常 when loaded; shapes keep the number, which is still not a choice
            var blend = Single(DesignFileValidator.Validate(Design(d => d["AuroraBlend"] = 7)));
            Assert.Contains("0〜4", blend.Message);
            Assert.Contains("0 として読み込まれます", blend.Message);

            var shape = Single(DesignFileValidator.Validate(Design(d => d["Shape"] = 9)));
            Assert.Equal("Shape", shape.Path);
            Assert.Contains("0〜2", shape.Message);
        }

        [Fact]
        public void TestColorsAreChecked()
        {
            var name = Single(DesignFileValidator.Validate(Design(d => d["RayColor"] = "blue")));
            Assert.Contains("色として読めません", name.Message);

            // Base colors take one color only; ray colors take a gradient
            string gradient = "linear-gradient(90deg, #FFFFFF 0%, #000000 100%)";
            var solidOnly = Single(DesignFileValidator.Validate(Design(d => d["BaseColor"] = gradient)));
            Assert.Contains("最初の色 #FFFFFF", solidOnly.Message);
            Assert.Empty(DesignFileValidator.Validate(Design(d => d["RayColor"] = gradient)).Issues);

            var tooMany = Single(DesignFileValidator.Validate(Design(d =>
                d["RayColor"] = "linear-gradient(90deg, #111111, #222222, #333333, #444444, #555555, #666666)")));
            Assert.Contains("5 個まで", tooMany.Message);

            // #RGB and #AARRGGBB are colors too
            Assert.Empty(DesignFileValidator.Validate(Design(d => { d["BaseColor"] = "#FFF"; d["RayColor"] = "#80FFFFFF"; })).Issues);
        }

        [Fact]
        public void TestSymbolsAndHighlightsAreCheckedItemByItem()
        {
            var issue = Single(DesignFileValidator.Validate(Design(d => d["Symbols"]![0]!["Size"] = 3)));
            Assert.Equal("Symbols[0].Size", issue.Path);

            var unknown = Single(DesignFileValidator.Validate(Design(d => d["Highlights"]![1]!["Twinkle"] = 1)));
            Assert.Equal("Highlights[1].Twinkle", unknown.Path);
        }

        [Fact]
        public void TestTooManyItemsAreReported()
        {
            var symbol = JsonSerializer.SerializeToNode(new DecorativeSymbolItem())!.AsObject();
            symbol.Remove("Id");
            var issue = Single(DesignFileValidator.Validate(Design(d =>
                d["Symbols"] = new JsonArray(Enumerable.Range(0, IrisParameters.MaxNestedItems + 2).Select(_ => (JsonNode)symbol.DeepClone()).ToArray()))));
            Assert.Equal("Symbols", issue.Path);
            Assert.Contains($"{IrisParameters.MaxNestedItems} 個まで", issue.Message);
        }

        [Fact]
        public void TestBrokenJsonIsAnError()
        {
            var syntax = DesignFileValidator.Validate("{ \"SizeMm\": 10, }");
            Assert.True(syntax.HasErrors);
            Assert.Contains("1 行目", Single(syntax, DesignIssueLevel.Error).Message);

            var type = DesignFileValidator.Validate(Design(d => d["SizeMm"] = "10"));
            Assert.Equal("SizeMm", Single(type, DesignIssueLevel.Error).Path);
            Assert.Empty(type.Designs);

            Assert.True(DesignFileValidator.Validate("42").HasErrors);
        }

        [Fact]
        public void TestFavoritesListIsCheckedPerEntry()
        {
            string design = Design(d => d["RayCount"] = 500);
            var report = DesignFileValidator.Validate($"[{{ \"Name\": \"案1\", \"Parameters\": {design} }}, {{ \"Name\": \"案2\" }}]");
            Assert.True(report.IsList);
            Assert.Equal(new[] { "案1", "案2" }, report.Designs.Select(d => d.Name));
            Assert.Contains(report.Issues, i => i.Path == "[0].Parameters.RayCount" && i.Level == DesignIssueLevel.Warning);
            Assert.Contains(report.Issues, i => i.Path == "[1].Parameters" && i.Message.Contains("既定のデザイン"));
            // Id / Note / CreatedAt are not expected in a hand-written list
            Assert.DoesNotContain(report.Issues, i => i.Path == "[0]");
        }

        // ---- validate command ----

        /// <summary>
        /// A name written twice: the app reads the last value without a word, so the check says so (as a warning) and
        /// goes on with that value. Not an exception (a JsonNode cannot hold a name twice).
        /// </summary>
        [Fact]
        public void TestNameWrittenTwiceIsAWarning_AndTheLastValueIsUsed()
        {
            string json = Design(d => d["RayCount"] = 12).Replace("\"RayCount\":12", "\"RayCount\":12,\"RayCount\":40");
            var report = DesignFileValidator.Validate(json);
            var issue = Single(report);
            Assert.Equal("RayCount", issue.Path);
            Assert.Contains("最後の値を使います", issue.Message);
            Assert.Equal(40, Assert.Single(report.Designs).Parameters.RayCount);
            Assert.Equal(40, FavoritesStore.ParseImportText(json, out _)![0].Parameters.RayCount); // as the app reads it

            // Three times is one warning; inside a symbol and in a favorites list, with the path
            json = "[{\"Name\": \"案\", \"Parameters\": {\"Name\": \"a\", \"Seed\": 1, \"Seed\": 2, \"Seed\": 3,"
                 + " \"Symbols\": [{\"Size\": 0.1, \"Size\": 0.2}]}}]";
            report = DesignFileValidator.Validate(json);
            Assert.Equal(new[] { "[0].Parameters.Seed", "[0].Parameters.Symbols[0].Size" },
                report.Issues.Where(i => i.Message.Contains("2 回以上")).Select(i => i.Path));
            var p = Assert.Single(report.Designs).Parameters;
            Assert.Equal(3, p.Seed);
            Assert.Equal(0.2, p.Symbols[0].Size);
        }

        [Fact]
        public void TestCommandsReadANameWrittenTwice()
        {
            string json = Design().Replace("\"Seed\":", "\"Seed\":5,\"Seed\":");
            var validate = Run("validate", WriteFile("twice.json", json));
            Assert.Equal(CliApp.ExitWarnings, validate.Code);
            Assert.Contains("警告 Seed:", validate.Out);

            string output = Path.Combine(_dir, "twice.svg");
            var svg = Run("svg", WriteFile("twice2.json", json), "-o", output);
            Assert.Equal(CliApp.ExitOk, svg.Code);
            Assert.True(File.Exists(output));
            Assert.Contains("最後の値を使います", svg.Err);
        }

        [Fact]
        public void TestValidateExitCodes()
        {
            var ok = Run("validate", WriteFile("ok.json", Design()));
            Assert.Equal(CliApp.ExitOk, ok.Code);
            Assert.Contains("問題はありません", ok.Out);

            var warn = Run("validate", WriteFile("warn.json", Design(d => d["SizeMm"] = 1)));
            Assert.Equal(CliApp.ExitWarnings, warn.Code);
            Assert.Contains("警告 SizeMm:", warn.Out);

            var missing = Run("validate", Path.Combine(_dir, "none.json"));
            Assert.Equal(CliApp.ExitError, missing.Code);
            Assert.Contains("ファイルがありません", missing.Err);

            // The worst file decides
            var both = Run("validate", WriteFile("a.json", Design()), WriteFile("b.json", "{"));
            Assert.Equal(CliApp.ExitError, both.Code);
            Assert.Contains("エラー 1 件", both.Out);
        }

        [Fact]
        public void TestCommandLineMistakesAreUsageErrors()
        {
            Assert.Equal(CliApp.ExitError, Run().Code);
            Assert.Equal(CliApp.ExitError, Run("paint").Code);
            Assert.Equal(CliApp.ExitError, Run("validate").Code);
            Assert.Equal(CliApp.ExitError, Run("svg", "a.json", "--spacing", "0.5").Code);
            Assert.Equal(CliApp.ExitError, Run("svg", "a.json", "--spacing").Code);
            Assert.Equal(CliApp.ExitError, Run("svg", "a.json", "--bogus").Code);
            Assert.Equal(CliApp.ExitOk, Run("--help").Code);
            Assert.Contains("画像生成 AI は使いません", Run("--help").Out);
            Assert.Equal(CliApp.ExitOk, Run("--version").Code);
        }

        // ---- svg command ----

        [Fact]
        public void TestSvgMatchesTheAppsSvgSave()
        {
            string input = WriteFile("sapphire.json", Design());
            var result = Run("svg", input);
            Assert.Equal(CliApp.ExitOk, result.Code);

            string written = File.ReadAllText(Path.Combine(_dir, "sapphire.svg"));
            var p = JsonSerializer.Deserialize<IrisParameters>(Design())!;
            // 💾 SVG 保存 with the sheet's default pair spacing and no cut lines
            Assert.Equal(SvgExporter.GeneratePairSvg(p, includePrintGuides: false, pairSpacingMm: new PrintSheetConfig().PairSpacingXMm), written);
            Assert.Equal(CliApp.DefaultSpacingMm, new PrintSheetConfig().PairSpacingXMm);
        }

        [Fact]
        public void TestSvgOptions()
        {
            string input = WriteFile("d.json", Design());
            var p = JsonSerializer.Deserialize<IrisParameters>(Design())!;

            var toStdout = Run("svg", input, "-o", "-", "--guides", "--spacing", "5");
            Assert.Equal(CliApp.ExitOk, toStdout.Code);
            Assert.Equal(SvgExporter.GeneratePairSvg(p, true, true, 5.0, SvgExporter.PairPaddingMm), toStdout.Out);

            string named = Path.Combine(_dir, "sub", "named.svg");
            Assert.Equal(CliApp.ExitOk, Run("svg", input, "-o", named).Code);
            Assert.True(File.Exists(named));
        }

        [Fact]
        public void TestSvgDoesNotOverwriteWithoutForce()
        {
            string input = WriteFile("d.json", Design());
            string output = WriteFile("d.svg", "keep");

            var refused = Run("svg", input);
            Assert.Equal(CliApp.ExitError, refused.Code);
            Assert.Contains("--force", refused.Err);
            Assert.Equal("keep", File.ReadAllText(output));

            Assert.Equal(CliApp.ExitOk, Run("svg", input, "--force").Code);
            Assert.StartsWith("<?xml", File.ReadAllText(output));
        }

        [Fact]
        public void TestSvgWritesOneFilePerFavorite()
        {
            string design = Design();
            string input = WriteFile("set.irisfav.json",
                $"[{{ \"Name\": \"青\", \"Parameters\": {design} }}, {{ \"Name\": \"星/空:2\", \"Parameters\": {design} }}]");
            string outDir = Path.Combine(_dir, "out");

            var result = Run("svg", input, "-o", outDir);
            Assert.Equal(CliApp.ExitOk, result.Code);
            Assert.Equal(new[] { "set-01-青.svg", "set-02-星_空_2.svg" },
                Directory.GetFiles(outDir).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal));

            // Several designs cannot go to one stream
            Assert.Equal(CliApp.ExitError, Run("svg", input, "-o", "-").Code);
        }

        [Fact]
        public void TestSvgStillWritesWithWarningsButNotWithErrors()
        {
            var warned = Run("svg", WriteFile("w.json", Design(d => d["SizeMm"] = 99)), "-o", "-");
            Assert.Equal(CliApp.ExitOk, warned.Code);
            Assert.Contains("警告 SizeMm:", warned.Err);
            Assert.Contains("<svg", warned.Out);

            var broken = Run("svg", WriteFile("b.json", "{ \"SizeMm\": "), "-o", "-");
            Assert.Equal(CliApp.ExitError, broken.Code);
            Assert.Equal("", broken.Out);
        }

        [Fact]
        public void TestSafeName()
        {
            Assert.Equal("a_b_c", CliApp.SafeName("a/b:c"));
            Assert.Equal("design", CliApp.SafeName("  ..  "));
            Assert.Equal(40, CliApp.SafeName(new string('x', 100)).Length);
        }
    }
}
