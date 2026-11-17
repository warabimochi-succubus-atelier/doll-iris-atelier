using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace DollIrisGenerator.Models
{
    public class HighlightItem : IArrangeableItem<HighlightItem>, IStretchable
    {
        private string _id = Guid.NewGuid().ToString("N");
        private HighlightShape _shape = HighlightShape.Circle;
        private double _positionX = -0.35; // -1.0 to 1.0 (relative to iris radius)
        private double _positionY = -0.35; // -1.0 to 1.0
        private double _size = 0.22;       // relative to radius
        private int _count = 1;            // 繰り返し数: copies on a ring round the position (as the symbols' Count)
        private double _starCoreScale = 1.0; // 星の芯の太さ (1 = the standard star)
        private double _cornerRounding = 0.0; // 角の丸み of the 4点星 / 菱形 (a circle has no corners)
        private double _rotation = 0.0;    // degrees
        private string _color = "#FFFFFF";
        private double _opacity = 0.90;
        private double _stretchTop = 1.0;    // 上下左右の長さ (1 = as drawn); an oval is a circle stretched sideways
        private double _stretchBottom = 1.0;
        private double _stretchLeft = 1.0;
        private double _stretchRight = 1.0;
        private bool _enabled = true;
        private LayerBlend _blend = LayerBlend.Normal;
        private RightEyeFlip _rightEyeFlip = RightEyeFlip.Mirror; // 右目の反転
        private bool _isSelected;

        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        /// <summary>Unknown numbers from a file (such as 1, the oval that is now a stretched circle) become a circle.</summary>
        public HighlightShape Shape
        {
            get => _shape;
            set { _shape = Enum.IsDefined(value) ? value : HighlightShape.Circle; OnPropertyChanged(); }
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

        /// <summary>
        /// 繰り返し数: 1 to <see cref="DecorativeSymbolItem.MaxCount"/>. With 2 or more, the copies sit evenly on a circle
        /// of radius size × 1.4 round the position (mirrored with the highlight), as the decorative symbols' copies do.
        /// </summary>
        public int Count
        {
            get => _count;
            set { _count = Math.Clamp(value, 1, DecorativeSymbolItem.MaxCount); OnPropertyChanged(); } // bounded: drawing loops over this
        }

        /// <summary>How much the tips / corners of the 4点星 and 菱形 are rounded: 0 = sharp (as before) to 1 = fully rounded.</summary>
        public double CornerRounding
        {
            get => _cornerRounding;
            set { _cornerRounding = IrisParameters.ClampCornerRounding(value); OnPropertyChanged(); }
        }

        /// <summary>
        /// 星の芯の太さ: the 4芒星 / 5芒星's inner radius as a share of the standard one (1 = as before); other shapes
        /// ignore it. See <see cref="StarCore"/>.
        /// </summary>
        public double StarCoreScale
        {
            get => _starCoreScale;
            set { _starCoreScale = StarCore.Clamp(value); OnPropertyChanged(); }
        }

        /// <summary>
        /// 上の長さ: the part of the shape above its center, stretched by this (1 = as drawn). When the right eye's
        /// highlight's shape is mirrored (<see cref="RightEyeFlip"/> 鏡像), 左 and 右 trade places there.
        /// </summary>
        public double StretchTop
        {
            get => _stretchTop;
            set { _stretchTop = ShapeStretch.Clamp(value); OnPropertyChanged(); }
        }

        /// <summary>下の長さ (see <see cref="StretchTop"/>).</summary>
        public double StretchBottom
        {
            get => _stretchBottom;
            set { _stretchBottom = ShapeStretch.Clamp(value); OnPropertyChanged(); }
        }

        /// <summary>左の長さ (see <see cref="StretchTop"/>).</summary>
        public double StretchLeft
        {
            get => _stretchLeft;
            set { _stretchLeft = ShapeStretch.Clamp(value); OnPropertyChanged(); }
        }

        /// <summary>右の長さ (see <see cref="StretchTop"/>).</summary>
        public double StretchRight
        {
            get => _stretchRight;
            set { _stretchRight = ShapeStretch.Clamp(value); OnPropertyChanged(); }
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

        /// <summary>
        /// 右目の反転: 鏡像 (position and direction mirrored, as before), 位置だけ反転 (placed mirrored, facing the same
        /// way — a moon looks the same in both eyes) or 反転しない (the same as in the left eye). Unknown numbers: 鏡像.
        /// </summary>
        public RightEyeFlip RightEyeFlip
        {
            get => _rightEyeFlip;
            set { _rightEyeFlip = RightEyeFlips.Valid(value); OnPropertyChanged(); }
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
            nameof(Enabled), nameof(Shape), nameof(Color), nameof(Size), nameof(Count),
            nameof(StretchTop), nameof(StretchBottom), nameof(StretchLeft), nameof(StretchRight),
            nameof(Rotation), nameof(CornerRounding), nameof(StarCoreScale), nameof(Opacity), nameof(Blend), nameof(RightEyeFlip)
        };

        public bool CopySettingTo(HighlightItem target, string propertyName)
        {
            switch (propertyName)
            {
                case nameof(Enabled): target.Enabled = Enabled; return true;
                case nameof(Shape): target.Shape = Shape; return true;
                case nameof(Color): target.Color = Color; return true;
                case nameof(Size): target.Size = Size; return true;
                case nameof(Count): target.Count = Count; return true;
                case nameof(CornerRounding): target.CornerRounding = CornerRounding; return true;
                case nameof(StarCoreScale): target.StarCoreScale = StarCoreScale; return true;
                case nameof(StretchTop): target.StretchTop = StretchTop; return true;
                case nameof(StretchBottom): target.StretchBottom = StretchBottom; return true;
                case nameof(StretchLeft): target.StretchLeft = StretchLeft; return true;
                case nameof(StretchRight): target.StretchRight = StretchRight; return true;
                case nameof(Rotation): target.Rotation = Rotation; return true;
                case nameof(Opacity): target.Opacity = Opacity; return true;
                case nameof(Blend): target.Blend = Blend; return true;
                case nameof(RightEyeFlip): target.RightEyeFlip = RightEyeFlip; return true;
                default: return false;
            }
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
                Count = this.Count,
                CornerRounding = this.CornerRounding,
                StarCoreScale = this.StarCoreScale,
                StretchTop = this.StretchTop,
                StretchBottom = this.StretchBottom,
                StretchLeft = this.StretchLeft,
                StretchRight = this.StretchRight,
                Rotation = this.Rotation,
                Color = this.Color,
                Opacity = this.Opacity,
                Enabled = this.Enabled,
                Blend = this.Blend,
                RightEyeFlip = this.RightEyeFlip
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
