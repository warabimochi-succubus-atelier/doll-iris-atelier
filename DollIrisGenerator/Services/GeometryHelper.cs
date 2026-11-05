using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace DollIrisGenerator.Services
{
    public static class GeometryHelper
    {
        /// <summary>
        /// Generates a 4-pointed star / sparkle path centered at (0, 0) with given radius.
        /// </summary>
        public static PathGeometry CreateStar4Geometry(double radius, double innerRatio = 0.22)
        {
            var geometry = new PathGeometry();
            var figure = new PathFigure();
            var points = new List<Point>();

            for (int i = 0; i < 8; i++)
            {
                double angle = i * Math.PI / 4.0 - Math.PI / 2.0;
                double r = (i % 2 == 0) ? radius : radius * innerRatio;
                points.Add(new Point(r * Math.Cos(angle), r * Math.Sin(angle)));
            }

            figure.StartPoint = points[0];
            for (int i = 1; i < points.Count; i++)
            {
                figure.Segments.Add(new LineSegment(points[i], true));
            }
            figure.IsClosed = true;
            geometry.Figures.Add(figure);
            return geometry;
        }

        /// <summary>
        /// Generates a 5-pointed star centered at (0, 0).
        /// </summary>
        public static PathGeometry CreateStar5Geometry(double radius, double innerRatio = 0.40)
        {
            var geometry = new PathGeometry();
            var figure = new PathFigure();
            var points = new List<Point>();

            for (int i = 0; i < 10; i++)
            {
                double angle = i * Math.PI / 5.0 - Math.PI / 2.0;
                double r = (i % 2 == 0) ? radius : radius * innerRatio;
                points.Add(new Point(r * Math.Cos(angle), r * Math.Sin(angle)));
            }

            figure.StartPoint = points[0];
            for (int i = 1; i < points.Count; i++)
            {
                figure.Segments.Add(new LineSegment(points[i], true));
            }
            figure.IsClosed = true;
            geometry.Figures.Add(figure);
            return geometry;
        }

        /// <summary>
        /// Generates a cute heart shape centered at (0, 0).
        /// </summary>
        public static PathGeometry CreateHeartGeometry(double radius)
        {
            var geometry = new PathGeometry();
            var figure = new PathFigure();

            double topY = -radius * 0.45;
            double bottomY = radius * 0.85;
            double leftX = -radius * 0.95;
            double rightX = radius * 0.95;

            figure.StartPoint = new Point(0, bottomY);

            var leftCubic = new BezierSegment(
                new Point(leftX * 1.05, bottomY * 0.2),
                new Point(leftX, topY * 1.3),
                new Point(0, topY),
                true
            );

            var rightCubic = new BezierSegment(
                new Point(rightX, topY * 1.3),
                new Point(rightX * 1.05, bottomY * 0.2),
                new Point(0, bottomY),
                true
            );

            figure.Segments.Add(leftCubic);
            figure.Segments.Add(rightCubic);
            figure.IsClosed = true;
            geometry.Figures.Add(figure);
            return geometry;
        }

        /// <summary>
        /// Generates a crescent moon shape centered at (0, 0).
        /// </summary>
        public static PathGeometry CreateMoonGeometry(double radius)
        {
            var geometry = new PathGeometry();
            var figure = new PathFigure();

            Point top = new Point(0, -radius);
            Point bottom = new Point(0, radius);

            figure.StartPoint = top;
            figure.Segments.Add(new ArcSegment(
                bottom,
                new Size(radius, radius),
                0,
                false,
                SweepDirection.Clockwise,
                true
            ));

            figure.Segments.Add(new ArcSegment(
                top,
                new Size(radius * 0.82, radius * 1.05),
                0,
                false,
                SweepDirection.Counterclockwise,
                true
            ));

            figure.IsClosed = true;
            geometry.Figures.Add(figure);
            return geometry;
        }

        /// <summary>
        /// Generates a cherry blossom petal (桜の花びら) centered around (0, 0).
        /// Has the characteristic gentle curve and cleft (notch) at the outer tip.
        /// </summary>
        public static PathGeometry CreateSakuraPetalGeometry(double radius)
        {
            var geometry = new PathGeometry();
            var figure = new PathFigure();

            double r = radius;
            Point basePoint = new Point(0, r * 0.85);
            Point leftLobe = new Point(-r * 0.38, -r * 0.95);
            Point cleft = new Point(0, -r * 0.68);
            Point rightLobe = new Point(r * 0.38, -r * 0.95);

            figure.StartPoint = basePoint;

            // Left side outward sweep to left lobe
            figure.Segments.Add(new BezierSegment(
                new Point(-r * 0.65, r * 0.35),
                new Point(-r * 0.70, -r * 0.55),
                leftLobe,
                true
            ));

            // Left lobe to notch cleft
            figure.Segments.Add(new BezierSegment(
                new Point(-r * 0.22, -r * 0.98),
                new Point(-r * 0.08, -r * 0.72),
                cleft,
                true
            ));

            // Notch cleft to right lobe
            figure.Segments.Add(new BezierSegment(
                new Point(r * 0.08, -r * 0.72),
                new Point(r * 0.22, -r * 0.98),
                rightLobe,
                true
            ));

            // Right lobe sweeping down to base
            figure.Segments.Add(new BezierSegment(
                new Point(r * 0.70, -r * 0.55),
                new Point(r * 0.65, r * 0.35),
                basePoint,
                true
            ));

            figure.IsClosed = true;
            geometry.Figures.Add(figure);
            return geometry;
        }

        /// <summary>
        /// Generates a flared cross shape centered at (0, 0).
        /// </summary>
        public static PathGeometry CreateCrossGeometry(double radius, double barThickness = 0.32)
        {
            double r = radius;
            double w = radius * barThickness;

            var geometry = new PathGeometry();
            var figure = new PathFigure();

            figure.StartPoint = new Point(-w, -r);
            figure.Segments.Add(new LineSegment(new Point(w, -r), true));
            figure.Segments.Add(new LineSegment(new Point(w, -w), true));
            figure.Segments.Add(new LineSegment(new Point(r, -w), true));
            figure.Segments.Add(new LineSegment(new Point(r, w), true));
            figure.Segments.Add(new LineSegment(new Point(w, w), true));
            figure.Segments.Add(new LineSegment(new Point(w, r), true));
            figure.Segments.Add(new LineSegment(new Point(-w, r), true));
            figure.Segments.Add(new LineSegment(new Point(-w, w), true));
            figure.Segments.Add(new LineSegment(new Point(-r, w), true));
            figure.Segments.Add(new LineSegment(new Point(-r, -w), true));
            figure.Segments.Add(new LineSegment(new Point(-w, -w), true));

            figure.IsClosed = true;
            geometry.Figures.Add(figure);
            return geometry;
        }

        /// <summary>
        /// Generates a diamond/rhombus shape centered at (0, 0).
        /// </summary>
        public static PathGeometry CreateDiamondGeometry(double radiusX, double radiusY)
        {
            var geometry = new PathGeometry();
            var figure = new PathFigure();

            figure.StartPoint = new Point(0, -radiusY);
            figure.Segments.Add(new LineSegment(new Point(radiusX, 0), true));
            figure.Segments.Add(new LineSegment(new Point(0, radiusY), true));
            figure.Segments.Add(new LineSegment(new Point(-radiusX, 0), true));

            figure.IsClosed = true;
            geometry.Figures.Add(figure);
            return geometry;
        }

        /// <summary>
        /// Generates a cat slit pupil geometry centered at (0, 0).
        /// </summary>
        public static PathGeometry CreateCatSlitGeometry(double radiusX, double radiusY)
        {
            var geometry = new PathGeometry();
            var figure = new PathFigure();

            Point top = new Point(0, -radiusY);
            Point bottom = new Point(0, radiusY);

            figure.StartPoint = top;
            figure.Segments.Add(new BezierSegment(
                new Point(radiusX * 1.4, -radiusY * 0.4),
                new Point(radiusX * 1.4, radiusY * 0.4),
                bottom,
                true
            ));
            figure.Segments.Add(new BezierSegment(
                new Point(-radiusX * 1.4, radiusY * 0.4),
                new Point(-radiusX * 1.4, -radiusY * 0.4),
                top,
                true
            ));

            figure.IsClosed = true;
            geometry.Figures.Add(figure);
            return geometry;
        }

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
