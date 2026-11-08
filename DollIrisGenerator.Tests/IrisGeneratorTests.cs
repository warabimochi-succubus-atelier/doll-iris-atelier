using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Xml.Linq;
using DollIrisGenerator.Controls;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using DollIrisGenerator.ViewModels;
using Xunit;

namespace DollIrisGenerator.Tests
{
    public class IrisGeneratorTests
    {
        /// <summary>
        /// Runs WPF-dependent test code on an STA thread and rethrows any failure on the test thread,
        /// so a failed Assert is reported against the test instead of crashing the test host.
        /// </summary>
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

        [Fact]
        public void TestJsonRoundTripPreservesAllParameters()
        {
            var original = new IrisParameters
            {
                Name = "テスト虹彩デザイン",
                SizeMm = 11.5,
                Shape = IrisShape.VerticalOval,
                AspectRatio = 1.18,
                TiltAngle = 12.0,
                Seed = 98765,
                BaseColor = "#3366CC",
                InnerColor = "#99CCFF",
                OuterColor = "#002266",
                BottomHighlightColor = "#E6F0FF",
                InnerGradientRadius = 0.48,
                OuterGradientRadius = 0.92,
                BottomHighlightIntensity = 0.55,
                LimbalRingThickness = 0.09,
                LimbalRingColor = "#0A1020",
                LimbalRingOpacity = 0.90,
                LimbalRingSoftness = 0.25,
                PupilShape = PupilShape.CatSlit,
                PupilSizeRatio = 0.35,
                PupilColor = "#050810",
                PupilOpacity = 0.98,
                PupilSoftness = 0.06,
                RayCount = 56,
                RayThickness = 1.4,
                RayLengthRatio = 0.72,
                RayIntensity = 0.80,
                RayRandomness = 0.50,
                RayColor = "#88DDFF",
                RayInnerRadiusRatio = 0.30,
                LensPatternEnabled = true,
                LensRingCount = 8,
                LensRingIntensity = 0.60,
                LensRingWidth = 1.1,
                LensRingColor = "#FFFFFF",
                LensRefractionGlow = 0.40,
                IsOddEye = true,
                OddEyeBaseColor = "#FFB300",
                OddEyeInnerColor = "#FFE082",
                OddEyeOuterColor = "#E65100",
                OddEyeRayColor = "#FFF9C4",
                ParticleCount = 80,
                ParticleMinSize = 1.2,
                ParticleMaxSize = 3.0,
                ParticleOpacity = 0.90,
                ParticleColor = "#FFFFFF",
                ParticleRadiusRatio = 0.82,
                MirrorHighlightsWithIris = true
            };

            original.Highlights.Add(new HighlightItem
            {
                Shape = HighlightShape.Star4,
                PositionX = -0.3,
                PositionY = -0.3,
                Size = 0.2,
                Color = "#FFFFFF",
                Opacity = 0.95
            });

            original.Symbols.Add(new DecorativeSymbolItem
            {
                Type = SymbolType.Heart,
                Count = 2,
                Size = 0.15,
                PositionX = 0.2,
                PositionY = 0.2,
                Color = "#FF80AB",
                Opacity = 0.85,
                Enabled = true
            });

            string json = JsonSerializer.Serialize(original, new JsonSerializerOptions { WriteIndented = true });
            Assert.False(string.IsNullOrWhiteSpace(json));

            var restored = JsonSerializer.Deserialize<IrisParameters>(json);
            Assert.NotNull(restored);

            Assert.Equal(original.Name, restored.Name);
            Assert.Equal(original.SizeMm, restored.SizeMm);
            Assert.Equal(original.Shape, restored.Shape);
            Assert.Equal(original.AspectRatio, restored.AspectRatio);
            Assert.Equal(original.TiltAngle, restored.TiltAngle);
            Assert.Equal(original.Seed, restored.Seed);
            Assert.Equal(original.BaseColor, restored.BaseColor);
            Assert.Equal(original.LensPatternEnabled, restored.LensPatternEnabled);
            Assert.Equal(original.LensRingCount, restored.LensRingCount);
            Assert.Equal(original.IsOddEye, restored.IsOddEye);
            Assert.Equal(original.OddEyeBaseColor, restored.OddEyeBaseColor);
            Assert.Equal(original.Highlights.Count, restored.Highlights.Count);
            Assert.Equal(original.Symbols.Count, restored.Symbols.Count);
        }

        [Fact]
        public void TestEllipticalIrisTiltAngleInSvg()
        {
            var p = new IrisParameters
            {
                Shape = IrisShape.VerticalOval,
                AspectRatio = 1.25,
                TiltAngle = 15.0
            };

            string svg = SvgExporter.GeneratePairSvg(p);
            // Must contain tilt rotation transform
            Assert.Contains("rotate(15.0", svg);
            Assert.Contains("rotate(-15.0", svg); // Right eye mirrored tilt
        }

        [Fact]
        public void TestLensPatternGeneratesOpticalRings()
        {
            var p = new IrisParameters
            {
                LensPatternEnabled = true,
                LensRingCount = 10,
                LensRingIntensity = 0.70
            };

            string svg = SvgExporter.GenerateSingleEyeSvg(p, isRightEye: false);
            Assert.Contains("lens_rings", svg);
            Assert.Contains("opacity=\"0.70\"", svg);
        }

        [Fact]
        public void TestOddEyeTemplatesApplyDistinctColors()
        {
            Assert.NotEmpty(OddEyeTemplate.Presets);
            var template = OddEyeTemplate.Presets[0]; // Sapphire & Gold

            var p = new IrisParameters();
            var vm = new MainViewModel();
            vm.CurrentIris = p;
            vm.ApplyOddEyeTemplate(template);

            Assert.True(p.IsOddEye);
            Assert.Equal(template.LeftBaseColor, p.BaseColor);
            Assert.Equal(template.RightBaseColor, p.OddEyeBaseColor);

            string svg = SvgExporter.GeneratePairSvg(p);
            Assert.Contains(template.LeftBaseColor, svg);
            Assert.Contains(template.RightBaseColor, svg);
        }

        [Fact]
        public void TestSharedColorPresetsExportAndImport()
        {
            string tempPalette = Path.Combine(Path.GetTempPath(), $"palette_{Guid.NewGuid():N}.json");
            try
            {
                var presets = ColorPresets.All;
                ColorPresets.ExportPalettesToFile(tempPalette, presets);

                Assert.True(File.Exists(tempPalette));
                var imported = ColorPresets.ImportPalettesFromFile(tempPalette);
                Assert.NotNull(imported);
                Assert.Equal(presets.Count, imported.Count);
                Assert.Equal(presets[0].Name, imported[0].Name);
            }
            finally
            {
                if (File.Exists(tempPalette)) File.Delete(tempPalette);
            }
        }

