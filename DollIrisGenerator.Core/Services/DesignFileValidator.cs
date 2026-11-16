using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using DollIrisGenerator.Models;

namespace DollIrisGenerator.Services
{
    public enum DesignIssueLevel
    {
        /// <summary>Worth knowing, but the file loads as written (e.g. fields left out fall back to defaults).</summary>
        Note,

        /// <summary>The file loads, but not as written: a value is pulled into range, a name is ignored, a color is unreadable.</summary>
        Warning,

        /// <summary>The file cannot be loaded at all.</summary>
        Error
    }

    /// <param name="Path">Where in the file, e.g. <c>Symbols[0].Size</c> or <c>[1].Parameters.RayCount</c>. Empty for the whole file.</param>
    public sealed record DesignIssue(DesignIssueLevel Level, string Path, string Message);

    /// <param name="Name">The name the app shows: the favorite's name in a list, the design's own name otherwise.</param>
    /// <param name="Label">How the report names the design: its name, and its position in a favorites list.</param>
    /// <param name="Path">Where the design is in the file (empty for a single design, <c>[1].Parameters</c> in a list).</param>
    public sealed record ValidatedDesign(string Name, string Label, string Path, IrisParameters Parameters);

    public sealed class DesignFileReport
    {
        public DesignFileReport(IReadOnlyList<ValidatedDesign> designs, IReadOnlyList<DesignIssue> issues, bool isList)
        {
            Designs = designs;
            Issues = issues;
            IsList = isList;
        }

        /// <summary>The designs as the app loads them (empty when the file has an error).</summary>
        public IReadOnlyList<ValidatedDesign> Designs { get; }

        public IReadOnlyList<DesignIssue> Issues { get; }

        /// <summary>True for a favorites list (<c>[ ... ]</c>), false for a single design (<c>{ ... }</c>).</summary>
        public bool IsList { get; }

        public bool HasErrors => Issues.Any(i => i.Level == DesignIssueLevel.Error);

        public int WarningCount => Issues.Count(i => i.Level == DesignIssueLevel.Warning);
    }

    /// <summary>
    /// Checks a design file (a single design, or a favorites list) the way 📥 インポート reads it, and says where the
    /// loaded design differs from what the file says: values pulled into range, unknown names, unreadable colors.
    /// The ranges are not listed here: the file is loaded with the app's own model and compared with what was
    /// written, so the check always matches what the app does.
    /// </summary>
    public static class DesignFileValidator
    {
        /// <summary>
        /// Color fields that take a gradient (the ones with a gradient option in the color boxes). The other
        /// <c>…Color</c> fields use the first color of a gradient.
        /// </summary>
        private static readonly HashSet<string> GradientColorNames = new(StringComparer.Ordinal)
        {
            nameof(IrisParameters.AuroraColor),
            nameof(IrisParameters.NebulaColor),
            nameof(IrisParameters.PupilColor),
            nameof(IrisParameters.RayColor),
            nameof(IrisParameters.LimbalRingColor),
            nameof(IrisParameters.ParticleColor),
            nameof(IrisParameters.LensRingColor),
            nameof(IrisParameters.OddEyeRayColor),
            nameof(DecorativeSymbolItem.Color), // symbols and highlights: their only color, "Color"
        };

        // Symbols and highlights get a new Id when the file has none, so a missing Id is not worth a note
        private static readonly HashSet<string> ItemNamesNotExpected = new(StringComparer.Ordinal) { "Id" };

        // A favorites list entry needs only Parameters; the app fills in the rest
        private static readonly HashSet<string> FavoriteNamesNotExpected = new(StringComparer.Ordinal)
        {
            nameof(FavoriteItem.Id), nameof(FavoriteItem.Name), nameof(FavoriteItem.Note), nameof(FavoriteItem.CreatedAt)
        };

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static DesignFileReport Validate(string json)
        {
            var issues = new List<DesignIssue>();
            var designs = new List<ValidatedDesign>();

            JsonNode? root;
            try
            {
                root = JsonNode.Parse(json);
            }
            catch (JsonException ex)
            {
                issues.Add(new DesignIssue(DesignIssueLevel.Error, "",
                    $"JSON として読めません（{Where(ex)}）。コメント、末尾のカンマ、閉じ忘れの括弧や引用符がないか確認してください"));
                return new DesignFileReport(designs, issues, false);
            }

            if (root is not JsonObject && root is not JsonArray)
            {
                issues.Add(new DesignIssue(DesignIssueLevel.Error, "",
                    "デザイン（{ ... }）か、お気に入りの一覧（[ ... ]）を書いてください"));
                return new DesignFileReport(designs, issues, false);
            }
            bool isList = root is JsonArray;

            List<FavoriteItem>? items;
            try
            {
                items = FavoritesStore.ParseImportText(json, out _);
            }
            catch (JsonException ex)
            {
                issues.Add(new DesignIssue(DesignIssueLevel.Error, PathOf(ex.Path),
                    $"値の種類が違います（{Where(ex)}）。数値は引用符で囲まず、選択肢は数値、真偽値は true / false、色は \"#RRGGBB\" で書きます"));
                return new DesignFileReport(designs, issues, isList);
            }
            catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or FormatException)
            {
                issues.Add(new DesignIssue(DesignIssueLevel.Error, "", $"読み込めません: {ex.Message}"));
                return new DesignFileReport(designs, issues, isList);
            }
            if (items == null)
            {
                issues.Add(new DesignIssue(DesignIssueLevel.Error, "", "中身が空（null）です"));
                return new DesignFileReport(designs, issues, isList);
            }

