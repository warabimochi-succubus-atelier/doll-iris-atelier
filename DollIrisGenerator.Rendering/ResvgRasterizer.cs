using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DollIrisGenerator.Services
{
    /// <summary>
    /// Rasterizes SVG with resvg (third_party/resvg, its C API through P/Invoke).
    /// The SVG from <see cref="SvgExporter"/> is the single source of the iris drawing; the screen, PNG and PDF
    /// are pictures of it made here (docs/design/svg-canonical-rendering.md).
    /// Unlike RenderTargetBitmap this needs no UI (STA) thread: every call has its own resvg objects, so calls
    /// may run on any thread, also at the same time.
    /// </summary>
    public static class ResvgRasterizer
    {
        private const string Lib = "resvg";

        /// <summary>
        /// Whether resvg.dll loads and draws (checked once at startup). <paramref name="error"/> says why not.
        /// </summary>
        public static bool TryCheck(out string? error)
        {
            try
            {
                var px = RenderToBgra("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1\" height=\"1\"><rect width=\"1\" height=\"1\" fill=\"#FF0000\" /></svg>", 1, 1);
                if (px[2] != 255 || px[3] != 255) throw new InvalidOperationException("unexpected pixels");
                error = null;
                return true;
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException
                                           or ResvgException or InvalidOperationException)
            {
                error = $"{ex.GetType().Name}: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Renders the whole SVG stretched to <paramref name="widthPx"/> × <paramref name="heightPx"/>
        /// (the SVG's width / height, e.g. in mm, map onto the full bitmap), over <paramref name="background"/>
        /// (transparent when null). Returns a frozen Pbgra32 bitmap at <paramref name="dpi"/>.
        /// </summary>
        public static BitmapSource Render(string svg, int widthPx, int heightPx, Color? background = null, double dpi = 96.0)
        {
            byte[] bgra = RenderToBgra(svg, widthPx, heightPx, background);
            var bmp = BitmapSource.Create(widthPx, heightPx, dpi, dpi, PixelFormats.Pbgra32, null, bgra, widthPx * 4);
            bmp.Freeze();
            return bmp;
        }

        /// <summary>
        /// Same as <see cref="Render"/>, as premultiplied BGRA bytes (row after row, 4 bytes a pixel):
        /// the layout of <see cref="PixelFormats.Pbgra32"/>.
        /// </summary>
        public static byte[] RenderToBgra(string svg, int widthPx, int heightPx, Color? background = null) =>
            RenderToBgra(svg, widthPx, heightPx, background, scale: null);

        /// <summary>
        /// Renders the SVG at its real size (its width / height in mm, in, px…) at <paramref name="dpi"/>, from the
        /// top-left corner, onto a <paramref name="widthPx"/> × <paramref name="heightPx"/> bitmap: geometry lands
        /// exactly where its mm say, whatever the bitmap's rounding. Returns a frozen Pbgra32 bitmap at that DPI.
        /// </summary>
        public static BitmapSource RenderAtDpi(string svg, double dpi, int widthPx, int heightPx, Color? background = null)
        {
            if (dpi <= 0) throw new ArgumentOutOfRangeException(nameof(dpi), "The DPI must be positive.");
            // resvg sizes the SVG in CSS pixels (96 per inch)
            byte[] bgra = RenderToBgra(svg, widthPx, heightPx, background, scale: dpi / 96.0);
            var bmp = BitmapSource.Create(widthPx, heightPx, dpi, dpi, PixelFormats.Pbgra32, null, bgra, widthPx * 4);
            bmp.Freeze();
            return bmp;
        }

        /// <summary>Stretches the SVG over the bitmap (<paramref name="scale"/> null) or scales it uniformly.</summary>
        private static byte[] RenderToBgra(string svg, int widthPx, int heightPx, Color? background, double? scale)
        {
            ArgumentNullException.ThrowIfNull(svg);
            if (widthPx <= 0 || heightPx <= 0) throw new ArgumentOutOfRangeException(nameof(widthPx), "The bitmap size must be positive.");

            byte[] data = Encoding.UTF8.GetBytes(svg);
            IntPtr opt = resvg_options_create();
            IntPtr tree = IntPtr.Zero;
            try
            {
                int err = resvg_parse_tree_from_data(data, (UIntPtr)data.Length, opt, out tree);
                if (err != 0) throw new ResvgException(err);

                var size = resvg_get_image_size(tree);
                if (size.Width <= 0 || size.Height <= 0) throw new ResvgException(ErrorInvalidSize);

                // resvg draws onto the given pixels (source-over), so a background is just the starting pixels
                byte[] pixels = new byte[checked(widthPx * heightPx * 4)];
                if (background is Color bg && bg.A > 0) FillPremultipliedRgba(pixels, bg);

                var transform = new ResvgTransform
                {
                    A = scale is double s ? (float)s : widthPx / size.Width,
                    D = scale is double t ? (float)t : heightPx / size.Height,
                };
                resvg_render(tree, transform, (uint)widthPx, (uint)heightPx, pixels);

                SwapRedBlue(pixels); // resvg: premultiplied RGBA, WPF: premultiplied BGRA
                return pixels;
            }
            finally
            {
                if (tree != IntPtr.Zero) resvg_tree_destroy(tree);
                resvg_options_destroy(opt);
            }
        }

        private static void FillPremultipliedRgba(byte[] pixels, Color c)
        {
            byte r = (byte)((c.R * c.A + 127) / 255), g = (byte)((c.G * c.A + 127) / 255), b = (byte)((c.B * c.A + 127) / 255);
            uint rgba = (uint)(r | (g << 8) | (b << 16) | (c.A << 24)); // little-endian: bytes R, G, B, A
            MemoryMarshal.Cast<byte, uint>(pixels.AsSpan()).Fill(rgba);
        }

        private static void SwapRedBlue(byte[] pixels)
        {
            var px = MemoryMarshal.Cast<byte, uint>(pixels.AsSpan());
            for (int i = 0; i < px.Length; i++)
            {
                uint v = px[i];
                px[i] = (v & 0xFF00FF00u) | ((v & 0x000000FFu) << 16) | ((v & 0x00FF0000u) >> 16);
            }
        }

        // ---- resvg C API (crates/c-api/resvg.h) ----

        internal const int ErrorInvalidSize = 6; // RESVG_ERROR_INVALID_SIZE

        [StructLayout(LayoutKind.Sequential)]
        private struct ResvgTransform
        {
            public float A, B, C, D, E, F;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ResvgSize
        {
            public float Width, Height;
        }

        [DllImport(Lib)] private static extern IntPtr resvg_options_create();
        [DllImport(Lib)] private static extern void resvg_options_destroy(IntPtr opt);
        [DllImport(Lib)] private static extern int resvg_parse_tree_from_data(byte[] data, UIntPtr len, IntPtr opt, out IntPtr tree);
        [DllImport(Lib)] private static extern ResvgSize resvg_get_image_size(IntPtr tree);
        [DllImport(Lib)] private static extern void resvg_render(IntPtr tree, ResvgTransform transform, uint width, uint height, [Out] byte[] pixmap);
        [DllImport(Lib)] private static extern void resvg_tree_destroy(IntPtr tree);
    }

    /// <summary>resvg could not read the SVG (error codes of resvg.h).</summary>
    public sealed class ResvgException : Exception
    {
        public int ErrorCode { get; }

        public ResvgException(int errorCode) : base($"resvg could not read the SVG ({Describe(errorCode)}).") => ErrorCode = errorCode;

        private static string Describe(int code) => code switch
        {
            1 => "not UTF-8",
            2 => "SVGZ unsupported",
            3 => "file open failed",
            4 => "malformed gzip",
            5 => "elements limit reached",
            6 => "invalid size",
            7 => "parsing failed",
            _ => "error " + code,
        };
    }
}
