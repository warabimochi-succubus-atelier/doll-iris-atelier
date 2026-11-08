using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;

namespace DollIrisGenerator.Controls
{
    public enum PreviewMode
    {
        Pair,
        A4Sheet
    }

    public class IrisPreviewControl : FrameworkElement
    {
        public static readonly DependencyProperty ParametersProperty =
            DependencyProperty.Register(nameof(Parameters), typeof(IrisParameters), typeof(IrisPreviewControl),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnParametersChanged));

        public static readonly DependencyProperty PreviewModeProperty =
            DependencyProperty.Register(nameof(PreviewMode), typeof(PreviewMode), typeof(IrisPreviewControl),
                new FrameworkPropertyMetadata(PreviewMode.Pair, FrameworkPropertyMetadataOptions.AffectsRender,
                    (d, _) => ((IrisPreviewControl)d).ResetPan())); // a different view starts centred

        public static readonly DependencyProperty ZoomScaleProperty =
            DependencyProperty.Register(nameof(ZoomScale), typeof(double), typeof(IrisPreviewControl),
                new FrameworkPropertyMetadata(1.0,
                    FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        // Items printed on the A4 sheet (A4 preview mode). Empty → the current design, exactly like the PDF export.
        public static readonly DependencyProperty SheetItemsProperty =
            DependencyProperty.Register(nameof(SheetItems), typeof(ObservableCollection<SheetItem>), typeof(IrisPreviewControl),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnSheetItemsChanged));

        public static readonly DependencyProperty ShowCutLinesProperty =
            DependencyProperty.Register(nameof(ShowCutLines), typeof(bool), typeof(IrisPreviewControl),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowCenterMarksProperty =
            DependencyProperty.Register(nameof(ShowCenterMarks), typeof(bool), typeof(IrisPreviewControl),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        // 「L (左)」「R (右・鏡像)」 under the pair. Off in the split preview, where every small cell would repeat them.
        public static readonly DependencyProperty ShowEyeLabelsProperty =
            DependencyProperty.Register(nameof(ShowEyeLabels), typeof(bool), typeof(IrisPreviewControl),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public bool ShowEyeLabels
        {
            get => (bool)GetValue(ShowEyeLabelsProperty);
            set => SetValue(ShowEyeLabelsProperty, value);
        }

        public static readonly DependencyProperty SheetConfigProperty =
            DependencyProperty.Register(nameof(SheetConfig), typeof(PrintSheetConfig), typeof(IrisPreviewControl),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnSheetConfigChanged));

        public IrisParameters? Parameters
        {
            get => (IrisParameters?)GetValue(ParametersProperty);
            set => SetValue(ParametersProperty, value);
        }

        public PreviewMode PreviewMode
        {
            get => (PreviewMode)GetValue(PreviewModeProperty);
            set => SetValue(PreviewModeProperty, value);
        }

        public double ZoomScale
        {
            get => (double)GetValue(ZoomScaleProperty);
            set => SetValue(ZoomScaleProperty, value);
        }

        public ObservableCollection<SheetItem>? SheetItems
        {
            get => (ObservableCollection<SheetItem>?)GetValue(SheetItemsProperty);
            set => SetValue(SheetItemsProperty, value);
        }

        public bool ShowCutLines
        {
            get => (bool)GetValue(ShowCutLinesProperty);
            set => SetValue(ShowCutLinesProperty, value);
        }

        public bool ShowCenterMarks
        {
            get => (bool)GetValue(ShowCenterMarksProperty);
            set => SetValue(ShowCenterMarksProperty, value);
        }

        public PrintSheetConfig? SheetConfig
        {
            get => (PrintSheetConfig?)GetValue(SheetConfigProperty);
            set => SetValue(SheetConfigProperty, value);
        }

        /// <summary>
        /// Pair view zoom the app opens at (and "リセット" returns to). 360% shows the default 10 mm iris
        /// about 136 DIP across, the same on-screen size the old 20 mm default had at 180%.
        /// </summary>
        public const double DefaultPairZoom = IrisStyle.ReferenceZoom;

        /// <summary>Starting zoom of the A4 sheet view: actual size (100%), so the whole sheet is easier to take in.</summary>
        public const double DefaultSheetZoom = 1.0;

        public const double MinZoom = 0.2;
        public const double MaxZoom = 16.0;

        // Standard screen pixel per mm at 96 DPI
        public const double ScreenDipsPerMm = 96.0 / 25.4; // ~3.7795 DIP/mm

        // Room kept around a pair when fitting it into a view: the L / R labels under the eyes (drawn 16 DIP
        // below the eye, ~16 DIP tall) and the cell's number label at the top. Kept on both sides because the pair is centred.
        public const double FitMarginX = 16.0;
        public const double FitMarginY = 36.0;

        /// <summary>
        /// Largest zoom at which every design's pair (left eye, gap, mirrored right eye and labels) fits inside a
        /// view of <paramref name="viewWidth"/> × <paramref name="viewHeight"/> DIP, clamped to the preview zoom range.
        /// </summary>
        public static double FitPairZoom(IEnumerable<IrisParameters> designs, double pairSpacingMm, double viewWidth, double viewHeight)
        {
            double widestMm = 0, tallestMm = 0;
            foreach (var design in designs)
            {
                var (wMm, hMm) = SheetLayout.GetEyeFootprintMm(design);
                widestMm = Math.Max(widestMm, 2 * wMm + pairSpacingMm);
                tallestMm = Math.Max(tallestMm, hMm);
            }
            if (widestMm <= 0 || tallestMm <= 0) return DefaultPairZoom;

            double usableW = Math.Max(1, viewWidth - 2 * FitMarginX);
            double usableH = Math.Max(1, viewHeight - 2 * FitMarginY);
            double zoom = Math.Min(usableW / (widestMm * ScreenDipsPerMm), usableH / (tallestMm * ScreenDipsPerMm));
            return Math.Clamp(zoom, MinZoom, MaxZoom);
        }

        private Point _panOffset = new Point(0, 0);
        private Point _lastMousePos;
        private bool _isDragging = false;

        // Eyes are pictures of their SVG drawn by resvg, redrawn in the background after a change
        private readonly PreviewEyeImages _eyes;

        // Bumped on every change of a design, the sheet or its settings: the eyes' SVG is rebuilt only then
        // (panning and zooming reuse it)
        private long _modelVersion;

        public IrisPreviewControl()
        {
            ClipToBounds = true;
            Focusable = true;
            _eyes = new PreviewEyeImages(Dispatcher, InvalidateVisual);
            // An older picture shown while the new zoom is drawn is scaled smoothly
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);

            MouseDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseUp += OnMouseUp;
            MouseWheel += OnMouseWheel;
        }

        // Auto-refresh: sliders mutate the same IrisParameters instance, so the DependencyProperty never sees
        // a new value. Listen to the model and redraw on any change (IrisParameters also reports
        // highlight / symbol edits via IrisParameters.NestedItemsChangedName).
        private static void OnParametersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (IrisPreviewControl)d;
            ctrl._modelVersion++;
            if (e.OldValue is INotifyPropertyChanged oldP) oldP.PropertyChanged -= ctrl.OnModelPropertyChanged;
            if (e.NewValue is INotifyPropertyChanged newP) newP.PropertyChanged += ctrl.OnModelPropertyChanged;
        }

        private static void OnSheetConfigChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (IrisPreviewControl)d;
            ctrl._modelVersion++;
            if (e.OldValue is INotifyPropertyChanged oldCfg) oldCfg.PropertyChanged -= ctrl.OnModelPropertyChanged;
            if (e.NewValue is INotifyPropertyChanged newCfg) newCfg.PropertyChanged += ctrl.OnModelPropertyChanged;
        }

