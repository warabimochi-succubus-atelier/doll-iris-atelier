using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DollIrisGenerator.Core.Geometry;
using DollIrisGenerator.Models;

namespace DollIrisGenerator.Services
{
    /// <summary>
    /// Nebula clouds (星雲): a seeded cloud image laid over the iris ellipse.
    ///
    /// The image is computed here, pixel by pixel, from the design (no WPF), at one fixed resolution, so the
    /// screen / PDF / PNG renderer (which draws it as a bitmap) and the SVG export (which embeds the same pixels
    /// as a PNG) show exactly the same clouds. The clouds are soft, so <see cref="Resolution"/> pixels across the
    /// iris are enough even when printed at 1200 dpi.
    ///
    /// The clouds are fractal value noise, warped by a second noise for wispy shapes and twisted round the center
    /// by 渦巻き. Each pixel takes the nebula color where it sits (a gradient is placed across the iris like the
    /// other iris-wide colors) and an alpha from the cloud density there.
    /// </summary>
    public static class NebulaTexture
    {
        public const int Resolution = 256;

        private const int SeedOffset = 4000;
        private const int Octaves = 5;
        private const int MaxCached = 12;

        private static readonly Dictionary<string, byte[]> Cache = new();
        private static readonly LinkedList<string> CacheOrder = new();
        private static readonly object CacheLock = new();

        /// <summary>
        /// Straight (non-premultiplied) RGBA pixels, <see cref="Resolution"/> square, row by row from the top.
        /// <paramref name="mirror"/> gives the right eye's mirror image. The result is shared: do not modify it.
        /// </summary>
        public static byte[] Rgba(IrisParameters p, int seed, bool mirror)
        {
            string key = string.Join("|",
                p.NebulaColor, F(p.NebulaScale), F(p.NebulaDensity), F(p.NebulaSwirl),
                seed.ToString(CultureInfo.InvariantCulture), mirror ? "m" : "n");
            lock (CacheLock)
            {
                if (Cache.TryGetValue(key, out var hit)) return hit;
            }

            // The right eye's image is exactly the left one flipped: its pixel centers sample -u, which is exactly
            // the u of the left image's opposite column (with a power-of-two Resolution these values are exact in
            // binary), so it is flipped instead of computed again
            var pixels = mirror ? FlipHorizontally(Rgba(p, seed, false)) : Generate(p, seed);
            lock (CacheLock)
            {
                if (!Cache.ContainsKey(key))
                {
                    Cache[key] = pixels;
                    CacheOrder.AddLast(key);
                    while (CacheOrder.Count > MaxCached)
                    {
                        Cache.Remove(CacheOrder.First!.Value);
                        CacheOrder.RemoveFirst();
                    }
                }
            }
            return pixels;
        }

        /// <summary>The same pixels as a PNG file (for the SVG export).</summary>
        public static byte[] Png(IrisParameters p, int seed, bool mirror) => PngEncoder.Encode(Resolution, Resolution, Rgba(p, seed, mirror));

        // Base64 PNG of each cached pixel array; it goes away with the array when that leaves the cache
        private static readonly ConditionalWeakTable<byte[], string> PngBase64Cache = new();

        /// <summary>
        /// <see cref="Png"/> as Base64 (what the SVG embeds). While the pixels stay cached the encoded text is reused,
        /// so moving a highlight does not encode the same PNG again (slow in the browser app).
        /// </summary>
        public static string PngBase64(IrisParameters p, int seed, bool mirror)
        {
            var pixels = Rgba(p, seed, mirror);
            return PngBase64Cache.GetValue(pixels, px => Convert.ToBase64String(PngEncoder.Encode(Resolution, Resolution, px)));
        }

        private static string F(double v) => v.ToString("R", CultureInfo.InvariantCulture);

        private static byte[] FlipHorizontally(byte[] source)
        {
            // Each pixel's 4 bytes move as one uint, a row at a time (one call per row, not per pixel)
            int n = Resolution;
            var px = new byte[source.Length];
            var from = MemoryMarshal.Cast<byte, uint>(source.AsSpan());
            var to = MemoryMarshal.Cast<byte, uint>(px.AsSpan());
            for (int y = 0; y < n; y++)
            {
                var row = to.Slice(y * n, n);
                from.Slice(y * n, n).CopyTo(row);
                row.Reverse();
            }
            return px;
        }

