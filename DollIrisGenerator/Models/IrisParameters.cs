using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DollIrisGenerator.Models
{
    public class IrisParameters : INotifyPropertyChanged
    {
        private string _name = "新規虹彩デザイン";
        private double _sizeMm = 20.0;
        private IrisShape _shape = IrisShape.Circle;
        private double _aspectRatio = 1.15; // used when shape is oval
        private double _tiltAngle = 0.0;    // degrees (-45 to 45) for stylized doll eyes
        private int _seed = 12345;

        // Base & Gradients
        private string _baseColor = "#2979FF";
        private string _innerColor = "#80D8FF";
        private string _outerColor = "#0D47A1";
        private string _bottomHighlightColor = "#B3E5FC"; // lower iris glow / reflective rim
        private double _innerGradientRadius = 0.55;
        private double _outerGradientRadius = 0.95;
        private double _bottomHighlightIntensity = 0.40;

        // Limbal Ring (外輪)
        private double _limbalRingThickness = 0.08; // fraction of radius
        private string _limbalRingColor = "#051636";
        private double _limbalRingOpacity = 0.95;
        private double _limbalRingSoftness = 0.20;

        // Pupil (瞳孔)
        private PupilShape _pupilShape = PupilShape.Round;
        private double _pupilSizeRatio = 0.32; // fraction of iris diameter
        private string _pupilColor = "#060A18";
        private double _pupilOpacity = 1.0;
        private double _pupilSoftness = 0.08;

        // Radial Pattern (放射状ライン / 放射模様)
        private int _rayCount = 48;
        private double _rayThickness = 1.2;
        private double _rayLengthRatio = 0.65;
        private double _rayIntensity = 0.65;
        private double _rayRandomness = 0.45;
        private string _rayColor = "#B3E5FC";
        private double _rayInnerRadiusRatio = 0.32;

        // Lens-Style Optical Pattern (レンズ風パターン)
        private bool _lensPatternEnabled = false;
        private int _lensRingCount = 6;
        private double _lensRingIntensity = 0.50;
        private double _lensRingWidth = 0.8;
        private string _lensRingColor = "#FFFFFF";
        private double _lensRefractionGlow = 0.35;

        // Glitter / Particles (粒子模様・ラメ)
        private int _particleCount = 50;
        private double _particleMinSize = 1.0;
        private double _particleMaxSize = 2.4;
        private double _particleOpacity = 0.85;
        private string _particleColor = "#FFFFFF";
        private double _particleRadiusRatio = 0.75;

        // Decorative Symbols (装飾記号)
        private ObservableCollection<DecorativeSymbolItem> _symbols = new();

        // Highlights (ハイライト)
        private ObservableCollection<HighlightItem> _highlights = new();

        // Symmetry & Lighting
        private bool _mirrorHighlightsWithIris = true; // Section 13

        // Odd-Eye Mode (オッドアイ対応)
        private bool _isOddEye = false;
        private string _oddEyeBaseColor = "#FFB300";
        private string _oddEyeInnerColor = "#FFE082";
        private string _oddEyeOuterColor = "#E65100";
        private string _oddEyeRayColor = "#FFF9C4";
        private string _oddEyeBottomHighlightColor = "#FFFDE7";
        private int _oddEyeSeedOffset = 1000;

        public IrisParameters()
        {
            // Default highlights matching standard anime doll eyes
            _highlights.Add(new HighlightItem
            {
                Shape = HighlightShape.Circle,
                PositionX = -0.32,
                PositionY = -0.32,
                Size = 0.26,
                Color = "#FFFFFF",
                Opacity = 0.95,
                Enabled = true
            });
            _highlights.Add(new HighlightItem
            {
                Shape = HighlightShape.Circle,
                PositionX = 0.35,
                PositionY = 0.30,
                Size = 0.14,
                Color = "#FFFFFF",
                Opacity = 0.75,
                Enabled = true
            });

            // Default decorative symbol (disabled by default)
            _symbols.Add(new DecorativeSymbolItem
            {
                Type = SymbolType.Star4,
                Count = 1,
                Size = 0.18,
                PositionX = 0.25,
                PositionY = -0.15,
                Color = "#FFFFFF",
                Opacity = 0.85,
                Enabled = false
            });

            AttachNested(_highlights);
            AttachNested(_symbols);
        }

        public string Name
        {
            get => _name;
            set { _name = value ?? "新規虹彩デザイン"; OnPropertyChanged(); }
        }

        public double SizeMm
        {
            get => _sizeMm;
            set { _sizeMm = Math.Max(4.0, Math.Min(60.0, value)); OnPropertyChanged(); }
        }

        public IrisShape Shape
        {
            get => _shape;
            set { _shape = value; OnPropertyChanged(); }
        }

        public double AspectRatio
        {
            get => _aspectRatio;
            set { _aspectRatio = Math.Max(0.5, Math.Min(2.5, value)); OnPropertyChanged(); }
        }

        public double TiltAngle
        {
            get => _tiltAngle;
            set { _tiltAngle = Math.Max(-45.0, Math.Min(45.0, value)); OnPropertyChanged(); }
        }

        public int Seed
        {
            get => _seed;
            set { _seed = value; OnPropertyChanged(); }
        }

        // Colors
        public string BaseColor
        {
            get => _baseColor;
            set { _baseColor = value; OnPropertyChanged(); }
        }

        public string InnerColor
        {
            get => _innerColor;
            set { _innerColor = value; OnPropertyChanged(); }
        }

        public string OuterColor
        {
            get => _outerColor;
            set { _outerColor = value; OnPropertyChanged(); }
        }

        public string BottomHighlightColor
        {
            get => _bottomHighlightColor;
            set { _bottomHighlightColor = value; OnPropertyChanged(); }
        }

        public double InnerGradientRadius
        {
            get => _innerGradientRadius;
            set { _innerGradientRadius = Math.Max(0.1, Math.Min(1.0, value)); OnPropertyChanged(); }
        }

        public double OuterGradientRadius
        {
            get => _outerGradientRadius;
            set { _outerGradientRadius = Math.Max(0.1, Math.Min(1.2, value)); OnPropertyChanged(); }
        }

        public double BottomHighlightIntensity
        {
            get => _bottomHighlightIntensity;
            set { _bottomHighlightIntensity = Math.Max(0.0, Math.Min(1.0, value)); OnPropertyChanged(); }
        }

        // Limbal Ring
        public double LimbalRingThickness
        {
            get => _limbalRingThickness;
            set { _limbalRingThickness = Math.Max(0.0, Math.Min(0.3, value)); OnPropertyChanged(); }
        }

        public string LimbalRingColor
        {
            get => _limbalRingColor;
            set { _limbalRingColor = value; OnPropertyChanged(); }
        }

        public double LimbalRingOpacity
        {
            get => _limbalRingOpacity;
            set { _limbalRingOpacity = Math.Max(0.0, Math.Min(1.0, value)); OnPropertyChanged(); }
        }

        public double LimbalRingSoftness
        {
            get => _limbalRingSoftness;
            set { _limbalRingSoftness = Math.Max(0.0, Math.Min(1.0, value)); OnPropertyChanged(); }
        }

        // Pupil
        public PupilShape PupilShape
        {
            get => _pupilShape;
            set { _pupilShape = value; OnPropertyChanged(); }
        }

        public double PupilSizeRatio
        {
            get => _pupilSizeRatio;
            set { _pupilSizeRatio = Math.Max(0.05, Math.Min(0.8, value)); OnPropertyChanged(); }
        }

        public string PupilColor
        {
            get => _pupilColor;
            set { _pupilColor = value; OnPropertyChanged(); }
        }

        public double PupilOpacity
        {
            get => _pupilOpacity;
            set { _pupilOpacity = Math.Max(0.0, Math.Min(1.0, value)); OnPropertyChanged(); }
        }

        public double PupilSoftness
        {
            get => _pupilSoftness;
            set { _pupilSoftness = Math.Max(0.0, Math.Min(0.5, value)); OnPropertyChanged(); }
        }

        // Rays
        public int RayCount
        {
            get => _rayCount;
            set { _rayCount = Math.Max(0, Math.Min(180, value)); OnPropertyChanged(); }
        }

        public double RayThickness
        {
            get => _rayThickness;
            set { _rayThickness = Math.Max(0.2, Math.Min(8.0, value)); OnPropertyChanged(); }
        }

        public double RayLengthRatio
        {
            get => _rayLengthRatio;
            set { _rayLengthRatio = Math.Max(0.1, Math.Min(1.0, value)); OnPropertyChanged(); }
        }

        public double RayIntensity
        {
            get => _rayIntensity;
            set { _rayIntensity = Math.Max(0.0, Math.Min(1.0, value)); OnPropertyChanged(); }
        }

        public double RayRandomness
        {
            get => _rayRandomness;
            set { _rayRandomness = Math.Max(0.0, Math.Min(1.0, value)); OnPropertyChanged(); }
        }

        public string RayColor
        {
            get => _rayColor;
            set { _rayColor = value; OnPropertyChanged(); }
        }

        public double RayInnerRadiusRatio
        {
            get => _rayInnerRadiusRatio;
            set { _rayInnerRadiusRatio = Math.Max(0.1, Math.Min(0.8, value)); OnPropertyChanged(); }
        }

        // Lens-Style Optical Pattern
        public bool LensPatternEnabled
        {
            get => _lensPatternEnabled;
            set { _lensPatternEnabled = value; OnPropertyChanged(); }
        }

        public int LensRingCount
        {
            get => _lensRingCount;
            set { _lensRingCount = Math.Max(1, Math.Min(24, value)); OnPropertyChanged(); }
        }

        public double LensRingIntensity
        {
            get => _lensRingIntensity;
            set { _lensRingIntensity = Math.Max(0.0, Math.Min(1.0, value)); OnPropertyChanged(); }
        }

        public double LensRingWidth
        {
            get => _lensRingWidth;
            set { _lensRingWidth = Math.Max(0.2, Math.Min(5.0, value)); OnPropertyChanged(); }
        }

        public string LensRingColor
        {
            get => _lensRingColor;
            set { _lensRingColor = value; OnPropertyChanged(); }
        }

        public double LensRefractionGlow
        {
            get => _lensRefractionGlow;
            set { _lensRefractionGlow = Math.Max(0.0, Math.Min(1.0, value)); OnPropertyChanged(); }
        }

        // Particles
        public int ParticleCount
        {
            get => _particleCount;
            set { _particleCount = Math.Max(0, Math.Min(500, value)); OnPropertyChanged(); }
        }

        public double ParticleMinSize
        {
            get => _particleMinSize;
            set { _particleMinSize = Math.Max(0.2, Math.Min(10.0, value)); OnPropertyChanged(); }
        }

        public double ParticleMaxSize
        {
            get => _particleMaxSize;
            set { _particleMaxSize = Math.Max(0.2, Math.Min(15.0, value)); OnPropertyChanged(); }
        }

        public double ParticleOpacity
        {
            get => _particleOpacity;
            set { _particleOpacity = Math.Max(0.0, Math.Min(1.0, value)); OnPropertyChanged(); }
        }

        public string ParticleColor
        {
            get => _particleColor;
            set { _particleColor = value; OnPropertyChanged(); }
        }

        public double ParticleRadiusRatio
        {
            get => _particleRadiusRatio;
            set { _particleRadiusRatio = Math.Max(0.1, Math.Min(1.0, value)); OnPropertyChanged(); }
        }

        // Collections
        public ObservableCollection<DecorativeSymbolItem> Symbols
        {
            get => _symbols ??= new();
            set
            {
                DetachNested(_symbols);
                _symbols = LimitCount(value, MaxNestedItems);
                AttachNested(_symbols);
                OnPropertyChanged();
            }
        }

        public ObservableCollection<HighlightItem> Highlights
        {
            get => _highlights ??= new();
            set
            {
                DetachNested(_highlights);
                _highlights = LimitCount(value, MaxNestedItems);
                AttachNested(_highlights);
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Raised (as PropertyChanged with this name) whenever a highlight / symbol is added, removed or edited,
        /// so listeners (preview, SVG sync, undo) see nested edits without watching the collections themselves.
        /// Deliberately not a real property name, so WPF bindings ignore it.
        /// </summary>
        public const string NestedItemsChangedName = "NestedItems";

        /// <summary>
        /// Upper bound on highlights / symbols accepted from a loaded file (JSON, favorites, shared files),
        /// so a crafted file cannot make every render loop over millions of items.
        /// </summary>
        public const int MaxNestedItems = 64;

        private static ObservableCollection<T> LimitCount<T>(ObservableCollection<T>? items, int max)
        {
            if (items == null) return new();
            return items.Count <= max ? items : new ObservableCollection<T>(items.Take(max));
        }

        private void AttachNested<T>(ObservableCollection<T>? items) where T : INotifyPropertyChanged
        {
            if (items == null) return;
            items.CollectionChanged += OnNestedCollectionChanged;
            foreach (var item in items) item.PropertyChanged += OnNestedItemPropertyChanged;
        }

        private void DetachNested<T>(ObservableCollection<T>? items) where T : INotifyPropertyChanged
        {
            if (items == null) return;
            items.CollectionChanged -= OnNestedCollectionChanged;
            foreach (var item in items) item.PropertyChanged -= OnNestedItemPropertyChanged;
        }

        private void OnNestedCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (INotifyPropertyChanged item in e.OldItems) item.PropertyChanged -= OnNestedItemPropertyChanged;
            if (e.NewItems != null)
                foreach (INotifyPropertyChanged item in e.NewItems) item.PropertyChanged += OnNestedItemPropertyChanged;
            OnPropertyChanged(NestedItemsChangedName);
        }

        private void OnNestedItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(NestedItemsChangedName);
        }

        public bool MirrorHighlightsWithIris
        {
            get => _mirrorHighlightsWithIris;
            set { _mirrorHighlightsWithIris = value; OnPropertyChanged(); }
        }

        // Odd-Eye Mode
        public bool IsOddEye
        {
            get => _isOddEye;
            set { _isOddEye = value; OnPropertyChanged(); }
        }

        public string OddEyeBaseColor
        {
            get => _oddEyeBaseColor;
            set { _oddEyeBaseColor = value; OnPropertyChanged(); }
        }

        public string OddEyeInnerColor
        {
            get => _oddEyeInnerColor;
            set { _oddEyeInnerColor = value; OnPropertyChanged(); }
        }

        public string OddEyeOuterColor
        {
            get => _oddEyeOuterColor;
            set { _oddEyeOuterColor = value; OnPropertyChanged(); }
        }

        public string OddEyeRayColor
        {
            get => _oddEyeRayColor;
            set { _oddEyeRayColor = value; OnPropertyChanged(); }
        }

        public string OddEyeBottomHighlightColor
        {
            get => _oddEyeBottomHighlightColor;
            set { _oddEyeBottomHighlightColor = value; OnPropertyChanged(); }
        }

        public int OddEyeSeedOffset
        {
            get => _oddEyeSeedOffset;
            set { _oddEyeSeedOffset = value; OnPropertyChanged(); }
        }

        public IrisParameters Clone()
        {
            var json = JsonSerializer.Serialize(this);
            var clone = JsonSerializer.Deserialize<IrisParameters>(json)!;
            return clone;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
