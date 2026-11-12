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
        /// <summary>
        /// Iris diameter range. This is the iris only, not the whole doll eye: the iris is roughly half the
        /// eye (a 20 mm eye has an iris of about 10 mm), so 4-20 mm covers small dolls up to oversized irises.
        /// The setter clamps to this range, so saved JSON / favorites with a size outside it are pulled in.
        /// </summary>
        public const double MinSizeMm = 4.0;
        public const double MaxSizeMm = 20.0;
        public const double DefaultSizeMm = 10.0;

        /// <summary>Aurora layer color a new design starts with: pink → blue → violet → mint round the iris, back to pink.</summary>
        public const string DefaultAuroraColor = "conic-gradient(from 270deg, #FF80AB 0%, #80D8FF 30%, #B388FF 55%, #A7FFEB 80%, #FF80AB 100%)";

        /// <summary>Nebula color a new design starts with: magenta in the middle, violet, then sky blue outside.</summary>
        public const string DefaultNebulaColor = "radial-gradient(#E040FB 0%, #7C4DFF 50%, #40C4FF 100%)";

        public const int MinPupilFacetCount = 4;
        public const int MaxPupilFacetCount = 24;
        public const double MinNebulaScale = 0.5;
        public const double MaxNebulaScale = 3.0;

        public const double MinRayCurve = -1.0;
        public const double MaxRayCurve = 1.0;
        public const double MinParticleFlareLength = 1.0;
        public const double MaxParticleFlareLength = 8.0;

        /// <summary>
        /// Fiber rays and glowing glitter are on by default, closer to the glossy sample eyes. A file saved before
        /// these settings existed has none of them, so it loads with these values too (and looks richer than it did).
        /// </summary>
        public const double DefaultRayTaper = 0.7;
        public const double DefaultRayWidthVariation = 0.5;
        public const double DefaultRayCurve = 0.2;
        public const double DefaultRayFineLayer = 0.6;
        public const double DefaultParticleGlow = 0.7;
        public const double DefaultParticleFlareRatio = 0.15;
        public const double DefaultParticleFlareLength = 4.0;

        /// <summary>Clamps a loaded / bound value; NaN or infinity (hand-edited file) falls back to <paramref name="fallback"/>.</summary>
        private static double ClampFinite(double value, double min, double max, double fallback) =>
            double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;

        private string _name = "新規虹彩デザイン";
        private double _sizeMm = DefaultSizeMm;
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
        private double _pupilCornerRounding = 0.0; // 0 = sharp corners (files saved before this existed load as 0)
        private double _pupilFacetIntensity = 0.0; // 宝石カット: strength of the light / dark facets inside the pupil. 0 = off
        private int _pupilFacetCount = 10;          // facets round the pupil

        // Nebula layer (星雲): soft clouds over the iris body. 0 = off.
        private string _nebulaColor = DefaultNebulaColor;
        private double _nebulaOpacity = 0.0;
        private double _nebulaScale = 1.2;    // cloud size (higher = smaller, busier clouds)
        private double _nebulaDensity = 0.5;  // how much of the iris the clouds cover
        private double _nebulaSwirl = 0.4;    // -1..1: spiral twist round the pupil
        private LayerBlend _glitterBlend = LayerBlend.Normal;
        private LayerBlend _auroraBlend = LayerBlend.Normal;
        private double _fiberNoiseScale = 1.0;
        private double _fiberNoise = 0.0;
        private double _lightStrength = 0.0;
        private double _lightAngle = 270.0;

        // Radial Pattern (放射状ライン / 放射模様)
        private int _rayCount = 48;
        private double _rayThickness = 1.2;
        private double _rayLengthRatio = 0.65;
        private double _rayIntensity = 0.65;
        private double _rayRandomness = 0.45;
        private string _rayColor = "#B3E5FC";
        private double _rayInnerRadiusRatio = 0.32;
        // Fiber look (繊維の質感). On by default (also for files saved before it existed); all 0 = plain straight lines.
        private double _rayTaper = DefaultRayTaper;         // 0 = even width, 1 = wide at the pupil, pointed at the tip
        private double _rayWidthVariation = DefaultRayWidthVariation;  // per-ray width spread
        private double _rayCurve = DefaultRayCurve;         // -1..1: bend (swirl) direction and amount
        private double _rayFineLayer = DefaultRayFineLayer;       // strength of a second layer of fine rays between the main ones

        // Aurora layer (オーロラ層): a color laid over the iris body, usually a conic (円周) gradient. 0 = off.
        private string _auroraColor = DefaultAuroraColor;
        private double _auroraOpacity = 0.0;

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
        // Glowing glitter (光るラメ). On by default (also for older files); 0 = plain grains.
        private double _particleGlow = DefaultParticleGlow;       // soft halo around every grain
        private double _particleFlareRatio = DefaultParticleFlareRatio;   // share of grains that also get a cross-shaped flare
        private double _particleFlareLength = DefaultParticleFlareLength;  // flare length, in grain sizes

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
            set { _sizeMm = Math.Clamp(value, MinSizeMm, MaxSizeMm); OnPropertyChanged(); }
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

        /// <summary>
        /// How much the corners of the cat slit / heart / star pupil are rounded: 0 = sharp (as before) to 1 = fully rounded.
        /// No effect on the round and oval pupils.
        /// </summary>
        /// <summary>宝石カット: strength (0..1) of the faceted light / dark planes cut into the pupil, like a gem. 0 = off.</summary>
        public double PupilFacetIntensity
        {
            get => _pupilFacetIntensity;
            set { _pupilFacetIntensity = ClampFinite(value, 0.0, 1.0, 0.0); OnPropertyChanged(); }
        }

        /// <summary>カット面の数: facets round the pupil.</summary>
        public int PupilFacetCount
        {
            get => _pupilFacetCount;
            set { _pupilFacetCount = Math.Clamp(value, MinPupilFacetCount, MaxPupilFacetCount); OnPropertyChanged(); }
        }

        /// <summary>Color of the nebula clouds (solid or gradient, laid across the iris like the other iris-wide colors).</summary>
        public string NebulaColor
        {
            get => _nebulaColor;
            set { _nebulaColor = value ?? DefaultNebulaColor; OnPropertyChanged(); }
        }

        /// <summary>Strength of the nebula layer. 0 = off.</summary>
        public double NebulaOpacity
        {
            get => _nebulaOpacity;
            set { _nebulaOpacity = ClampFinite(value, 0.0, 1.0, 0.0); OnPropertyChanged(); }
        }

        /// <summary>雲の細かさ: higher values give smaller, busier clouds.</summary>
        public double NebulaScale
        {
            get => _nebulaScale;
            set { _nebulaScale = ClampFinite(value, MinNebulaScale, MaxNebulaScale, 1.2); OnPropertyChanged(); }
        }

        /// <summary>雲の量: how much of the iris the clouds cover (0..1).</summary>
        public double NebulaDensity
        {
            get => _nebulaDensity;
            set { _nebulaDensity = ClampFinite(value, 0.0, 1.0, 0.5); OnPropertyChanged(); }
        }

        /// <summary>渦巻き: spiral twist of the clouds round the pupil; the sign picks the direction (mirrored in the right eye).</summary>
        public double NebulaSwirl
        {
            get => _nebulaSwirl;
            set { _nebulaSwirl = ClampFinite(value, -1.0, 1.0, 0.4); OnPropertyChanged(); }
        }

        /// <summary>光の向き: the direction the light comes from, in degrees on screen (0 = right, 90 = down, 270 = up). Mirrored in the right eye when the highlights are.</summary>
        public double LightAngle
        {
            get => _lightAngle;
            set { _lightAngle = ClampFinite(value, 0.0, 360.0, 270.0); OnPropertyChanged(); }
        }

        /// <summary>光の強さ: how far the brightest point of the base gradient moves toward the light (0 = centered, off).</summary>
        public double LightStrength
        {
            get => _lightStrength;
            set { _lightStrength = ClampFinite(value, 0.0, 1.0, 0.0); OnPropertyChanged(); }
        }

        /// <summary>繊維ノイズ: strength of the fine fibrous noise laid over the iris (feTurbulence in the SVG). 0 = off.</summary>
        public double FiberNoise
        {
            get => _fiberNoise;
            set { _fiberNoise = ClampFinite(value, 0.0, 1.0, 0.0); OnPropertyChanged(); }
        }

        /// <summary>繊維ノイズの細かさ: higher values give thinner, denser fibers.</summary>
        public double FiberNoiseScale
        {
            get => _fiberNoiseScale;
            set { _fiberNoiseScale = ClampFinite(value, 0.5, 2.0, 1.0); OnPropertyChanged(); }
        }

        /// <summary>オーロラ層の重ね方: How the aurora layer is laid over the iris. Unknown numbers from a file become Normal.</summary>
        public LayerBlend AuroraBlend
        {
            get => _auroraBlend;
            set { _auroraBlend = Enum.IsDefined(value) ? value : LayerBlend.Normal; OnPropertyChanged(); }
        }

        /// <summary>ラメの重ね方: How the glitter (and its glow) is laid over the iris. Unknown numbers from a file become Normal.</summary>
        public LayerBlend GlitterBlend
        {
            get => _glitterBlend;
            set { _glitterBlend = Enum.IsDefined(value) ? value : LayerBlend.Normal; OnPropertyChanged(); }
        }


        public double PupilCornerRounding
        {
            get => _pupilCornerRounding;
            set { _pupilCornerRounding = ClampCornerRounding(value); OnPropertyChanged(); }
        }

        /// <summary>Corner rounding is 0..1; NaN / infinity from a hand-edited file become 0 (sharp).</summary>
        public static double ClampCornerRounding(double value) =>
            double.IsFinite(value) ? Math.Clamp(value, 0.0, 1.0) : 0.0;

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

        /// <summary>先細り: 0 = even width (round caps), 1 = wide at the pupil side and pointed at the tip.</summary>
        public double RayTaper
        {
            get => _rayTaper;
            set { _rayTaper = ClampFinite(value, 0.0, 1.0, DefaultRayTaper); OnPropertyChanged(); }
        }

        /// <summary>太さのばらつき: 0 = every ray as wide as 線幅, 1 = from about a third to twice as wide.</summary>
        public double RayWidthVariation
        {
            get => _rayWidthVariation;
            set { _rayWidthVariation = ClampFinite(value, 0.0, 1.0, DefaultRayWidthVariation); OnPropertyChanged(); }
        }

        /// <summary>うねり: rays bend round the pupil (swirl); the sign picks the direction, the right eye mirrors it.</summary>
        public double RayCurve
        {
            get => _rayCurve;
            set { _rayCurve = ClampFinite(value, MinRayCurve, MaxRayCurve, DefaultRayCurve); OnPropertyChanged(); }
        }

        /// <summary>細線レイヤー: opacity (relative to 強度) of a second set of thin rays between the main ones.</summary>
        public double RayFineLayer
        {
            get => _rayFineLayer;
            set { _rayFineLayer = ClampFinite(value, 0.0, 1.0, DefaultRayFineLayer); OnPropertyChanged(); }
        }

        /// <summary>True when any fiber setting is on; the rays are then drawn as shaped outlines instead of plain lines.</summary>
        [JsonIgnore]
        public bool HasFiberRays => RayTaper > 0.001 || RayWidthVariation > 0.001 || Math.Abs(RayCurve) > 0.001;

        /// <summary>Color of the aurora layer (solid or gradient; a conic gradient gives the aurora look).</summary>
        public string AuroraColor
        {
            get => _auroraColor;
            set { _auroraColor = value ?? DefaultAuroraColor; OnPropertyChanged(); }
        }

        /// <summary>Strength of the aurora layer over the iris body. 0 = off.</summary>
        public double AuroraOpacity
        {
            get => _auroraOpacity;
            set { _auroraOpacity = ClampFinite(value, 0.0, 1.0, 0.0); OnPropertyChanged(); }
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

        /// <summary>光のにじみ: a soft halo of the grain's color around every grain. 0 = off.</summary>
        public double ParticleGlow
        {
            get => _particleGlow;
            set { _particleGlow = ClampFinite(value, 0.0, 1.0, DefaultParticleGlow); OnPropertyChanged(); }
        }

        /// <summary>十字フレアの割合: share of grains (0..1) that also get a thin cross-shaped flare.</summary>
        public double ParticleFlareRatio
        {
            get => _particleFlareRatio;
            set { _particleFlareRatio = ClampFinite(value, 0.0, 1.0, DefaultParticleFlareRatio); OnPropertyChanged(); }
        }

        /// <summary>フレアの長さ: flare reach, in multiples of the grain's size.</summary>
        public double ParticleFlareLength
        {
            get => _particleFlareLength;
            set { _particleFlareLength = ClampFinite(value, MinParticleFlareLength, MaxParticleFlareLength, DefaultParticleFlareLength); OnPropertyChanged(); }
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
