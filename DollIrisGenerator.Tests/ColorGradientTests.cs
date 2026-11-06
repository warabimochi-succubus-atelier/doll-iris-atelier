using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using DollIrisGenerator.Controls;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using DollIrisGenerator.ViewModels;
using Xunit;

namespace DollIrisGenerator.Tests
{
    /// <summary>Gradient color settings (単色 / グラデーション): model, saving, rendering, SVG, UI.</summary>
    [Collection(ColorPickerBoxUiCollection.Name)]
    public class ColorGradientTests
    {
        private const string RedToBlue = "linear-gradient(0deg, #FF0000 0%, #0000FF 100%)";
        private const string RadialRedToBlue = "radial-gradient(#FF0000 0%, #0000FF 100%)";

        private static void RunOnSta(Action body)
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try { body(); }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }

        // ------------------------------------------------------------------
        // Model / notation
        // ------------------------------------------------------------------

        [Theory]
        [InlineData("#FFFFFF")]
        [InlineData("#abc")]
        [InlineData("#80FF0000")]
        [InlineData("")]
        [InlineData("not a color")]
        [InlineData("linear-gradient(90deg, red 0%, blue 100%)")]   // named colors are not accepted
        [InlineData("linear-gradient(90deg, #FF0000 0%)")]          // one stop is not a gradient
        [InlineData("radial-gradient(45deg, #FF0000, #0000FF)")]    // radial has no angle
        [InlineData("linear-gradient(90deg, #FF0000 0%, #0000FF 100%")] // unbalanced
        public void TestAnythingThatIsNotAGradientStaysTheSameSolidString(string value)
        {
            var fill = ColorFill.Parse(value);
            Assert.Equal(ColorFillKind.Solid, fill.Kind);
            Assert.False(fill.IsGradient);
            Assert.Equal(value, fill.ToString());
        }

        [Theory]
        [InlineData("linear-gradient(90deg, #FFFFFF 0%, #80D8FF 100%)")]
        [InlineData("linear-gradient(33.5deg, #FFFFFF 0%, #80D8FF 42.5%, #0D47A1 100%)")]
        [InlineData("radial-gradient(#FFFFFF 0%, #80D8FF 100%)")]
        [InlineData("radial-gradient(#FFFFFF 10%, #80D8FF 20%, #0D47A1 30%, #000000 40%, #FF00FF 100%)")]
        [InlineData("linear-gradient(0deg, #80FF0000 0%, #0000FF 100%)")]
        public void TestGradientNotationRoundTripsExactly(string value)
        {
            var fill = ColorFill.Parse(value);
            Assert.True(fill.IsGradient);
            Assert.Equal(value, fill.ToString());
            Assert.Equal(value, ColorFill.Parse(fill.ToString()).ToString());
        }

        [Fact]
        public void TestGradientIsNormalized()
        {
            // Lower case, unsorted, out-of-range offsets, negative angle, missing positions, spaces
            var fill = ColorFill.Parse("  LINEAR-GRADIENT( -90deg , #ff0000 80% , #00ff00 , #0000ff 150% )  ");
            Assert.Equal(ColorFillKind.Linear, fill.Kind);
            Assert.Equal(270.0, fill.Angle);
            Assert.Equal(new[] { 0.5, 0.8, 1.0 }, fill.Stops.Select(s => s.Offset).ToArray());
            Assert.Equal("linear-gradient(270deg, #00FF00 50%, #FF0000 80%, #0000FF 100%)", fill.ToString());

            // Too many stops are cut to 5; too few are filled up to 2
            var many = ColorFill.Create(ColorFillKind.Radial, 0, Enumerable.Range(0, 8).Select(i => new ColorStop("#FFFFFF", i / 7.0)));
            Assert.Equal(ColorFill.MaxStops, many.Stops.Count);
            var one = ColorFill.Create(ColorFillKind.Linear, 400, new[] { new ColorStop("#123456", 0.3) });
            Assert.Equal(2, one.Stops.Count);
            Assert.Equal(40.0, one.Angle);
            Assert.Equal(ColorFill.DefaultAngle, ColorFill.Create(ColorFillKind.Linear, double.NaN, one.Stops).Angle);

            // A crafted, huge value is not parsed at all
            string huge = "linear-gradient(90deg, " + string.Join(", ", Enumerable.Repeat("#FFFFFF 0%", 200)) + ")";
            Assert.False(ColorFill.Parse(huge).IsGradient);
        }