        [Fact]
        public void TestPdfTemplatePresetsApplyProperSheetConfigurations()
        {
            Assert.True(PdfTemplatePreset.All.Count >= 5);

            var template1_3 = PdfTemplatePreset.All.Find(t => t.Type == PdfTemplateType.Scale1_3_Doll);
            Assert.NotNull(template1_3);

            var sheetConfig = new PrintSheetConfig();
            template1_3.ApplyTo(sheetConfig);

            Assert.Equal(4, sheetConfig.ManualColumns);
            Assert.Equal(8, sheetConfig.ManualRows);
            Assert.Equal(18.0, sheetConfig.PageMarginTopMm);
        }

        [Theory]
        [InlineData(PdfTemplateType.Scale1_3_Doll, 12.0)] // largest iris named in each template
        [InlineData(PdfTemplateType.Scale1_4_Doll, 10.0)]
        [InlineData(PdfTemplateType.Scale1_6_Doll, 7.0)]
        [InlineData(PdfTemplateType.CirclePunchGrid, 12.0)]
        public void TestManualGridTemplatesFitAFullPageAtTheTopOfTheirSizeRange(PdfTemplateType type, double sizeMm)
        {
            var config = new PrintSheetConfig();
            PdfTemplatePreset.All.Find(t => t.Type == type)!.ApplyTo(config);
            Assert.False(config.AutoCalculateGrid);

            int fullPage = config.ManualColumns * config.ManualRows;
            var layout = new SheetLayout(new ObservableCollection<SheetItem> { new(new IrisParameters { SizeMm = sizeMm }, fullPage) }, config);
            Assert.Equal(1, layout.PageCount);
            Assert.True(layout.FitsOnPage, $"{type} at {sizeMm} mm: {layout.GridWidthMm:F1} x {layout.GridHeightMm:F1} mm");
        }

        [Theory]
        [InlineData(9.0, 6, 12)]
        [InlineData(10.0, 6, 11)]
        [InlineData(11.0, 6, 11)]
        [InlineData(12.0, 5, 10)]
        public void TestDefaultAutoLayoutCapacityForQuickSelectSizes(double sizeMm, int columns, int rows)
        {
            var layout = new SheetLayout(new ObservableCollection<SheetItem> { new(new IrisParameters { SizeMm = sizeMm }, 1) }, new PrintSheetConfig());
            Assert.Equal(columns, layout.Columns);
            Assert.Equal(rows, layout.Rows);

            var full = new SheetLayout(new ObservableCollection<SheetItem> { new(new IrisParameters { SizeMm = sizeMm }, columns * rows) }, new PrintSheetConfig());
            Assert.Equal(1, full.PageCount);
            Assert.True(full.FitsOnPage);
        }

        [Fact]
        public void TestIrisSizeDefaultsToTenMillimetresAndClampsToFourToTwenty()
        {
            Assert.Equal(10.0, new IrisParameters().SizeMm);
            Assert.Equal(20.0, new IrisParameters { SizeMm = 24.0 }.SizeMm);
            Assert.Equal(4.0, new IrisParameters { SizeMm = 2.0 }.SizeMm);
            Assert.Equal(12.3, new IrisParameters { SizeMm = 12.3 }.SizeMm);

            // Eye-sized values saved by older versions are pulled into the iris range on load
            var loaded = JsonSerializer.Deserialize<IrisParameters>("{\"SizeMm\": 36.0}")!;
            Assert.Equal(IrisParameters.MaxSizeMm, loaded.SizeMm);
        }

        [Fact]
        public void TestSizeAdjustmentNoteOnlyWhenTheSavedSizeWasOutOfRange()
        {
            string? note = DesignFileWorkflow.DescribeSizeAdjustment("{\"SizeMm\": 24.0}", 20.0);
            Assert.NotNull(note);
            Assert.Contains("24.0mm", note);
            Assert.Contains("20.0mm", note);

            Assert.Null(DesignFileWorkflow.DescribeSizeAdjustment("{\"SizeMm\": 10.5}", 10.5));
            Assert.Null(DesignFileWorkflow.DescribeSizeAdjustment("{\"Name\": \"x\"}", 10.0));
            Assert.Null(DesignFileWorkflow.DescribeSizeAdjustment("[1, 2]", 10.0));
        }

        [Theory]
        [InlineData(9.0, "9.0", true)]
        [InlineData(10.0, "10.0", true)]
        [InlineData(11.04, "11.0", true)]  // slider steps are 0.1 mm; tiny float error still matches
        [InlineData(11.1, "11.0", false)]
        [InlineData(12.0, "11.0", false)]
        [InlineData(12.0, "12.0", true)]
        public void TestQuickSizeButtonsShowTheCurrentIrisSize(double sizeMm, string buttonSize, bool expectedChecked)
        {
            var converter = new DollIrisGenerator.Converters.SizeMatchesConverter();
            var culture = System.Globalization.CultureInfo.GetCultureInfo("de-DE"); // decimal comma: parameter must still parse as 11.0
            Assert.Equal(expectedChecked, converter.Convert(sizeMm, typeof(bool), buttonSize, culture));
            if (expectedChecked)
            {
                Assert.Equal(double.Parse(buttonSize, System.Globalization.CultureInfo.InvariantCulture),
                    converter.ConvertBack(true, typeof(double), buttonSize, culture));
            }
        }

        [Fact]
        public void TestHistoryManagementAndUndoRedo()
        {
            var vm = new MainViewModel();
            string originalName = vm.CurrentIris.Name;

            vm.CurrentIris.Name = "変更後1";
            vm.RandomizeSeedCommand.Execute(null);

            Assert.True(vm.CanUndo);
            vm.UndoCommand.Execute(null);
            Assert.True(vm.CanRedo);
            vm.RedoCommand.Execute(null);

            Assert.NotEmpty(vm.HistoryItems);
        }

        [Fact]
        public void TestFavoritesManagementSaveAndRestore()
        {
            // A temporary store: adding a favorite auto-saves, and must never touch the user's real favorites
            string favoritesPath = Path.Combine(Path.GetTempPath(), $"favorites_{Guid.NewGuid():N}.json");
            try
            {
                var vm = new MainViewModel(new FakeDialogService(), new FavoritesStore(favoritesPath));
                vm.CurrentIris.Name = "特選サファイア";
                vm.CurrentIris.SizeMm = 20.0;
                vm.AddFavoriteCommand.Execute(null);

                Assert.NotEmpty(vm.Favorites);
                var fav = vm.Favorites[0];
                Assert.Equal("特選サファイア", fav.Name);

                // Change current iris
                vm.CurrentIris.Name = "別のデザイン";
                vm.CurrentIris.SizeMm = 14.0;

                // Restore from favorite
                vm.LoadFavoriteCommand.Execute(fav);
                Assert.Equal("特選サファイア", vm.CurrentIris.Name);
                Assert.Equal(20.0, vm.CurrentIris.SizeMm);
            }
            finally
            {
                File.Delete(favoritesPath);
            }
        }

