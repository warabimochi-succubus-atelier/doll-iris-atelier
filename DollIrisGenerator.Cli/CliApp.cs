using System.Globalization;
using System.IO;
using System.Reflection;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;

namespace DollIrisGenerator.Cli
{
    /// <summary>
    /// The commands of DollIrisCli (SPECIFICATION.md 23): <c>validate</c>, <c>svg</c>, <c>png</c> and <c>pdf</c>.
    /// Output goes to the writers passed in, so tests can run it without a console. The PNG and A4 PDF come from
    /// the same code as the desktop app (DollIrisGenerator.Rendering), so this must run on an STA thread.
    /// </summary>
    public static class CliApp
    {
        /// <summary>Done; for <c>validate</c>, no warnings.</summary>
        public const int ExitOk = 0;

        /// <summary><c>validate</c> found warnings: the files load, but not as written.</summary>
        public const int ExitWarnings = 1;

        /// <summary>A file could not be read or written, or the command line was wrong.</summary>
        public const int ExitError = 2;

        /// <summary>Same default as the A4 sheet's 「ペア内左右の間隔」, which 💾 SVG 保存 and the PNG buttons use.</summary>
        public const double DefaultSpacingMm = 3.0;

        public const double MinSpacingMm = 1.0;
        public const double MaxSpacingMm = 50.0;

        /// <summary>The 「1200 DPI」 button; the recommended resolution (SPECIFICATION.md 19).</summary>
        public const double DefaultDpi = 1200.0;

        public const double MinDpi = 72.0;
        public const double MaxDpi = 2400.0;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The <c>--template</c> names, with the app's template each one picks.</summary>
        public static readonly IReadOnlyDictionary<string, PdfTemplateType> TemplateNames =
            new Dictionary<string, PdfTemplateType>(StringComparer.OrdinalIgnoreCase)
            {
                ["standard"] = PdfTemplateType.Custom,
                ["1-3"] = PdfTemplateType.Scale1_3_Doll,
                ["1-4"] = PdfTemplateType.Scale1_4_Doll,
                ["1-6"] = PdfTemplateType.Scale1_6_Doll,
                ["punch"] = PdfTemplateType.CirclePunchGrid,
                ["trial"] = PdfTemplateType.ColorTrialSheet,
            };

        public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
        {
            if (args.Length == 0 || args[0] is "help" or "--help" or "-h" or "/?")
            {
                stdout.Write(HelpText);
                return args.Length == 0 ? ExitError : ExitOk;
            }
            if (args[0] is "--version" or "version")
            {
                stdout.WriteLine($"DollIrisCli {Version}");
                return ExitOk;
            }

            var rest = args.Skip(1).ToArray();
            try
            {
                return args[0] switch
                {
                    "validate" => Validate(rest, stdout, stderr),
                    "svg" => Svg(rest, stdout, stderr),
                    "png" => Png(rest, stdout, stderr),
                    "pdf" => Pdf(rest, stdout, stderr),
                    _ => Usage(stderr, $"知らないコマンドです: {args[0]}")
                };
            }
            catch (UsageException ex)
            {
                return Usage(stderr, ex.Message);
            }
        }

        // ---- validate ----

        private static int Validate(string[] args, TextWriter stdout, TextWriter stderr)
        {
            var files = new List<string>();
            foreach (var arg in args)
            {
                if (arg is "--help" or "-h") { stdout.Write(HelpText); return ExitOk; }
                if (arg.StartsWith('-') && arg != "-") throw new UsageException($"validate に {arg} という指定はありません");
                files.Add(arg);
            }
            if (files.Count == 0) throw new UsageException("調べるファイルを指定してください（例: DollIrisCli validate design.json）");

            int warnings = 0, notes = 0, errors = 0;
            foreach (var file in files)
            {
                var report = ReadReport(file, stderr);
                if (report == null)
                {
                    errors++;
                    continue;
                }

                stdout.WriteLine(report.HasErrors ? $"{file}:" : report.Kind switch
                {
                    DesignFileKind.Favorites => $"{file}: お気に入りの一覧（デザイン {report.Designs.Count} 個）",
                    DesignFileKind.Sheet => $"{file}: A4 シート（{report.Designs.Count} 行、計 {report.Designs.Sum(d => d.PairCount)} セット）",
                    _ => $"{file}: デザイン{report.Designs[0].Label}"
                });
                foreach (var issue in report.Issues)
                {
                    stdout.WriteLine($"  {LevelLabel(issue.Level)} {(issue.Path.Length > 0 ? issue.Path + ": " : "")}{issue.Message}");
                }
                if (report.Issues.Count == 0) stdout.WriteLine("  問題はありません");

                warnings += report.WarningCount;
                notes += report.Issues.Count(i => i.Level == DesignIssueLevel.Note);
                errors += report.Issues.Count(i => i.Level == DesignIssueLevel.Error);
            }

            stdout.WriteLine($"結果: エラー {errors} 件、警告 {warnings} 件、注意 {notes} 件");
            return errors > 0 ? ExitError : warnings > 0 ? ExitWarnings : ExitOk;
        }

