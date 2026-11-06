using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using DollIrisGenerator.Controls;
using DollIrisGenerator.Services;
using Xunit;

namespace DollIrisGenerator.Tests
{
    /// <summary>CMYK entry in the color palette: conversion to / from the stored sRGB hex.</summary>
    [Collection(ColorPickerBoxUiCollection.Name)]
    public class CmykColorTests
    {
        [Theory]
        [InlineData(255, 255, 255, 0, 0, 0, 0)]
        [InlineData(0, 0, 0, 0, 0, 0, 100)]
        [InlineData(255, 0, 0, 0, 100, 100, 0)]
        [InlineData(0, 255, 255, 100, 0, 0, 0)]
        [InlineData(0, 0, 255, 100, 100, 0, 0)]
        [InlineData(128, 128, 128, 0, 0, 0, 49.8)]
        public void FromRgb_KnownColors(byte r, byte g, byte b, double c, double m, double y, double k)
        {
            var cmyk = CmykColor.FromRgb(r, g, b);
            Assert.Equal(c, cmyk.C, 1);
            Assert.Equal(m, cmyk.M, 1);
            Assert.Equal(y, cmyk.Y, 1);
            Assert.Equal(k, cmyk.K, 1);
        }

        [Fact]
        public void FromRgb_AppBlue()
        {
            // #2979FF (the default color): C 84 / M 53 / Y 0 / K 0 when shown in whole percents
            var cmyk = CmykColor.FromRgb(0x29, 0x79, 0xFF);
            Assert.Equal("84", CmykColor.FormatPercent(cmyk.C));
            Assert.Equal("53", CmykColor.FormatPercent(cmyk.M));
            Assert.Equal("0", CmykColor.FormatPercent(cmyk.Y));
            Assert.Equal("0", CmykColor.FormatPercent(cmyk.K));
        }

        [Fact]
        public void RgbToCmykToRgb_IsExactForEveryColor()
        {
            // All 16.7 M colors: the held (unrounded) CMYK always gives back the same #RRGGBB
            for (int r = 0; r < 256; r++)
                for (int g = 0; g < 256; g++)
                    for (int b = 0; b < 256; b++)
                    {
                        var cmyk = CmykColor.FromRgb((byte)r, (byte)g, (byte)b);
                        var (r2, g2, b2) = cmyk.ToRgb();
                        if (r2 != r || g2 != g || b2 != b)
                            Assert.Fail($"#{r:X2}{g:X2}{b:X2} → {cmyk} → #{r2:X2}{g2:X2}{b2:X2}");
                    }
        }

        [Fact]
        public void WholePercentCmyk_RoundTripsThroughRgb_WithinOnePercent()
        {
            // Whole-percent CMYK → RGB → CMYK is not exact (the RGB is rounded to 0-255), which is why the
            // UI keeps the entered values (Resolve). For CMYK in the form FromRgb gives (one of C/M/Y = 0)
            // and K up to 50 %, re-converting stays within 1 % and gives the same color again.
            for (int c = 0; c <= 100; c += 3)
                for (int m = 0; m <= 100; m += 7)
                    for (int k = 0; k <= 50; k += 5)
                    {
                        var cmyk = new CmykColor(c, m, 0, k);
                        var (r, g, b) = cmyk.ToRgb();
                        var back = CmykColor.FromRgb(r, g, b);
                        for (int i = 0; i < 4; i++) Assert.InRange(Math.Abs(back[i] - cmyk[i]), 0, 1.0);
                        Assert.Equal(cmyk.ToHex(), back.ToHex());
                    }
        }

        [Fact]
        public void Resolve_KeepsHeldValuesWhileTheyGiveTheSameColor()
        {
            // The user typed C 33 / M 0 / Y 0 / K 0 → stored #ABFFFF; FromRgb of that would be 32.9 %
            var held = new CmykColor(33, 0, 0, 0);
            var (r, g, b) = held.ToRgb();
            Assert.NotEqual(33.0, CmykColor.FromRgb(r, g, b).C);
            Assert.Equal(held, CmykColor.Resolve(held, r, g, b));

            // C / M / Y at K = 100 % have no effect on the color but stay as entered
            var black = new CmykColor(40, 20, 10, 100);
            Assert.Equal("#000000", black.ToHex());
            Assert.Equal(black, CmykColor.Resolve(black, 0, 0, 0));
        }

