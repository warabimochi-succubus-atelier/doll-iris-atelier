using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using DollIrisGenerator.Models;

namespace DollIrisGenerator.Services
{
    public static class PdfExporter
    {
        private const double PtPerMm = 72.0 / 25.4; // 2.83464566929 pt per mm
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static double MmToPt(double mm) => mm * PtPerMm;

        /// <summary>Resolution of the embedded eye images.</summary>
        private const double EyeImageDpi = 1200.0;

        public static void ExportA4SheetToPdf(
            string filePath,
            IReadOnlyList<SheetItem> items,
            PrintSheetConfig config)
        {
            // A4 Portrait dimensions in points
            double pageW_Pt = MmToPt(210.0); // 595.2756
            double pageH_Pt = MmToPt(297.0); // 841.8898

            double pageW_Mm = 210.0;
            double pageH_Mm = 297.0;

            // Shared imposition (identical to the A4 preview). Cells are sized from the largest
            // design on the sheet so mixed sizes never overlap. An empty sheet yields one default pair.
            var layout = new SheetLayout(items, config);
            var pairList = layout.Pairs;
            int cols = layout.Columns;
            int rows = layout.Rows;
            int pairIndex = 0;

            // PDF Structure Builder
            using var fileStream = File.Create(filePath);
            var writer = new PdfWriter(fileStream);

            // Reusable cache for rasterized images (Key -> (ImageObjId, WidthPx, HeightPx))
            var imageCache = new Dictionary<string, (int ObjId, int W, int H)>();

            // Rendered non-ASCII labels (label text -> image object and size in points)
            var labelCache = new Dictionary<string, (int ObjId, double W_Pt, double H_Pt)>();

            var pageObjectIds = new List<int>();

            while (pairIndex < pairList.Count)
            {
                int startPairIndex = pairIndex;
                int pageObjId = writer.AllocateObjectId();
                pageObjectIds.Add(pageObjId);

                // Collect page contents and resources
                var pageContentSb = new StringBuilder();
                var pageImages = new Dictionary<string, int>(); // ResourceName -> ImageObjId

                // Header comment
                pageContentSb.AppendLine("% Doll Iris Atelier - A4 Print Sheet (100% Real Size)");

                // Draw Title Text at top
                double headerY_Pt = pageH_Pt - MmToPt(config.PageMarginTopMm * 0.65);
                pageContentSb.AppendLine("BT");
                pageContentSb.AppendLine("/F1 8 Tf");
                pageContentSb.AppendLine("0.3 0.3 0.3 rg");
                pageContentSb.AppendLine($"1 0 0 1 {MmToPt(config.PageMarginLeftMm).ToString("F2", Inv)} {headerY_Pt.ToString("F2", Inv)} Tm");
                pageContentSb.AppendLine("(Doll Iris Atelier - A4 Print Sheet [Print at 100% Actual Size]) Tj");
                pageContentSb.AppendLine("ET");

                // Draw Trim Guides (トンボ)
                if (config.ShowTrimGuides)
                {
                    DrawTrimGuides(pageContentSb, config.PageMarginLeftMm, config.PageMarginTopMm, pageW_Mm - config.PageMarginRightMm, pageH_Mm - config.PageMarginBottomMm, pageH_Mm);
                }

                // 1 cm reference mark (実寸確認用スケール)
                if (config.ShowScaleMark)
                {
                    DrawScaleMark(pageContentSb, layout, pageH_Mm);
                }

                // Grid Imposition Loop
                for (int r = 0; r < rows && pairIndex < pairList.Count; r++)
                {
                    for (int c = 0; c < cols && pairIndex < pairList.Count; c++)
                    {
                        var curP = pairList[pairIndex++];

                        var (eyeW_Mm, eyeH_Mm) = SheetLayout.GetEyeSizeMm(curP);

                        // Layout coordinates are measured from the page TOP; PDF origin (0,0) is BOTTOM-LEFT, so invert Y
                        var (leftCx_Mm, rightCx_Mm, centerFromTop_Mm) = layout.GetEyeCentersMm(r * cols + c);
                        double leftCy_Mm = pageH_Mm - centerFromTop_Mm;
                        double rightCy_Mm = leftCy_Mm;

                        // Bottom edge of the cell (labels are printed just below it)
                        double cellOriginY_PdfMm = leftCy_Mm - (layout.CellEyeHeightMm / 2.0);

                        // Reuse a rendered image only when EVERY parameter matches (highlights, pupil size, glitter, ...)
                        string designKey = JsonSerializer.Serialize(curP);

                        // 1. Render Left Eye Image
                        string cacheKeyLeft = "L_" + designKey;
                        if (!imageCache.TryGetValue(cacheKeyLeft, out var leftImg))
                        {
                            var bmp = RasterExporter.RenderSingleToBitmap(curP, isRightEye: false, dpi: EyeImageDpi, showCutLines: false, showCenterMarks: false);
                            byte[] jpegBytes = RasterExporter.SaveBitmapToJpegBytes(bmp, 98);
                            int objId = writer.WriteImageObject(bmp.PixelWidth, bmp.PixelHeight, jpegBytes);
                            leftImg = (objId, bmp.PixelWidth, bmp.PixelHeight);
                            imageCache[cacheKeyLeft] = leftImg;
                        }

                        // 2. Render Right Eye Image (with Odd-Eye and Mirroring support)
                        string cacheKeyRight = "R_" + designKey;
                        if (!imageCache.TryGetValue(cacheKeyRight, out var rightImg))
                        {
                            var bmp = RasterExporter.RenderSingleToBitmap(curP, isRightEye: true, dpi: EyeImageDpi, showCutLines: false, showCenterMarks: false);
                            byte[] jpegBytes = RasterExporter.SaveBitmapToJpegBytes(bmp, 98);
                            int objId = writer.WriteImageObject(bmp.PixelWidth, bmp.PixelHeight, jpegBytes);
                            rightImg = (objId, bmp.PixelWidth, bmp.PixelHeight);
                            imageCache[cacheKeyRight] = rightImg;
                        }

                        // Register resources for this page
                        string leftResName = $"Im{leftImg.ObjId}";
                        string rightResName = $"Im{rightImg.ObjId}";
                        pageImages[leftResName] = leftImg.ObjId;
                        pageImages[rightResName] = rightImg.ObjId;

                        // RenderSingleToBitmap draws the eye's tilted footprint plus its padding on each side, at its
                        // real size from the image's top-left corner; the image is that size rounded up to whole pixels
                        // and is placed at its own size (not stretched over the rounding).
                        var (footW_Mm, footH_Mm) = SheetLayout.GetEyeFootprintMm(curP);
                        double imgTotalW_Mm = footW_Mm + RasterExporter.SingleEyePaddingMm * 2.0;
                        double imgTotalH_Mm = footH_Mm + RasterExporter.SingleEyePaddingMm * 2.0;
                        double imgW_Mm = leftImg.W * 25.4 / EyeImageDpi;
                        double imgH_Mm = leftImg.H * 25.4 / EyeImageDpi;

                        // PDF y grows upwards: the image's top edge is half the canvas above the eye center
                        double imgLeftX_Pt = MmToPt(leftCx_Mm - imgTotalW_Mm / 2.0);
                        double imgLeftY_Pt = MmToPt(leftCy_Mm + imgTotalH_Mm / 2.0 - imgH_Mm);
                        double imgRightX_Pt = MmToPt(rightCx_Mm - imgTotalW_Mm / 2.0);
                        double imgRightY_Pt = MmToPt(rightCy_Mm + imgTotalH_Mm / 2.0 - imgH_Mm);

                        double imgW_Pt = MmToPt(imgW_Mm);
                        double imgH_Pt = MmToPt(imgH_Mm);

                        // Draw Left Image (cm = current transformation matrix)
                        pageContentSb.AppendLine("q");
                        pageContentSb.AppendLine($"{imgW_Pt.ToString("F3", Inv)} 0 0 {imgH_Pt.ToString("F3", Inv)} {imgLeftX_Pt.ToString("F3", Inv)} {imgLeftY_Pt.ToString("F3", Inv)} cm");
                        pageContentSb.AppendLine($"/{leftResName} Do");
                        pageContentSb.AppendLine("Q");

                        // Draw Right Image
                        pageContentSb.AppendLine("q");
                        pageContentSb.AppendLine($"{imgW_Pt.ToString("F3", Inv)} 0 0 {imgH_Pt.ToString("F3", Inv)} {imgRightX_Pt.ToString("F3", Inv)} {imgRightY_Pt.ToString("F3", Inv)} cm");
                        pageContentSb.AppendLine($"/{rightResName} Do");
                        pageContentSb.AppendLine("Q");

                        // 3. Vector Cut Lines (カットライン)
                        if (config.ShowCutLines)
                        {
                            pageContentSb.AppendLine("q");
                            pageContentSb.AppendLine("0.6 0.6 0.6 RG"); // Gray color
                            pageContentSb.AppendLine($"{MmToPt(config.CutLineThicknessMm).ToString("F3", Inv)} w");
                            pageContentSb.AppendLine("[2 2] 0 d"); // Dashed line

                            // Cut lines follow the eye's tilt (the right eye is mirrored, so its tilt is reversed)
                            AppendCutEllipse(pageContentSb, leftCx_Mm, leftCy_Mm, eyeW_Mm / 2.0, eyeH_Mm / 2.0, curP.TiltAngle);
                            AppendCutEllipse(pageContentSb, rightCx_Mm, rightCy_Mm, eyeW_Mm / 2.0, eyeH_Mm / 2.0, -curP.TiltAngle);
                            pageContentSb.AppendLine("Q");
                        }

                        // 4. Vector Center Marks (センターマーク)
                        if (config.ShowCenterMarks)
                        {
                            pageContentSb.AppendLine("q");
                            pageContentSb.AppendLine("0.4 0.4 0.4 RG");
                            pageContentSb.AppendLine("0.35 w"); // Thin line
                            pageContentSb.AppendLine("[] 0 d"); // Solid line

                            double mLenPt = MmToPt(config.CenterMarkLengthMm);

                            // Left eye crosshair
                            double lcxPt = MmToPt(leftCx_Mm);
                            double lcyPt = MmToPt(leftCy_Mm);
                            pageContentSb.AppendLine($"{(lcxPt - mLenPt / 2.0).ToString("F2", Inv)} {lcyPt.ToString("F2", Inv)} m {(lcxPt + mLenPt / 2.0).ToString("F2", Inv)} {lcyPt.ToString("F2", Inv)} l S");
                            pageContentSb.AppendLine($"{lcxPt.ToString("F2", Inv)} {(lcyPt - mLenPt / 2.0).ToString("F2", Inv)} m {lcxPt.ToString("F2", Inv)} {(lcyPt + mLenPt / 2.0).ToString("F2", Inv)} l S");

                            // Right eye crosshair
                            double rcxPt = MmToPt(rightCx_Mm);
                            double rcyPt = MmToPt(rightCy_Mm);
                            pageContentSb.AppendLine($"{(rcxPt - mLenPt / 2.0).ToString("F2", Inv)} {rcyPt.ToString("F2", Inv)} m {(rcxPt + mLenPt / 2.0).ToString("F2", Inv)} {rcyPt.ToString("F2", Inv)} l S");
                            pageContentSb.AppendLine($"{rcxPt.ToString("F2", Inv)} {(rcyPt - mLenPt / 2.0).ToString("F2", Inv)} m {rcxPt.ToString("F2", Inv)} {(rcyPt + mLenPt / 2.0).ToString("F2", Inv)} l S");

                            pageContentSb.AppendLine("Q");
                        }

                        // 5. Labels (サイズ・デザイン名)
                        if (config.ShowLabels)
                        {
                            string labelText = $"{curP.SizeMm:F1}mm - {curP.Name}";
                            double labelCenterX_Pt = MmToPt((leftCx_Mm + rightCx_Mm) / 2.0);
                            double labelY_Pt = MmToPt(cellOriginY_PdfMm - 3.8);

                            if (labelText.Any(ch => ch > 126))
                            {
                                // The built-in Helvetica font has no Japanese glyphs: draw the label as a
                                // high-resolution image instead so names like 「サファイア」 are printed.
                                if (!labelCache.TryGetValue(labelText, out var labelImg))
                                {
                                    var labelBmp = RasterExporter.RenderLabelToBitmap(labelText.Replace('\r', ' ').Replace('\n', ' '), 6.5);
                                    byte[] labelJpeg = RasterExporter.SaveBitmapToJpegBytes(labelBmp, 95);
                                    int labelObjId = writer.WriteImageObject(labelBmp.PixelWidth, labelBmp.PixelHeight, labelJpeg);
                                    labelImg = (labelObjId, labelBmp.PixelWidth * 72.0 / labelBmp.DpiX, labelBmp.PixelHeight * 72.0 / labelBmp.DpiY);
                                    labelCache[labelText] = labelImg;
                                }

                                // Never wider than the pair itself, so long names don't run into the next column
                                double maxW_Pt = MmToPt(layout.PairTotalWidthMm);
                                double scale = Math.Min(1.0, maxW_Pt / labelImg.W_Pt);
                                double drawW = labelImg.W_Pt * scale;
                                double drawH = labelImg.H_Pt * scale;

                                string labelRes = $"Im{labelImg.ObjId}";
                                pageImages[labelRes] = labelImg.ObjId;
                                pageContentSb.AppendLine("q");
                                // Bottom of the image sits slightly below the text baseline used by vector labels
                                pageContentSb.AppendLine($"{drawW.ToString("F3", Inv)} 0 0 {drawH.ToString("F3", Inv)} {(labelCenterX_Pt - drawW / 2.0).ToString("F3", Inv)} {(labelY_Pt - drawH * 0.25).ToString("F3", Inv)} cm");
                                pageContentSb.AppendLine($"/{labelRes} Do");
                                pageContentSb.AppendLine("Q");
                                continue;
                            }

                            string safeText = MakeSafeAscii(labelText);
                            pageContentSb.AppendLine("BT");
                            pageContentSb.AppendLine("/F1 6.5 Tf");
                            pageContentSb.AppendLine("0.4 0.4 0.4 rg");
                            // Center roughly assuming ~3.5 pt per char
                            double estWidth = safeText.Length * 3.5;
                            pageContentSb.AppendLine($"1 0 0 1 {(labelCenterX_Pt - estWidth / 2.0).ToString("F2", Inv)} {labelY_Pt.ToString("F2", Inv)} Tm");
                            pageContentSb.AppendLine($"({safeText}) Tj");
                            pageContentSb.AppendLine("ET");
                        }
                    }
                }

                if (pairIndex == startPairIndex)
                {
                    // Defensively prevent infinite loop if pagination fails to advance
                    break;
                }

                // Write Page Content Stream
                int contentObjId = writer.WriteContentStreamObject(pageContentSb.ToString());

                // Write Page Object
                writer.WritePageObject(pageObjId, pageW_Pt, pageH_Pt, contentObjId, pageImages);
            }

            // Write Document Structure (Pages, Catalog, Font, Xref)
            writer.FinishDocument(pageObjectIds);
        }