        [Fact]
        public void TestMirroringFlipsOnlyTheLinearDirection()
        {
            Assert.Equal(180.0, ColorFill.Parse(RedToBlue).MirroredX().Angle);
            Assert.Equal(90.0, ColorFill.Parse("linear-gradient(90deg, #FF0000 0%, #0000FF 100%)").MirroredX().Angle);
            Assert.Equal(150.0, ColorFill.Parse("linear-gradient(30deg, #FF0000 0%, #0000FF 100%)").MirroredX().Angle);
            Assert.Equal(RadialRedToBlue, ColorFill.Parse(RadialRedToBlue).MirroredX().ToString());
        }

        [Fact]
        public void TestDefaultGradientStartsFromTheCurrentColor()
        {
            var dark = ColorFill.DefaultGradientFrom("#0D47A1");
            Assert.Equal(ColorFillKind.Linear, dark.Kind);
            Assert.Equal("#0D47A1", dark.Stops[0].Color);
            Assert.NotEqual("#0D47A1", dark.Stops[1].Color);

            var white = ColorFill.DefaultGradientFrom("#FFFFFF");
            Assert.Equal("#FFFFFF", white.Stops[0].Color);
            Assert.NotEqual("#FFFFFF", white.Stops[1].Color); // light colors fade darker instead
        }

        [Fact]
        public void TestSingleColorHelpersUseTheFirstColorOfAGradient()
        {
            // A gradient in a solid-only setting (hand-edited file) must still give a valid color
            Assert.Equal("#FF0000", GeometryHelper.SanitizeColorHex(RedToBlue, "#000000"));
            Assert.Equal(Color.FromRgb(255, 0, 0), GeometryHelper.ParseHexColor(RedToBlue));
            Assert.Equal("#ABCDEF", GeometryHelper.SanitizeColorHex("#ABCDEF"));
        }

        // ------------------------------------------------------------------
        // Saving, Clone, undo, presets, randomizer
        // ------------------------------------------------------------------

        private static IrisParameters WithGradients()
        {
            var p = new IrisParameters
            {
                LimbalRingColor = RadialRedToBlue,
                PupilColor = RedToBlue,
                RayColor = "linear-gradient(45deg, #FFFFFF 0%, #80D8FF 50%, #0D47A1 100%)",
                ParticleColor = RadialRedToBlue,
                LensRingColor = RedToBlue,
                LensPatternEnabled = true,
                IsOddEye = true,
                OddEyeRayColor = "linear-gradient(135deg, #FFE082 0%, #E65100 100%)"
            };
            p.Highlights[0].Color = "radial-gradient(#FFFFFF 0%, #80FFFFFF 100%)";
            p.Symbols[0].Enabled = true;
            p.Symbols[0].Count = 3;
            p.Symbols[0].Color = RedToBlue;
            return p;
        }

