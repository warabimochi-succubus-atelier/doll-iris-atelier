using System.Linq;
using System.Text.RegularExpressions;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using Xunit;

namespace DollIrisGenerator.Tests
{
    /// <summary>繊維ノイズ: feTurbulence strips round the iris, overlay-blended, faded at the pupil and the rim.</summary>
    public class FiberNoiseTests
    {
        [Fact]
        public void OffByDefaultAndExportsNothing()
        {
            var p = new IrisParameters();
            Assert.Equal(0.0, p.FiberNoise);
            Assert.Equal(1.0, p.FiberNoiseScale);
            string svg = SvgExporter.GenerateSingleEyeSvg(p, false);
            Assert.DoesNotContain("feTurbulence", svg);
            Assert.DoesNotContain("_fibers", svg);
        }

        [Fact]
        public void StripsOfTurbulenceCoverTheIris()
        {
            var p = new IrisParameters { FiberNoise = 0.5 };
            string svg = SvgExporter.GenerateSingleEyeSvg(p, false);
            Assert.True(SvgValidator.IsWellFormed(svg, out var error), error);

            Assert.Equal(SvgExporter.FiberStripCount, Regex.Matches(svg, "<feTurbulence ").Count);
            Assert.Equal(SvgExporter.FiberStripCount, Regex.Matches(svg, "<rect x=\"0\" [^>]*filter=\"url\\(#eye_left_fibers_f\\d+\\)\"").Count);
            Assert.Contains("<g id=\"eye_left_fibers\" transform=\"translate(", svg);
            Assert.Contains("style=\"mix-blend-mode:overlay\" opacity=\"0.400\"", svg); // 0.5 × 0.8
            Assert.Contains("<g mask=\"url(#eye_left_fibers_mask)\">", svg);

            // Under the shading, above the body layers
            Assert.True(svg.IndexOf("eye_left_fibers\"", System.StringComparison.Ordinal) < svg.IndexOf("eye_left_upper_shadow", System.StringComparison.Ordinal));
        }

        [Fact]
        public void FinenessScalesTheFrequencies()
        {
            string Freq(double scale) => Regex.Match(
                SvgExporter.GenerateSingleEyeSvg(new IrisParameters { FiberNoise = 1, FiberNoiseScale = scale }, false),
                "baseFrequency=\"([^\"]+)\"").Groups[1].Value;
            Assert.Equal("1.750 30.000", Freq(1.0));
            Assert.Equal("3.500 60.000", Freq(2.0));
        }

        /// <summary>The right eye is the mirror image; the odd eye gets other seeds (another pattern).</summary>
        [Fact]
        public void RightEyeMirrorsAndOddEyeDiffers()
        {
            var p = new IrisParameters { FiberNoise = 1 };
            string Seeds(string svg) => string.Join(",", Regex.Matches(svg, "seed=\"(\\d+)\"").Select(m => m.Groups[1].Value));

            string left = SvgExporter.GenerateSingleEyeSvg(p, false);
            string right = SvgExporter.GenerateSingleEyeSvg(p, true);
            Assert.Matches("eye_right_fibers\" transform=\"translate\\([\\d.]+ [\\d.]+\\) scale\\(-", right);
            Assert.Equal(Seeds(left), Seeds(right));

            p.IsOddEye = true;
            p.OddEyeSeedOffset = 5;
            Assert.NotEqual(Seeds(left), Seeds(SvgExporter.GenerateSingleEyeSvg(p, true)));
        }

        /// <summary>Drawn (resvg): the noise changes the iris body, and is not on the pupil.</summary>
        [Fact]
        public void TheNoiseShowsOnTheIrisBodyOnly()
        {
            byte[] Pixels(double noise)
            {
                var p = new IrisParameters { SizeMm = 10, FiberNoise = noise, RayCount = 0, ParticleCount = 0, PupilSizeRatio = 0.3 };
                p.Highlights.Clear();
                var bmp = RasterExporter.RenderSingleToBitmap(p, false, dpi: 300);
                var px = new byte[bmp.PixelWidth * bmp.PixelHeight * 4];
                bmp.CopyPixels(px, bmp.PixelWidth * 4, 0);
                return px;
            }
            var plain = Pixels(0);
            var noisy = Pixels(1);
            int w = (int)System.Math.Sqrt(plain.Length / 4); // square canvas

            int Diff(int x, int y)
            {
                int i = (y * w + x) * 4;
                return System.Math.Abs(plain[i] - noisy[i]) + System.Math.Abs(plain[i + 1] - noisy[i + 1]) + System.Math.Abs(plain[i + 2] - noisy[i + 2]);
            }
            int c = w / 2;
            Assert.Equal(0, Diff(c, c)); // pupil center: untouched

            // Along a ring through the iris body, most pixels change
            int changed = 0, total = 0;
            for (int a = 0; a < 360; a += 3)
            {
                double r = w * 0.3;
                int x = c + (int)(r * System.Math.Cos(a * System.Math.PI / 180)), y = c + (int)(r * System.Math.Sin(a * System.Math.PI / 180));
                total++;
                if (Diff(x, y) > 6) changed++;
            }
            Assert.True(changed > total * 0.6, $"only {changed} / {total} body pixels changed");
        }

        [Fact]
        public void PresetsAndCopiesKeepTheNoise()
        {
            var p = new IrisParameters { FiberNoise = 0.6, FiberNoiseScale = 1.4 };
            var q = new IrisParameters();
            ColorPresets.ApplyPreset(q, ColorPresets.FromDesign(p, "x", ""));
            Assert.Equal(0.6, q.FiberNoise);
            Assert.Equal(1.4, q.FiberNoiseScale);

            ColorPresets.ApplyPreset(q, ColorPresets.All[0]);
            Assert.Equal(0.0, q.FiberNoise);
            Assert.Equal(1.0, q.FiberNoiseScale);

            Assert.Equal(1.4, p.Clone().FiberNoiseScale);
            Assert.Equal(2.0, new IrisParameters { FiberNoiseScale = 9 }.FiberNoiseScale);
        }
    }
}
