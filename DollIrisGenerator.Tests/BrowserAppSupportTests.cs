using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;

namespace DollIrisGenerator.Tests
{
    /// <summary>
    /// What the browser app (DollIrisGenerator.Web) relies on in the core: favorites as text in the desktop app's
    /// file format, and the faster color / nebula paths giving exactly the old results.
    /// </summary>
    public class BrowserAppSupportTests
    {
        private static IrisParameters Design()
        {
            var p = new IrisParameters { Name = "受け渡し確認", Seed = 4242, SizeMm = 11.5, IsOddEye = true };
            p.PupilColor = "radial-gradient(#B3F0FF 0%, #29B6F6 45%, #0B4F8A 100%)";
            p.NebulaColor = IrisParameters.DefaultNebulaColor;
            p.NebulaOpacity = 0.7;
            return p;
        }

        [Fact]
        public void TestSerializedFavoritesAreTheDesktopFileFormat()
        {
            var favorites = new List<FavoriteItem> { new(Design()), new(new IrisParameters()) };
            string path = Path.Combine(Path.GetTempPath(), $"irisfav-{Guid.NewGuid():N}.irisfav.json");
            try
            {
                FavoritesStore.WriteToFile(path, favorites);
                Assert.Equal(File.ReadAllText(path), FavoritesStore.Serialize(favorites));

                // What the browser exports, the desktop app's import reads (and the other way round)
                var imported = FavoritesStore.ReadImportFile(path)!;
                var parsed = FavoritesStore.ParseImportText(FavoritesStore.Serialize(favorites), out string? single)!;
                Assert.Null(single);
                Assert.Equal(2, parsed.Count);
                for (int i = 0; i < 2; i++)
                {
                    Assert.Equal(favorites[i].Id, parsed[i].Id);
                    Assert.Equal(imported[i].Name, parsed[i].Name);
                    Assert.Equal(
                        SvgExporter.GeneratePairSvg(favorites[i].Parameters),
                        SvgExporter.GeneratePairSvg(parsed[i].Parameters));
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void TestASingleDesignTextBecomesOneFavorite()
        {
            string json = System.Text.Json.JsonSerializer.Serialize(Design());
            var parsed = FavoritesStore.ParseImportText(json, out string? single)!;
            Assert.Equal(json, single);
            Assert.Single(parsed);
            Assert.Equal("受け渡し確認", parsed[0].Parameters.Name);
        }

        [Fact]
        public void TestBrokenTextThrowsForTheCallerToReport()
        {
            Assert.ThrowsAny<System.Text.Json.JsonException>(() => FavoritesStore.ParseImportText("{ not json", out _));
        }

        [Fact]
        public void TestStopSamplerGivesExactlyColorAt()
        {
            var fills = new[]
            {
                "linear-gradient(90deg, #FFFFFF 0%, #2979FF 100%)",
                "radial-gradient(#E040FB 0%, #7C4DFF 50%, #40C4FF 100%)",
                "conic-gradient(from 270deg, #80FF0000 0%, #FF80D8FF 30%, #B388FF 55%, #A7FFEB 80%, #FF80AB 100%)",
                "linear-gradient(0deg, #ABC 0%, #ABC 50%, #123456 50%, #FEDCBA 100%)"
            };
            foreach (string value in fills)
            {
                var stops = ColorFill.Parse(value).Stops;
                var sampler = new ColorFill.StopSampler(stops);
                for (int i = -20; i <= 1020; i++)
                {
                    double t = i / 1000.0;
                    var c = sampler.ArgbAt(t);
                    Assert.Equal(ColorFill.ColorAt(stops, t), $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}");
                }
                Assert.Equal(ColorFill.ColorAt(stops, double.NaN),
                    $"#{sampler.ArgbAt(double.NaN).A:X2}{sampler.ArgbAt(double.NaN).R:X2}{sampler.ArgbAt(double.NaN).G:X2}{sampler.ArgbAt(double.NaN).B:X2}");
            }
        }

        [Fact]
        public void TestRightEyeNebulaIsTheLeftEyeMirrored()
        {
            var p = Design();
            var left = NebulaTexture.Rgba(p, 777, mirror: false);
            var right = NebulaTexture.Rgba(p, 777, mirror: true);
            int n = NebulaTexture.Resolution;
            Assert.Equal(left.Length, right.Length);
            for (int y = 0; y < n; y += 7)
            {
                for (int x = 0; x < n; x++)
                {
                    int a = (y * n + x) * 4, b = (y * n + (n - 1 - x)) * 4;
                    Assert.True(left.AsSpan(a, 4).SequenceEqual(right.AsSpan(b, 4)), $"pixel ({x}, {y})");
                }
            }
            // The clouds are not symmetric themselves, so the two images really differ
            Assert.False(left.AsSpan().SequenceEqual(right));
        }
    }
}