        private static void AppendPdfEllipse(StringBuilder sb, double cx, double cy, double rx, double ry)
        {
            // Standard Bezier approximation for 4-quadrant ellipse
            // k = r * 0.5522847498307935
            double kx = rx * 0.5522847498;
            double ky = ry * 0.5522847498;

            sb.AppendLine($"{(cx + rx).ToString("F3", Inv)} {cy.ToString("F3", Inv)} m");
            sb.AppendLine($"{(cx + rx).ToString("F3", Inv)} {(cy + ky).ToString("F3", Inv)} {(cx + kx).ToString("F3", Inv)} {(cy + ry).ToString("F3", Inv)} {cx.ToString("F3", Inv)} {(cy + ry).ToString("F3", Inv)} c");
            sb.AppendLine($"{(cx - kx).ToString("F3", Inv)} {(cy + ry).ToString("F3", Inv)} {(cx - rx).ToString("F3", Inv)} {(cy + ky).ToString("F3", Inv)} {(cx - rx).ToString("F3", Inv)} {cy.ToString("F3", Inv)} c");
            sb.AppendLine($"{(cx - rx).ToString("F3", Inv)} {(cy - ky).ToString("F3", Inv)} {(cx - kx).ToString("F3", Inv)} {(cy - ry).ToString("F3", Inv)} {cx.ToString("F3", Inv)} {(cy - ry).ToString("F3", Inv)} c");
            sb.AppendLine($"{(cx + kx).ToString("F3", Inv)} {(cy - ry).ToString("F3", Inv)} {(cx + rx).ToString("F3", Inv)} {(cy - ky).ToString("F3", Inv)} {(cx + rx).ToString("F3", Inv)} {cy.ToString("F3", Inv)} c");
        }