        [Fact]
        public void TestSakuraPetalSymbolRendersInSvgAndGeometry()
        {
            var p = new IrisParameters { SizeMm = 20.0 };
            p.Symbols.Clear();
            p.Symbols.Add(new DecorativeSymbolItem
            {
                Type = SymbolType.SakuraPetal,
                Count = 3,
                Size = 0.20,
                Color = "#FFB2D0",
                Opacity = 0.90,
                Enabled = true
            });

            string svg = SvgExporter.GenerateSingleEyeSvg(p, isRightEye: false);
            Assert.Contains("symbol_SakuraPetal", svg);
            Assert.Contains("#FFB2D0", svg);

            // Test geometry generation on STA thread
            RunOnSta(() =>
            {
                var geom = WpfGeometry.CreateSakuraPetalGeometry(50.0);
                Assert.NotNull(geom);
                Assert.True(geom.Figures.Count > 0);
                Assert.True(geom.Figures[0].IsClosed);
            });
        }

        [Fact]
        public void TestDeterministicSvgGenerationGuaranteesIdenticalOutput()
        {
            var p1 = new IrisParameters { Seed = 54321, SizeMm = 20.0, BaseColor = "#2979FF" };
            var p2 = new IrisParameters { Seed = 54321, SizeMm = 20.0, BaseColor = "#2979FF" };

            string svg1 = SvgExporter.GeneratePairSvg(p1);
            string svg2 = SvgExporter.GeneratePairSvg(p2);

            Assert.Equal(svg1, svg2);
        }

        [Fact]
        public void TestPdfExportProducesValidA4Document()
        {
            RunOnSta(() =>
            {
                string tempPdf = Path.Combine(Path.GetTempPath(), $"test_doll_iris_{Guid.NewGuid():N}.pdf");
                try
                {
                    var p1 = new IrisParameters { Name = "アクア", SizeMm = 20.0, Seed = 12345 };
                    var p2 = new IrisParameters { Name = "オッドアイ", SizeMm = 18.0, IsOddEye = true, OddEyeBaseColor = "#FFB300" };

                    var items = new ObservableCollection<SheetItem>
                    {
                        new(p1, 2),
                        new(p2, 1)
                    };

                    var config = new PrintSheetConfig
                    {
                        ShowCutLines = true,
                        ShowCenterMarks = true,
                        ShowTrimGuides = true,
                        ShowLabels = true
                    };

                    PdfExporter.ExportA4SheetToPdf(tempPdf, items, config);

                    Assert.True(File.Exists(tempPdf));
                    var fileInfo = new FileInfo(tempPdf);
                    Assert.True(fileInfo.Length > 1000);

                    byte[] header = new byte[4];
                    using (var fs = File.OpenRead(tempPdf))
                    {
                        fs.ReadExactly(header);
                    }
                    Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(header));
                }
                finally
                {
                    if (File.Exists(tempPdf)) File.Delete(tempPdf);
                }
            });

        }

        [Fact]
        public void TestHighResolutionRasterExportProducesValidPngAt1200Dpi()
        {
            RunOnSta(() =>
            {
                string tempPng = Path.Combine(Path.GetTempPath(), $"test_iris_1200dpi_{Guid.NewGuid():N}.png");
                try
                {
                    var p = new IrisParameters { SizeMm = 18.0, Seed = 777, LensPatternEnabled = true };
                    var bmp = RasterExporter.RenderPairToBitmap(p, dpi: 1200.0, pairSpacingMm: 4.0);

                    Assert.NotNull(bmp);
                    Assert.True(bmp.PixelWidth > 1500);
                    Assert.True(bmp.PixelHeight > 900);

                    RasterExporter.SaveBitmapToPng(bmp, tempPng);
                    Assert.True(File.Exists(tempPng));

                    byte[] pngMagic = new byte[8];
                    using (var fs = File.OpenRead(tempPng))
                    {
                        fs.ReadExactly(pngMagic);
                    }
                    Assert.Equal(0x89, pngMagic[0]);
                    Assert.Equal(0x50, pngMagic[1]);
                    Assert.Equal(0x4E, pngMagic[2]);
                    Assert.Equal(0x47, pngMagic[3]);
                }
                finally
                {
                    if (File.Exists(tempPng)) File.Delete(tempPng);
                }
            });

        }

        [Fact]
        public void TestMillimeterPointConversionAccuracy()
        {
            double pt = PdfExporter.MmToPt(20.0);
            double backToMm = pt / (72.0 / 25.4);
            Assert.True(Math.Abs(backToMm - 20.0) < 0.0001);
        }

        [Fact]
        public void GenerateSampleOutputsForUserInspection()
        {
            RunOnSta(() =>
            {
                string outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\SampleOutputs");
                Directory.CreateDirectory(outDir);

                var blue = new IrisParameters { Name = "サファイアブルー", SizeMm = 10.0, Seed = 12345 };
                ColorPresets.ApplyPreset(blue, ColorPresets.All.Single(x => x.Name == "ベーシック: クリアブルー"));

                var galaxy = new IrisParameters { Name = "ギャラクシー", SizeMm = 10.0, Seed = 45678 };
                ColorPresets.ApplyPreset(galaxy, ColorPresets.All.Single(x => x.Name == "特殊: 銀河"));

                var sakura = new IrisParameters { Name = "サクラ (桜舞う春風)", SizeMm = 10.0, Seed = 56789 };
                ColorPresets.ApplyPreset(sakura, ColorPresets.All.Single(x => x.Name == "ベーシック: サクラ")); // with the SakuraPetal symbol

                // Export SVGs
                File.WriteAllText(Path.Combine(outDir, "Sample_SapphireBlue_10.0mm.svg"), SvgExporter.GeneratePairSvg(blue, includePrintGuides: true));
                File.WriteAllText(Path.Combine(outDir, "Sample_Galaxy_10.0mm.svg"), SvgExporter.GeneratePairSvg(galaxy, includePrintGuides: true));
                File.WriteAllText(Path.Combine(outDir, "Sample_Sakura_10.0mm.svg"), SvgExporter.GeneratePairSvg(sakura, includePrintGuides: true));

                // Export Sample PDF Sheet
                var sheetItems = new ObservableCollection<SheetItem>
                {
                    new(blue, 4),
                    new(galaxy, 4),
                    new(sakura, 4)
                };
                var sheetConfig = new PrintSheetConfig();
                PdfExporter.ExportA4SheetToPdf(Path.Combine(outDir, "Sample_A4_Imposition_Sheet.pdf"), sheetItems, sheetConfig);
            });

        }

        [Fact]
        public void TestPdfExportHasUniqueObjectIdsAndValidStructure()
        {
            RunOnSta(() =>
            {
                string tempPdf = Path.Combine(Path.GetTempPath(), $"test_pdf_structure_{Guid.NewGuid():N}.pdf");
                try
                {
                    var p = new IrisParameters { Name = "UniqueObjTest", SizeMm = 20.0 };
                    var items = new ObservableCollection<SheetItem> { new(p, 2) };
                    var config = new PrintSheetConfig();

                    PdfExporter.ExportA4SheetToPdf(tempPdf, items, config);

                    string pdfText = File.ReadAllText(tempPdf);
                    var matches = System.Text.RegularExpressions.Regex.Matches(pdfText, @"(\d+)\s+0\s+obj");
                    var objIds = new System.Collections.Generic.List<int>();
                    var seen = new System.Collections.Generic.HashSet<int>();

                    foreach (System.Text.RegularExpressions.Match m in matches)
                    {
                        int id = int.Parse(m.Groups[1].Value);
                        Assert.DoesNotContain(id, seen); // Assert every object ID is unique (no collisions)
                        seen.Add(id);
                        objIds.Add(id);
                    }

                    // Object 1 must be Font, Object 2 must be Pages root
                    Assert.Contains("1 0 obj\r\n<< /Type /Font", pdfText);
                    Assert.Contains("2 0 obj\r\n<<\r\n  /Type /Pages", pdfText);
                }
                finally
                {
                    if (File.Exists(tempPdf)) File.Delete(tempPdf);
                }
            });

        }

