using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using Xunit;

namespace DollIrisGenerator.Tests
{
    /// <summary>SVG rasterizing with resvg (third_party/resvg).</summary>
    public class ResvgRasterizerTests
    {
        private const string TwoHalves =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10mm\" height=\"5mm\" viewBox=\"0 0 10 5\">" +
            "<rect x=\"0\" y=\"0\" width=\"5\" height=\"5\" fill=\"#FF0000\" />" +
            "<rect x=\"5\" y=\"0\" width=\"5\" height=\"5\" fill=\"#0000FF\" fill-opacity=\"0.5\" />" +
            "</svg>";

        /// <summary>B, G, R, A of one pixel of premultiplied BGRA bytes.</summary>
        private static (int B, int G, int R, int A) At(byte[] bgra, int width, int x, int y)
        {
            int i = (y * width + x) * 4;
            return (bgra[i], bgra[i + 1], bgra[i + 2], bgra[i + 3]);
        }

        [Fact]
        public void Render_StretchesTheSvgOverTheBitmap_InBgraOrder()
        {
            var px = ResvgRasterizer.RenderToBgra(TwoHalves, 200, 100);

            Assert.Equal(200 * 100 * 4, px.Length);
            Assert.Equal((0, 0, 255, 255), At(px, 200, 50, 50));    // opaque red on the left half
            Assert.Equal((128, 0, 0, 128), At(px, 200, 150, 50));   // half blue, premultiplied, on the right half
        }

        [Fact]
        public void Render_DrawsOverTheBackground()
        {
            var px = ResvgRasterizer.RenderToBgra(TwoHalves, 200, 100, Colors.White);

            Assert.Equal((0, 0, 255, 255), At(px, 200, 50, 50));
            var (b, g, r, a) = At(px, 200, 150, 50); // half blue over white = light blue
            Assert.Equal(255, a);
            Assert.Equal(255, b);
            Assert.InRange(r, 126, 129);
            Assert.InRange(g, 126, 129);
        }

        [Fact]
        public void Render_ReturnsAFrozenPbgra32BitmapAtTheGivenDpi()
        {
            var bmp = ResvgRasterizer.Render(TwoHalves, 40, 20, null, dpi: 1200);

            Assert.True(bmp.IsFrozen);
            Assert.Equal(PixelFormats.Pbgra32, bmp.Format);
            Assert.Equal((40, 20), (bmp.PixelWidth, bmp.PixelHeight));
            Assert.Equal(1200, bmp.DpiX, 3);
        }

        [Fact]
        public void Render_ThrowsOnBrokenSvg()
        {
            var ex = Assert.Throws<ResvgException>(() => ResvgRasterizer.RenderToBgra("<svg", 10, 10));
            Assert.NotEqual(0, ex.ErrorCode);
            Assert.Throws<ArgumentOutOfRangeException>(() => ResvgRasterizer.RenderToBgra(TwoHalves, 0, 10));
        }

        /// <summary>
        /// The exported eye SVG renders: the pupil color in the middle, white outside the iris,
        /// and the embedded nebula PNG (raster-images) is drawn.
        /// </summary>
        [Fact]
        public void Render_DrawsTheExportedIris()
        {
            var p = new IrisParameters { SizeMm = 10.0, PupilColor = "#060A18", PupilOpacity = 1.0, PupilSizeRatio = 0.3, PupilFacetIntensity = 0 };
            string svg = SvgExporter.GenerateSingleEyeSvg(p, isRightEye: false); // 10mm + 2mm padding each side
            const int size = 280; // 14mm at 20 px/mm
            var px = ResvgRasterizer.RenderToBgra(svg, size, size, Colors.White);

            var (b, g, r, a) = At(px, size, size / 2, size / 2);
            Assert.Equal(255, a);
            Assert.True(r < 40 && g < 40 && b < 60, $"pupil expected dark, got R{r} G{g} B{b}");
            Assert.Equal((255, 255, 255, 255), At(px, size, 3, 3));

            var nebula = new IrisParameters { SizeMm = 10.0, NebulaOpacity = 1.0, NebulaColor = "#FF00FF" };
            var withNebula = ResvgRasterizer.RenderToBgra(SvgExporter.GenerateSingleEyeSvg(nebula, false), size, size, Colors.White);
            var without = ResvgRasterizer.RenderToBgra(SvgExporter.GenerateSingleEyeSvg(new IrisParameters { SizeMm = 10.0 }, false), size, size, Colors.White);
            Assert.False(withNebula.SequenceEqual(without));
        }

        /// <summary>No UI thread needed, and calls may overlap: the same SVG gives the same pixels on every thread.</summary>
        [Fact]
        public void Render_IsSafeOnParallelThreads()
        {
            string svg = SvgExporter.GenerateSingleEyeSvg(new IrisParameters { SizeMm = 10.0 }, false);
            var expected = ResvgRasterizer.RenderToBgra(svg, 140, 140);

            var results = new byte[8][];
            Parallel.For(0, results.Length, i => results[i] = ResvgRasterizer.RenderToBgra(svg, 140, 140));

            Assert.All(results, r => Assert.True(r.SequenceEqual(expected)));
        }
    }
}
