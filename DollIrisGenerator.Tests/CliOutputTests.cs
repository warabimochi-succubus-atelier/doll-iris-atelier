using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using DollIrisGenerator.Cli;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;

namespace DollIrisGenerator.Tests
{
    /// <summary>
    /// DollIrisCli stage 2 (SPECIFICATION.md 23.7): PNG and A4 PDF from the same code as the app, and A4 sheet files.
    /// </summary>
    public class CliOutputTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "DollIrisCliOutputTests-" + Guid.NewGuid().ToString("N"));

        public CliOutputTests() => Directory.CreateDirectory(_dir);

        public void Dispose()
        {
            try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
        }

        private static IrisParameters Design(string name = "テスト", double sizeMm = 10.0) =>
            new() { Name = name, SizeMm = sizeMm, Seed = 777 };

        private static string Json(object value) => JsonSerializer.Serialize(value);

        private string WriteFile(string name, string text)
        {
            string path = Path.Combine(_dir, name);
            File.WriteAllText(path, text);
            return path;
        }

        /// <summary>Runs the CLI on an STA thread, as Program.Main does (the PDF labels are drawn with WPF).</summary>
        private static (int Code, string Out, string Err) Run(params string[] args)
        {
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            int code = 0;
            StaRunner.Run(() => code = CliApp.Run(args, stdout, stderr));
            return (code, stdout.ToString(), stderr.ToString());
        }

        /// <summary>A4 sheet file as the app saves it (print-sheet.json).</summary>
        private static string Sheet(PrintSheetConfig config, params (IrisParameters Design, int Pairs)[] rows) =>
            Json(new PrintSheetState
            {
                Config = config,
                Items = rows.Select(r => new PrintSheetItemState { Parameters = r.Design, PairCount = r.Pairs }).ToList()
            });

        // ---- png ----

        [Fact]
        public void TestPngMatchesTheAppsPngButton()
        {
            var p = Design();
            string input = WriteFile("d.json", Json(p));
            var result = Run("png", input);
            Assert.Equal(CliApp.ExitOk, result.Code);

            byte[] expected = null!;
            StaRunner.Run(() => expected = RasterExporter.SaveBitmapToPngBytes(RasterExporter.RenderPairToBitmap(
                p, dpi: 1200.0, pairSpacingMm: new PrintSheetConfig().PairSpacingXMm, showCutLines: false, showCenterMarks: false)));
            Assert.Equal(expected, File.ReadAllBytes(Path.Combine(_dir, "d.png")));
        }

        [Fact]
        public void TestPngOptions()
        {
            var p = Design();
            string input = WriteFile("d.json", Json(p));
            string output = Path.Combine(_dir, "d600.png");
            Assert.Equal(CliApp.ExitOk, Run("png", input, "--dpi", "600", "--guides", "--spacing", "5", "-o", output).Code);

            byte[] expected = null!;
            StaRunner.Run(() => expected = RasterExporter.SaveBitmapToPngBytes(
                RasterExporter.RenderPairToBitmap(p, dpi: 600.0, pairSpacingMm: 5.0, showCutLines: true, showCenterMarks: true)));
            Assert.Equal(expected, File.ReadAllBytes(output));

            Assert.Equal(CliApp.ExitError, Run("png", input, "--dpi", "5000").Code);
            Assert.Equal(CliApp.ExitError, Run("png", input, "-o", "-").Code);
        }

        [Fact]
        public void TestPngWritesOneFilePerFavorite()
        {
            string input = WriteFile("set.irisfav.json",
                $"[{{ \"Name\": \"青\", \"Parameters\": {Json(Design())} }}, {{ \"Name\": \"赤\", \"Parameters\": {Json(Design())} }}]");
            Assert.Equal(CliApp.ExitOk, Run("png", input, "--dpi", "150", "-o", Path.Combine(_dir, "out")).Code);
            Assert.Equal(new[] { "set-01-青.png", "set-02-赤.png" },
                Directory.GetFiles(Path.Combine(_dir, "out")).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal));
        }

        // ---- pdf ----

        private static byte[] AppPdf(IReadOnlyList<SheetItem> items, PrintSheetConfig config, string dir)
        {
            string path = Path.Combine(dir, $"expected-{Guid.NewGuid():N}.pdf");
            StaRunner.Run(() => PdfExporter.ExportA4SheetToPdf(path, items, config));
            return File.ReadAllBytes(path);
        }

        [Fact]
        public void TestPdfOfADesignMatchesTheApp()
        {
            var p = Design();
            string input = WriteFile("d.json", Json(p));
            var result = Run("pdf", input);
            Assert.Equal(CliApp.ExitOk, result.Code);
            Assert.Contains("1 ページ、計 1 セット", result.Out);

            // An empty sheet in the app prints one pair of the current design with the default settings
            Assert.Equal(AppPdf([new SheetItem(p, 1)], new PrintSheetConfig(), _dir), File.ReadAllBytes(Path.Combine(_dir, "d.pdf")));
        }

        [Fact]
        public void TestPdfOfASheetFileUsesItsSettingsAndQuantities()
        {
            var config = new PrintSheetConfig { ShowLabels = false, ShowCutLines = true };
            var a = Design("A");
            var b = Design("B", 8.0);
            string input = WriteFile("print-sheet.json", Sheet(config, (a, 3), (b, 2)));

            var result = Run("pdf", input, "-o", Path.Combine(_dir, "sheet.pdf"));
            Assert.Equal(CliApp.ExitOk, result.Code);
            Assert.Contains("計 5 セット", result.Out);
            Assert.Equal(AppPdf([new SheetItem(a, 3), new SheetItem(b, 2)], config, _dir), File.ReadAllBytes(Path.Combine(_dir, "sheet.pdf")));

            // The quantities come from the file
            Assert.Equal(CliApp.ExitError, Run("pdf", input, "--pairs", "2", "--force").Code);
        }

        [Fact]
        public void TestPdfTemplatePairsAndFill()
        {
            var config = new PrintSheetConfig();
            PdfTemplatePreset.All.First(t => t.Type == PdfTemplateType.Scale1_4_Doll).ApplyTo(config);
            var p = Design();
            string input = WriteFile("d.json", Json(p));

            var result = Run("pdf", input, "--template", "1-4", "--pairs", "3");
            Assert.Equal(CliApp.ExitOk, result.Code);
            Assert.Equal(AppPdf([new SheetItem(p, 3)], config, _dir), File.ReadAllBytes(Path.Combine(_dir, "d.pdf")));

            // 1/4 template: 5 columns × 10 rows
            var filled = Run("pdf", input, "--template", "1-4", "--fill", "--force");
            Assert.Equal(CliApp.ExitOk, filled.Code);
            Assert.Contains("計 50 セット（5 列 × 10 行）", filled.Out);

            Assert.Equal(CliApp.ExitError, Run("pdf", input, "--template", "huge").Code);
            Assert.Equal(CliApp.ExitError, Run("pdf", input, "--pairs", "0").Code);
            Assert.Equal(CliApp.ExitError, Run("pdf", input, "--pairs", "1.5").Code);
        }

        [Fact]
        public void TestPdfRefusesASheetThatRunsOffThePage()
        {
            // 8 pairs of 20 mm irises in the 8 manual columns of the 1/6 template do not fit across A4
            // (with 1 pair only one column is used, which fits)
            string input = WriteFile("big.json", Json(Design(sizeMm: 20.0)));
            Assert.Equal(CliApp.ExitOk, Run("pdf", input, "--template", "1-6", "-o", Path.Combine(_dir, "one.pdf")).Code);
            var refused = Run("pdf", input, "--template", "1-6", "--pairs", "8");
            Assert.Equal(CliApp.ExitError, refused.Code);
            Assert.Contains("--allow-overflow", refused.Err);
            Assert.False(File.Exists(Path.Combine(_dir, "big.pdf")));

            var allowed = Run("pdf", input, "--template", "1-6", "--pairs", "8", "--allow-overflow");
            Assert.Equal(CliApp.ExitOk, allowed.Code);
            Assert.Contains("警告 配置が用紙", allowed.Err);
        }

        [Fact]
        public void TestPdfDoesNotOverwriteWithoutForce()
        {
            string input = WriteFile("d.json", Json(Design()));
            string output = WriteFile("d.pdf", "keep");
            Assert.Equal(CliApp.ExitError, Run("pdf", input).Code);
            Assert.Equal("keep", File.ReadAllText(output));
            Assert.Equal(CliApp.ExitOk, Run("pdf", input, "--force").Code);
            Assert.StartsWith("%PDF", File.ReadAllText(output));
        }

        [Fact]
        public void TestSameDesignTwiceRaisesItsQuantity()
        {
            // Like 🖨️ 選択したお気に入りをA4シートに追加
            var items = CliApp.AddToSheet([Design("A"), Design("B"), Design("A")], 2);
            Assert.Equal(new[] { 4, 2 }, items.Select(i => i.PairCount));
            Assert.Equal(SheetItem.MaxPairCount, CliApp.AddToSheet([Design(), Design()], SheetItem.MaxPairCount).Single().PairCount);
        }

        [Fact]
        public void TestFillSharesTheFreeSlotsByQuantity()
        {
            var config = new PrintSheetConfig();
            var items = new List<SheetItem> { new(Design("A"), 2), new(Design("B"), 1) };
            int perPage = new SheetLayout(items, config).PairsPerPage;
            CliApp.Fill(items, config);
            Assert.Equal(perPage, items.Sum(i => i.PairCount));
            Assert.True(items[0].PairCount > items[1].PairCount);
        }

        // ---- A4 sheet files in validate / svg ----

        [Fact]
        public void TestSheetFileIsValidated()
        {
            var node = JsonNode.Parse(Sheet(new PrintSheetConfig(), (Design("A"), 2), (Design("B"), 1)))!.AsObject();
            node["Config"]!["PageMarginLeftMm"] = 100;
            node["Items"]![0]!["PairCount"] = 500;
            node["Items"]![1]!["Parameters"]!["SizeMm"] = 1;
            node["Items"]![1]!["Qty"] = 1;
            node["Extra"] = true;

            var report = DesignFileValidator.Validate(node.ToJsonString());
            Assert.Equal(DesignFileKind.Sheet, report.Kind);
            Assert.Equal(new[] { 100, 1 }, report.Designs.Select(d => d.PairCount));
            string[] warned = report.Issues.Where(i => i.Level == DesignIssueLevel.Warning).Select(i => i.Path).ToArray();
            Assert.Equal(new[] { "Extra", "Config.PageMarginLeftMm", "Items[0].PairCount", "Items[1].Qty", "Items[1].Parameters.SizeMm" }, warned);

            // A sheet saved by the app has nothing to report
            Assert.Empty(DesignFileValidator.Validate(Sheet(new PrintSheetConfig(), (Design(), 1))).Issues);
        }

        [Fact]
        public void TestSheetFileWithANameWrittenTwice()
        {
            string json = JsonNode.Parse(Sheet(new PrintSheetConfig(), (Design("A"), 2)))!.ToJsonString()
                .Replace("\"Config\":{", "\"Config\":{\"PageMarginLeftMm\":99,");
            var report = DesignFileValidator.Validate(json);
            Assert.Equal(DesignFileKind.Sheet, report.Kind);
            var issue = Assert.Single(report.Issues, i => i.Level == DesignIssueLevel.Warning);
            Assert.Equal("Config.PageMarginLeftMm", issue.Path);
            Assert.Contains("最後の値を使います", issue.Message);
            Assert.Equal(new PrintSheetConfig().PageMarginLeftMm, report.Sheet!.Config.PageMarginLeftMm);
        }

        [Fact]
        public void TestValidateAndSvgReadSheetFiles()
        {
            string input = WriteFile("print-sheet.json", Sheet(new PrintSheetConfig(), (Design("A"), 2), (Design("B"), 1)));
            var validated = Run("validate", input);
            Assert.Equal(CliApp.ExitOk, validated.Code);
            Assert.Contains("A4 シート（2 行、計 3 セット）", validated.Out);

            Assert.Equal(CliApp.ExitOk, Run("svg", input, "-o", Path.Combine(_dir, "svg")).Code);
            Assert.Equal(new[] { "print-sheet-01-A.svg", "print-sheet-02-B.svg" },
                Directory.GetFiles(Path.Combine(_dir, "svg")).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal));
        }
    }
}
