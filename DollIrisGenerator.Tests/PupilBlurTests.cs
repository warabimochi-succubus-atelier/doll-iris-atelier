using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using Xunit;

namespace DollIrisGenerator.Tests
{
    /// <summary>輪郭の柔らかさ: the pupil's fill is blurred, with a sharp core inside.</summary>
    public class PupilBlurTests
    {
        /// <summary>
        /// A blurred pupil gets a sharp core inside (a small blur can leave the blurred inside slightly see-through);
        /// a sharp pupil gets none.
        /// </summary>
        [Fact]
        public void BlurredPupilHasASolidCore()
        {
            string soft = SvgExporter.GenerateSingleEyeSvg(new IrisParameters { PupilSoftness = 0.2 }, false);
            Assert.Contains("<filter id=\"eye_left_pupil_blur\"", soft);
            Assert.Contains("<rect id=\"eye_left_pupil_core\"", soft);
            Assert.Contains("clip-path=\"url(#eye_left_pupil_core_clip)\"", soft);

            string sharp = SvgExporter.GenerateSingleEyeSvg(new IrisParameters { PupilSoftness = 0 }, false);
            Assert.DoesNotContain("pupil_core", sharp);
            Assert.DoesNotContain("pupil_blur", sharp);
        }

        /// <summary>Drawn at preview size (where the blur is a few pixels): the pupil's middle is exactly its color.</summary>
        [Fact]
        public void PupilMiddleIsSolidAtPreviewSize()
        {
            var p = new IrisParameters { SizeMm = 10, PupilSoftness = 0.08, PupilColor = "#0B0826", PupilSizeRatio = 0.32, PupilFacetIntensity = 0 };
            var bmp = RasterExporter.RenderSingleToBitmap(p, false, dpi: 600);
            int w = bmp.PixelWidth, h = bmp.PixelHeight;
            var px = new byte[w * h * 4];
            bmp.CopyPixels(px, w * 4, 0);
            for (int dy = -10; dy <= 10; dy += 5)
                for (int dx = -10; dx <= 10; dx += 5)
                {
                    int i = ((h / 2 + dy) * w + (w / 2 + dx)) * 4;
                    Assert.Equal((0x26, 0x08, 0x0B), (px[i], px[i + 1], px[i + 2]));
                }
        }
    }
}
