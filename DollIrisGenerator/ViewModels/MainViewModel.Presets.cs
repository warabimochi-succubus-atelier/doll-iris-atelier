using System.Collections;
using System.IO;
using System.Text.Json;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;

namespace DollIrisGenerator.ViewModels
{
    // Presets and favorites: add / import / export, and local persistence of the favorites list
    public partial class MainViewModel
    {
        private void OnAddCurrentAsPreset()
        {
            // Same settings as a built-in preset holds (colors, pupil, rays, sparkles, layers, symbol)
            var customPreset = ColorPresets.FromDesign(
                CurrentIris,
                $"カスタム: {CurrentIris.Name}",
                $"ユーザー作成 ({CurrentIris.SizeMm:F1}mm / {CurrentIris.BaseColor})");

            Presets.Add(customPreset);
            SelectedPreset = customPreset;
            StatusMessage = $"現在のデザインを虹彩プリセット「{customPreset.Name}」として追加しました。";
        }

        private void OnExportPresets()
        {
            string? path = _dialogs.PickSaveFile(
                "虹彩プリセット (*.irispalette.json)|*.irispalette.json|JSON ファイル (*.json)|*.json",
                "MyDollIris_Presets.irispalette.json",
                "虹彩プリセットを書き出し");
            if (path == null) return;

            try
            {
                ColorPresets.ExportPalettesToFile(path, Presets);
                StatusMessage = $"虹彩プリセットを書き出しました: {Path.GetFileName(path)}";
            }
            catch (Exception ex)
            {
                _dialogs.ShowError($"エクスポートエラー: {ex.Message}", "エラー");
            }
        }

        private void OnImportPresets()
        {
            string? path = _dialogs.PickOpenFile(
                "虹彩プリセット (*.irispalette.json)|*.irispalette.json|JSON ファイル (*.json)|*.json",
                "虹彩プリセットを読み込み");
            if (path == null) return;

            try
            {
                var loaded = ColorPresets.ImportPalettesFromFile(path);
                if (loaded != null && loaded.Count > 0)
                {
                    int addedCount = 0;
                    foreach (var p in loaded)
                    {
                        if (p != null && !string.IsNullOrWhiteSpace(p.Name) && !Presets.Any(x => x != null && x.Name == p.Name))
                        {
                            Presets.Add(p);
                            addedCount++;
                        }
                    }
                    StatusMessage = $"ファイルから {addedCount} 件の虹彩プリセットを読み込みました。";
                }
            }
            catch (Exception ex)
            {
                _dialogs.ShowError($"インポートエラー: {ex.Message}", "エラー");
            }
        }

        /// <summary>Tells the user if the saved favorites could not be read. Call once the main window is showing.</summary>
        public void ShowFavoritesLoadProblem()
        {
            if (_favoritesLoadProblem == null) return;
            string problem = _favoritesLoadProblem;
            _favoritesLoadProblem = null;
            _dialogs.ShowWarning(problem, "お気に入りの読み込み");
        }

        private void SaveFavorites()
        {
            if (!_favoritesStore.TrySavePersisted(Favorites, out string error))
            {
                _dialogs.ShowWarning(
                    $"お気に入りを保存できませんでした。このまま終了すると、今回の変更は失われます。\n\n理由: {error}",
                    "お気に入りの保存");
            }
        }

        private void OnAddFavorite()
        {
            var favorite = new FavoriteItem(CurrentIris);
            Favorites.Insert(0, favorite);
            SaveFavorites();
            StatusMessage = $"「{CurrentIris.Name}」をお気に入りに追加しました (計 {Favorites.Count} 件)。";
        }

        private void OnRemoveFavorite(object? param)
        {
            if (param is FavoriteItem item)
            {
                Favorites.Remove(item);
                SaveFavorites();
                StatusMessage = "お気に入りから削除しました。";
            }
        }

        private void OnLoadFavorite(object? param)
        {
            if (param is FavoriteItem item)
            {
                PushUndo();
                CurrentIris = item.Parameters.Clone();
                RecordHistory($"お気に入り復元: {item.Name}");
                StatusMessage = $"お気に入り「{item.Name}」のパラメータを読み込みました。";
            }
        }

        /// <summary>
        /// Copies a favorite and puts the copy right below it. The copy gets a new name (「〇〇 のコピー」, then
        /// 「〇〇 のコピー (2)」…) on both the list entry and the design itself, so the two can be told apart.
        /// </summary>
        private void OnDuplicateFavorite(object? param)
        {
            var source = param as FavoriteItem ?? SelectedFavorite;
            if (source == null) return;

            string newName = MakeCopyName(source.Name);
            var parameters = source.Parameters.Clone();
            parameters.Name = newName;
            var copy = new FavoriteItem(parameters, source.Note) { Name = newName };

            int index = Favorites.IndexOf(source);
            Favorites.Insert(index < 0 ? 0 : index + 1, copy);
            SelectedFavorite = copy;
            SaveFavorites();
            StatusMessage = $"お気に入り「{source.Name}」を「{newName}」として複製しました (計 {Favorites.Count} 件)。";
        }

