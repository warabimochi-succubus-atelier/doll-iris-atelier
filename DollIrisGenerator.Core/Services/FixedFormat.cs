using System;
using System.Globalization;

namespace DollIrisGenerator.Services
{
    /// <summary>
    /// <c>value.ToString("F3", CultureInfo.InvariantCulture)</c> (and F1 / F2), fast.
    ///
    /// An SVG holds tens of thousands of numbers. The framework's formatter is fine on the desktop, but in the
    /// browser app (WebAssembly) each call costs tens of microseconds, which made one preview take most of a
    /// second. This gives exactly the same text: it rounds the scaled value to the nearest integer and falls back
    /// to the framework whenever the answer could differ (a value too close to a rounding tie for the product to
    /// tell, a value too large, NaN / infinity). FixedFormatTests checks it against the framework.
    /// </summary>
    public static class FixedFormat
    {
        private static readonly double[] Pow10 = { 1, 10, 100, 1000, 10000, 100000, 1000000 };
        private static readonly string[] Formats = { "F0", "F1", "F2", "F3", "F4", "F5", "F6" };

        /// <summary>Same text as <c>value.ToString("F" + decimals, CultureInfo.InvariantCulture)</c>.</summary>
        public static string ToFixed(this double value, int decimals)
        {
            if ((uint)decimals >= (uint)Pow10.Length) return value.ToString("F" + decimals, CultureInfo.InvariantCulture);
            if (!double.IsFinite(value)) return Slow(value, decimals);

            double scaled = Math.Abs(value) * Pow10[decimals];
            // Below 1e9 the product is off by at most ~6e-8, far less than the margin around a tie below
            if (scaled >= 1e9) return Slow(value, decimals);

            double whole = Math.Floor(scaled);
            double fraction = scaled - whole; // exact
            if (Math.Abs(fraction - 0.5) < 1e-6) return Slow(value, decimals);
            long units = (long)whole + (fraction > 0.5 ? 1 : 0);

            // .NET writes the sign of negative values (also -0 and values that round to 0): "-0.000"
            bool negative = double.IsNegative(value);

            Span<char> buffer = stackalloc char[24];
            int pos = buffer.Length;
            for (int i = 0; i < decimals; i++)
            {
                buffer[--pos] = (char)('0' + (int)(units % 10));
                units /= 10;
            }
            if (decimals > 0) buffer[--pos] = '.';
            do
            {
                buffer[--pos] = (char)('0' + (int)(units % 10));
                units /= 10;
            } while (units > 0);
            if (negative) buffer[--pos] = '-';
            return new string(buffer[pos..]);
        }

        private static string Slow(double value, int decimals) => value.ToString(Formats[decimals], CultureInfo.InvariantCulture);
    }
}
