using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DollIrisGenerator.Services;

namespace DollIrisGenerator.Controls
{
    public partial class ColorPickerBox : UserControl
    {
        public static readonly DependencyProperty SelectedColorHexProperty =
            DependencyProperty.Register(
                nameof(SelectedColorHex),
                typeof(string),
                typeof(ColorPickerBox),
                new FrameworkPropertyMetadata("#2979FF", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedColorHexChanged));

        public string SelectedColorHex
        {
            get => (string)GetValue(SelectedColorHexProperty);
            set => SetValue(SelectedColorHexProperty, value);
        }

        private bool _isUpdating = false;

        private static readonly string[] EyeColorsPalette = new[]
        {
            "#2979FF", "#80D8FF", "#0D47A1", "#051636", "#E1F5FE",
            "#00C853", "#B9F6CA", "#1B5E20", "#092E10", "#CCFF90",
            "#D50000", "#FF8A80", "#5A0000", "#2B0000", "#FFCDD2",
            "#AA00FF", "#EA80FC", "#4A148C", "#1E053D", "#E1BEE7",
            "#FFB300", "#FFE082", "#E65100", "#3E2723", "#FFF9C4",
            "#90A4AE", "#ECEFF1", "#37474F", "#1B2327", "#FFFFFF",
            "#FF4081", "#FF80AB", "#880E4F", "#3F0523", "#FCE4EC",
            "#7C4DFF", "#FF9100", "#00E5FF", "#FFD600", "#060A18"
        };

        private static readonly string[] PrimaryColors = new[]
        {
            "#000000", "#333333", "#666666", "#999999", "#CCCCCC", "#FFFFFF",
            "#FF0000", "#FF7F00", "#FFFF00", "#00FF00", "#00FFFF", "#0000FF",
            "#8B00FF", "#FF00FF", "#FF1493", "#8B4513", "#2F4F4F", "#4B0082"
        };

        public ColorPickerBox()
        {
            InitializeComponent();
            PopulatePalettes();
            UpdateVisuals(SelectedColorHex);
        }

        private void PopulatePalettes()
        {
            foreach (var hex in EyeColorsPalette)
            {
                var btn = CreatePaletteButton(hex);
                PaletteWrapPanel.Children.Add(btn);
            }

            foreach (var hex in PrimaryColors)
            {
                var btn = CreatePaletteButton(hex);
                PrimaryWrapPanel.Children.Add(btn);
            }
        }

        private Button CreatePaletteButton(string hex)
        {
            var btn = new Button
            {
                Width = 20,
                Height = 20,
                Margin = new Thickness(1),
                Padding = new Thickness(0),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = hex
            };
            var color = GeometryHelper.ParseHexColor(hex);
            btn.Background = new SolidColorBrush(color);
            btn.Click += (s, e) =>
            {
                SelectedColorHex = hex;
                PalettePopup.IsOpen = false;
            };
            return btn;
        }

        private static void OnSelectedColorHexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ColorPickerBox cpb && e.NewValue is string hex)
            {
                cpb.UpdateVisuals(hex);
            }
        }

        private void UpdateVisuals(string hex)
        {
            if (_isUpdating) return;
            _isUpdating = true;
            try
            {
                HexTextBox.Text = hex;
                var color = GeometryHelper.ParseHexColor(hex);
                ColorSwatch.Background = new SolidColorBrush(color);

                RSlider.Value = color.R;
                GSlider.Value = color.G;
                BSlider.Value = color.B;

                RValueTxt.Text = color.R.ToString();
                GValueTxt.Text = color.G.ToString();
                BValueTxt.Text = color.B.ToString();
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void OnColorPreviewClick(object sender, RoutedEventArgs e)
        {
            PalettePopup.IsOpen = !PalettePopup.IsOpen;
        }

        private void OnHexTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating) return;
            string text = HexTextBox.Text.Trim();
            if (Regex.IsMatch(text, @"^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$"))
            {
                _isUpdating = true;
                try
                {
                    SelectedColorHex = text;
                    var color = GeometryHelper.ParseHexColor(text);
                    ColorSwatch.Background = new SolidColorBrush(color);

                    RSlider.Value = color.R;
                    GSlider.Value = color.G;
                    BSlider.Value = color.B;

                    RValueTxt.Text = color.R.ToString();
                    GValueTxt.Text = color.G.ToString();
                    BValueTxt.Text = color.B.ToString();
                }
                finally
                {
                    _isUpdating = false;
                }
            }
        }

        private void OnRgbSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdating) return;
            byte r = (byte)Math.Clamp((int)RSlider.Value, 0, 255);
            byte g = (byte)Math.Clamp((int)GSlider.Value, 0, 255);
            byte b = (byte)Math.Clamp((int)BSlider.Value, 0, 255);

            RValueTxt.Text = r.ToString();
            GValueTxt.Text = g.ToString();
            BValueTxt.Text = b.ToString();

            string newHex = $"#{r:X2}{g:X2}{b:X2}";
            _isUpdating = true;
            try
            {
                HexTextBox.Text = newHex;
                SelectedColorHex = newHex;
                ColorSwatch.Background = new SolidColorBrush(Color.FromRgb(r, g, b));
            }
            finally
            {
                _isUpdating = false;
            }
        }
    }
}
