using System;
using System.Windows;
using System.Windows.Media;
using DollIrisGenerator.Models;

namespace DollIrisGenerator.Services
{
    public static class WpfIrisRenderer
    {
        /// <summary>
        /// Preview zoom the app opens at (and "リセット" returns to). 360% shows the default 10 mm iris
        /// about 136 DIP across, the same on-screen size the old 20 mm default had at 180%.
        /// </summary>
        public const double DefaultPreviewZoom = 3.6;
        /// <summary>Starting zoom of the A4 sheet view: actual size (100%), so the whole sheet is easier to take in.</summary>
        public const double DefaultSheetPreviewZoom = 1.0;
        public const double MinPreviewZoom = 0.2;
        public const double MaxPreviewZoom = 16.0;

        /// <summary>
        /// Physical size of one "style unit" (ray thickness, lens ring width, glitter size, guide lines):
        /// one screen DIP at the default preview zoom, i.e. 25.4 / (96 * 3.6) = 0.0735 mm.
        /// Tying these to millimetres makes the preview (at any zoom), PDF, PNG and SVG all match the
        /// default-zoom preview the design was made with. It moved together with the default zoom when the
        /// size range changed from eye-sized (around 20 mm) to iris-sized (around 10 mm) values, so the
        /// built-in presets keep the same proportions on the smaller iris.
        /// </summary>
        public const double MmPerStyleUnit = 25.4 / (96.0 * DefaultPreviewZoom);

        /// <summary>
        /// Brush for a color setting that may be a gradient (see <see cref="ColorFill"/>).
        /// A solid color gives the same SolidColorBrush as before. A gradient is laid on <paramref name="box"/>
        /// (in the drawing coordinates the element is drawn in, see <see cref="GradientMapping"/>) in absolute
        /// coordinates, so geometry must not carry its own Transform: push the transform on the context instead.
        /// <paramref name="opacity"/> multiplies every stop's alpha, exactly like the solid alpha.
        /// </summary>
        public static Brush CreateFillBrush(string? value, double opacity, Rect box, bool mirror = false, double bandInner = 0.0, double bandOuter = 1.0)
        {
            var fill = ColorFill.Parse(value);
            if (!fill.IsGradient)
            {
                var solid = new SolidColorBrush(GeometryHelper.ParseHexColor(value, opacity));
                solid.Freeze();
                return solid;
            }

            if (mirror) fill = fill.MirroredX();
            var map = GradientMapping.Create(fill, box, bandInner, bandOuter);
            if (map.Kind == ColorFillKind.Conic) return CreateConicBrush(map, opacity, BrushMappingMode.Absolute);
            var stops = new GradientStopCollection();
            foreach (var s in map.Stops) stops.Add(new GradientStop(GeometryHelper.ParseHexColor(s.Color, opacity), s.Offset));

            GradientBrush brush = map.Kind == ColorFillKind.Radial
                ? new RadialGradientBrush(stops)
                {
                    MappingMode = BrushMappingMode.Absolute,
                    Center = map.Center,
                    GradientOrigin = map.Center,
                    RadiusX = map.RadiusX,
                    RadiusY = map.RadiusY
                }
                : new LinearGradientBrush(stops)
                {
                    MappingMode = BrushMappingMode.Absolute,
                    StartPoint = map.Start,
                    EndPoint = map.End
                };
            brush.Freeze();
            return brush;
        }

        /// <summary>
        /// Brush for a conic (円周) gradient: WPF has no such gradient brush, so it is a DrawingBrush of thin wedges
        /// around the center (<see cref="GradientMapping.ConicWedges"/>, the same wedges the SVG export writes).
        /// The wedges are drawn aliased so no hairline seams show between them; they are only 2° wide, so their
        /// edges are not visible either. <paramref name="units"/> is Absolute for the iris (map in drawing
        /// coordinates) and RelativeToBoundingBox for a swatch (map on the unit box).
        /// </summary>
        public static Brush CreateConicBrush(GradientMapping map, double opacity, BrushMappingMode units)
        {
            var area = map.ConicArea();
            // Long enough to reach every corner of the area from the center
            double reach = Math.Sqrt(area.Width * area.Width + area.Height * area.Height);
            var group = new DrawingGroup { ClipGeometry = new RectangleGeometry(area) };
            RenderOptions.SetEdgeMode(group, EdgeMode.Aliased);
            var c = map.Center;
            foreach (var (from, to, color) in map.ConicWedges())
            {
                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    ctx.BeginFigure(c, true, true);
                    ctx.LineTo(PolarPoint(c, reach, from), false, false);
                    ctx.LineTo(PolarPoint(c, reach, to), false, false);
                }
                geo.Freeze();
                var wedgeBrush = new SolidColorBrush(GeometryHelper.ParseHexColor(color, opacity));
                wedgeBrush.Freeze();
                group.Children.Add(new GeometryDrawing(wedgeBrush, null, geo));
            }
            group.Freeze();

