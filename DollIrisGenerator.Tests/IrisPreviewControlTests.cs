using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DollIrisGenerator.Controls;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using Xunit;

namespace DollIrisGenerator.Tests
{
    /// <summary>The preview draws the eyes as pictures of their SVG (resvg), redrawn in the background after changes.</summary>
    public class IrisPreviewControlTests
    {
        private const double ViewW = 600, ViewH = 300, Zoom = 3.6;

        // The whole A4 sheet fits in the view at this zoom (297 mm ≈ 252 DIP)
        private const double SheetZoom = 0.225;

        private static IrisPreviewControl Create(IrisParameters p, PreviewMode mode = PreviewMode.Pair)
        {
            var ctrl = new IrisPreviewControl
            {
                Parameters = p,
                PreviewMode = mode,
                ZoomScale = mode == PreviewMode.Pair ? Zoom : SheetZoom,
                ShowCutLines = false,
                ShowCenterMarks = false,
            };
            ctrl.Measure(new Size(ViewW, ViewH));
            ctrl.Arrange(new Rect(0, 0, ViewW, ViewH));
            return ctrl;
        }

        private static Color PixelAt(UIElement element, Point at)
        {
            element.UpdateLayout(); // runs OnRender for pending changes, as the next frame would
            var bmp = new RenderTargetBitmap((int)ViewW, (int)ViewH, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(element);
            var px = new byte[4];
            bmp.CopyPixels(new Int32Rect((int)at.X, (int)at.Y, 1, 1), px, 4, 0);
            return Color.FromArgb(px[3], px[2], px[1], px[0]);
        }

        /// <summary>Center of the left eye in the pair view (same layout as IrisPreviewControl, spacing 4 mm).</summary>
        private static Point LeftEyeCenter(IrisParameters p)
        {
            double ppm = IrisPreviewControl.ScreenDipsPerMm * Zoom;
            var (w, _) = SheetLayout.GetEyeFootprintMm(p);
            double halfSpanX = w * ppm + 4.0 * ppm / 2.0;
            return new Point(ViewW / 2.0 - halfSpanX + w * ppm / 2.0, ViewH / 2.0);
        }

        /// <summary>Runs the dispatcher until <paramref name="done"/> (background pictures arrive through it).</summary>
        private static void PumpUntil(Func<bool> done, double seconds = 10)
        {
            var deadline = DateTime.UtcNow.AddSeconds(seconds);
            while (!done())
            {
                Assert.True(DateTime.UtcNow < deadline, "timed out waiting for the preview");
                var frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false);
                Dispatcher.PushFrame(frame);
                System.Threading.Thread.Sleep(5);
            }
        }

        [Fact]
        public void FirstViewShowsTheEyesAtOnce() => StaRunner.Run(() =>
        {
            var p = new IrisParameters { SizeMm = 10.0, PupilColor = "#000000" };
            var ctrl = Create(p);

            var c = PixelAt(ctrl, LeftEyeCenter(p));
            Assert.True(c.R < 30 && c.G < 30 && c.B < 30, $"pupil expected black, got {c}");
            Assert.Equal(0, ctrl.EyePicturesInProgress);
        });

        /// <summary>
        /// A change keeps the old picture on screen until the new one, drawn in the background, arrives;
        /// then the preview shows the new state.
        /// </summary>
        [Fact]
        public void ChangesArriveFromTheBackground() => StaRunner.Run(() =>
        {
            var p = new IrisParameters { SizeMm = 10.0, PupilColor = "#000000" };
            var ctrl = Create(p);
            PixelAt(ctrl, LeftEyeCenter(p));

            p.PupilColor = "#FF0000";
            var stale = PixelAt(ctrl, LeftEyeCenter(p));
            Assert.True(stale.R < 30, $"old picture expected while the new one is drawn, got {stale}");

            PumpUntil(() => ctrl.EyePicturesInProgress == 0);
            var fresh = PixelAt(ctrl, LeftEyeCenter(p));
            Assert.True(fresh.R > 200 && fresh.G < 60 && fresh.B < 60, $"new red pupil expected, got {fresh}");
        });

        /// <summary>Many changes while one picture is drawn: only the newest state is drawn next, and shown.</summary>
        [Fact]
        public void OnlyTheNewestChangeIsDrawn() => StaRunner.Run(() =>
        {
            var p = new IrisParameters { SizeMm = 10.0, PupilColor = "#000000" };
            var ctrl = Create(p);
            PixelAt(ctrl, LeftEyeCenter(p));

            foreach (var color in new[] { "#FF0000", "#00FF00", "#0000FF", "#FFFF00", "#00FFFF" })
            {
                p.PupilColor = color;
                PixelAt(ctrl, LeftEyeCenter(p));
                Assert.InRange(ctrl.EyePicturesInProgress, 0, 2); // left and right eye at most, never a queue
            }

            PumpUntil(() => ctrl.EyePicturesInProgress == 0);
            PixelAt(ctrl, LeftEyeCenter(p)); // may start the newest one if it was waiting
            PumpUntil(() => ctrl.EyePicturesInProgress == 0);
            var c = PixelAt(ctrl, LeftEyeCenter(p));
            Assert.True(c.R < 60 && c.G > 200 && c.B > 200, $"newest (cyan) pupil expected, got {c}");
        });

        /// <summary>The A4 view draws the sheet's eyes (the current design when the sheet is empty).</summary>
        [Fact]
        public void SheetViewShowsTheEyes() => StaRunner.Run(() =>
        {
            // Big eye and pupil: the sheet is shown small (about 10 px of pupil)
            var p = new IrisParameters { SizeMm = 20.0, PupilSizeRatio = 0.6, PupilColor = "#000000" };
            var cfg = new PrintSheetConfig();
            var ctrl = Create(p, PreviewMode.A4Sheet);
            ctrl.SheetConfig = cfg;

            var layout = new SheetLayout(new[] { new SheetItem(p, 1) }, cfg);
            var (leftX, _, y) = layout.GetEyeCentersMm(0);
            double ppm = IrisPreviewControl.ScreenDipsPerMm * SheetZoom;
            double sheetTop = IrisPreviewControl.SheetHomeCenterY(ViewH, 297.0 * ppm) - 297.0 * ppm / 2.0;
            var at = new Point(ViewW / 2.0 - 210.0 * ppm / 2.0 + leftX * ppm, sheetTop + y * ppm);

            var c = PixelAt(ctrl, at);
            Assert.True(c.R < 40 && c.G < 40 && c.B < 40, $"pupil expected black on the sheet, got {c}");
        });
    }

    /// <summary>Runs WPF code on an STA thread; failures are rethrown on the test thread.</summary>
    internal static class StaRunner
    {
        public static void Run(Action body)
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
