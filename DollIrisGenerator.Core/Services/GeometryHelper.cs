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
        /// Anything unreadable is opaque black, as the renderers have always drawn it.
        /// </summary>
        public static (byte A, byte R, byte G, byte B) ParseHexArgb(string? hex, double alpha = 1.0)
        {
            if (string.IsNullOrWhiteSpace(hex)) return (255, 0, 0, 0);
            // A gradient where one color is needed (e.g. a hand-edited file): use its first color
            if (Models.ColorFill.Parse(hex) is { IsGradient: true } fill) hex = fill.PrimaryColor;
            try
            {
                hex = hex.Trim().TrimStart('#');
                byte a = (byte)Math.Clamp((int)(alpha * 255.0), 0, 255);
                byte r = 0, g = 0, b = 0;

                if (hex.Length == 6)
                {
                    r = Convert.ToByte(hex.Substring(0, 2), 16);
                    g = Convert.ToByte(hex.Substring(2, 2), 16);
                    b = Convert.ToByte(hex.Substring(4, 2), 16);
                }
                else if (hex.Length == 8)
                {
                    a = (byte)(Convert.ToByte(hex.Substring(0, 2), 16) * alpha);
                    r = Convert.ToByte(hex.Substring(2, 2), 16);
                    g = Convert.ToByte(hex.Substring(4, 2), 16);
                    b = Convert.ToByte(hex.Substring(6, 2), 16);
                }
                else if (hex.Length == 3)
                {
                    r = Convert.ToByte(new string(hex[0], 2), 16);
                    g = Convert.ToByte(new string(hex[1], 2), 16);
                    b = Convert.ToByte(new string(hex[2], 2), 16);
                }

                return (a, r, g, b);
            }
            catch
            {
                return (255, 0, 0, 0);
            }
        }
    }
}