        /// <summary>
        /// Strokes an elliptical cut line rotated by <paramref name="screenTiltDeg"/> (the same angle the eye is
        /// drawn with on screen, where positive = clockwise). PDF's y axis points up, so the rotation is negated.
        /// </summary>
        public static void AppendCutEllipse(StringBuilder sb, double cx_Mm, double cy_Mm, double rx_Mm, double ry_Mm, double screenTiltDeg)
        {
            if (Math.Abs(screenTiltDeg) < 0.05)
            {
                AppendPdfEllipse(sb, MmToPt(cx_Mm), MmToPt(cy_Mm), MmToPt(rx_Mm), MmToPt(ry_Mm));
                sb.AppendLine("S");
                return;
            }

            double phi = -screenTiltDeg * Math.PI / 180.0;
            double cos = Math.Cos(phi), sin = Math.Sin(phi);
            sb.AppendLine("q");
            sb.AppendLine($"{cos.ToString("F6", Inv)} {sin.ToString("F6", Inv)} {(-sin).ToString("F6", Inv)} {cos.ToString("F6", Inv)} {MmToPt(cx_Mm).ToString("F3", Inv)} {MmToPt(cy_Mm).ToString("F3", Inv)} cm");
            AppendPdfEllipse(sb, 0, 0, MmToPt(rx_Mm), MmToPt(ry_Mm));
            sb.AppendLine("S");
            sb.AppendLine("Q");
        }

