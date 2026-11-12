using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DollIrisGenerator.Models
{
    public class DecorativeSymbolItem : INotifyPropertyChanged
    {
        public const int MaxCount = 12;

        private string _id = Guid.NewGuid().ToString("N");
        private SymbolType _type = SymbolType.Star4;
        private int _count = 1;
        private double _size = 0.18;       // relative to radius
        private double _positionX = 0.35;  // -1.0 to 1.0 (relative to iris radius)
        private double _positionY = 0.35;  // -1.0 to 1.0
        private string _color = "#FFFFFF";
        private double _opacity = 0.85;
        private double _rotation = 0.0;    // degrees
        private double _cornerRounding = 0.0; // 0 = sharp corners (files saved before this existed load as 0) .. 1 = fully rounded
        private bool _enabled = false;
        private LayerBlend _blend = LayerBlend.Normal;

        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public SymbolType Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(); }
        }

        public int Count
        {
            get => _count;
            set { _count = Math.Clamp(value, 1, MaxCount); OnPropertyChanged(); } // bounded: drawing loops over this
        }

        public double Size
        {
            get => _size;
            set { _size = Math.Clamp(value, 0.01, 1.0); OnPropertyChanged(); }
        }

        public double PositionX
        {
            get => _positionX;
            set { _positionX = Math.Clamp(value, -1.5, 1.5); OnPropertyChanged(); }
        }

        public double PositionY
        {
            get => _positionY;
            set { _positionY = Math.Clamp(value, -1.5, 1.5); OnPropertyChanged(); }
        }

        public string Color
        {
            get => _color;
            set { _color = value; OnPropertyChanged(); }
        }

        public double Opacity
        {
            get => _opacity;
            set { _opacity = Math.Clamp(value, 0.0, 1.0); OnPropertyChanged(); }
        }

        public double Rotation
        {
            get => _rotation;
            set { _rotation = Math.Clamp(value, -360.0, 360.0); OnPropertyChanged(); }
        }

        /// <summary>How much the corners / tips of the shape are rounded: 0 = sharp (as before) to 1 = fully rounded.</summary>
        public double CornerRounding
        {
            get => _cornerRounding;
            set { _cornerRounding = IrisParameters.ClampCornerRounding(value); OnPropertyChanged(); }
        }

        public bool Enabled
        {
            get => _enabled;
            set { _enabled = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 重ね方: how this symbol (all its copies together) is laid over the iris. Unknown numbers from a file become Normal.
        /// </summary>
        public LayerBlend Blend
        {
            get => _blend;
            set { _blend = Enum.IsDefined(value) ? value : LayerBlend.Normal; OnPropertyChanged(); }
        }

        public DecorativeSymbolItem Clone()
        {
            return new DecorativeSymbolItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = this.Type,
                Count = this.Count,
                Size = this.Size,
                PositionX = this.PositionX,
                PositionY = this.PositionY,
                Color = this.Color,
                Opacity = this.Opacity,
                Rotation = this.Rotation,
                CornerRounding = this.CornerRounding,
                Enabled = this.Enabled,
                Blend = this.Blend
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
