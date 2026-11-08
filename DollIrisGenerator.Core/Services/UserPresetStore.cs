using System.IO;
using System.Text.Json;
using DollIrisGenerator.Models;

namespace DollIrisGenerator.Services
{
    /// <param name="Presets">The presets that could be read. Empty when the file was unreadable.</param>
    /// <param name="Problem">A message for the user when the saved file could not be read; otherwise null.</param>
    public sealed record UserPresetLoadResult(IReadOnlyList<PresetDefinition> Presets, string? Problem);

    /// <summary>
    /// Keeps the iris presets the user added (registered, imported, renamed) in user-presets.json, so they are
    /// there on the next start. Same protection as <see cref="FavoritesStore"/>: writes go through a temporary file,
    /// and an unreadable file is set aside instead of being overwritten.
    /// </summary>
    public sealed class UserPresetStore
    {
        private const string FileName = "user-presets.json";

        private readonly string _filePath;

        // Set when an unreadable file could not be set aside: saving would destroy the only copy of the user's data
        private string? _saveBlockedReason;

        /// <param name="filePath">Where to save. Defaults to the user's AppData folder.</param>
        public UserPresetStore(string? filePath = null)
        {
            _filePath = filePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DollIrisAtelier",
                FileName);
        }

        public UserPresetLoadResult Load()
        {
            if (!File.Exists(_filePath)) return new UserPresetLoadResult(new List<PresetDefinition>(), null);

            try
            {
                var loaded = ColorPresets.ImportPalettesFromFile(_filePath) ?? new List<PresetDefinition>();
                var presets = loaded.Where(p => p != null && !string.IsNullOrWhiteSpace(p.Name)).ToList();
                return new UserPresetLoadResult(presets, null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"追加したプリセットの読み込みに失敗: {ex.Message}");
                return new UserPresetLoadResult(new List<PresetDefinition>(), SetAsideUnreadableFile(ex.Message));
            }
        }

        /// <returns>False when the list could not be saved; <paramref name="error"/> then says why.</returns>
        public bool TrySave(IEnumerable<PresetDefinition> presets, out string error)
        {
            if (_saveBlockedReason != null)
            {
                error = _saveBlockedReason;
                return false;
            }

            try
            {
                var dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var opt = new JsonSerializerOptions { WriteIndented = true };
                AtomicFileWriter.WriteAllText(_filePath, JsonSerializer.Serialize(presets, opt));
                error = "";
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"追加したプリセットの保存に失敗: {ex.Message}");
                error = ex.Message;
                return false;
            }
        }

        /// <summary>Moves an unreadable file out of the way of the next save, so its contents can still be recovered.</summary>
        /// <returns>The message for the user.</returns>
        private string SetAsideUnreadableFile(string reason)
        {
            const string intro = "追加した虹彩プリセットのファイルを読み込めませんでした。内蔵プリセットだけで起動します。";
            try
            {
                string backupPath = Path.Combine(Path.GetDirectoryName(_filePath) ?? "",
                    $"user-presets.broken-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                File.Move(_filePath, backupPath);
                return $"{intro}\n\n元のファイルは次の場所に残しています:\n{backupPath}\n\n理由: {reason}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"プリセットファイルの退避に失敗: {ex.Message}");
                _saveBlockedReason = $"読み込めなかったファイルを上書きしないよう、保存を停止しています: {_filePath}";
                return $"{intro}\n\n元のファイルを退避できなかったため、上書きしないよう、今回は追加したプリセットを保存しません:\n{_filePath}\n\n理由: {reason}";
            }
        }
    }
}
