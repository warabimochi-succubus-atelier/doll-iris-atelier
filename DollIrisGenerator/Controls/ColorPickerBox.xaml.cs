using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;

namespace DollIrisGenerator.Controls
{
    /// <summary>
    /// Color box: a swatch with palette popup and a HEX field. With <see cref="AllowGradient"/> it also has a
    /// 単色 / グラデーション switch and an inline gradient editor (type, angle, 2-5 colors with positions).
    /// The value is always the string stored in the model (<see cref="ColorFill"/>): a hex color, or the
    /// gradient notation, so bindings, undo and saving stay the same as for a plain color.
    /// </summary>
    public partial class ColorPickerBox : UserControl
    {
        public static readonly DependencyProperty SelectedColorHexProperty =
            DependencyProperty.Register(
                nameof(SelectedColorHex),
                typeof(string),
                typeof(ColorPickerBox),
                new FrameworkPropertyMetadata("#2979FF", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedColorHexChanged));

        /// <summary>The stored color value: "#RRGGBB" or a gradient (see <see cref="ColorFill"/>).</summary>
        public string SelectedColorHex
        {
            get => (string)GetValue(SelectedColorHexProperty);
            set => SetValue(SelectedColorHexProperty, value);
        }

        public static readonly DependencyProperty AllowGradientProperty =
            DependencyProperty.Register(
                nameof(AllowGradient),
                typeof(bool),
                typeof(ColorPickerBox),
                new PropertyMetadata(false, OnAllowGradientChanged));

        /// <summary>
        /// Shows the 単色 / グラデーション switch. Off for colors that are themselves part of a built-in gradient
        /// (base / inner / outer / lower glow) and for the color of each gradient stop.
        /// </summary>
        public bool AllowGradient
        {
            get => (bool)GetValue(AllowGradientProperty);
            set => SetValue(AllowGradientProperty, value);
        }

        /// <summary>Raised whenever <see cref="SelectedColorHex"/> changes (used by the stop rows of an outer box).</summary>
        public event EventHandler? SelectedColorHexChanged;

        private bool _isUpdating = false;

        /// <summary>
        /// RGB or CMYK sliders in the palette popup. Shared by every color box (switching in one applies to the
        /// next popup opened) and remembered in the window settings.
        /// </summary>
        public static ColorInputMode PreferredInputMode { get; set; } = ColorInputMode.Rgb;

        // The CMYK the user last entered, kept while it still converts to the current color, so the
        // numbers do not jump after rounding to #RRGGBB (see CmykColor.Resolve)
        private CmykColor? _heldCmyk;
        private bool _suppressInputModeEvents;

        // Gradient editing state. Stops are kept in the order of the rows (not re-sorted while editing,
        // so dragging one stop past another does not swap rows under the mouse); the stored string is sorted.
        private bool _gradientMode;
        private bool _suppressGradientEvents;
        private bool _editorCollapsed;
        private ColorFillKind _kind = ColorFillKind.Linear;
        private double _angle = ColorFill.DefaultAngle;
        private List<ColorStop> _stops = new();
        private string? _lastWrittenGradient;

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
            ShowInputMode(PreferredInputMode);
            LoadFromValue(SelectedColorHex);
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
            var color = WpfGeometry.ParseHexColor(hex);
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
            if (d is ColorPickerBox cpb)
            {
                var value = e.NewValue as string;
                // Our own gradient edit coming back: the editor already shows it (rows stay as the user arranged them)
                if (!(cpb._gradientMode && value != null && value == cpb._lastWrittenGradient))
                {
                    cpb.LoadFromValue(value);
                }
                cpb.SelectedColorHexChanged?.Invoke(cpb, EventArgs.Empty);
            }
        }