        // ---- svg / png: one file per design ----

        private static int Svg(string[] args, TextWriter stdout, TextWriter stderr)
        {
            var o = PairOptions.Parse("svg", args, allowDpi: false);
            if (o == null) { stdout.Write(HelpText); return ExitOk; }

            return ExportPairs(o, ".svg", stdout, stderr,
                p => SvgExporter.GeneratePairSvg(p, o.Guides, o.Guides, o.SpacingMm, SvgExporter.PairPaddingMm),
                (path, text) => AtomicFileWriter.WriteAllText(path, text),
                text => stdout.Write(text));
        }

        private static int Png(string[] args, TextWriter stdout, TextWriter stderr)
        {
            var o = PairOptions.Parse("png", args, allowDpi: true);
            if (o == null) { stdout.Write(HelpText); return ExitOk; }
            if (o.Output == "-") throw new UsageException("PNG は標準出力に書き出せません。-o でファイルかフォルダーを指定してください");

            // Same call as the 🖼️ PNG buttons (DesignFileWorkflow.ExportPng)
            return ExportPairs(o, ".png", stdout, stderr,
                p => RasterExporter.SaveBitmapToPngBytes(RasterExporter.RenderPairToBitmap(
                    p, dpi: o.Dpi, pairSpacingMm: o.SpacingMm, showCutLines: o.Guides, showCenterMarks: o.Guides)),
                (path, bytes) => WriteAtomically(path, temp => File.WriteAllBytes(temp, bytes)),
                toStdout: null);
        }

        private sealed class PairOptions
        {
            public string Input = "";
            public string? Output;
            public bool Guides, Force;
            public double SpacingMm = DefaultSpacingMm;
            public double Dpi = DefaultDpi;

            /// <returns>Null when --help was asked for.</returns>
            public static PairOptions? Parse(string command, string[] args, bool allowDpi)
            {
                var o = new PairOptions();
                string? input = null;
                for (int i = 0; i < args.Length; i++)
                {
                    string arg = args[i];
                    switch (arg)
                    {
                        case "--help" or "-h":
                            return null;
                        case "-o" or "--output":
                            o.Output = Value(args, ref i);
                            break;
                        case "--spacing":
                            o.SpacingMm = Number(args, ref i, MinSpacingMm, MaxSpacingMm, "mm");
                            break;
                        case "--dpi" when allowDpi:
                            o.Dpi = Number(args, ref i, MinDpi, MaxDpi, "DPI");
                            break;
                        case "--guides":
                            o.Guides = true;
                            break;
                        case "--force":
                            o.Force = true;
                            break;
                        default:
                            input = Input(command, arg, input);
                            break;
                    }
                }
                o.Input = input ?? throw new UsageException($"入力ファイルを指定してください（例: DollIrisCli {command} design.json）");
                return o;
            }
        }

