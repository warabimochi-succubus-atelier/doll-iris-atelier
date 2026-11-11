using System;
using System.Collections.Generic;

namespace DollIrisGenerator.Models
{
    /// <summary>
    /// Attribute groups that 「属性ランダム生成」 leaves unchanged. Size, shape, aspect ratio and tilt are always kept,
    /// so they are not listed here.
    /// <para>
    /// A color is kept when <see cref="Colors"/> is locked <b>or</b> when the part it belongs to is locked
    /// (e.g. the ray color is kept by either 色 or 放射模様), so whatever the user locked never changes.
    /// </para>
    /// Saved in windowsettings.json as names ("Colors, Pupil"), so the numeric values must not be reordered.
    /// </summary>
    [Flags]
    public enum RandomizeLocks
    {
        None = 0,
        /// <summary>配色: every color (iris, pupil, rays, limbal ring, glitter, lens, odd-eye side), the gradient ranges, the glow intensity and the design name.</summary>
        Colors = 1 << 0,
        /// <summary>乱数シード (the pattern jitter of rays and glitter).</summary>
        Seed = 1 << 1,
        /// <summary>瞳孔: shape, size, opacity, softness and color.</summary>
        Pupil = 1 << 2,
        /// <summary>放射模様: count, thickness, length, intensity, randomness, inner radius and color.</summary>
        Rays = 1 << 3,
        /// <summary>外輪: thickness, opacity, softness and color.</summary>
        LimbalRing = 1 << 4,
        /// <summary>ラメ: count, sizes, opacity, spread and color.</summary>
        Particles = 1 << 5,
        /// <summary>装飾記号: the whole symbol list.</summary>
        Symbols = 1 << 6,
        /// <summary>ハイライト: the whole highlight list.</summary>
        Highlights = 1 << 7,
        /// <summary>レンズ風: on/off and every lens parameter including its color.</summary>
        Lens = 1 << 8,
        /// <summary>オッドアイ: on/off, the odd-eye colors and its seed offset.</summary>
        OddEye = 1 << 9,

        All = Colors | Seed | Pupil | Rays | LimbalRing | Particles | Symbols | Highlights | Lens | OddEye
    }

    public static class RandomizeLocksInfo
    {
        /// <summary>Display order and Japanese labels, used for the status message.</summary>
        public static readonly IReadOnlyList<(RandomizeLocks Lock, string Label)> Groups = new[]
        {
            (RandomizeLocks.Colors, "色"),
            (RandomizeLocks.Seed, "シード"),
            (RandomizeLocks.Pupil, "瞳孔"),
            (RandomizeLocks.Rays, "放射模様"),
            (RandomizeLocks.LimbalRing, "外輪"),
            (RandomizeLocks.Particles, "ラメ"),
            (RandomizeLocks.Symbols, "装飾記号"),
            (RandomizeLocks.Highlights, "ハイライト"),
            (RandomizeLocks.Lens, "レンズ風"),
            (RandomizeLocks.OddEye, "オッドアイ")
        };

        /// <summary>Drops bits that are not a known group (hand-edited or newer settings files).</summary>
        public static RandomizeLocks Sanitize(RandomizeLocks locks) => locks & RandomizeLocks.All;

        /// <summary>「色・瞳孔」 style list of the locked groups, or an empty string when nothing is locked.</summary>
        public static string Describe(RandomizeLocks locks)
        {
            var names = new List<string>();
            foreach (var (flag, label) in Groups)
            {
                if (locks.HasFlag(flag)) names.Add(label);
            }
            return string.Join("・", names);
        }
    }
}