        /// <summary>Formats one straight line segment (mm in, PDF points out) the way the scale mark writes it.</summary>
        public static string FormatLineMm(double x1_Mm, double y1_Mm, double x2_Mm, double y2_Mm) =>
            $"{MmToPt(x1_Mm).ToString("F3", Inv)} {MmToPt(y1_Mm).ToString("F3", Inv)} m {MmToPt(x2_Mm).ToString("F3", Inv)} {MmToPt(y2_Mm).ToString("F3", Inv)} l S";

        /// <summary>
        /// 1 cm L-shaped ruler: a 10 mm horizontal bar and (when there is room) a 10 mm vertical bar, with
        /// millimetre ticks. Measuring both bars shows whether the print was scaled in either direction.
        /// </summary>
        private static void DrawScaleMark(StringBuilder sb, SheetLayout layout, double pageH_Mm)
        {
            var (cornerX, cornerYFromTop, includeVertical) = layout.GetScaleMarkPosition();
            double len = SheetLayout.ScaleMarkLengthMm;
            double x0 = cornerX;
            double y0 = pageH_Mm - cornerYFromTop; // PDF origin is bottom-left

            sb.AppendLine("% 1 cm scale mark");
            sb.AppendLine("q");
            sb.AppendLine("0 0 0 RG");
            sb.AppendLine($"{MmToPt(0.2).ToString("F3", Inv)} w");
            sb.AppendLine("[] 0 d");
            sb.AppendLine("0 J"); // butt caps: line ends sit exactly on the 0 / 10 mm positions

            // Horizontal bar with upward ticks
            sb.AppendLine(FormatLineMm(x0, y0, x0 + len, y0));
            for (int mm = 0; mm <= 10; mm++)
            {
                double tick = mm == 0 || mm == 10 ? 2.5 : mm == 5 ? 1.6 : 1.0;
                sb.AppendLine(FormatLineMm(x0 + mm, y0, x0 + mm, y0 + tick));
            }

            // Vertical bar with ticks pointing right
            if (includeVertical)
            {
                sb.AppendLine(FormatLineMm(x0, y0, x0, y0 + len));
                for (int mm = 1; mm <= 10; mm++)
                {
                    double tick = mm == 10 ? 2.5 : mm == 5 ? 1.6 : 1.0;
                    sb.AppendLine(FormatLineMm(x0, y0 + mm, x0 + tick, y0 + mm));
                }
            }

            // Label to the right of the horizontal bar
            sb.AppendLine("BT");
            sb.AppendLine("/F1 6.5 Tf");
            sb.AppendLine("0 0 0 rg");
            sb.AppendLine($"1 0 0 1 {MmToPt(x0 + len + 2.0).ToString("F2", Inv)} {MmToPt(y0 + 0.2).ToString("F2", Inv)} Tm");
            sb.AppendLine("(1 cm = 10 mm \\(check with a ruler: 100% print\\)) Tj");
            sb.AppendLine("ET");
            sb.AppendLine("Q");
        }