        /// <summary>
        /// Eye images are placed at their own size (pixels at 1200 DPI), not stretched over the canvas in mm:
        /// a 10 mm eye has a 12 mm canvas = 566.9 px, rounded up to 567 px = 34.020 pt (12 mm would be 34.016 pt).
        /// </summary>
        [Fact]
        public void TestPdfPlacesEyeImagesAtTheirRealSize()
        {
            string tempPdf = Path.Combine(Path.GetTempPath(), $"test_pdf_image_size_{Guid.NewGuid():N}.pdf");
            try
            {
                var items = new ObservableCollection<SheetItem> { new(new IrisParameters { Name = "Size", SizeMm = 10.0 }, 1) };
                PdfExporter.ExportA4SheetToPdf(tempPdf, items, new PrintSheetConfig());

                string pdfText = File.ReadAllText(tempPdf);
                Assert.Contains("/Width 567 /Height 567", pdfText);
                var cm = System.Text.RegularExpressions.Regex.Matches(pdfText, @"([\d.]+) 0 0 ([\d.]+) ([\d.]+) ([\d.]+) cm\r?\n/Im\d+ Do");
                Assert.Equal(2, cm.Count);
                foreach (System.Text.RegularExpressions.Match m in cm)
                {
                    Assert.Equal("34.020", m.Groups[1].Value);
                    Assert.Equal("34.020", m.Groups[2].Value);
                }
            }
            finally
            {
                if (File.Exists(tempPdf)) File.Delete(tempPdf);
            }
        }

        [Fact]
        public void TestPdfExportPreventsInfiniteLoopOnExtremeOrZeroGrid()
        {
            RunOnSta(() =>
            {
                string tempPdf = Path.Combine(Path.GetTempPath(), $"test_pdf_loop_{Guid.NewGuid():N}.pdf");
                try
                {
                    var p = new IrisParameters { Name = "LoopTest", SizeMm = 20.0 };
                    var items = new ObservableCollection<SheetItem> { new(p, 1) };
                    var config = new PrintSheetConfig
                    {
                        AutoCalculateGrid = false,
                        ManualColumns = 0,
                        ManualRows = 0
                    };

                    // Should complete quickly and safely without hanging in infinite loop
                    PdfExporter.ExportA4SheetToPdf(tempPdf, items, config);
                    Assert.True(File.Exists(tempPdf));
                }
                finally
                {
                    if (File.Exists(tempPdf)) File.Delete(tempPdf);
                }
            });

        }

        [Fact]
        public void TestIrisParametersNullCoalescingResilience()
        {
            var p = new IrisParameters();
            p.Name = null!;
            Assert.NotNull(p.Name);

            p.Highlights = null!;
            Assert.NotNull(p.Highlights);

            p.Symbols = null!;
            Assert.NotNull(p.Symbols);

            // Adding item should succeed without NullReferenceException
            p.Highlights.Add(new HighlightItem());
            p.Symbols.Add(new DecorativeSymbolItem());
            Assert.Single(p.Highlights);
            Assert.Single(p.Symbols);
        }

        [Fact]
        public void TestColorSanitizationPreventsAttributeInjection()
        {
            // Valid colors
            Assert.Equal("#2979FF", GeometryHelper.SanitizeColorHex("#2979FF"));
            Assert.Equal("#FFF", GeometryHelper.SanitizeColorHex("#FFF"));
            Assert.Equal("#12345678", GeometryHelper.SanitizeColorHex("#12345678"));

            // Malicious or invalid injection attempts
            string payload1 = @"red"" onload=""alert(1)";
            string payload2 = @"#123; </script><script>";
            string payload3 = @"javascript:void(0)";

            Assert.Equal("#000000", GeometryHelper.SanitizeColorHex(payload1));
            Assert.Equal("#000000", GeometryHelper.SanitizeColorHex(payload2));
            Assert.Equal("#000000", GeometryHelper.SanitizeColorHex(payload3));
            Assert.Equal("#000000", GeometryHelper.SanitizeColorHex(null));
        }

        [Fact]
        public void TestWindowSettingsSaveLoadAndSanitize()
        {
            string tempSettings = Path.Combine(Path.GetTempPath(), $"window_settings_{Guid.NewGuid():N}.json");
            try
            {
                RunOnSta(() =>
                {
                    var window = new System.Windows.Window
                    {
                        Width = 1500,
                        Height = 920,
                        WindowState = System.Windows.WindowState.Normal
                    };

                    WindowSettingsService.SaveSettings(window, tempSettings);
                    Assert.True(File.Exists(tempSettings));

                    var loaded = WindowSettingsService.LoadSettings(tempSettings);
                    Assert.Equal(1500, loaded.Width);
                    Assert.Equal(920, loaded.Height);
                    Assert.Equal(System.Windows.WindowState.Normal, loaded.WindowState);

                    // Test restoring onto a new window
                    var newWindow = new System.Windows.Window
                    {
                        MinWidth = 1100,
                        MinHeight = 700
                    };
                    WindowSettingsService.ApplySettings(newWindow, loaded);
                    Assert.Equal(1500, newWindow.Width);
                    Assert.Equal(920, newWindow.Height);
                });

            }
            finally
            {
                if (File.Exists(tempSettings)) File.Delete(tempSettings);
            }
        }

        [Fact]
        public void TestWindowSettingsSanitizesExtremeOrInvalidValues()
        {
            var invalidSettings = new WindowSettings
            {
                Width = -500,
                Height = double.NaN,
                WindowState = System.Windows.WindowState.Minimized
            };

            var sanitized = WindowSettingsService.Sanitize(invalidSettings);
            Assert.Equal(1440.0, sanitized.Width); // Fallback to standard width
            Assert.Equal(880.0, sanitized.Height);  // Fallback to standard height
            Assert.Equal(System.Windows.WindowState.Normal, sanitized.WindowState); // Never restore minimized

            var oversizedSettings = new WindowSettings
            {
                Width = 99999,
                Height = 88888
            };
            var sanitizedOversized = WindowSettingsService.Sanitize(oversizedSettings);
            Assert.True(sanitizedOversized.Width <= 8192.0);
            Assert.True(sanitizedOversized.Height <= 8192.0);
        }

