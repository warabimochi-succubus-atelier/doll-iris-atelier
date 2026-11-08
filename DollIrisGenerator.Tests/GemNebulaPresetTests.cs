using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using Xunit;

namespace DollIrisGenerator.Tests
{
    /// <summary>Gem-cut pupil facets, nebula clouds and the presets modelled on the sample eyes.</summary>
    public class GemNebulaPresetTests
    {
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

        // ---------------- Gem cut ----------------

        [Fact]
        public void TestFacetsAreOffByDefaultAndExportClippedToThePupil()
        {
            var p = new IrisParameters();
            Assert.Equal(0.0, p.PupilFacetIntensity);
            Assert.DoesNotContain("pupil_facets", SvgExporter.GeneratePairSvg(p));

            p.PupilShape = PupilShape.Heart;
            p.PupilFacetIntensity = 0.8;
            p.PupilFacetCount = 9;
            string svg = SvgExporter.GeneratePairSvg(p);
            Assert.True(SvgValidator.IsWellFormed(svg, out var error), error);
            Assert.Contains("<g id=\"left_eye_pupil_facets\" clip-path=\"url(#left_eye_pupil_facet_clip)\">", svg);
            int facets = svg.Split("<g id=\"left_eye_pupil_facets\"")[1].Split("</g>")[0].Split("<path ").Length - 1;
            Assert.Equal(9 * 4, facets);
        }

        [Fact]
        public void TestFacetsMirrorInTheRightEyeAndAlternateLightAndDark()
        {
            var left = IrisPatterns.PupilFacets(10, 10, 8, 123, false);
            var right = IrisPatterns.PupilFacets(10, 10, 8, 123, true);
            Assert.Equal(8 * 4, left.Count);
            for (int i = 0; i < left.Count; i++)
            {
                Assert.Equal(left[i].Shade, right[i].Shade);
                for (int j = 0; j < left[i].Polygon.Length; j++)
                {
                    Assert.Equal(-left[i].Polygon[j].X, right[i].Polygon[j].X, 9);
                    Assert.Equal(left[i].Polygon[j].Y, right[i].Polygon[j].Y, 9);
                }
            }
            Assert.Contains(left, f => f.Shade > 0);
            Assert.Contains(left, f => f.Shade < 0);
            Assert.All(left, f => Assert.InRange(Math.Abs(f.Shade), 0.1, 1.0));
        }

        [Fact]
        public void TestFacetSettingsClamp()
        {
            var p = new IrisParameters { PupilFacetCount = 99, PupilFacetIntensity = double.NaN };
            Assert.Equal(IrisParameters.MaxPupilFacetCount, p.PupilFacetCount);
            Assert.Equal(0.0, p.PupilFacetIntensity);
            p.PupilFacetCount = 1;
            Assert.Equal(IrisParameters.MinPupilFacetCount, p.PupilFacetCount);
        }

        // ---------------- Nebula ----------------

        [Fact]
        public void TestNebulaIsOffByDefault()
        {
            var p = new IrisParameters();
            Assert.Equal(0.0, p.NebulaOpacity);
            Assert.DoesNotContain("_nebula", SvgExporter.GeneratePairSvg(p));
        }

        [Fact]
        public void TestNebulaIsDeterministicAndMirrored()
        {
            var p = new IrisParameters { NebulaOpacity = 0.8 };
            var a = NebulaTexture.Rgba(p, 77, false);
            var again = NebulaTexture.Rgba(new IrisParameters { NebulaOpacity = 0.3 }, 77, false); // opacity is applied when drawing
            Assert.Equal(a, again);
            var other = NebulaTexture.Rgba(p, 78, false);
            Assert.NotEqual(a, other);

            int n = NebulaTexture.Resolution;
            var mirrored = NebulaTexture.Rgba(p, 77, true);
            for (int y = 0; y < n; y += 17)
                for (int x = 0; x < n; x += 13)
                    for (int c = 0; c < 4; c++)
                        Assert.Equal(a[(y * n + x) * 4 + c], mirrored[(y * n + (n - 1 - x)) * 4 + c]);
        }

