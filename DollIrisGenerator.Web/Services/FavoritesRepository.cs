using DollIrisGenerator.Models;
using DollIrisGenerator.Services;

namespace DollIrisGenerator.Web.Services;

/// <summary>
/// The favorites list, kept in the browser (IndexedDB) as the same text the desktop app writes to favorites.json
/// and *.irisfav.json, so export / import between the two is a plain file copy.
/// Saved after every change, like the desktop app.
/// </summary>
public sealed class FavoritesRepository
{
    private const string StorageKey = "favorites.json";

    /// <summary>Largest file accepted for import (same limit as the desktop app).</summary>
    public const long MaxImportBytes = SafeFileReader.DefaultMaxBytes;

    private readonly BrowserApi _browser;
    private bool _loaded;

    // Set when the stored list could not be read: saving would overwrite the only copy
    private string? _saveBlockedReason;

    public FavoritesRepository(BrowserApi browser)
    {
        _browser = browser;
    }

    public List<FavoriteItem> Items { get; } = new();

    public event Action? Changed;

    /// <returns>A message for the user when the stored list could not be read; otherwise null.</returns>
    public async Task<string?> LoadAsync()
    {
        if (_loaded) return null;
        _loaded = true;

        string? text;
        try
        {
            text = await _browser.LoadAsync(StorageKey);
        }
        catch (Exception ex)
        {
            _saveBlockedReason = "保存領域を開けなかったため、お気に入りを保存しません。";
            return $"お気に入りの保存領域を開けませんでした ({ex.Message})。このブラウザではお気に入りを保存できません。";
        }
        if (string.IsNullOrWhiteSpace(text)) return null;

        try
        {
            var list = FavoritesStore.ParseImportText(text, out _) ?? new List<FavoriteItem>();
            Items.Clear();
            Items.AddRange(list.Where(f => f?.Parameters != null));
            Changed?.Invoke();
            return null;
        }
        catch (Exception ex)
        {
            // Keep the unreadable text where it is and stop saving, so it can still be recovered
            _saveBlockedReason = "読み込めなかったお気に入りを上書きしないよう、保存を停止しています。";
            return $"保存されていたお気に入りを読み込めませんでした。上書きしないよう、今回はお気に入りを保存しません。\n理由: {ex.Message}";
        }
    }

    /// <returns>An error message when the list could not be saved; otherwise null.</returns>
    public async Task<string?> SaveAsync()
    {
        Changed?.Invoke();
        if (_saveBlockedReason != null) return _saveBlockedReason;
        try
        {
            await _browser.SaveAsync(StorageKey, FavoritesStore.Serialize(Items));
            return null;
        }
        catch (Exception ex)
        {
            return $"お気に入りを保存できませんでした: {ex.Message}";
        }
    }

    /// <summary>「〇〇 のコピー」, then 「〇〇 のコピー (2)」… (same rule as the desktop app).</summary>
    public string MakeCopyName(string name)
    {
        string baseName = $"{name} のコピー";
        if (Items.All(f => f.Name != baseName)) return baseName;
        for (int n = 2; ; n++)
        {
            string candidate = $"{baseName} ({n})";
            if (Items.All(f => f.Name != candidate)) return candidate;
        }
    }
}
