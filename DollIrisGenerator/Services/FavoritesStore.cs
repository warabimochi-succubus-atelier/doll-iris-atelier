using System.IO;
using System.Text;
using System.Text.Json;
using DollIrisGenerator.Models;

namespace DollIrisGenerator.Services
{
    /// <param name="Favorites">The favorites that could be read. Empty when the file was unreadable.</param>
    /// <param name="Problem">A message for the user when the saved file could not be read; otherwise null.</param>
    public sealed record FavoritesLoadResult(IReadOnlyList<FavoriteItem> Favorites, string? Problem);

    /// <summary>Reads and writes favorites: the auto-saved local list, and files the user exports or imports.</summary>
    public sealed class FavoritesStore
    {
        private readonly string _filePath;
        private readonly bool _checkLegacyLocations;

        // Set when an unreadable file could not be set aside: saving would destroy the only copy of the user's data
        private string? _saveBlockedReason;

        /// <param name="filePath">Where to auto-save. Defaults to the user's AppData folder.</param>
        public FavoritesStore(string? filePath = null)
        {
            _checkLegacyLocations = filePath == null;
            _filePath = filePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DollIrisAtelier",
                "favorites.json");
        }

        /// <returns>False when the list could not be saved; <paramref name="error"/> then says why.</returns>
        public bool TrySavePersisted(IEnumerable<FavoriteItem> favorites, out string error)
        {
            if (_saveBlockedReason != null)
            {
                error = _saveBlockedReason;
                return false;
            }

            try
            {
                var dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                WriteToFile(_filePath, favorites);
                error = "";
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"お気に入りの保存に失敗: {ex.Message}");
                error = ex.Message;
                return false;
            }
        }

        public FavoritesLoadResult LoadPersisted()
        {
            string targetPath = _filePath;
            // If not yet in AppData, check legacy locations for seamless migration
            if (_checkLegacyLocations && !File.Exists(targetPath))
            {
                string legacyAppData = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "DollIrisGenerator",
                    "favorites.json");
                string legacyBase = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "favorites.json");

                if (File.Exists(legacyAppData))
                {
                    targetPath = legacyAppData;
                }
                else if (File.Exists(legacyBase))
                {
                    targetPath = legacyBase;
                }
            }

            var favorites = new List<FavoriteItem>();
            if (!File.Exists(targetPath)) return new FavoritesLoadResult(favorites, null);

            try
            {
                var loaded = ReadFromFile(targetPath);
                if (loaded != null)
                {
                    foreach (var fav in loaded)
                    {
                        if (fav != null && fav.Parameters != null)
                        {
                            favorites.Add(fav);
                        }
                    }
                }
                return new FavoritesLoadResult(favorites, null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"お気に入りの読み込みに失敗: {ex.Message}");
                return new FavoritesLoadResult(new List<FavoriteItem>(), SetAsideUnreadableFile(targetPath, ex.Message));
            }
        }

        /// <summary>Keeps an unreadable file out of the way of the next save, so its contents can still be recovered.</summary>
        /// <returns>The message for the user.</returns>
        private string SetAsideUnreadableFile(string path, string reason)
        {
            try
            {
                var dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                string backupPath = Path.Combine(dir ?? "", $"favorites.broken-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                if (path == _filePath)
                {
                    File.Move(path, backupPath);
                }
                else
                {
                    File.Copy(path, backupPath); // a legacy location is left as it was
                }
                return $"お気に入りファイルを読み込めませんでした。お気に入りは空の状態で起動します。\n\n" +
                       $"元のファイルは次の場所に残しています:\n{backupPath}\n\n理由: {reason}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"お気に入りファイルの退避に失敗: {ex.Message}");
                _saveBlockedReason = $"読み込めなかったファイルを上書きしないよう、保存を停止しています: {path}";
                return $"お気に入りファイルを読み込めませんでした。お気に入りは空の状態で起動します。\n\n" +
                       $"元のファイルを退避できなかったため、上書きしないよう、今回はお気に入りを保存しません:\n{path}\n\n理由: {reason}";
            }
        }

        /// <summary>
        /// Writes through a temporary file and then replaces the target, so a crash or power loss
        /// during the write can't leave a truncated file behind.
        /// </summary>
        /// <exception cref="Exception">Any write failure; the caller reports it to the user.</exception>
        public static void WriteToFile(string path, IEnumerable<FavoriteItem> favorites)
        {
            var opt = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(favorites, opt);

            string tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
            try
            {
                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write))
                {
                    byte[] bytes = new UTF8Encoding(false).GetBytes(json);
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(flushToDisk: true);
                }
                File.Move(tempPath, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        /// <exception cref="Exception">Any read or parse failure; the caller decides how to report it.</exception>
        public static List<FavoriteItem>? ReadFromFile(string path)
        {
            string json = SafeFileReader.ReadAllText(path);
            return JsonSerializer.Deserialize<List<FavoriteItem>>(json);
        }
    }
}