        [Fact]
        public void TestRandomizeAttributesStrictlyPreservesSizeAndDimensions()
        {
            var iris = new IrisParameters
            {
                SizeMm = 12.5,
                Shape = IrisShape.VerticalOval,
                AspectRatio = 1.35,
                TiltAngle = -18.0,
                BaseColor = "#000000",
                Seed = 11111
            };

            for (int i = 0; i < 20; i++)
            {
                IrisRandomizer.RandomizeAttributes(iris);

                // Dimension and size attributes MUST remain exactly identical
                Assert.Equal(12.5, iris.SizeMm);
                Assert.Equal(IrisShape.VerticalOval, iris.Shape);
                Assert.Equal(1.35, iris.AspectRatio);
                Assert.Equal(-18.0, iris.TiltAngle);

                // Other attributes should have valid randomized values
                Assert.True(iris.Seed != 11111 || i > 0);
                Assert.StartsWith("#", iris.BaseColor);
                Assert.StartsWith("#", iris.InnerColor);
                Assert.StartsWith("#", iris.OuterColor);
                Assert.True(iris.InnerGradientRadius >= 0.1 && iris.InnerGradientRadius <= 1.0);
                Assert.True(iris.OuterGradientRadius >= 0.1 && iris.OuterGradientRadius <= 1.2);
                Assert.True(iris.RayCount >= 0 && iris.RayCount <= 180);
            }
        }

        [Fact]
        public void TestRandomizeAttributesCommandInViewModelPreservesSizeAndSupportsUndo()
        {
            var vm = new MainViewModel();
            vm.CurrentIris.SizeMm = 16.0;
            vm.CurrentIris.Shape = IrisShape.HorizontalOval;
            vm.CurrentIris.AspectRatio = 1.20;
            vm.CurrentIris.TiltAngle = 10.0;
            string originalBaseColor = vm.CurrentIris.BaseColor;

            Assert.True(vm.RandomizeAttributesCommand.CanExecute(null));
            vm.RandomizeAttributesCommand.Execute(null);

            // Verify size & dimensions preserved
            Assert.Equal(16.0, vm.CurrentIris.SizeMm);
            Assert.Equal(IrisShape.HorizontalOval, vm.CurrentIris.Shape);
            Assert.Equal(1.20, vm.CurrentIris.AspectRatio);
            Assert.Equal(10.0, vm.CurrentIris.TiltAngle);

            // Verify undo restores previous state
            Assert.True(vm.CanUndo);
            vm.UndoCommand.Execute(null);
            Assert.Equal(originalBaseColor, vm.CurrentIris.BaseColor);
            Assert.Equal(16.0, vm.CurrentIris.SizeMm);
        }

        [Fact]
        public void TestApplicationIconFilesExistAndAreValid()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string projectDir = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\DollIrisGenerator"));

            string pngPath = Path.Combine(projectDir, "IrisGenerationApp.png");
            string icoPath = Path.Combine(projectDir, "app.ico");

            Assert.True(File.Exists(pngPath), "IrisGenerationApp.png should exist in project folder.");
            Assert.True(File.Exists(icoPath), "app.ico should exist in project folder.");

            var pngInfo = new FileInfo(pngPath);
            Assert.True(pngInfo.Length > 1000);

