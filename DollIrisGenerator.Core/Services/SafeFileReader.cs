using System.IO;

namespace DollIrisGenerator.Services
{
    /// <summary>
    /// Reads the small JSON files this app opens (parameters, palettes, favorites, settings).
    /// Files may come from other people (shared palettes / favorites), so refuse anything far larger
    /// than a real file instead of loading it whole into memory.
    /// </summary>
    public static class SafeFileReader
    {
        public const long DefaultMaxBytes = 10L * 1024 * 1024; // 10 MB: thousands of designs

        public static string ReadAllText(string path, long maxBytes = DefaultMaxBytes)
        {
            var info = new FileInfo(path);
            if (info.Length > maxBytes)
            {
                throw new InvalidDataException(
                    $"ファイルが大きすぎます ({info.Length / 1024 / 1024} MB)。上限は {maxBytes / 1024 / 1024} MB です。");
            }
            return File.ReadAllText(path);
        }
    }
}
