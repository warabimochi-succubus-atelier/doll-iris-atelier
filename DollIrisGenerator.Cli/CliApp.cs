using System.Globalization;
using System.Reflection;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;

namespace DollIrisGenerator.Cli
{
    /// <summary>
    /// The commands of DollIrisCli (SPECIFICATION.md 23.3, stage 1): <c>validate</c> and <c>svg</c>. Output goes to
    /// the writers passed in, so tests can run it without a console.
    /// </summary>
    public static class CliApp
    {
        /// <summary>Done; for <c>validate</c>, no warnings.</summary>
        public const int ExitOk = 0;

        /// <summary><c>validate</c> found warnings: the files load, but not as written.</summary>
        public const int ExitWarnings = 1;

        /// <summary>A file could not be read or written, or the command line was wrong.</summary>
        public const int ExitError = 2;

        /// <summary>Same default as the A4 sheet's 「ペア内左右の間隔」, which 💾 SVG 保存 uses.</summary>
        public const double DefaultSpacingMm = 3.0;

        public const double MinSpacingMm = 1.0;
        public const double MaxSpacingMm = 50.0;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

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

                stdout.WriteLine(report.HasErrors ? $"{file}:"
                    : report.IsList ? $"{file}: お気に入りの一覧（デザイン {report.Designs.Count} 個）"
                    : $"{file}: デザイン{report.Designs[0].Label}");
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

        // ---- svg ----

        private static int Svg(string[] args, TextWriter stdout, TextWriter stderr)
        {
            string? input = null, output = null;
            bool guides = false, force = false;
            double spacing = DefaultSpacingMm;

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
                    case "--spacing":
                        string text = Value(args, ref i);
                        if (!double.TryParse(text, NumberStyles.Float, Inv, out spacing) || !double.IsFinite(spacing)
                            || spacing < MinSpacingMm || spacing > MaxSpacingMm)
                            throw new UsageException($"--spacing は {MinSpacingMm:0.#}〜{MaxSpacingMm:0.#} の数値（mm）で指定してください: {text}");
                        break;
                    case "--guides":
                        guides = true;
                        break;
                    case "--force":
                        force = true;
                        break;
                    default:
                        if (arg.StartsWith('-') && arg != "-") throw new UsageException($"svg に {arg} という指定はありません");
                        if (input != null) throw new UsageException("svg で指定できる入力ファイルは 1 つです");
                        input = arg;
                        break;
                }
            }
            if (input == null) throw new UsageException("入力ファイルを指定してください（例: DollIrisCli svg design.json）");

            var report = ReadReport(input, stderr);
            if (report == null) return ExitError;
            foreach (var issue in report.Issues.Where(i => i.Level != DesignIssueLevel.Note))
            {
                stderr.WriteLine($"{LevelLabel(issue.Level)} {(issue.Path.Length > 0 ? issue.Path + ": " : "")}{issue.Message}");
            }
            if (report.HasErrors) return ExitError;
            if (report.Designs.Count == 0)
            {
                stderr.WriteLine("エラー 書き出すデザインがありません");
                return ExitError;
            }

            string Render(IrisParameters p) =>
                SvgExporter.GeneratePairSvg(p, guides, guides, spacing, SvgExporter.PairPaddingMm);

            if (output == "-")
            {
                if (report.Designs.Count > 1)
                    throw new UsageException("お気に入りの一覧（デザインが 2 つ以上）は標準出力に書き出せません。-o でフォルダーを指定してください");
                stdout.Write(Render(report.Designs[0].Parameters));
                return ExitOk;
            }

            var targets = Targets(input, output, report);
            var existing = targets.Where(t => File.Exists(t.Path)).Select(t => t.Path).ToList();
            if (existing.Count > 0 && !force)
            {
                foreach (var path in existing) stderr.WriteLine($"エラー すでにあります: {path}");
                stderr.WriteLine("上書きするときは --force を付けてください（何も書き出していません）");
                return ExitError;
            }

