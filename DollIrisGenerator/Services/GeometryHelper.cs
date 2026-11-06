using System;
using System.Windows.Media;

namespace DollIrisGenerator.Services
{
    public static class GeometryHelper
    {
        // Shape geometries centered at (0, 0). The outlines themselves live in ShapeOutline so that the
        // SVG exporter draws exactly the same shapes; corner rounding goes through ShapeOutline.Rounded.

        /// <summary>4-pointed star / sparkle.</summary>
        public static PathGeometry CreateStar4Geometry(double radius, double innerRatio = 0.22) =>
            ShapeOutline.Star(4, radius, innerRatio).ToPathGeometry();

        /// <summary>5-pointed star.</summary>
        public static PathGeometry CreateStar5Geometry(double radius, double innerRatio = 0.40) =>
            ShapeOutline.Star(5, radius, innerRatio).ToPathGeometry();

        /// <summary>Heart.</summary>
        public static PathGeometry CreateHeartGeometry(double radius) =>
            ShapeOutline.Heart(radius).ToPathGeometry();

        /// <summary>Crescent moon.</summary>
        public static PathGeometry CreateMoonGeometry(double radius) =>
            ShapeOutline.Moon(radius).ToPathGeometry();

        /// <summary>Cherry blossom petal (桜の花びら) with the notch (cleft) at the outer tip.</summary>
        public static PathGeometry CreateSakuraPetalGeometry(double radius) =>
            ShapeOutline.SakuraPetal(radius).ToPathGeometry();

        /// <summary>Cross.</summary>
        public static PathGeometry CreateCrossGeometry(double radius, double barThickness = 0.32) =>
            ShapeOutline.Cross(radius, barThickness).ToPathGeometry();

        /// <summary>Diamond / rhombus.</summary>
        public static PathGeometry CreateDiamondGeometry(double radiusX, double radiusY) =>
            ShapeOutline.Diamond(radiusX, radiusY).ToPathGeometry();

        /// <summary>Cat slit pupil.</summary>
        public static PathGeometry CreateCatSlitGeometry(double radiusX, double radiusY) =>
            ShapeOutline.CatSlit(radiusX, radiusY).ToPathGeometry();

        private static readonly System.Text.RegularExpressions.Regex HexColorRegex =
            new(@"^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$", System.Text.RegularExpressions.RegexOptions.Compiled);

        public static string SanitizeColorHex(string? hex, string defaultHex = "#000000")
        {
            if (string.IsNullOrWhiteSpace(hex)) return defaultHex;
            hex = hex.Trim();
            if (HexColorRegex.IsMatch(hex))
            {
                return hex;
            }
            return defaultHex;
        }

        public static Color ParseHexColor(string? hex, double alpha = 1.0)
        {
            if (string.IsNullOrWhiteSpace(hex)) return Colors.Black;
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

                return Color.FromArgb(a, r, g, b);
            }
            catch
            {
                return Colors.Black;
            }
        }
    }
}
