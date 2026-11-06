using System;
using System.Linq;
using System.Text.Json;
using System.Windows;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using Xunit;

namespace DollIrisGenerator.Tests
{
    /// <summary>Conic (円周) gradients / aurora layer, fiber-style rays and glowing glitter.</summary>
    public class SampleLooksTests
    {
        /// <summary>A design with every new look turned off (they are on by default).</summary>
        private static IrisParameters Plain() => new()
        {
            RayTaper = 0, RayWidthVariation = 0, RayCurve = 0, RayFineLayer = 0,
            ParticleGlow = 0, ParticleFlareRatio = 0
        };

        private const string Aurora = "conic-gradient(from 270deg, #FF0000 0%, #00FF00 50%, #FF0000 100%)";

        /// <summary>WPF rendering needs an STA thread; failures are rethrown on the test thread.</summary>
        private static void RunOnSta(Action body)
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

        // ---------------- Conic gradients ----------------

        [Fact]
        public void TestConicGradientRoundTripsThroughItsString()
        {
            var fill = ColorFill.Parse(Aurora);
            Assert.Equal(ColorFillKind.Conic, fill.Kind);
            Assert.Equal(270.0, fill.Angle);
            Assert.Equal(3, fill.Stops.Count);
            Assert.Equal(Aurora, fill.ToString());
            Assert.Equal(Aurora, ColorFill.Parse(fill.ToString()).ToString());
        }

        [Fact]
        public void TestConicWithoutFromStartsAtTheTop()
        {
            var fill = ColorFill.Parse("conic-gradient(#FF0000 0%, #0000FF 100%)");
            Assert.Equal(ColorFillKind.Conic, fill.Kind);
            Assert.Equal(ColorFill.ConicDefaultAngle, fill.Angle);
        }

        [Theory]
        [InlineData("linear-gradient(from 90deg, #FF0000 0%, #0000FF 100%)")]  // "from" is conic only
        [InlineData("conic-gradient(90deg, #FF0000 0%, #0000FF 100%)")]        // conic needs "from"
        [InlineData("radial-gradient(from 90deg, #FF0000 0%, #0000FF 100%)")]
        [InlineData("conic-gradient(from 90deg, #FF0000 0%)")]                  // one stop
        public void TestMalformedAngleNotationsStaySolid(string value)
        {
            var fill = ColorFill.Parse(value);
            Assert.False(fill.IsGradient);
            Assert.Equal(value, fill.PrimaryColor);
        }

        [Fact]
        public void TestConicOffsetGoesRoundClockwiseFromItsStart()
        {
            var map = GradientMapping.Create(ColorFill.Parse(Aurora), new Rect(-1, -1, 2, 2));
            Assert.Equal(0.0, map.OffsetAt(new Point(0, -1)), 6);   // up = start (270°)
            Assert.Equal(0.25, map.OffsetAt(new Point(1, 0)), 6);   // right
            Assert.Equal(0.5, map.OffsetAt(new Point(0, 1)), 6);    // down
            Assert.Equal(0.75, map.OffsetAt(new Point(-1, 0)), 6);  // left
            Assert.Equal("#FF00FF00", map.ColorAt(new Point(0, 1)));
        }

        [Fact]
        public void TestMirroredConicShowsTheMirrorImage()
        {
            var fill = ColorFill.Parse("conic-gradient(from 20deg, #FF0000 0%, #00FF00 40%, #0000FF 100%)");
            var box = new Rect(-1, -1, 2, 2);
            var map = GradientMapping.Create(fill, box);
            var mirrored = GradientMapping.Create(fill.MirroredX(), box);

            for (int deg = 5; deg < 360; deg += 17)
            {
                double rad = deg * Math.PI / 180.0;
                var pt = new Point(Math.Cos(rad), Math.Sin(rad));
                var mirrorPt = new Point(-pt.X, pt.Y);
                Assert.Equal(map.ColorAt(pt), mirrored.ColorAt(mirrorPt));
            }
        }