        private static void OnSheetItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (IrisPreviewControl)d;
            ctrl._modelVersion++;
            if (e.OldValue is ObservableCollection<SheetItem> oldItems)
            {
                oldItems.CollectionChanged -= ctrl.OnSheetItemsCollectionChanged;
                foreach (var item in oldItems) item.PropertyChanged -= ctrl.OnModelPropertyChanged;
            }
            if (e.NewValue is ObservableCollection<SheetItem> newItems)
            {
                newItems.CollectionChanged += ctrl.OnSheetItemsCollectionChanged;
                foreach (var item in newItems) item.PropertyChanged += ctrl.OnModelPropertyChanged;
            }
        }

        private void OnSheetItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (var item in e.OldItems.OfType<INotifyPropertyChanged>()) item.PropertyChanged -= OnModelPropertyChanged;
            if (e.NewItems != null)
                foreach (var item in e.NewItems.OfType<INotifyPropertyChanged>()) item.PropertyChanged += OnModelPropertyChanged;
            _modelVersion++;
            InvalidateVisual();
        }

        private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            _modelVersion++;
            InvalidateVisual();
        }

        public void ResetPan()
        {
            _panOffset = new Point(0, 0);
            InvalidateVisual();
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Double-click re-centres the view after panning
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
            {
                ResetPan();
                e.Handled = true;
                return;
            }
            if (e.MiddleButton == MouseButtonState.Pressed || e.LeftButton == MouseButtonState.Pressed)
            {
                _isDragging = true;
                _lastMousePos = e.GetPosition(this);
                CaptureMouse();
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                var curPos = e.GetPosition(this);
                _panOffset.X += (curPos.X - _lastMousePos.X);
                _panOffset.Y += (curPos.Y - _lastMousePos.Y);
                _lastMousePos = curPos;
                InvalidateVisual();
            }
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                ReleaseMouseCapture();
            }
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            double factor = e.Delta > 0 ? 1.15 : 0.85;
            // Two-way bound: keeps the toolbar zoom buttons and status bar in sync (same range as MainViewModel)
            ZoomScale = Math.Clamp(ZoomScale * factor, MinZoom, MaxZoom);
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            _eyes.BeginFrame();
            try
            {
                RenderView(dc);
            }
            finally
            {
                _eyes.EndFrame();
            }
        }

        private double PixelsPerDip => VisualTreeHelper.GetDpi(this).PixelsPerDip;

        /// <summary>Eye pictures still being drawn in the background (for tests).</summary>
        internal int EyePicturesInProgress => _eyes.BusyCount;

        private void RenderView(DrawingContext dc)
        {

            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            // Draw checkerboard or subtle dark-grid pattern background
            DrawBackgroundGrid(dc, w, h);

            if (Parameters == null) return;

            var p = Parameters;
            double cx = (w / 2.0) + _panOffset.X;
            double cy = (h / 2.0) + _panOffset.Y;

            // Base rendering resolution scale in DIPs
            double pixelsPerMm = ScreenDipsPerMm * ZoomScale;

            if (PreviewMode == PreviewMode.A4Sheet)
            {
                cy = SheetHomeCenterY(h, A4HeightMm * pixelsPerMm) + _panOffset.Y;
                RenderA4SheetPreview(dc, cx, cy, pixelsPerMm, p);
                return;
            }

            // Spacing and label positions use the eye's tilted footprint (same as the exports)
            var (irisW_Mm, irisH_Mm) = SheetLayout.GetEyeFootprintMm(p);

            // Same gap as SVG / PNG / PDF output ("ペア内左右の間隔")
            double pairSpacingPx = (SheetConfig?.PairSpacingXMm ?? 4.0) * pixelsPerMm;

            switch (PreviewMode)
            {
                case PreviewMode.Pair:
                default:
                    double halfSpanX = ((irisW_Mm * pixelsPerMm) + (pairSpacingPx / 2.0));
                    Point leftCenter = new Point(cx - halfSpanX + ((irisW_Mm * pixelsPerMm) / 2.0), cy);
                    Point rightCenter = new Point(cx + halfSpanX - ((irisW_Mm * pixelsPerMm) / 2.0), cy);

                    // Left Eye
                    _eyes.Draw(dc, p, isRight: false, leftCenter, pixelsPerMm, PixelsPerDip, ShowCutLines, ShowCenterMarks, _modelVersion);
                    if (ShowEyeLabels) DrawEyeLabel(dc, "L (左)", new Point(leftCenter.X, leftCenter.Y + (irisH_Mm / 2.0) * pixelsPerMm + 16));

                    // Right Eye (Mirrored)
                    _eyes.Draw(dc, p, isRight: true, rightCenter, pixelsPerMm, PixelsPerDip, ShowCutLines, ShowCenterMarks, _modelVersion);
                    if (ShowEyeLabels) DrawEyeLabel(dc, "R (右・鏡像)", new Point(rightCenter.X, rightCenter.Y + (irisH_Mm / 2.0) * pixelsPerMm + 16));
                    break;
            }
        }

        private const double A4HeightMm = 297.0;

        // Gap above the paper when its top edge is pinned to the top of the view
        private const double SheetTopGapPx = 16.0;

        /// <summary>
        /// Vertical centre of the A4 paper before panning: centred when it fits in the view, otherwise its top
        /// edge sits just below the top of the view so the start of the sheet is what you see first
        /// (initial display, view switch, リセット and double-click all come back here).
        /// </summary>
        public static double SheetHomeCenterY(double viewHeight, double sheetHeightPx) =>
            sheetHeightPx + 2 * SheetTopGapPx <= viewHeight
                ? viewHeight / 2.0
                : SheetTopGapPx + sheetHeightPx / 2.0;

        private void RenderA4SheetPreview(DrawingContext dc, double cx, double cy, double pixelsPerMm, IrisParameters p)
        {
            var cfg = SheetConfig ?? new PrintSheetConfig();

            // A4 dimensions in mm
            double a4W_Mm = 210.0;
            double a4H_Mm = A4HeightMm;

            double sheetW_Px = a4W_Mm * pixelsPerMm;
            double sheetH_Px = a4H_Mm * pixelsPerMm;

            Rect sheetRect = new Rect(cx - sheetW_Px / 2.0, cy - sheetH_Px / 2.0, sheetW_Px, sheetH_Px);

            // Draw A4 paper sheet with shadow
            var shadowPen = new Pen(new SolidColorBrush(Color.FromArgb(50, 0, 0, 0)), 4);
            dc.DrawRectangle(Brushes.White, null, sheetRect);
            dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(200, 180, 180, 180)), 1), sheetRect);

            // Margin guide line
            double marginLPx = cfg.PageMarginLeftMm * pixelsPerMm;
            double marginTPx = cfg.PageMarginTopMm * pixelsPerMm;
            double marginRPx = cfg.PageMarginRightMm * pixelsPerMm;
            double marginBPx = cfg.PageMarginBottomMm * pixelsPerMm;

            Rect marginRect = new Rect(
                sheetRect.Left + marginLPx,
                sheetRect.Top + marginTPx,
                Math.Max(10, sheetRect.Width - marginLPx - marginRPx),
                Math.Max(10, sheetRect.Height - marginTPx - marginBPx)
            );
            dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(60, 0, 120, 215)), 1) { DashStyle = DashStyles.Dash }, marginRect);

            // Same imposition as the PDF export: sheet items (or the current design when the sheet is empty)
            var items = SheetItems is { Count: > 0 } ? SheetItems : new ObservableCollection<SheetItem> { new(p, 1) };
            var layout = new SheetLayout(items, cfg);

            // Preview shows the first page. A sheet usually repeats the same design many times: each design's eye
            // picture is made once (per side) and drawn in every cell; eyes outside the view are not drawn at all.
            var view = new Rect(0, 0, ActualWidth, ActualHeight);
            double pixelsPerDip = PixelsPerDip;
            void DrawEye(IrisParameters design, bool isRight, Point center)
            {
                var (fw, fh) = SheetLayout.GetEyeFootprintMm(design);
                var bounds = new Rect(center.X - fw / 2.0 * pixelsPerMm, center.Y - fh / 2.0 * pixelsPerMm, fw * pixelsPerMm, fh * pixelsPerMm);
                if (!bounds.IntersectsWith(view)) return;
                _eyes.Draw(dc, design, isRight, center, pixelsPerMm, pixelsPerDip, cfg.ShowCutLines, cfg.ShowCenterMarks, _modelVersion);
            }

            int pairsOnPage = Math.Min(layout.PairsPerPage, layout.Pairs.Count);
            for (int slot = 0; slot < pairsOnPage; slot++)
            {
                var pair = layout.Pairs[slot];
                var (leftX_Mm, rightX_Mm, y_Mm) = layout.GetEyeCentersMm(slot);
                double cy_Px = sheetRect.Top + y_Mm * pixelsPerMm;
                DrawEye(pair, false, new Point(sheetRect.Left + leftX_Mm * pixelsPerMm, cy_Px));
                DrawEye(pair, true, new Point(sheetRect.Left + rightX_Mm * pixelsPerMm, cy_Px));
            }

            if (cfg.ShowScaleMark)
            {
                DrawScaleMark(dc, layout, sheetRect, pixelsPerMm);
            }

            string pageInfo = layout.PageCount > 1
                ? $"1 / {layout.PageCount} ページ目を表示中 (全 {layout.Pairs.Count} ペア)"
                : $"全 {layout.Pairs.Count} ペア";
            DrawEyeLabel(dc, pageInfo, new Point(sheetRect.Left + sheetRect.Width / 2.0, sheetRect.Bottom + 8));

            if (!layout.FitsOnPage)
            {
                // Pinned to the top of the view (not the sheet) so it stays visible at any zoom / pan
                DrawEyeLabel(dc,
                    $"⚠ 配置が用紙の印刷範囲からはみ出しています ({layout.GridWidthMm:F0} × {layout.GridHeightMm:F0} mm / 範囲 {layout.UsableWidthMm:F0} × {layout.UsableHeightMm:F0} mm)",
                    new Point(ActualWidth / 2.0, 10), WarningBrush, background: true);
            }
        }

        // Same 1 cm L-shaped ruler as the PDF (PdfExporter.DrawScaleMark), so the preview shows where it prints
        private static void DrawScaleMark(DrawingContext dc, SheetLayout layout, Rect sheetRect, double pixelsPerMm)
        {
            var (cornerX, cornerY, includeVertical) = layout.GetScaleMarkPosition();
            double len = SheetLayout.ScaleMarkLengthMm;
            var pen = new Pen(Brushes.Black, Math.Max(0.6, 0.2 * pixelsPerMm)) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
            pen.Freeze();
            Point P(double xMm, double yMm) => new Point(sheetRect.Left + xMm * pixelsPerMm, sheetRect.Top + yMm * pixelsPerMm);

            // y grows downward here, so "up" on the page is minus
            dc.DrawLine(pen, P(cornerX, cornerY), P(cornerX + len, cornerY));
            for (int mm = 0; mm <= 10; mm++)
            {
                double tick = mm == 0 || mm == 10 ? 2.5 : mm == 5 ? 1.6 : 1.0;
                dc.DrawLine(pen, P(cornerX + mm, cornerY), P(cornerX + mm, cornerY - tick));
            }
            if (includeVertical)
            {
                dc.DrawLine(pen, P(cornerX, cornerY), P(cornerX, cornerY - len));
                for (int mm = 1; mm <= 10; mm++)
                {
                    double tick = mm == 10 ? 2.5 : mm == 5 ? 1.6 : 1.0;
                    dc.DrawLine(pen, P(cornerX, cornerY - mm), P(cornerX + tick, cornerY - mm));
                }
            }
        }

        private void DrawBackgroundGrid(DrawingContext dc, double w, double h)
        {
            // Dark elegant studio workspace background
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(30, 32, 38)), null, new Rect(0, 0, w, h));

            var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), 1);
            gridPen.Freeze();

            double step = 30.0;
            for (double x = 0; x < w; x += step)
            {
                dc.DrawLine(gridPen, new Point(x, 0), new Point(x, h));
            }
            for (double y = 0; y < h; y += step)
            {
                dc.DrawLine(gridPen, new Point(0, y), new Point(w, y));
            }
        }

        private static readonly Brush WarningBrush = CreateFrozenBrush(Color.FromRgb(255, 138, 128));
        private static readonly Brush WarningBackground = CreateFrozenBrush(Color.FromArgb(230, 74, 32, 32));

        private static Brush CreateFrozenBrush(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        private void DrawEyeLabel(DrawingContext dc, string text, Point location, Brush? brush = null, bool background = false)
        {
            var formattedText = new FormattedText(
                text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                12,
                brush ?? new SolidColorBrush(Color.FromRgb(200, 210, 230)),
                VisualTreeHelper.GetDpi(this).PixelsPerDip
            )
            {
                TextAlignment = TextAlignment.Center
            };

            if (background)
            {
                // Centered text: the box extends half the width to each side of the location
                var box = new Rect(location.X - formattedText.Width / 2.0 - 8, location.Y - 4, formattedText.Width + 16, formattedText.Height + 8);
                dc.DrawRoundedRectangle(WarningBackground, null, box, 4, 4);
            }

            dc.DrawText(formattedText, location);
        }
    }
}
