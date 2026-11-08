using System.Windows.Media;
using DollIrisGenerator.Models;
using CorePoint = DollIrisGenerator.Core.Geometry.Point;
using CoreRect = DollIrisGenerator.Core.Geometry.Rect;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;

namespace DollIrisGenerator.Services
{
    /// <summary>
    /// Bridge between the design core (DollIrisGenerator.Core, no WPF) and WPF: point / box conversions,
    /// WPF geometries of the core's outlines, and WPF colors of color strings.
    /// </summary>
    public static class WpfGeometry
    {
        public static WpfPoint ToWpf(this CorePoint p) => new(p.X, p.Y);
        public static CorePoint ToCore(this WpfPoint p) => new(p.X, p.Y);

        public static WpfRect ToWpf(this CoreRect r) => r.IsEmpty ? WpfRect.Empty : new WpfRect(r.X, r.Y, r.Width, r.Height);
        public static CoreRect ToCore(this WpfRect r) => r.IsEmpty ? CoreRect.Empty : new CoreRect(r.X, r.Y, r.Width, r.Height);

        /// <summary>WPF geometry of this outline (closed and filled), centered at (0, 0).</summary>
        public static PathGeometry ToPathGeometry(this ShapeOutline outline)
        {
            var figure = new PathFigure { StartPoint = outline.Start.ToWpf(), IsClosed = true, IsFilled = true };
            var segments = outline.Segments;
            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                // A straight closing edge is implied by IsClosed (same as the original polygons)
                if (i == segments.Count - 1 && seg.IsLine) break;
                figure.Segments.Add(seg.IsLine
                    ? new LineSegment(seg.End.ToWpf(), true)
                    : new BezierSegment(seg.C1.ToWpf(), seg.C2.ToWpf(), seg.End.ToWpf(), true));
            }
            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            return geometry;
        }

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

        /// <summary>WPF color of a color string (see <see cref="GeometryHelper.ParseHexArgb"/>).</summary>
        public static Color ParseHexColor(string? hex, double alpha = 1.0)
        {
            var (a, r, g, b) = GeometryHelper.ParseHexArgb(hex, alpha);
            return Color.FromArgb(a, r, g, b);
        }
    }
}
