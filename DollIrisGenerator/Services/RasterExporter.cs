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
        public static RenderTargetBitmap RenderPairToBitmap(
            IrisParameters p,
            double dpi = 1200.0,
            double pairSpacingMm = 4.0,
            bool showCutLines = true,
            bool showCenterMarks = true)
        {
            // Drawing coordinates are WPF DIPs (1/96 inch); RenderTargetBitmap scales them to the target DPI.
            // (Drawing in pixel units here would be scaled a second time and land off-canvas.)
            double pixelsPerMm = dpi / 25.4;
            double dipPerMm = 96.0 / 25.4;

            // Canvas holds the eye's real footprint (bigger than its size when an oval is tilted)
            var (widthMm, heightMm) = SheetLayout.GetEyeFootprintMm(p);

            double paddingMm = 2.0;
            double totalWidthMm = (widthMm * 2.0) + pairSpacingMm + (paddingMm * 2.0);
            double totalHeightMm = heightMm + (paddingMm * 2.0);

            int pixelWidth = (int)Math.Ceiling(totalWidthMm * pixelsPerMm);
            int pixelHeight = (int)Math.Ceiling(totalHeightMm * pixelsPerMm);

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, pixelWidth * 96.0 / dpi, pixelHeight * 96.0 / dpi));

                double rxMm = widthMm / 2.0;
                double cyPx = (totalHeightMm / 2.0) * dipPerMm;

                // Left Eye
                double leftCxPx = (paddingMm + rxMm) * dipPerMm;
                WpfIrisRenderer.RenderIris(
                    dc,
                    new Point(leftCxPx, cyPx),
                    p.SizeMm / 2.0,
                    dipPerMm,
                    p,
                    isRightEye: false,
                    showCutLine: showCutLines,
                    showCenterMark: showCenterMarks
                );

                // Right Eye
                double rightCxPx = (paddingMm + rxMm + rxMm + pairSpacingMm + rxMm) * dipPerMm;
                WpfIrisRenderer.RenderIris(
                    dc,
                    new Point(rightCxPx, cyPx),
                    p.SizeMm / 2.0,
                    dipPerMm,
                    p,
                    isRightEye: true,
                    showCutLine: showCutLines,
                    showCenterMark: showCenterMarks
                );
            }

            var rtb = new RenderTargetBitmap(pixelWidth, pixelHeight, dpi, dpi, PixelFormats.Pbgra32);
            rtb.Render(visual);
            return rtb;
        }

        public static RenderTargetBitmap RenderSingleToBitmap(
            IrisParameters p,
            bool isRightEye,
            double dpi = 1200.0,
            bool showCutLines = false,
            bool showCenterMarks = false,
            bool whiteBackground = true)
        {
            // Drawing coordinates are WPF DIPs (1/96 inch); RenderTargetBitmap scales them to the target DPI.
            // (Drawing in pixel units here would be scaled a second time and land off-canvas.)
            double pixelsPerMm = dpi / 25.4;
            double dipPerMm = 96.0 / 25.4;

            // Canvas holds the eye's real footprint (bigger than its size when an oval is tilted)
            var (widthMm, heightMm) = SheetLayout.GetEyeFootprintMm(p);

            double paddingMm = 1.0;
            double totalWidthMm = widthMm + (paddingMm * 2.0);
            double totalHeightMm = heightMm + (paddingMm * 2.0);

            int pixelWidth = (int)Math.Ceiling(totalWidthMm * pixelsPerMm);
            int pixelHeight = (int)Math.Ceiling(totalHeightMm * pixelsPerMm);

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                if (whiteBackground)
                {
                    dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, pixelWidth * 96.0 / dpi, pixelHeight * 96.0 / dpi));
                }
                else
                {
                    dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, pixelWidth * 96.0 / dpi, pixelHeight * 96.0 / dpi));
                }

                double cxPx = (totalWidthMm / 2.0) * dipPerMm;
                double cyPx = (totalHeightMm / 2.0) * dipPerMm;

                WpfIrisRenderer.RenderIris(
                    dc,
                    new Point(cxPx, cyPx),
                    p.SizeMm / 2.0,
                    dipPerMm,
                    p,
                    isRightEye: isRightEye,
                    showCutLine: showCutLines,
                    showCenterMark: showCenterMarks
                );
            }

            var rtb = new RenderTargetBitmap(pixelWidth, pixelHeight, dpi, dpi, PixelFormats.Pbgra32);
            rtb.Render(visual);
            return rtb;
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