            var brush = new DrawingBrush(group)
            {
                Viewbox = area,
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewport = area,
                ViewportUnits = units,
                Stretch = Stretch.Fill,
                TileMode = TileMode.None
            };
            brush.Freeze();
            return brush;
        }

        /// <summary>
        /// Draws rays: plain round-capped lines when no fiber setting is on (exactly as earlier versions), otherwise
        /// each ray's fiber outline (先細り / 太さのばらつき / うねり) filled with the same brush.
        /// </summary>
        private static void DrawRays(DrawingContext dc, System.Collections.Generic.IEnumerable<RaySpec> rays, Brush brush,
            IrisParameters p, Point center, double rx, double ry, double baseR, double style, bool isRightEye)
        {
            double width = p.RayThickness * style;
            if (!p.HasFiberRays)
            {
                // Pens per width factor (the fine rays are thinner); without 太さのばらつき there are at most two
                var pens = new System.Collections.Generic.Dictionary<double, Pen>();
                foreach (var ray in rays)
                {
                    if (!pens.TryGetValue(ray.WidthFactor, out var pen))
                    {
                        pen = new Pen(brush, width * ray.WidthFactor) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                        pen.Freeze();
                        pens[ray.WidthFactor] = pen;
                    }
                    dc.DrawLine(pen,
                        IrisPatterns.OnIris(center, rx, ry, baseR, ray.InnerR, ray.Angle),
                        IrisPatterns.OnIris(center, rx, ry, baseR, ray.OuterR, ray.Angle));
                }
                return;
            }

            foreach (var ray in rays)
            {
                var outline = IrisPatterns.FiberOutline(center, rx, ry, baseR, ray, width * ray.WidthFactor, p.RayTaper, p.RayCurve, isRightEye);
                dc.DrawGeometry(brush, null, outline.ToPathGeometry());
            }
        }

        /// <summary>
        /// Color ("#AARRGGBB" or plain hex) a glitter grain has where it sits: the solid color, or the iris-wide
        /// gradient's color at that point (placed exactly like the grains' brush). Shared with the SVG export.
        /// </summary>
        internal static Func<Point, string> GrainColorSampler(string? value, Rect irisBox, bool mirror, double bandInner, double bandOuter)
        {
            var fill = ColorFill.Parse(value);
            if (!fill.IsGradient)
            {
                string solid = GeometryHelper.SanitizeColorHex(value, "#FFFFFF");
                return _ => solid;
            }
            if (mirror) fill = fill.MirroredX();
            var map = GradientMapping.Create(fill, irisBox, bandInner, bandOuter);
            return pt => map.ColorAt(pt);
        }

        /// <summary>Soft round halo of one color, on the grain's own circle (relative to its bounds).</summary>
        private static Brush CreateHaloBrush(string color, double opacity, double glow)
        {
            var stops = new GradientStopCollection();
            foreach (var (offset, alpha) in IrisPatterns.HaloStops(glow))
                stops.Add(new GradientStop(GeometryHelper.ParseHexColor(color, opacity * alpha), offset));
            var brush = new RadialGradientBrush(stops);
            brush.Freeze();
            return brush;
        }

        /// <summary>The nebula clouds as a frozen bitmap (same pixels the SVG export embeds).</summary>
        private static System.Windows.Media.Imaging.BitmapSource NebulaBitmap(IrisParameters p, int seed, bool mirror)
        {
            int n = NebulaTexture.Resolution;
            var rgba = NebulaTexture.Rgba(p, seed, mirror);
            // WPF wants BGRA
            var bgra = new byte[rgba.Length];
            for (int i = 0; i < rgba.Length; i += 4)
            {
                bgra[i] = rgba[i + 2];
                bgra[i + 1] = rgba[i + 1];
                bgra[i + 2] = rgba[i];
                bgra[i + 3] = rgba[i + 3];
            }
            var bmp = System.Windows.Media.Imaging.BitmapSource.Create(n, n, 96, 96, PixelFormats.Bgra32, null, bgra, n * 4);
            bmp.Freeze();
            return bmp;
        }

