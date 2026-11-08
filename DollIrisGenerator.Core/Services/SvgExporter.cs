using System;
using System.Globalization;
using System.IO;
using System.Text;
using DollIrisGenerator.Core.Geometry;
using DollIrisGenerator.Models;

namespace DollIrisGenerator.Services
{
    public static class SvgExporter
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>
        /// Paint (fill / stroke value) for a color setting that may be a gradient.
        /// A solid color returns the sanitized hex exactly as before. A gradient writes a
        /// <c>&lt;linearGradient&gt;</c> / <c>&lt;radialGradient&gt;</c> (userSpaceOnUse, placed by
        /// <see cref="GradientMapping"/> like the screen renderer) into its own <c>&lt;defs&gt;</c> at the current
        /// position and returns <c>url(#id)</c>. <paramref name="box"/> is in the user space of the element
        /// that uses the paint (inside its transform attribute, if any).
        /// </summary>
        internal static string Paint(StringBuilder sb, string indent, string id, string? value, string fallbackHex,
            Rect box, bool mirror = false, double bandInner = 0.0, double bandOuter = 1.0)
        {
            var fill = ColorFill.Parse(value);
            if (!fill.IsGradient) return GeometryHelper.SanitizeColorHex(value, fallbackHex);

            if (mirror) fill = fill.MirroredX();
            var map = GradientMapping.Create(fill, box, bandInner, bandOuter);

            if (map.Kind == ColorFillKind.Conic)
            {
                AppendConicPattern(sb, indent, id, map);
                return $"url(#{id})";
            }

            sb.AppendLine($"{indent}<defs>");
            if (map.Kind == ColorFillKind.Radial)
            {
                // Elliptical radial gradient: unit circle stretched onto the box
                sb.AppendLine($@"{indent}  <radialGradient id=""{id}"" gradientUnits=""userSpaceOnUse"" cx=""0"" cy=""0"" r=""1"" gradientTransform=""translate({F(map.Center.X)} {F(map.Center.Y)}) scale({F(map.RadiusX, 6)} {F(map.RadiusY, 6)})"">");
            }
            else
            {
                sb.AppendLine($@"{indent}  <linearGradient id=""{id}"" gradientUnits=""userSpaceOnUse"" x1=""{F(map.Start.X)}"" y1=""{F(map.Start.Y)}"" x2=""{F(map.End.X)}"" y2=""{F(map.End.Y)}"">");
            }
            foreach (var s in map.Stops)
            {
                var (rgb, alpha) = SvgStopColor(s.Color);
                string opacityAttr = alpha < 1.0 ? $@" stop-opacity=""{alpha.ToString("0.###", Inv)}""" : "";
                sb.AppendLine($@"{indent}    <stop offset=""{(s.Offset * 100.0).ToString("0.###", Inv)}%"" stop-color=""{rgb}""{opacityAttr} />");
            }
            sb.AppendLine(map.Kind == ColorFillKind.Radial ? $"{indent}  </radialGradient>" : $"{indent}  </linearGradient>");
            sb.AppendLine($"{indent}</defs>");
            return $"url(#{id})";
        }

        /// <summary>
        /// A conic (円周) gradient: SVG has none, so it is a pattern holding the same thin wedges the screen renderer
        /// draws (<see cref="GradientMapping.ConicWedges"/>). The single tile covers <see cref="GradientMapping.ConicArea"/>
        /// in user space (its content is placed relative to the tile's corner), and crispEdges keeps seams from
        /// showing between neighbouring wedges, like the aliased wedges on screen.
        /// </summary>
        private static void AppendConicPattern(StringBuilder sb, string indent, string id, GradientMapping map)
        {
            var area = map.ConicArea();
            double reach = Math.Sqrt(area.Width * area.Width + area.Height * area.Height);
            // Tile-relative center
            var c = new Point(map.Center.X - area.X, map.Center.Y - area.Y);

            sb.AppendLine($"{indent}<defs>");
            sb.AppendLine($@"{indent}  <pattern id=""{id}"" patternUnits=""userSpaceOnUse"" x=""{F(area.X)}"" y=""{F(area.Y)}"" width=""{F(area.Width)}"" height=""{F(area.Height)}"">");
            sb.AppendLine($@"{indent}    <g shape-rendering=""crispEdges"">");
            foreach (var (from, to, color) in map.ConicWedges())
            {
                var a = IrisStyle.PolarPoint(c, reach, from);
                var b = IrisStyle.PolarPoint(c, reach, to);
                var (rgb, alpha) = SvgStopColor(color);
                string opacityAttr = alpha < 1.0 ? $@" fill-opacity=""{alpha.ToString("0.###", Inv)}""" : "";
                sb.AppendLine($@"{indent}      <path d=""M{F(c.X)},{F(c.Y)} L{F(a.X)},{F(a.Y)} L{F(b.X)},{F(b.Y)} Z"" fill=""{rgb}""{opacityAttr} />");
            }
            sb.AppendLine($"{indent}    </g>");
            sb.AppendLine($"{indent}  </pattern>");
            sb.AppendLine($"{indent}</defs>");
        }

        /// <summary>
        /// Opens a group of many shapes sharing one paint (close it with <see cref="CloseSharedPaintGroup"/>).
        /// A conic (円周) paint is a pattern, which renderers draw again for every shape filled with it
        /// (hundreds of rays took resvg about 1 ms each at print size). Then the shapes are drawn white into a mask
        /// over one rectangle in that paint instead: the same picture, with the pattern drawn once.
        /// <paramref name="paintAttr"/> is "fill" or "stroke"; <paramref name="attrs"/> are further group attributes.
        /// </summary>
        private static void OpenSharedPaintGroup(StringBuilder sb, string id, string paintAttr, string paint, string? colorValue,
            string attrs, Rect area)
        {
            if (!IsConic(colorValue))
            {
                sb.AppendLine($@"      <g id=""{id}"" {paintAttr}=""{paint}""{attrs}>");
                return;
            }
            var (x, y, w, h) = MaskArea(area);
            sb.AppendLine($@"      <mask id=""{id}_mask"" maskUnits=""userSpaceOnUse"" x=""{F(x)}"" y=""{F(y)}"" width=""{F(w)}"" height=""{F(h)}"">");
            sb.AppendLine($@"      <g {paintAttr}=""#FFFFFF""{attrs}>");
        }

        /// <summary>Closes <see cref="OpenSharedPaintGroup"/>; <paramref name="opacity"/> is the group's opacity.</summary>
        private static void CloseSharedPaintGroup(StringBuilder sb, string id, string paint, string? colorValue, double opacity, Rect area)
        {
            sb.AppendLine("      </g>");
            if (!IsConic(colorValue)) return;
            var (x, y, w, h) = MaskArea(area);
            sb.AppendLine("      </mask>");
            sb.AppendLine($@"      <rect id=""{id}"" x=""{F(x)}"" y=""{F(y)}"" width=""{F(w)}"" height=""{F(h)}"" fill=""{paint}"" mask=""url(#{id}_mask)"" opacity=""{opacity.ToFixed(2)}"" />");
        }

        /// <summary>The group's opacity attribute, or nothing when it goes on the masked rectangle instead.</summary>
        private static string OpacityUnlessMasked(string? colorValue, double opacity) =>
            IsConic(colorValue) ? "" : $@" opacity=""{opacity.ToFixed(2)}""";

        private static bool IsConic(string? colorValue) => ColorFill.Parse(colorValue) is { IsGradient: true, Kind: ColorFillKind.Conic };

        /// <summary>The iris box with a margin for strokes and flares reaching past the iris (clipped to it anyway).</summary>
        private static (double X, double Y, double W, double H) MaskArea(Rect r)
        {
            double m = Math.Max(r.Width, r.Height) * 0.1;
            return (r.X - m, r.Y - m, r.Width + 2 * m, r.Height + 2 * m);
        }

        /// <summary>
        /// SVG stop color for a stop's hex: "#AARRGGBB" (the app's 8-digit form, alpha first) becomes
        /// "#RRGGBB" plus a stop-opacity, since SVG would read 8 digits as RRGGBBAA.
        /// </summary>
        private static (string Rgb, double Alpha) SvgStopColor(string hex)
        {
            string safe = GeometryHelper.SanitizeColorHex(hex, "#FFFFFF");
            if (safe.Length == 9)
            {
                double a = Convert.ToByte(safe.Substring(1, 2), 16) / 255.0;
                return ("#" + safe.Substring(3), a);
            }
            return (safe, 1.0);
        }

        private static string F(double v, int decimals = 3) => v.ToFixed(decimals);

        /// <summary>Default padding around one eye in the exported SVG (mm).</summary>
        public const double SingleEyePaddingMm = 2.0;

        /// <summary>Default padding around the pair in the exported SVG (mm).</summary>
        public const double PairPaddingMm = 3.0;

        public static string GenerateSingleEyeSvg(IrisParameters p, bool isRightEye, bool includePrintGuides = false) =>
            GenerateSingleEyeSvg(p, isRightEye, includePrintGuides, includePrintGuides, SingleEyePaddingMm);

        /// <summary>
        /// One eye on a canvas of its tilted footprint plus <paramref name="paddingMm"/> on each side, the eye centered.
        /// The canvas is the exact size in mm (also used to rasterize: PNG / PDF images, see RasterExporter).
        /// </summary>
        public static string GenerateSingleEyeSvg(IrisParameters p, bool isRightEye, bool cutLine, bool centerMark, double paddingMm)
        {
            double sizeMm = p.SizeMm;
            double widthMm = sizeMm;
            double heightMm = sizeMm;

            if (p.Shape == IrisShape.VerticalOval) heightMm *= p.AspectRatio;
            else if (p.Shape == IrisShape.HorizontalOval) widthMm *= p.AspectRatio;

            // Canvas holds the tilted footprint so a tilted oval is not cut off
            var (footW, footH) = SheetLayout.GetEyeFootprintMm(p);
            double totalWidthMm = footW + paddingMm * 2.0;
            double totalHeightMm = footH + paddingMm * 2.0;

            var sb = new StringBuilder();
            sb.AppendLine($@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""no""?>");
            sb.AppendLine($@"<svg width=""{totalWidthMm.ToFixed(2)}mm"" height=""{totalHeightMm.ToFixed(2)}mm"" viewBox=""0 0 {totalWidthMm.ToFixed(2)} {totalHeightMm.ToFixed(2)}"" xmlns=""http://www.w3.org/2000/svg"">");
            sb.AppendLine($@"  <!-- Generated by Doll Iris Atelier - Parametric Doll Eye -->");

            string eyeId = isRightEye ? "eye_right" : "eye_left";
            double cx = totalWidthMm / 2.0;
            double cy = totalHeightMm / 2.0;
            double rx = widthMm / 2.0;
            double ry = heightMm / 2.0;

            AppendIrisSvg(sb, eyeId, cx, cy, rx, ry, p, isRightEye, cutLine, centerMark);

            sb.AppendLine("</svg>");
            return sb.ToString();
        }

        public static string GeneratePairSvg(IrisParameters p, bool includePrintGuides = false, double pairSpacingMm = 4.0) =>
            GeneratePairSvg(p, includePrintGuides, includePrintGuides, pairSpacingMm, PairPaddingMm);

        /// <summary>
        /// Left and right eye side by side, <paramref name="pairSpacingMm"/> apart (between their footprints), with
        /// <paramref name="paddingMm"/> around the pair.
        /// </summary>
        public static string GeneratePairSvg(IrisParameters p, bool cutLine, bool centerMark, double pairSpacingMm, double paddingMm)
        {
            double sizeMm = p.SizeMm;
            double widthMm = sizeMm;
            double heightMm = sizeMm;

            if (p.Shape == IrisShape.VerticalOval) heightMm *= p.AspectRatio;
            else if (p.Shape == IrisShape.HorizontalOval) widthMm *= p.AspectRatio;

            // Canvas and eye spacing use the tilted footprint so tilted ovals are not cut off or overlapping
            var (footW, footH) = SheetLayout.GetEyeFootprintMm(p);
            double totalWidthMm = (footW * 2.0) + pairSpacingMm + (paddingMm * 2.0);
            double totalHeightMm = footH + (paddingMm * 2.0);

            var sb = new StringBuilder();
            sb.AppendLine($@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""no""?>");
            sb.AppendLine($@"<svg width=""{totalWidthMm.ToFixed(2)}mm"" height=""{totalHeightMm.ToFixed(2)}mm"" viewBox=""0 0 {totalWidthMm.ToFixed(2)} {totalHeightMm.ToFixed(2)}"" xmlns=""http://www.w3.org/2000/svg"">");
            sb.AppendLine($@"  <!-- Generated by Doll Iris Atelier - Symmetrical / Odd-Eye Pair -->");

            double rx = widthMm / 2.0;
            double ry = heightMm / 2.0;
            double cy = totalHeightMm / 2.0;

            // Left eye
            double leftCx = paddingMm + footW / 2.0;
            AppendIrisSvg(sb, "left_eye", leftCx, cy, rx, ry, p, false, cutLine, centerMark);

            // Right eye (Mirrored or Odd-Eye)
            double rightCx = leftCx + footW + pairSpacingMm;
            AppendIrisSvg(sb, "right_eye", rightCx, cy, rx, ry, p, true, cutLine, centerMark);

            sb.AppendLine("</svg>");
            return sb.ToString();
        }

        /// <summary>Number of radial strips the fiber noise is made of.</summary>
        public const int FiberStripCount = 40;

        /// <summary>
        /// 繊維ノイズ: fine fibers over the iris body. SVG has no polar noise, so the iris is covered by
        /// <see cref="FiberStripCount"/> strips from the center outward, each filled with feTurbulence stretched
        /// along the strip (long along the radius, fine across it) and turned to its angle; neighbouring strips
        /// overlap a little so no gaps show at the rim. The noise is gray around 50%, blended with overlay so it both
        /// lightens and darkens the colors under it, and faded out near the pupil and at the rim by a mask.
        /// 細かさ multiplies the frequencies. Each strip has its own seed (from the design's seed, so the odd eye
        /// differs); the right eye is the mirror image.
        /// </summary>
        private static void AppendFiberNoise(StringBuilder sb, string idPrefix, double cx, double cy, double rx, double ry,
            double baseR, IrisParameters p, bool isRightEye, int seed)
        {
            string id = $"{idPrefix}_fibers";
            int n = FiberStripCount;
            // Strips are drawn on a unit-radius circle round (0, 0), then placed and stretched onto the iris ellipse
            double halfWidth = Math.PI / n * 1.15;           // half of each strip's width at the rim, with overlap
            double along = 1.75 * p.FiberNoiseScale;         // noise cycles along a strip (radius 1)
            double across = 30.0 * p.FiberNoiseScale;        // and across it
            double inner = Math.Max(0.0, p.PupilSizeRatio * 0.9);
            double full = Math.Min(0.95, p.PupilSizeRatio + 0.15);

            sb.AppendLine("      <defs>");
            for (int i = 0; i < n; i++)
            {
                int stripSeed = Math.Abs((seed * 31 + i * 7919) % 100000);
                sb.AppendLine($@"        <filter id=""{id}_f{i}"" x=""0"" y=""0"" width=""1"" height=""1"" color-interpolation-filters=""sRGB"">");
                sb.AppendLine($@"          <feTurbulence type=""fractalNoise"" baseFrequency=""{F(along)} {F(across)}"" numOctaves=""3"" seed=""{stripSeed}"" />");
                sb.AppendLine($@"          <feColorMatrix type=""matrix"" values=""2.5 0 0 0 -0.75  2.5 0 0 0 -0.75  2.5 0 0 0 -0.75  0 0 0 0 1"" />");
                sb.AppendLine("        </filter>");
            }
            sb.AppendLine($@"        <radialGradient id=""{id}_fade"" cx=""0"" cy=""0"" r=""1"" gradientUnits=""userSpaceOnUse"">");
            sb.AppendLine($@"          <stop offset=""{F(inner)}"" stop-color=""#FFFFFF"" stop-opacity=""0"" />");
            sb.AppendLine($@"          <stop offset=""{F(full)}"" stop-color=""#FFFFFF"" />");
            sb.AppendLine($@"          <stop offset=""0.9"" stop-color=""#FFFFFF"" stop-opacity=""0.7"" />");
            sb.AppendLine($@"          <stop offset=""1"" stop-color=""#FFFFFF"" stop-opacity=""0"" />");
            sb.AppendLine("        </radialGradient>");
            sb.AppendLine($@"        <mask id=""{id}_mask"" maskUnits=""userSpaceOnUse"" x=""-1.1"" y=""-1.1"" width=""2.2"" height=""2.2"">");
            sb.AppendLine($@"          <circle cx=""0"" cy=""0"" r=""1"" fill=""url(#{id}_fade)"" />");
            sb.AppendLine("        </mask>");
            sb.AppendLine("      </defs>");

            string mirror = isRightEye ? "-" : "";
            sb.AppendLine($@"      <g id=""{id}"" transform=""translate({F(cx)} {F(cy)}) scale({mirror}{F(rx, 6)} {F(ry, 6)})"" style=""mix-blend-mode:overlay"" opacity=""{F(p.FiberNoise * 0.8)}"">");
            sb.AppendLine($@"        <g mask=""url(#{id}_mask)"">");
            for (int i = 0; i < n; i++)
            {
                double angle = 360.0 * i / n;
                sb.AppendLine($@"          <rect x=""0"" y=""{F(-halfWidth, 5)}"" width=""1"" height=""{F(halfWidth * 2.0, 5)}"" filter=""url(#{id}_f{i})"" transform=""rotate({F(angle)})"" />");
            }
            sb.AppendLine("        </g>");
            sb.AppendLine("      </g>");
        }

        /// <summary>Farthest the light moves the base gradient's focal point, as a fraction of the iris box.</summary>
        public const double MaxLightShift = 0.4;

        /// <summary>
        /// 光の入る向き: the base gradient's focal point (where the inner color is brightest) moved from its center
        /// toward the light, by 光の強さ × <see cref="MaxLightShift"/> of the iris box (staying inside the gradient's
        /// 55% circle). The angle is on screen: the iris's tilt is taken back out, since the gradient turns with the
        /// iris. The right eye mirrors it when the highlights are mirrored (the highlights are the light's reflection).
        /// Nothing when the strength is 0, so such designs export exactly as before.
        /// </summary>
        private static string LightFocus(IrisParameters p, bool isRightEye, double tilt)
        {
            if (p.LightStrength < 0.005) return "";
            double angle = p.LightAngle;
            if (isRightEye && p.MirrorHighlightsWithIris) angle = 180.0 - angle;
            double rad = (angle - tilt) * Math.PI / 180.0;
            double shift = p.LightStrength * MaxLightShift;
            double fx = 0.5 + Math.Cos(rad) * shift;
            double fy = 0.42 + Math.Sin(rad) * shift;
            return $@" fx=""{(fx * 100.0).ToString("0.###", Inv)}%"" fy=""{(fy * 100.0).ToString("0.###", Inv)}%""";
        }

        private static void AppendIrisSvg(
            StringBuilder sb,
            string idPrefix,
            double cx,
            double cy,
            double rx,
            double ry,
            IrisParameters p,
            bool isRightEye,
            bool cutLine,
            bool centerMark)
        {
            double baseR = Math.Min(rx, ry);

            // Odd-Eye Mode Overrides for Right Eye (with strict Hex Color Sanitization against XML injection)
            bool useOddEye = isRightEye && p.IsOddEye;
            string effectiveBaseColor = GeometryHelper.SanitizeColorHex(useOddEye ? p.OddEyeBaseColor : p.BaseColor, "#2979FF");
            string effectiveInnerColor = GeometryHelper.SanitizeColorHex(useOddEye ? p.OddEyeInnerColor : p.InnerColor, "#80D8FF");
            string effectiveOuterColor = GeometryHelper.SanitizeColorHex(useOddEye ? p.OddEyeOuterColor : p.OuterColor, "#0D47A1");
            string effectiveBottomColor = GeometryHelper.SanitizeColorHex(useOddEye ? p.OddEyeBottomHighlightColor : p.BottomHighlightColor, "#B3E5FC");
            // Colors that may be gradients go through Paint (sanitized hex, or url(#…) of a generated gradient)
            string effectiveRayColor = useOddEye ? p.OddEyeRayColor : p.RayColor;
            int effectiveSeed = useOddEye ? (p.Seed + p.OddEyeSeedOffset) : p.Seed;

            // Iris-wide gradients are laid across the iris ellipse (same as the screen renderer)
            var irisBox = GradientMapping.EllipseBox(new Point(cx, cy), rx, ry);

            // Tilt rotation angle
            double tilt = p.TiltAngle;
            if (isRightEye) tilt = -tilt;
            string tiltAttr = Math.Abs(tilt) > 0.05 ? $@" transform=""rotate({tilt.ToFixed(1)}, {cx.ToFixed(3)}, {cy.ToFixed(3)})""" : "";

            string clipId = $"{idPrefix}_clip";
            string radGradId = $"{idPrefix}_rad_grad";
            string upperShadowId = $"{idPrefix}_shadow_grad";
            string bottomGlowId = $"{idPrefix}_glow_grad";

            sb.AppendLine("  <defs>");
            // Boundary clip
            sb.AppendLine($@"    <clipPath id=""{clipId}"">");
            sb.AppendLine($@"      <ellipse cx=""{cx.ToFixed(3)}"" cy=""{cy.ToFixed(3)}"" rx=""{rx.ToFixed(3)}"" ry=""{ry.ToFixed(3)}"" />");
            sb.AppendLine("    </clipPath>");

            // Base Radial Gradient
            sb.AppendLine($@"    <radialGradient id=""{radGradId}"" cx=""50%"" cy=""42%"" r=""55%""{LightFocus(p, isRightEye, tilt)}>");
            sb.AppendLine($@"      <stop offset=""0%"" stop-color=""{effectiveInnerColor}"" />");
            sb.AppendLine($@"      <stop offset=""{(p.InnerGradientRadius * 40.0).ToFixed(1)}%"" stop-color=""{effectiveInnerColor}"" />");
            sb.AppendLine($@"      <stop offset=""{(p.InnerGradientRadius * 100.0).ToFixed(1)}%"" stop-color=""{effectiveBaseColor}"" />");
            sb.AppendLine($@"      <stop offset=""{(p.OuterGradientRadius * 100.0).ToFixed(1)}%"" stop-color=""{effectiveOuterColor}"" />");
            sb.AppendLine($@"      <stop offset=""100%"" stop-color=""{effectiveOuterColor}"" />");
            sb.AppendLine("    </radialGradient>");

            // Upper Shadow
            sb.AppendLine($@"    <linearGradient id=""{upperShadowId}"" x1=""0"" y1=""0"" x2=""0"" y2=""1"">");
            sb.AppendLine($@"      <stop offset=""0%"" stop-color=""#000000"" stop-opacity=""0.65"" />");
            sb.AppendLine($@"      <stop offset=""65%"" stop-color=""#000000"" stop-opacity=""0"" />");
            sb.AppendLine("    </linearGradient>");

            // Lower Glow
            sb.AppendLine($@"    <linearGradient id=""{bottomGlowId}"" x1=""0"" y1=""0"" x2=""0"" y2=""1"">");
            sb.AppendLine($@"      <stop offset=""45%"" stop-color=""{effectiveBottomColor}"" stop-opacity=""0"" />");
            sb.AppendLine($@"      <stop offset=""100%"" stop-color=""{effectiveBottomColor}"" stop-opacity=""{p.BottomHighlightIntensity.ToFixed(2)}"" />");
            sb.AppendLine("    </linearGradient>");

            sb.AppendLine("  </defs>");

            // Start Group with tilt if specified
            sb.AppendLine($@"  <g id=""{idPrefix}""{tiltAttr}>");
            sb.AppendLine($@"    <g id=""{idPrefix}_clipped"" clip-path=""url(#{clipId})"">");

            // Base Background
            sb.AppendLine($@"      <ellipse id=""{idPrefix}_base"" cx=""{cx.ToFixed(3)}"" cy=""{cy.ToFixed(3)}"" rx=""{rx.ToFixed(3)}"" ry=""{ry.ToFixed(3)}"" fill=""url(#{radGradId})"" />");
            if (p.AuroraOpacity > 0.01)
            {
                string auroraPaint = Paint(sb, "      ", $"{idPrefix}_aurora_fill", p.AuroraColor, "#FF80AB", irisBox, isRightEye);
                sb.AppendLine($@"      <ellipse id=""{idPrefix}_aurora"" cx=""{cx.ToFixed(3)}"" cy=""{cy.ToFixed(3)}"" rx=""{rx.ToFixed(3)}"" ry=""{ry.ToFixed(3)}"" fill=""{auroraPaint}"" opacity=""{p.AuroraOpacity.ToFixed(2)}"" />");
            }
            if (p.NebulaOpacity > 0.01)
            {
                // The same cloud pixels the screen / PDF renderer draws, embedded as a PNG over the iris box
                string png = NebulaTexture.PngBase64(p, effectiveSeed, isRightEye);
                sb.AppendLine($@"      <image id=""{idPrefix}_nebula"" x=""{F(irisBox.X)}"" y=""{F(irisBox.Y)}"" width=""{F(irisBox.Width)}"" height=""{F(irisBox.Height)}"" preserveAspectRatio=""none"" opacity=""{p.NebulaOpacity.ToFixed(2)}"" href=""data:image/png;base64,{png}"" />");
            }
            if (p.FiberNoise > 0.005)
            {
                AppendFiberNoise(sb, idPrefix, cx, cy, rx, ry, baseR, p, isRightEye, effectiveSeed);
            }
            sb.AppendLine($@"      <ellipse id=""{idPrefix}_upper_shadow"" cx=""{cx.ToFixed(3)}"" cy=""{cy.ToFixed(3)}"" rx=""{rx.ToFixed(3)}"" ry=""{ry.ToFixed(3)}"" fill=""url(#{upperShadowId})"" />");
            if (p.BottomHighlightIntensity > 0.01)
            {
                sb.AppendLine($@"      <ellipse id=""{idPrefix}_bottom_glow"" cx=""{cx.ToFixed(3)}"" cy=""{cy.ToFixed(3)}"" rx=""{rx.ToFixed(3)}"" ry=""{ry.ToFixed(3)}"" fill=""url(#{bottomGlowId})"" />");
            }

            // Radial Rays
            if (p.RayCount > 0 && p.RayIntensity > 0.01)
            {
                double innerR = IrisPatterns.InnerRayRadius(p, baseR);
                double bandInner = innerR / baseR, bandOuter = Math.Min(0.96, p.RayLengthRatio);
                var center = new Point(cx, cy);

                string rayPaint = Paint(sb, "      ", $"{idPrefix}_rays_fill", effectiveRayColor, "#B3E5FC", irisBox, isRightEye, bandInner, bandOuter);
                AppendRays(sb, $"{idPrefix}_rays", IrisPatterns.MainRays(p, baseR, effectiveSeed, isRightEye), rayPaint, effectiveRayColor, irisBox, p.RayIntensity,
                    p, center, rx, ry, baseR, isRightEye);

                if (p.RayFineLayer > 0.01)
                {
                    AppendRays(sb, $"{idPrefix}_fine_rays", IrisPatterns.FineRays(p, baseR, effectiveSeed, isRightEye), rayPaint, effectiveRayColor, irisBox,
                        p.RayIntensity * p.RayFineLayer, p, center, rx, ry, baseR, isRightEye);
                }
            }

            // Lens-Style Pattern (Fresnel Rings)
            if (p.LensPatternEnabled && p.LensRingIntensity > 0.01)
            {
                string lensPaint = Paint(sb, "      ", $"{idPrefix}_lens_fill", p.LensRingColor, "#FFFFFF", irisBox, isRightEye, p.PupilSizeRatio, 0.92);
                double ringStroke = p.LensRingWidth * IrisStyle.MmPerStyleUnit;
                // Odd rings: thinner and finely dotted, dashes 4 : 3 of the line width with square ends
                // (WPF's dash units and default dash cap)
                double dotStroke = ringStroke * 0.8;
                string dotAttr = $@" stroke-width=""{F(dotStroke)}"" stroke-dasharray=""{F(dotStroke * 4.0)},{F(dotStroke * 3.0)}"" stroke-linecap=""square""";
                OpenSharedPaintGroup(sb, $"{idPrefix}_lens_rings", "stroke", lensPaint, p.LensRingColor,
                    $@" stroke-width=""{F(ringStroke)}""{OpacityUnlessMasked(p.LensRingColor, p.LensRingIntensity)} fill=""none""", irisBox);
                double pupilR = baseR * p.PupilSizeRatio;
                double ringSpan = (baseR * 0.92) - pupilR;

                for (int k = 1; k <= p.LensRingCount; k++)
                {
                    double progress = Math.Pow((double)k / p.LensRingCount, 0.82);
                    double ringR = pupilR + ringSpan * progress;
                    double ringRx = ringR * (rx / baseR);
                    double ringRy = ringR * (ry / baseR);

                    string dashAttr = (k % 2 == 1) ? dotAttr : "";
                    sb.AppendLine($@"        <ellipse cx=""{cx.ToFixed(3)}"" cy=""{cy.ToFixed(3)}"" rx=""{ringRx.ToFixed(3)}"" ry=""{ringRy.ToFixed(3)}""{dashAttr} />");
                }
                CloseSharedPaintGroup(sb, $"{idPrefix}_lens_rings", lensPaint, p.LensRingColor, p.LensRingIntensity, irisBox);

                // Center refraction glow (中心屈折光): one color (a gradient's first), fading out to the edge
                if (p.LensRefractionGlow > 0.01)
                {
                    var (ga, gr, gg, gb) = GeometryHelper.ParseHexArgb(p.LensRingColor, p.LensRefractionGlow * 0.45);
                    string glowRgb = $"#{gr:X2}{gg:X2}{gb:X2}";
                    string glowId = $"{idPrefix}_lens_glow_grad";
                    sb.AppendLine("      <defs>");
                    sb.AppendLine($@"        <radialGradient id=""{glowId}"">");
                    sb.AppendLine($@"          <stop offset=""0%"" stop-color=""{glowRgb}"" stop-opacity=""{(ga / 255.0).ToString("0.###", Inv)}"" />");
                    sb.AppendLine($@"          <stop offset=""100%"" stop-color=""{glowRgb}"" stop-opacity=""0"" />");
                    sb.AppendLine("        </radialGradient>");
                    sb.AppendLine("      </defs>");
                    sb.AppendLine($@"      <ellipse id=""{idPrefix}_lens_glow"" cx=""{F(cx)}"" cy=""{F(cy)}"" rx=""{F(rx * 0.55)}"" ry=""{F(ry * 0.55)}"" fill=""url(#{glowId})"" />");
                }
            }

            // Limbal Ring
            if (p.LimbalRingThickness > 0.005 && p.LimbalRingOpacity > 0.01)
            {
                double ringWidth = baseR * p.LimbalRingThickness;
                string limbalPaint = Paint(sb, "      ", $"{idPrefix}_limbal_fill", p.LimbalRingColor, "#051636", irisBox, isRightEye, 1.0 - p.LimbalRingThickness);
                sb.AppendLine($@"      <ellipse id=""{idPrefix}_limbal_ring"" cx=""{cx.ToFixed(3)}"" cy=""{cy.ToFixed(3)}"" rx=""{rx.ToFixed(3)}"" ry=""{ry.ToFixed(3)}"" fill=""none"" stroke=""{limbalPaint}"" stroke-width=""{(ringWidth * 2.0).ToFixed(3)}"" opacity=""{p.LimbalRingOpacity.ToFixed(2)}"" />");

                // Soft inner edge (境界の柔らかさ): a wider, fainter ring in the same paint
                if (p.LimbalRingSoftness > 0.05)
                {
                    sb.AppendLine($@"      <ellipse id=""{idPrefix}_limbal_soft"" cx=""{cx.ToFixed(3)}"" cy=""{cy.ToFixed(3)}"" rx=""{rx.ToFixed(3)}"" ry=""{ry.ToFixed(3)}"" fill=""none"" stroke=""{limbalPaint}"" stroke-width=""{(ringWidth * 3.5 * p.LimbalRingSoftness).ToFixed(3)}"" opacity=""{(p.LimbalRingOpacity * 0.45).ToFixed(3)}"" />");
                }
            }

            // Pupil
            if (p.PupilSizeRatio > 0.01 && p.PupilOpacity > 0.01)
            {
                double prx = baseR * p.PupilSizeRatio;
                double pry = baseR * p.PupilSizeRatio;
                if (p.Shape == IrisShape.VerticalOval) pry *= p.AspectRatio;
                else if (p.Shape == IrisShape.HorizontalOval) prx *= p.AspectRatio;

                // Cat slit / heart / star: same outline as the screen / PDF renderer.
                // A gradient spans the pupil's own bounding box (as on screen).
                var pupilOutline = ShapeOutline.ForPupil(p.PupilShape, prx, pry)?.Rounded(p.PupilCornerRounding);
                Rect pupilBox;
                if (pupilOutline != null) pupilBox = pupilOutline.GetBounds();
                else if (p.PupilShape == PupilShape.VerticalOval) pupilBox = GradientMapping.EllipseBox(new Point(0, 0), prx * 0.75, pry * 1.25);
                else pupilBox = GradientMapping.EllipseBox(new Point(0, 0), prx, pry);
                pupilBox.Offset(cx, cy);

                string pupilPaint = Paint(sb, "      ", $"{idPrefix}_pupil_fill", p.PupilColor, "#060A18", pupilBox, isRightEye);
                sb.AppendLine($@"      <g id=""{idPrefix}_pupil"" fill=""{pupilPaint}"" opacity=""{p.PupilOpacity.ToFixed(2)}"">");
                if (pupilOutline != null)
                {
                    AppendPath(sb, "        <path d=\"", pupilOutline, cx, cy, "\" />");
                }
                else if (p.PupilShape == PupilShape.VerticalOval)
                {
                    sb.AppendLine($@"        <ellipse cx=""{cx.ToFixed(3)}"" cy=""{cy.ToFixed(3)}"" rx=""{(prx * 0.75).ToFixed(3)}"" ry=""{(pry * 1.25).ToFixed(3)}"" />");
                }
                else
                {
                    sb.AppendLine($@"        <ellipse cx=""{cx.ToFixed(3)}"" cy=""{cy.ToFixed(3)}"" rx=""{prx.ToFixed(3)}"" ry=""{pry.ToFixed(3)}"" />");
                }
                sb.AppendLine("      </g>");

                // Gem-cut facets (宝石カット), clipped to the same pupil outline
                if (p.PupilFacetIntensity > 0.01)
                {
                    string facetClip = $"{idPrefix}_pupil_facet_clip";
                    sb.AppendLine("      <defs>");
                    sb.AppendLine($@"        <clipPath id=""{facetClip}"">");
                    if (pupilOutline != null)
                        AppendPath(sb, "          <path d=\"", pupilOutline, cx, cy, "\" />");
                    else if (p.PupilShape == PupilShape.VerticalOval)
                        sb.AppendLine($@"          <ellipse cx=""{F(cx)}"" cy=""{F(cy)}"" rx=""{F(prx * 0.75)}"" ry=""{F(pry * 1.25)}"" />");
                    else
                        sb.AppendLine($@"          <ellipse cx=""{F(cx)}"" cy=""{F(cy)}"" rx=""{F(prx)}"" ry=""{F(pry)}"" />");
                    sb.AppendLine("        </clipPath>");
                    sb.AppendLine("      </defs>");
                    sb.AppendLine($@"      <g id=""{idPrefix}_pupil_facets"" clip-path=""url(#{facetClip})"">");
                    foreach (var (poly, shade) in IrisPatterns.PupilFacets(prx, pry, p.PupilFacetCount, effectiveSeed, isRightEye))
                    {
                        double alpha = p.PupilOpacity * p.PupilFacetIntensity * IrisPatterns.MaxFacetAlpha * Math.Abs(shade);
                        string color = shade >= 0 ? "#FFFFFF" : "#000000";
                        AppendPath(sb, "        <path d=\"", ShapeOutline.Polygon(poly), cx, cy,
                            $@""" fill=""{color}"" fill-opacity=""{alpha.ToString("0.###", Inv)}"" />");
                    }
                    sb.AppendLine("      </g>");
                }

                // Soft edge (輪郭の柔らかさ): the outline stroked wide and faint in the pupil's paint
                if (p.PupilSoftness > 0.02)
                {
                    string soft = $@" fill=""none"" stroke=""{pupilPaint}"" stroke-width=""{F(prx * p.PupilSoftness * 1.5)}"" opacity=""{F(p.PupilOpacity * 0.35)}""";
                    if (pupilOutline != null)
                        AppendPath(sb, $@"      <path id=""{idPrefix}_pupil_soft"" d=""", pupilOutline, cx, cy, $@"""{soft} />");
                    else if (p.PupilShape == PupilShape.VerticalOval)
                        sb.AppendLine($@"      <ellipse id=""{idPrefix}_pupil_soft"" cx=""{F(cx)}"" cy=""{F(cy)}"" rx=""{F(prx * 0.75)}"" ry=""{F(pry * 1.25)}""{soft} />");
                    else
                        sb.AppendLine($@"      <ellipse id=""{idPrefix}_pupil_soft"" cx=""{F(cx)}"" cy=""{F(cy)}"" rx=""{F(prx)}"" ry=""{F(pry)}""{soft} />");
                }
            }

            // Particles / Glitter
            if (p.ParticleCount > 0 && p.ParticleOpacity > 0.01)
            {
                double bandInner = p.PupilSizeRatio * 0.95, bandOuter = p.ParticleRadiusRatio;
                var grains = IrisPatterns.Grains(p, new Point(cx, cy), rx, ry, baseR, effectiveSeed, isRightEye, IrisStyle.MmPerStyleUnit, 0.5);

                // Halos (光のにじみ): one small radial gradient per distinct grain color, opacity baked into its stops
                if (p.ParticleGlow > 0.01)
                {
                    var colorAt = IrisStyle.GrainColorSampler(p.ParticleColor, irisBox, isRightEye, bandInner, bandOuter);
                    var haloIds = new System.Collections.Generic.Dictionary<string, string>();
                    var halos = new StringBuilder();
                    sb.AppendLine("      <defs>");
                    foreach (var g in grains)
                    {
                        string color = colorAt(g.Position);
                        if (!haloIds.TryGetValue(color, out var haloId))
                        {
                            haloId = $"{idPrefix}_halo{haloIds.Count}";
                            haloIds[color] = haloId;
                            var (rgb, alpha) = SvgStopColor(color);
                            sb.AppendLine($@"        <radialGradient id=""{haloId}"">");
                            foreach (var (offset, a) in IrisPatterns.HaloStops(p.ParticleGlow))
                                sb.AppendLine($@"          <stop offset=""{(offset * 100.0).ToString("0.###", Inv)}%"" stop-color=""{rgb}"" stop-opacity=""{(alpha * a * p.ParticleOpacity).ToString("0.###", Inv)}"" />");
                            sb.AppendLine("        </radialGradient>");
                        }
                        double hr = IrisPatterns.HaloRadius(g.Size, p.ParticleGlow);
                        halos.AppendLine($@"        <circle cx=""{F(g.Position.X)}"" cy=""{F(g.Position.Y)}"" r=""{F(hr)}"" fill=""url(#{haloId})"" />");
                    }
                    sb.AppendLine("      </defs>");
                    sb.AppendLine($@"      <g id=""{idPrefix}_glitter_glow"">");
                    sb.Append(halos);
                    sb.AppendLine("      </g>");
                }

                string glitterPaint = Paint(sb, "      ", $"{idPrefix}_glitter_fill", p.ParticleColor, "#FFFFFF", irisBox, isRightEye, bandInner, bandOuter);
                OpenSharedPaintGroup(sb, $"{idPrefix}_glitter", "fill", glitterPaint, p.ParticleColor,
                    OpacityUnlessMasked(p.ParticleColor, p.ParticleOpacity), irisBox);
                foreach (var g in grains)
                {
                    if (g.IsStar)
                    {
                        sb.AppendLine(BuildSvgStar4(g.Position.X, g.Position.Y, g.Size * 1.5, 0.25));
                    }
                    else
                    {
                        sb.AppendLine($@"        <circle cx=""{g.Position.X.ToFixed(3)}"" cy=""{g.Position.Y.ToFixed(3)}"" r=""{(g.Size * 0.5).ToFixed(3)}"" />");
                    }
                }
                // Cross flares (十字フレア)
                if (p.ParticleFlareRatio > 0.001)
                {
                    foreach (var g in grains)
                    {
                        if (!g.HasFlare) continue;
                        AppendPath(sb, "        <path d=\"", IrisPatterns.Flare(g.Size, p.ParticleFlareLength), g.Position.X, g.Position.Y, "\" />");
                    }
                }
                CloseSharedPaintGroup(sb, $"{idPrefix}_glitter", glitterPaint, p.ParticleColor, p.ParticleOpacity, irisBox);
            }

            // Decorative Symbols
            if (p.Symbols != null)
            {
                int symbolIndex = -1;
                foreach (var symbol in p.Symbols)
                {
                    symbolIndex++;
                    if (!symbol.Enabled || symbol.Type == SymbolType.None || symbol.Opacity <= 0.01)
                        continue;

                    double posX = symbol.PositionX;
                    double rot = symbol.Rotation;
                    if (isRightEye)
                    {
                        posX = -posX;
                        rot = -rot;
                    }

                    double symX = cx + posX * rx;
                    double symY = cy + symbol.PositionY * ry;
                    double symRadius = baseR * symbol.Size;
                    // A gradient's first color stands in on the group; each copy then gets its own gradient
                    string safeSymColor = GeometryHelper.SanitizeColorHex(symbol.Color, "#FFFFFF");
                    bool symGradient = ColorFill.Parse(symbol.Color).IsGradient;

                    sb.AppendLine($@"      <g id=""{idPrefix}_symbol_{symbol.Type}"" fill=""{safeSymColor}"" opacity=""{symbol.Opacity.ToFixed(2)}"">");

                    // Same outline as the screen / PDF renderer; rounded once and reused for every copy
                    var symOutline = ShapeOutline.ForSymbol(symbol.Type, symRadius)?.Rounded(symbol.CornerRounding);
                    bool mirror = symbol.Type == SymbolType.Moon && isRightEye;
                    Rect symBounds = symOutline?.GetBounds(mirror) ?? Rect.Empty;

                    for (int c = 0; c < symbol.Count; c++)
                    {
                        double curX = symX;
                        double curY = symY;
                        double curRot = rot;

                        if (symbol.Count > 1)
                        {
                            double offsetAngle = (2.0 * Math.PI * c) / symbol.Count;
                            if (isRightEye) offsetAngle = Math.PI - offsetAngle;
                            curX += symRadius * 1.4 * Math.Cos(offsetAngle);
                            curY += symRadius * 1.4 * Math.Sin(offsetAngle);
                        }

                        string rotAttr = Math.Abs(curRot) > 0.1 ? $@" transform=""rotate({curRot.ToFixed(1)}, {curX.ToFixed(3)}, {curY.ToFixed(3)})""" : "";
                        if (symOutline == null) continue;

                        // The path is written at (curX, curY) and turned around it, so the gradient box moves with it
                        string fillAttr = "";
                        if (symGradient)
                        {
                            var box = symBounds;
                            box.Offset(curX, curY);
                            string paint = Paint(sb, "        ", $"{idPrefix}_symbol{symbolIndex}_{c}_fill", symbol.Color, "#FFFFFF", box, isRightEye);
                            fillAttr = $@" fill=""{paint}""";
                        }
                        // The moon faces the other way in the right eye (mirrored before the rotation, as on screen)
                        AppendPath(sb, "        <path d=\"", symOutline, curX, curY, $"\"{fillAttr}{rotAttr} />", mirror);
                    }
                    sb.AppendLine("      </g>");
                }
            }

            // Close boundary clip group
            sb.AppendLine("    </g>");

            // Highlights (Drawn on top without clipping)
            if (p.Highlights != null)
            {
                sb.AppendLine($@"    <g id=""{idPrefix}_highlights"">");
                int highlightIndex = -1;
                foreach (var h in p.Highlights)
                {
                    highlightIndex++;
                    if (!h.Enabled || h.Opacity <= 0.01 || h.Size <= 0.005)
                        continue;

                    double posX = h.PositionX;
                    double rot = h.Rotation;

                    if (isRightEye && p.MirrorHighlightsWithIris)
                    {
                        posX = -posX;
                        rot = -rot;
                    }

                    double hx = cx + posX * rx;
                    double hy = cy + h.PositionY * ry;
                    double hRadius = baseR * h.Size;
                    // Gradient across the highlight's own box around (hx, hy), turned with it (as on screen)
                    var hBox = IrisStyle.HighlightBox(h, hRadius);
                    hBox.Offset(hx, hy);
                    string safeHColor = Paint(sb, "      ", $"{idPrefix}_highlight{highlightIndex}_fill", h.Color, "#FFFFFF", hBox,
                        isRightEye && p.MirrorHighlightsWithIris);

                    string rotAttr = Math.Abs(rot) > 0.1 ? $@" transform=""rotate({rot.ToFixed(1)}, {hx.ToFixed(3)}, {hy.ToFixed(3)})""" : "";

                    switch (h.Shape)
                    {
                        case HighlightShape.Oval:
                            double hrx = hRadius * (h.AspectRatio > 0 ? h.AspectRatio : 1.0);
                            sb.AppendLine($@"      <ellipse cx=""{hx.ToFixed(3)}"" cy=""{hy.ToFixed(3)}"" rx=""{hrx.ToFixed(3)}"" ry=""{hRadius.ToFixed(3)}"" fill=""{safeHColor}"" opacity=""{h.Opacity.ToFixed(2)}""{rotAttr} />");
                            break;
                        case HighlightShape.Star4:
                            sb.AppendLine(BuildSvgStar4(hx, hy, hRadius, 0.22, rotAttr, safeHColor, h.Opacity));
                            break;
                        case HighlightShape.Rhombus:
                            sb.AppendLine(BuildSvgDiamond(hx, hy, hRadius * 0.65, hRadius, rotAttr, safeHColor, h.Opacity));
                            break;
                        case HighlightShape.Circle:
                        default:
                            sb.AppendLine($@"      <ellipse cx=""{hx.ToFixed(3)}"" cy=""{hy.ToFixed(3)}"" rx=""{hRadius.ToFixed(3)}"" ry=""{hRadius.ToFixed(3)}"" fill=""{safeHColor}"" opacity=""{h.Opacity.ToFixed(2)}""{rotAttr} />");
                            break;
                    }
                }
                sb.AppendLine("    </g>");
            }

            // Print Guides
            // Print guides: dashed cut line on the iris edge, center cross
            if (cutLine || centerMark)
            {
                sb.AppendLine($@"    <g id=""{idPrefix}_guides"">");
                if (cutLine)
                    sb.AppendLine($@"      <ellipse cx=""{cx.ToFixed(3)}"" cy=""{cy.ToFixed(3)}"" rx=""{rx.ToFixed(3)}"" ry=""{ry.ToFixed(3)}"" fill=""none"" stroke=""#999999"" stroke-width=""0.15"" stroke-dasharray=""2,2"" />");
                if (centerMark)
                {
                    double markLen = baseR * 0.15;
                    sb.AppendLine($@"      <line x1=""{(cx - markLen).ToFixed(3)}"" y1=""{cy.ToFixed(3)}"" x2=""{(cx + markLen).ToFixed(3)}"" y2=""{cy.ToFixed(3)}"" stroke=""#777777"" stroke-width=""0.1"" />");
                    sb.AppendLine($@"      <line x1=""{cx.ToFixed(3)}"" y1=""{(cy - markLen).ToFixed(3)}"" x2=""{cx.ToFixed(3)}"" y2=""{(cy + markLen).ToFixed(3)}"" stroke=""#777777"" stroke-width=""0.1"" />");
                }
                sb.AppendLine("    </g>");
            }

            // Close tilt group
            sb.AppendLine("  </g>");
        }

        /// <summary>
        /// Writes a group of rays: plain round-capped lines when no fiber setting is on (as earlier versions),
        /// otherwise each ray's fiber outline (the same outline the screen renderer fills).
        /// </summary>
        private static void AppendRays(StringBuilder sb, string id, System.Collections.Generic.IEnumerable<RaySpec> rays, string paint,
            string? colorValue, Rect area, double opacity, IrisParameters p, Point center, double rx, double ry, double baseR, bool isRightEye)
        {
            double width = p.RayThickness * IrisStyle.MmPerStyleUnit;
            if (!p.HasFiberRays)
            {
                // Without 太さのばらつき all rays of a group share one width (the fine rays a thinner one)
                double groupFactor = 1.0;
                foreach (var r in rays) { groupFactor = r.WidthFactor; break; }
                OpenSharedPaintGroup(sb, id, "stroke", paint, colorValue,
                    $@" stroke-width=""{(width * groupFactor).ToFixed(3)}"" stroke-linecap=""round""" + OpacityUnlessMasked(colorValue, opacity), area);
                foreach (var ray in rays)
                {
                    var a = IrisPatterns.OnIris(center, rx, ry, baseR, ray.InnerR, ray.Angle);
                    var b = IrisPatterns.OnIris(center, rx, ry, baseR, ray.OuterR, ray.Angle);
                    string widthAttr = Math.Abs(ray.WidthFactor - groupFactor) > 1e-9 ? $@" stroke-width=""{(width * ray.WidthFactor).ToFixed(3)}""" : "";
                    sb.AppendLine($@"        <line x1=""{a.X.ToFixed(3)}"" y1=""{a.Y.ToFixed(3)}"" x2=""{b.X.ToFixed(3)}"" y2=""{b.Y.ToFixed(3)}""{widthAttr} />");
                }
                CloseSharedPaintGroup(sb, id, paint, colorValue, opacity, area);
                return;
            }

            OpenSharedPaintGroup(sb, id, "fill", paint, colorValue, OpacityUnlessMasked(colorValue, opacity), area);
            foreach (var ray in rays)
            {
                var outline = IrisPatterns.FiberOutline(center, rx, ry, baseR, ray, width * ray.WidthFactor, p.RayTaper, p.RayCurve, isRightEye);
                AppendPath(sb, "        <path d=\"", outline, 0, 0, "\" />");
            }
            CloseSharedPaintGroup(sb, id, paint, colorValue, opacity, area);
        }

        /// <summary>
        /// One line: <paramref name="before"/>, the outline's path data, <paramref name="after"/> (the same text as
        /// <c>sb.AppendLine(before + outline.ToSvgPathData(…) + after)</c>, without the in-between strings).
        /// </summary>
        private static void AppendPath(StringBuilder sb, string before, ShapeOutline outline, double offsetX, double offsetY,
            string after, bool mirrorX = false)
        {
            sb.Append(before);
            outline.AppendSvgPathData(sb, offsetX, offsetY, mirrorX);
            sb.AppendLine(after);
        }

        private static string BuildSvgStar4(double cx, double cy, double radius, double innerRatio, string transform = "", string? fill = null, double opacity = 1.0)
        {
            var sb = new StringBuilder();
            sb.Append("        <polygon points=\"");
            for (int i = 0; i < 8; i++)
            {
                double angle = i * Math.PI / 4.0 - Math.PI / 2.0;
                double r = (i % 2 == 0) ? radius : radius * innerRatio;
                double x = cx + r * Math.Cos(angle);
                double y = cy + r * Math.Sin(angle);
                sb.Append($"{x.ToFixed(3)},{y.ToFixed(3)} ");
            }
            sb.Append("\"");
            if (fill != null) sb.Append($@" fill=""{fill}"" opacity=""{opacity.ToFixed(2)}""");
            if (!string.IsNullOrEmpty(transform)) sb.Append(transform);
            sb.Append(" />");
            return sb.ToString();
        }

        private static string BuildSvgDiamond(double cx, double cy, double rx, double ry, string transform = "", string? fill = null, double opacity = 1.0)
        {
            var sb = new StringBuilder();
            sb.Append($@"        <polygon points=""{cx.ToFixed(3)},{(cy - ry).ToFixed(3)} {(cx + rx).ToFixed(3)},{cy.ToFixed(3)} {cx.ToFixed(3)},{(cy + ry).ToFixed(3)} {(cx - rx).ToFixed(3)},{cy.ToFixed(3)}""");
            if (fill != null) sb.Append($@" fill=""{fill}"" opacity=""{opacity.ToFixed(2)}""");
            if (!string.IsNullOrEmpty(transform)) sb.Append(transform);
            sb.Append(" />");
            return sb.ToString();
        }
    }
}
