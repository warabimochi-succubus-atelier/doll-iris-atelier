using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DollIrisGenerator.Models
{
    /// <summary>
    /// The 並べて追加 form (decorative symbols / highlights): how many items to scatter, where, and whether each gets
    /// a random direction. The new items' look (type, color, size...) is the editor's template item.
    /// Screen-only: not part of a design and not saved. Distances are in iris-radius units like the items' position.
    /// </summary>
    public class ItemLayoutSettings : INotifyPropertyChanged
    {
        public const int MinCount = 1;
        public const int MaxCount = 24;
        public const double MaxRadius = 0.85; // the items' position sliders

        /// <summary>The form's starting 個数 for decorative symbols / highlights.</summary>
        public const int DefaultSymbolCount = 3;
        public const int DefaultHighlightCount = 2;

        private int _count = DefaultSymbolCount;
        private double _innerRadius = 0.3;
        private double _outerRadius = 0.75;
        private bool _randomRotation = true;

        /// <summary>How many items to add.</summary>
        public int Count
        {
            get => _count;
            set { _count = Math.Clamp(value, MinCount, MaxCount); OnPropertyChanged(); }
        }

        /// <summary>Nearest the items come to the iris center (keeps them off the pupil).</summary>
        public double InnerRadius
        {
            get => _innerRadius;
            set { _innerRadius = Finite(value, 0.0, MaxRadius, 0.3); OnPropertyChanged(); }
        }

        /// <summary>Farthest the items go from the iris center.</summary>
        public double OuterRadius
        {
            get => _outerRadius;
            set { _outerRadius = Finite(value, 0.05, MaxRadius, 0.75); OnPropertyChanged(); }
        }

        /// <summary>Turn each item a random way (otherwise every item is upright, 0°).</summary>
        public bool RandomRotation
        {
            get => _randomRotation;
            set { _randomRotation = value; OnPropertyChanged(); }
        }

        private static double Finite(double v, double min, double max, double fallback) =>
            double.IsFinite(v) ? Math.Clamp(v, min, max) : fallback;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