        private static void DrawTrimGuides(StringBuilder sb, double x1_Mm, double y1_Mm, double x2_Mm, double y2_Mm, double pageH_Mm)
        {
            sb.AppendLine("q");
            sb.AppendLine("0.5 0.5 0.5 RG");
            sb.AppendLine("0.3 w");
            sb.AppendLine("[] 0 d");

            double armMm = 6.0;
            double offsetMm = 2.0;

            // Invert Y for PDF
            double y1_Pdf = pageH_Mm - y1_Mm;
            double y2_Pdf = pageH_Mm - y2_Mm;

            // Top-Left
            sb.AppendLine($"{MmToPt(x1_Mm - offsetMm - armMm).ToString("F2", Inv)} {MmToPt(y1_Pdf).ToString("F2", Inv)} m {MmToPt(x1_Mm - offsetMm).ToString("F2", Inv)} {MmToPt(y1_Pdf).ToString("F2", Inv)} l S");
            sb.AppendLine($"{MmToPt(x1_Mm).ToString("F2", Inv)} {MmToPt(y1_Pdf + offsetMm + armMm).ToString("F2", Inv)} m {MmToPt(x1_Mm).ToString("F2", Inv)} {MmToPt(y1_Pdf + offsetMm).ToString("F2", Inv)} l S");

            // Top-Right
            sb.AppendLine($"{MmToPt(x2_Mm + offsetMm).ToString("F2", Inv)} {MmToPt(y1_Pdf).ToString("F2", Inv)} m {MmToPt(x2_Mm + offsetMm + armMm).ToString("F2", Inv)} {MmToPt(y1_Pdf).ToString("F2", Inv)} l S");
            sb.AppendLine($"{MmToPt(x2_Mm).ToString("F2", Inv)} {MmToPt(y1_Pdf + offsetMm + armMm).ToString("F2", Inv)} m {MmToPt(x2_Mm).ToString("F2", Inv)} {MmToPt(y1_Pdf + offsetMm).ToString("F2", Inv)} l S");

            // Bottom-Left
            sb.AppendLine($"{MmToPt(x1_Mm - offsetMm - armMm).ToString("F2", Inv)} {MmToPt(y2_Pdf).ToString("F2", Inv)} m {MmToPt(x1_Mm - offsetMm).ToString("F2", Inv)} {MmToPt(y2_Pdf).ToString("F2", Inv)} l S");
            sb.AppendLine($"{MmToPt(x1_Mm).ToString("F2", Inv)} {MmToPt(y2_Pdf - offsetMm - armMm).ToString("F2", Inv)} m {MmToPt(x1_Mm).ToString("F2", Inv)} {MmToPt(y2_Pdf - offsetMm).ToString("F2", Inv)} l S");

            // Bottom-Right
            sb.AppendLine($"{MmToPt(x2_Mm + offsetMm).ToString("F2", Inv)} {MmToPt(y2_Pdf).ToString("F2", Inv)} m {MmToPt(x2_Mm + offsetMm + armMm).ToString("F2", Inv)} {MmToPt(y2_Pdf).ToString("F2", Inv)} l S");
            sb.AppendLine($"{MmToPt(x2_Mm).ToString("F2", Inv)} {MmToPt(y2_Pdf - offsetMm - armMm).ToString("F2", Inv)} m {MmToPt(x2_Mm).ToString("F2", Inv)} {MmToPt(y2_Pdf - offsetMm).ToString("F2", Inv)} l S");

            sb.AppendLine("Q");
        }