        [Fact]
        public void TestNebulaHasCloudsAndGapsAndFadesAtTheEdge()
        {
            var p = new IrisParameters { NebulaOpacity = 1.0, NebulaDensity = 0.5 };
            var px = NebulaTexture.Rgba(p, 5, false);
            int n = NebulaTexture.Resolution;
            var alphas = Enumerable.Range(0, n * n).Select(i => px[i * 4 + 3]).ToArray();
            Assert.Contains(alphas, a => a > 200);
            Assert.Contains(alphas, a => a < 20);
            // Corners lie outside the iris circle: fully clear
            Assert.Equal(0, alphas[0]);
            Assert.Equal(0, alphas[n * n - 1]);

            var dense = NebulaTexture.Rgba(new IrisParameters { NebulaDensity = 1.0 }, 5, false);
            var sparse = NebulaTexture.Rgba(new IrisParameters { NebulaDensity = 0.0 }, 5, false);
            static double Mean(byte[] b) => Enumerable.Range(0, b.Length / 4).Average(i => b[i * 4 + 3]);
            Assert.True(Mean(dense) > Mean(sparse) * 2);
        }

        [Fact]
        public void TestNebulaIsEmbeddedAsAValidPngInTheSvg()
        {
            var p = new IrisParameters { NebulaOpacity = 0.7 };
            string svg = SvgExporter.GeneratePairSvg(p);
            Assert.True(SvgValidator.IsWellFormed(svg, out var error), error);
            Assert.Contains("<image id=\"left_eye_nebula\"", svg);
            Assert.Contains("<image id=\"right_eye_nebula\"", svg);
            Assert.Contains("opacity=\"0.70\" href=\"data:image/png;base64,", svg);

            // The embedded PNG decodes to the same pixels the screen renderer uses
            string b64 = svg.Split("left_eye_nebula")[1].Split("base64,")[1].Split('"')[0];
            var bytes = Convert.FromBase64String(b64);
            RunOnSta(() =>
            {
                var decoder = new System.Windows.Media.Imaging.PngBitmapDecoder(new MemoryStream(bytes),
                    System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                var frame = new System.Windows.Media.Imaging.FormatConvertedBitmap(decoder.Frames[0], System.Windows.Media.PixelFormats.Bgra32, null, 0);
                int n = NebulaTexture.Resolution;
                Assert.Equal(n, frame.PixelWidth);
                var bgra = new byte[n * n * 4];
                frame.CopyPixels(bgra, n * 4, 0);
                var rgba = NebulaTexture.Rgba(p, p.Seed, false);
                for (int i = 0; i < n * n; i += 97)
                {
                    Assert.Equal(rgba[i * 4 + 3], bgra[i * 4 + 3]);
                    if (rgba[i * 4 + 3] > 0) Assert.Equal(rgba[i * 4], bgra[i * 4 + 2]);
                }
            });
        }

        [Fact]
        public void TestNebulaAndFacetsRenderOnScreen()
        {
            RunOnSta(() =>
            {
                var p = new IrisParameters { NebulaOpacity = 0.8, PupilShape = PupilShape.Star, PupilFacetIntensity = 0.9, IsOddEye = true };
                var left = RasterExporter.RenderSingleToBitmap(p, isRightEye: false, dpi: 200.0, showCutLines: false, showCenterMarks: false);
                var right = RasterExporter.RenderSingleToBitmap(p, isRightEye: true, dpi: 200.0, showCutLines: false, showCenterMarks: false);
                Assert.True(left.PixelWidth > 0 && right.PixelWidth > 0);
            });
        }

        [Fact]
        public void TestNebulaSettingsClampAndRoundTrip()
        {
            var p = new IrisParameters { NebulaScale = 99, NebulaSwirl = -9, NebulaDensity = double.NaN, NebulaOpacity = 0.4, NebulaColor = null! };
            Assert.Equal(IrisParameters.MaxNebulaScale, p.NebulaScale);
            Assert.Equal(-1.0, p.NebulaSwirl);
            Assert.Equal(0.5, p.NebulaDensity);
            Assert.Equal(IrisParameters.DefaultNebulaColor, p.NebulaColor);
            var loaded = JsonSerializer.Deserialize<IrisParameters>(JsonSerializer.Serialize(p))!;
            Assert.Equal(0.4, loaded.NebulaOpacity);
            Assert.Equal(p.NebulaScale, loaded.NebulaScale);
        }

        [Fact]
        public void TestPngEncoderWritesAReadableImage()
        {
            var rgba = new byte[3 * 2 * 4];
            for (int i = 0; i < rgba.Length; i++) rgba[i] = (byte)(i * 11);
            var png = PngEncoder.Encode(3, 2, rgba);
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png.Take(4));
            RunOnSta(() =>
            {
                var decoder = new System.Windows.Media.Imaging.PngBitmapDecoder(new MemoryStream(png),
                    System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                var frame = new System.Windows.Media.Imaging.FormatConvertedBitmap(decoder.Frames[0], System.Windows.Media.PixelFormats.Bgra32, null, 0);
                var bgra = new byte[3 * 2 * 4];
                frame.CopyPixels(bgra, 3 * 4, 0);
                for (int i = 0; i < 6; i++)
                {
                    Assert.Equal(rgba[i * 4 + 3], bgra[i * 4 + 3]);
                    Assert.Equal(rgba[i * 4], bgra[i * 4 + 2]);
                    Assert.Equal(rgba[i * 4 + 1], bgra[i * 4 + 1]);
                    Assert.Equal(rgba[i * 4 + 2], bgra[i * 4]);
                }
            });
        }

        // ---------------- Presets ----------------

        private static readonly string[] SampleNames =
        {
            "ベーシック: クリアブルー", "ベーシック: アクア", "ベーシック: ラベンダー", "ベーシック: パープル",
            "ベーシック: ピンク", "ベーシック: サクラ", "ベーシック: レッド", "ベーシック: アンバー",
            "ベーシック: グリーン", "ベーシック: グレー", "ベーシック: キャットアイ",
            "特殊: 星屑", "特殊: オーロラ", "特殊: 銀河", "特殊: 夕焼け", "特殊: 夜空", "特殊: 宝石", "特殊: ハート", "特殊: 花びら",
            "雰囲気: A. ナチュラル (ピンク系)", "雰囲気: B. 精霊 (ブルー系)", "雰囲気: C. アンニュイ (パープル系)",
            "雰囲気: D. つんでれ (レッド系)", "雰囲気: E. 小悪魔 (赤紫系)", "雰囲気: F. 無邪気 (アクア系)"
        };

        private static PresetDefinition Preset(string name) => ColorPresets.All.Single(x => x.Name == name);

        [Fact]
        public void TestBuiltInPresetsAreTheThreeGroups()
        {
            // ベーシック (the first-generation presets merged in) / 特殊 / 雰囲気, in this order
            Assert.Equal(SampleNames, ColorPresets.All.Select(x => x.Name));
            // Selected at start-up
            Assert.Equal("ベーシック: クリアブルー", ColorPresets.All[0].Name);
        }

        [Fact]
        public void TestAPresetLooksTheSameWhateverWasPickedBefore()
        {
            foreach (var next in ColorPresets.All)
            {
                var fresh = new IrisParameters();
                ColorPresets.ApplyPreset(fresh, next);
                string expected = SvgExporter.GeneratePairSvg(fresh);

                foreach (var before in ColorPresets.All)
                {
                    var p = new IrisParameters();
                    ColorPresets.ApplyPreset(p, before);
                    ColorPresets.ApplyPreset(p, next);
                    Assert.True(expected == SvgExporter.GeneratePairSvg(p), $"{before.Name} → {next.Name}");
                }
            }
        }

        [Fact]
        public void TestEveryPresetRendersAndExports()
        {
            foreach (var preset in ColorPresets.All)
            {
                var p = new IrisParameters();
                ColorPresets.ApplyPreset(p, preset);
                string svg = SvgExporter.GeneratePairSvg(p);
                Assert.True(SvgValidator.IsWellFormed(svg, out var error), $"{preset.Name}: {error}");
            }
        }

        [Fact]
        public void TestSpecialPresetsBringTheirLook()
        {
            var p = new IrisParameters();
            ColorPresets.ApplyPreset(p, Preset("特殊: 銀河"));
            Assert.True(p.NebulaOpacity > 0.5);
            ColorPresets.ApplyPreset(p, Preset("特殊: オーロラ"));
            Assert.Equal(0.0, p.NebulaOpacity); // the galaxy's clouds do not stay behind
            Assert.True(p.AuroraOpacity > 0.3);
            Assert.Equal(ColorFillKind.Conic, ColorFill.Parse(p.AuroraColor).Kind);
            ColorPresets.ApplyPreset(p, Preset("特殊: 宝石"));
            Assert.Equal(PupilShape.Heart, p.PupilShape);
            Assert.True(p.PupilFacetIntensity > 0.5);
            Assert.Equal(0.0, p.AuroraOpacity);
            ColorPresets.ApplyPreset(p, Preset("特殊: 星屑"));
            Assert.True(p.ParticleCount >= 200);
            Assert.Equal(0.0, p.PupilFacetIntensity);
        }

        [Fact]
        public void TestPetalPresetOutlinesTheHeartAndTheNextPresetRemovesIt()
        {
            var p = new IrisParameters();
            ColorPresets.ApplyPreset(p, Preset("特殊: 花びら"));
            var heart = p.Symbols.Single(s => s.Enabled);
            Assert.Equal(SymbolType.Heart, heart.Type);
            Assert.Equal(0.0, heart.PositionX);
            Assert.Equal(0.0, heart.PositionY);
            Assert.True(heart.Size < p.PupilSizeRatio); // smaller than the pupil heart: only its rim shows

            ColorPresets.ApplyPreset(p, Preset("ベーシック: アクア"));
            Assert.DoesNotContain(p.Symbols, s => s.Enabled);
        }

        [Fact]
        public void TestSettingsAPresetLeavesOutGoBackToDefaults()
        {
            // A preset with colors only: the look settings and layers return to the app's defaults, symbols go off
            var colorsOnly = JsonSerializer.Deserialize<PresetDefinition>("{\"Name\":\"色だけ\",\"BaseColor\":\"#112233\"}")!;
            var p = new IrisParameters { AuroraOpacity = 0.5, NebulaOpacity = 0.5, PupilFacetIntensity = 0.5, ParticleCount = 33, RayCount = 21, PupilSizeRatio = 0.45 };
            p.Symbols[0].Enabled = true;
            ColorPresets.ApplyPreset(p, colorsOnly);
            var d = new IrisParameters();
            Assert.Equal("#112233", p.BaseColor);
            Assert.Equal(0.0, p.AuroraOpacity);
            Assert.Equal(0.0, p.NebulaOpacity);
            Assert.Equal(0.0, p.PupilFacetIntensity);
            Assert.Equal(d.ParticleCount, p.ParticleCount);
            Assert.Equal(d.RayCount, p.RayCount);
            Assert.Equal(d.PupilSizeRatio, p.PupilSizeRatio);
            Assert.DoesNotContain(p.Symbols, s => s.Enabled);
        }

        [Fact]
        public void TestPresetFromDesignBringsBackTheSameLook()
        {
            // Start from a built-in look with a symbol, then change the look settings by hand
            var source = new IrisParameters();
            ColorPresets.ApplyPreset(source, Preset("特殊: 花びら"));
            source.RayCount = 37;
            source.RayIntensity = 0.42;
            source.ParticleCount = 123;
            source.ParticleGlow = 0.66;
            source.PupilSizeRatio = 0.27;
            source.NebulaDensity = 0.31;
            source.PupilColor = "linear-gradient(90deg, #FF0000 0%, #0000FF 100%)";

            var preset = ColorPresets.FromDesign(source, "カスタム: テスト", "");

            // Apply it to a design that looks different: everything the preset holds comes back
            var target = new IrisParameters { SizeMm = 12.0, Seed = 777 };
            ColorPresets.ApplyPreset(target, Preset("特殊: 銀河"));
            target.Symbols.Add(new DecorativeSymbolItem { Type = SymbolType.Star5, Enabled = true });
            ColorPresets.ApplyPreset(target, preset);

            Assert.Equal(source.PupilColor, target.PupilColor);
            Assert.Equal(source.BaseColor, target.BaseColor);
            Assert.Equal(source.PupilShape, target.PupilShape);
            Assert.Equal(37, target.RayCount);
            Assert.Equal(0.42, target.RayIntensity);
            Assert.Equal(123, target.ParticleCount);
            Assert.Equal(0.66, target.ParticleGlow);
            Assert.Equal(0.27, target.PupilSizeRatio);
            Assert.Equal(0.31, target.NebulaDensity);
            Assert.Equal(source.NebulaOpacity, target.NebulaOpacity);
            Assert.Equal(source.PupilFacetIntensity, target.PupilFacetIntensity);

            var sourceSymbol = source.Symbols.First(s => s.Enabled);
            var symbol = Assert.Single(target.Symbols, s => s.Enabled);
            Assert.Equal(sourceSymbol.Type, symbol.Type);
            Assert.Equal(sourceSymbol.Size, symbol.Size);
            Assert.Equal(sourceSymbol.PositionX, symbol.PositionX);
            Assert.Equal(sourceSymbol.PositionY, symbol.PositionY);

            // Size and seed are not part of a preset
            Assert.Equal(12.0, target.SizeMm);
            Assert.Equal(777, target.Seed);
        }

        [Fact]
        public void TestPresetFromDesignWithoutSymbolsTurnsSymbolsOff()
        {
            var source = new IrisParameters();
            foreach (var s in source.Symbols) s.Enabled = false;
            var preset = ColorPresets.FromDesign(source, "カスタム: 記号なし", "");
            Assert.Equal(SymbolType.None, preset.DefaultSymbol);

            var target = new IrisParameters();
            ColorPresets.ApplyPreset(target, Preset("特殊: 花びら"));
            ColorPresets.ApplyPreset(target, preset);
            Assert.DoesNotContain(target.Symbols, s => s.Enabled);
        }

        [Fact]
        public void TestPresetFromRandomDesignRebuildsItsLookExceptOddEye()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                var source = new IrisParameters();
                IrisRandomizer.RandomizeAttributes(source, new Random(seed));
                source.LensPatternEnabled = seed % 2 == 0;
                source.Highlights.Add(new HighlightItem { PositionX = 0.4, PositionY = 0.3, Size = 0.1 });
                source.Symbols.Add(new DecorativeSymbolItem { Type = SymbolType.Moon, Enabled = true, Rotation = 30 });
                source.IsOddEye = false;

                // Through a shared file, like a preset passed to another user
                string path = Path.Combine(Path.GetTempPath(), $"dolliris_preset_{Guid.NewGuid():N}.json");
                PresetDefinition preset;
                try
                {
                    ColorPresets.ExportPalettesToFile(path, new[] { ColorPresets.FromDesign(source, "カスタム", "") });
                    preset = ColorPresets.ImportPalettesFromFile(path)![0];
                }
                finally
                {
                    File.Delete(path);
                }

                // Another design that had a built-in preset and its own random look: the preset rebuilds the source
                var target = new IrisParameters();
                IrisRandomizer.RandomizeAttributes(target, new Random(seed + 1000));
                target.IsOddEye = false;
                ColorPresets.ApplyPreset(target, Preset("特殊: 花びら"));
                ColorPresets.ApplyPreset(target, preset);
                target.Seed = source.Seed; // the seed is not part of a preset
                Assert.True(SvgExporter.GeneratePairSvg(source) == SvgExporter.GeneratePairSvg(target), $"seed {seed}");
            }
        }

