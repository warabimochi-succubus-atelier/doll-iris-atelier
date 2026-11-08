using System.Globalization;

namespace DollIrisGenerator.Web.Editor;

/// <summary>
/// One control of the settings panel, editing a property of <typeparamref name="T"/> (the design, or one
/// decorative symbol / highlight). Labels and ranges follow the desktop app's settings tabs.
/// </summary>
public abstract record Field<T>(string Label, string? Hint = null);

/// <summary>Section heading inside a tab.</summary>
public sealed record HeadingField<T>(string Label, string? Hint = null) : Field<T>(Label, Hint);

/// <summary>Slider over a number. Integer properties use <see cref="Step"/> 1 and round in their setter.</summary>
public sealed record SliderField<T>(
    string Label,
    Func<T, double> Get,
    Action<T, double> Set,
    double Min,
    double Max,
    double Step,
    Func<double, string> Format,
    string? Hint = null) : Field<T>(Label, Hint);

/// <summary>A color setting: "#RRGGBB", or (when <see cref="AllowGradient"/>) a gradient in CSS-like notation.</summary>
public sealed record ColorField<T>(
    string Label,
    Func<T, string> Get,
    Action<T, string> Set,
    bool AllowGradient = false,
    string? Hint = null) : Field<T>(Label, Hint);

public sealed record ToggleField<T>(string Label, Func<T, bool> Get, Action<T, bool> Set, string? Hint = null) : Field<T>(Label, Hint);

public sealed record TextField<T>(string Label, Func<T, string> Get, Action<T, string> Set, string? Hint = null) : Field<T>(Label, Hint);

/// <summary>One of a few named values (an enum), shown as buttons.</summary>
public sealed record ChoiceField<T>(
    string Label,
    Func<T, object> Get,
    Action<T, object> Set,
    IReadOnlyList<(string Label, object Value)> Options,
    string? Hint = null) : Field<T>(Label, Hint);

/// <summary>Number formats the slider read-outs use.</summary>
public static class Fmt
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Percent(double v) => $"{Math.Round(v * 100.0).ToString("F0", Inv)}%";
    public static string Mm(double v) => $"{v.ToString("F1", Inv)} mm";
    public static string Deg(double v) => $"{v.ToString("F0", Inv)}°";
    public static string F1(double v) => v.ToString("F1", Inv);
    public static string F2(double v) => v.ToString("F2", Inv);
    public static string Count(double v, string unit) => $"{Math.Round(v).ToString("F0", Inv)} {unit}";
}