        private static string MakeSafeAscii(string? text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new StringBuilder();
            foreach (char c in text)
            {
                if (c == '(' || c == ')' || c == '\\')
                {
                    sb.Append('\\').Append(c);
                }
                else if (c == '\r' || c == '\n')
                {
                    sb.Append(' ');
                }
                else if (c >= 32 && c <= 126)
                {
                    sb.Append(c);
                }
                else
                {
                    // For non-ASCII in standard Helvetica, skip or replace with space
                    sb.Append(' ');
                }
            }
            return sb.ToString().Trim();
        }

        /// <summary>
        /// Pure C# PDF ISO 32000-1 binary writer.
        /// </summary>
        private class PdfWriter
        {
            private readonly Stream _stream;
            private readonly List<long> _offsets = new();
            // Object 1 is reserved for Font (/F1), Object 2 is reserved for Pages catalog root
            private int _nextObjId = 3;

            public PdfWriter(Stream stream)
            {
                _stream = stream;
                // Write PDF Header
                WriteAscii("%PDF-1.7\r\n%\xFF\xFF\xFF\xFF\r\n");
                _offsets.Add(0); // Dummy for 0 0 R
                _offsets.Add(0); // Slot for Obj 1 (Font)
                _offsets.Add(0); // Slot for Obj 2 (Pages)
            }