            if (root is JsonObject single)
            {
                var p = items[0].Parameters;
                CompareObject(single, ToNode(p), "", typeof(IrisParameters), null, issues);
                designs.Add(new ValidatedDesign(p.Name, $"「{p.Name}」", "", p));
            }
            else
            {
                var array = (JsonArray)root;
                if (array.Count == 0)
                    issues.Add(new DesignIssue(DesignIssueLevel.Warning, "", "お気に入りの一覧が空です（デザインが 1 つもありません）"));

                for (int i = 0; i < array.Count && i < items.Count; i++)
                {
                    string at = $"[{i}]";
                    var favorite = items[i];
                    if (array[i] is not JsonObject entry || favorite?.Parameters == null)
                    {
                        issues.Add(new DesignIssue(DesignIssueLevel.Warning, at,
                            "お気に入りではありません（{ \"Name\": ..., \"Parameters\": { ... } } の形にします）。読み込みでは飛ばされます"));
                        continue;
                    }

                    CompareObject(entry, ToNode(favorite), at, typeof(FavoriteItem), FavoriteNamesNotExpected, issues, nested: false);
                    if (entry["Parameters"] is JsonObject design)
                    {
                        CompareObject(design, ToNode(favorite.Parameters), $"{at}.Parameters", typeof(IrisParameters), null, issues);
                    }
                    else
                    {
                        issues.Add(new DesignIssue(DesignIssueLevel.Warning, $"{at}.Parameters",
                            "デザイン（Parameters）が書かれていません。既定のデザインになります"));
                    }
                    designs.Add(new ValidatedDesign(favorite.Name, $"{i + 1} 番目「{favorite.Name}」", $"{at}.Parameters", favorite.Parameters));
                }
            }

            return new DesignFileReport(designs, issues, isList);
        }

        /// <summary>The object as the app saves it, to compare names and values with what the file says.</summary>
        private static JsonObject ToNode<T>(T value) => JsonSerializer.SerializeToNode(value)!.AsObject();

        /// <param name="nested">False for a favorites list entry: only its names are checked.</param>
        private static void CompareObject(JsonObject written, JsonObject loaded, string prefix, Type type,
            ISet<string>? notExpected, List<DesignIssue> issues, bool nested = true)
        {
            foreach (var (name, value) in written)
            {
                string path = Join(prefix, name);
                if (!loaded.ContainsKey(name))
                {
                    string? sameIgnoringCase = loaded.Select(kv => kv.Key)
                        .FirstOrDefault(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase));
                    issues.Add(new DesignIssue(DesignIssueLevel.Warning, path, sameIgnoringCase != null
                        ? $"知らない項目名です（無視されます）。大文字・小文字まで同じ「{sameIgnoringCase}」にします"
                        : "知らない項目名です（無視されます）"));
                    continue;
                }
                // A favorites entry's Name / Note / CreatedAt are only labels; its Parameters are compared separately
                if (!nested) continue;

                var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                var loadedValue = loaded[name];

                if (value is JsonArray writtenItems && loadedValue is JsonArray loadedItems)
                {
                    CompareItems(writtenItems, loadedItems, path, property, issues);
                    continue;
                }
                CompareValue(value, loadedValue, path, name, property?.PropertyType, issues);
            }