        private string MakeCopyName(string name)
        {
            string baseName = $"{name} のコピー";
            if (!Favorites.Any(f => f.Name == baseName)) return baseName;
            for (int n = 2; ; n++)
            {
                string candidate = $"{baseName} ({n})";
                if (!Favorites.Any(f => f.Name == candidate)) return candidate;
            }
        }

        /// <summary>The favorites a command acts on: the list's selected items, one favorite, or the current selection.</summary>
        private List<FavoriteItem> ResolveFavorites(object? param)
        {
            var picked = param switch
            {
                IList list => list.OfType<FavoriteItem>().ToList(),
                FavoriteItem single => new List<FavoriteItem> { single },
                _ => SelectedFavorite != null ? new List<FavoriteItem> { SelectedFavorite } : new List<FavoriteItem>()
            };
            // Keep the order of the favorites list, not the order the user clicked them in
            return picked.Distinct().OrderBy(f => Favorites.IndexOf(f)).ToList();
        }

        private bool CanAddFavoritesToSheet(object? param) => ResolveFavorites(param).Count > 0;

        /// <summary>
        /// Adds the chosen favorites to the A4 sheet. A design already on the sheet gets its quantity raised by 1
        /// instead of a second row, so the sheet list stays easy to read.
        /// </summary>
        private void OnAddFavoritesToSheet(object? param)
        {
            var chosen = ResolveFavorites(param);
            if (chosen.Count == 0) return;

            int added = 0, increased = 0, atLimit = 0;
            foreach (var favorite in chosen)
            {
                string key = JsonSerializer.Serialize(favorite.Parameters);
                var existing = SheetItems.FirstOrDefault(i => JsonSerializer.Serialize(i.Parameters) == key);
                if (existing == null)
                {
                    SheetItems.Add(new SheetItem(favorite.Parameters.Clone(), 1));
                    added++;
                }
                else if (existing.PairCount < SheetItem.MaxPairCount)
                {
                    existing.PairCount++;
                    increased++;
                }
                else
                {
                    atLimit++;
                }
            }

            string what = chosen.Count == 1 ? $"「{chosen[0].Name}」" : $"{chosen.Count} 件のお気に入り";
            var parts = new List<string>();
            if (added > 0) parts.Add($"新規 {added} 行");
            if (increased > 0) parts.Add($"既存 {increased} 行の数量 +1");
            if (atLimit > 0) parts.Add($"{atLimit} 件は上限 {SheetItem.MaxPairCount} セットのため追加せず");
            StatusMessage = $"{what}をA4シートに追加しました ({string.Join(" / ", parts)})。" +
                            $"シート: {SheetRowCount} 行 / 数量合計 {SheetTotalPairs} セット。";
        }

        private void OnExportFavorites()
        {
            string? path = _dialogs.PickSaveFile(
                "虹彩お気に入りファイル (*.irisfav.json)|*.irisfav.json|JSON ファイル (*.json)|*.json",
                "MyDollIris_Favorites.irisfav.json",
                "お気に入り一覧をファイルに書き出し");
            if (path == null) return;

            try
            {
                FavoritesStore.WriteToFile(path, Favorites);
                StatusMessage = $"お気に入り一覧を保存しました: {Path.GetFileName(path)}";
            }
            catch (Exception ex)
            {
                _dialogs.ShowError($"保存エラー: {ex.Message}", "エラー");
            }
        }

        private void OnImportFavorites()
        {
            string? path = _dialogs.PickOpenFile(
                "虹彩お気に入りファイル (*.irisfav.json)|*.irisfav.json|JSON ファイル (*.json)|*.json",
                "お気に入り一覧をファイルから読み込み");
            if (path == null) return;

            try
            {
                var loaded = FavoritesStore.ReadImportFile(path, out string? singleDesignJson);
                if (loaded != null)
                {
                    // A single design saved by an older version may hold an eye-sized value such as 24 mm: say it was clamped
                    string? sizeNote = singleDesignJson != null && loaded.Count == 1
                        ? DesignFileWorkflow.DescribeSizeAdjustment(singleDesignJson, loaded[0].Parameters.SizeMm)
                        : null;
                    int addedCount = 0;
                    foreach (var fav in loaded)
                    {
                        if (fav != null && fav.Parameters != null && !Favorites.Any(x => x.Id == fav.Id))
                        {
                            Favorites.Add(fav);
                            addedCount++;
                        }
                    }
                    SaveFavorites();
                    StatusMessage = $"お気に入りファイルから {addedCount} 件を読み込みました。" +
                                    (sizeNote == null ? "" : $" ({sizeNote})");
                }
            }
            catch (Exception ex)
            {
                _dialogs.ShowError($"読み込みエラー: {ex.Message}", "エラー");
            }
        }
    }
}
