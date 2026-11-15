using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DollIrisGenerator.Models
{
    /// <summary>How 並べて追加 lays the new items out.</summary>
    public enum ItemLayoutKind
    {
        /// <summary>On a circle (or an arc of it) round a center.</summary>
        Circle = 0,
        /// <summary>Evenly from a start point to an end point.</summary>
        Line = 1
    }

    /// <summary>
    /// The 並べて追加 form (decorative symbols / highlights). Screen-only: not part of a design and not saved.
    /// Positions are in iris-radius units like the items' own position (X right, Y down), angles in degrees with
    /// 0° at the top and going clockwise.
    /// </summary>
    public class ItemLayoutSettings : INotifyPropertyChanged
    {
        public const int MinCount = 2;
        public const int MaxCount = 24;
        public const double PositionLimit = 0.85; // the items' position sliders
        public const double MinRadius = 0.05;

        private ItemLayoutKind _kind = ItemLayoutKind.Circle;
        private int _count = 8;
        private double _centerX;
        private double _centerY;
        private double _radius = 0.6;
        private double _startAngle;
        private double _sweepAngle = 360.0;
        private double _startX = -0.6;
        private double _startY = 0.55;
        private double _endX = 0.6;
        private double _endY = 0.55;
        private bool _alignRotation = true;

        public ItemLayoutKind Kind
        {
            get => _kind;
            set { _kind = Enum.IsDefined(value) ? value : ItemLayoutKind.Circle; OnPropertyChanged(); OnPropertyChanged(nameof(IsCircle)); OnPropertyChanged(nameof(IsLine)); }
        }

        /// <summary>For radio buttons / showing the circle's or the line's settings.</summary>
        public bool IsCircle
        {
            get => Kind == ItemLayoutKind.Circle;
            set { if (value) Kind = ItemLayoutKind.Circle; }
        }

        public bool IsLine
        {
            get => Kind == ItemLayoutKind.Line;
            set { if (value) Kind = ItemLayoutKind.Line; }
        }

        /// <summary>How many items to add.</summary>
        public int Count
        {
            get => _count;
            set { _count = Math.Clamp(value, MinCount, MaxCount); OnPropertyChanged(); }
        }

        public double CenterX { get => _centerX; set { _centerX = Pos(value); OnPropertyChanged(); } }
        public double CenterY { get => _centerY; set { _centerY = Pos(value); OnPropertyChanged(); } }

        public double Radius
        {
            get => _radius;
            set { _radius = Finite(value, MinRadius, PositionLimit, 0.6); OnPropertyChanged(); }
        }

        /// <summary>Where the first item goes: 0° = straight above the center, clockwise.</summary>
        public double StartAngle
        {
            get => _startAngle;
            set { _startAngle = Finite(value, -180.0, 180.0, 0.0); OnPropertyChanged(); }
        }

        /// <summary>360° = all the way round (evenly spaced, the last one does not land on the first); less = an arc from end to end.</summary>
        public double SweepAngle
        {
            get => _sweepAngle;
            set { _sweepAngle = Finite(value, 10.0, 360.0, 360.0); OnPropertyChanged(); }
        }

        public double StartX { get => _startX; set { _startX = Pos(value); OnPropertyChanged(); } }
        public double StartY { get => _startY; set { _startY = Pos(value); OnPropertyChanged(); } }
        public double EndX { get => _endX; set { _endX = Pos(value); OnPropertyChanged(); } }
        public double EndY { get => _endY; set { _endY = Pos(value); OnPropertyChanged(); } }

        /// <summary>
        /// Turn each item along the layout: on a circle its top points away from the center, on a line it follows
        /// the line's direction. The angle is added to the model item's own rotation.
        /// </summary>
        public bool AlignRotation
        {
            get => _alignRotation;
            set { _alignRotation = value; OnPropertyChanged(); }
        }

        private static double Pos(double v) => Finite(v, -PositionLimit, PositionLimit, 0.0);

        private static double Finite(double v, double min, double max, double fallback) =>
            double.IsFinite(v) ? Math.Clamp(v, min, max) : fallback;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