            var missing = loaded.Select(kv => kv.Key)
                .Where(k => !written.ContainsKey(k) && (notExpected == null || !notExpected.Contains(k)))
                .ToList();
            if (missing.Count > 0)
            {
                issues.Add(new DesignIssue(DesignIssueLevel.Note, prefix,
                    $"書かれていない項目が {missing.Count} 個あります（既定値になります）: {string.Join(", ", missing)}"));
            }
        }

        private static void CompareItems(JsonArray written, JsonArray loaded, string path, PropertyInfo? property,
            List<DesignIssue> issues)
        {
            if (written.Count > loaded.Count)
            {
                issues.Add(new DesignIssue(DesignIssueLevel.Warning, path,
                    $"{written.Count} 個書かれていますが、読み込めるのは {loaded.Count} 個までです（残りは無視されます）"));
            }

            // Symbols / Highlights: compare each item with the item type's own rules
            var itemType = property?.PropertyType.IsGenericType == true ? property.PropertyType.GetGenericArguments()[0] : null;
            for (int i = 0; i < Math.Min(written.Count, loaded.Count); i++)
            {
                if (written[i] is JsonObject w && loaded[i] is JsonObject l && itemType != null)
                    CompareObject(w, l, $"{path}[{i}]", itemType, ItemNamesNotExpected, issues);
            }
        }

        private static void CompareValue(JsonNode? written, JsonNode? loaded, string path, string name, Type? type,
            List<DesignIssue> issues)
        {
            if (written == null)
            {
                issues.Add(new DesignIssue(DesignIssueLevel.Warning, path,
                    $"null は使えません。{Show(loaded)} として読み込まれます"));
                return;
            }

            if (TryNumber(written, out double w) && TryNumber(loaded, out double l))
            {
                if (type is { IsEnum: true })
                {
                    if (!SameNumber(w, l))
                    {
                        issues.Add(new DesignIssue(DesignIssueLevel.Warning, path,
                            $"{Show(written)} は選べる値ではありません（{Choices(type)}）。{Show(loaded)} として読み込まれます"));
                    }
                    else if (!Enum.IsDefined(type, Enum.ToObject(type, (int)l)))
                    {
                        issues.Add(new DesignIssue(DesignIssueLevel.Warning, path,
                            $"{Show(written)} は選べる値ではありません（{Choices(type)}）"));
                    }
                    return;
                }
                if (!SameNumber(w, l))
                {
                    issues.Add(new DesignIssue(DesignIssueLevel.Warning, path,
                        $"{Show(written)} は範囲外です。{Show(loaded)} として読み込まれます"));
                }
                return;
            }

            if (written is JsonValue v && v.TryGetValue(out string? text) && name.EndsWith("Color", StringComparison.Ordinal))
            {
                CheckColor(text, path, name, issues);
                return;
            }

            if (!JsonNode.DeepEquals(written, loaded))
            {
                issues.Add(new DesignIssue(DesignIssueLevel.Warning, path,
                    $"{Show(written)} は {Show(loaded)} として読み込まれます"));
            }
        }

        private static void CheckColor(string text, string path, string name, List<DesignIssue> issues)
        {
            if (ColorFill.IsHexColor(text)) return;

            var fill = ColorFill.Parse(text);
            if (!fill.IsGradient)
            {
                issues.Add(new DesignIssue(DesignIssueLevel.Warning, path,
                    $"\"{text}\" は色として読めません。\"#RRGGBB\" か、グラデーションの書き方にします（描画では既定の色になります）"));
                return;
            }

            if (!GradientColorNames.Contains(name))
            {
                issues.Add(new DesignIssue(DesignIssueLevel.Warning, path,
                    $"この項目はグラデーションを使えません。最初の色 {fill.PrimaryColor} だけが使われます"));
                return;
            }

            int written = text.Count(c => c == '#');
            if (written > ColorFill.MaxStops)
            {
                issues.Add(new DesignIssue(DesignIssueLevel.Warning, path,
                    $"グラデーションの色は {ColorFill.MaxStops} 個までです。{ColorFill.MaxStops + 1} 個目以降は無視されます"));
            }
        }

        private static bool TryNumber(JsonNode? node, out double value)
        {
            value = 0;
            return node is JsonValue v && v.GetValueKind() == JsonValueKind.Number && v.TryGetValue(out value);
        }

        private static bool SameNumber(double a, double b) => Math.Abs(a - b) <= 1e-9 * Math.Max(1.0, Math.Abs(a));

        private static string Choices(Type enumType)
        {
            var numbers = Enum.GetValues(enumType).Cast<object>().Select(Convert.ToInt32).OrderBy(n => n).ToList();
            return $"{numbers.First()}〜{numbers.Last()}";
        }

        private static string Show(JsonNode? node) => node switch
        {
            null => "null",
            JsonValue v when TryNumber(v, out double d) => d.ToString("0.######", Inv),
            _ => node.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping })
        };

        private static string Join(string prefix, string name) => prefix.Length == 0 ? name : $"{prefix}.{name}";

        /// <summary>"$.Symbols[0].Size" → "Symbols[0].Size", "$[1].Parameters" → "[1].Parameters".</summary>
        private static string PathOf(string? jsonPath)
        {
            if (string.IsNullOrEmpty(jsonPath)) return "";
            string p = jsonPath.StartsWith('$') ? jsonPath[1..] : jsonPath;
            return p.StartsWith('.') ? p[1..] : p;
        }

        private static string Where(JsonException ex) =>
            ex.LineNumber is long line
                ? $"{line + 1} 行目、{(ex.BytePositionInLine ?? 0) + 1} 文字目あたり"
                : "位置不明";
    }
}
