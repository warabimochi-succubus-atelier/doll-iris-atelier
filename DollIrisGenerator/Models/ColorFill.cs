using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DollIrisGenerator.Models
{
    public enum ColorFillKind
    {
        Solid,
        Linear,
        Radial
    }

    /// <summary>One color stop of a gradient: a "#RRGGBB" / "#AARRGGBB" / "#RGB" color at 0..1 along the gradient.</summary>
    public readonly record struct ColorStop(string Color, double Offset);

    /// <summary>
    /// A color setting that is either a single color or a gradient (単色 / グラデーション).
    ///
    /// It is stored in the same string property the color always used, so files, favorites, presets,
    /// Clone and undo keep working unchanged:
    /// <list type="bullet">
    /// <item>単色: the plain hex color, exactly as before (e.g. <c>#FFFFFF</c>).</item>
    /// <item>線形: <c>linear-gradient(90deg, #FFFFFF 0%, #80D8FF 100%)</c></item>
    /// <item>放射状: <c>radial-gradient(#FFFFFF 0%, #80D8FF 100%)</c></item>
    /// </list>
    /// The angle is in degrees, clockwise on screen: 0° = left → right, 90° = top → bottom.
    /// A gradient has <see cref="MinStops"/>..<see cref="MaxStops"/> stops, sorted by position.
    /// Anything that is not a well-formed gradient is treated as a solid color string (the renderers then
    /// sanitize it as they always did), so a broken or hand-edited value can never break rendering.
    /// </summary>
    public sealed class ColorFill
    {
        public const int MinStops = 2;
        public const int MaxStops = 5;
        public const double DefaultAngle = 90.0;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly Regex HexRegex = new(@"^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$", RegexOptions.Compiled);
        private static readonly Regex GradientRegex = new(@"^\s*(linear|radial)-gradient\s*\((.*)\)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex AngleRegex = new(@"^\s*([-+]?\d+(?:\.\d+)?)\s*deg\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex StopRegex = new(@"^\s*(#[0-9a-fA-F]+)\s*(?:([-+]?\d+(?:\.\d+)?)\s*%)?\s*$", RegexOptions.Compiled);

        /// <summary>Longest string <see cref="Parse"/> looks at; a crafted file cannot make it parse megabytes.</summary>
        private const int MaxParseLength = 512;

        public ColorFillKind Kind { get; }

        /// <summary>Linear gradient direction in degrees (0..360). Unused for solid / radial.</summary>
        public double Angle { get; }

        /// <summary>For a solid fill: the single entry with the color as stored. For a gradient: 2..5 stops sorted by offset.</summary>
        public IReadOnlyList<ColorStop> Stops { get; }

        public bool IsGradient => Kind != ColorFillKind.Solid;

        /// <summary>The solid color, or the first stop of a gradient (used where one color is needed, e.g. the lens glow).</summary>
        public string PrimaryColor => Stops[0].Color;

        private ColorFill(ColorFillKind kind, double angle, IReadOnlyList<ColorStop> stops)
        {
            Kind = kind;
            Angle = angle;
            Stops = stops;
        }

        public static ColorFill Solid(string? color) =>
            new(ColorFillKind.Solid, DefaultAngle, new[] { new ColorStop(color ?? "", 0.0) });

        /// <summary>
        /// Builds a gradient. Stop colors must be hex colors; offsets are clamped to 0..1 and sorted,
        /// the stop count is limited to 2..5 (missing stops are filled from the last one), and the angle is
        /// normalized to 0..360. Passing <see cref="ColorFillKind.Solid"/> keeps only the first stop's color.
        /// </summary>
        public static ColorFill Create(ColorFillKind kind, double angle, IEnumerable<ColorStop> stops)
        {
            var list = (stops ?? Enumerable.Empty<ColorStop>())
                .Take(MaxStops)
                .Select(s => new ColorStop(IsHexColor(s.Color) ? s.Color.Trim().ToUpperInvariant() : "#FFFFFF", ClampOffset(s.Offset)))
                .ToList();

            if (kind == ColorFillKind.Solid)
                return Solid(list.Count > 0 ? list[0].Color : "#FFFFFF");

            if (list.Count == 0) list.Add(new ColorStop("#FFFFFF", 0.0));
            while (list.Count < MinStops) list.Add(new ColorStop(list[^1].Color, 1.0));

            // Stable sort keeps the order of stops at the same position
            var sorted = list.Select((s, i) => (s, i)).OrderBy(t => t.s.Offset).ThenBy(t => t.i).Select(t => t.s).ToArray();
            return new ColorFill(kind, NormalizeAngle(angle), sorted);
        }

        /// <summary>
        /// Reads a stored color value. Old files (a plain hex color) and anything that is not a well-formed
        /// gradient come back as <see cref="ColorFillKind.Solid"/> holding the original string unchanged.
        /// </summary>
        public static ColorFill Parse(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > MaxParseLength) return Solid(value);

            var m = GradientRegex.Match(value);
            if (!m.Success) return Solid(value);

            var kind = m.Groups[1].Value.Equals("linear", StringComparison.OrdinalIgnoreCase) ? ColorFillKind.Linear : ColorFillKind.Radial;
            var parts = m.Groups[2].Value.Split(',');
            int index = 0;
            double angle = DefaultAngle;

            if (parts.Length > 0)
            {
                var am = AngleRegex.Match(parts[0]);
                if (am.Success)
                {
                    if (kind != ColorFillKind.Linear) return Solid(value);
                    angle = double.Parse(am.Groups[1].Value, Inv);
                    index = 1;
                }
            }

            var raw = new List<(string Color, double? Offset)>();
            for (; index < parts.Length; index++)
            {
                var sm = StopRegex.Match(parts[index]);
                if (!sm.Success || !IsHexColor(sm.Groups[1].Value)) return Solid(value);
                double? offset = sm.Groups[2].Success ? double.Parse(sm.Groups[2].Value, Inv) / 100.0 : null;
                raw.Add((sm.Groups[1].Value, offset));
            }
            if (raw.Count < MinStops) return Solid(value);

            // Stops without a position are spread evenly (as in CSS: first 0%, last 100%)
            var stops = raw.Select((s, i) => new ColorStop(
                s.Color,
                s.Offset ?? (raw.Count == 1 ? 0.0 : (double)i / (raw.Count - 1)))).ToList();
            return Create(kind, angle, stops);
        }

        /// <summary>The string stored in the model: the color itself for a solid fill, the gradient notation otherwise.</summary>
        public override string ToString()
        {
            if (Kind == ColorFillKind.Solid) return Stops[0].Color;

            var sb = new StringBuilder();
            sb.Append(Kind == ColorFillKind.Linear ? "linear-gradient(" : "radial-gradient(");
            if (Kind == ColorFillKind.Linear)
                sb.Append(FormatNumber(Angle)).Append("deg, ");
            sb.Append(string.Join(", ", Stops.Select(s => $"{s.Color} {FormatNumber(s.Offset * 100.0)}%")));
            sb.Append(')');
            return sb.ToString();
        }

        /// <summary>Same fill seen in a mirror (right eye): a linear gradient's direction is flipped left-right.</summary>
        public ColorFill MirroredX() =>
            Kind == ColorFillKind.Linear ? new ColorFill(Kind, NormalizeAngle(180.0 - Angle), Stops) : this;

        /// <summary>
        /// The gradient the "グラデーション" switch starts from: the current color at 0 %, and a lighter version of
        /// it (a darker one for very light colors) at 100 %, top to bottom.
        /// </summary>
        public static ColorFill DefaultGradientFrom(string? color, ColorFillKind kind = ColorFillKind.Linear)
        {
            string start = IsHexColor(color) ? color!.Trim().ToUpperInvariant() : "#FFFFFF";
            var (r, g, b) = ToRgb(start);
            double luminance = (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255.0;
            (int R, int G, int B) target = luminance > 0.7 ? (0, 0, 0) : (255, 255, 255);
            double t = luminance > 0.7 ? 0.4 : 0.5;
            string end = $"#{Lerp(r, target.R, t):X2}{Lerp(g, target.G, t):X2}{Lerp(b, target.B, t):X2}";
            return Create(kind == ColorFillKind.Solid ? ColorFillKind.Linear : kind, DefaultAngle,
                new[] { new ColorStop(start, 0.0), new ColorStop(end, 1.0) });

            static int Lerp(int a, int z, double t) => (int)Math.Round(a + (z - a) * t);
        }

        /// <summary>RGB of a hex color (alpha ignored); invalid input gives white.</summary>
        private static (int R, int G, int B) ToRgb(string hex)
        {
            if (!IsHexColor(hex)) return (255, 255, 255);
            string h = hex.Trim().TrimStart('#');
            if (h.Length == 3) h = string.Concat(h.Select(ch => new string(ch, 2)));
            if (h.Length == 8) h = h.Substring(2);
            return (Convert.ToInt32(h.Substring(0, 2), 16), Convert.ToInt32(h.Substring(2, 2), 16), Convert.ToInt32(h.Substring(4, 2), 16));
        }

        public static bool IsHexColor(string? value) => value != null && HexRegex.IsMatch(value.Trim());

        public static bool IsGradientString(string? value) => Parse(value).IsGradient;

        // Rounded to 0.1 % (what ToString writes) so a value survives save / load / Clone unchanged
        private static double ClampOffset(double offset) => double.IsFinite(offset) ? Math.Round(Math.Clamp(offset, 0.0, 1.0), 3) : 0.0;

        private static double NormalizeAngle(double angle)
        {
            if (!double.IsFinite(angle)) return DefaultAngle;
            angle %= 360.0;
            if (angle < 0) angle += 360.0;
            // Rounding keeps ToString / Parse round trips stable
            angle = Math.Round(angle, 1);
            return angle >= 360.0 ? 0.0 : angle;
        }

        private static string FormatNumber(double value) => Math.Round(value, 1).ToString("0.#", Inv);
    }
}
