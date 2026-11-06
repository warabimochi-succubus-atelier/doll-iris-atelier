using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using DollIrisGenerator.Models;

namespace DollIrisGenerator.Services
{
    public class WindowSettings
    {
        public double Width { get; set; } = 1440;
        public double Height { get; set; } = 880;
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public WindowState WindowState { get; set; } = WindowState.Normal;

        /// <summary>RGB or CMYK sliders last used in the color palette (older files without it: RGB).</summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ColorInputMode ColorInputMode { get; set; } = ColorInputMode.Rgb;

        /// <summary>
        /// Groups 「✨ 属性ランダム生成」 leaves unchanged, written as names ("Colors, Pupil").
        /// Older files without it: nothing locked.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public RandomizeLocks RandomizeLocks { get; set; } = RandomizeLocks.None;
    }

    public static class WindowSettingsService
    {
        public static string DefaultSettingsFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DollIrisAtelier",
            "windowsettings.json");

        public static WindowSettings LoadSettings(string? customFilePath = null)
        {
            string path = customFilePath ?? DefaultSettingsFilePath;
            try
            {
                // If not found in primary path, check legacy folder
                if (!File.Exists(path) && customFilePath == null)
                {
                    string legacyPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "DollIrisGenerator",
                        "windowsettings.json");
                    if (File.Exists(legacyPath))
                    {
                        path = legacyPath;
                    }
                }

                if (File.Exists(path))
                {
                    string json = SafeFileReader.ReadAllText(path);
                    var settings = JsonSerializer.Deserialize<WindowSettings>(json);
                    if (settings != null)
                    {
                        return Sanitize(settings);
                    }
                }
            }
            catch (Exception ex)
            {
                // Fallback to safe defaults if settings file is corrupted or inaccessible
                System.Diagnostics.Debug.WriteLine($"ウィンドウ設定の読み込みに失敗: {ex.Message}");
            }

            return new WindowSettings();
        }

        public static void SaveSettings(Window window, string? customFilePath = null, ColorInputMode colorInputMode = ColorInputMode.Rgb,
            RandomizeLocks randomizeLocks = RandomizeLocks.None)
        {
            string path = customFilePath ?? DefaultSettingsFilePath;
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var settings = new WindowSettings
                {
                    ColorInputMode = colorInputMode,
                    RandomizeLocks = RandomizeLocksInfo.Sanitize(randomizeLocks)
                };

                if (window.WindowState == WindowState.Maximized)
                {
                    settings.WindowState = WindowState.Maximized;
                    // When maximized, RestoreBounds holds the unmaximized dimensions
                    var bounds = window.RestoreBounds;
                    if (bounds.Width > 0 && bounds.Height > 0)
                    {
                        settings.Width = bounds.Width;
                        settings.Height = bounds.Height;
                    }
                    else
                    {
                        settings.Width = window.ActualWidth > 0 ? window.ActualWidth : window.Width;
                        settings.Height = window.ActualHeight > 0 ? window.ActualHeight : window.Height;
                    }
                }
                else
                {
                    settings.WindowState = WindowState.Normal;
                    settings.Width = window.ActualWidth > 0 ? window.ActualWidth : window.Width;
                    settings.Height = window.ActualHeight > 0 ? window.ActualHeight : window.Height;
                }

                var opt = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(settings, opt);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                // Graceful failure: avoid crashing on application shutdown
                System.Diagnostics.Debug.WriteLine($"ウィンドウ設定の保存に失敗: {ex.Message}");
            }
        }

        public static void ApplySettings(Window window, WindowSettings? settings = null)
        {
            settings ??= LoadSettings();

            if (settings.Width >= window.MinWidth)
            {
                window.Width = settings.Width;
            }

            if (settings.Height >= window.MinHeight)
            {
                window.Height = settings.Height;
            }

            if (settings.WindowState == WindowState.Maximized)
            {
                window.WindowState = WindowState.Maximized;
            }
        }

        public static WindowSettings Sanitize(WindowSettings settings)
        {
            const double minWidth = 1100.0;
            const double minHeight = 700.0;
            const double maxWidth = 8192.0;
            const double maxHeight = 8192.0;

            if (double.IsNaN(settings.Width) || double.IsInfinity(settings.Width) || settings.Width < minWidth)
            {
                settings.Width = 1440.0;
            }
            else
            {
                settings.Width = Math.Min(settings.Width, maxWidth);
            }

            if (double.IsNaN(settings.Height) || double.IsInfinity(settings.Height) || settings.Height < minHeight)
            {
                settings.Height = 880.0;
            }
            else
            {
                settings.Height = Math.Min(settings.Height, maxHeight);
            }

            if (settings.WindowState == WindowState.Minimized)
            {
                settings.WindowState = WindowState.Normal;
            }

            if (!Enum.IsDefined(settings.ColorInputMode))
            {
                settings.ColorInputMode = ColorInputMode.Rgb;
            }

            // Unknown bits (hand-edited numbers) are dropped; known groups stay locked
            settings.RandomizeLocks = RandomizeLocksInfo.Sanitize(settings.RandomizeLocks);

            return settings;
        }
    }
}