            public int AllocateObjectId() => _nextObjId++;

            public int WriteImageObject(int width, int height, byte[] jpegBytes)
            {
                int objId = AllocateObjectId();
                RecordOffset(objId);

                string header = $"{objId} 0 obj\r\n<< /Type /XObject /Subtype /Image /Width {width} /Height {height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {jpegBytes.Length} >>\r\nstream\r\n";
                WriteAscii(header);
                _stream.Write(jpegBytes, 0, jpegBytes.Length);
                WriteAscii("\r\nendstream\r\nendobj\r\n");
                return objId;
            }

            public int WriteContentStreamObject(string content)
            {
                int objId = AllocateObjectId();
                RecordOffset(objId);

                byte[] bytes = Encoding.ASCII.GetBytes(content);
                string dict = $"{objId} 0 obj\r\n<< /Length {bytes.Length} >>\r\nstream\r\n";
                WriteAscii(dict);
                _stream.Write(bytes, 0, bytes.Length);
                WriteAscii("\r\nendstream\r\nendobj\r\n");
                return objId;
            }

            public void WritePageObject(int pageObjId, double widthPt, double heightPt, int contentObjId, Dictionary<string, int> images)
            {
                RecordOffset(pageObjId);

                var sb = new StringBuilder();
                sb.AppendLine($"{pageObjId} 0 obj");
                sb.AppendLine("<<");
                sb.AppendLine("  /Type /Page");
                sb.AppendLine("  /Parent 2 0 R"); // Pages root
                sb.AppendLine($"  /MediaBox [ 0 0 {widthPt.ToString("F3", Inv)} {heightPt.ToString("F3", Inv)} ]");
                sb.AppendLine("  /Resources <<");
                sb.AppendLine("    /Font << /F1 1 0 R >>");

                if (images.Count > 0)
                {
                    sb.AppendLine("    /XObject <<");
                    foreach (var kvp in images)
                    {
                        sb.AppendLine($"      /{kvp.Key} {kvp.Value} 0 R");
                    }
                    sb.AppendLine("    >>");
                }

                sb.AppendLine("  >>");
                sb.AppendLine($"  /Contents {contentObjId} 0 R");
                sb.AppendLine(">>");
                sb.AppendLine("endobj");

                WriteAscii(sb.ToString());
            }