        private static void OnAllowGradientChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ColorPickerBox cpb)
            {
                cpb.ModeCombo.Visibility = cpb.AllowGradient ? Visibility.Visible : Visibility.Collapsed;
                cpb.LoadFromValue(cpb.SelectedColorHex);
            }
        }

        /// <summary>Shows a value set from outside (binding, undo, preset, file): solid UI or the gradient editor.</summary>
        private void LoadFromValue(string? value)
        {
            var fill = ColorFill.Parse(value);
            if (AllowGradient && fill.IsGradient)
            {
                _gradientMode = true;
                _kind = fill.Kind;
                _angle = fill.Angle;
                _stops = fill.Stops.ToList();
                _lastWrittenGradient = fill.ToString();
                ShowGradientUi();
                RebuildStopRows();
                RefreshGradientWidgets();
            }
            else
            {
                _gradientMode = false;
                _lastWrittenGradient = null;
                ShowGradientUi();
                // A gradient in a solid-only box (hand-edited file) shows its first color until edited
                UpdateVisuals(fill.IsGradient ? fill.PrimaryColor : value ?? "");
            }
            SetModeComboSilently(_gradientMode ? 1 : 0);
        }

        private void UpdateVisuals(string hex)
        {
            if (_isUpdating) return;
            _isUpdating = true;
            try
            {
                HexTextBox.Text = hex;
                var color = WpfGeometry.ParseHexColor(hex);
                ColorSwatch.Background = new SolidColorBrush(color);
                ShowCustomSliders(color);
            }
            finally
            {
                _isUpdating = false;
            }
        }

        /// <summary>RGB and CMYK sliders / values for a color (call with <see cref="_isUpdating"/> set).</summary>
        private void ShowCustomSliders(Color color)
        {
            RSlider.Value = color.R;
            GSlider.Value = color.G;
            BSlider.Value = color.B;

            RValueTxt.Text = color.R.ToString();
            GValueTxt.Text = color.G.ToString();
            BValueTxt.Text = color.B.ToString();

            var cmyk = CmykColor.Resolve(_heldCmyk, color.R, color.G, color.B);
            _heldCmyk = cmyk;
            ShowCmyk(cmyk);
        }

        private void ShowCmyk(CmykColor cmyk)
        {
            var sliders = CmykSliders();
            var boxes = CmykBoxes();
            for (int i = 0; i < 4; i++)
            {
                sliders[i].Value = cmyk[i];
                // Whole percents on screen; the held value keeps the fraction so RGB → CMYK → RGB is exact
                boxes[i].Text = CmykColor.FormatPercent(cmyk[i]);
            }
        }

        private Slider[] CmykSliders() => new[] { CSlider, MSlider, YSlider, KSlider };
        private TextBox[] CmykBoxes() => new[] { CValueBox, MValueBox, YValueBox, KValueBox };

        private void OnColorPreviewClick(object sender, RoutedEventArgs e)
        {
            if (_gradientMode)
            {
                // Gradient mode: the swatch folds the editor away / back (keeps long lists short)
                _editorCollapsed = !_editorCollapsed;
                ShowGradientUi();
                return;
            }
            if (!PalettePopup.IsOpen) ShowInputMode(PreferredInputMode); // may have been switched in another box
            PalettePopup.IsOpen = !PalettePopup.IsOpen;
        }

        // ------------------------------------------------------------------
        // RGB / CMYK input
        // ------------------------------------------------------------------

        private void ShowInputMode(ColorInputMode mode)
        {
            _suppressInputModeEvents = true;
            try
            {
                RgbModeBtn.IsChecked = mode == ColorInputMode.Rgb;
                CmykModeBtn.IsChecked = mode == ColorInputMode.Cmyk;
            }
            finally
            {
                _suppressInputModeEvents = false;
            }
            RgbPanel.Visibility = mode == ColorInputMode.Rgb ? Visibility.Visible : Visibility.Collapsed;
            CmykPanel.Visibility = mode == ColorInputMode.Cmyk ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnInputModeChecked(object sender, RoutedEventArgs e)
        {
            if (_suppressInputModeEvents || CmykPanel == null || RgbPanel == null) return;
            var mode = sender == CmykModeBtn ? ColorInputMode.Cmyk : ColorInputMode.Rgb;
            PreferredInputMode = mode;
            ShowInputMode(mode);
        }

        private void OnCmykSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdating || sender is not Slider slider) return;
            SetCmykChannel(ChannelOf(slider), Math.Round(slider.Value, MidpointRounding.AwayFromZero), slider);
        }

        private void OnCmykValueBoxKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (sender is not TextBox box) return;
            switch (e.Key)
            {
                case System.Windows.Input.Key.Enter:
                    CommitCmykBox(box);
                    box.SelectAll();
                    e.Handled = true;
                    break;
                case System.Windows.Input.Key.Up:
                case System.Windows.Input.Key.Down:
                    int channel = ChannelOf(box);
                    double current = CmykColor.TryParsePercent(box.Text, out double typed) ? typed : CurrentCmyk()[channel];
                    double step = e.Key == System.Windows.Input.Key.Up ? 1 : -1;
                    SetCmykChannel(channel, Math.Round(current, MidpointRounding.AwayFromZero) + step, null);
                    box.SelectAll();
                    e.Handled = true;
                    break;
            }
        }

        private void OnCmykValueBoxLostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox box) CommitCmykBox(box);
        }

        private void CommitCmykBox(TextBox box)
        {
            int channel = ChannelOf(box);
            var current = CurrentCmyk();
            string shown = CmykColor.FormatPercent(current[channel]);
            // Text still as shown (e.g. "33" for a held 33.3): leaving the box must not round the color
            if (box.Text.Trim() != shown && CmykColor.TryParsePercent(box.Text, out double percent))
            {
                SetCmykChannel(channel, percent, null);
            }
            else
            {
                box.Text = CmykColor.FormatPercent(current[channel]); // invalid or unchanged: show the value again
            }
        }

        private static int ChannelOf(FrameworkElement element)
            => int.TryParse(element.Tag as string, out int i) ? Math.Clamp(i, 0, 3) : 0;

        /// <summary>The CMYK shown for the current solid color.</summary>
        private CmykColor CurrentCmyk()
        {
            var color = WpfGeometry.ParseHexColor(ColorFill.Parse(SelectedColorHex).PrimaryColor);
            return CmykColor.Resolve(_heldCmyk, color.R, color.G, color.B);
        }

        /// <summary>
        /// One C / M / Y / K value changed: the other three are kept exactly as they are, so only the RGB
        /// channels this one affects can change, and the CMYK numbers on screen stay as entered.
        /// </summary>
        private void SetCmykChannel(int channel, double percent, Slider? source)
        {
            if (_gradientMode) return;
            var cmyk = CurrentCmyk().With(channel, percent);
            _heldCmyk = cmyk;
            var (r, g, b) = cmyk.ToRgb();
            string newHex = cmyk.ToHex();

            _isUpdating = true;
            try
            {
                HexTextBox.Text = newHex;
                SelectedColorHex = newHex;
                ColorSwatch.Background = new SolidColorBrush(Color.FromRgb(r, g, b));

                RSlider.Value = r;
                GSlider.Value = g;
                BSlider.Value = b;
                RValueTxt.Text = r.ToString();
                GValueTxt.Text = g.ToString();
                BValueTxt.Text = b.ToString();

                var sliders = CmykSliders();
                var boxes = CmykBoxes();
                for (int i = 0; i < 4; i++)
                {
                    if (sliders[i] != source) sliders[i].Value = cmyk[i];
                    boxes[i].Text = CmykColor.FormatPercent(cmyk[i]);
                }
            }
            finally
            {
                _isUpdating = false;
            }
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
                    var color = WpfGeometry.ParseHexColor(text);
                    ColorSwatch.Background = new SolidColorBrush(color);
                    ShowCustomSliders(color);
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

                var cmyk = CmykColor.Resolve(_heldCmyk, r, g, b);
                _heldCmyk = cmyk;
                ShowCmyk(cmyk);
            }
            finally
            {
                _isUpdating = false;
            }
        }

        // ------------------------------------------------------------------
        // Gradient mode
        // ------------------------------------------------------------------

        private void SetModeComboSilently(int index)
        {
            _suppressGradientEvents = true;
            try { ModeCombo.SelectedIndex = index; }
            finally { _suppressGradientEvents = false; }
        }

        private void OnModeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressGradientEvents || !IsLoadedOrInitialized()) return;
            bool wantGradient = ModeCombo.SelectedIndex == 1;
            if (wantGradient == _gradientMode) return;

            if (wantGradient)
            {
                // Start from the current color, so the design keeps its look as much as possible
                var start = ColorFill.DefaultGradientFrom(ColorFill.Parse(SelectedColorHex).PrimaryColor);
                _gradientMode = true;
                _editorCollapsed = false;
                _kind = start.Kind;
                _angle = start.Angle;
                _stops = start.Stops.ToList();
                ShowGradientUi();
                RebuildStopRows();
                RefreshGradientWidgets();
                WriteGradient();
            }
            else
            {
                // Back to one color: the gradient's first (0 %) color
                string first = CurrentFill().PrimaryColor;
                _gradientMode = false;
                _lastWrittenGradient = null;
                SelectedColorHex = first; // -> LoadFromValue shows the solid UI
                ShowGradientUi();
            }
        }

        private bool IsLoadedOrInitialized() => IsInitialized;

        private ColorFill CurrentFill() => ColorFill.Create(_kind, _angle, _stops);

        private static int KindToIndex(ColorFillKind kind) => kind switch
        {
            ColorFillKind.Radial => 1,
            ColorFillKind.Conic => 2,
            _ => 0
        };

        private void WriteGradient()
        {
            var fill = CurrentFill();
            string value = fill.ToString();
            _lastWrittenGradient = value;
            SelectedColorHex = value;
            RefreshPreview(fill);
        }

        private void ShowGradientUi()
        {
            bool showEditor = _gradientMode && !_editorCollapsed;
            HexTextBox.Visibility = _gradientMode ? Visibility.Collapsed : Visibility.Visible;
            GradientBar.Visibility = _gradientMode ? Visibility.Visible : Visibility.Collapsed;
            GradientEditor.Visibility = showEditor ? Visibility.Visible : Visibility.Collapsed;
            ModeCombo.Visibility = AllowGradient ? Visibility.Visible : Visibility.Collapsed;
            ColorPreviewBtn.ToolTip = _gradientMode
                ? (showEditor ? "クリックしてグラデーション設定を閉じる" : "クリックしてグラデーション設定を開く")
                : "クリックしてカラーパレットを開く";
        }

        /// <summary>Kind / angle widgets and the previews, from the current state.</summary>
        private void RefreshGradientWidgets()
        {
            _suppressGradientEvents = true;
            try
            {
                KindCombo.SelectedIndex = KindToIndex(_kind);
                AngleSlider.Value = _angle;
                AngleValueTxt.Text = $"{_angle:0}°";
                AngleLabel.Text = _kind == ColorFillKind.Conic ? "開始:" : "角度:";
                AngleSlider.ToolTip = _kind == ColorFillKind.Conic
                    ? "0% の色がある向き: 0° = 右、90° = 下、180° = 左、270° = 上 (そこから時計回り)"
                    : "0° = 左→右、90° = 上→下、180° = 右→左、270° = 下→上";
                AnglePanel.Visibility = _kind == ColorFillKind.Radial ? Visibility.Collapsed : Visibility.Visible;
                RadialHint.Visibility = _kind == ColorFillKind.Radial ? Visibility.Visible : Visibility.Collapsed;
                ConicHint.Visibility = _kind == ColorFillKind.Conic ? Visibility.Visible : Visibility.Collapsed;
                AddStopBtn.IsEnabled = _stops.Count < ColorFill.MaxStops;
            }
            finally
            {
                _suppressGradientEvents = false;
            }
            RefreshPreview(CurrentFill());
        }

        private void RefreshPreview(ColorFill fill)
        {
            // Swatch: the gradient as it is laid on a shape; bar: the colors in order, left = 0 %, right = 100 %
            ColorSwatch.Background = CreatePreviewBrush(fill);
            var bar = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
            foreach (var s in fill.Stops) bar.GradientStops.Add(new GradientStop(WpfGeometry.ParseHexColor(s.Color), s.Offset));
            GradientBar.Background = bar;
        }

        /// <summary>Preview brush on a unit box (relative to the swatch), placed the same way as on the iris.</summary>
        internal static Brush CreatePreviewBrush(ColorFill fill)
        {
            if (!fill.IsGradient) return new SolidColorBrush(WpfGeometry.ParseHexColor(fill.PrimaryColor));
            var map = GradientMapping.Create(fill, new DollIrisGenerator.Core.Geometry.Rect(0, 0, 1, 1));
            if (map.Kind == ColorFillKind.Conic) return WpfIrisRenderer.CreateConicBrush(map, 1.0, BrushMappingMode.RelativeToBoundingBox);
            var stops = new GradientStopCollection(map.Stops.Select(s => new GradientStop(WpfGeometry.ParseHexColor(s.Color), s.Offset)));
            return map.Kind == ColorFillKind.Radial
                ? new RadialGradientBrush(stops) { Center = map.Center.ToWpf(), GradientOrigin = map.Center.ToWpf(), RadiusX = map.RadiusX, RadiusY = map.RadiusY }
                : new LinearGradientBrush(stops) { StartPoint = map.Start.ToWpf(), EndPoint = map.End.ToWpf() };
        }

        private void OnKindChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressGradientEvents || !_gradientMode) return;
            var previous = _kind;
            _kind = KindCombo.SelectedIndex switch
            {
                1 => ColorFillKind.Radial,
                2 => ColorFillKind.Conic,
                _ => ColorFillKind.Linear
            };
            // The angle means a direction of travel for linear but a starting direction for conic: switching
            // to conic starts from the top (the usual place for an aurora's first color)
            if (_kind == ColorFillKind.Conic && previous != ColorFillKind.Conic) _angle = ColorFill.ConicDefaultAngle;
            else if (previous == ColorFillKind.Conic && _kind == ColorFillKind.Linear) _angle = ColorFill.DefaultAngle;
            RefreshGradientWidgets();
            WriteGradient();
        }

        private void OnAngleChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressGradientEvents || !_gradientMode) return;
            _angle = Math.Round(AngleSlider.Value);
            AngleValueTxt.Text = $"{_angle:0}°";
            WriteGradient();
        }

        private void OnReverseClick(object sender, RoutedEventArgs e)
        {
            if (!_gradientMode) return;
            _stops = _stops.Select(s => new ColorStop(s.Color, 1.0 - s.Offset)).Reverse().ToList();
            RebuildStopRows();
            RefreshGradientWidgets();
            WriteGradient();
        }

        private void OnAddStopClick(object sender, RoutedEventArgs e)
        {
            if (!_gradientMode || _stops.Count >= ColorFill.MaxStops) return;

            // New color in the middle of the widest gap, mixed from its two neighbours (no visible change yet)
            var sorted = _stops.OrderBy(s => s.Offset).ToList();
            int gapAt = 0;
            double widest = -1;
            for (int i = 0; i + 1 < sorted.Count; i++)
            {
                double gap = sorted[i + 1].Offset - sorted[i].Offset;
                if (gap > widest) { widest = gap; gapAt = i; }
            }
            var a = sorted[gapAt];
            var b = sorted[Math.Min(gapAt + 1, sorted.Count - 1)];
            var mixed = Mix(WpfGeometry.ParseHexColor(a.Color), WpfGeometry.ParseHexColor(b.Color), 0.5);
            var stop = new ColorStop($"#{mixed.R:X2}{mixed.G:X2}{mixed.B:X2}", Math.Round((a.Offset + b.Offset) / 2.0, 3));

            int insertAt = _stops.IndexOf(a) + 1;
            _stops.Insert(Math.Clamp(insertAt, 0, _stops.Count), stop);
            RebuildStopRows();
            RefreshGradientWidgets();
            WriteGradient();
        }

        private static Color Mix(Color a, Color b, double t) => Color.FromRgb(
            (byte)Math.Round(a.R + (b.R - a.R) * t),
            (byte)Math.Round(a.G + (b.G - a.G) * t),
            (byte)Math.Round(a.B + (b.B - a.B) * t));

        /// <summary>One row per stop: color box, position slider, position text, remove button.</summary>
        private void RebuildStopRows()
        {
            StopsPanel.Children.Clear();
            for (int i = 0; i < _stops.Count; i++)
            {
                int index = i;
                var row = new Grid { Margin = new Thickness(0, 1, 0, 1) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(128) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var picker = new ColorPickerBox { SelectedColorHex = _stops[i].Color, ToolTip = $"{index + 1} 色目" };
                picker.SelectedColorHexChanged += (s, e) =>
                {
                    if (_suppressGradientEvents || !_gradientMode || index >= _stops.Count) return;
                    string hex = picker.SelectedColorHex;
                    if (!ColorFill.IsHexColor(hex)) return;
                    _stops[index] = new ColorStop(hex, _stops[index].Offset);
                    WriteGradient();
                };
                Grid.SetColumn(picker, 0);
                row.Children.Add(picker);

                var valueText = new TextBlock
                {
                    Text = $"{_stops[i].Offset * 100.0:0}%",
                    Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0xD8, 0xFF)),
                    FontSize = 11,
                    TextAlignment = TextAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var slider = new Slider
                {
                    Minimum = 0,
                    Maximum = 100,
                    SmallChange = 1,
                    LargeChange = 10,
                    Value = _stops[i].Offset * 100.0,
                    Margin = new Thickness(6, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    ToolTip = "この色の位置 (0% = 始点 / 中心側、100% = 終点 / 外側)"
                };
                slider.ValueChanged += (s, e) =>
                {
                    if (_suppressGradientEvents || !_gradientMode || index >= _stops.Count) return;
                    double offset = Math.Round(slider.Value) / 100.0;
                    valueText.Text = $"{offset * 100.0:0}%";
                    _stops[index] = new ColorStop(_stops[index].Color, offset);
                    WriteGradient();
                };
                Grid.SetColumn(slider, 1);
                row.Children.Add(slider);
                Grid.SetColumn(valueText, 2);
                row.Children.Add(valueText);

                var remove = new Button
                {
                    Content = "✕",
                    FontSize = 10,
                    Padding = new Thickness(5, 0, 5, 0),
                    Margin = new Thickness(4, 2, 0, 2),
                    IsEnabled = _stops.Count > ColorFill.MinStops,
                    ToolTip = "この色を削除 (最低2色)"
                };
                remove.Click += (s, e) =>
                {
                    if (!_gradientMode || _stops.Count <= ColorFill.MinStops || index >= _stops.Count) return;
                    _stops.RemoveAt(index);
                    RebuildStopRows();
                    RefreshGradientWidgets();
                    WriteGradient();
                };
                Grid.SetColumn(remove, 3);
                row.Children.Add(remove);

                StopsPanel.Children.Add(row);
            }
        }
    }
}