        [Fact]
        public void TestColorAtInterpolatesAllChannels()
        {
            var stops = new[] { new ColorStop("#00000000", 0.0), new ColorStop("#FFFF8000", 1.0) };
            Assert.Equal("#80804000", ColorFill.ColorAt(stops, 0.5));
            Assert.Equal("#00000000", ColorFill.ColorAt(stops, -1.0));
            Assert.Equal("#FFFF8000", ColorFill.ColorAt(stops, 2.0));
            Assert.Equal("#FF123456", ColorFill.ColorAt(new[] { new ColorStop("#123456", 0.0), new ColorStop("#123456", 1.0) }, 0.3));
        }

        [Fact]
        public void TestConicWedgesCoverTheCircleOnce()
        {
            var map = GradientMapping.Create(ColorFill.Parse(Aurora), new Rect(0, 0, 10, 10));
            var wedges = map.ConicWedges().ToList();
            Assert.Equal(GradientMapping.ConicSegments, wedges.Count);
            Assert.Equal(270.0, wedges[0].From, 6);
            Assert.Equal(270.0 + 360.0, wedges[^1].To, 6);
            for (int i = 1; i < wedges.Count; i++) Assert.Equal(wedges[i - 1].To, wedges[i].From, 6);
        }

        [Fact]
        public void TestDefaultConicGradientWrapsWithoutASeam()
        {
            var fill = ColorFill.DefaultGradientFrom("#2979FF", ColorFillKind.Conic);
            Assert.Equal(ColorFillKind.Conic, fill.Kind);
            Assert.Equal(fill.Stops[0].Color, fill.Stops[^1].Color);
        }

        // ---------------- Aurora layer ----------------

        [Fact]
        public void TestAuroraIsOffByDefaultAndForOldFiles()
        {
            Assert.Equal(0.0, new IrisParameters().AuroraOpacity);
            var old = JsonSerializer.Deserialize<IrisParameters>("{\"Name\":\"旧\"}")!;
            Assert.Equal(0.0, old.AuroraOpacity);
            Assert.DoesNotContain("_aurora", SvgExporter.GeneratePairSvg(old));
        }

        [Fact]
        public void TestAuroraLayerIsExportedAsAWedgePattern()
        {
            var p = new IrisParameters { AuroraColor = Aurora, AuroraOpacity = 0.4 };
            string svg = SvgExporter.GeneratePairSvg(p);
            Assert.True(SvgValidator.IsWellFormed(svg, out var error), error);
            Assert.Contains("left_eye_aurora_fill", svg);
            Assert.Contains("right_eye_aurora_fill", svg);
            Assert.Contains("<pattern id=\"left_eye_aurora_fill\" patternUnits=\"userSpaceOnUse\"", svg);
            Assert.Contains("fill=\"url(#left_eye_aurora_fill)\" opacity=\"0.40\"", svg);
        }

        [Fact]
        public void TestConicWorksOnEveryGradientColor()
        {
            var p = new IrisParameters
            {
                RayColor = Aurora,
                ParticleColor = Aurora,
                LimbalRingColor = Aurora,
                PupilColor = Aurora,
                ParticleGlow = 0.5
            };
            p.Highlights[0].Color = Aurora;
            string svg = SvgExporter.GeneratePairSvg(p);
            Assert.True(SvgValidator.IsWellFormed(svg, out var error), error);
            foreach (var id in new[] { "rays_fill", "glitter_fill", "limbal_fill", "pupil_fill", "highlight0_fill" })
                Assert.Contains($"<pattern id=\"left_eye_{id}\"", svg);
        }

        [Fact]
        public void TestConicBrushRendersOnScreen() => RunOnSta(ConicBrushRendersOnScreen);

