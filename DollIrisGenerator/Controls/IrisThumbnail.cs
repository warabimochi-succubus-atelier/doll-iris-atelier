using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;

namespace DollIrisGenerator.Controls
{
    /// <summary>
    /// Small picture of a design for lists (favorites): the left eye, drawn by resvg from the exported SVG like the
    /// preview. An odd-eyed design shows both eyes overlapping, the left one top-left and the right one bottom-right,
    /// so it can be told apart at a glance. Drawn on a worker thread; pictures are kept per design object.
    /// </summary>
    internal sealed class IrisThumbnail : Grid
    {
        /// <summary>Share of the box each eye takes when both are shown.</summary>
        private const double OddEyeScale = 0.72;

        /// <summary>Pictures are drawn for the largest box they are shown in, on screens up to this many pixels per DIP.</summary>
        private const double MaxPixelsPerDip = 3.0;

        private static readonly ConditionalWeakTable<IrisParameters, Task<BitmapSource?>> LeftCache = new();
        private static readonly ConditionalWeakTable<IrisParameters, Task<BitmapSource?>> RightCache = new();

        public static readonly DependencyProperty DesignProperty = DependencyProperty.Register(
            nameof(Design), typeof(IrisParameters), typeof(IrisThumbnail),
            new PropertyMetadata(null, (d, _) => ((IrisThumbnail)d).Update()));

        public IrisParameters? Design
        {
            get => (IrisParameters?)GetValue(DesignProperty);
            set => SetValue(DesignProperty, value);
        }

        private readonly Image _left = new() { Stretch = Stretch.Uniform };
        private readonly Image _right = new() { Stretch = Stretch.Uniform };

        public IrisThumbnail()
        {
            Children.Add(_left);
            Children.Add(_right);
            RenderOptions.SetBitmapScalingMode(_left, BitmapScalingMode.HighQuality);
            RenderOptions.SetBitmapScalingMode(_right, BitmapScalingMode.HighQuality);
            SizeChanged += (_, _) => Update();
        }

        private void Update()
        {
            var design = Design;
            bool odd = design?.IsOddEye == true;

            _left.Source = _right.Source = null;
            _right.Visibility = odd ? Visibility.Visible : Visibility.Collapsed;
            ToolTip = odd ? "オッドアイ (左上: 左目 / 右下: 右目)" : null;

            if (odd)
            {
                _left.HorizontalAlignment = HorizontalAlignment.Left;
                _left.VerticalAlignment = VerticalAlignment.Top;
                _right.HorizontalAlignment = HorizontalAlignment.Right;
                _right.VerticalAlignment = VerticalAlignment.Bottom;
                _left.Width = _right.Width = Width * OddEyeScale;
                _left.Height = _right.Height = Height * OddEyeScale;
            }
            else
            {
                _left.HorizontalAlignment = HorizontalAlignment.Stretch;
                _left.VerticalAlignment = VerticalAlignment.Stretch;
                _left.Width = _left.Height = double.NaN;
            }

            if (design == null || double.IsNaN(Width) || Width <= 0) return;
            int sidePx = (int)Math.Ceiling(Math.Max(Width, Height) * MaxPixelsPerDip);
            Show(_left, design, isRight: false, sidePx);
            if (odd) Show(_right, design, isRight: true, sidePx);
        }

        private async void Show(Image image, IrisParameters design, bool isRight, int sidePx)
        {
            var cache = isRight ? RightCache : LeftCache;
            if (!cache.TryGetValue(design, out var task))
            {
                // The SVG is made from a copy, so the worker never reads a design the window may change
                var copy = design.Clone();
                task = Task.Run(() => Render(copy, isRight, sidePx));
                cache.AddOrUpdate(design, task);
            }
            var bitmap = await task;
            // Still showing the same design (the list reuses its rows)
            if (ReferenceEquals(Design, design) && (!isRight || design.IsOddEye)) image.Source = bitmap;
        }

        private static BitmapSource? Render(IrisParameters design, bool isRight, int sidePx)
        {
            try
            {
                const double padMm = 0.2;
                var (footW, footH) = SheetLayout.GetEyeFootprintMm(design);
                double widthMm = footW + padMm * 2.0, heightMm = footH + padMm * 2.0;
                string svg = SvgExporter.GenerateSingleEyeSvg(design, isRight, cutLine: false, centerMark: false, padMm);
                double pxPerMm = sidePx / Math.Max(widthMm, heightMm);
                int w = Math.Max(1, (int)Math.Ceiling(widthMm * pxPerMm));
                int h = Math.Max(1, (int)Math.Ceiling(heightMm * pxPerMm));
                return ResvgRasterizer.RenderAtDpi(svg, pxPerMm * 25.4, w, h);
            }
            catch (Exception)
            {
                // No picture (resvg missing, a broken design): the row stays without one
                return null;
            }
        }
    }
}