        private static int ExportPairs<T>(PairOptions o, string extension, TextWriter stdout, TextWriter stderr,
            Func<IrisParameters, T> render, Action<string, T> write, Action<T>? toStdout)
        {
            var report = ReadForExport(o.Input, stderr);
            if (report == null) return ExitError;

            if (o.Output == "-" && toStdout != null)
            {
                if (report.Designs.Count > 1)
                    throw new UsageException("デザインが 2 つ以上あるファイルは標準出力に書き出せません。-o でフォルダーを指定してください");
                toStdout(render(report.Designs[0].Parameters));
                return ExitOk;
            }

            var targets = Targets(o.Input, o.Output, report, extension);
            if (!CheckOverwrite(targets.Select(t => t.Path), o.Force, stderr)) return ExitError;

            foreach (var (path, design) in targets)
            {
                if (!TryWrite(path, p => write(p, render(design.Parameters)), stderr)) return ExitError;
                stdout.WriteLine(path);
            }
            return ExitOk;
        }

        /// <summary>
        /// Where each design goes. One design: <c>-o</c> as given (a folder gets <c>&lt;入力名&gt;.svg</c>), or the
        /// input's name with the extension. Several (favorites, A4 sheet): <c>&lt;入力名&gt;-01-&lt;デザイン名&gt;.svg</c> …
        /// in the <c>-o</c> folder, or next to the input.
        /// </summary>
        private static List<(string Path, ValidatedDesign Design)> Targets(string input, string? output, DesignFileReport report,
            string extension)
        {
            string stem = Stem(input);
            string inputDir = Path.GetDirectoryName(input) ?? "";

            if (!report.IsList)
                return [(SingleTarget(stem, inputDir, output, extension), report.Designs[0])];

            if (output != null && File.Exists(output))
                throw new UsageException($"デザインが複数あるので、-o にはフォルダーを指定してください: {output}");
            string dir = output ?? inputDir;
            int digits = Math.Max(2, report.Designs.Count.ToString(Inv).Length);
            return report.Designs
                .Select((d, i) => (Path.Combine(dir, $"{stem}-{(i + 1).ToString("D" + digits, Inv)}-{SafeName(d.Name)}{extension}"), d))
                .ToList();
        }

        // ---- pdf: one A4 sheet for the whole file ----

        private static int Pdf(string[] args, TextWriter stdout, TextWriter stderr)
        {
            string? input = null, output = null, templateName = null;
            int? pairs = null;
            bool fill = false, force = false, allowOverflow = false;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                switch (arg)
                {
                    case "--help" or "-h":
                        stdout.Write(HelpText);
                        return ExitOk;
                    case "-o" or "--output":
                        output = Value(args, ref i);
                        break;
                    case "--template":
                        templateName = Value(args, ref i);
                        if (!TemplateNames.ContainsKey(templateName) && !Enum.TryParse<PdfTemplateType>(templateName, true, out _))
                            throw new UsageException($"知らないテンプレートです: {templateName}（{string.Join(" / ", TemplateNames.Keys)}）");
                        break;
                    case "--pairs":
                        pairs = (int)Number(args, ref i, 1, SheetItem.MaxPairCount, "セット", integer: true);
                        break;
                    case "--fill":
                        fill = true;
                        break;
                    case "--allow-overflow":
                        allowOverflow = true;
                        break;
                    case "--force":
                        force = true;
                        break;
                    default:
                        input = Input("pdf", arg, input);
                        break;
                }
            }
            if (input == null) throw new UsageException("入力ファイルを指定してください（例: DollIrisCli pdf favorites.irisfav.json）");
            if (output == "-") throw new UsageException("PDF は標準出力に書き出せません。-o でファイルかフォルダーを指定してください");

            var report = ReadForExport(input, stderr);
            if (report == null) return ExitError;

            // The sheet: an A4 sheet file as saved, or each design once (or --pairs times) like 🖨️ A4シートに追加
            PrintSheetConfig config;
            List<SheetItem> items;
            if (report.Kind == DesignFileKind.Sheet)
            {
                if (pairs != null) throw new UsageException("A4 シートのファイルには --pairs を使えません（数量はファイルの PairCount で決まります）");
                config = report.Sheet!.Config!;
                items = report.Designs.Select(d => new SheetItem(d.Parameters, d.PairCount)).ToList();
            }
            else
            {
                config = new PrintSheetConfig();
                items = AddToSheet(report.Designs.Select(d => d.Parameters), pairs ?? 1);
            }
            if (templateName != null) Template(templateName).ApplyTo(config);
            if (fill) Fill(items, config);

