using System.IO;
using System.Text.Json;
using DollIrisGenerator.Models;

namespace DollIrisGenerator.Services
{
    /// <summary>Reads and writes favorites: the auto-saved local list, and files the user exports or imports.</summary>
    public sealed class FavoritesStore
    {
        private readonly string _filePath;
        private readonly bool _checkLegacyLocations;

        /// <param name="filePath">Where to auto-save. Defaults to the user's AppData folder.</param>
        public FavoritesStore(string? filePath = null)
        {
            _checkLegacyLocations = filePath == null;
            _filePath = filePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DollIrisAtelier",
                "favorites.json");
        }

        public void SavePersisted(IEnumerable<FavoriteItem> favorites)
        {
            try
            {
                var dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                WriteToFile(_filePath, favorites);
            }
            catch (Exception ex)
            {
                // Ignore local persistence failure (the in-memory list keeps working), but leave a trace for debugging
                System.Diagnostics.Debug.WriteLine($"お気に入りの保存に失敗: {ex.Message}");
            }
        }

        public List<FavoriteItem> LoadPersisted()
        {
            var favorites = new List<FavoriteItem>();
            try
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

                if (File.Exists(targetPath))
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
                }
            }
            catch (Exception ex)
            {
                // Ignore load error (start with an empty list), but leave a trace for debugging
                System.Diagnostics.Debug.WriteLine($"お気に入りの読み込みに失敗: {ex.Message}");
            }
            return favorites;
        }

        /// <exception cref="Exception">Any write failure; the caller reports it to the user.</exception>
        public static void WriteToFile(string path, IEnumerable<FavoriteItem> favorites)
        {
            var opt = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(favorites, opt);
            File.WriteAllText(path, json);
        }

        /// <exception cref="Exception">Any read or parse failure; the caller decides how to report it.</exception>
        public static List<FavoriteItem>? ReadFromFile(string path)
        {
            string json = SafeFileReader.ReadAllText(path);
            return JsonSerializer.Deserialize<List<FavoriteItem>>(json);
        }
    }
}
