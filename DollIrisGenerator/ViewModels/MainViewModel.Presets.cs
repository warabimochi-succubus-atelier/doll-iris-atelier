using System.IO;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;

namespace DollIrisGenerator.ViewModels
{
    // Presets and favorites: add / import / export, and local persistence of the favorites list
    public partial class MainViewModel
    {
        private void OnAddCurrentAsPreset()
        {
            var customPreset = new PresetDefinition
            {
                Name = $"カスタム: {CurrentIris.Name}",
                Description = $"ユーザー作成 ({CurrentIris.SizeMm:F1}mm / {CurrentIris.BaseColor})",
                BaseColor = CurrentIris.BaseColor,
                InnerColor = CurrentIris.InnerColor,
                OuterColor = CurrentIris.OuterColor,
                BottomHighlightColor = CurrentIris.BottomHighlightColor,
                LimbalRingColor = CurrentIris.LimbalRingColor,
                PupilColor = CurrentIris.PupilColor,
                RayColor = CurrentIris.RayColor,
                ParticleColor = CurrentIris.ParticleColor,
                PupilShape = CurrentIris.PupilShape,
                InnerGradientRadius = CurrentIris.InnerGradientRadius,
                OuterGradientRadius = CurrentIris.OuterGradientRadius
            };

            Presets.Add(customPreset);
            SelectedPreset = customPreset;
            StatusMessage = $"現在のカラーパレットを新規プリセット「{customPreset.Name}」として追加しました。";
        }

        private void OnExportPresets()
        {
            string? path = _dialogs.PickSaveFile(
                "虹彩カラーパレット (*.irispalette.json)|*.irispalette.json|JSON ファイル (*.json)|*.json",
                "MyDollIris_ColorPalettes.irispalette.json",
                "カラープリセットパレットを書き出し");
            if (path == null) return;

            try
            {
                ColorPresets.ExportPalettesToFile(path, Presets);
                StatusMessage = $"カラーパレットをエクスポートしました: {Path.GetFileName(path)}";
            }
            catch (Exception ex)
            {
                _dialogs.ShowError($"エクスポートエラー: {ex.Message}", "エラー");
            }
        }

        private void OnImportPresets()
        {
            string? path = _dialogs.PickOpenFile(
                "虹彩カラーパレット (*.irispalette.json)|*.irispalette.json|JSON ファイル (*.json)|*.json",
                "カラープリセットパレットを読み込み");
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
                    StatusMessage = $"カラーパレットから {addedCount} 件のプリセットを読み込みました。";
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
                var loaded = FavoritesStore.ReadFromFile(path);
                if (loaded != null)
                {
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
                    StatusMessage = $"お気に入りファイルから {addedCount} 件を読み込みました。";
                }
            }
            catch (Exception ex)
            {
                _dialogs.ShowError($"読み込みエラー: {ex.Message}", "エラー");
            }
        }
    }
}