            var layout = new SheetLayout(items, config);
            if (!layout.FitsOnPage)
            {
                string size = $"配置の大きさ {layout.GridWidthMm:F1} × {layout.GridHeightMm:F1} mm、印刷範囲 {layout.UsableWidthMm:F1} × {layout.UsableHeightMm:F1} mm";
                if (!allowOverflow)
                {
                    stderr.WriteLine($"エラー 配置が用紙の印刷範囲に収まりません（{size}）。虹彩サイズや列数・行数・余白を見直すか、--allow-overflow を付けてください（何も書き出していません）");
                    return ExitError;
                }
                stderr.WriteLine($"警告 配置が用紙の印刷範囲に収まりません（{size}）。はみ出した部分は印刷されません");
            }

            string path = SingleTarget(Stem(input), Path.GetDirectoryName(input) ?? "", output, ".pdf");
            if (!CheckOverwrite([path], force, stderr)) return ExitError;
            if (!TryWrite(path, temp => PdfExporter.ExportA4SheetToPdf(temp, items, config), stderr)) return ExitError;

            stdout.WriteLine(path);
            stdout.WriteLine($"  {layout.PageCount} ページ、計 {items.Sum(i => i.PairCount)} セット（{layout.Columns} 列 × {layout.Rows} 行）。" +
                             "印刷時はプリンター設定で「実際のサイズ (100%)」を選んでください");
            return ExitOk;
        }

        /// <summary>
        /// Like 🖨️ 選択したお気に入りをA4シートに追加: one row per design; the same design again raises that row's
        /// quantity instead (up to <see cref="SheetItem.MaxPairCount"/>).
        /// </summary>
        internal static List<SheetItem> AddToSheet(IEnumerable<IrisParameters> designs, int pairsEach)
        {
            var items = new List<SheetItem>();
            var keys = new List<string>();
            foreach (var p in designs)
            {
                string key = DesignKey(p);
                int at = keys.IndexOf(key);
                if (at < 0)
                {
                    items.Add(new SheetItem(p.Clone(), pairsEach));
                    keys.Add(key);
                }
                else
                {
                    items[at].PairCount = Math.Min(SheetItem.MaxPairCount, items[at].PairCount + pairsEach);
                }
            }
            return items;
        }

        /// <summary>
        /// The design as saved, without the symbols' and highlights' Id: a file that has no Ids gets new ones on
        /// every load, so two copies of the same design would otherwise never match.
        /// </summary>
        private static string DesignKey(IrisParameters p)
        {
            var node = System.Text.Json.JsonSerializer.SerializeToNode(p)!.AsObject();
            foreach (var list in new[] { nameof(IrisParameters.Symbols), nameof(IrisParameters.Highlights) })
            {
                if (node[list] is System.Text.Json.Nodes.JsonArray items)
                    foreach (var item in items) (item as System.Text.Json.Nodes.JsonObject)?.Remove("Id");
            }
            return node.ToJsonString();
        }

        /// <summary>Like ⚡ A4を満杯に自動配置: the last page's free slots are shared in proportion to the quantities.</summary>
        internal static void Fill(List<SheetItem> items, PrintSheetConfig config)
        {
            var layout = new SheetLayout(items, config);
            var counts = items.Select(i => i.PairCount).ToList();
            int target = SheetFill.TargetPairs(counts.Sum(), layout.PairsPerPage);
            var filled = SheetFill.Distribute(counts, target, SheetItem.MaxPairCount);
            for (int i = 0; i < filled.Length; i++) items[i].PairCount = filled[i];
        }

        private static PdfTemplatePreset Template(string name)
        {
            var type = TemplateNames.TryGetValue(name, out var t) ? t : Enum.Parse<PdfTemplateType>(name, true);
            return PdfTemplatePreset.All.First(p => p.Type == type);
        }

        // ---- shared ----

        /// <summary>A design name usable in a file name on any OS: no path characters, not too long.</summary>
        internal static string SafeName(string? name)
        {
            var invalid = Path.GetInvalidFileNameChars().Concat(new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' }).ToHashSet();
            var chars = (name ?? "").Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c).ToArray();
            string safe = new string(chars).Trim().Trim('.');
            if (safe.Length > 40) safe = safe[..40];
            return safe.Length == 0 ? "design" : safe;
        }

