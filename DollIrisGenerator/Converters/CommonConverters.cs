using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DollIrisGenerator.Converters
{
    public class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;
            return value.ToString() == parameter.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b && parameter is string paramString)
            {
                return Enum.Parse(targetType, paramString);
            }
            return Binding.DoNothing;
        }
    }

    public class DoubleToPercentageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d)
            {
                return $"{d * 100.0:F0}%";
            }
            return "0%";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    /// <summary>
    /// 「透明度」 sliders: the design keeps an opacity (1 = opaque), the slider shows 1 − opacity so that raising it
    /// makes the element clearer (0% opaque, 100% invisible), like the browser version. Writes back 1 − value,
    /// rounded to 4 decimals (the same as the browser version). Use with DoubleToPercentageConverter-style labels
    /// through <see cref="TransparencyPercentConverter"/>.
    /// </summary>
    public class OpacityToTransparencyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is double opacity ? 1.0 - opacity : 0.0;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is double transparency ? Math.Round(1.0 - transparency, 4) : Binding.DoNothing;
    }

    /// <summary>The 「透明度」 label: an opacity shown as its transparency in percent (opacity 0.85 → "15%").</summary>
    public class TransparencyPercentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is double opacity ? $"{(1.0 - opacity) * 100.0:F0}%" : "0%";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    /// <summary>
    /// Quick-size radio buttons: checked when the size equals the parameter (e.g. "18.0"); checking one sets that size.
    /// The parameter is parsed with the invariant culture so "18.0" means 18 mm on every Windows locale.
    /// </summary>
    public class SizeMatchesConverter : IValueConverter
    {
        private static double ParseSize(object parameter) =>
            double.Parse(parameter?.ToString() ?? "0", NumberStyles.Float, CultureInfo.InvariantCulture);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is double d && Math.Abs(d - ParseSize(parameter)) < 0.05;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && b ? ParseSize(parameter) : Binding.DoNothing;
        }
    }

    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(value is bool b && b);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(value is bool b && b);
        }
    }

    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isTrue = value is bool b && b;
            if (parameter is string s && s == "Inverse") isTrue = !isTrue;
            return isTrue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
