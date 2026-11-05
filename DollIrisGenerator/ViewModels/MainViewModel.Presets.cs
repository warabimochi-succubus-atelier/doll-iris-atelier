using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml;
using System.Xml.Linq;
using DollIrisGenerator.Controls;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using Microsoft.Win32;

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
            var sfd = new SaveFileDialog
            {
                Filter = "虹彩カラーパレット (*.irispalette.json)|*.irispalette.json|JSON ファイル (*.json)|*.json",
                FileName = "MyDollIris_ColorPalettes.irispalette.json",
                Title = "カラープリセットパレットを書き出し"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    ColorPresets.ExportPalettesToFile(sfd.FileName, Presets);
                    StatusMessage = $"カラーパレットをエクスポートしました: {Path.GetFileName(sfd.FileName)}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"エクスポートエラー: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OnImportPresets()
        {
            var ofd = new OpenFileDialog
            {
                Filter = "虹彩カラーパレット (*.irispalette.json)|*.irispalette.json|JSON ファイル (*.json)|*.json",
                Title = "カラープリセットパレットを読み込み"
            };

            if (ofd.ShowDialog() == true)
            {
                try
                {
                    var loaded = ColorPresets.ImportPalettesFromFile(ofd.FileName);
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
                    MessageBox.Show($"インポートエラー: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OnAddFavorite()
        {
            var favorite = new FavoriteItem(CurrentIris);
            Favorites.Insert(0, favorite);
            SavePersistedFavorites();
            StatusMessage = $"「{CurrentIris.Name}」をお気に入りに追加しました (計 {Favorites.Count} 件)。";
        }

        private void OnRemoveFavorite(object? param)
        {
            if (param is FavoriteItem item)
            {
                Favorites.Remove(item);
                SavePersistedFavorites();
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
            var sfd = new SaveFileDialog
            {
                Filter = "虹彩お気に入りファイル (*.irisfav.json)|*.irisfav.json|JSON ファイル (*.json)|*.json",
                FileName = "MyDollIris_Favorites.irisfav.json",
                Title = "お気に入り一覧をファイルに書き出し"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    var opt = new JsonSerializerOptions { WriteIndented = true };
                    string json = JsonSerializer.Serialize(Favorites, opt);
                    File.WriteAllText(sfd.FileName, json);
                    StatusMessage = $"お気に入り一覧を保存しました: {Path.GetFileName(sfd.FileName)}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"保存エラー: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OnImportFavorites()
        {
            var ofd = new OpenFileDialog
            {
                Filter = "虹彩お気に入りファイル (*.irisfav.json)|*.irisfav.json|JSON ファイル (*.json)|*.json",
                Title = "お気に入り一覧をファイルから読み込み"
            };

            if (ofd.ShowDialog() == true)
            {
                try
                {
                    string json = SafeFileReader.ReadAllText(ofd.FileName);
                    var loaded = JsonSerializer.Deserialize<List<FavoriteItem>>(json);
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
                        SavePersistedFavorites();
                        StatusMessage = $"お気に入りファイルから {addedCount} 件を読み込みました。";
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"読み込みエラー: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SavePersistedFavorites()
        {
            try
            {
                var dir = Path.GetDirectoryName(_favoritesFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                var opt = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(Favorites, opt);
                File.WriteAllText(_favoritesFilePath, json);
            }
            catch (Exception ex)
            {
                // Ignore local persistence failure (the in-memory list keeps working), but leave a trace for debugging
                System.Diagnostics.Debug.WriteLine($"お気に入りの保存に失敗: {ex.Message}");
            }
        }

        private void LoadPersistedFavorites()
        {
            try
            {
                string targetPath = _favoritesFilePath;
                // If not yet in AppData, check legacy locations for seamless migration
                if (!File.Exists(targetPath))
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
                    string json = SafeFileReader.ReadAllText(targetPath);
                    var loaded = JsonSerializer.Deserialize<List<FavoriteItem>>(json);
                    if (loaded != null)
                    {
                        foreach (var fav in loaded)
                        {
                            if (fav != null && fav.Parameters != null)
                            {
                                Favorites.Add(fav);
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
        }
    }
}