        private static byte[] Generate(IrisParameters p, int seed)
        {
            int n = Resolution;
            var px = new byte[n * n * 4];
            var clouds = Clouds(p.NebulaScale, p.NebulaSwirl, seed);
            var fade = EdgeFade;

            // Color: solid, or the gradient placed across the unit iris box (same placement as the other iris-wide colors)
            var fill = ColorFill.Parse(p.NebulaColor);
            var colorAt = fill.IsGradient ? GradientMapping.Create(fill, new Rect(-1, -1, 2, 2)).ArgbSampler() : null;
            (int A, int R, int G, int B) solid = GeometryHelper.ParseHexArgb(GeometryHelper.SanitizeColorHex(p.NebulaColor, "#B388FF"));

            // Density moves the threshold: more density → clouds start at lower noise values
            double center = 0.66 - 0.32 * p.NebulaDensity;
            const double Soft = 0.2;
            double lo = center - Soft, hi = center + Soft;

            // Each pixel: smoothstep(lo, hi, cloud), faded toward the very edge so the clouds do not paint over the
            // limbal ring as a hard band, times the color's alpha. Written out (no calls, a loop of its own for a
            // solid color) with the same arithmetic as SmoothStep and Math.Round
            for (int y = 0; y < n; y++)
            {
                double v = (y + 0.5) / n * 2.0 - 1.0;
                for (int x = 0; x < n; x++)
                {
                    int k = y * n + x;
                    double q = (clouds[k] - lo) / (hi - lo);
                    if (q < 0.0) q = 0.0;
                    else if (q > 1.0) q = 1.0;
                    double t = q * q * (3.0 - 2.0 * q) * fade[k];

                    int i = k * 4;
                    int a;
                    if (colorAt == null)
                    {
                        px[i] = (byte)solid.R;
                        px[i + 1] = (byte)solid.G;
                        px[i + 2] = (byte)solid.B;
                        a = RoundHalfEven(solid.A * t);
                    }
                    else
                    {
                        var c = colorAt(new Point((x + 0.5) / n * 2.0 - 1.0, v));
                        px[i] = (byte)c.R;
                        px[i + 1] = (byte)c.G;
                        px[i + 2] = (byte)c.B;
                        a = RoundHalfEven(c.A * t);
                    }
                    px[i + 3] = (byte)(a > 255 ? 255 : a);
                }
            }
            return px;
        }

        /// <summary>
        /// Math.Round (to even) of a value from 0 to 255: the fraction of a double is exact, so this is the same.
        /// Inlined, so the per-pixel loop above makes no call for it.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int RoundHalfEven(double x)
        {
            int f = (int)x;
            double frac = x - f;
            return frac > 0.5 || (frac == 0.5 && (f & 1) == 1) ? f + 1 : f;
        }

        /// <summary>1 − smoothstep(0.88, 1, r) at each pixel: it depends only on where the pixel is, so it is worked out once.</summary>
        private static readonly double[] EdgeFade = BuildEdgeFade();

        private static double[] BuildEdgeFade()
        {
            int n = Resolution;
            var fade = new double[n * n];
            for (int y = 0; y < n; y++)
            {
                double v = (y + 0.5) / n * 2.0 - 1.0;
                for (int x = 0; x < n; x++)
                {
                    double u = (x + 0.5) / n * 2.0 - 1.0;
                    fade[y * n + x] = 1.0 - SmoothStep(0.88, 1.0, Math.Sqrt(u * u + v * v));
                }
            }
            return fade;
        }

        // The cloud noise of the last few cloud shapes (細かさ, 渦巻き, seed). 量 and 色 only change how it is
        // colored, so dragging those sliders does not compute the noise again (slow in the browser app).
        private const int MaxCachedClouds = 3;
        private static readonly Dictionary<string, double[]> CloudCache = new();
        private static readonly LinkedList<string> CloudCacheOrder = new();

        /// <summary>The cloud noise value (0..1) at each pixel, row by row from the top. Shared: do not modify it.</summary>
        private static double[] Clouds(double scale, double swirl, int seed)
        {
            string key = string.Join("|", F(scale), F(swirl), seed.ToString(CultureInfo.InvariantCulture));
            lock (CacheLock)
            {
                if (CloudCache.TryGetValue(key, out var hit)) return hit;
            }

            var clouds = ComputeClouds(scale, swirl, seed);
            lock (CacheLock)
            {
                if (!CloudCache.ContainsKey(key))
                {
                    CloudCache[key] = clouds;
                    CloudCacheOrder.AddLast(key);
                    while (CloudCacheOrder.Count > MaxCachedClouds)
                    {
                        CloudCache.Remove(CloudCacheOrder.First!.Value);
                        CloudCacheOrder.RemoveFirst();
                    }
                }
            }
            return clouds;
        }

