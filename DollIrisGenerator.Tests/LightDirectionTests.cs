using System.Text.RegularExpressions;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using Xunit;

namespace DollIrisGenerator.Tests
{
    /// <summary>光の入る向き: the base gradient's brightest point moves toward the light.</summary>
    public class LightDirectionTests
    {
        private static (double Fx, double Fy) Focus(string svg, string eye = "eye_left")
        {
            var m = Regex.Match(svg, $"<radialGradient id=\"{eye}_rad_grad\" [^>]*fx=\"([\\d.]+)%\" fy=\"([\\d.]+)%\"");
            Assert.True(m.Success, "focal point expected");
            return (double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                    double.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture));
        }

        [Fact]
        public void OffByDefaultAndExportsAsBefore()
        {
            var p = new IrisParameters();
            Assert.Equal(0.0, p.LightStrength);
            Assert.Equal(270.0, p.LightAngle);
            Assert.Contains("cx=\"50%\" cy=\"42%\" r=\"55%\">", SvgExporter.GenerateSingleEyeSvg(p, false));
        }

        [Fact]
        public void FocusMovesTowardTheLight()
        {
            var p = new IrisParameters { LightStrength = 1.0, LightAngle = 270 }; // from the top
            var (fx, fy) = Focus(SvgExporter.GenerateSingleEyeSvg(p, false));
            Assert.Equal(50.0, fx, 3);
            Assert.Equal(2.0, fy, 3); // 42% − 40%

            p.LightAngle = 0; // from the right, half strength
            p.LightStrength = 0.5;
            (fx, fy) = Focus(SvgExporter.GenerateSingleEyeSvg(p, false));
            Assert.Equal(70.0, fx, 3);
            Assert.Equal(42.0, fy, 3);
        }

        /// <summary>The light stays where it is on screen when the iris is tilted (the gradient turns with the iris).</summary>
        [Fact]
        public void TiltIsTakenBackOut()
        {
            var p = new IrisParameters { LightStrength = 1.0, LightAngle = 0, TiltAngle = 30, Shape = IrisShape.VerticalOval, AspectRatio = 1.2 };
            var (fx, fy) = Focus(SvgExporter.GenerateSingleEyeSvg(p, false));
            // Turned back by −30° inside the rotated group: 40% toward −30°
            Assert.Equal(50.0 + 40.0 * System.Math.Cos(-System.Math.PI / 6), fx, 2);
            Assert.Equal(42.0 - 20.0, fy, 2);
        }

        /// <summary>The right eye mirrors the light only when it mirrors the highlights.</summary>
        [Fact]
        public void RightEyeFollowsTheHighlightMirroring()
        {
            var p = new IrisParameters { LightStrength = 1.0, LightAngle = 0, MirrorHighlightsWithIris = true };
            Assert.Equal(10.0, Focus(SvgExporter.GenerateSingleEyeSvg(p, true), "eye_right").Fx, 3);

            p.MirrorHighlightsWithIris = false;
            Assert.Equal(90.0, Focus(SvgExporter.GenerateSingleEyeSvg(p, true), "eye_right").Fx, 3);
        }

        /// <summary>Drawn: the side the light comes from is brighter than the other side.</summary>
        [Fact]
        public void TheLitSideIsBrighter()
        {
            int Brightness(IrisParameters p, double fx, double fy)
            {
                var bmp = RasterExporter.RenderSingleToBitmap(p, false, dpi: 200);
                int w = bmp.PixelWidth, h = bmp.PixelHeight;
                var px = new byte[w * h * 4];
                bmp.CopyPixels(px, w * 4, 0);
                int i = ((int)(h * fy) * w + (int)(w * fx)) * 4;
                return px[i] + px[i + 1] + px[i + 2];
            }
            IrisParameters Plain(double strength, double angle) => new()
            {
                SizeMm = 10, LightStrength = strength, LightAngle = angle, RayCount = 0, ParticleCount = 0,
                BottomHighlightIntensity = 0, LimbalRingThickness = 0, PupilSizeRatio = 0.1,
                InnerColor = "#FFFFFF", BaseColor = "#2050C0", OuterColor = "#000040"
            };

            var fromLeft = Plain(1.0, 180);
            fromLeft.Highlights.Clear();
            Assert.True(Brightness(fromLeft, 0.3, 0.5) > Brightness(fromLeft, 0.7, 0.5) + 60, "left side should be brighter");

            var fromRight = Plain(1.0, 0);
            fromRight.Highlights.Clear();
            Assert.True(Brightness(fromRight, 0.7, 0.5) > Brightness(fromRight, 0.3, 0.5) + 60, "right side should be brighter");
        }

        [Fact]
        public void PresetsResetTheLightAndKeepItWhenSaved()
        {
            var p = new IrisParameters { LightStrength = 0.6, LightAngle = 200 };
            ColorPresets.ApplyPreset(p, ColorPresets.All[0]);
            Assert.Equal(0.0, p.LightStrength);
            Assert.Equal(270.0, p.LightAngle);

            var lit = new IrisParameters { LightStrength = 0.6, LightAngle = 200 };
            var preset = ColorPresets.FromDesign(lit, "x", "");
            var q = new IrisParameters();
            ColorPresets.ApplyPreset(q, preset);
            Assert.Equal(0.6, q.LightStrength);
            Assert.Equal(200.0, q.LightAngle);

            var copy = lit.Clone();
            Assert.Equal(0.6, copy.LightStrength);
            Assert.Equal(200.0, copy.LightAngle);

            Assert.Equal(1.0, new IrisParameters { LightStrength = 5 }.LightStrength);
            Assert.Equal(0.0, new IrisParameters { LightStrength = double.NaN }.LightStrength);
        }
    }
}