        private static void ConicBrushRendersOnScreen()
        {
            var p = new IrisParameters { AuroraColor = Aurora, AuroraOpacity = 1.0, RayCount = 0, ParticleCount = 0, PupilOpacity = 0 };
            p.Highlights.Clear();
            p.LimbalRingOpacity = 0;
            p.BottomHighlightIntensity = 0;
            var bmp = RasterExporter.RenderSingleToBitmap(p, isRightEye: false, dpi: 300.0, showCutLines: false, showCenterMarks: false);
            int w = bmp.PixelWidth, h = bmp.PixelHeight;
            var px = new byte[w * h * 4];
            bmp.CopyPixels(px, w * 4, 0);

            // Lower half (away from the upper shadow): left of center is past 50 % → red-ish going back to red,
            // right of center is before 50 % → green-ish; just below the center it is green (50 %)
            (byte R, byte G) At(double fx, double fy)
            {
                int x = (int)(w * fx), y = (int)(h * fy);
                int i = (y * w + x) * 4;
                return (px[i + 2], px[i + 1]);
            }
            var below = At(0.5, 0.7);
            Assert.True(below.G > below.R, $"below center should be green, got R{below.R} G{below.G}");
        }

        // ---------------- Fiber rays ----------------

        [Fact]
        public void TestNewLooksAreOnByDefault()
        {
            var p = new IrisParameters();
            Assert.True(p.HasFiberRays);
            Assert.True(p.ParticleGlow > 0 && p.ParticleFlareRatio > 0);
            Assert.Equal(0.0, p.AuroraOpacity); // the aurora does not suit every color, so it stays off
            string svg = SvgExporter.GeneratePairSvg(p);
            Assert.Contains("<g id=\"left_eye_rays\" fill=", svg);
            Assert.Contains("<g id=\"left_eye_fine_rays\" fill=", svg);
            Assert.Contains("<g id=\"left_eye_glitter_glow\">", svg);
        }

        [Fact]
        public void TestAllFiberSettingsAtZeroGivePlainLines()
        {
            var p = Plain();
            Assert.False(p.HasFiberRays);
            string svg = SvgExporter.GeneratePairSvg(p);
            Assert.Contains("<g id=\"left_eye_rays\" stroke=", svg);
            Assert.DoesNotContain("fine_rays", svg);
            Assert.DoesNotContain("glitter_glow", svg);
        }

        [Fact]
        public void TestFiberSettingsLeaveTheRayLayoutAlone()
        {
            var plain = Plain();
            var fiber = new IrisParameters { RayTaper = 0.8, RayWidthVariation = 0.9, RayCurve = 0.5, RayFineLayer = 0.6 };
            var a = IrisPatterns.MainRays(plain, 100, plain.Seed, false);
            var b = IrisPatterns.MainRays(fiber, 100, fiber.Seed, false);
            Assert.Equal(a.Select(r => (r.Angle, r.InnerR, r.OuterR)), b.Select(r => (r.Angle, r.InnerR, r.OuterR)));
            Assert.All(a, r => Assert.Equal(1.0, r.WidthFactor));
            Assert.Contains(b, r => Math.Abs(r.WidthFactor - 1.0) > 0.05);
        }

        [Fact]
        public void TestTaperedRayIsWideAtTheRootAndPointedAtTheTip()
        {
            var ray = new RaySpec(0.0, 20.0, 80.0, 1.0);
            var outline = IrisPatterns.FiberOutline(new Point(0, 0), 100, 100, 100, ray, 10.0, 1.0, 0.0, false);
            var pts = outline.Flatten(1);
            double WidthNear(double x) => pts.Where(q => Math.Abs(q.X - x) < 1.0).Select(q => Math.Abs(q.Y)).DefaultIfEmpty(0).Max() * 2.0;
            Assert.True(WidthNear(20.0) > 10.0, "root wider than 線幅 at full taper");
            Assert.True(WidthNear(80.0) < 2.0, "tip pointed at full taper");
        }

