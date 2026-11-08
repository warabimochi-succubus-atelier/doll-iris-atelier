using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;

namespace DollIrisGenerator.Controls
{
    /// <summary>
    /// Pictures of the eyes for the preview: each eye's exported SVG drawn by resvg at the screen's pixel size
    /// (docs/design/svg-canonical-rendering.md, step 4).
    /// <list type="bullet">
    /// <item>The first picture of an eye is drawn at once, so the preview never opens empty.</item>
    /// <item>After a change (a slider, the zoom) the eye keeps showing its last picture, scaled to the new zoom,
    /// while the new one is drawn on a worker thread; then <c>onReady</c> asks for a redraw. Changes arriving
    /// meanwhile are not queued: only the newest state is drawn next.</item>
    /// <item>Panning draws nothing new: the SVG is rebuilt only when <c>modelVersion</c> or the guides change.</item>
    /// </list>
    /// Eyes are kept per (design object, left / right); <see cref="BeginFrame"/> / <see cref="EndFrame"/> forget
    /// eyes a frame no longer shows.
    /// </summary>
    internal sealed class PreviewEyeImages
    {
        /// <summary>Room around the eye's footprint on its picture, for the cut line's stroke (mm).</summary>
        public const double PaddingMm = 0.5;

        /// <summary>Longest side of one picture (px); closer zooms scale it up instead of drawing more.</summary>
        public const int MaxSidePx = 4096;

        private sealed class Slot
        {
            // SVG of the current state, rebuilt when the model or the guides change
            public long SvgVersion = -1;
            public bool SvgCutLine, SvgCenterMark;
            public string Svg = "";
            public double CanvasWidthMm, CanvasHeightMm;

            // Picture shown now (maybe of an older state or zoom) and what it was drawn from
            public BitmapSource? Image;
            public double ImageWidthMm, ImageHeightMm, ImagePixelsPerMm;
            public string? ImageKey;

            // Worker state: the request being drawn, and the newest one waiting behind it
            public bool Busy;
            public Job? Waiting;

            public bool UsedThisFrame;
        }

        private sealed record Job(string Key, string Svg, double CanvasWidthMm, double CanvasHeightMm, double PixelsPerMm);

        private readonly Dictionary<(IrisParameters Design, bool IsRight), Slot> _slots = new();
        private readonly Dispatcher _dispatcher;
        private readonly Action _onReady;

        public PreviewEyeImages(Dispatcher dispatcher, Action onReady)
        {
            _dispatcher = dispatcher;
            _onReady = onReady;
        }

        /// <summary>Number of eyes being drawn in the background (for tests).</summary>
        public int BusyCount { get; private set; }

        public void BeginFrame()
        {
            foreach (var slot in _slots.Values) slot.UsedThisFrame = false;
        }

        public void EndFrame()
        {
            List<(IrisParameters, bool)>? unused = null;
            foreach (var (key, slot) in _slots)
                if (!slot.UsedThisFrame && !slot.Busy) (unused ??= new()).Add(key);
            if (unused != null) foreach (var key in unused) _slots.Remove(key);
        }

