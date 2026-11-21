using System;

namespace DollIrisGenerator.Services
{
    /// <summary>
    /// Color strings as the renderers read them. The WPF shape geometries and <c>Color</c> values are made from
    /// these in the app (WpfGeometry).
    /// </summary>
    public static class GeometryHelper
    {
        public static string SanitizeColorHex(string? hex, string defaultHex = "#000000")
        {
            if (string.IsNullOrWhiteSpace(hex)) return defaultHex;
            // A gradient where one color is needed (e.g. a hand-edited file): use its first color
            if (Models.ColorFill.Parse(hex) is { IsGradient: true } fill) hex = fill.PrimaryColor;
            hex = hex.Trim();
            if (Models.ColorFill.IsHexColor(hex))
            {
                return hex;
            }
            return defaultHex;
        }

        /// <summary>
        /// Channels of a "#RGB" / "#RRGGBB" / "#AARRGGBB" color with its alpha multiplied by <paramref name="alpha"/>.
        /// Anything unreadable is opaque black: callers that can tell the user should use <see cref="TryParseHexArgb"/>,
        /// and the renderers pass every color through <see cref="SanitizeColorHex"/> (with each field's own default) first.
        /// </summary>
        public static (byte A, byte R, byte G, byte B) ParseHexArgb(string? hex, double alpha = 1.0) =>
            TryParseHexArgb(hex, out var argb, alpha) ? argb : ((byte)255, (byte)0, (byte)0, (byte)0);

        /// <summary>
        /// Reads a "#RGB" / "#RRGGBB" / "#AARRGGBB" color (the "#" may be left out; a gradient gives its first color),
        /// with its alpha multiplied by <paramref name="alpha"/>. False when it is not a color.
        /// </summary>
        public static bool TryParseHexArgb(string? hex, out (byte A, byte R, byte G, byte B) argb, double alpha = 1.0)
        {
            argb = (255, 0, 0, 0);
            if (string.IsNullOrWhiteSpace(hex)) return false;
            // A gradient where one color is needed (e.g. a hand-edited file): use its first color
            if (Models.ColorFill.Parse(hex) is { IsGradient: true } fill) hex = fill.PrimaryColor;

            ReadOnlySpan<char> h = hex.AsSpan().Trim().TrimStart('#');
            byte a = (byte)Math.Clamp((int)(alpha * 255.0), 0, 255);
            byte r, g, b;
            switch (h.Length)
            {
                case 6:
                    if (!Hex(h[0..2], out r) || !Hex(h[2..4], out g) || !Hex(h[4..6], out b)) return false;
                    break;
                case 8:
                    if (!Hex(h[0..2], out byte aa) || !Hex(h[2..4], out r) || !Hex(h[4..6], out g) || !Hex(h[6..8], out b)) return false;
                    a = (byte)(aa * alpha);
                    break;
                case 3:
                    if (!Hex([h[0], h[0]], out r) || !Hex([h[1], h[1]], out g) || !Hex([h[2], h[2]], out b)) return false;
                    break;
                default:
                    return false;
            }
            argb = (a, r, g, b);
            return true;
        }

        // Exactly two hex digits: no white space or sign, unlike NumberStyles.HexNumber
        private static bool Hex(ReadOnlySpan<char> digits, out byte value) =>
            byte.TryParse(digits, System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture, out value);
    }
}