            public void FinishDocument(List<int> pageObjIds)
            {
                // Font Object (Obj 1)
                RecordOffset(1);
                WriteAscii("1 0 obj\r\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\r\nendobj\r\n");

                // Pages Object (Obj 2)
                RecordOffset(2);
                var pagesSb = new StringBuilder();
                pagesSb.AppendLine("2 0 obj");
                pagesSb.AppendLine("<<");
                pagesSb.AppendLine("  /Type /Pages");
                pagesSb.Append("  /Kids [ ");
                foreach (int pid in pageObjIds) pagesSb.Append($"{pid} 0 R ");
                pagesSb.AppendLine("]");
                pagesSb.AppendLine($"  /Count {pageObjIds.Count}");
                pagesSb.AppendLine(">>");
                pagesSb.AppendLine("endobj");
                WriteAscii(pagesSb.ToString());

                // Catalog Object
                int catalogObjId = AllocateObjectId();
                RecordOffset(catalogObjId);
                WriteAscii($"{catalogObjId} 0 obj\r\n<< /Type /Catalog /Pages 2 0 R >>\r\nendobj\r\n");

                // Cross Reference Table
                long startXref = _stream.Position;
                int totalObjects = _nextObjId;

                var xrefSb = new StringBuilder();
                xrefSb.AppendLine("xref");
                xrefSb.AppendLine($"0 {totalObjects}");
                xrefSb.AppendLine("0000000000 65535 f ");

                for (int i = 1; i < totalObjects; i++)
                {
                    long offset = (i < _offsets.Count) ? _offsets[i] : 0;
                    xrefSb.AppendLine($"{offset:D10} 00000 n ");
                }

                xrefSb.AppendLine("trailer");
                xrefSb.AppendLine("<<");
                xrefSb.AppendLine($"  /Size {totalObjects}");
                xrefSb.AppendLine($"  /Root {catalogObjId} 0 R");
                xrefSb.AppendLine(">>");
                xrefSb.AppendLine("startxref");
                xrefSb.AppendLine(startXref.ToString());
                xrefSb.AppendLine("%%EOF");

                WriteAscii(xrefSb.ToString());
            }

            private void RecordOffset(int objId)
            {
                while (_offsets.Count <= objId) _offsets.Add(0);
                _offsets[objId] = _stream.Position;
            }

            private void WriteAscii(string str)
            {
                byte[] bytes = Encoding.ASCII.GetBytes(str);
                _stream.Write(bytes, 0, bytes.Length);
            }
        }
    }
}