        /// <summary>
        /// The clouds a row at a time: each noise runs octave by octave over the whole row, which needs far fewer
        /// calls (the browser app's interpreter pays for each one) and lets neighbouring pixels share the lattice
        /// values of their cell. Every pixel still gets exactly the arithmetic of one fractal noise per pixel, so the
        /// image does not change (FastPathParityTests).
        /// </summary>
        private static double[] ComputeClouds(double scale, double swirl, int seed)
        {
            int n = Resolution;
            var clouds = new double[n * n];
            uint s = unchecked((uint)(seed + SeedOffset));
            double freq = 2.2 * scale;
            double twist = swirl * 2.6;

            // The seed part of every lattice hash, per noise and octave: the two warp noises, then the clouds
            var warpXSeeds = OctaveSeeds(s ^ 0x9E3779B9u);
            var warpYSeeds = OctaveSeeds(s ^ 0x85EBCA6Bu);
            var cloudSeeds = OctaveSeeds(s);

            var su = new double[n];
            var sv = new double[n];
            var xs = new double[n];
            var ys = new double[n];
            var wx = new double[n];
            var wy = new double[n];
            var cloud = new double[n];

            for (int y = 0; y < n; y++)
            {
                double v = (y + 0.5) / n * 2.0 - 1.0;
                for (int x = 0; x < n; x++)
                {
                    double u = (x + 0.5) / n * 2.0 - 1.0;
                    double r = Math.Sqrt(u * u + v * v);

                    // Swirl: turn each point round the center, more the further out it is (a spiral)
                    double ang = Math.Atan2(v, u) + twist * r;
                    su[x] = r * Math.Cos(ang);
                    sv[x] = r * Math.Sin(ang);
                }

                // Domain warp for wispy, flowing clouds
                for (int x = 0; x < n; x++) { xs[x] = su[x] * freq + 5.2; ys[x] = sv[x] * freq + 1.3; }
                FbmRow(xs, ys, wx, warpXSeeds);
                for (int x = 0; x < n; x++) { xs[x] = su[x] * freq + 1.7; ys[x] = sv[x] * freq + 9.2; }
                FbmRow(xs, ys, wy, warpYSeeds);
                for (int x = 0; x < n; x++) { xs[x] = su[x] * freq + 1.6 * wx[x]; ys[x] = sv[x] * freq + 1.6 * wy[x]; }
                FbmRow(xs, ys, cloud, cloudSeeds);

                Array.Copy(cloud, 0, clouds, y * n, n);
            }
            return clouds;
        }

        private static uint[] OctaveSeeds(uint seed)
        {
            var terms = new uint[Octaves];
            for (int o = 0; o < Octaves; o++) terms[o] = unchecked((seed + (uint)o * 0x632BE5ABu) * 0xCB1AB31Fu);
            return terms;
        }

        private static double SmoothStep(double a, double b, double x)
        {
            double t = Math.Clamp((x - a) / (b - a), 0.0, 1.0);
            return t * t * (3.0 - 2.0 * t);
        }

