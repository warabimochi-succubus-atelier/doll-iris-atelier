using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Windows;
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

            var pixels = Generate(p, seed, mirror);
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

        private static string F(double v) => v.ToString("R", CultureInfo.InvariantCulture);

        private static byte[] Generate(IrisParameters p, int seed, bool mirror)
        {
            int n = Resolution;
            var px = new byte[n * n * 4];
            uint s = unchecked((uint)(seed + SeedOffset));

            // Color: solid, or the gradient placed across the unit iris box (same placement as the other iris-wide colors)
            var fill = ColorFill.Parse(p.NebulaColor);
            GradientMapping? map = fill.IsGradient ? GradientMapping.Create(fill, new Rect(-1, -1, 2, 2)) : null;
            var solid = Argb(GeometryHelper.SanitizeColorHex(p.NebulaColor, "#B388FF"));

            double freq = 2.2 * p.NebulaScale;
            // Density moves the threshold: more density → clouds start at lower noise values
            double center = 0.66 - 0.32 * p.NebulaDensity;
            const double Soft = 0.2;
            double twist = p.NebulaSwirl * 2.6;

            for (int y = 0; y < n; y++)
            {
                double v = (y + 0.5) / n * 2.0 - 1.0;
                for (int x = 0; x < n; x++)
                {
                    // The right eye is the mirror image: sample the left eye's pattern at -u
                    double u0 = (x + 0.5) / n * 2.0 - 1.0;
                    double u = mirror ? -u0 : u0;
                    double r = Math.Sqrt(u * u + v * v);

                    // Swirl: turn each point round the center, more the further out it is (a spiral)
                    double ang = Math.Atan2(v, u) + twist * r;
                    double su = r * Math.Cos(ang), sv = r * Math.Sin(ang);

                    // Domain warp for wispy, flowing clouds
                    double wx = Fbm(su * freq + 5.2, sv * freq + 1.3, s ^ 0x9E3779B9u);
                    double wy = Fbm(su * freq + 1.7, sv * freq + 9.2, s ^ 0x85EBCA6Bu);
                    double cloud = Fbm(su * freq + 1.6 * wx, sv * freq + 1.6 * wy, s);

                    double t = SmoothStep(center - Soft, center + Soft, cloud);
                    // Fade toward the very edge so the clouds do not paint over the limbal ring as a hard band
                    t *= 1.0 - SmoothStep(0.88, 1.0, r);

                    var c = map is { } m ? Argb(m.ColorAt(new Point(u, v))) : solid;
                    int i = (y * n + x) * 4;
                    px[i] = c.R;
                    px[i + 1] = c.G;
                    px[i + 2] = c.B;
                    px[i + 3] = (byte)Math.Clamp((int)Math.Round(c.A * t), 0, 255);
                }
            }
            return px;
        }

        private static double SmoothStep(double a, double b, double x)
        {
            double t = Math.Clamp((x - a) / (b - a), 0.0, 1.0);
            return t * t * (3.0 - 2.0 * t);
        }

        /// <summary>Fractal value noise, 0..1.</summary>
        private static double Fbm(double x, double y, uint seed)
        {
            double sum = 0, amp = 0.5, norm = 0;
            for (int o = 0; o < Octaves; o++)
            {
                sum += amp * ValueNoise(x, y, seed + (uint)o * 0x632BE5ABu);
                norm += amp;
                amp *= 0.5;
                x *= 2.03;
                y *= 2.03;
            }
            return sum / norm;
        }

        private static double ValueNoise(double x, double y, uint seed)
        {
            int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y);
            double fx = x - xi, fy = y - yi;
            double ux = fx * fx * (3 - 2 * fx), uy = fy * fy * (3 - 2 * fy);
            double a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed);
            double c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
            return a + (b - a) * ux + (c - a) * uy + (a - b - c + d) * ux * uy;
        }

        /// <summary>Lattice value 0..1 from integer coordinates; plain integer arithmetic, so every machine agrees.</summary>
        private static double Hash(int x, int y, uint seed)
        {
            unchecked
            {
                uint h = (uint)x * 0x8DA6B343u ^ (uint)y * 0xD8163841u ^ seed * 0xCB1AB31Fu;
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                h *= 0x297A2D39u;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / 16777215.0;
            }
        }

        private static (byte A, byte R, byte G, byte B) Argb(string hex)
        {
            var c = GeometryHelper.ParseHexColor(hex);
            return (c.A, c.R, c.G, c.B);
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
                int row = y * width * 4;
                for (int i = 0; i < width * 4; i++)
                {
                    byte left = i >= 4 ? rgba[row + i - 4] : (byte)0;
                    raw[o++] = unchecked((byte)(rgba[row + i] - left));
                }
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