        /// <summary>
        /// Draws one eye centered at <paramref name="center"/> (DIP), at <paramref name="dipPerMm"/> DIP per mm on a
        /// screen of <paramref name="pixelsPerDip"/> device pixels per DIP. Pixel-snapped to the device grid.
        /// </summary>
        public void Draw(DrawingContext dc, IrisParameters design, bool isRight, Point center, double dipPerMm, double pixelsPerDip,
            bool cutLine, bool centerMark, long modelVersion)
        {
            if (!_slots.TryGetValue((design, isRight), out var slot))
                _slots[(design, isRight)] = slot = new Slot();
            slot.UsedThisFrame = true;

            if (slot.SvgVersion != modelVersion || slot.SvgCutLine != cutLine || slot.SvgCenterMark != centerMark)
            {
                var (footW, footH) = SheetLayout.GetEyeFootprintMm(design);
                slot.Svg = SvgExporter.GenerateSingleEyeSvg(design, isRight, cutLine, centerMark, PaddingMm);
                slot.CanvasWidthMm = footW + PaddingMm * 2.0;
                slot.CanvasHeightMm = footH + PaddingMm * 2.0;
                slot.SvgVersion = modelVersion;
                slot.SvgCutLine = cutLine;
                slot.SvgCenterMark = centerMark;
            }

            // Device pixels per mm, capped so one picture stays within MaxSidePx
            double pxPerMm = dipPerMm * pixelsPerDip;
            double longestMm = Math.Max(slot.CanvasWidthMm, slot.CanvasHeightMm);
            if (longestMm * pxPerMm > MaxSidePx) pxPerMm = MaxSidePx / longestMm;

            string key = slot.Svg.Length + ":" + slot.Svg.GetHashCode() + "@" + pxPerMm.ToString("R");
            var job = new Job(key, slot.Svg, slot.CanvasWidthMm, slot.CanvasHeightMm, pxPerMm);

            if (slot.ImageKey != key)
            {
                if (slot.Image == null)
                {
                    // First picture: at once. Without a working resvg (warned at startup) the eye stays empty
                    // instead of breaking the whole window's drawing.
                    try { Store(slot, job, RenderJob(job)); }
                    catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or ResvgException) { }
                }
                else Request(slot, job);
            }

            if (slot.Image is not BitmapSource image) return;

            // The picture's canvas is centered on the eye; its top-left corner is snapped to whole device pixels
            double left = center.X - slot.ImageWidthMm / 2.0 * dipPerMm;
            double top = center.Y - slot.ImageHeightMm / 2.0 * dipPerMm;
            left = Math.Round(left * pixelsPerDip) / pixelsPerDip;
            top = Math.Round(top * pixelsPerDip) / pixelsPerDip;
            // The picture is its canvas at real size from the top-left, rounded up to whole pixels: draw it at
            // the size of its pixels (scaled from the zoom it was drawn at), not stretched over the canvas
            double width = image.PixelWidth / slot.ImagePixelsPerMm * dipPerMm;
            double height = image.PixelHeight / slot.ImagePixelsPerMm * dipPerMm;
            dc.DrawImage(image, new Rect(left, top, width, height));
        }

        private void Request(Slot slot, Job job)
        {
            if (slot.Busy)
            {
                slot.Waiting = job; // replaces an older waiting request
                return;
            }
            Start(slot, job);
        }

        private void Start(Slot slot, Job job)
        {
            slot.Busy = true;
            BusyCount++;
            Task.Run(() => RenderJob(job)).ContinueWith(t =>
            {
                _dispatcher.BeginInvoke(() =>
                {
                    slot.Busy = false;
                    BusyCount--;
                    if (t.IsCompletedSuccessfully) Store(slot, job, t.Result);
                    if (slot.Waiting is Job next)
                    {
                        slot.Waiting = null;
                        if (next.Key != slot.ImageKey) Start(slot, next);
                    }
                    _onReady();
                });
            }, TaskScheduler.Default);
        }

        private static void Store(Slot slot, Job job, BitmapSource image)
        {
            slot.Image = image;
            slot.ImageKey = job.Key;
            slot.ImageWidthMm = job.CanvasWidthMm;
            slot.ImageHeightMm = job.CanvasHeightMm;
            slot.ImagePixelsPerMm = job.PixelsPerMm;
        }

        private static BitmapSource RenderJob(Job job)
        {
            int w = Math.Max(1, (int)Math.Ceiling(job.CanvasWidthMm * job.PixelsPerMm));
            int h = Math.Max(1, (int)Math.Ceiling(job.CanvasHeightMm * job.PixelsPerMm));
            return ResvgRasterizer.RenderAtDpi(job.Svg, job.PixelsPerMm * 25.4, w, h);
        }
    }
}