        /// <summary>
        /// Fractal value noise (0..1) at each point (<paramref name="xs"/>, <paramref name="ys"/>) into
        /// <paramref name="result"/>; the coordinates are used up (scaled for each octave). Per point the same
        /// arithmetic as one call per octave, value noise and lattice hash. The lattice value is plain integer
        /// arithmetic, so every machine agrees.
        /// </summary>
        private static void FbmRow(double[] xs, double[] ys, double[] result, uint[] seedTerms)
        {
            int n = xs.Length;
            Array.Clear(result);
            double amp = 0.5, norm = 0;
            for (int o = 0; o < Octaves; o++)
            {
                uint st = seedTerms[o];
                int lastXi = int.MinValue, lastYi = int.MinValue;
                double a = 0, b = 0, c = 0, d = 0;
                for (int i = 0; i < n; i++)
                {
                    double x = xs[i], y = ys[i];
                    // Math.Floor without the call (the same for these finite, moderate coordinates)
                    int xi = (int)x, yi = (int)y;
                    if (xi > x) xi--;
                    if (yi > y) yi--;

                    // Neighbouring points mostly sit in the same lattice cell, above all in the coarse octaves
                    if (xi != lastXi || yi != lastYi)
                    {
                        lastXi = xi;
                        lastYi = yi;
                        unchecked
                        {
                            uint x0 = (uint)xi * 0x8DA6B343u, x1 = (uint)(xi + 1) * 0x8DA6B343u;
                            uint y0 = (uint)yi * 0xD8163841u, y1 = (uint)(yi + 1) * 0xD8163841u;

                            uint h = x0 ^ y0 ^ st;
                            h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
                            a = (h & 0xFFFFFF) / 16777215.0;

                            h = x1 ^ y0 ^ st;
                            h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
                            b = (h & 0xFFFFFF) / 16777215.0;

                            h = x0 ^ y1 ^ st;
                            h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
                            c = (h & 0xFFFFFF) / 16777215.0;

                            h = x1 ^ y1 ^ st;
                            h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
                            d = (h & 0xFFFFFF) / 16777215.0;
                        }
                    }

                    double fx = x - xi, fy = y - yi;
                    double ux = fx * fx * (3 - 2 * fx), uy = fy * fy * (3 - 2 * fy);
                    result[i] += amp * (a + (b - a) * ux + (c - a) * uy + (a - b - c + d) * ux * uy);
                    xs[i] = x * 2.03;
                    ys[i] = y * 2.03;
                }
                norm += amp;
                amp *= 0.5;
            }
            for (int i = 0; i < n; i++) result[i] /= norm;
        }
    }

    /// <summary>Minimal PNG writer for 8-bit RGBA images (zlib from the framework, no WPF needed).</summary>
    public static class PngEncoder
    {
        private static readonly uint[] CrcTable = BuildCrcTable();

        public static byte[] Encode(int width, int height, byte[] rgba)
        {
            if (rgba.Length != width * height * 4) throw new ArgumentException("pixel buffer size does not match", nameof(rgba));

            // Every row starts with filter "Sub" (1): soft images compress much better than unfiltered
            var raw = new byte[height * (width * 4 + 1)];
            int o = 0;
            for (int y = 0; y < height; y++)
            {
                raw[o++] = 1;
                int row = y * width * 4, rowEnd = row + width * 4;
                // The first pixel has nothing to its left
                for (int i = row; i < row + 4; i++) raw[o++] = rgba[i];
                for (int i = row + 4; i < rowEnd; i++) raw[o++] = unchecked((byte)(rgba[i] - rgba[i - 4]));
            }

            byte[] compressed;
            using (var ms = new MemoryStream())
            {
                using (var z = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true)) z.Write(raw, 0, raw.Length);
                compressed = ms.ToArray();
            }

            using var png = new MemoryStream();
            png.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
            var ihdr = new byte[13];
            WriteBigEndian(ihdr, 0, (uint)width);
            WriteBigEndian(ihdr, 4, (uint)height);
            ihdr[8] = 8;  // bit depth
            ihdr[9] = 6;  // RGBA
            WriteChunk(png, "IHDR", ihdr);
            WriteChunk(png, "IDAT", compressed);
            WriteChunk(png, "IEND", Array.Empty<byte>());
            return png.ToArray();
        }

        private static void WriteChunk(Stream s, string type, byte[] data)
        {
            var len = new byte[4];
            WriteBigEndian(len, 0, (uint)data.Length);
            s.Write(len);
            var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
            s.Write(typeBytes);
            s.Write(data);
            uint crc = 0xFFFFFFFFu;
            crc = Crc(crc, typeBytes);
            crc = Crc(crc, data);
            var crcBytes = new byte[4];
            WriteBigEndian(crcBytes, 0, crc ^ 0xFFFFFFFFu);
            s.Write(crcBytes);
        }

        private static uint Crc(uint crc, byte[] data)
        {
            foreach (byte b in data) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return crc;
        }

        private static uint[] BuildCrcTable()
        {
            var t = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                t[n] = c;
            }
            return t;
        }

        private static void WriteBigEndian(byte[] b, int offset, uint v)
        {
            b[offset] = (byte)(v >> 24);
            b[offset + 1] = (byte)(v >> 16);
            b[offset + 2] = (byte)(v >> 8);
            b[offset + 3] = (byte)v;
        }
    }
}