        [Fact]
        public void TestOldFilesWithPlainColorsLoadAndSaveUnchanged()
        {
            const string oldJson = "{\"Name\":\"旧\",\"RayColor\":\"#B3E5FC\",\"PupilColor\":\"#060A18\",\"Symbols\":[{\"Type\":3,\"Color\":\"#FF80AB\",\"Enabled\":true}]}";
            var p = JsonSerializer.Deserialize<IrisParameters>(oldJson)!;
            Assert.Equal("#B3E5FC", p.RayColor);
            Assert.Equal("#FF80AB", p.Symbols[0].Color);

            string saved = JsonSerializer.Serialize(p);
            Assert.Contains("\"RayColor\":\"#B3E5FC\"", saved);
            // The aurora layer (newer than these files) starts with a conic gradient but is off (strength 0);
            // every color the old file had stays a plain color
            // The same goes for the nebula layer
            Assert.Equal(0.0, p.AuroraOpacity);
            Assert.Equal(0.0, p.NebulaOpacity);
            string savedWithoutLayers = saved
                .Replace(JsonSerializer.Serialize(IrisParameters.DefaultAuroraColor), "\"\"")
                .Replace(JsonSerializer.Serialize(IrisParameters.DefaultNebulaColor), "\"\"");
            Assert.DoesNotContain("gradient", savedWithoutLayers);
        }

        [Fact]
        public void TestGradientsSurviveSaveLoadAndClone()
        {
            var p = WithGradients();
            var loaded = JsonSerializer.Deserialize<IrisParameters>(JsonSerializer.Serialize(p))!;
            var clone = p.Clone();

            foreach (var copy in new[] { loaded, clone })
            {
                Assert.Equal(p.LimbalRingColor, copy.LimbalRingColor);
                Assert.Equal(p.PupilColor, copy.PupilColor);
                Assert.Equal(p.RayColor, copy.RayColor);
                Assert.Equal(p.ParticleColor, copy.ParticleColor);
                Assert.Equal(p.LensRingColor, copy.LensRingColor);
                Assert.Equal(p.OddEyeRayColor, copy.OddEyeRayColor);
                Assert.Equal(p.Highlights[0].Color, copy.Highlights[0].Color);
                Assert.Equal(p.Symbols[0].Color, copy.Symbols[0].Color);
            }
            Assert.Equal(p.Symbols[0].Color, p.Symbols[0].Clone().Color);
            Assert.Equal(p.Highlights[0].Color, p.Highlights[0].Clone().Color);
        }

        [Fact]
        public void TestGradientEditsAreUndoableAndRedoable()
        {
            var vm = new MainViewModel();
            string original = vm.CurrentIris.RayColor;

            // A stop slider drag: many values in a row become one undo step
            vm.CurrentIris.RayColor = "linear-gradient(90deg, #FFFFFF 0%, #0000FF 100%)";
            vm.CurrentIris.RayColor = "linear-gradient(90deg, #FFFFFF 10%, #0000FF 100%)";
            vm.CurrentIris.RayColor = "linear-gradient(90deg, #FFFFFF 20%, #0000FF 100%)";

            vm.UndoCommand.Execute(null);
            Assert.Equal(original, vm.CurrentIris.RayColor);
            vm.RedoCommand.Execute(null);
            Assert.Equal("linear-gradient(90deg, #FFFFFF 20%, #0000FF 100%)", vm.CurrentIris.RayColor);

            vm.CurrentIris.Symbols[0].Color = RedToBlue;
            vm.UndoCommand.Execute(null);
            Assert.Equal("#FFFFFF", vm.CurrentIris.Symbols[0].Color);
        }

        [Fact]
        public void TestPresetsAndRandomizerReplaceGradientsWithValidColors()
        {
            var p = WithGradients();
            ColorPresets.ApplyPreset(p, ColorPresets.All.First());
            Assert.False(ColorFill.IsGradientString(p.PupilColor));
            Assert.False(ColorFill.IsGradientString(p.RayColor));
            Assert.False(ColorFill.IsGradientString(p.LimbalRingColor));

            var q = WithGradients();
            IrisRandomizer.RandomizeAttributes(q, new Random(7));
            Assert.True(ColorFill.IsHexColor(q.RayColor));
            Assert.True(ColorFill.IsHexColor(q.PupilColor));
            Assert.True(SvgValidator.IsWellFormed(SvgExporter.GeneratePairSvg(q), out _));
        }