        [Fact]
        public void TestCurvedRaySwirlsTheOtherWayInTheRightEye()
        {
            var p = new IrisParameters { RayCurve = 1.0 };
            var ray = new RaySpec(0.0, 20.0, 80.0, 1.0);
            var left = IrisPatterns.FiberOutline(new Point(0, 0), 100, 100, 100, ray, 2.0, 0.0, p.RayCurve, false).GetBounds();
            var mirroredRay = ray with { Angle = Math.PI };
            var right = IrisPatterns.FiberOutline(new Point(0, 0), 100, 100, 100, mirroredRay, 2.0, 0.0, p.RayCurve, true).GetBounds();
            // Left eye bends clockwise (down on screen), the mirrored right eye bends the same way seen in a mirror
            Assert.True(left.Bottom > 30.0);
            Assert.Equal(left.Top, right.Top, 6);
            Assert.Equal(left.Bottom, right.Bottom, 6);
            Assert.Equal(left.Left, -right.Right, 6);
        }

        [Fact]
        public void TestFiberRaysExportAsFilledOutlines()
        {
            var p = new IrisParameters { RayTaper = 0.6, RayCurve = -0.4, RayFineLayer = 0.5 };
            string svg = SvgExporter.GeneratePairSvg(p);
            Assert.True(SvgValidator.IsWellFormed(svg, out var error), error);
            Assert.Contains("<g id=\"left_eye_rays\" fill=", svg);
            Assert.Contains("<g id=\"left_eye_fine_rays\" fill=", svg);
            int paths = svg.Split("<g id=\"left_eye_rays\"")[1].Split("</g>")[0].Split("<path ").Length - 1;
            Assert.Equal(p.RayCount, paths);
        }

        [Fact]
        public void TestFineRaysWithPlainLinesAreThinner()
        {
            var p = Plain();
            p.RayFineLayer = 0.5;
            string svg = SvgExporter.GeneratePairSvg(p);
            double main = p.RayThickness * WpfIrisRenderer.MmPerStyleUnit;
            Assert.Contains($"<g id=\"left_eye_fine_rays\" stroke=\"{p.RayColor}\" stroke-width=\"{(main * IrisPatterns.FineRayWidthFactor).ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}\"", svg);
        }

        // ---------------- Glowing glitter ----------------

        [Fact]
        public void TestGlowAndFlaresLeaveTheGrainsWhereTheyWere()
        {
            var plain = Plain();
            var glowing = new IrisParameters { ParticleGlow = 0.8, ParticleFlareRatio = 0.5, ParticleFlareLength = 6 };
            var a = IrisPatterns.Grains(plain, new Point(0, 0), 50, 50, 50, plain.Seed, false, 1.0, 1.0);
            var b = IrisPatterns.Grains(glowing, new Point(0, 0), 50, 50, 50, glowing.Seed, false, 1.0, 1.0);
            Assert.Equal(a.Select(g => (g.Position, g.Size, g.IsStar)), b.Select(g => (g.Position, g.Size, g.IsStar)));
            Assert.All(a, g => Assert.False(g.HasFlare));
            int flares = b.Count(g => g.HasFlare);
            Assert.InRange(flares, b.Count / 4, b.Count * 3 / 4);
        }

        [Fact]
        public void TestGlowingGlitterExportsHalosAndFlares()
        {
            var p = new IrisParameters { ParticleGlow = 0.6, ParticleFlareRatio = 1.0 };
            string svg = SvgExporter.GeneratePairSvg(p);
            Assert.True(SvgValidator.IsWellFormed(svg, out var error), error);
            Assert.Contains("<g id=\"left_eye_glitter_glow\">", svg);
            Assert.Contains("<radialGradient id=\"left_eye_halo0\">", svg);
            // A solid glitter color needs one halo gradient only
            Assert.DoesNotContain("left_eye_halo1\"", svg);
            int halos = svg.Split("<g id=\"left_eye_glitter_glow\">")[1].Split("</g>")[0].Split("<circle ").Length - 1;
            Assert.Equal(p.ParticleCount, halos);
            int flares = svg.Split("<g id=\"left_eye_glitter\"")[1].Split("</g>")[0].Split("<path ").Length - 1;
            Assert.Equal(p.ParticleCount, flares);
        }

