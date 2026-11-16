using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DollIrisGenerator.Models;

namespace DollIrisGenerator.Services
{
    public static class RasterExporter
    {
        // PNG and PDF images are pictures of the exported SVG, drawn by resvg (docs/design/svg-canonical-rendering.md).
        // The SVG is laid out in mm at its real size; the bitmap is that size rounded up to whole pixels.

        /// <summary>Padding around the pair in the PNG export (mm).</summary>
        public const double PairPaddingMm = 2.0;

        /// <summary>Padding around one eye in the PDF images (mm); PdfExporter places the image by it.</summary>
        public const double SingleEyePaddingMm = 1.0;

        /// <summary>The left and right eye (transparent background), as exported to PNG.</summary>
        public static BitmapSource RenderPairToBitmap(
            IrisParameters p,
            double dpi = 1200.0,
            double pairSpacingMm = 4.0,
            bool showCutLines = true,
            bool showCenterMarks = true)
        {
            var (widthMm, heightMm) = SheetLayout.GetEyeFootprintMm(p);
            double totalWidthMm = (widthMm * 2.0) + pairSpacingMm + (PairPaddingMm * 2.0);
            double totalHeightMm = heightMm + (PairPaddingMm * 2.0);

            string svg = SvgExporter.GeneratePairSvg(p, showCutLines, showCenterMarks, pairSpacingMm, PairPaddingMm);
            return Rasterize(svg, totalWidthMm, totalHeightMm, dpi, background: null);
        }

        /// <summary>One eye on its tilted footprint plus <see cref="SingleEyePaddingMm"/> each side (for the PDF).</summary>
        public static BitmapSource RenderSingleToBitmap(
            IrisParameters p,
            bool isRightEye,
            double dpi = 1200.0,
            bool showCutLines = false,
            bool showCenterMarks = false,
            bool whiteBackground = true)
        {
            var (widthMm, heightMm) = SheetLayout.GetEyeFootprintMm(p);
            double totalWidthMm = widthMm + (SingleEyePaddingMm * 2.0);
            double totalHeightMm = heightMm + (SingleEyePaddingMm * 2.0);

            string svg = SvgExporter.GenerateSingleEyeSvg(p, isRightEye, showCutLines, showCenterMarks, SingleEyePaddingMm);
            return Rasterize(svg, totalWidthMm, totalHeightMm, dpi, whiteBackground ? Colors.White : null);
        }

        private static BitmapSource Rasterize(string svg, double widthMm, double heightMm, double dpi, Color? background)
        {
            double pixelsPerMm = dpi / 25.4;
            int pixelWidth = (int)Math.Ceiling(widthMm * pixelsPerMm);
            int pixelHeight = (int)Math.Ceiling(heightMm * pixelsPerMm);
            return ResvgRasterizer.RenderAtDpi(svg, dpi, pixelWidth, pixelHeight, background);
        }

        public static void SaveBitmapToPng(BitmapSource bitmap, string filePath)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(filePath);
            encoder.Save(stream);
        }

        public static byte[] SaveBitmapToPngBytes(BitmapSource bitmap)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            return stream.ToArray();
        }

        public static byte[] SaveBitmapToJpegBytes(BitmapSource bitmap, int quality = 98)
        {
            var encoder = new JpegBitmapEncoder { QualityLevel = quality };
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            return stream.ToArray();
        }

        /// <summary>
        /// Renders a one-line text label (dark gray on white) for the PDF sheet. Used for labels the PDF's
        /// built-in Latin font cannot show, such as Japanese design names.
        /// </summary>
        public static RenderTargetBitmap RenderLabelToBitmap(string text, double fontSizePt, double dpi = 600.0)
        {
            double fontSizeDip = fontSizePt * 96.0 / 72.0;
            var formatted = new FormattedText(
                text,
                System.Globalization.CultureInfo.GetCultureInfo("ja-JP"),
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Yu Gothic UI, Meiryo UI, Meiryo, Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                fontSizeDip,
                new SolidColorBrush(Color.FromRgb(102, 102, 102)), // same gray as the vector labels (0.4)
                dpi / 96.0);

            double padDip = fontSizeDip * 0.15;
            double wDip = Math.Ceiling(formatted.WidthIncludingTrailingWhitespace + padDip * 2);
            double hDip = Math.Ceiling(formatted.Height + padDip * 2);

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, wDip, hDip)); // JPEG has no alpha
                dc.DrawText(formatted, new Point(padDip, padDip));
            }

            int wPx = Math.Max(1, (int)Math.Ceiling(wDip * dpi / 96.0));
            int hPx = Math.Max(1, (int)Math.Ceiling(hDip * dpi / 96.0));
            var bmp = new RenderTargetBitmap(wPx, hPx, dpi, dpi, PixelFormats.Pbgra32);
            bmp.Render(visual);
            bmp.Freeze();
            return bmp;
        }
    }
}
