using System;
using System.Globalization;

namespace DollIrisGenerator.Services
{
    /// <summary>Which sliders the color palette popup shows (remembered in the window settings).</summary>
    public enum ColorInputMode
    {
        Rgb,
        Cmyk
    }

    /// <summary>
    /// A CMYK color in percent (0-100 each), used only as another way to enter an sRGB color.
    /// The stored value stays "#RRGGBB"; this is the plain device-independent formula without a color
    /// profile, so it is a rough guide and does not match how a printer actually mixes ink.
    ///
    /// <para>Rounding: the values are kept as doubles (not rounded to whole percents), so
    /// RGB → CMYK → RGB gives back exactly the same color. Whole percents alone could not: K in 1 % steps has
    /// only 101 grays for 256. The UI shows them rounded and keeps the held values while they still
    /// convert to the current color (<see cref="Resolve"/>), so the numbers do not jump while you drag.</para>
    /// </summary>
    public readonly record struct CmykColor(double C, double M, double Y, double K)
    {
        public const double Max = 100.0;

        /// <summary>sRGB (0-255) to CMYK percent: K = 1 - max(R,G,B), C = (1 - R - K) / (1 - K), etc. Black is 0/0/0/100.</summary>
        public static CmykColor FromRgb(byte r, byte g, byte b)
        {
            double rf = r / 255.0, gf = g / 255.0, bf = b / 255.0;
            double max = Math.Max(rf, Math.Max(gf, bf));
            double k = 1.0 - max;
            if (max <= 0.0) return new CmykColor(0, 0, 0, Max);
            return new CmykColor(
                (max - rf) / max * Max,
                (max - gf) / max * Max,
                (max - bf) / max * Max,
                k * Max);
        }

        /// <summary>CMYK percent to sRGB (0-255, rounded): R = 255 × (1 - C) × (1 - K), etc. Out-of-range values are clamped.</summary>
        public (byte R, byte G, byte B) ToRgb()
        {
            double k = Clamp01(K);
            return (Channel(C, k), Channel(M, k), Channel(Y, k));
        }

        /// <summary>"#RRGGBB" of <see cref="ToRgb"/>.</summary>
        public string ToHex()
        {
            var (r, g, b) = ToRgb();
            return $"#{r:X2}{g:X2}{b:X2}";
        }

        /// <summary>Converts to the same RGB color (exact match after rounding).</summary>
        public bool Matches(byte r, byte g, byte b)
        {
            var (cr, cg, cb) = ToRgb();
            return cr == r && cg == g && cb == b;
        }

        /// <summary>
        /// The CMYK to show for an RGB color: <paramref name="held"/> (what the user last entered) as long as it
        /// still gives that color, otherwise freshly converted. Keeps e.g. C/M/Y at K = 100 % and the exact
        /// percent the user typed, instead of re-deriving slightly different numbers from the rounded RGB.
        /// </summary>
        public static CmykColor Resolve(CmykColor? held, byte r, byte g, byte b)
            => held is { } h && h.Matches(r, g, b) ? h : FromRgb(r, g, b);

        /// <summary>A copy with one channel (0 = C, 1 = M, 2 = Y, 3 = K) set, clamped to 0-100.</summary>
        public CmykColor With(int channel, double percent)
        {
            double v = Math.Clamp(double.IsNaN(percent) ? 0.0 : percent, 0.0, Max);
            return channel switch
            {
                0 => this with { C = v },
                1 => this with { M = v },
                2 => this with { Y = v },
                3 => this with { K = v },
                _ => throw new ArgumentOutOfRangeException(nameof(channel))
            };
        }

        /// <summary>Channel value by index (0 = C, 1 = M, 2 = Y, 3 = K).</summary>
        public double this[int channel] => channel switch
        {
            0 => C,
            1 => M,
            2 => Y,
            3 => K,
            _ => throw new ArgumentOutOfRangeException(nameof(channel))
        };

        /// <summary>Whole percent for the value boxes ("33" for 33.3).</summary>
        public static string FormatPercent(double percent)
            => Math.Round(percent, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);

        /// <summary>Parses "50", "50.5", "50,5" or "50%"; false for anything else. The result is clamped to 0-100.</summary>
        public static bool TryParsePercent(string? text, out double percent)
        {
            percent = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string t = text.Trim().TrimEnd('%', '％').Trim().Replace(',', '.');
            if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) || double.IsNaN(v) || double.IsInfinity(v))
                return false;
            percent = Math.Clamp(v, 0.0, Max);
            return true;
        }

        private static byte Channel(double percent, double k)
            => (byte)Math.Round(255.0 * (1.0 - Clamp01(percent)) * (1.0 - k), MidpointRounding.AwayFromZero);

        private static double Clamp01(double percent)
            => double.IsNaN(percent) ? 0.0 : Math.Clamp(percent / Max, 0.0, 1.0);
    }
}
