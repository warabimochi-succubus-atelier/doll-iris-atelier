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
            "ベーシック: クリアブルー", "ベーシック: アクア", "ベーシック: ラベンダー", "ベーシック: ピンク",
            "ベーシック: レッド", "ベーシック: アンバー", "ベーシック: グリーン", "ベーシック: グレー",
            "特殊: 星屑", "特殊: オーロラ", "特殊: 銀河", "特殊: 夕焼け", "特殊: 夜空", "特殊: 宝石", "特殊: ハート", "特殊: 花びら",
            "雰囲気: A. ナチュラル (ピンク系)", "雰囲気: B. 精霊 (ブルー系)", "雰囲気: C. アンニュイ (パープル系)",
            "雰囲気: D. つんでれ (レッド系)", "雰囲気: E. 小悪魔 (赤紫系)", "雰囲気: F. 無邪気 (アクア系)"
        };

        private static PresetDefinition Preset(string name) => ColorPresets.All.Single(x => x.Name == name);

        [Fact]
        public void TestAllSampleInspiredPresetsExistAfterTheOldOnes()
        {
            foreach (var name in SampleNames) Assert.Single(ColorPresets.All, x => x.Name == name);
            // The earlier presets keep their places (saved selections and tests refer to them by position)
            Assert.Equal("ブルー (サファイア)", ColorPresets.All[0].Name);
            Assert.Equal("キャットアイ (猫目スリット)", ColorPresets.All[10].Name);
            Assert.Equal(11 + SampleNames.Length, ColorPresets.All.Count);
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
        public void TestOldPalettesStillLoadAndClearTheLayers()
        {
            // A palette saved before the layers existed: applying it turns them off and keeps the look settings
            var old = JsonSerializer.Deserialize<PresetDefinition>("{\"Name\":\"旧パレット\",\"BaseColor\":\"#112233\"}")!;
            var p = new IrisParameters { AuroraOpacity = 0.5, NebulaOpacity = 0.5, PupilFacetIntensity = 0.5, ParticleCount = 33, RayCount = 21 };
            p.Symbols[0].Enabled = true;
            ColorPresets.ApplyPreset(p, old);
            Assert.Equal("#112233", p.BaseColor);
            Assert.Equal(0.0, p.AuroraOpacity);
            Assert.Equal(0.0, p.NebulaOpacity);
            Assert.Equal(0.0, p.PupilFacetIntensity);
            Assert.Equal(33, p.ParticleCount);
            Assert.Equal(21, p.RayCount);
            Assert.True(p.Symbols[0].Enabled); // old palettes do not replace symbols
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
                Assert.True(loaded[1].ReplaceSymbols);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