        [Fact]
        public void TestPresetLeavesSizeShapeAndOddEyeAlone()
        {
            var p = new IrisParameters { SizeMm = 12.5, Shape = IrisShape.VerticalOval, AspectRatio = 1.3, TiltAngle = 10, Seed = 4242, IsOddEye = true, OddEyeBaseColor = "#123456" };
            ColorPresets.ApplyPreset(p, Preset("特殊: 銀河"));
            Assert.Equal(12.5, p.SizeMm);
            Assert.Equal(IrisShape.VerticalOval, p.Shape);
            Assert.Equal(1.3, p.AspectRatio);
            Assert.Equal(10, p.TiltAngle);
            Assert.Equal(4242, p.Seed);
            Assert.True(p.IsOddEye);
            Assert.Equal("#123456", p.OddEyeBaseColor);
        }

        [Fact]
        public void TestBuiltInPresetAfterRandomGenerationLooksTheSame()
        {
            foreach (var preset in ColorPresets.All)
            {
                var fresh = new IrisParameters();
                ColorPresets.ApplyPreset(fresh, preset);

                var p = new IrisParameters();
                IrisRandomizer.RandomizeAttributes(p, new Random(7));
                p.IsOddEye = false;
                p.Seed = fresh.Seed;
                ColorPresets.ApplyPreset(p, preset);
                Assert.True(SvgExporter.GeneratePairSvg(fresh) == SvgExporter.GeneratePairSvg(p), preset.Name);
            }
        }

        [Fact]
        public void TestPresetsWithLayersSurvivePaletteExport()
        {
            string path = Path.Combine(Path.GetTempPath(), $"dolliris_palette_{Guid.NewGuid():N}.json");
            try
            {
                ColorPresets.ExportPalettesToFile(path, new[] { Preset("特殊: 銀河"), Preset("特殊: 花びら") });
                var loaded = ColorPresets.ImportPalettesFromFile(path)!;
                Assert.Equal(Preset("特殊: 銀河").NebulaOpacity, loaded[0].NebulaOpacity);
                Assert.Equal(Preset("特殊: 花びら").SymbolSize, loaded[1].SymbolSize);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