        internal static Point PolarPoint(Point c, double r, double degrees)
        {
            double rad = degrees * Math.PI / 180.0;
            return new Point(c.X + r * Math.Cos(rad), c.Y + r * Math.Sin(rad));
        }

        /// <summary>Local box (around 0, 0) of a highlight shape, the area its gradient is laid across.</summary>
        public static Rect HighlightBox(HighlightItem h, double radius) => h.Shape switch
        {
            HighlightShape.Oval => new Rect(-radius * HighlightAspect(h), -radius, 2.0 * radius * HighlightAspect(h), 2.0 * radius),
            HighlightShape.Rhombus => new Rect(-radius * 0.65, -radius, 1.3 * radius, 2.0 * radius),
            _ => new Rect(-radius, -radius, 2.0 * radius, 2.0 * radius)
        };

        private static double HighlightAspect(HighlightItem h) => h.AspectRatio > 0 ? h.AspectRatio : 1.0;

        public static void RenderIris(
            DrawingContext dc,
            Point center,
            double radiusMm,
            double pixelsPerMm,
            IrisParameters p,
            bool isRightEye,
            bool showCutLine = false,
            bool showCenterMark = false)
        {
            // Converts style units (see MmPerStyleUnit) to drawing units at this scale
            double style = MmPerStyleUnit * pixelsPerMm;

            double rx = radiusMm * pixelsPerMm;
            double ry = radiusMm * pixelsPerMm;

            if (p.Shape == IrisShape.VerticalOval)
            {
                ry *= p.AspectRatio;
            }
            else if (p.Shape == IrisShape.HorizontalOval)
            {
                rx *= p.AspectRatio;
            }

            double baseR = Math.Min(rx, ry);

            // Odd-Eye Mode Overrides for Right Eye
            bool useOddEye = isRightEye && p.IsOddEye;
            string effectiveBaseColor = useOddEye ? p.OddEyeBaseColor : p.BaseColor;
            string effectiveInnerColor = useOddEye ? p.OddEyeInnerColor : p.InnerColor;
            string effectiveOuterColor = useOddEye ? p.OddEyeOuterColor : p.OuterColor;
            string effectiveRayColor = useOddEye ? p.OddEyeRayColor : p.RayColor;
            string effectiveBottomColor = useOddEye ? p.OddEyeBottomHighlightColor : p.BottomHighlightColor;
            int effectiveSeed = useOddEye ? (p.Seed + p.OddEyeSeedOffset) : p.Seed;

            // Handle Elliptical Tilt Angle
            double tilt = p.TiltAngle;
            if (isRightEye) tilt = -tilt; // Mirror tilt angle for right eye

            bool hasTilt = Math.Abs(tilt) > 0.05;
            if (hasTilt)
            {
                dc.PushTransform(new RotateTransform(tilt, center.X, center.Y));
            }

            // 1. Define Iris Boundary Geometry
            var boundaryGeometry = new EllipseGeometry(center, rx, ry);

            // Gradients of iris-wide elements (rays, rings, glitter) are laid across the iris ellipse;
            // the right eye sees them mirrored like everything else
            var irisBox = GradientMapping.EllipseBox(center, rx, ry);

            // Push Clip to Iris Boundary
            dc.PushClip(boundaryGeometry);

            // 2. Base Radial Gradient Background
            var baseRadialBrush = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.42),
                GradientOrigin = new Point(0.5, 0.42),
                RadiusX = 0.55 * (rx / baseR),
                RadiusY = 0.55 * (ry / baseR)
            };

            var colInner = GeometryHelper.ParseHexColor(effectiveInnerColor);
            var colBase = GeometryHelper.ParseHexColor(effectiveBaseColor);
            var colOuter = GeometryHelper.ParseHexColor(effectiveOuterColor);

            baseRadialBrush.GradientStops.Add(new GradientStop(colInner, 0.0));
            baseRadialBrush.GradientStops.Add(new GradientStop(colInner, p.InnerGradientRadius * 0.4));
            baseRadialBrush.GradientStops.Add(new GradientStop(colBase, p.InnerGradientRadius));
            baseRadialBrush.GradientStops.Add(new GradientStop(colOuter, p.OuterGradientRadius));
            baseRadialBrush.GradientStops.Add(new GradientStop(colOuter, 1.0));

            dc.DrawGeometry(baseRadialBrush, null, boundaryGeometry);

            // Aurora layer (オーロラ層): a color over the iris body, under the shading so the depth stays
            if (p.AuroraOpacity > 0.01)
            {
                var auroraBrush = CreateFillBrush(p.AuroraColor, p.AuroraOpacity, irisBox, isRightEye);
                dc.DrawGeometry(auroraBrush, null, boundaryGeometry);
            }

            // Nebula layer (星雲): the shared cloud image stretched over the iris ellipse
            if (p.NebulaOpacity > 0.01)
            {
                dc.PushOpacity(p.NebulaOpacity);
                dc.DrawImage(NebulaBitmap(p, effectiveSeed, isRightEye), irisBox);
                dc.Pop();
            }

            // Upper Shadow (Eyelid depth)
            var upperShadowBrush = new LinearGradientBrush(
                Color.FromArgb(160, 0, 0, 0),
                Color.FromArgb(0, 0, 0, 0),
                new Point(0.5, 0.0),
                new Point(0.5, 0.65)
            );
            dc.DrawGeometry(upperShadowBrush, null, boundaryGeometry);

            // Lower Glow / Reflected Light (アニメ調下部ハイライト・照り返し)
            if (p.BottomHighlightIntensity > 0.01)
            {
                var bottomColor = GeometryHelper.ParseHexColor(effectiveBottomColor, p.BottomHighlightIntensity);
                var lowerGlowBrush = new LinearGradientBrush(
                    Color.FromArgb(0, 0, 0, 0),
                    bottomColor,
                    new Point(0.5, 0.45),
                    new Point(0.5, 1.0)
                );
                dc.DrawGeometry(lowerGlowBrush, null, boundaryGeometry);
            }

            // 3. Radial Rays (放射状ライン / 放射模様)
            if (p.RayCount > 0 && p.RayIntensity > 0.01)
            {
                double innerR = IrisPatterns.InnerRayRadius(p, baseR);
                double bandInner = innerR / baseR, bandOuter = Math.Min(0.96, p.RayLengthRatio);

                // Radial gradient: from the rays' start to their (nominal) tips
                var rayBrush = CreateFillBrush(effectiveRayColor, p.RayIntensity, irisBox, isRightEye, bandInner, bandOuter);
                DrawRays(dc, IrisPatterns.MainRays(p, baseR, effectiveSeed, isRightEye), rayBrush, p, center, rx, ry, baseR, style, isRightEye);

                // Fine rays (細線レイヤー) between the main ones, on top so their light lines stay visible
                if (p.RayFineLayer > 0.01)
                {
                    var fineBrush = CreateFillBrush(effectiveRayColor, p.RayIntensity * p.RayFineLayer, irisBox, isRightEye, bandInner, bandOuter);
                    DrawRays(dc, IrisPatterns.FineRays(p, baseR, effectiveSeed, isRightEye), fineBrush, p, center, rx, ry, baseR, style, isRightEye);
                }
            }

            // 4. Lens-Style Optical Pattern (レンズ風パターン・フレネル屈折リング)
            if (p.LensPatternEnabled && p.LensRingIntensity > 0.01)
            {
                double pupilR = baseR * p.PupilSizeRatio;
                double ringSpan = (baseR * 0.92) - pupilR;

                // Radial gradient: from the innermost ring (pupil edge) to the outermost one
                var lensBrush = CreateFillBrush(p.LensRingColor, p.LensRingIntensity, irisBox, isRightEye, p.PupilSizeRatio, 0.92);
                var lensPen = new Pen(lensBrush, p.LensRingWidth * style);
                lensPen.Freeze();

                for (int k = 1; k <= p.LensRingCount; k++)
                {
                    // Non-linear Fresnel lens power spacing
                    double progress = Math.Pow((double)k / p.LensRingCount, 0.82);
                    double ringR = pupilR + ringSpan * progress;
                    double ringRx = ringR * (rx / baseR);
                    double ringRy = ringR * (ry / baseR);

                    // Alternate slightly dotted or continuous optical grooves
                    if (k % 2 == 1)
                    {
                        var dashPen = new Pen(lensBrush, p.LensRingWidth * 0.8 * style)
                        {
                            DashStyle = new DashStyle(new double[] { 4, 3 }, 0)
                        };
                        dashPen.Freeze();
                        dc.DrawEllipse(null, dashPen, center, ringRx, ringRy);
                    }
                    else
                    {
                        dc.DrawEllipse(null, lensPen, center, ringRx, ringRy);
                    }
                }

                // Center Refraction Caustic Glow
                if (p.LensRefractionGlow > 0.01)
                {
                    // One color only (a gradient's first color): the glow already fades out by itself
                    var glowColor = GeometryHelper.ParseHexColor(p.LensRingColor, p.LensRefractionGlow * 0.45);
                    var causticBrush = new RadialGradientBrush(glowColor, Color.FromArgb(0, 0, 0, 0));
                    dc.DrawEllipse(causticBrush, null, center, baseR * 0.55 * (rx / baseR), baseR * 0.55 * (ry / baseR));
                }
            }

            // 5. Limbal Ring (外輪)
            if (p.LimbalRingThickness > 0.005 && p.LimbalRingOpacity > 0.01)
            {
                double ringWidth = baseR * p.LimbalRingThickness;
                // Radial gradient: from the ring's inner edge to the iris edge
                double ringInner = 1.0 - p.LimbalRingThickness;
                var ringPen = new Pen(CreateFillBrush(p.LimbalRingColor, p.LimbalRingOpacity, irisBox, isRightEye, ringInner), ringWidth * 2.0);
                ringPen.Freeze();
                dc.DrawGeometry(null, ringPen, boundaryGeometry);

                if (p.LimbalRingSoftness > 0.05)
                {
                    var softBrush = CreateFillBrush(p.LimbalRingColor, p.LimbalRingOpacity * 0.45, irisBox, isRightEye, ringInner);
                    var softPen = new Pen(softBrush, ringWidth * 3.5 * p.LimbalRingSoftness);
                    softPen.Freeze();
                    dc.DrawGeometry(null, softPen, boundaryGeometry);
                }
            }

            // 6. Pupil (瞳孔)
            if (p.PupilSizeRatio > 0.01 && p.PupilOpacity > 0.01)
            {
                double prx = baseR * p.PupilSizeRatio;
                double pry = baseR * p.PupilSizeRatio;

                if (p.Shape == IrisShape.VerticalOval) pry *= p.AspectRatio;
                else if (p.Shape == IrisShape.HorizontalOval) prx *= p.AspectRatio;

                // Drawn around (0, 0) with the move pushed on the context, so a gradient follows the pupil;
                // the gradient spans the pupil's own bounding box
                Geometry pupilGeo;
                Rect pupilBox;
                var pupilOutline = ShapeOutline.ForPupil(p.PupilShape, prx, pry);
                if (pupilOutline != null)
                {
                    // Cat slit / heart / star: shared outline (same as SVG), corners rounded as requested
                    var rounded = pupilOutline.Rounded(p.PupilCornerRounding);
                    pupilGeo = rounded.ToPathGeometry();
                    pupilBox = rounded.GetBounds();
                }
                else if (p.PupilShape == PupilShape.VerticalOval)
                {
                    pupilGeo = new EllipseGeometry(new Point(0, 0), prx * 0.75, pry * 1.25);
                    pupilBox = GradientMapping.EllipseBox(new Point(0, 0), prx * 0.75, pry * 1.25);
                }
                else
                {
                    pupilGeo = new EllipseGeometry(new Point(0, 0), prx, pry);
                    pupilBox = GradientMapping.EllipseBox(new Point(0, 0), prx, pry);
                }

                var pupilBrush = CreateFillBrush(p.PupilColor, p.PupilOpacity, pupilBox, isRightEye);

                dc.PushTransform(new TranslateTransform(center.X, center.Y));
                dc.DrawGeometry(pupilBrush, null, pupilGeo);

                // Gem-cut facets (宝石カット), clipped to the pupil's outline
                if (p.PupilFacetIntensity > 0.01)
                {
                    dc.PushClip(pupilGeo);
                    foreach (var (poly, shade) in IrisPatterns.PupilFacets(prx, pry, p.PupilFacetCount, effectiveSeed, isRightEye))
                    {
                        double alpha = p.PupilOpacity * p.PupilFacetIntensity * IrisPatterns.MaxFacetAlpha * Math.Abs(shade);
                        var c = shade >= 0 ? Colors.White : Colors.Black;
                        var facetBrush = new SolidColorBrush(Color.FromArgb((byte)Math.Clamp((int)(alpha * 255), 0, 255), c.R, c.G, c.B));
                        facetBrush.Freeze();
                        dc.DrawGeometry(facetBrush, null, ShapeOutline.Polygon(poly).ToPathGeometry());
                    }
                    dc.Pop();
                }

                if (p.PupilSoftness > 0.02)
                {
                    var softPupilPen = new Pen(
                        CreateFillBrush(p.PupilColor, p.PupilOpacity * 0.35, pupilBox, isRightEye),
                        prx * p.PupilSoftness * 1.5
                    );
                    softPupilPen.Freeze();
                    dc.DrawGeometry(null, softPupilPen, pupilGeo);
                }
                dc.Pop();
            }

            // 7. Particles / Glitter (ラメ・粒子)
            if (p.ParticleCount > 0 && p.ParticleOpacity > 0.01)
            {
                double bandInner = p.PupilSizeRatio * 0.95, bandOuter = p.ParticleRadiusRatio;
                var grains = IrisPatterns.Grains(p, center, rx, ry, baseR, effectiveSeed, isRightEye, style, 1.0);

                // One brush over the whole iris: each grain takes the gradient color of where it sits
                // (radial: from the inner to the outer edge of the glitter band)
                var particleBrush = CreateFillBrush(p.ParticleColor, p.ParticleOpacity, irisBox, isRightEye, bandInner, bandOuter);

                // Halos first (光のにじみ), so every grain stays crisp on top of its neighbours' glow
                if (p.ParticleGlow > 0.01)
                {
                    var colorAt = GrainColorSampler(p.ParticleColor, irisBox, isRightEye, bandInner, bandOuter);
                    var haloBrushes = new System.Collections.Generic.Dictionary<string, Brush>();
                    foreach (var g in grains)
                    {
                        string color = colorAt(g.Position);
                        if (!haloBrushes.TryGetValue(color, out var halo))
                        {
                            halo = CreateHaloBrush(color, p.ParticleOpacity, p.ParticleGlow);
                            haloBrushes[color] = halo;
                        }
                        double hr = IrisPatterns.HaloRadius(g.Size, p.ParticleGlow);
                        dc.DrawEllipse(halo, null, g.Position, hr, hr);
                    }
                }

                foreach (var g in grains)
                {
                    if (g.IsStar)
                    {
                        // Built in place (no geometry transform) so the iris-wide brush is not shifted
                        var glitterGeo = ShapeOutline.Star(4, g.Size * 1.5, 0.2).Offset(g.Position.X, g.Position.Y).ToPathGeometry();
                        dc.DrawGeometry(particleBrush, null, glitterGeo);
                    }
                    else
                    {
                        dc.DrawEllipse(particleBrush, null, g.Position, g.Size * 0.5, g.Size * 0.5);
                    }
                }

                // Cross flares (十字フレア) on the chosen grains
                if (p.ParticleFlareRatio > 0.001)
                {
                    foreach (var g in grains)
                    {
                        if (!g.HasFlare) continue;
                        var flareGeo = IrisPatterns.Flare(g.Size, p.ParticleFlareLength).Offset(g.Position.X, g.Position.Y).ToPathGeometry();
                        dc.DrawGeometry(particleBrush, null, flareGeo);
                    }
                }
            }

            // 8. Decorative Symbols (装飾記号)
            if (p.Symbols != null)
            {
                foreach (var symbol in p.Symbols)
                {
                    if (!symbol.Enabled || symbol.Type == SymbolType.None || symbol.Opacity <= 0.01)
                        continue;

                    double posX = symbol.PositionX;
                    double rot = symbol.Rotation;

                    if (isRightEye)
                    {
                        posX = -posX;
                        rot = -rot;
                    }

                    double symX = center.X + posX * rx;
                    double symY = center.Y + symbol.PositionY * ry;
                    double symRadius = baseR * symbol.Size;

                    // Same outline as the SVG export; rounded once and reused for every copy.
                    // The moon faces the other way in the right eye.
                    var symOutline = ShapeOutline.ForSymbol(symbol.Type, symRadius)?.Rounded(symbol.CornerRounding);
                    if (symOutline != null && symbol.Type == SymbolType.Moon && isRightEye) symOutline = symOutline.MirroredX();
                    if (symOutline == null) continue;

                    // Each copy is drawn around (0, 0) with its rotation / position pushed on the context,
                    // so one brush laid across the symbol's bounding box serves every copy and turns with it
                    var symBrush = CreateFillBrush(symbol.Color, symbol.Opacity, symOutline.GetBounds(), isRightEye);
                    Geometry symGeo = symOutline.ToPathGeometry();
                    symGeo.Freeze();

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

                        var transform = new TransformGroup();
                        // The mirrored moon always took its rotation, the other symbols from 0.1° on
                        if (Math.Abs(curRot) > 0.1 || (symbol.Type == SymbolType.Moon && isRightEye))
                        {
                            transform.Children.Add(new RotateTransform(curRot));
                        }
                        transform.Children.Add(new TranslateTransform(curX, curY));

                        dc.PushTransform(transform);
                        dc.DrawGeometry(symBrush, null, symGeo);
                        dc.Pop();
                    }
                }
            }

            // Pop boundary clip before drawing highlights
            dc.Pop();

            // 9. Highlights (ハイライト)
            if (p.Highlights != null)
            {
                foreach (var h in p.Highlights)
                {
                    if (!h.Enabled || h.Opacity <= 0.01 || h.Size <= 0.005)
                        continue;

                    double posX = h.PositionX;
                    double rot = h.Rotation;

                    if (isRightEye && p.MirrorHighlightsWithIris)
                    {
                        posX = -posX;
                        rot = -rot;
                    }

                    double hx = center.X + posX * rx;
                    double hy = center.Y + h.PositionY * ry;
                    double hRadius = baseR * h.Size;

                    // Gradient across the highlight's own box, turning with it; mirrored with the highlight
                    var hBrush = CreateFillBrush(h.Color, h.Opacity, HighlightBox(h, hRadius), isRightEye && p.MirrorHighlightsWithIris);

                    Geometry hGeo;
                    switch (h.Shape)
                    {
                        case HighlightShape.Oval:
                            hGeo = new EllipseGeometry(new Point(0, 0), hRadius * (h.AspectRatio > 0 ? h.AspectRatio : 1.0), hRadius);
                            break;
                        case HighlightShape.Star4:
                            hGeo = GeometryHelper.CreateStar4Geometry(hRadius, 0.22);
                            break;
                        case HighlightShape.Rhombus:
                            hGeo = GeometryHelper.CreateDiamondGeometry(hRadius * 0.65, hRadius);
                            break;
                        case HighlightShape.Circle:
                        default:
                            hGeo = new EllipseGeometry(new Point(0, 0), hRadius, hRadius);
                            break;
                    }

                    var hTransform = new TransformGroup();
                    if (Math.Abs(rot) > 0.1)
                    {
                        hTransform.Children.Add(new RotateTransform(rot));
                    }
                    hTransform.Children.Add(new TranslateTransform(hx, hy));

                    dc.PushTransform(hTransform);
                    dc.DrawGeometry(hBrush, null, hGeo);
                    dc.Pop();
                }
            }

            // 10. Print Guides (Cut lines & Center marks)
            if (showCutLine)
            {
                var cutPen = new Pen(new SolidColorBrush(Color.FromArgb(180, 160, 160, 160)), Math.Max(0.5, 0.75 * style)) // guides keep a visible minimum on screen
                {
                    DashStyle = DashStyles.Dash
                };
                cutPen.Freeze();
                dc.DrawGeometry(null, cutPen, boundaryGeometry);
            }

            if (showCenterMark)
            {
                double markLen = baseR * 0.15;
                var markPen = new Pen(new SolidColorBrush(Color.FromArgb(160, 120, 120, 120)), Math.Max(0.5, 0.6 * style));
                markPen.Freeze();
                dc.DrawLine(markPen, new Point(center.X - markLen, center.Y), new Point(center.X + markLen, center.Y));
                dc.DrawLine(markPen, new Point(center.X, center.Y - markLen), new Point(center.X, center.Y + markLen));
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), null, center, 0.8 * style, 0.8 * style);
            }

            // Pop tilt rotation transform if applied
            if (hasTilt)
            {
                dc.Pop();
            }
        }
    }
}