        [Fact]
        public void TestHalosFollowAGradientGlitterColor()
        {
            var p = new IrisParameters { ParticleGlow = 0.6, ParticleColor = "linear-gradient(0deg, #FF0000 0%, #0000FF 100%)" };
            string svg = SvgExporter.GeneratePairSvg(p);
            Assert.Contains("left_eye_halo1\"", svg); // grains across the iris take different colors
        }

        [Fact]
        public void TestOldFilesGetTheNewLooksAndValuesClamp()
        {
            // Saved before these settings existed: loads with the new looks
            var old = JsonSerializer.Deserialize<IrisParameters>("{\"Name\":\"旧\",\"RayCount\":40}")!;
            Assert.Equal(IrisParameters.DefaultRayTaper, old.RayTaper);
            Assert.Equal(IrisParameters.DefaultRayWidthVariation, old.RayWidthVariation);
            Assert.Equal(IrisParameters.DefaultRayCurve, old.RayCurve);
            Assert.Equal(IrisParameters.DefaultRayFineLayer, old.RayFineLayer);
            Assert.Equal(IrisParameters.DefaultParticleGlow, old.ParticleGlow);
            Assert.Equal(IrisParameters.DefaultParticleFlareRatio, old.ParticleFlareRatio);

            // Turned off and saved: stays off
            var off = JsonSerializer.Deserialize<IrisParameters>(JsonSerializer.Serialize(Plain()))!;
            Assert.False(off.HasFiberRays);
            Assert.Equal(0.0, off.RayFineLayer);
            Assert.Equal(0.0, off.ParticleGlow);

            // Out of range is clamped; NaN / infinity (hand-edited file) falls back to the default
            var p = new IrisParameters { RayCurve = -5, RayTaper = double.NaN, ParticleFlareLength = 99, ParticleGlow = double.PositiveInfinity };
            Assert.Equal(-1.0, p.RayCurve);
            Assert.Equal(IrisParameters.DefaultRayTaper, p.RayTaper);
            Assert.Equal(IrisParameters.MaxParticleFlareLength, p.ParticleFlareLength);
            Assert.Equal(IrisParameters.DefaultParticleGlow, p.ParticleGlow);

            var clone = new IrisParameters { RayTaper = 0.4, ParticleGlow = 0.3, AuroraOpacity = 0.2, AuroraColor = Aurora }.Clone();
            Assert.Equal(0.4, clone.RayTaper);
            Assert.Equal(0.3, clone.ParticleGlow);
            Assert.Equal(0.2, clone.AuroraOpacity);
            Assert.Equal(Aurora, clone.AuroraColor);
        }

        [Fact]
        public void TestNewLooksRenderOnScreenWithoutErrors() => RunOnSta(NewLooksRenderOnScreen);

        private static void NewLooksRenderOnScreen()
        {
            var p = new IrisParameters
            {
                RayTaper = 0.7, RayWidthVariation = 0.5, RayCurve = 0.6, RayFineLayer = 0.5,
                ParticleGlow = 0.7, ParticleFlareRatio = 0.3, ParticleColor = Aurora,
                AuroraColor = Aurora, AuroraOpacity = 0.4, IsOddEye = true
            };
            var left = RasterExporter.RenderSingleToBitmap(p, isRightEye: false, dpi: 200.0, showCutLines: false, showCenterMarks: false);
            var right = RasterExporter.RenderSingleToBitmap(p, isRightEye: true, dpi: 200.0, showCutLines: false, showCenterMarks: false);
            Assert.True(left.PixelWidth > 0 && right.PixelWidth > 0);
        }
    }
}