        [Fact]
        public void Resolve_ConvertsAgainWhenTheColorChangedElsewhere()
        {
            var held = new CmykColor(33, 0, 0, 0);
            Assert.Equal(CmykColor.FromRgb(255, 0, 0), CmykColor.Resolve(held, 255, 0, 0));
            Assert.Equal(CmykColor.FromRgb(1, 2, 3), CmykColor.Resolve(null, 1, 2, 3));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void ChangingOneInk_OnlyChangesItsOwnRgbChannel(int channel)
        {
            // Dragging C must not move G / B by a rounding step (the other values are kept exact)
            var start = CmykColor.FromRgb(0x29, 0x79, 0xFF);
            var (r0, g0, b0) = start.ToRgb();
            for (int p = 0; p <= 100; p++)
            {
                var (r, g, b) = start.With(channel, p).ToRgb();
                if (channel != 0) Assert.Equal(r0, r);
                if (channel != 1) Assert.Equal(g0, g);
                if (channel != 2) Assert.Equal(b0, b);
            }
        }

        [Fact]
        public void HeldValues_DoNotDriftOverRepeatedEdits()
        {
            // Simulates a slider drag: each step resolves against the current color, then sets C
            CmykColor? held = null;
            byte r = 0x80, g = 0x40, b = 0x20;
            var first = CmykColor.Resolve(held, r, g, b);
            for (int p = 0; p <= 100; p++)
            {
                var cmyk = CmykColor.Resolve(held, r, g, b).With(0, p);
                held = cmyk;
                (r, g, b) = cmyk.ToRgb();
                Assert.Equal(first.M, cmyk.M);
                Assert.Equal(first.Y, cmyk.Y);
                Assert.Equal(first.K, cmyk.K);
                Assert.Equal(p, cmyk.C);
            }
        }

        [Fact]
        public void With_ClampsToZeroToHundred()
        {
            var cmyk = new CmykColor(10, 20, 30, 40);
            Assert.Equal(100, cmyk.With(0, 150).C);
            Assert.Equal(0, cmyk.With(3, -5).K);
            Assert.Equal(0, cmyk.With(1, double.NaN).M);
            Assert.Throws<ArgumentOutOfRangeException>(() => cmyk.With(4, 1));
        }

        [Fact]
        public void ToRgb_ClampsOutOfRangeValues()
        {
            Assert.Equal("#000000", new CmykColor(-10, 200, 50, 100).ToHex());
            Assert.Equal("#FF0000", new CmykColor(-10, 200, 150, -1).ToHex());
        }

        [Theory]
        [InlineData("50", 50)]
        [InlineData(" 12.5 ", 12.5)]
        [InlineData("12,5", 12.5)]
        [InlineData("80%", 80)]
        [InlineData("80％", 80)]
        [InlineData("120", 100)]
        [InlineData("-3", 0)]
        public void TryParsePercent_Accepts(string text, double expected)
        {
            Assert.True(CmykColor.TryParsePercent(text, out double v));
            Assert.Equal(expected, v);
        }

        [Theory]
        [InlineData("")]
        [InlineData("abc")]
        [InlineData("NaN")]
        [InlineData(null)]
        public void TryParsePercent_Rejects(string? text)
        {
            Assert.False(CmykColor.TryParsePercent(text, out _));
        }

        [Fact]
        public void FormatPercent_RoundsToWholePercent()
        {
            Assert.Equal("33", CmykColor.FormatPercent(33.3));
            Assert.Equal("50", CmykColor.FormatPercent(49.5));
            Assert.Equal("0", CmykColor.FormatPercent(0));
            Assert.Equal("100", CmykColor.FormatPercent(100));
        }

        // ------------------------------------------------------------------
        // Color box UI
        // ------------------------------------------------------------------

        private static void RunOnSta(Action body)
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try { body(); }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }

        private static T Find<T>(ColorPickerBox box, string name) where T : class
            => box.FindName(name) as T ?? throw new InvalidOperationException(name);

