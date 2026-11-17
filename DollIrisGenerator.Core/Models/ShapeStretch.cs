using System;

namespace DollIrisGenerator.Models
{
    /// <summary>
    /// 上下左右の長さ of a decorative symbol or a highlight: each half of the shape stretched from its center
    /// (1 = as drawn). See <see cref="Services.ShapeOutline.Stretched(double, double, double, double)"/>.
    /// </summary>
    public interface IStretchable
    {
        double StretchTop { get; }
        double StretchBottom { get; }
        double StretchLeft { get; }
        double StretchRight { get; }
    }

    /// <summary>Ranges and helpers shared by every <see cref="IStretchable"/> item.</summary>
    public static class ShapeStretch
    {
        /// <summary>The slider range (50% to 200%); files may go further, up to <see cref="Clamp"/>.</summary>
        public const double MinSlider = 0.5, MaxSlider = 2.0;

        /// <summary>0.25 to 3 (NaN becomes 1). Wider than the sliders, as with the other settings.</summary>
        public static double Clamp(double value) => double.IsNaN(value) ? 1.0 : Math.Clamp(value, 0.25, 3.0);

        /// <summary>The longest of the four lengths: how far past its size the shape can reach.</summary>
        public static double Max(IStretchable s) =>
            Math.Max(Math.Max(s.StretchTop, s.StretchBottom), Math.Max(s.StretchLeft, s.StretchRight));

        /// <summary>True when all four lengths are 1 (the shape is drawn as it is).</summary>
        public static bool IsNone(IStretchable s) =>
            s.StretchTop == 1.0 && s.StretchBottom == 1.0 && s.StretchLeft == 1.0 && s.StretchRight == 1.0;
    }

    /// <summary>
    /// 星の芯の太さ of a decorative symbol or highlight: the stars' inner (valley) radius as a share of the standard one
    /// for that star (1 = standard: 0.28 / 0.42 of the radius for the symbols' 4芒星 / 5芒星, 0.22 for the highlights' 4芒星).
    /// The other shapes ignore it.
    /// </summary>
    public static class StarCore
    {
        /// <summary>The slider range (50% to 200%); files may go further, up to <see cref="Clamp"/>.</summary>
        public const double MinSlider = 0.5, MaxSlider = 2.0;

        /// <summary>0.25 to 3 (NaN becomes 1).</summary>
        public static double Clamp(double value) => double.IsNaN(value) ? 1.0 : Math.Clamp(value, 0.25, 3.0);
    }
}
