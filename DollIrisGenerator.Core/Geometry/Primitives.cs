using System;

namespace DollIrisGenerator.Core.Geometry
{
    // Point / Vector / Rect for the design core, which does not use WPF.
    //
    // They do their arithmetic exactly the way System.Windows.Point / Vector / Rect do (same operations in the
    // same order, e.g. Vector division multiplies by 1 / scalar and Normalize divides by the largest component
    // first), so the outlines, patterns and gradients come out bit for bit the same as when they were computed
    // with the WPF types. Do not "simplify" the formulas: the SVG output and the screen / PDF / PNG output must
    // keep agreeing with designs made in earlier versions.
    //
    // The WPF app converts with WpfGeometry.ToWpf() / ToCore().

    /// <summary>A point in drawing coordinates (same arithmetic as System.Windows.Point).</summary>
    public struct Point : IEquatable<Point>
    {
        public Point(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; set; }
        public double Y { get; set; }

        public static Vector operator -(Point point1, Point point2) => new(point1.X - point2.X, point1.Y - point2.Y);
        public static Point operator +(Point point, Vector vector) => new(point.X + vector.X, point.Y + vector.Y);
        public static Point operator -(Point point, Vector vector) => new(point.X - vector.X, point.Y - vector.Y);

        public static bool operator ==(Point point1, Point point2) => point1.X == point2.X && point1.Y == point2.Y;
        public static bool operator !=(Point point1, Point point2) => !(point1 == point2);

        public bool Equals(Point value) => X.Equals(value.X) && Y.Equals(value.Y);
        public override bool Equals(object? o) => o is Point p && Equals(p);
        public override int GetHashCode() => X.GetHashCode() ^ Y.GetHashCode();
        public override string ToString() => FormattableString.Invariant($"{X},{Y}");
    }

    /// <summary>A displacement (same arithmetic as System.Windows.Vector).</summary>
    public struct Vector : IEquatable<Vector>
    {
        public Vector(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; set; }
        public double Y { get; set; }

        public double Length => Math.Sqrt(X * X + Y * Y);
        public double LengthSquared => X * X + Y * Y;

        /// <summary>Makes this a unit vector (divides by the largest component first, as WPF does, to avoid overflow).</summary>
        public void Normalize()
        {
            this /= Math.Max(Math.Abs(X), Math.Abs(Y));
            this /= Length;
        }

        /// <summary>Signed angle in degrees from <paramref name="vector1"/> to <paramref name="vector2"/>.</summary>
        public static double AngleBetween(Vector vector1, Vector vector2)
        {
            double sin = vector1.X * vector2.Y - vector2.X * vector1.Y;
            double cos = vector1.X * vector2.X + vector1.Y * vector2.Y;
            return Math.Atan2(sin, cos) * (180 / Math.PI);
        }

        /// <summary>Dot product.</summary>
        public static double Multiply(Vector vector1, Vector vector2) => vector1.X * vector2.X + vector1.Y * vector2.Y;

        public static Vector operator -(Vector vector) => new(-vector.X, -vector.Y);
        public static Vector operator +(Vector vector1, Vector vector2) => new(vector1.X + vector2.X, vector1.Y + vector2.Y);
        public static Vector operator -(Vector vector1, Vector vector2) => new(vector1.X - vector2.X, vector1.Y - vector2.Y);
        public static Point operator +(Vector vector, Point point) => new(point.X + vector.X, point.Y + vector.Y);
        public static Vector operator *(Vector vector, double scalar) => new(vector.X * scalar, vector.Y * scalar);
        public static Vector operator *(double scalar, Vector vector) => new(vector.X * scalar, vector.Y * scalar);
        public static Vector operator /(Vector vector, double scalar) => vector * (1.0 / scalar);
        public static double operator *(Vector vector1, Vector vector2) => Multiply(vector1, vector2);

        public static bool operator ==(Vector vector1, Vector vector2) => vector1.X == vector2.X && vector1.Y == vector2.Y;
        public static bool operator !=(Vector vector1, Vector vector2) => !(vector1 == vector2);

        public bool Equals(Vector value) => X.Equals(value.X) && Y.Equals(value.Y);
        public override bool Equals(object? o) => o is Vector v && Equals(v);
        public override int GetHashCode() => X.GetHashCode() ^ Y.GetHashCode();
        public override string ToString() => FormattableString.Invariant($"{X},{Y}");
    }

    /// <summary>An axis-aligned box (same values as System.Windows.Rect, including <see cref="Empty"/>).</summary>
    public struct Rect : IEquatable<Rect>
    {
        public Rect(double x, double y, double width, double height)
        {
            if (width < 0 || height < 0) throw new ArgumentException("Width and height must be non-negative.");
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        // Empty: the same sentinel values WPF uses (position +∞, size -∞)
        private Rect(bool _)
        {
            X = double.PositiveInfinity;
            Y = double.PositiveInfinity;
            Width = double.NegativeInfinity;
            Height = double.NegativeInfinity;
        }

        public static Rect Empty { get; } = new(true);

        public double X { get; private set; }
        public double Y { get; private set; }
        public double Width { get; }
        public double Height { get; }

        public bool IsEmpty => Width < 0;
        public double Left => X;
        public double Top => Y;
        public double Right => IsEmpty ? double.NegativeInfinity : X + Width;
        public double Bottom => IsEmpty ? double.NegativeInfinity : Y + Height;

        /// <summary>Moves this box by (<paramref name="offsetX"/>, <paramref name="offsetY"/>); like WPF, an empty box cannot be moved.</summary>
        public void Offset(double offsetX, double offsetY)
        {
            if (IsEmpty) throw new InvalidOperationException("An empty Rect cannot be offset.");
            X += offsetX;
            Y += offsetY;
        }

        public static bool operator ==(Rect rect1, Rect rect2) =>
            rect1.X == rect2.X && rect1.Y == rect2.Y && rect1.Width == rect2.Width && rect1.Height == rect2.Height;
        public static bool operator !=(Rect rect1, Rect rect2) => !(rect1 == rect2);

        public bool Equals(Rect value) => this == value;
        public override bool Equals(object? o) => o is Rect r && Equals(r);
        public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
        public override string ToString() => IsEmpty ? "Empty" : FormattableString.Invariant($"{X},{Y},{Width},{Height}");
    }
}