        private static string Stem(string input)
        {
            string stem = input == "-" ? "design" : Path.GetFileNameWithoutExtension(input);
            return stem.EndsWith(".irisfav", StringComparison.OrdinalIgnoreCase) ? stem[..^".irisfav".Length] : stem;
        }

        /// <summary>One output file: <c>-o</c> as given, inside it when it is a folder, or the input's name.</summary>
        private static string SingleTarget(string stem, string inputDir, string? output, string extension)
        {
            if (output == null) return Path.Combine(inputDir, stem + extension);
            if (Directory.Exists(output) || EndsWithSeparator(output)) return Path.Combine(output, stem + extension);
            return output;
        }

        private static bool EndsWithSeparator(string path) =>
            path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar);

        /// <returns>False (and nothing written) when a file is there already and <paramref name="force"/> is off.</returns>
        private static bool CheckOverwrite(IEnumerable<string> paths, bool force, TextWriter stderr)
        {
            var existing = paths.Where(File.Exists).ToList();
            if (existing.Count == 0 || force) return true;
            foreach (var path in existing) stderr.WriteLine($"エラー すでにあります: {path}");
            stderr.WriteLine("上書きするときは --force を付けてください（何も書き出していません）");
            return false;
        }

        /// <summary>Makes the folder and writes the file; reports a failure instead of throwing.</summary>
        private static bool TryWrite(string path, Action<string> writeTo, TextWriter stderr)
        {
            try
            {
                var dir = Path.GetDirectoryName(Path.GetFullPath(path));
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                WriteAtomically(path, writeTo);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
            {
                stderr.WriteLine($"エラー 書き出せません: {path}（{ex.Message}）");
                return false;
            }
        }

        /// <summary>
        /// Writes to a temporary file next to <paramref name="path"/>, then moves it over, so a failure half-way
        /// never leaves a broken file (or a broken earlier version) behind.
        /// </summary>
        private static void WriteAtomically(string path, Action<string> writeTo)
        {
            string temp = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)) ?? "", $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
            try
            {
                writeTo(temp);
                File.Move(temp, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        /// <summary>Reads the file and prints its warnings and errors; null when there is nothing to export.</summary>
        private static DesignFileReport? ReadForExport(string input, TextWriter stderr)
        {
            var report = ReadReport(input, stderr);
            if (report == null) return null;
            foreach (var issue in report.Issues.Where(i => i.Level != DesignIssueLevel.Note))
            {
                stderr.WriteLine($"{LevelLabel(issue.Level)} {(issue.Path.Length > 0 ? issue.Path + ": " : "")}{issue.Message}");
            }
            if (report.HasErrors) return null;
            if (report.Designs.Count == 0)
            {
                stderr.WriteLine("エラー 書き出すデザインがありません");
                return null;
            }
            return report;
        }

        /// <returns>Null when the file could not be read (the reason is written to <paramref name="stderr"/>).</returns>
        private static DesignFileReport? ReadReport(string file, TextWriter stderr)
        {
            string json;
            try
            {
                json = file == "-" ? Console.In.ReadToEnd() : SafeFileReader.ReadAllText(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or InvalidDataException)
            {
                stderr.WriteLine($"エラー 読み込めません: {file}（{(ex is FileNotFoundException or DirectoryNotFoundException ? "ファイルがありません" : ex.Message)}）");
                return null;
            }
            return DesignFileValidator.Validate(json);
        }

        private static string LevelLabel(DesignIssueLevel level) => level switch
        {
            DesignIssueLevel.Error => "エラー",
            DesignIssueLevel.Warning => "警告",
            _ => "注意"
        };

        private static string Input(string command, string arg, string? input)
        {
            if (arg.StartsWith('-') && arg != "-") throw new UsageException($"{command} に {arg} という指定はありません");
            if (input != null) throw new UsageException($"{command} で指定できる入力ファイルは 1 つです");
            return arg;
        }

        private static string Value(string[] args, ref int i)
        {
            if (i + 1 >= args.Length) throw new UsageException($"{args[i]} の後に値を書いてください");
            return args[++i];
        }

        private static double Number(string[] args, ref int i, double min, double max, string unit, bool integer = false)
        {
            string name = args[i];
            string text = Value(args, ref i);
            if (!double.TryParse(text, NumberStyles.Float, Inv, out double value) || !double.IsFinite(value)
                || value < min || value > max || (integer && value != Math.Floor(value)))
                throw new UsageException($"{name} は {min.ToString("0.#", Inv)}〜{max.ToString("0.#", Inv)} の{(integer ? "整数" : "数値")}（{unit}）で指定してください: {text}");
            return value;
        }

        private static int Usage(TextWriter stderr, string message)
        {
            stderr.WriteLine($"エラー {message}");
            stderr.WriteLine("使い方は DollIrisCli --help で表示できます");
            return ExitError;
        }

        public static string Version =>
            typeof(CliApp).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
            ?? typeof(CliApp).Assembly.GetName().Version?.ToString(3) ?? "";

        public static string HelpText => $"""
            Doll Iris Atelier コマンドライン {Version}

            デザインファイル（JSON）を調べたり、SVG・PNG・A4 PDF に書き出したりします。
            描画はアプリと同じ数式・同じ処理で行い、画像生成 AI は使いません。通信もしません。
            ファイルの書き方: docs/DESIGN_JSON_FORMAT.md

            読めるファイル: デザイン 1 つ（*.json）、お気に入りの一覧（*.irisfav.json）、
                            A4 シート（アプリが保存する print-sheet.json と同じ形）

            使い方:
              DollIrisCli validate <ファイル>...
                  アプリで読み込んだときと同じように読み、書いたとおりにならない所を一覧にします
                  （範囲外の値、知らない項目名、読めない色など）。
                  終了コード: 0 = 問題なし、1 = 警告あり、2 = 読み込めない

              DollIrisCli svg <ファイル> [-o <出力先>] [--guides] [--spacing <mm>] [--force]
                  左右ペアの SVG を書き出します（アプリの「💾 SVG 保存」と同じ内容）。
              DollIrisCli png <ファイル> [-o <出力先>] [--dpi <DPI>] [--guides] [--spacing <mm>] [--force]
                  左右ペアの透過 PNG を書き出します（アプリの「🖼️ PNG」ボタンと同じ内容）。
                  -o, --output <出力先>  書き出すファイル。省略すると入力と同じ名前（.svg / .png）。
                                         svg は - で標準出力。デザインが複数あるファイルはフォルダーを
                                         指定し、<入力名>-01-<デザイン名>.svg … の名前で書き出します
                  --dpi <DPI>            PNG の解像度（既定 {DefaultDpi:0}、{MinDpi:0}〜{MaxDpi:0}）
                  --guides               カットラインとセンターマークを入れる
                  --spacing <mm>         左右の目の間隔（既定 {DefaultSpacingMm:0.0}、{MinSpacingMm:0.#}〜{MaxSpacingMm:0.#}）

              DollIrisCli pdf <ファイル> [-o <出力先>] [--template <名前>] [--pairs <数>] [--fill]
                              [--allow-overflow] [--force]
                  A4 の印刷用 PDF を書き出します（アプリの「📄 A4 PDF 出力」と同じ内容）。
                  A4 シートのファイルは、その設定と数量のまま。デザインやお気に入りは、それぞれを
                  1 セットずつ（同じデザインは数量を足して）既定の設定のシートに並べます。
                  --template <名前>      テンプレート: standard（標準）/ 1-3 / 1-4 / 1-6（ドール向け）/
                                         punch（クラフトパンチ）/ trial（カラー見本）
                  --pairs <数>           デザインごとのセット数（既定 1、1〜{SheetItem.MaxPairCount}。A4 シートのファイルには使えない）
                  --fill                 最後のページの空きを、数量の比率で埋める（⚡ A4を満杯に自動配置）
                  --allow-overflow       用紙からはみ出す配置でも書き出す（既定は書き出さない）

              共通:
                  --force                すでにあるファイルを上書きする
                  終了コード: 0 = 書き出した、2 = 書き出せなかった

            入力ファイルに - を指定すると標準入力から読みます。

            """;

        private sealed class UsageException(string message) : Exception(message);
    }
}
