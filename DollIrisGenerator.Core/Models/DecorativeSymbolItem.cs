using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace DollIrisGenerator.Models
{
    public class DecorativeSymbolItem : IArrangeableItem<DecorativeSymbolItem>
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
        private double _stretchTop = 1.0;    // 上下左右の長さ: each half of the shape stretched from its center (1 = as drawn)
        private double _stretchBottom = 1.0;
        private double _stretchLeft = 1.0;
        private double _stretchRight = 1.0;
        private bool _enabled = false;
        private LayerBlend _blend = LayerBlend.Normal;

        /// <summary>The slider range of the four lengths (上下左右の長さ); files may go further, up to <see cref="ClampStretch"/>.</summary>
        public const double MinStretchSlider = 0.5, MaxStretchSlider = 2.0;
        private bool _isSelected;

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

        /// <summary>
        /// 上の長さ: the part of the shape above its center, stretched by this (1 = as drawn). The right eye is the
        /// mirror image, so there 左 and 右 trade places (as the positions and copies do).
        /// </summary>
        public double StretchTop
        {
            get => _stretchTop;
            set { _stretchTop = ClampStretch(value); OnPropertyChanged(); }
        }

        /// <summary>下の長さ (see <see cref="StretchTop"/>).</summary>
        public double StretchBottom
        {
            get => _stretchBottom;
            set { _stretchBottom = ClampStretch(value); OnPropertyChanged(); }
        }

        /// <summary>左の長さ (see <see cref="StretchTop"/>).</summary>
        public double StretchLeft
        {
            get => _stretchLeft;
            set { _stretchLeft = ClampStretch(value); OnPropertyChanged(); }
        }

        /// <summary>右の長さ (see <see cref="StretchTop"/>).</summary>
        public double StretchRight
        {
            get => _stretchRight;
            set { _stretchRight = ClampStretch(value); OnPropertyChanged(); }
        }

        /// <summary>The longest of the four lengths: how far past its size the shape can reach.</summary>
        [JsonIgnore]
        public double MaxStretch => Math.Max(Math.Max(StretchTop, StretchBottom), Math.Max(StretchLeft, StretchRight));

        /// <summary>0.25 to 3 (NaN becomes 1). Wider than the sliders, as with the other settings.</summary>
        public static double ClampStretch(double value) => double.IsNaN(value) ? 1.0 : Math.Clamp(value, 0.25, 3.0);

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

        /// <inheritdoc cref="IArrangeableItem.IsSelected"/>
        [JsonIgnore]
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected == value) return; _isSelected = value; OnPropertyChanged(); }
        }

        public static IReadOnlyList<string> BulkSettingNames { get; } = new[]
        {
            nameof(Enabled), nameof(Type), nameof(Color), nameof(Size), nameof(Count),
            nameof(Rotation), nameof(CornerRounding),
            nameof(StretchTop), nameof(StretchBottom), nameof(StretchLeft), nameof(StretchRight),
            nameof(Opacity), nameof(Blend)
        };

        public bool CopySettingTo(DecorativeSymbolItem target, string propertyName)
        {
            switch (propertyName)
            {
                case nameof(Enabled): target.Enabled = Enabled; return true;
                case nameof(Type): target.Type = Type; return true;
                case nameof(Color): target.Color = Color; return true;
                case nameof(Size): target.Size = Size; return true;
                case nameof(Count): target.Count = Count; return true;
                case nameof(Rotation): target.Rotation = Rotation; return true;
                case nameof(CornerRounding): target.CornerRounding = CornerRounding; return true;
                case nameof(StretchTop): target.StretchTop = StretchTop; return true;
                case nameof(StretchBottom): target.StretchBottom = StretchBottom; return true;
                case nameof(StretchLeft): target.StretchLeft = StretchLeft; return true;
                case nameof(StretchRight): target.StretchRight = StretchRight; return true;
                case nameof(Opacity): target.Opacity = Opacity; return true;
                case nameof(Blend): target.Blend = Blend; return true;
                default: return false;
            }
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
                StretchTop = this.StretchTop,
                StretchBottom = this.StretchBottom,
                StretchLeft = this.StretchLeft,
                StretchRight = this.StretchRight,
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