            foreach (var (path, design) in targets)
            {
                try
                {
                    var dir = Path.GetDirectoryName(Path.GetFullPath(path));
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    AtomicFileWriter.WriteAllText(path, Render(design.Parameters));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
                {
                    stderr.WriteLine($"エラー 書き出せません: {path}（{ex.Message}）");
                    return ExitError;
                }
                stdout.WriteLine(path);
            }
            return ExitOk;
        }

        /// <summary>
        /// Where each design goes. A single design: <c>-o</c> as given (a folder gets <c>&lt;入力名&gt;.svg</c>), or the
        /// input's name with .svg. A favorites list: <c>&lt;入力名&gt;-01-&lt;デザイン名&gt;.svg</c> … in the <c>-o</c>
        /// folder, or next to the input.
        /// </summary>
        private static List<(string Path, ValidatedDesign Design)> Targets(string input, string? output, DesignFileReport report)
        {
            string stem = Path.GetFileNameWithoutExtension(input);
            if (stem.EndsWith(".irisfav", StringComparison.OrdinalIgnoreCase)) stem = stem[..^".irisfav".Length];
            string inputDir = Path.GetDirectoryName(input) ?? "";

            if (!report.IsList)
            {
                var design = report.Designs[0];
                if (output == null) return [(Path.Combine(inputDir, stem + ".svg"), design)];
                if (Directory.Exists(output) || EndsWithSeparator(output)) return [(Path.Combine(output, stem + ".svg"), design)];
                return [(output, design)];
            }

            if (output != null && File.Exists(output))
                throw new UsageException($"お気に入りの一覧は複数のファイルになるので、-o にはフォルダーを指定してください: {output}");
            string dir = output ?? inputDir;
            int digits = Math.Max(2, report.Designs.Count.ToString(Inv).Length);
            return report.Designs
                .Select((d, i) => (Path.Combine(dir, $"{stem}-{(i + 1).ToString("D" + digits, Inv)}-{SafeName(d.Name)}.svg"), d))
                .ToList();
        }

        private static bool EndsWithSeparator(string path) =>
            path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar);

        /// <summary>A design name usable in a file name on any OS: no path characters, not too long.</summary>
        internal static string SafeName(string? name)
        {
            var invalid = Path.GetInvalidFileNameChars().Concat(new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' }).ToHashSet();
            var chars = (name ?? "").Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c).ToArray();
            string safe = new string(chars).Trim().Trim('.');
            if (safe.Length > 40) safe = safe[..40];
            return safe.Length == 0 ? "design" : safe;
        }

        // ---- shared ----

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

        private static string Value(string[] args, ref int i)
        {
            if (i + 1 >= args.Length) throw new UsageException($"{args[i]} の後に値を書いてください");
            return args[++i];
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

            デザインファイル（JSON）を調べたり、SVG に書き出したりします。
            描画はアプリと同じ数式で行い、画像生成 AI は使いません。通信もしません。
            ファイルの書き方: docs/DESIGN_JSON_FORMAT.md

            使い方:
              DollIrisCli validate <ファイル>...
                  アプリで読み込んだときと同じように読み、書いたとおりにならない所を一覧にします
                  （範囲外の値、知らない項目名、読めない色など）。
                  終了コード: 0 = 問題なし、1 = 警告あり、2 = 読み込めない

              DollIrisCli svg <ファイル> [-o <出力先>] [--guides] [--spacing <mm>] [--force]
                  左右ペアの SVG を書き出します（アプリの「💾 SVG 保存」と同じ内容）。
                  -o, --output <出力先>  書き出すファイル。省略すると入力と同じ名前の .svg。
                                         - で標準出力。お気に入りの一覧はフォルダーを指定し、
                                         <入力名>-01-<デザイン名>.svg … の名前で書き出します
                  --guides               カットラインとセンターマークを入れる
                  --spacing <mm>         左右の目の間隔（既定 {DefaultSpacingMm:0.0}、{MinSpacingMm:0.#}〜{MaxSpacingMm:0.#}）
                  --force                すでにあるファイルを上書きする
                  終了コード: 0 = 書き出した、2 = 書き出せなかった

            入力ファイルに - を指定すると標準入力から読みます。

            """;

        private sealed class UsageException(string message) : Exception(message);
    }
}