        // ------------------------------------------------------------------
        // Shared placement
        // ------------------------------------------------------------------

        [Fact]
        public void TestLinearMappingCoversTheBoxAtAnyAngle()
        {
            var box = new Rect(10, 20, 40, 20);
            var horizontal = GradientMapping.Create(ColorFill.Parse(RedToBlue), box);
            Assert.Equal(new Point(10, 30), horizontal.Start);
            Assert.Equal(new Point(50, 30), horizontal.End);

            var down = GradientMapping.Create(ColorFill.Parse("linear-gradient(90deg, #FF0000 0%, #0000FF 100%)"), box);
            Assert.Equal(30, down.Start.X, 9);
            Assert.Equal(20, down.Start.Y, 9);
            Assert.Equal(40, down.End.Y, 9);

            // Diagonal: every corner projects inside 0..100 %
            var diag = GradientMapping.Create(ColorFill.Parse("linear-gradient(37deg, #FF0000 0%, #0000FF 100%)"), box);
            var dir = diag.End - diag.Start;
            foreach (var corner in new[] { box.TopLeft, box.TopRight, box.BottomLeft, box.BottomRight })
            {
                double t = Vector.Multiply(corner - diag.Start, dir) / dir.LengthSquared;
                Assert.InRange(t, -1e-9, 1.0 + 1e-9);
            }
        }

        [Fact]
        public void TestRadialMappingMovesStopsIntoTheBand()
        {
            var map = GradientMapping.Create(ColorFill.Parse(RadialRedToBlue), new Rect(0, 0, 100, 60), 0.8, 1.0);
            Assert.Equal(new Point(50, 30), map.Center);
            Assert.Equal(50, map.RadiusX);
            Assert.Equal(30, map.RadiusY);
            Assert.Equal(0.8, map.Stops[0].Offset, 9);
            Assert.Equal(1.0, map.Stops[1].Offset, 9);
        }

        // ------------------------------------------------------------------
        // Screen / PDF renderer (pixels)
        // ------------------------------------------------------------------

        /// <summary>An iris with nothing but what the test switches on, on a 200 x 200 DIP canvas (radius 90).</summary>
        private static IrisParameters Bare()
        {
            var p = new IrisParameters
            {
                SizeMm = 18,
                BaseColor = "#000000",
                InnerColor = "#000000",
                OuterColor = "#000000",
                BottomHighlightIntensity = 0,
                RayCount = 0,
                ParticleCount = 0,
                LimbalRingThickness = 0,
                PupilSizeRatio = 0.05,
                PupilOpacity = 0,
                LensPatternEnabled = false
            };
            p.Highlights.Clear();
            p.Symbols.Clear();
            return p;
        }

