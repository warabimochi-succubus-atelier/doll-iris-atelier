using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using Xunit;

namespace DollIrisGenerator.Tests
{
    /// <summary>
    /// Temporary (docs/design/svg-canonical-rendering.md, steps 2–5): while the screen still draws with
    /// WpfIrisRenderer, the PDF / PNG images (the SVG drawn by resvg, RasterExporter) must look the same.
    /// Removed together with WpfIrisRenderer.
    /// Set DOLL_IRIS_PARITY_DIR to a folder to also write WPF | resvg | difference (×4) images there.
    /// </summary>
    public class SvgWpfParityTests
    {
        private const double Dpi = 600.0;

        /// <summary>
        /// Mean channel difference (0–255) allowed per eye: edge anti-aliasing only (dense glitter reaches about 1.1).
        /// A real mismatch (a missing layer, another gradient) gave 2.8–5.
        /// </summary>
        private const double MaxMeanDiff = 1.5;

        public static IEnumerable<object[]> Presets() =>
            ColorPresets.All.Select((p, i) => new object[] { i, p.Name });

        [Theory]
        [MemberData(nameof(Presets))]
        public void ExportedSvg_LooksLikeTheScreenRenderer(int index, string name)
        {
            var p = new IrisParameters();
            ColorPresets.ApplyPreset(p, ColorPresets.All[index]);
            p.SizeMm = 10.0;

            foreach (bool right in new[] { false, true })
            {
                double mean = Compare(p, right, $"{index:00}_{(right ? "R" : "L")}");
                Assert.True(mean <= MaxMeanDiff, $"{name} ({(right ? "right" : "left")}): mean difference {mean:F2} > {MaxMeanDiff}");
            }
        }

        /// <summary>Settings no preset uses: odd eye, tilt, oval, softness at its maximum, gradients of every kind.</summary>
        [Fact]
        public void ExportedSvg_LooksLikeTheScreenRenderer_Extremes()
        {
            var p = new IrisParameters
            {
                SizeMm = 10.0,
                Shape = IrisShape.VerticalOval,
                AspectRatio = 1.25,
                TiltAngle = 12,
                PupilSoftness = 0.3,
                LimbalRingSoftness = 0.8,
                LimbalRingThickness = 0.15,
                LensPatternEnabled = true,
                IsOddEye = true,
                OddEyeBaseColor = "#C2185B",
                PupilColor = "linear-gradient(30deg, #000000 0%, #6A1B9A 100%)",
                LimbalRingColor = "radial-gradient(#0D47A1 0%, #000000 100%)",
                RayColor = "conic-gradient(from 270deg, #FF0000 0%, #00FF00 50%, #FF0000 100%)",
            };
            foreach (bool right in new[] { false, true })
            {
                double mean = Compare(p, right, $"extreme_{(right ? "R" : "L")}");
                Assert.True(mean <= MaxMeanDiff, $"extremes ({(right ? "right" : "left")}): mean difference {mean:F2} > {MaxMeanDiff}");
            }
        }

        private static double Compare(IrisParameters p, bool right, string tag)
        {
            var svgImage = RasterExporter.RenderSingleToBitmap(p, right, Dpi); // resvg, Pbgra32, white
            int w = svgImage.PixelWidth, h = svgImage.PixelHeight;
            var b = new byte[w * h * 4];
            svgImage.CopyPixels(b, w * 4, 0);

            var a = new byte[w * h * 4];
            RunOnSta(() => RenderWithWpf(p, right, w, h).CopyPixels(a, w * 4, 0));

            long sum = 0;
            var diff = new byte[a.Length];
            for (int i = 0; i < a.Length; i += 4)
            {
                int m = 0;
                for (int c = 0; c < 3; c++)
                {
                    int d = Math.Abs(a[i + c] - b[i + c]);
                    sum += d;
                    if (d > m) m = d;
                }
                byte v = (byte)Math.Min(255, m * 4);
                diff[i] = diff[i + 1] = diff[i + 2] = v;
                diff[i + 3] = 255;
            }

            string? dir = Environment.GetEnvironmentVariable("DOLL_IRIS_PARITY_DIR");
            if (!string.IsNullOrEmpty(dir)) SaveRow(Path.Combine(dir, tag + ".png"), w, h, a, b, diff);

            return sum / (3.0 * w * h);
        }

        /// <summary>The same canvas drawn by the screen renderer (what RasterExporter did before it used resvg).</summary>
        private static BitmapSource RenderWithWpf(IrisParameters p, bool right, int w, int h)
        {
            double dipPerMm = 96.0 / 25.4;
            var (footW, footH) = SheetLayout.GetEyeFootprintMm(p);
            double pad = RasterExporter.SingleEyePaddingMm;
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new System.Windows.Rect(0, 0, w * 96.0 / Dpi, h * 96.0 / Dpi));
                var center = new System.Windows.Point((footW / 2.0 + pad) * dipPerMm, (footH / 2.0 + pad) * dipPerMm);
                WpfIrisRenderer.RenderIris(dc, center, p.SizeMm / 2.0, dipPerMm, p, isRightEye: right);
            }
            var rtb = new RenderTargetBitmap(w, h, Dpi, Dpi, PixelFormats.Pbgra32);
            rtb.Render(visual);
            return rtb;
        }

        private static void SaveRow(string path, int w, int h, params byte[][] tiles)
        {
            const int gap = 8;
            int W = tiles.Length * w + (tiles.Length - 1) * gap;
            var all = new byte[W * h * 4];
            for (int i = 0; i < all.Length; i++) all[i] = 128;
            for (int t = 0; t < tiles.Length; t++)
                for (int y = 0; y < h; y++)
                    Buffer.BlockCopy(tiles[t], y * w * 4, all, (y * W + t * (w + gap)) * 4, w * 4);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(BitmapSource.Create(W, h, 96, 96, PixelFormats.Pbgra32, null, all, W * 4)));
            using var fs = File.Create(path);
            enc.Save(fs);
        }

        private static void RunOnSta(Action body)
        {
            Exception? failure = null;
            var thread = new System.Threading.Thread(() =>
            {
                try { body(); }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
