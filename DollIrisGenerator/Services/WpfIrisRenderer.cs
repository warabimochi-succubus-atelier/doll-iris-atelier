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
                var rayRng = new Random(effectiveSeed);
                var rayBaseColor = GeometryHelper.ParseHexColor(effectiveRayColor, p.RayIntensity);
                var rayPen = new Pen(new SolidColorBrush(rayBaseColor), p.RayThickness * style)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round
                };
                rayPen.Freeze();

                double pupilR = baseR * p.PupilSizeRatio;
                double innerR = Math.Max(pupilR * 0.85, baseR * p.RayInnerRadiusRatio);
                double outerR = baseR * p.RayLengthRatio;

                for (int i = 0; i < p.RayCount; i++)
                {
                    double baseAngle = (2.0 * Math.PI * i) / p.RayCount;
                    double jitter = (rayRng.NextDouble() - 0.5) * (p.RayRandomness * 0.5);
                    double angle = baseAngle + jitter;

                    if (isRightEye)
                    {
                        angle = Math.PI - angle;
                    }

                    double lenVariation = 1.0 + (rayRng.NextDouble() - 0.5) * p.RayRandomness * 0.6;
                    double curOuterR = Math.Min(baseR * 0.96, outerR * lenVariation);
                    double curInnerR = innerR * (0.9 + rayRng.NextDouble() * 0.2);

                    double cosA = Math.Cos(angle);
                    double sinA = Math.Sin(angle);

                    Point startPt = new Point(
                        center.X + curInnerR * cosA * (rx / baseR),
                        center.Y + curInnerR * sinA * (ry / baseR)
                    );
                    Point endPt = new Point(
                        center.X + curOuterR * cosA * (rx / baseR),
                        center.Y + curOuterR * sinA * (ry / baseR)
                    );

                    dc.DrawLine(rayPen, startPt, endPt);
                }
            }

            // 4. Lens-Style Optical Pattern (レンズ風パターン・フレネル屈折リング)
            if (p.LensPatternEnabled && p.LensRingIntensity > 0.01)
            {
                var lensColor = GeometryHelper.ParseHexColor(p.LensRingColor, p.LensRingIntensity);
                var lensPen = new Pen(new SolidColorBrush(lensColor), p.LensRingWidth * style);
                lensPen.Freeze();

                double pupilR = baseR * p.PupilSizeRatio;
                double ringSpan = (baseR * 0.92) - pupilR;

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
                        var dashPen = new Pen(new SolidColorBrush(lensColor), p.LensRingWidth * 0.8 * style)
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
                    var glowColor = GeometryHelper.ParseHexColor(p.LensRingColor, p.LensRefractionGlow * 0.45);
                    var causticBrush = new RadialGradientBrush(glowColor, Color.FromArgb(0, 0, 0, 0));
                    dc.DrawEllipse(causticBrush, null, center, baseR * 0.55 * (rx / baseR), baseR * 0.55 * (ry / baseR));
                }
            }

            // 5. Limbal Ring (外輪)
            if (p.LimbalRingThickness > 0.005 && p.LimbalRingOpacity > 0.01)
            {
                double ringWidth = baseR * p.LimbalRingThickness;
                var ringColor = GeometryHelper.ParseHexColor(p.LimbalRingColor, p.LimbalRingOpacity);
                var ringPen = new Pen(new SolidColorBrush(ringColor), ringWidth * 2.0);
                ringPen.Freeze();
                dc.DrawGeometry(null, ringPen, boundaryGeometry);

                if (p.LimbalRingSoftness > 0.05)
                {
                    var softColor = GeometryHelper.ParseHexColor(p.LimbalRingColor, p.LimbalRingOpacity * 0.45);
                    var softPen = new Pen(new SolidColorBrush(softColor), ringWidth * 3.5 * p.LimbalRingSoftness);
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

                var pupilBrush = new SolidColorBrush(GeometryHelper.ParseHexColor(p.PupilColor, p.PupilOpacity));
                pupilBrush.Freeze();

                Geometry pupilGeo;
                switch (p.PupilShape)
                {
                    case PupilShape.VerticalOval:
                        pupilGeo = new EllipseGeometry(center, prx * 0.75, pry * 1.25);
                        break;
                    case PupilShape.CatSlit:
                        var slit = GeometryHelper.CreateCatSlitGeometry(prx * 0.45, pry * 1.15);
                        slit.Transform = new TranslateTransform(center.X, center.Y);
                        pupilGeo = slit;
                        break;
                    case PupilShape.Heart:
                        var heart = GeometryHelper.CreateHeartGeometry(prx * 0.95);
                        heart.Transform = new TranslateTransform(center.X, center.Y);
                        pupilGeo = heart;
                        break;
                    case PupilShape.Star:
                        var star = GeometryHelper.CreateStar5Geometry(prx * 1.05);
                        star.Transform = new TranslateTransform(center.X, center.Y);
                        pupilGeo = star;
                        break;
                    case PupilShape.Round:
                    default:
                        pupilGeo = new EllipseGeometry(center, prx, pry);
                        break;
                }

                dc.DrawGeometry(pupilBrush, null, pupilGeo);

                if (p.PupilSoftness > 0.02)
                {
                    var softPupilPen = new Pen(
                        new SolidColorBrush(GeometryHelper.ParseHexColor(p.PupilColor, p.PupilOpacity * 0.35)),
                        prx * p.PupilSoftness * 1.5
                    );
                    softPupilPen.Freeze();
                    dc.DrawGeometry(null, softPupilPen, pupilGeo);
                }
            }

            // 7. Particles / Glitter (ラメ・粒子)
            if (p.ParticleCount > 0 && p.ParticleOpacity > 0.01)
            {
                var particleRng = new Random(effectiveSeed + 1000);
                var particleBrush = new SolidColorBrush(GeometryHelper.ParseHexColor(p.ParticleColor, p.ParticleOpacity));
                particleBrush.Freeze();

                double minR = baseR * p.PupilSizeRatio * 0.95;
                double maxR = baseR * p.ParticleRadiusRatio;

                for (int i = 0; i < p.ParticleCount; i++)
                {
                    double dist = minR + particleRng.NextDouble() * Math.Max(1.0, maxR - minR);
                    double angle = particleRng.NextDouble() * 2.0 * Math.PI;

                    if (isRightEye)
                    {
                        angle = Math.PI - angle;
                    }

                    double pSize = (p.ParticleMinSize + particleRng.NextDouble() * Math.Max(0.1, p.ParticleMaxSize - p.ParticleMinSize)) * style;
                    double px = center.X + dist * Math.Cos(angle) * (rx / baseR);
                    double py = center.Y + dist * Math.Sin(angle) * (ry / baseR);

                    if (i % 5 == 0)
                    {
                        var glitterGeo = GeometryHelper.CreateStar4Geometry(pSize * 1.5, 0.2);
                        glitterGeo.Transform = new TranslateTransform(px, py);
                        dc.DrawGeometry(particleBrush, null, glitterGeo);
                    }
                    else
                    {
                        dc.DrawEllipse(particleBrush, null, new Point(px, py), pSize * 0.5, pSize * 0.5);
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

                    var symBrush = new SolidColorBrush(GeometryHelper.ParseHexColor(symbol.Color, symbol.Opacity));
                    symBrush.Freeze();

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

                        Geometry symGeo;
                        switch (symbol.Type)
                        {
                            case SymbolType.Star4:
                                symGeo = GeometryHelper.CreateStar4Geometry(symRadius, 0.28);
                                break;
                            case SymbolType.Star5:
                                symGeo = GeometryHelper.CreateStar5Geometry(symRadius, 0.42);
                                break;
                            case SymbolType.Heart:
                                symGeo = GeometryHelper.CreateHeartGeometry(symRadius);
                                break;
                            case SymbolType.Moon:
                                symGeo = GeometryHelper.CreateMoonGeometry(symRadius);
                                if (isRightEye)
                                {
                                    var tg = new TransformGroup();
                                    tg.Children.Add(new ScaleTransform(-1, 1));
                                    tg.Children.Add(new RotateTransform(curRot));
                                    tg.Children.Add(new TranslateTransform(curX, curY));
                                    symGeo.Transform = tg;
                                    dc.DrawGeometry(symBrush, null, symGeo);
                                    continue;
                                }
                                break;
                            case SymbolType.Cross:
                                symGeo = GeometryHelper.CreateCrossGeometry(symRadius);
                                break;
                            case SymbolType.Diamond:
                                symGeo = GeometryHelper.CreateDiamondGeometry(symRadius * 0.7, symRadius);
                                break;
                            case SymbolType.SakuraPetal:
                                symGeo = GeometryHelper.CreateSakuraPetalGeometry(symRadius);
                                break;
                            default:
                                continue;
                        }

                        var transform = new TransformGroup();
                        if (Math.Abs(curRot) > 0.1)
                        {
                            transform.Children.Add(new RotateTransform(curRot));
                        }
                        transform.Children.Add(new TranslateTransform(curX, curY));
                        symGeo.Transform = transform;

                        dc.DrawGeometry(symBrush, null, symGeo);
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

                    var hBrush = new SolidColorBrush(GeometryHelper.ParseHexColor(h.Color, h.Opacity));
                    hBrush.Freeze();

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
                    hGeo.Transform = hTransform;

                    dc.DrawGeometry(hBrush, null, hGeo);
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