        private static Func<int, int, Color> Render(IrisParameters p, bool rightEye)
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, 200, 200));
                WpfIrisRenderer.RenderIris(dc, new Point(100, 100), 9.0, 10.0, p, rightEye);
            }
            var bmp = new RenderTargetBitmap(200, 200, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(visual);
            var px = new byte[200 * 200 * 4];
            bmp.CopyPixels(px, 200 * 4, 0);
            return (x, y) =>
            {
                int i = (y * 200 + x) * 4;
                return Color.FromRgb(px[i + 2], px[i + 1], px[i]);
            };
        }

        private static void AssertRedDominant(Color c) => Assert.True(c.R > 150 && c.B < 100, $"expected red, got {c}");
        private static void AssertBlueDominant(Color c) => Assert.True(c.B > 150 && c.R < 100, $"expected blue, got {c}");

        [Fact]
        public void TestPupilGradientRunsAcrossThePupilAndMirrorsInTheRightEye()
        {
            RunOnSta(() =>
            {
                var p = Bare();
                p.PupilSizeRatio = 0.6; // radius 54
                p.PupilOpacity = 1.0;
                p.PupilSoftness = 0;
                p.PupilColor = RedToBlue;

                var left = Render(p, rightEye: false);
                AssertRedDominant(left(100 - 45, 100));
                AssertBlueDominant(left(100 + 45, 100));

                var right = Render(p, rightEye: true);
                AssertBlueDominant(right(100 - 45, 100));
                AssertRedDominant(right(100 + 45, 100));
            });
        }

        [Fact]
        public void TestSymbolGradientTurnsWithTheSymbol()
        {
            RunOnSta(() =>
            {
                var p = Bare();
                p.Symbols.Add(new DecorativeSymbolItem { Type = SymbolType.Diamond, Size = 0.5, PositionX = 0, PositionY = 0, Opacity = 1, Enabled = true, Color = RedToBlue });

                var straight = Render(p, rightEye: false);
                AssertRedDominant(straight(100 - 18, 100));
                AssertBlueDominant(straight(100 + 18, 100));

                p.Symbols[0].Rotation = 180;
                var turned = Render(p, rightEye: false);
                AssertBlueDominant(turned(100 - 18, 100));
                AssertRedDominant(turned(100 + 18, 100));
            });
        }

        [Fact]
        public void TestLimbalRingRadialGradientRunsFromInnerEdgeToIrisEdge()
        {
            RunOnSta(() =>
            {
                var p = Bare();
                p.LimbalRingThickness = 0.2; // band 72..90
                p.LimbalRingOpacity = 1.0;
                p.LimbalRingSoftness = 0;
                p.LimbalRingColor = RadialRedToBlue;

                var img = Render(p, rightEye: false);
                AssertRedDominant(img(100 + 74, 100));
                AssertBlueDominant(img(100 + 88, 100));
                AssertRedDominant(img(100, 100 - 74));
            });
        }

        [Fact]
        public void TestHighlightGradientRunsAcrossTheHighlight()
        {
            RunOnSta(() =>
            {
                var p = Bare();
                p.Highlights.Add(new HighlightItem { Shape = HighlightShape.Circle, PositionX = 0, PositionY = 0, Size = 0.4, Opacity = 1, Enabled = true, Color = RedToBlue });

                var img = Render(p, rightEye: false);
                AssertRedDominant(img(100 - 30, 100));
                AssertBlueDominant(img(100 + 30, 100));
            });
        }

        [Fact]
        public void TestSolidColorsRenderExactlyAsSolidBrushes()
        {
            RunOnSta(() =>
            {
                var brush = WpfIrisRenderer.CreateFillBrush("#336699", 0.5, new Rect(0, 0, 10, 10));
                var solid = Assert.IsType<SolidColorBrush>(brush);
                Assert.Equal(GeometryHelper.ParseHexColor("#336699", 0.5), solid.Color);
            });
        }

        // ------------------------------------------------------------------
        // SVG
        // ------------------------------------------------------------------

        [Fact]
        public void TestSolidDesignsWriteNoGradientDefinitions()
        {
            string svg = SvgExporter.GeneratePairSvg(new IrisParameters());
            Assert.DoesNotContain("_fill", svg);
            Assert.DoesNotContain("userSpaceOnUse", svg);
        }

        [Fact]
        public void TestSvgGradientsMatchTheRendererPlacement()
        {
            var p = Bare();
            p.SizeMm = 10;
            p.PupilSizeRatio = 0.5;
            p.PupilOpacity = 1;
            p.PupilColor = RedToBlue;
            p.LimbalRingThickness = 0.2;
            p.LimbalRingOpacity = 1;
            p.LimbalRingColor = RadialRedToBlue;

            var doc = XDocument.Parse(SvgExporter.GeneratePairSvg(p));
            XNamespace ns = "http://www.w3.org/2000/svg";
            var grads = doc.Descendants().Where(e => e.Name == ns + "linearGradient" || e.Name == ns + "radialGradient").ToList();

            // Left pupil: 0° runs from the pupil's left edge to its right edge, through the center
            var leftPupil = grads.Single(g => (string?)g.Attribute("id") == "left_eye_pupil_fill");
            var leftEllipse = doc.Descendants(ns + "g").Single(g => (string?)g.Attribute("id") == "left_eye_pupil").Element(ns + "ellipse")!;
            double cx = double.Parse((string)leftEllipse.Attribute("cx")!, System.Globalization.CultureInfo.InvariantCulture);
            double prx = double.Parse((string)leftEllipse.Attribute("rx")!, System.Globalization.CultureInfo.InvariantCulture);
            double X(XElement g, string a) => double.Parse((string)g.Attribute(a)!, System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal("userSpaceOnUse", (string?)leftPupil.Attribute("gradientUnits"));
            Assert.Equal(cx - prx, X(leftPupil, "x1"), 2);
            Assert.Equal(cx + prx, X(leftPupil, "x2"), 2);
            Assert.Equal("url(#left_eye_pupil_fill)", (string?)doc.Descendants(ns + "g").Single(g => (string?)g.Attribute("id") == "left_eye_pupil").Attribute("fill"));

            // Right pupil is mirrored: runs right to left
            var rightPupil = grads.Single(g => (string?)g.Attribute("id") == "right_eye_pupil_fill");
            Assert.True(X(rightPupil, "x1") > X(rightPupil, "x2"));

            // Limbal ring: radial over the iris, stops moved into the ring band (80 % .. 100 %)
            var ring = grads.Single(g => (string?)g.Attribute("id") == "left_eye_limbal_fill");
            Assert.Equal(ns + "radialGradient", ring.Name);
            Assert.Equal(new[] { "80%", "100%" }, ring.Elements(ns + "stop").Select(s => (string)s.Attribute("offset")!).ToArray());
            Assert.Contains("scale(5.000000 5.000000)", (string)ring.Attribute("gradientTransform")!);
        }

        [Fact]
        public void TestSvgWithEveryGradientIsWellFormedWithUniqueIds()
        {
            var p = WithGradients();
            p.Highlights[1].Color = RedToBlue;
            p.Symbols.Add(new DecorativeSymbolItem { Type = SymbolType.Moon, Count = 2, Enabled = true, Color = RedToBlue, Rotation = 30 });
            string svg = SvgExporter.GeneratePairSvg(p, includePrintGuides: true);
            Assert.True(SvgValidator.IsWellFormed(svg, out string error), error);

            var ids = Regex.Matches(svg, @"\bid=""([^""]+_fill)""").Select(m => m.Groups[1].Value).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
            // 3 + 2 symbol copies, 2 highlights, pupil, ring, rays, lens, glitter — per eye
            Assert.Equal(2 * (5 + 2 + 5), ids.Count);
            foreach (var id in ids) Assert.Contains($"url(#{id})", svg);

            // The app's #AARRGGBB becomes #RRGGBB + stop-opacity (SVG would read 8 digits as RRGGBBAA)
            Assert.Contains(@"stop-color=""#FFFFFF"" stop-opacity=""0.502""", svg);
            Assert.DoesNotContain("#80FFFFFF", svg);
            // The odd-eye side uses its own ray gradient
            Assert.Contains(@"stop-color=""#E65100""", svg);
        }

        // ------------------------------------------------------------------
        // Color box UI
        // ------------------------------------------------------------------

        [Fact]
        public void TestColorBoxSwitchesBetweenSolidAndGradient()
        {
            RunOnSta(() =>
            {
                var box = new ColorPickerBox { AllowGradient = true, SelectedColorHex = "#0D47A1" };
                var mode = (ComboBox)box.FindName("ModeCombo");
                var editor = (FrameworkElement)box.FindName("GradientEditor");
                var stops = (StackPanel)box.FindName("StopsPanel");
                Assert.Equal(0, mode.SelectedIndex);
                Assert.Equal(Visibility.Collapsed, editor.Visibility);

                mode.SelectedIndex = 1;
                var fill = ColorFill.Parse(box.SelectedColorHex);
                Assert.Equal(ColorFillKind.Linear, fill.Kind);
                Assert.Equal("#0D47A1", fill.Stops[0].Color);
                Assert.Equal(Visibility.Visible, editor.Visibility);
                Assert.Equal(2, stops.Children.Count);

                // Add a color, switch to radial
                var add = (Button)box.FindName("AddStopBtn");
                add.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Assert.Equal(3, ColorFill.Parse(box.SelectedColorHex).Stops.Count);
                ((ComboBox)box.FindName("KindCombo")).SelectedIndex = 1;
                Assert.Equal(ColorFillKind.Radial, ColorFill.Parse(box.SelectedColorHex).Kind);

                // Back to one color: the first color
                mode.SelectedIndex = 0;
                Assert.Equal("#0D47A1", box.SelectedColorHex);
                Assert.Equal(Visibility.Collapsed, editor.Visibility);
            });
        }

        [Fact]
        public void TestColorBoxMakesAConicGradient()
        {
            RunOnSta(() =>
            {
                var box = new ColorPickerBox { AllowGradient = true, SelectedColorHex = "#FF80AB" };
                ((ComboBox)box.FindName("ModeCombo")).SelectedIndex = 1;
                var kind = (ComboBox)box.FindName("KindCombo");
                kind.SelectedIndex = 2;

                var fill = ColorFill.Parse(box.SelectedColorHex);
                Assert.Equal(ColorFillKind.Conic, fill.Kind);
                Assert.Equal(ColorFill.ConicDefaultAngle, fill.Angle);
                Assert.Equal(Visibility.Visible, ((FrameworkElement)box.FindName("AnglePanel")).Visibility);
                Assert.Equal(Visibility.Visible, ((FrameworkElement)box.FindName("ConicHint")).Visibility);
                Assert.Equal("開始:", ((TextBlock)box.FindName("AngleLabel")).Text);

                // Shown again from outside: the conic choice stays selected; back to linear restores the linear default angle
                box.SelectedColorHex = "conic-gradient(from 45deg, #FF0000 0%, #0000FF 100%)";
                Assert.Equal(2, kind.SelectedIndex);
                Assert.Equal(45, ((Slider)box.FindName("AngleSlider")).Value);
                kind.SelectedIndex = 0;
                fill = ColorFill.Parse(box.SelectedColorHex);
                Assert.Equal(ColorFillKind.Linear, fill.Kind);
                Assert.Equal(ColorFill.DefaultAngle, fill.Angle);
            });
        }

        [Fact]
        public void TestColorBoxShowsAGradientSetFromOutside()
        {
            RunOnSta(() =>
            {
                var box = new ColorPickerBox { AllowGradient = true };
                box.SelectedColorHex = "linear-gradient(30deg, #FF0000 0%, #00FF00 50%, #0000FF 100%)";
                Assert.Equal(1, ((ComboBox)box.FindName("ModeCombo")).SelectedIndex);
                Assert.Equal(3, ((StackPanel)box.FindName("StopsPanel")).Children.Count);
                Assert.Equal(30, ((Slider)box.FindName("AngleSlider")).Value);
                // Loading it did not rewrite it
                Assert.Equal("linear-gradient(30deg, #FF0000 0%, #00FF00 50%, #0000FF 100%)", box.SelectedColorHex);

                // A solid-only box shows the first color and leaves the value alone
                var solidOnly = new ColorPickerBox { SelectedColorHex = RedToBlue };
                Assert.Equal("#FF0000", ((TextBox)solidOnly.FindName("HexTextBox")).Text);
                Assert.Equal(RedToBlue, solidOnly.SelectedColorHex);
                Assert.Equal(Visibility.Collapsed, ((FrameworkElement)solidOnly.FindName("ModeCombo")).Visibility);
            });
        }
    }
}
