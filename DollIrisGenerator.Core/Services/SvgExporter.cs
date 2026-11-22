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
        /// <summary>
        /// Where <see cref="GeneratePairSvg(IrisParameters, bool, bool, double, double)"/> puts things, in its mm
        /// coordinates (= its viewBox): the canvas size and the two eyes' centers. The browser preview lays its
        /// item markers over the picture with these.
        /// </summary>
        public static (double Width, double Height, double LeftCx, double RightCx, double Cy) PairLayoutMm(
            IrisParameters p, double pairSpacingMm, double paddingMm)
        {
            // Canvas and eye spacing use the tilted footprint so tilted ovals are not cut off or overlapping
            var (footW, footH) = SheetLayout.GetEyeFootprintMm(p);
            double totalWidthMm = (footW * 2.0) + pairSpacingMm + (paddingMm * 2.0);
            double totalHeightMm = footH + (paddingMm * 2.0);
            double leftCx = paddingMm + footW / 2.0;
            double rightCx = leftCx + footW + pairSpacingMm;
            return (totalWidthMm, totalHeightMm, leftCx, rightCx, totalHeightMm / 2.0);
        }

        public static string GeneratePairSvg(IrisParameters p, bool cutLine, bool centerMark, double pairSpacingMm, double paddingMm)
        {
            double sizeMm = p.SizeMm;
            double widthMm = sizeMm;
            double heightMm = sizeMm;

            if (p.Shape == IrisShape.VerticalOval) heightMm *= p.AspectRatio;
            else if (p.Shape == IrisShape.HorizontalOval) widthMm *= p.AspectRatio;

            var (totalWidthMm, totalHeightMm, leftCx, rightCx, cy) = PairLayoutMm(p, pairSpacingMm, paddingMm);

            var sb = new StringBuilder();
            sb.AppendLine($@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""no""?>");
            sb.AppendLine($@"<svg width=""{totalWidthMm.ToFixed(2)}mm"" height=""{totalHeightMm.ToFixed(2)}mm"" viewBox=""0 0 {totalWidthMm.ToFixed(2)} {totalHeightMm.ToFixed(2)}"" xmlns=""http://www.w3.org/2000/svg"">");
            sb.AppendLine($@"  <!-- Generated by Doll Iris Atelier - Symmetrical / Odd-Eye Pair -->");

            double rx = widthMm / 2.0;
            double ry = heightMm / 2.0;

            // Left eye
            AppendIrisSvg(sb, "left_eye", leftCx, cy, rx, ry, p, false, cutLine, centerMark);

            // Right eye (Mirrored or Odd-Eye)
            AppendIrisSvg(sb, "right_eye", rightCx, cy, rx, ry, p, true, cutLine, centerMark);

            sb.AppendLine("</svg>");
            return sb.ToString();
        }

        /// <summary>
        /// The style attribute for a layer's 重ね方 (CSS / SVG mix-blend-mode), or nothing for Normal, so designs that
        /// keep the default export exactly as before.
        /// </summary>
        internal static string BlendStyle(LayerBlend blend) => blend switch
        {
            LayerBlend.Screen => @" style=""mix-blend-mode:screen""",
            LayerBlend.ColorDodge => @" style=""mix-blend-mode:color-dodge""",
            LayerBlend.Overlay => @" style=""mix-blend-mode:overlay""",
            LayerBlend.SoftLight => @" style=""mix-blend-mode:soft-light""",
            _ => ""
        };

        /// <summary>Blur of the pupil's edge at 輪郭の柔らかさ 1, as a fraction of the pupil's radius (max setting 0.3 → 0.15).</summary>
        public const double PupilBlurPerSoftness = 0.5;

        /// <summary>How far inside the outline (in blur deviations) the blurred pupil's sharp core starts.</summary>
        public const double PupilCoreInset = 2.5;

        /// <summary>Softness (one Gaussian deviation) of the limbal ring's inner edge at 境界の柔らかさ 1, as a fraction of the ring's width.</summary>
        public const double LimbalBlurPerSoftness = 0.9;

        /// <summary>Blur of the glitter glow, in mean grain sizes: base + per 光のにじみ.</summary>
        public const double GlowBlurBase = 0.4, GlowBlurPerGlow = 1.6;

        /// <summary>The glow's alpha is multiplied by this (clamped) so the faint blurred edges still read as light.</summary>
        public const double GlowAlphaSlope = 2.5;

        /// <summary>Blur (one deviation) of a highlight's 輪郭のぼかし at 100%, in highlight radii.</summary>
        public const double HighlightSoftnessBlur = 0.35;

        /// <summary>Blur of a highlight's 光のにじみ, in highlight radii: base + per にじみ.</summary>
        public const double HighlightGlowBlurBase = 0.35, HighlightGlowBlurPerGlow = 0.75;

        /// <summary>The highlight glow's alpha is multiplied by this (clamped), so the halo reads as light round the shape.</summary>
        public const double HighlightGlowAlphaSlope = 2.0;

        /// <summary>
        /// Stops (offset 0–1 of the radius, alpha) of an edge blurred by a Gaussian of <paramref name="sigma"/>:
        /// clear inside, rising through 50% at <paramref name="edge"/>, opaque outside (the normal distribution's
        /// cumulative curve at −2.5σ … +2.5σ). Offsets are clamped to 0–1 and kept in order.
        /// </summary>
        internal static (double Offset, double Alpha)[] GaussianEdgeStops(double edge, double sigma)
        {
            double[] ks = { -2.5, -2.0, -1.5, -1.0, -0.5, 0.0, 0.5, 1.0, 1.5, 2.0, 2.5 };
            var stops = new System.Collections.Generic.List<(double, double)>();
            double last = 0.0;
            foreach (double k in ks)
            {
                double offset = Math.Clamp(edge + k * sigma, 0.0, 1.0);
                if (offset < last) offset = last;
                last = offset;
                stops.Add((offset, k <= -2.5 ? 0.0 : k >= 2.5 ? 1.0 : NormalCdf(k)));
            }
            return stops.ToArray();
        }

        /// <summary>Φ(x), the standard normal cumulative distribution (Abramowitz–Stegun 7.1.26, error &lt; 1.5e-7).</summary>
        internal static double NormalCdf(double x)
        {
            double z = Math.Abs(x) / Math.Sqrt(2.0);
            double t = 1.0 / (1.0 + 0.3275911 * z);
            double erf = 1.0 - (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * Math.Exp(-z * z);
            return 0.5 * (1.0 + (x >= 0 ? erf : -erf));
        }

        /// <summary>Longest step (mm) between the nested strokes of a soft limbal ring on a shaped outline.</summary>
        public const double OutlineRingStepMm = 0.02;
        public const int MinOutlineRingSteps = 12, MaxOutlineRingSteps = 64;

        /// <summary>
        /// The nested strokes (half width in mm from the rim, opacity) that make a soft limbal ring's mask on a shaped
        /// outline (<see cref="IrisOutline"/>): a stroke of width 2d centered on the rim covers everything within d of
        /// it, at the same distance all the way round. Drawn white one over another, from the widest, so that at a
        /// distance d inside the rim the mask is Φ((<paramref name="widthMm"/> − d) / σ), the same curve as the
        /// ellipse's radial gradient (<see cref="GaussianEdgeStops"/>) in steps of at most <see cref="OutlineRingStepMm"/>.
        /// </summary>
        internal static (double HalfWidthMm, double Opacity)[] OutlineRingStrokes(double widthMm, double sigmaMm) =>
            NestedFadeSteps(widthMm, sigmaMm, OutlineRingStepMm, MinOutlineRingSteps, MaxOutlineRingSteps);

        /// <summary>Longest step (in the ring's width) between the nested shapes of a soft 瞳孔まわりの輪, and how many.</summary>
        public const double CollaretteFadeStep = 0.04;
        public const int MinCollaretteFadeSteps = 8, MaxCollaretteFadeSteps = 32;

        /// <summary>Gaussian deviation of the 瞳孔まわりの輪's outer edge at 柔らかさ 1, in the ring's width.</summary>
        public const double CollaretteSigmaPerSoftness = 0.4;

        /// <summary>
        /// Nested shapes (reach, opacity), widest first, each covering everything closer than its reach, drawn white one
        /// over another so that at a distance d the result is Φ((<paramref name="width"/> − d) / σ) in steps of at most
        /// <paramref name="maxStep"/> (between <paramref name="minSteps"/> and <paramref name="maxSteps"/> shapes).
        /// </summary>
        internal static (double Reach, double Opacity)[] NestedFadeSteps(double width, double sigma, double maxStep, int minSteps, int maxSteps)
        {
            double lo = Math.Max(0.0, width - 2.5 * sigma), hi = width + 2.5 * sigma;
            int count = Math.Clamp((int)Math.Ceiling((hi - lo) / maxStep), minSteps, maxSteps);
            double step = (hi - lo) / count;
            // Target mask value in each band (its middle), band k between lo + k·step and lo + (k + 1)·step
            var target = new double[count];
            for (int k = 0; k < count; k++)
                target[k] = NormalCdf((width - (lo + (k + 0.5) * step)) / sigma);
            // Band k is covered by the strokes k … count − 1, so 1 − target[k] = Π (1 − opacity[j]) over them
            var strokes = new (double, double)[count];
            for (int k = count - 1; k >= 0; k--)
            {
                double outer = k == count - 1 ? 1.0 : 1.0 - target[k + 1];
                double opacity = outer > 1e-9 ? Math.Clamp(1.0 - (1.0 - target[k]) / outer, 0.0, 1.0) : 0.0;
                strokes[k] = (lo + (k + 1) * step, opacity);
            }
            Array.Reverse(strokes); // widest first
            return strokes;
        }

        /// <summary>
        /// A Gaussian blur filter of <paramref name="stdDeviation"/> (mm) whose region is <paramref name="box"/> grown
        /// by <paramref name="extra"/> plus three deviations (user space: a stroke is outside an element's bounding box).
        /// <paramref name="alphaSlope"/> &gt; 1 strengthens the blurred alpha.
        /// </summary>
        private static void AppendBlurFilter(StringBuilder sb, string id, double stdDeviation, Rect box, double extra, double alphaSlope = 1.0)
        {
            double m = extra + stdDeviation * 3.0;
            sb.AppendLine("      <defs>");
            sb.AppendLine($@"        <filter id=""{id}"" filterUnits=""userSpaceOnUse"" x=""{F(box.X - m)}"" y=""{F(box.Y - m)}"" width=""{F(box.Width + 2 * m)}"" height=""{F(box.Height + 2 * m)}"">");
            sb.AppendLine($@"          <feGaussianBlur stdDeviation=""{F(stdDeviation, 4)}"" />");
            if (alphaSlope > 1.0)
            {
                sb.AppendLine("          <feComponentTransfer>");
                sb.AppendLine($@"            <feFuncA type=""linear"" slope=""{F(alphaSlope, 2)}"" />");
                sb.AppendLine("          </feComponentTransfer>");
            }
            sb.AppendLine("        </filter>");
            sb.AppendLine("      </defs>");
        }

        /// <summary>
        /// 瞳孔まわりの輪: a jagged ring of color from the pupil's edge out (<see cref="IrisPatterns.CollaretteEdge"/>),
        /// stretched onto the iris. Crisp: the edge filled with the ring's paint. Soft: the ring's paint over the iris,
        /// shown through a mask of the edge shrunk / grown toward the pupil's edge (<see cref="NestedFadeSteps"/>).
        /// </summary>
        private static void AppendCollarette(StringBuilder sb, string idPrefix, double cx, double cy, double rx, double ry,
            IrisParameters p, bool isRightEye, int seed, Rect irisBox)
        {
            double pupil = p.PupilSizeRatio;
            double width = Math.Min(p.CollaretteWidth, 0.95 - pupil);
            if (width < 0.01) return;

            var edge = IrisPatterns.CollaretteEdge(pupil, width, p.CollaretteJagged, p.CollaretteSpikeCount, seed, isRightEye);
            string paint = Paint(sb, "      ", $"{idPrefix}_collarette_fill", p.CollaretteColor, IrisParameters.DefaultCollaretteColor,
                irisBox, isRightEye, pupil, pupil + width);
            string common = $@" opacity=""{p.CollaretteIntensity.ToFixed(2)}""{BlendStyle(p.CollaretteBlend)}";

            // The edge at u (0 = the pupil's edge, 1 = the ring's edge)
            void AppendEdge(double u)
            {
                sb.Append(@" d=""M");
                for (int k = 0; k < edge.Length; k++)
                {
                    var (angle, r) = edge[k];
                    double radius = pupil + u * (r - pupil);
                    if (k > 0) sb.Append(" L");
                    sb.AppendFixed(cx + Math.Cos(angle) * radius * rx, 3).Append(',').AppendFixed(cy + Math.Sin(angle) * radius * ry, 3);
                }
                sb.Append(@" Z""");
            }

            if (p.CollaretteSoftness < 0.01)
            {
                sb.Append($@"      <path id=""{idPrefix}_collarette""");
                AppendEdge(1.0);
                sb.AppendLine($@" fill=""{paint}""{common} />");
                return;
            }

            string maskId = $"{idPrefix}_collarette_mask";
            double sigma = p.CollaretteSoftness * CollaretteSigmaPerSoftness;
            sb.AppendLine("      <defs>");
            sb.AppendLine($@"        <mask id=""{maskId}"">");
            foreach (var (reach, opacity) in NestedFadeSteps(1.0, sigma, CollaretteFadeStep, MinCollaretteFadeSteps, MaxCollaretteFadeSteps))
            {
                if (opacity < 1e-4) continue;
                sb.Append("          <path");
                AppendEdge(reach);
                sb.AppendLine($@" fill=""#FFFFFF"" fill-opacity=""{F(opacity, 4)}"" />");
            }
            sb.AppendLine("        </mask>");
            sb.AppendLine("      </defs>");
            sb.AppendLine($@"      <ellipse id=""{idPrefix}_collarette"" cx=""{cx.ToFixed(3)}"" cy=""{cy.ToFixed(3)}"" rx=""{rx.ToFixed(3)}"" ry=""{ry.ToFixed(3)}"" fill=""{paint}""{common} mask=""url(#{maskId})"" />");
        }

        /// <summary>
        /// 色の段: the stops (offset 0–1 of the iris radius, "#RRGGBB", alpha) of the bands' radial gradient. N bands
        /// split the pupil's edge … rim evenly; band k's color is the inner color mixed toward the outer one by k / (N − 1).
        /// Each boundary gets the colors on either side at ± 境目のぼかし × the band's width / 2 (the same offset twice
        /// when crisp). Past the first and last stops the end colors carry on (inside the pupil, the shaped outline's corners).
        /// </summary>
        internal static List<(double Offset, string Rgb, double Alpha)> ToneBandStops(IrisParameters p)
        {
            int n = p.ToneBandCount;
            double pupil = Math.Clamp(p.PupilSizeRatio, 0.0, 0.95);
            double width = (1.0 - pupil) / n;
            double half = p.ToneBandSoftness * width / 2.0;
            var inner = GeometryHelper.ParseHexArgb(GeometryHelper.SanitizeColorHex(p.ToneBandInnerColor, IrisParameters.DefaultToneBandInnerColor));
            var outer = GeometryHelper.ParseHexArgb(GeometryHelper.SanitizeColorHex(p.ToneBandOuterColor, IrisParameters.DefaultToneBandOuterColor));

            (string, double) Band(int k)
            {
                double t = (double)k / (n - 1);
                int Mix(byte a, byte b) => (int)Math.Round(a + (b - a) * t);
                return ($"#{Mix(inner.R, outer.R):X2}{Mix(inner.G, outer.G):X2}{Mix(inner.B, outer.B):X2}", Mix(inner.A, outer.A) / 255.0);
            }

            var stops = new List<(double, string, double)>();
            var (firstRgb, firstAlpha) = Band(0);
            stops.Add((0.0, firstRgb, firstAlpha));
            for (int k = 1; k < n; k++)
            {
                double edge = pupil + width * k;
                var (beforeRgb, beforeAlpha) = Band(k - 1);
                var (afterRgb, afterAlpha) = Band(k);
                stops.Add((edge - half, beforeRgb, beforeAlpha));
                stops.Add((edge + half, afterRgb, afterAlpha));
            }
            var (lastRgb, lastAlpha) = Band(n - 1);
            stops.Add((1.0, lastRgb, lastAlpha));
            return stops;
        }

        /// <summary>Gaussian deviation (iris radii) of the 二色分け boundary at 境目のぼかし 1.</summary>
        public const double TwoToneSigmaPerSoftness = 0.25;

        /// <summary>Reach the soft 二色分け boundary's nested shapes are measured from (past 2.5 deviations at full softness).</summary>
        private const double TwoToneFadeOrigin = 1.0;

        /// <summary>
        /// 二色分け (<see cref="IrisPatterns.TwoToneRegion"/>): crisp, the region filled with the paint; soft, the paint over
        /// the iris body through a mask of the region with its boundary shifted along u (<see cref="NestedFadeSteps"/>),
        /// so the color rises as Φ(t / σ) across the boundary. The right eye's side is mirrored (180° − 向き).
        /// </summary>
        private static void AppendTwoTone(StringBuilder sb, string idPrefix, double cx, double cy, double rx, double ry,
            IrisParameters p, bool isRightEye, Rect irisBox, Func<string, string> irisBody)
        {
            double angle = isRightEye ? 180.0 - p.TwoToneAngle : p.TwoToneAngle;
            string paint = Paint(sb, "      ", $"{idPrefix}_two_tone_fill", p.TwoToneColor, IrisParameters.DefaultTwoToneColor, irisBox, isRightEye);
            string common = $@" opacity=""{p.TwoToneIntensity.ToFixed(2)}""{BlendStyle(p.TwoToneBlend)}";

            void AppendRegion(double shift)
            {
                var region = IrisPatterns.TwoToneRegion(p.TwoToneHeight, p.TwoToneCurve, angle, shift);
                sb.Append(@" d=""M");
                for (int i = 0; i < region.Length; i++)
                {
                    if (i > 0) sb.Append(" L");
                    sb.AppendFixed(cx + region[i].X * rx, 3).Append(',').AppendFixed(cy + region[i].Y * ry, 3);
                }
                sb.Append(@" Z""");
            }

            if (p.TwoToneSoftness < 0.01)
            {
                sb.Append($@"      <path id=""{idPrefix}_two_tone""");
                AppendRegion(0.0);
                sb.AppendLine($@" fill=""{paint}""{common} />");
                return;
            }

            // A shape that reaches r covers everything with (origin − t) < r, i.e. t > origin − r (t: past the boundary)
            string maskId = $"{idPrefix}_two_tone_mask";
            double sigma = p.TwoToneSoftness * TwoToneSigmaPerSoftness;
            sb.AppendLine("      <defs>");
            sb.AppendLine($@"        <mask id=""{maskId}"">");
            foreach (var (reach, opacity) in NestedFadeSteps(TwoToneFadeOrigin, sigma, CollaretteFadeStep / 4.0, MinCollaretteFadeSteps, MaxCollaretteFadeSteps))
            {
                if (opacity < 1e-4) continue;
                sb.Append("          <path");
                AppendRegion(TwoToneFadeOrigin - reach);
                sb.AppendLine($@" fill=""#FFFFFF"" fill-opacity=""{F(opacity, 4)}"" />");
            }
            sb.AppendLine("        </mask>");
            sb.AppendLine("      </defs>");
            sb.AppendLine($@"      {irisBody($"{idPrefix}_two_tone")} fill=""{paint}""{common} mask=""url(#{maskId})"" />");
        }

        /// <summary>How far past the rim (iris radii) a 外周からの線 carries on at full width, so shaped outlines' corners are covered.</summary>
        public const double RimRayReach = 1.5;

        /// <summary>
        /// 外周からの線 (<see cref="IrisPatterns.RimRays"/>): each line a triangle from its inner tip to the rim (線幅 wide
        /// there), then a strip of that width out to <see cref="RimRayReach"/>, along its direction on the stretched iris.
        /// </summary>
        private static void AppendRimRays(StringBuilder sb, string idPrefix, double cx, double cy, double rx, double ry,
            IrisParameters p, bool isRightEye, int seed)
        {
            string color = GeometryHelper.SanitizeColorHex(p.RimRayColor, IrisParameters.DefaultRimRayColor);
            double half = p.RimRayWidth * IrisStyle.MmPerStyleUnit / 2.0;
            sb.AppendLine($@"      <g id=""{idPrefix}_rim_rays"" fill=""{color}"">");
            foreach (var ray in IrisPatterns.RimRays(p.RimRayCount, p.RimRayRandomness, seed, isRightEye))
            {
                double ux = Math.Cos(ray.Angle) * rx, uy = Math.Sin(ray.Angle) * ry; // radius 1 on the stretched iris
                double len = Math.Sqrt(ux * ux + uy * uy);
                double nx = -uy / len * half, ny = ux / len * half;                 // half the width, across the line
                double tip = 1.0 - p.RimRayLength * ray.LengthFactor;
                double alpha = p.RimRayIntensity * ray.AlphaFactor;
                sb.Append(@"        <path d=""M");
                sb.AppendFixed(cx + ux * tip, 3).Append(',').AppendFixed(cy + uy * tip, 3);
                foreach (var (r, side) in new[] { (1.0, 1.0), (RimRayReach, 1.0), (RimRayReach, -1.0), (1.0, -1.0) })
                {
                    sb.Append(" L");
                    sb.AppendFixed(cx + ux * r + nx * side, 3).Append(',').AppendFixed(cy + uy * r + ny * side, 3);
                }
                sb.AppendLine($@" Z"" fill-opacity=""{alpha.ToString("0.###", Inv)}"" />");
            }
            sb.AppendLine("      </g>");
        }

        /// <summary>Length of a 斑点 at 細長さ 1, in widths (1 + this × 細長さ).</summary>
        public const double SpotStretchPerElongation = 2.5;

        /// <summary>
        /// 斑点: dark ellipses (<see cref="IrisPatterns.Spots"/>) in one group carrying the blend mode. Soft spots share a
        /// bounding-box radial gradient: the color up to 1 − 柔らかさ, clear at the edge.
        /// </summary>
        private static void AppendSpots(StringBuilder sb, string idPrefix, double cx, double cy, double rx, double ry, double baseR,
            IrisParameters p, bool isRightEye, int seed)
        {
            var (rgb, colorAlpha) = SvgStopColor(GeometryHelper.SanitizeColorHex(p.SpotColor, IrisParameters.DefaultSpotColor));
            string fill = rgb;
            if (p.SpotSoftness > 0.005)
            {
                string gradId = $"{idPrefix}_spots_grad";
                string opacityAttr = colorAlpha < 1.0 ? $@" stop-opacity=""{colorAlpha.ToString("0.###", Inv)}""" : "";
                sb.AppendLine("      <defs>");
                sb.AppendLine($@"        <radialGradient id=""{gradId}"">");
                sb.AppendLine($@"          <stop offset=""0%"" stop-color=""{rgb}""{opacityAttr} />");
                sb.AppendLine($@"          <stop offset=""{((1.0 - p.SpotSoftness) * 100.0).ToString("0.###", Inv)}%"" stop-color=""{rgb}""{opacityAttr} />");
                sb.AppendLine($@"          <stop offset=""100%"" stop-color=""{rgb}"" stop-opacity=""0"" />");
                sb.AppendLine("        </radialGradient>");
                sb.AppendLine("      </defs>");
                fill = $"url(#{gradId})";
                colorAlpha = 1.0; // carried by the stops
            }

            double width = p.SpotSize * baseR;
            double length = width * (1.0 + SpotStretchPerElongation * p.SpotElongation);
            sb.AppendLine($@"      <g id=""{idPrefix}_spots"" fill=""{fill}""{BlendStyle(p.SpotBlend)}>");
            foreach (var spot in IrisPatterns.Spots(p.SpotCount, p.PupilSizeRatio, p.SpotRadiusRatio, seed, isRightEye))
            {
                double x = cx + spot.Position.X * rx, y = cy + spot.Position.Y * ry;
                // The long (vertical) axis turned to the spot's direction, as seen on the stretched iris
                double deg = Math.Atan2(Math.Sin(spot.Angle) * ry, Math.Cos(spot.Angle) * rx) * 180.0 / Math.PI - 90.0;
                double alpha = p.SpotIntensity * spot.AlphaFactor * colorAlpha;
                sb.AppendLine($@"        <ellipse cx=""{F(x)}"" cy=""{F(y)}"" rx=""{F(width * spot.SizeFactor, 4)}"" ry=""{F(length * spot.SizeFactor, 4)}"" fill-opacity=""{alpha.ToString("0.###", Inv)}"" transform=""rotate({deg.ToFixed(1)}, {F(x)}, {F(y)})"" />");
            }
            sb.AppendLine("      </g>");
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
        /// iris. The right eye mirrors it unless the light is set to 反転しない (<see cref="IrisParameters.LightRightEyeFlip"/>).
        /// Nothing when the strength is 0, so such designs export exactly as before.
        /// </summary>
        private static string LightFocus(IrisParameters p, bool isRightEye, double tilt)
        {
            if (p.LightStrength < 0.005) return "";
            double angle = p.LightAngle;
            if (RightEyeFlips.MirrorsShape(p.LightRightEyeFlip, isRightEye)) angle = 180.0 - angle;
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

            // A shaped outline (虹彩の輪郭) is written once and used by the clip, the limbal ring and the cut line; the
            // plain ellipse keeps its <ellipse> elements (unchanged output)
            string? outlineId = IrisOutline.IsEllipse(p) ? null : $"{idPrefix}_outline";
            // The start of an element covering the whole iris: the ellipse, or the outline (which reaches into the
            // box's corners). Both have the iris box as bounding box, so bounding-box gradients come out the same.
            string IrisBody(string id) => outlineId != null
                ? $@"<use id=""{id}"" href=""#{outlineId}"""
                : $@"<ellipse id=""{id}"" cx=""{cx.ToFixed(3)}"" cy=""{cy.ToFixed(3)}"" rx=""{rx.ToFixed(3)}"" ry=""{ry.ToFixed(3)}""";

            sb.AppendLine("  <defs>");
            if (outlineId != null)
            {
                sb.Append($@"    <path id=""{outlineId}"" d=""");
                IrisOutline.Build(p, rx, ry).AppendSvgPathData(sb, cx, cy);
                sb.AppendLine(@""" />");
            }
            // Boundary clip
            sb.AppendLine($@"    <clipPath id=""{clipId}"">");
            if (outlineId != null)
                sb.AppendLine($@"      <use href=""#{outlineId}"" />");
            else
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

            // Upper Shadow (まぶたの影): the shadow color at 強さ on the top edge, clear at 深さ. The defaults write what
            // the fixed shadow always did
            bool upperShadow = p.UpperShadowIntensity > 0.005;
            if (upperShadow)
            {
                var (shadowRgb, shadowAlpha) = SvgStopColor(GeometryHelper.SanitizeColorHex(p.UpperShadowColor, IrisParameters.DefaultUpperShadowColor));
                sb.AppendLine($@"    <linearGradient id=""{upperShadowId}"" x1=""0"" y1=""0"" x2=""0"" y2=""1"">");
                sb.AppendLine($@"      <stop offset=""0%"" stop-color=""{shadowRgb}"" stop-opacity=""{(p.UpperShadowIntensity * shadowAlpha).ToString("0.###", Inv)}"" />");
                sb.AppendLine($@"      <stop offset=""{(p.UpperShadowDepth * 100.0).ToString("0.###", Inv)}%"" stop-color=""{shadowRgb}"" stop-opacity=""0"" />");
                sb.AppendLine("    </linearGradient>");
            }

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
            sb.AppendLine($@"      {IrisBody($"{idPrefix}_base")} fill=""url(#{radGradId})"" />");
            if (p.AuroraOpacity > 0.01)
            {
                string auroraPaint = Paint(sb, "      ", $"{idPrefix}_aurora_fill", p.AuroraColor, "#FF80AB", irisBox, isRightEye);
                sb.AppendLine($@"      {IrisBody($"{idPrefix}_aurora")} fill=""{auroraPaint}"" opacity=""{p.AuroraOpacity.ToFixed(2)}""{BlendStyle(p.AuroraBlend)} />");
            }
            if (p.ToneBandIntensity > 0.01)
            {
                // 色の段: one radial gradient over the iris ellipse, with a step of color at each band boundary
                string gradId = $"{idPrefix}_tone_bands_grad";
                sb.AppendLine("      <defs>");
                sb.AppendLine($@"        <radialGradient id=""{gradId}"" gradientUnits=""userSpaceOnUse"" cx=""0"" cy=""0"" r=""1"" gradientTransform=""translate({F(cx)} {F(cy)}) scale({F(rx, 6)} {F(ry, 6)})"">");
                foreach (var (offset, rgb, alpha) in ToneBandStops(p))
                {
                    string opacityAttr = alpha < 1.0 ? $@" stop-opacity=""{alpha.ToString("0.###", Inv)}""" : "";
                    sb.AppendLine($@"          <stop offset=""{(offset * 100.0).ToString("0.###", Inv)}%"" stop-color=""{rgb}""{opacityAttr} />");
                }
                sb.AppendLine("        </radialGradient>");
                sb.AppendLine("      </defs>");
                sb.AppendLine($@"      {IrisBody($"{idPrefix}_tone_bands")} fill=""url(#{gradId})"" opacity=""{p.ToneBandIntensity.ToFixed(2)}""{BlendStyle(p.ToneBandBlend)} />");
            }
            if (p.TwoToneIntensity > 0.01)
            {
                AppendTwoTone(sb, idPrefix, cx, cy, rx, ry, p, isRightEye, irisBox, IrisBody);
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
            if (p.SpotIntensity > 0.01)
            {
                AppendSpots(sb, idPrefix, cx, cy, rx, ry, baseR, p, isRightEye, effectiveSeed);
            }
            if (p.CollaretteIntensity > 0.01)
            {
                AppendCollarette(sb, idPrefix, cx, cy, rx, ry, p, isRightEye, effectiveSeed, irisBox);
            }
            if (upperShadow)
            {
                sb.AppendLine($@"      {IrisBody($"{idPrefix}_upper_shadow")} fill=""url(#{upperShadowId})"" />");
            }
            if (p.BottomHighlightIntensity > 0.01)
            {
                sb.AppendLine($@"      {IrisBody($"{idPrefix}_bottom_glow")} fill=""url(#{bottomGlowId})"" />");
            }

            // 外周からの線: tapered lines from the rim inward, under the rays (and the limbal ring drawn later)
            if (p.RimRayIntensity > 0.01)
            {
                AppendRimRays(sb, idPrefix, cx, cy, rx, ry, p, isRightEye, effectiveSeed);
            }

            // Radial Rays
            if (p.RayCount > 0 && p.RayIntensity > 0.01)
            {
                double innerR = IrisPatterns.InnerRayRadius(p, baseR);
                double bandInner = innerR / baseR, bandOuter = Math.Min(0.96, p.RayLengthRatio);
                var center = new Point(cx, cy);

                // 見える範囲: the main and fine rays together, inside one masked group
                string? rayRange = AppendRangeMask(sb, $"{idPrefix}_rays_range", p, PatternLayer.Rays, isRightEye, cx, cy, rx, ry, baseR, irisBox);
                if (rayRange != null) sb.AppendLine($@"      <g id=""{idPrefix}_rays_ranged"" mask=""url(#{rayRange})"">");

                string rayPaint = Paint(sb, "      ", $"{idPrefix}_rays_fill", effectiveRayColor, "#B3E5FC", irisBox, isRightEye, bandInner, bandOuter);
                AppendRays(sb, $"{idPrefix}_rays", IrisPatterns.MainRays(p, baseR, effectiveSeed, isRightEye), rayPaint, effectiveRayColor, irisBox, p.RayIntensity,
                    p, center, rx, ry, baseR, isRightEye);

                if (p.RayFineLayer > 0.01)
                {
                    AppendRays(sb, $"{idPrefix}_fine_rays", IrisPatterns.FineRays(p, baseR, effectiveSeed, isRightEye), rayPaint, effectiveRayColor, irisBox,
                        p.RayIntensity * p.RayFineLayer, p, center, rx, ry, baseR, isRightEye);
                }
                if (rayRange != null) sb.AppendLine("      </g>");
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
                string? lensRange = AppendRangeMask(sb, $"{idPrefix}_lens_range", p, PatternLayer.LensRings, isRightEye, cx, cy, rx, ry, baseR, irisBox);
                if (lensRange != null) sb.AppendLine($@"      <g id=""{idPrefix}_lens_ranged"" mask=""url(#{lensRange})"">");
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
                if (lensRange != null) sb.AppendLine("      </g>");

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

            // 宝石カット面 (gem facets over the whole iris): white / black polygons in one group, which carries the blend
            // mode and the 見える範囲 mask (as the glitter does). Above the patterns, under the limbal ring and pupil.
            if (p.IrisFacetIntensity > 0.01)
            {
                string? facetRange = AppendRangeMask(sb, $"{idPrefix}_iris_facets_range", p, PatternLayer.IrisFacets, isRightEye, cx, cy, rx, ry, baseR, irisBox);
                string facetMask = facetRange != null ? $@" mask=""url(#{facetRange})""" : "";
                sb.AppendLine($@"      <g id=""{idPrefix}_iris_facets""{BlendStyle(p.IrisFacetBlend)}{facetMask}>");
                foreach (var (poly, shade) in IrisPatterns.IrisFacets(p.IrisFacetCount, p.IrisFacetTableSize, p.IrisFacetShadeAngle,
                             p.IrisFacetShadeBias, effectiveSeed, isRightEye))
                {
                    double alpha = p.IrisFacetIntensity * IrisPatterns.MaxIrisFacetAlpha * Math.Abs(shade);
                    if (alpha < 0.002) continue;
                    sb.Append(@"        <path d=""M");
                    for (int j = 0; j < poly.Length; j++)
                    {
                        if (j > 0) sb.Append(" L");
                        sb.AppendFixed(cx + poly[j].X * rx, 3).Append(',').AppendFixed(cy + poly[j].Y * ry, 3);
                    }
                    sb.AppendLine($@" Z"" fill=""{(shade >= 0 ? "#FFFFFF" : "#000000")}"" fill-opacity=""{alpha.ToString("0.###", Inv)}"" />");
                }
                sb.AppendLine("      </g>");
            }

            // Limbal Ring
            if (p.LimbalRingThickness > 0.005 && p.LimbalRingOpacity > 0.01)
            {
                double ringWidth = baseR * p.LimbalRingThickness;
                string limbalPaint = Paint(sb, "      ", $"{idPrefix}_limbal_fill", p.LimbalRingColor, "#051636", irisBox, isRightEye, 1.0 - p.LimbalRingThickness);
                // Soft inner edge (境界の柔らかさ): the ring's paint over the whole iris, shown through a mask that rises
                // from clear to opaque across the inner edge along a Gaussian curve (the shape a blur gives, but exact:
                // a large blur in resvg leaves faint steps). The rim stays fully covered; any ring color works.
                if (outlineId != null)
                {
                    // Shaped outline: the ring follows it at the same width all the way round (strokes of the outline,
                    // inner half shown by the clip)
                    if (p.LimbalRingSoftness > 0.05)
                    {
                        string maskId = $"{idPrefix}_limbal_mask";
                        double sigmaMm = ringWidth * p.LimbalRingSoftness * LimbalBlurPerSoftness;
                        sb.AppendLine("      <defs>");
                        sb.AppendLine($@"        <mask id=""{maskId}"">");
                        foreach (var (half, opacity) in OutlineRingStrokes(ringWidth, sigmaMm))
                            sb.AppendLine($@"          <use href=""#{outlineId}"" fill=""none"" stroke=""#FFFFFF"" stroke-width=""{F(half * 2.0, 4)}"" stroke-opacity=""{F(opacity, 4)}"" />");
                        sb.AppendLine("        </mask>");
                        sb.AppendLine("      </defs>");
                        sb.AppendLine($@"      <use id=""{idPrefix}_limbal_ring"" href=""#{outlineId}"" fill=""{limbalPaint}"" opacity=""{p.LimbalRingOpacity.ToFixed(2)}"" mask=""url(#{maskId})"" />");
                    }
                    else
                    {
                        sb.AppendLine($@"      <use id=""{idPrefix}_limbal_ring"" href=""#{outlineId}"" fill=""none"" stroke=""{limbalPaint}"" stroke-width=""{(ringWidth * 2.0).ToFixed(3)}"" opacity=""{p.LimbalRingOpacity.ToFixed(2)}"" />");
                    }
                }
                else if (p.LimbalRingSoftness > 0.05)
                {
                    string maskId = $"{idPrefix}_limbal_mask";
                    double edge = 1.0 - p.LimbalRingThickness;                                 // inner edge, radius 1 = rim
                    double sigma = p.LimbalRingThickness * p.LimbalRingSoftness * LimbalBlurPerSoftness;
                    sb.AppendLine("      <defs>");
                    sb.AppendLine($@"        <radialGradient id=""{maskId}_grad"">");
                    foreach (var (offset, alpha) in GaussianEdgeStops(edge, sigma))
                        sb.AppendLine($@"          <stop offset=""{F(offset, 4)}"" stop-color=""#FFFFFF"" stop-opacity=""{F(alpha, 4)}"" />");
                    sb.AppendLine("        </radialGradient>");
                    sb.AppendLine($@"        <mask id=""{maskId}"">");
                    sb.AppendLine($@"          <ellipse cx=""{cx.ToFixed(3)}"" cy=""{cy.ToFixed(3)}"" rx=""{rx.ToFixed(3)}"" ry=""{ry.ToFixed(3)}"" fill=""url(#{maskId}_grad)"" />");
                    sb.AppendLine("        </mask>");
                    sb.AppendLine("      </defs>");
                    sb.AppendLine($@"      <ellipse id=""{idPrefix}_limbal_ring"" cx=""{cx.ToFixed(3)}"" cy=""{cy.ToFixed(3)}"" rx=""{rx.ToFixed(3)}"" ry=""{ry.ToFixed(3)}"" fill=""{limbalPaint}"" opacity=""{p.LimbalRingOpacity.ToFixed(2)}"" mask=""url(#{maskId})"" />");
                }
                else
                {
                    sb.AppendLine($@"      <ellipse id=""{idPrefix}_limbal_ring"" cx=""{cx.ToFixed(3)}"" cy=""{cy.ToFixed(3)}"" rx=""{rx.ToFixed(3)}"" ry=""{ry.ToFixed(3)}"" fill=""none"" stroke=""{limbalPaint}"" stroke-width=""{(ringWidth * 2.0).ToFixed(3)}"" opacity=""{p.LimbalRingOpacity.ToFixed(2)}"" />");
                }
            }

            // 外輪の内側の輪: a band at the same distance inside the rim all the way round (strokes of the rim in a mask)
            if (p.LimbalInnerRingIntensity > 0.01)
            {
                double inset = baseR * (p.LimbalRingThickness + p.LimbalInnerRingGap);
                double width = baseR * p.LimbalInnerRingWidth;
                string maskId = $"{idPrefix}_limbal_inner_mask";
                string Rim(string stroke, double strokeWidth) => outlineId != null
                    ? $@"<use href=""#{outlineId}"" fill=""none"" stroke=""{stroke}"" stroke-width=""{F(strokeWidth, 4)}"" />"
                    : $@"<ellipse cx=""{F(cx)}"" cy=""{F(cy)}"" rx=""{F(rx)}"" ry=""{F(ry)}"" fill=""none"" stroke=""{stroke}"" stroke-width=""{F(strokeWidth, 4)}"" />";
                string color = GeometryHelper.SanitizeColorHex(p.LimbalInnerRingColor, IrisParameters.DefaultLimbalInnerRingColor);
                sb.AppendLine("      <defs>");
                sb.AppendLine($@"        <mask id=""{maskId}"">");
                sb.AppendLine("          " + Rim("#FFFFFF", 2.0 * (inset + width)));
                if (inset > 1e-6) sb.AppendLine("          " + Rim("#000000", 2.0 * inset));
                sb.AppendLine("        </mask>");
                sb.AppendLine("      </defs>");
                sb.AppendLine($@"      {IrisBody($"{idPrefix}_limbal_inner_ring")} fill=""{color}"" opacity=""{p.LimbalInnerRingIntensity.ToFixed(2)}"" mask=""url(#{maskId})"" />");
            }

            // Pupil
            if (p.PupilSizeRatio > 0.01 && p.PupilOpacity > 0.01)
            {
                double prx = baseR * p.PupilSizeRatio;
                double pry = baseR * p.PupilSizeRatio;
                if (p.Shape == IrisShape.VerticalOval) pry *= p.AspectRatio;
                else if (p.Shape == IrisShape.HorizontalOval) prx *= p.AspectRatio;

                // 上下左右の長さ of the pupil. The right eye is the mirror image: the pupil is drawn as it is, so its left and
                // right lengths trade places, except the moon, which is mirrored as a whole after stretching (as the symbols)
                bool flipPupil = RightEyeFlips.MirrorsShape(p.PupilRightEyeFlip, isRightEye); // 反転しない: the same pupil
                bool mirrorPupil = flipPupil && ShapeKinds.IsAsymmetric(ShapeKinds.Of(p.PupilShape));
                bool swapPupil = flipPupil && !mirrorPupil;
                double pTop = p.PupilStretchTop, pBottom = p.PupilStretchBottom;
                double pLeft = swapPupil ? p.PupilStretchRight : p.PupilStretchLeft;
                double pRight = swapPupil ? p.PupilStretchLeft : p.PupilStretchRight;

                // A round pupil whose up / down and left / right lengths are equal stays an ellipse; every other pupil is
                // its outline (the same outline as the screen / PDF renderer), rounded, then stretched.
                // A gradient spans the pupil's own bounding box (as on screen).
                bool pupilEllipse = p.PupilShape == PupilShape.Round && pTop == pBottom && pLeft == pRight;
                double erx = prx * pLeft, ery = pry * pTop;
                ShapeOutline? pupilOutline = pupilEllipse
                    ? null
                    : ShapeOutline.ForPupil(p.PupilShape, prx, pry, p.PupilStarCoreScale)
                        .Rounded(p.PupilCornerRounding)
                        .Stretched(pTop, pBottom, pLeft, pRight);
                Rect pupilBox = pupilOutline?.GetBounds(mirrorPupil) ?? GradientMapping.EllipseBox(new Point(0, 0), erx, ery);
                pupilBox.Offset(cx, cy);

                // The pupil shape (its outline or ellipse) at the eye center, with extra attributes (the core's transform)
                void AppendPupilShape(string indent, string extra)
                {
                    if (pupilOutline != null)
                        AppendPath(sb, indent + "<path d=\"", pupilOutline, cx, cy, "\"" + extra + " />", mirrorPupil);
                    else
                        sb.AppendLine($@"{indent}<ellipse cx=""{F(cx)}"" cy=""{F(cy)}"" rx=""{F(erx)}"" ry=""{F(ery)}""{extra} />");
                }

                string pupilPaint = Paint(sb, "      ", $"{idPrefix}_pupil_fill", p.PupilColor, "#060A18", pupilBox, flipPupil);

                // Soft edge (輪郭の柔らかさ): the pupil's fill blurred (the gem-cut facets stay sharp, clipped to the outline)
                string pupilBlur = "";
                double pupilSigma = 0.0;
                if (p.PupilSoftness > 0.02)
                {
                    string blurId = $"{idPrefix}_pupil_blur";
                    pupilSigma = Math.Max(prx, pry) * p.PupilSoftness * PupilBlurPerSoftness;
                    AppendBlurFilter(sb, blurId, pupilSigma, pupilBox, 0.0);
                    pupilBlur = $@" filter=""url(#{blurId})""";
                }
                sb.AppendLine($@"      <g id=""{idPrefix}_pupil"" fill=""{pupilPaint}"" opacity=""{p.PupilOpacity.ToFixed(2)}""{pupilBlur}>");
                AppendPupilShape("        ", "");
                sb.AppendLine("      </g>");

                // Sharp core over the blurred pupil: the same pupil, cut to its outline shrunk inward by 2.5 deviations.
                // A small blur (a few pixels) is computed approximately and can leave the inside of the blurred pupil
                // a little see-through; the core fills it, while the soft edge stays as the blur drew it.
                if (pupilSigma > 0)
                {
                    double narrowest = Math.Min(prx * Math.Min(pLeft, pRight), pry * Math.Min(pTop, pBottom));
                    double core = Math.Max(0.0, 1.0 - PupilCoreInset * pupilSigma / narrowest);
                    if (core > 0.05)
                    {
                        string coreClip = $"{idPrefix}_pupil_core_clip";
                        string coreTransform = $@" transform=""translate({F(cx)} {F(cy)}) scale({F(core, 5)}) translate({F(-cx)} {F(-cy)})""";
                        sb.AppendLine("      <defs>");
                        sb.AppendLine($@"        <clipPath id=""{coreClip}"">");
                        AppendPupilShape("          ", coreTransform);
                        sb.AppendLine("        </clipPath>");
                        sb.AppendLine("      </defs>");
                        sb.AppendLine($@"      <rect id=""{idPrefix}_pupil_core"" x=""{F(pupilBox.X)}"" y=""{F(pupilBox.Y)}"" width=""{F(pupilBox.Width)}"" height=""{F(pupilBox.Height)}"" fill=""{pupilPaint}"" opacity=""{p.PupilOpacity.ToFixed(2)}"" clip-path=""url(#{coreClip})"" />");
                    }
                }

                // Gem-cut facets (宝石カット), clipped to the same pupil outline
                if (p.PupilFacetIntensity > 0.01)
                {
                    string facetClip = $"{idPrefix}_pupil_facet_clip";
                    sb.AppendLine("      <defs>");
                    sb.AppendLine($@"        <clipPath id=""{facetClip}"">");
                    AppendPupilShape("          ", "");
                    sb.AppendLine("        </clipPath>");
                    sb.AppendLine("      </defs>");
                    sb.AppendLine($@"      <g id=""{idPrefix}_pupil_facets"" clip-path=""url(#{facetClip})"">");
                    foreach (var (poly, shade) in IrisPatterns.PupilFacets(prx * (pLeft + pRight) / 2.0, pry * (pTop + pBottom) / 2.0, p.PupilFacetCount, effectiveSeed, flipPupil))
                    {
                        double alpha = p.PupilOpacity * p.PupilFacetIntensity * IrisPatterns.MaxFacetAlpha * Math.Abs(shade);
                        string color = shade >= 0 ? "#FFFFFF" : "#000000";
                        AppendPath(sb, "        <path d=\"", ShapeOutline.Polygon(poly), cx, cy,
                            $@""" fill=""{color}"" fill-opacity=""{alpha.ToString("0.###", Inv)}"" />");
                    }
                    sb.AppendLine("      </g>");
                }

                // 瞳孔の中の輪: the same outline shrunk about the pupil's center; the stroke widened to match, so the line
                // keeps its width. Under the 縁取り線
                if (p.PupilInnerRingIntensity > 0.01)
                {
                    string ringColor = GeometryHelper.SanitizeColorHex(p.PupilInnerRingColor, IrisParameters.DefaultPupilInnerRingColor);
                    double k = p.PupilInnerRingSize;
                    AppendPupilShape("      ", $@" id=""{idPrefix}_pupil_inner_ring"" fill=""none"" stroke=""{ringColor}"" stroke-width=""{F(baseR * p.PupilInnerRingWidth / k, 4)}"" stroke-linejoin=""round"" opacity=""{p.PupilInnerRingIntensity.ToFixed(2)}"" transform=""translate({F(cx)} {F(cy)}) scale({F(k, 5)}) translate({F(-cx)} {F(-cy)})""");
                }

                // 瞳孔の縁取り線: a line centered on the same outline (not blurred)
                if (p.PupilOutlineIntensity > 0.01)
                {
                    string color = GeometryHelper.SanitizeColorHex(p.PupilOutlineColor, IrisParameters.DefaultPupilOutlineColor);
                    AppendPupilShape("      ", $@" id=""{idPrefix}_pupil_outline"" fill=""none"" stroke=""{color}"" stroke-width=""{F(baseR * p.PupilOutlineWidth, 4)}"" stroke-linejoin=""round"" opacity=""{p.PupilOutlineIntensity.ToFixed(2)}""");
                }
            }

            // Particles / Glitter
            if (p.ParticleCount > 0 && p.ParticleOpacity > 0.01)
            {
                double bandInner = p.PupilSizeRatio * 0.95, bandOuter = p.ParticleRadiusRatio;
                var grains = IrisPatterns.Grains(p, new Point(cx, cy), rx, ry, baseR, effectiveSeed, isRightEye, IrisStyle.MmPerStyleUnit, 0.5);

                // 重ね方: the glow and the grains are blended onto the iris together (among themselves they stack normally)
                bool glitterBlended = p.GlitterBlend != LayerBlend.Normal;
                // 見える範囲: masks the glow and the grains together. With a 重ね方 the mask goes on the blended group
                // itself (a masked group inside it would be isolated and blend with nothing)
                string? glitterRange = AppendRangeMask(sb, $"{idPrefix}_glitter_range", p, PatternLayer.Glitter, isRightEye, cx, cy, rx, ry, baseR, irisBox);
                string glitterMask = glitterRange != null ? $@" mask=""url(#{glitterRange})""" : "";
                if (glitterBlended) sb.AppendLine($@"      <g id=""{idPrefix}_glitter_layer""{BlendStyle(p.GlitterBlend)}{glitterMask}>");
                else if (glitterRange != null) sb.AppendLine($@"      <g id=""{idPrefix}_glitter_ranged""{glitterMask}>");

                // Glow (光のにじみ): a blurred, brightened copy of the grains (stars stay star-shaped, gradient colors
                // stay where they are) under the sharp grains. The copy points at the glitter drawn just below.
                if (p.ParticleGlow > 0.01 && grains.Count > 0)
                {
                    double meanSize = 0;
                    foreach (var g in grains) meanSize += g.Size;
                    meanSize /= grains.Count;
                    string glowId = $"{idPrefix}_glitter_glow";
                    AppendBlurFilter(sb, glowId + "_filter", meanSize * (GlowBlurBase + GlowBlurPerGlow * p.ParticleGlow), irisBox, meanSize * 4.0,
                        alphaSlope: GlowAlphaSlope);
                    sb.AppendLine($@"      <use id=""{glowId}"" href=""#{idPrefix}_glitter"" filter=""url(#{glowId}_filter)"" opacity=""{F(Math.Min(1.0, p.ParticleGlow))}"" />");
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
                if (glitterBlended || glitterRange != null) sb.AppendLine("      </g>");
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

                    // 右目の反転: the position (−X) and the shape (rotation, moon, lengths, copies, gradient) separately
                    bool flipSymbol = RightEyeFlips.MirrorsShape(symbol.RightEyeFlip, isRightEye);
                    double posX = RightEyeFlips.MirrorsPosition(symbol.RightEyeFlip, isRightEye) ? -symbol.PositionX : symbol.PositionX;
                    double rot = flipSymbol ? -symbol.Rotation : symbol.Rotation;

                    double symX = cx + posX * rx;
                    double symY = cy + symbol.PositionY * ry;
                    double symRadius = baseR * symbol.Size;
                    // A gradient's first color stands in on the group; each copy then gets its own gradient
                    string safeSymColor = GeometryHelper.SanitizeColorHex(symbol.Color, "#FFFFFF");
                    bool symGradient = ColorFill.Parse(symbol.Color).IsGradient;

                    // 重ね方 on the group: the copies overlap each other normally, and the group (faded by its opacity)
                    // is blended with the iris below
                    sb.AppendLine($@"      <g id=""{idPrefix}_symbol_{symbol.Type}"" fill=""{safeSymColor}"" opacity=""{symbol.Opacity.ToFixed(2)}""{BlendStyle(symbol.Blend)}>");

                    // Same outline as the screen / PDF renderer; rounded, then stretched (上下左右の長さ), once for every copy.
                    // A mirrored shape: the moon is mirrored as a whole after stretching, the other symbols are drawn as
                    // they are, so their left and right lengths trade places
                    bool mirror = symbol.Type == SymbolType.Moon && flipSymbol;
                    bool swapSides = flipSymbol && !mirror;
                    var symOutline = ShapeOutline.ForSymbol(symbol.Type, symRadius, symbol.StarCoreScale)?
                        .Rounded(symbol.CornerRounding)
                        .Stretched(symbol, swapSides);
                    Rect symBounds = symOutline?.GetBounds(mirror) ?? Rect.Empty;

                    for (int c = 0; c < symbol.Count; c++)
                    {
                        double curX = symX;
                        double curY = symY;
                        double curRot = rot;

                        if (symbol.Count > 1)
                        {
                            double offsetAngle = (2.0 * Math.PI * c) / symbol.Count;
                            if (flipSymbol) offsetAngle = Math.PI - offsetAngle;
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
                            string paint = Paint(sb, "        ", $"{idPrefix}_symbol{symbolIndex}_{c}_fill", symbol.Color, "#FFFFFF", box, flipSymbol);
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

                    // 右目の反転 (反転しない: the light source stays where it is, as a natural lighting)
                    bool mirrorHighlight = RightEyeFlips.MirrorsShape(h.RightEyeFlip, isRightEye);
                    double posX = RightEyeFlips.MirrorsPosition(h.RightEyeFlip, isRightEye) ? -h.PositionX : h.PositionX;
                    double rot = mirrorHighlight ? -h.Rotation : h.Rotation;

                    double hx = cx + posX * rx;
                    double hy = cy + h.PositionY * ry;
                    double hRadius = baseR * h.Size;
                    // A mirrored highlight is drawn as it is, so its left and right lengths trade places; the moon is not
                    // the same mirrored, so it is mirrored as a whole after stretching instead (as the symbols' moon)
                    bool mirrorShape = mirrorHighlight && ShapeKinds.IsAsymmetric(ShapeKinds.Of(h.Shape));
                    bool swapSides = mirrorHighlight && !mirrorShape;

                    // How the shape is written. A circle whose up / down and left / right lengths are equal is an
                    // ellipse (a plain circle too; corner rounding does nothing to it). The sparkle and rhombus are the
                    // same polygons as before while neither stretched nor rounded. Anything else is drawn from its
                    // outline: rounded (角の丸み), then stretched (上下左右の長さ), as the decorative symbols
                    bool ellipse = h.Shape == HighlightShape.Circle
                        && h.StretchTop == h.StretchBottom && h.StretchLeft == h.StretchRight;
                    bool polygon = (h.Shape == HighlightShape.Star4 || h.Shape == HighlightShape.Diamond)
                        && ShapeStretch.IsNone(h) && h.CornerRounding <= 0.0 && h.StarCoreScale == 1.0;
                    ShapeOutline? hOutline = ellipse || polygon
                        ? null
                        : ShapeOutline.ForHighlight(h.Shape, hRadius, h.StarCoreScale).Rounded(h.CornerRounding).Stretched(h, swapSides);
                    // Gradient across the highlight's own box (each copy its own), turned with it (as on screen)
                    Rect localBox = hOutline?.GetBounds(mirrorShape) ?? IrisStyle.HighlightBox(h, hRadius, mirrorHighlight);

                    for (int c = 0; c < h.Count; c++)
                    {
                        double curX = hx, curY = hy;
                        if (h.Count > 1)
                        {
                            // 繰り返し数: evenly on a ring of radius size × 1.4 round the position, mirrored with the highlight
                            double offsetAngle = 2.0 * Math.PI * c / h.Count;
                            if (mirrorHighlight) offsetAngle = Math.PI - offsetAngle;
                            curX += hRadius * 1.4 * Math.Cos(offsetAngle);
                            curY += hRadius * 1.4 * Math.Sin(offsetAngle);
                        }

                        var hBox = localBox;
                        hBox.Offset(curX, curY);
                        string fillId = h.Count > 1 ? $"{idPrefix}_highlight{highlightIndex}_{c}_fill" : $"{idPrefix}_highlight{highlightIndex}_fill";
                        string safeHColor = Paint(sb, "      ", fillId, h.Color, "#FFFFFF", hBox, mirrorHighlight);

                        string rotAttr = Math.Abs(rot) > 0.1 ? $@" transform=""rotate({rot.ToFixed(1)}, {curX.ToFixed(3)}, {curY.ToFixed(3)})""" : "";
                        string blend = BlendStyle(h.Blend);

                        // The copy's shape at its place, turned, with the given opacity and extra attributes
                        void AppendShape(string indent, double opacity, string extra)
                        {
                            string attrs = rotAttr + extra;
                            if (hOutline != null)
                                AppendPath(sb, indent + "<path d=\"", hOutline, curX, curY, $"\" fill=\"{safeHColor}\" opacity=\"{opacity.ToFixed(2)}\"{attrs} />", mirrorShape);
                            else if (ellipse)
                                sb.AppendLine($@"{indent}<ellipse cx=""{curX.ToFixed(3)}"" cy=""{curY.ToFixed(3)}"" rx=""{(hRadius * h.StretchLeft).ToFixed(3)}"" ry=""{(hRadius * h.StretchTop).ToFixed(3)}"" fill=""{safeHColor}"" opacity=""{opacity.ToFixed(2)}""{attrs} />");
                            else if (h.Shape == HighlightShape.Star4)
                                sb.AppendLine(indent + BuildSvgStar4(curX, curY, hRadius, 0.22, attrs, safeHColor, opacity).TrimStart());
                            else
                                sb.AppendLine(indent + BuildSvgDiamond(curX, curY, hRadius * 0.65, hRadius, attrs, safeHColor, opacity).TrimStart());
                        }

                        // A square round the copy that holds it however it is turned (the blur filters' region)
                        double reach = Math.Max(Math.Max(Math.Abs(localBox.Left), Math.Abs(localBox.Right)), Math.Max(Math.Abs(localBox.Top), Math.Abs(localBox.Bottom))) * Math.Sqrt(2.0);
                        var around = new Rect(curX - reach, curY - reach, 2 * reach, 2 * reach);
                        string copyId = h.Count > 1 ? $"{idPrefix}_highlight{highlightIndex}_{c}" : $"{idPrefix}_highlight{highlightIndex}";

                        // 光のにじみ: a blurred, brightened copy under the highlight (as the glitter's glow), so it looks
                        // lit from within. It is laid over the eye with the highlight's 重ね方
                        if (h.Glow > 0.01)
                        {
                            double glowSigma = hRadius * (HighlightGlowBlurBase + HighlightGlowBlurPerGlow * h.Glow);
                            AppendBlurFilter(sb, copyId + "_glow_filter", glowSigma, around, 0.0, alphaSlope: HighlightGlowAlphaSlope);
                            sb.AppendLine($@"      <g id=""{copyId}_glow"" filter=""url(#{copyId}_glow_filter)"" opacity=""{(Math.Sqrt(h.Glow) * h.Opacity).ToFixed(2)}""{blend}>");
                            AppendShape("        ", 1.0, "");
                            sb.AppendLine("      </g>");
                        }

                        // Each copy carries the highlight's opacity and 重ね方 itself (the group is not isolated, so every
                        // shape blends with the eye below, as separate highlights do). 輪郭のぼかし blurs the shape in a
                        // group that carries them instead
                        if (h.Softness > 0.01)
                        {
                            double softSigma = hRadius * h.Softness * HighlightSoftnessBlur;
                            AppendBlurFilter(sb, copyId + "_soft_filter", softSigma, around, 0.0);
                            sb.AppendLine($@"      <g id=""{copyId}_soft"" filter=""url(#{copyId}_soft_filter)"" opacity=""{h.Opacity.ToFixed(2)}""{blend}>");
                            AppendShape("        ", 1.0, "");
                            sb.AppendLine("      </g>");
                        }
                        else
                        {
                            AppendShape("      ", h.Opacity, blend);
                        }
                    }
                }
                sb.AppendLine("    </g>");
            }

            // Print Guides
            // Print guides: dashed cut line on the iris edge, center cross
            if (cutLine || centerMark)
            {
                sb.AppendLine($@"    <g id=""{idPrefix}_guides"">");
                if (cutLine && outlineId != null)
                    sb.AppendLine($@"      <use href=""#{outlineId}"" fill=""none"" stroke=""#999999"" stroke-width=""0.15"" stroke-dasharray=""2,2"" />");
                else if (cutLine)
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
        /// <summary>
        /// Writes the mask of a pattern layer's 見える範囲 (<see cref="PatternRange"/>) and returns its id, or null when
        /// the range covers the whole iris (nothing is written: unchanged output). The mask holds nested white sectors
        /// from the eye's center, each one piece reaching well past the iris, stretched with the iris like the
        /// patterns' own angles; on the right eye the fan is mirrored.
        /// </summary>
        private static string? AppendRangeMask(StringBuilder sb, string id, IrisParameters p, PatternLayer layer, bool isRightEye,
            double cx, double cy, double rx, double ry, double baseR, Rect irisBox)
        {
            var (angle, width, feather) = PatternRange.Of(p, layer);
            if (PatternRange.IsFull(width)) return null;

            double centerDeg = PatternRange.CenterFor(angle, isRightEye);
            var center = new Point(cx, cy);
            double reach = baseR * 3.0;
            var (x, y, w, h) = MaskArea(irisBox);
            sb.AppendLine($@"      <mask id=""{id}"" maskUnits=""userSpaceOnUse"" x=""{F(x)}"" y=""{F(y)}"" width=""{F(w)}"" height=""{F(h)}"">");
            foreach (var (half, opacity) in PatternRange.Sectors(width, feather))
            {
                string opacityAttr = opacity < 0.9995 ? $@" fill-opacity=""{F(opacity, 4)}""" : "";
                if (half >= 179.999)
                {
                    sb.AppendLine($@"        <rect x=""{F(x)}"" y=""{F(y)}"" width=""{F(w)}"" height=""{F(h)}"" fill=""#FFFFFF""{opacityAttr} />");
                    continue;
                }
                // The fan: center, then the far arc from one edge to the other in steps of at most 10°
                int steps = Math.Max(1, (int)Math.Ceiling(half * 2.0 / 10.0));
                sb.Append($@"        <path d=""M{F(cx)},{F(cy)}");
                for (int k = 0; k <= steps; k++)
                {
                    double a = (centerDeg - half + 2.0 * half * k / steps) * Math.PI / 180.0;
                    var pt = IrisPatterns.OnIris(center, rx, ry, baseR, reach, a);
                    sb.Append($" L{F(pt.X)},{F(pt.Y)}");
                }
                sb.AppendLine($@" Z"" fill=""#FFFFFF""{opacityAttr} />");
            }
            sb.AppendLine("      </mask>");
            return id;
        }

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
