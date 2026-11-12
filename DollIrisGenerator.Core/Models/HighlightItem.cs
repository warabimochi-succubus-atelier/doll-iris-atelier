using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace DollIrisGenerator.Models
{
    public class HighlightItem : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString("N");
        private HighlightShape _shape = HighlightShape.Circle;
        private double _positionX = -0.35; // -1.0 to 1.0 (relative to iris radius)
        private double _positionY = -0.35; // -1.0 to 1.0
        private double _size = 0.22;       // relative to radius
        private double _aspectRatio = 1.0; // width/height for oval
        private double _rotation = 0.0;    // degrees
        private string _color = "#FFFFFF";
        private double _opacity = 0.90;
        private bool _enabled = true;
        private LayerBlend _blend = LayerBlend.Normal;

        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public HighlightShape Shape
        {
            get => _shape;
            set { _shape = value; OnPropertyChanged(); }
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

        public double Size
        {
            get => _size;
            set { _size = Math.Clamp(value, 0.01, 1.0); OnPropertyChanged(); }
        }

        public double AspectRatio
        {
            get => _aspectRatio;
            set { _aspectRatio = Math.Clamp(value, 0.1, 5.0); OnPropertyChanged(); }
        }

        public double Rotation
        {
            get => _rotation;
            set { _rotation = Math.Clamp(value, -360.0, 360.0); OnPropertyChanged(); }
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

        public bool Enabled
        {
            get => _enabled;
            set { _enabled = value; OnPropertyChanged(); }
        }

        /// <summary>重ね方: how this highlight is laid over the eye. Unknown numbers from a file become Normal.</summary>
        public LayerBlend Blend
        {
            get => _blend;
            set { _blend = Enum.IsDefined(value) ? value : LayerBlend.Normal; OnPropertyChanged(); }
        }

        public HighlightItem Clone()
        {
            return new HighlightItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Shape = this.Shape,
                PositionX = this.PositionX,
                PositionY = this.PositionY,
                Size = this.Size,
                AspectRatio = this.AspectRatio,
                Rotation = this.Rotation,
                Color = this.Color,
                Opacity = this.Opacity,
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