            var icoInfo = new FileInfo(icoPath);
            Assert.True(icoInfo.Length > 1000);
        }

        [Fact]
        public void TestPdfDoesNotReuseImageForDesignsDifferingOnlyInDetails()
        {
            RunOnSta(() =>
            {
                string tempPdf = Path.Combine(Path.GetTempPath(), $"test_doll_iris_{Guid.NewGuid():N}.pdf");
                try
                {
                    // Same seed, size, shape and colors; only the highlight size and pupil size differ
                    var p1 = new IrisParameters { Name = "A" };
                    var p2 = p1.Clone();
                    p2.Name = "A";
                    p2.Highlights[0].Size = 0.40;
                    p2.PupilSizeRatio = 0.50;

                    var items = new ObservableCollection<SheetItem> { new(p1, 1), new(p2, 1) };
                    PdfExporter.ExportA4SheetToPdf(tempPdf, items, new PrintSheetConfig());

                    string pdf = System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(tempPdf));
                    int imageCount = System.Text.RegularExpressions.Regex.Matches(pdf, "/Subtype /Image").Count;
                    Assert.Equal(4, imageCount); // L + R for each of the two different designs
                }
                finally
                {
                    if (File.Exists(tempPdf)) File.Delete(tempPdf);
                }
            });
        }

        [Fact]
        public void TestSheetLayoutSizesCellsFromLargestDesignSoMixedSizesNeverOverlap()
        {
            var small = new IrisParameters { SizeMm = 9.0 };
            var large = new IrisParameters { SizeMm = 12.0 };
            var config = new PrintSheetConfig();
            var layout = new SheetLayout(new ObservableCollection<SheetItem> { new(small, 1), new(large, 1) }, config);

            Assert.Equal(12.0, layout.CellEyeWidthMm, 3);
            Assert.True(layout.Columns >= 2);

            // Right edge of the small pair's right eye must not reach the large pair's left eye
            var slot0 = layout.GetEyeCentersMm(0);
            var slot1 = layout.GetEyeCentersMm(1);
            double slot0RightEdge = slot0.RightX + small.SizeMm / 2.0;
            double slot1LeftEdge = slot1.LeftX - large.SizeMm / 2.0;
            Assert.True(slot1LeftEdge > slot0RightEdge);

            // Left/right eyes of the large pair must not overlap each other
            Assert.True(slot1.RightX - slot1.LeftX >= large.SizeMm + config.PairSpacingXMm - 0.001);
        }

        [Fact]
        public void TestHighlightEditsRaiseNestedChangeNotificationEvenAfterClone()
        {
            var p = new IrisParameters().Clone(); // Clone goes through the JSON collection setters
            int nestedChanges = 0;
            p.PropertyChanged += (_, e) => { if (e.PropertyName == IrisParameters.NestedItemsChangedName) nestedChanges++; };

            p.Highlights[0].Size = 0.33;
            p.Symbols[0].Enabled = true;
            p.Highlights.Add(new HighlightItem());

            Assert.Equal(3, nestedChanges);
        }

        [Fact]
        public void TestSliderStyleEditsAreUndoableAsOneStep()
        {
            var vm = new MainViewModel();
            double original = vm.CurrentIris.PupilSizeRatio;

            // Simulates a slider drag: many small changes in a row
            vm.CurrentIris.PupilSizeRatio = 0.40;
            vm.CurrentIris.PupilSizeRatio = 0.45;
            vm.CurrentIris.PupilSizeRatio = 0.50;

            Assert.True(vm.CanUndo);
            vm.UndoCommand.Execute(null);
            Assert.Equal(original, vm.CurrentIris.PupilSizeRatio, 6);
            Assert.False(vm.CanUndo);

            vm.RedoCommand.Execute(null);
            Assert.Equal(0.50, vm.CurrentIris.PupilSizeRatio, 6);
        }

        [Fact]
        public void TestHighlightEditIsUndoable()
        {
            var vm = new MainViewModel();
            double original = vm.CurrentIris.Highlights[0].Size;

            vm.CurrentIris.Highlights[0].Size = 0.40;
            vm.UndoCommand.Execute(null);

            Assert.Equal(original, vm.CurrentIris.Highlights[0].Size, 6);
        }

        [Fact]
        public void TestPdfPaginatesExactlyAsSheetLayoutPredicts()
        {
            RunOnSta(() =>
            {
                string tempPdf = Path.Combine(Path.GetTempPath(), $"test_doll_iris_{Guid.NewGuid():N}.pdf");
                try
                {
                    var config = new PrintSheetConfig { AutoCalculateGrid = false, ManualColumns = 2, ManualRows = 2 };
                    var items = new ObservableCollection<SheetItem> { new(new IrisParameters { SizeMm = 16.0 }, 5) };

                    var layout = new SheetLayout(items, config);
                    Assert.Equal(4, layout.PairsPerPage);
                    Assert.Equal(2, layout.PageCount); // 5 pairs on a 2x2 grid

                    PdfExporter.ExportA4SheetToPdf(tempPdf, items, config);
                    string pdf = System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(tempPdf));
                    int pageObjects = System.Text.RegularExpressions.Regex.Matches(pdf, @"/Type /Page\r?\n").Count;
                    Assert.Equal(layout.PageCount, pageObjects);
                }
                finally
                {
                    if (File.Exists(tempPdf)) File.Delete(tempPdf);
                }
            });
        }

        [Fact]
        public void TestSheetLayoutHonorsManualGridAndMargins()
        {
            var config = new PrintSheetConfig
            {
                AutoCalculateGrid = false,
                ManualColumns = 3,
                ManualRows = 5,
                PageMarginLeftMm = 20.0,
                PageMarginTopMm = 25.0
            };
            var layout = new SheetLayout(new ObservableCollection<SheetItem> { new(new IrisParameters { SizeMm = 14.0 }, 1) }, config);

            Assert.Equal(3, layout.Columns);
            Assert.Equal(5, layout.Rows);
            var origin = layout.GetCellOriginMm(0);
            Assert.Equal(20.0, origin.X, 6);
            Assert.Equal(25.0, origin.Y, 6);

            // Slot 3 wraps to the second row, first column
            var slot3 = layout.GetCellOriginMm(3);
            Assert.Equal(20.0, slot3.X, 6);
            Assert.Equal(25.0 + layout.PairTotalHeightMm + config.RowSpacingMm, slot3.Y, 6);
        }

        [Fact]
        public void TestEmptySheetLayoutStillProducesOneDefaultPair()
        {
            var layout = new SheetLayout(new ObservableCollection<SheetItem>(), new PrintSheetConfig());
            Assert.Single(layout.Pairs);
            Assert.Equal(1, layout.PageCount);
        }

        [Fact]
        public void TestCraftedJsonCannotRequestUnboundedSymbolOrHighlightWork()
        {
            // A shared file asking for 2 billion symbol repeats and 1000 highlights
            var highlights = string.Join(",", Enumerable.Repeat("{\"Size\":0.1}", 1000));
            string json = "{\"Symbols\":[{\"Count\":2000000000,\"Size\":99,\"PositionX\":-1e9,\"Opacity\":7}],"
                        + "\"Highlights\":[" + highlights + "]}";

            var p = JsonSerializer.Deserialize<IrisParameters>(json)!;

            Assert.Equal(DecorativeSymbolItem.MaxCount, p.Symbols[0].Count);
            Assert.Equal(1.0, p.Symbols[0].Size);
            Assert.Equal(-1.5, p.Symbols[0].PositionX);
            Assert.Equal(1.0, p.Symbols[0].Opacity);
            Assert.Equal(IrisParameters.MaxNestedItems, p.Highlights.Count);
        }

        [Fact]
        public void TestSafeFileReaderRejectsOversizedFiles()
        {
            string tmp = Path.Combine(Path.GetTempPath(), $"test_doll_iris_{Guid.NewGuid():N}.json");
            try
            {
                File.WriteAllText(tmp, new string(' ', 2048));
                Assert.Throws<InvalidDataException>(() => SafeFileReader.ReadAllText(tmp, maxBytes: 1024));
                Assert.Equal(2048, SafeFileReader.ReadAllText(tmp, maxBytes: 4096).Length);
            }
            finally
            {
                if (File.Exists(tmp)) File.Delete(tmp);
            }
        }

        [Fact]
        public void TestSvgValidationRejectsDtdEntitiesAndNonSvg()
        {
            string xxe = "<!DOCTYPE svg [<!ENTITY x SYSTEM \"file:///c:/windows/win.ini\">]><svg>&x;</svg>";
            Assert.False(SvgValidator.IsWellFormed(xxe, out _));
            Assert.False(SvgValidator.IsWellFormed("<html></html>", out _));
            Assert.False(SvgValidator.IsWellFormed("<svg><g></svg>", out _));
            Assert.True(SvgValidator.IsWellFormed("<svg xmlns=\"http://www.w3.org/2000/svg\"/>", out _));
        }

        [Fact]
        public void TestPdfPrintsJapaneseLabelsAsImageAndAsciiLabelsAsText()
        {
            RunOnSta(() =>
            {
                string jaPdf = Path.Combine(Path.GetTempPath(), $"test_doll_iris_{Guid.NewGuid():N}.pdf");
                string enPdf = Path.Combine(Path.GetTempPath(), $"test_doll_iris_{Guid.NewGuid():N}.pdf");
                try
                {
                    var config = new PrintSheetConfig { ShowLabels = true };

                    PdfExporter.ExportA4SheetToPdf(jaPdf, new ObservableCollection<SheetItem> { new(new IrisParameters { Name = "サファイア" }, 2) }, config);
                    string ja = System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(jaPdf));
                    // 2 eye images + 1 label image (same label reused for both pairs)
                    Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(ja, "/Subtype /Image").Count);

                    PdfExporter.ExportA4SheetToPdf(enPdf, new ObservableCollection<SheetItem> { new(new IrisParameters { Name = "Sapphire" }, 1) }, config);
                    string en = System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(enPdf));
                    Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(en, "/Subtype /Image").Count);
                    Assert.Contains("(10.0mm - Sapphire) Tj", en);
                }
                finally
                {
                    if (File.Exists(jaPdf)) File.Delete(jaPdf);
                    if (File.Exists(enPdf)) File.Delete(enPdf);
                }
            });
        }

        private static (byte B, byte G, byte R, byte A) PixelAt(System.Windows.Media.Imaging.BitmapSource bmp, int x, int y)
        {
            var px = new byte[4];
            bmp.CopyPixels(new System.Windows.Int32Rect(x, y, 1, 1), px, 4, 0);
            return (px[0], px[1], px[2], px[3]);
        }

        [Fact]
        public void TestRasterExportsActuallyContainTheEyeAndMirrorTheRightEye()
        {
            RunOnSta(() =>
            {
                var p = new IrisParameters { SizeMm = 20.0 };

                // Single eye at print resolution (used for the PDF): the centre is the dark pupil, not white paper
                var left = RasterExporter.RenderSingleToBitmap(p, isRightEye: false, dpi: 1200.0);
                var right = RasterExporter.RenderSingleToBitmap(p, isRightEye: true, dpi: 1200.0);
                var c = PixelAt(left, left.PixelWidth / 2, left.PixelHeight / 2);
                Assert.True(c.R < 80 && c.G < 80 && c.B < 80, $"pupil centre should be dark, was {c.R},{c.G},{c.B}");

                // Right eye is the mirror image: the default highlights are asymmetric, so pixels must differ
                var lStride = left.PixelWidth * 4;
                var lPx = new byte[lStride * left.PixelHeight];
                var rPx = new byte[lStride * right.PixelHeight];
                left.CopyPixels(lPx, lStride, 0);
                right.CopyPixels(rPx, lStride, 0);
                Assert.False(lPx.AsSpan().SequenceEqual(rPx), "right eye must not be identical to the left eye");

                // Pair PNG: the left eye's centre is opaque (not an empty transparent canvas)
                var pair = RasterExporter.RenderPairToBitmap(p, dpi: 600.0, pairSpacingMm: 4.0);
                int leftCenterX = (int)Math.Round((2.0 + 10.0) * 600.0 / 25.4);
                var pc = PixelAt(pair, leftCenterX, pair.PixelHeight / 2);
                Assert.Equal(255, pc.A);
            });
        }

        [Fact]
        public void TestScaleMarkSitsBelowTheGridAndDropsVerticalBarWhenNoRoom()
        {
            // Default sheet: plenty of space under the grid -> full L (horizontal + vertical bar), centred
            var roomy = new SheetLayout(new ObservableCollection<SheetItem> { new(new IrisParameters { SizeMm = 20.0 }, 1) }, new PrintSheetConfig());
            var (x, y, vertical) = roomy.GetScaleMarkPosition();
            Assert.Equal(100.0, x, 6);                                   // (210 - 10) / 2
            Assert.Equal(297.0 - SheetLayout.ScaleMarkBottomOffsetMm, y, 6);
            Assert.True(vertical);
            double gridBottom = 15.0 + roomy.Rows * roomy.PairTotalHeightMm + (roomy.Rows - 1) * roomy.Config.RowSpacingMm;
            Assert.True(y - SheetLayout.ScaleMarkLengthMm > gridBottom, "vertical bar must stay below the last row");

            // Manual grid that runs to the bottom of the page -> horizontal bar only
            var crowded = new SheetLayout(new ObservableCollection<SheetItem> { new(new IrisParameters { SizeMm = 20.0 }, 1) },
                new PrintSheetConfig { AutoCalculateGrid = false, ManualColumns = 3, ManualRows = 10 });
            Assert.False(crowded.GetScaleMarkPosition().IncludeVertical);
        }

        [Fact]
        public void TestPdfScaleMarkBarsAreExactlyTenMillimetres()
        {
            RunOnSta(() =>
            {
                string onPdf = Path.Combine(Path.GetTempPath(), $"test_doll_iris_{Guid.NewGuid():N}.pdf");
                string offPdf = Path.Combine(Path.GetTempPath(), $"test_doll_iris_{Guid.NewGuid():N}.pdf");
                try
                {
                    var items = new ObservableCollection<SheetItem> { new(new IrisParameters { Name = "A" }, 1) };
                    PdfExporter.ExportA4SheetToPdf(onPdf, items, new PrintSheetConfig());
                    string on = System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(onPdf));

                    // Corner at x = 100 mm, 7 mm above the bottom edge (PDF y is measured from the bottom)
                    Assert.Contains("% 1 cm scale mark", on);
                    Assert.Contains(PdfExporter.FormatLineMm(100.0, 7.0, 110.0, 7.0), on); // horizontal 10 mm
                    Assert.Contains(PdfExporter.FormatLineMm(100.0, 7.0, 100.0, 17.0), on); // vertical 10 mm
                    Assert.Equal(28.346, PdfExporter.MmToPt(10.0), 3);                      // 10 mm in PDF points

                    PdfExporter.ExportA4SheetToPdf(offPdf, items, new PrintSheetConfig { ShowScaleMark = false });
                    Assert.DoesNotContain("% 1 cm scale mark", System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(offPdf)));
                }
                finally
                {
                    if (File.Exists(onPdf)) File.Delete(onPdf);
                    if (File.Exists(offPdf)) File.Delete(offPdf);
                }
            });
        }

        [Fact]
        public void TestEyeFootprintAccountsForTilt()
        {
            var oval = new IrisParameters { SizeMm = 20.0, Shape = IrisShape.HorizontalOval, AspectRatio = 1.6 }; // 32 x 20 mm
            Assert.Equal((32.0, 20.0), SheetLayout.GetEyeFootprintMm(oval));

            oval.TiltAngle = 45.0;
            var (w, h) = SheetLayout.GetEyeFootprintMm(oval);
            double expected = 2.0 * Math.Sqrt((16.0 * 16.0 + 10.0 * 10.0) / 2.0); // both axes at 45 degrees
            Assert.Equal(expected, w, 6);
            Assert.Equal(expected, h, 6);

            // A circle has the same footprint at any tilt
            var circle = new IrisParameters { SizeMm = 20.0, TiltAngle = 30.0 };
            Assert.Equal((20.0, 20.0), SheetLayout.GetEyeFootprintMm(circle));

            // Sheet cells hold the tilted footprint
            var layout = new SheetLayout(new ObservableCollection<SheetItem> { new(oval, 1) }, new PrintSheetConfig());
            Assert.Equal(expected, layout.CellEyeHeightMm, 6);
        }

        [Fact]
        public void TestTiltedOvalImageIsNotClipped()
        {
            RunOnSta(() =>
            {
                var p = new IrisParameters { SizeMm = 18.0, Shape = IrisShape.HorizontalOval, AspectRatio = 1.6, TiltAngle = 30.0 };
                foreach (bool right in new[] { false, true })
                {
                    var bmp = RasterExporter.RenderSingleToBitmap(p, isRightEye: right, dpi: 300.0);
                    int w = bmp.PixelWidth, h = bmp.PixelHeight, stride = w * 4;
                    var px = new byte[stride * h];
                    bmp.CopyPixels(px, stride, 0);

                    // The 1 mm padding around the eye must be blank paper on all four sides: nothing cut off
                    bool IsWhite(int x, int y) { int i = y * stride + x * 4; return px[i] > 245 && px[i + 1] > 245 && px[i + 2] > 245; }
                    for (int x = 0; x < w; x++) { Assert.True(IsWhite(x, 0), $"top edge touched at x={x}"); Assert.True(IsWhite(x, h - 1), $"bottom edge touched at x={x}"); }
                    for (int y = 0; y < h; y++) { Assert.True(IsWhite(0, y), $"left edge touched at y={y}"); Assert.True(IsWhite(w - 1, y), $"right edge touched at y={y}"); }
                }
            });
        }

        [Fact]
        public void TestPdfCutLineRotatesWithTiltedEye()
        {
            RunOnSta(() =>
            {
                string pdf = Path.Combine(Path.GetTempPath(), $"test_doll_iris_{Guid.NewGuid():N}.pdf");
                try
                {
                    var p = new IrisParameters { SizeMm = 18.0, Shape = IrisShape.HorizontalOval, AspectRatio = 1.6, TiltAngle = 30.0 };
                    PdfExporter.ExportA4SheetToPdf(pdf, new ObservableCollection<SheetItem> { new(p, 1) }, new PrintSheetConfig { ShowCutLines = true });
                    string text = System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(pdf));

                    // Left eye drawn 30 deg clockwise on screen -> PDF rotation -30 deg; right eye mirrored -> +30 deg
                    Assert.Contains("0.866025 -0.500000 0.500000 0.866025", text);
                    Assert.Contains("0.866025 0.500000 -0.500000 0.866025", text);
                }
                finally
                {
                    if (File.Exists(pdf)) File.Delete(pdf);
                }
            });
        }

        [Fact]
        public void TestPairAndSheetViewsKeepTheirOwnZoom()
        {
            var vm = new MainViewModel { PreviewMode = PreviewMode.Pair };
            vm.ZoomScale = 2.0;
            vm.PreviewMode = PreviewMode.A4Sheet;
            Assert.Equal(1.0, vm.ZoomScale, 9); // the sheet keeps its own zoom, starting at actual size
            vm.ZoomScale = 0.5;
            Assert.Equal(2.0, vm.PairZoomScale, 9);

            vm.PreviewMode = PreviewMode.Pair;
            Assert.Equal(2.0, vm.ZoomScale, 9);
            Assert.Equal(0.5, vm.SheetZoomScale, 9);

            // リセット goes back to each view's own starting zoom
            vm.ZoomResetCommand.Execute(null);
            Assert.Equal(IrisPreviewControl.DefaultPairZoom, vm.PairZoomScale, 9);
            vm.PreviewMode = PreviewMode.A4Sheet;
            vm.ZoomResetCommand.Execute(null);
            Assert.Equal(1.0, vm.SheetZoomScale, 9);
        }

        [Fact]
        public void TestLineWidthsUseTheSameMillimetreUnitEverywhere()
        {
            // 1 style unit = 1 screen DIP at the default 360% zoom
            Assert.Equal(25.4 / (96.0 * 3.6), IrisStyle.MmPerStyleUnit, 9);
            Assert.Equal(IrisPreviewControl.DefaultPairZoom, new MainViewModel().ZoomScale, 9);

            // Plain lines (the fiber look, on by default, draws filled outlines instead of strokes)
            var p = new IrisParameters { RayThickness = 1.2, RayTaper = 0, RayWidthVariation = 0, RayCurve = 0 };
            string svg = SvgExporter.GeneratePairSvg(p);
            string expectedWidth = (1.2 * IrisStyle.MmPerStyleUnit).ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
            Assert.Contains($"_rays\" stroke=\"{p.RayColor}\" stroke-width=\"{expectedWidth}\"", svg); // plain mm (viewBox units), no "mm" suffix
            Assert.DoesNotContain("mm\" stroke-linecap", svg);
        }

        [Fact]
        public void TestSheetLayoutDetectsContentThatRunsOffThePage()
        {
            static SheetLayout Layout(IrisParameters p, PrintSheetConfig cfg) =>
                new(new ObservableCollection<SheetItem> { new(p, 1) }, cfg);

            // Automatic grid always fits, including the largest designs the sliders allow
            Assert.True(Layout(new IrisParameters { SizeMm = 10 }, new PrintSheetConfig()).FitsOnPage);
            Assert.True(Layout(new IrisParameters { SizeMm = 20, Shape = IrisShape.HorizontalOval, AspectRatio = 2.5 }, new PrintSheetConfig()).FitsOnPage);
            Assert.True(Layout(new IrisParameters { SizeMm = 20, Shape = IrisShape.HorizontalOval, AspectRatio = 2.5, TiltAngle = 45 }, new PrintSheetConfig()).FitsOnPage);

            // Pair wider than the space between very wide margins
            var wideMargins = new PrintSheetConfig { PageMarginLeftMm = 60, PageMarginRightMm = 60 };
            var huge = Layout(new IrisParameters { SizeMm = 20, Shape = IrisShape.HorizontalOval, AspectRatio = 2.5 }, wideMargins);
            Assert.False(huge.FitsOnPage);
            Assert.Equal(103.0, huge.GridWidthMm, 6);  // 2 x 50 mm + 3 mm gap
            Assert.Equal(90.0, huge.UsableWidthMm, 6); // 210 - 60 - 60

            // Manual grid too wide for the margins: 6 filled columns of 12 mm pairs = 6 x 27 + 5 x 6 = 192 mm > 180 mm
            var manualCfg = new PrintSheetConfig { AutoCalculateGrid = false, ManualColumns = 6, ManualRows = 6 };
            var sixPairs = new SheetLayout(new ObservableCollection<SheetItem> { new(new IrisParameters { SizeMm = 12 }, 6) }, manualCfg);
            Assert.Equal(192.0, sixPairs.GridWidthMm, 6);
            Assert.False(sixPairs.FitsOnPage);

            // Same grid with only one pair: the empty cells print nothing, so it fits (no false alarm)
            var onePair = Layout(new IrisParameters { SizeMm = 12 }, manualCfg);
            Assert.Equal(27.0, onePair.GridWidthMm, 6);
            Assert.True(onePair.FitsOnPage);

            // 5 columns of 12 mm pairs = 5 x 27 + 4 x 6 = 159 mm: fits
            Assert.True(new SheetLayout(new ObservableCollection<SheetItem> { new(new IrisParameters { SizeMm = 12 }, 30) },
                new PrintSheetConfig { AutoCalculateGrid = false, ManualColumns = 5, ManualRows = 6 }).FitsOnPage);
        }

        [Fact]
        public void TestRunOnStaReportsFailuresToTheTestThread()
        {
            Assert.Throws<InvalidOperationException>(() => RunOnSta(() => throw new InvalidOperationException("boom")));
        }

        [Fact]
        public void TestQuickSizeConverterMatchesSizeOnAnyLocale()
        {
            var conv = new DollIrisGenerator.Converters.SizeMatchesConverter();
            var german = System.Globalization.CultureInfo.GetCultureInfo("de-DE"); // comma decimal separator
            var previous = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture = german;
                Assert.Equal(true, conv.Convert(18.0, typeof(bool), "18.0", german));
                Assert.Equal(false, conv.Convert(18.6, typeof(bool), "18.0", german));
                Assert.Equal(18.0, conv.ConvertBack(true, typeof(double), "18.0", german));
                Assert.Same(System.Windows.Data.Binding.DoNothing, conv.ConvertBack(false, typeof(double), "18.0", german));
            }
            finally
            {
                System.Globalization.CultureInfo.CurrentCulture = previous;
            }
        }

        [Fact]
        public void TestRemoveSheetItemRemovesTheClickedRowNotTheSelection()
        {
            var vm = new MainViewModel();
            vm.AddCurrentToSheetCommand.Execute(null);
            var first = vm.SheetItems[0];
            var second = vm.SheetItems[1];
            vm.SelectedSheetItem = first;

            vm.RemoveSelectedSheetItemCommand.Execute(second);

            Assert.Single(vm.SheetItems);
            Assert.Same(first, vm.SheetItems[0]);
        }
    }
}