        [Fact]
        public void ColorBox_CmykSlidersFollowTheColor_AndSetIt()
        {
            RunOnSta(() =>
            {
                var box = new ColorPickerBox { SelectedColorHex = "#FF0000" };
                var c = Find<Slider>(box, "CSlider");
                var m = Find<Slider>(box, "MSlider");
                var k = Find<Slider>(box, "KSlider");
                var cText = Find<TextBox>(box, "CValueBox");
                var mText = Find<TextBox>(box, "MValueBox");
                Assert.Equal(0, c.Value);
                Assert.Equal(100, m.Value);
                Assert.Equal("100", mText.Text);

                // CMYK → stored hex, RGB sliders and the HEX box follow
                m.Value = 50;
                Assert.Equal("#FF8000", box.SelectedColorHex);
                Assert.Equal(128, Find<Slider>(box, "GSlider").Value);
                Assert.Equal("#FF8000", Find<TextBox>(box, "HexTextBox").Text);
                Assert.Equal("50", mText.Text);

                // K = 100 %: black, but M stays at what was entered
                k.Value = 100;
                Assert.Equal("#000000", box.SelectedColorHex);
                Assert.Equal(50, m.Value);
                k.Value = 0;
                Assert.Equal("#FF8000", box.SelectedColorHex);

                // RGB slider → CMYK follows
                Find<Slider>(box, "RSlider").Value = 0;
                Assert.Equal("#008000", box.SelectedColorHex);
                Assert.Equal("100", cText.Text);
                Assert.Equal("0", mText.Text);
                Assert.Equal("50", Find<TextBox>(box, "KValueBox").Text);

                // Value from outside (undo, preset, palette)
                box.SelectedColorHex = "#FFFFFF";
                Assert.Equal("0", cText.Text);
                Assert.Equal(0, k.Value);
            });
        }

        [Fact]
        public void ColorBox_InputModeSwitchesPanels_AndIsShared()
        {
            RunOnSta(() =>
            {
                var saved = ColorPickerBox.PreferredInputMode;
                try
                {
                    ColorPickerBox.PreferredInputMode = ColorInputMode.Rgb;
                    var box = new ColorPickerBox();
                    Assert.Equal(Visibility.Visible, Find<StackPanel>(box, "RgbPanel").Visibility);
                    Assert.Equal(Visibility.Collapsed, Find<StackPanel>(box, "CmykPanel").Visibility);

                    Find<RadioButton>(box, "CmykModeBtn").IsChecked = true;
                    Assert.Equal(ColorInputMode.Cmyk, ColorPickerBox.PreferredInputMode);
                    Assert.Equal(Visibility.Collapsed, Find<StackPanel>(box, "RgbPanel").Visibility);
                    Assert.Equal(Visibility.Visible, Find<StackPanel>(box, "CmykPanel").Visibility);

                    var next = new ColorPickerBox();
                    Assert.Equal(true, Find<RadioButton>(next, "CmykModeBtn").IsChecked);
                    Assert.Equal(Visibility.Visible, Find<StackPanel>(next, "CmykPanel").Visibility);
                }
                finally
                {
                    ColorPickerBox.PreferredInputMode = saved;
                }
            });
        }

        [Fact]
        public void WindowSettings_RemembersColorInputMode()
        {
            string dir = Path.Combine(Path.GetTempPath(), "DollIrisCmykTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string path = Path.Combine(dir, "windowsettings.json");
                File.WriteAllText(path, JsonSerializer.Serialize(new WindowSettings { ColorInputMode = ColorInputMode.Cmyk }));
                Assert.Contains("\"Cmyk\"", File.ReadAllText(path));
                Assert.Equal(ColorInputMode.Cmyk, WindowSettingsService.LoadSettings(path).ColorInputMode);

                // Files written before this setting existed: RGB
                File.WriteAllText(path, "{\"Width\":1440,\"Height\":880,\"WindowState\":\"Normal\"}");
                Assert.Equal(ColorInputMode.Rgb, WindowSettingsService.LoadSettings(path).ColorInputMode);

                // Unknown number: back to RGB
                File.WriteAllText(path, "{\"Width\":1440,\"Height\":880,\"ColorInputMode\":7}");
                Assert.Equal(ColorInputMode.Rgb, WindowSettingsService.LoadSettings(path).ColorInputMode);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }
    }
}

namespace DollIrisGenerator.Tests
{
    /// <summary>
    /// Test classes that create <see cref="ColorPickerBox"/>: run one after another. Loading the same XAML on
    /// several STA threads at once can fail inside WPF (Application.LoadComponent shares the package stream).
    /// </summary>
    [CollectionDefinition(Name)]
    public class ColorPickerBoxUiCollection
    {
        public const string Name = "ColorPickerBox UI";
    }
}
