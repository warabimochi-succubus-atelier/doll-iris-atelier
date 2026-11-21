using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using DollIrisGenerator.Services;

namespace DollIrisGenerator.Models
{
    public class PresetDefinition : INotifyPropertyChanged
    {
        private string _name = "";

        /// <summary>Shown in the preset list; a renamed preset updates there at once.</summary>
        public string Name
        {
            get => _name;
            set
            {
                if (_name == value) return;
                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Description { get; set; } = "";
        public string BaseColor { get; set; } = "#2979FF";
        public string InnerColor { get; set; } = "#80D8FF";
        public string OuterColor { get; set; } = "#0D47A1";
        public string BottomHighlightColor { get; set; } = "#B3E5FC";
        public string LimbalRingColor { get; set; } = "#051636";
        public string PupilColor { get; set; } = "#060A18";
        public string RayColor { get; set; } = "#B3E5FC";
        public string ParticleColor { get; set; } = "#FFFFFF";
        public SymbolType DefaultSymbol { get; set; } = SymbolType.None;
        public string SymbolColor { get; set; } = "#FFFFFF";
        public PupilShape PupilShape { get; set; } = PupilShape.Round;
        public double InnerGradientRadius { get; set; } = 0.55;
        public double OuterGradientRadius { get; set; } = 0.95;

        // Layers: always applied (0 = off), so picking a plain preset clears another preset's aurora / nebula / gem cut
        public string? AuroraColor { get; set; }
        public double AuroraOpacity { get; set; }
        public string? NebulaColor { get; set; }
        public double NebulaOpacity { get; set; }
        public double PupilFacetIntensity { get; set; }

        // Look settings. null = the app's default value, never the design's current one: a preset always gives the
        // same look whatever was picked before it.
        public double? NebulaDensity { get; set; }
        public double? NebulaSwirl { get; set; }
        public int? PupilFacetCount { get; set; }
        public double? PupilSizeRatio { get; set; }
        public double? BottomHighlightIntensity { get; set; }
        public int? RayCount { get; set; }
        public double? RayIntensity { get; set; }
        public int? ParticleCount { get; set; }
        public double? ParticleRadiusRatio { get; set; }
        public double? ParticleGlow { get; set; }
        public double? ParticleFlareRatio { get; set; }
        public double? SymbolSize { get; set; }
        public double? SymbolPositionX { get; set; }
        public double? SymbolPositionY { get; set; }
        public int? SymbolCount { get; set; }
        public double? SymbolOpacity { get; set; }

        // Detail settings (limbal ring, pupil, rays, glitter, nebula)
        public double? LimbalRingThickness { get; set; }
        public double? LimbalRingOpacity { get; set; }
        public double? LimbalRingSoftness { get; set; }
        public double? PupilOpacity { get; set; }
        public double? PupilSoftness { get; set; }
        public double? PupilCornerRounding { get; set; }
        public double? PupilStretchTop { get; set; }
        public double? PupilStretchBottom { get; set; }
        public double? PupilStretchLeft { get; set; }
        public double? PupilStretchRight { get; set; }
        public double? PupilStarCoreScale { get; set; }
        public double? NebulaScale { get; set; }
        public double? RayThickness { get; set; }
        public double? RayLengthRatio { get; set; }
        public double? RayRandomness { get; set; }
        public double? RayInnerRadiusRatio { get; set; }
        public double? RayTaper { get; set; }
        public double? RayWidthVariation { get; set; }
        public double? RayCurve { get; set; }
        public double? RayFineLayer { get; set; }
        public double? ParticleMinSize { get; set; }
        public double? ParticleMaxSize { get; set; }
        public double? ParticleOpacity { get; set; }
        public double? ParticleFlareLength { get; set; }
        // 見える範囲 (放射ライン・ラメ・レンズ風リング)
        public double? RayRangeAngle { get; set; }
        public double? RayRangeWidth { get; set; }
        public double? RayRangeFeather { get; set; }
        public double? ParticleRangeAngle { get; set; }
        public double? ParticleRangeWidth { get; set; }
        public double? ParticleRangeFeather { get; set; }
        public double? LensRangeAngle { get; set; }
        public double? LensRangeWidth { get; set; }
        public double? LensRangeFeather { get; set; }
        // 宝石カット面
        public double? IrisFacetIntensity { get; set; }
        public double? IrisFacetTableSize { get; set; }
        public double? IrisFacetShadeAngle { get; set; }
        public double? IrisFacetShadeBias { get; set; }
        public double? IrisFacetRangeAngle { get; set; }
        public double? IrisFacetRangeWidth { get; set; }
        public double? IrisFacetRangeFeather { get; set; }
        public int? IrisFacetCount { get; set; }
        public LayerBlend? IrisFacetBlend { get; set; }
        public LayerBlend? GlitterBlend { get; set; }
        public LayerBlend? AuroraBlend { get; set; }
        public double? FiberNoiseScale { get; set; }
        public double? FiberNoise { get; set; }
        public double? LightStrength { get; set; }
        public double? LightAngle { get; set; }

        // Highlights: null = the app's default highlights
        public List<HighlightItem>? Highlights { get; set; }
        // 右目の反転 of the pupil and the light (each highlight carries its own in the list): null = 鏡像
        public RightEyeFlip? PupilRightEyeFlip { get; set; }
        public RightEyeFlip? LightRightEyeFlip { get; set; }

        // Lens pattern
        public bool? LensPatternEnabled { get; set; }
        public int? LensRingCount { get; set; }
        public double? LensRingIntensity { get; set; }
        public double? LensRingWidth { get; set; }
        public string? LensRingColor { get; set; }
        public double? LensRefractionGlow { get; set; }

        /// <summary>
        /// The whole symbol list (presets made from a design). When set, it replaces the design's symbols and
        /// <see cref="DefaultSymbol"/> and the Symbol* settings are not used.
        /// </summary>
        public List<DecorativeSymbolItem>? Symbols { get; set; }
    }

    public static class ColorPresets
    {
        /// <summary>
        /// The built-in presets (SPECIFICATION.md 7.2). The list the user sees (with the presets they added) is
        /// MainViewModel.Presets. Each preset has its own highlight layout (<see cref="HighlightStyles"/>).
        /// </summary>
        public static IReadOnlyList<PresetDefinition> All { get; } = new List<PresetDefinition>
        {
            // ---------------- ベーシック: one hue, round pupil, fiber rays and glitter ----------------
            // The first one is the preset selected at start-up
            Basic("クリアブルー", "澄んだ水色のガラスのようなクリアブルー",
                "#2E86FF", "#9BE1FF", "#0A2A8A", "#CFF3FF", "#06173F", "#071030", "#C9F0FF", HighlightStyle.Classic),
            // Five hues; the purple, red, aqua and silver looks are in 雰囲気 (C, D, F, G)
            Basic("ピンク", "甘く華やかなローズピンク",
                "#F06292", "#FFC1D9", "#8E1F4F", "#FFE6F0", "#4A0A27", "#3A0A20", "#FFD6E6", HighlightStyle.Heart),
            Basic("グリーン", "透明感のあるエメラルドグリーン",
                "#19A36B", "#8FF0C0", "#064D33", "#D5FFEA", "#022B1C", "#021F14", "#C6FFE3", HighlightStyle.Droplet),
            Basic("アンバー", "蜂蜜のようにとろける琥珀色",
                "#E08A1E", "#FFD27A", "#7A3A06", "#FFE9B8", "#3A1A03", "#2A1202", "#FFE3A8", HighlightStyle.ThreeDots),
            // A natural brown: muted glitter, more fibers and soft-edged highlights that melt into the iris
            Tweak(Basic("ブラウン", "あたたかく落ち着いたチョコレートブラウン。光はやわらか",
                "#8A5A33", "#D9A86C", "#3A2010", "#F2D2A6", "#1E0F06", "#140A04", "#F5DDB8", HighlightStyle.Window),
                p => { p.ParticleCount = 40; p.FiberNoise = 0.35; p.Highlights = HighlightStyles.Build(HighlightStyle.Window, softness: 0.3); }),

            // ---------------- 特殊: layers, shaped pupils and symbols ----------------
            new PresetDefinition
            {
                Name = "特殊: 星屑",
                Description = "濃紺の虹彩いっぱいに光る星屑が散らばる",
                BaseColor = "#1C2C8C", InnerColor = "#4E7CFF", OuterColor = "#070D33", BottomHighlightColor = "#8FB4FF",
                LimbalRingColor = "#03061A", PupilColor = "#02040F", RayColor = "#9FB8FF", ParticleColor = "#FFFFFF",
                RayCount = 48, RayIntensity = 0.35, ParticleCount = 230, ParticleRadiusRatio = 0.92,
                ParticleGlow = 0.7, ParticleFlareRatio = 0.15, BottomHighlightIntensity = 0.45,
                Highlights = HighlightStyles.Build(HighlightStyle.Sparkle, glow: 0.6)
            },
            new PresetDefinition
            {
                Name = "特殊: オーロラ",
                Description = "ピンク・ブルー・パープルが瞳孔のまわりで移り変わる",
                BaseColor = "#6F7CE0", InnerColor = "#D7C8FF", OuterColor = "#2A2370", BottomHighlightColor = "#E3F6FF",
                LimbalRingColor = "#140F3A", PupilColor = "#0B0826", ParticleColor = "#FFFFFF",
                RayColor = "conic-gradient(from 270deg, #FFFFFF 0%, #FF80AB 33%, #80D8FF 66%, #FFFFFF 100%)",
                AuroraColor = IrisParameters.DefaultAuroraColor, AuroraOpacity = 0.55, AuroraBlend = LayerBlend.Screen,
                RayCount = 64, RayIntensity = 0.65, ParticleCount = 90, ParticleGlow = 0.7, ParticleFlareRatio = 0.12,
                Highlights = HighlightStyles.Build(HighlightStyle.Window, softness: 0.3)
            },
            new PresetDefinition
            {
                Name = "特殊: 銀河",
                Description = "紫と青の星雲が渦巻く銀河",
                BaseColor = "#3D2A9A", InnerColor = "#7E57C2", OuterColor = "#12082E", BottomHighlightColor = "#B39DDB",
                LimbalRingColor = "#0A0420", PupilColor = "#05020F", RayColor = "#D1C4E9", ParticleColor = "#FFFFFF",
                NebulaColor = IrisParameters.DefaultNebulaColor, NebulaOpacity = 0.85, NebulaDensity = 0.55, NebulaSwirl = 0.5,
                RayCount = 48, RayIntensity = 0.3, ParticleCount = 170, ParticleRadiusRatio = 0.92,
                ParticleGlow = 0.8, ParticleFlareRatio = 0.12,
                Highlights = HighlightStyles.Build(HighlightStyle.Sparkle, glow: 0.5)
            },
            new PresetDefinition
            {
                Name = "特殊: 夕焼け",
                Description = "オレンジから深紅へ沈む夕焼け色。光の筋は下側だけ",
                BaseColor = "#F2683C", InnerColor = "#FFD08A", OuterColor = "#8A1E12", BottomHighlightColor = "#FFE1B0",
                LimbalRingColor = "#3D0A05", PupilColor = "radial-gradient(#C62828 0%, #4A0505 100%)", RayColor = "#FFE0B0",
                ParticleColor = "#FFF1D6", RayCount = 64, RayIntensity = 0.7, ParticleCount = 80, ParticleGlow = 0.6,
                BottomHighlightIntensity = 0.6, LightStrength = 0.35, LightAngle = 250,
                // The light of the setting sun from below: rays on the lower side only
                RayRangeWidth = 200, RayRangeFeather = 50,
                Highlights = HighlightStyles.Build(HighlightStyle.Droplet, softness: 0.25, glow: 0.35)
            },
            new PresetDefinition
            {
                Name = "特殊: 夜空",
                Description = "夜空の濃紺に小さなハートの瞳孔と星",
                BaseColor = "#142060", InnerColor = "#2E4BB0", OuterColor = "#050A24", BottomHighlightColor = "#5C7CFA",
                LimbalRingColor = "#02051A", PupilColor = "#060B2E", RayColor = "#90A4F8", ParticleColor = "#FFFFFF",
                PupilShape = PupilShape.Heart, PupilSizeRatio = 0.3, PupilFacetIntensity = 0.35, PupilFacetCount = 8,
                RayCount = 40, RayIntensity = 0.3, ParticleCount = 160, ParticleRadiusRatio = 0.9,
                ParticleGlow = 0.85, ParticleFlareRatio = 0.12,
                DefaultSymbol = SymbolType.Star4, SymbolColor = "#FFFFFF", SymbolSize = 0.12, SymbolPositionX = 0.4, SymbolPositionY = -0.35, SymbolCount = 1,
                Highlights = HighlightStyles.Build(HighlightStyle.Classic, glow: 0.5)
            },
            new PresetDefinition
            {
                Name = "特殊: 宝石",
                Description = "カットされた宝石のハートと、虹彩全体のカット面",
                BaseColor = "#2E8FC8", InnerColor = "#9FE6FF", OuterColor = "#0B3A6A", BottomHighlightColor = "#D8F6FF",
                LimbalRingColor = "#041A33", PupilColor = "radial-gradient(#B3F0FF 0%, #29B6F6 45%, #0B4F8A 100%)",
                RayColor = "#D6F5FF", ParticleColor = "#FFFFFF",
                PupilShape = PupilShape.Heart, PupilSizeRatio = 0.46, PupilFacetIntensity = 0.85, PupilFacetCount = 10,
                RayCount = 56, RayIntensity = 0.45, ParticleCount = 120, ParticleGlow = 0.8, ParticleFlareRatio = 0.15,
                IrisFacetIntensity = 0.45, IrisFacetCount = 10, IrisFacetTableSize = 0.5,
                Highlights = HighlightStyles.Build(HighlightStyle.Diamond)
            },
            new PresetDefinition
            {
                Name = "特殊: ハート",
                Description = "淡いピンクに、ふちどりのハートが浮かぶ",
                BaseColor = "#F48FB1", InnerColor = "#FFE0EC", OuterColor = "#C2185B", BottomHighlightColor = "#FFF0F6",
                LimbalRingColor = "#6A0A33", PupilColor = "#C2185B", RayColor = "#FFFFFF", ParticleColor = "#FFFFFF",
                PupilShape = PupilShape.Heart, PupilSizeRatio = 0.46,
                RayCount = 64, RayIntensity = 0.5, ParticleCount = 110, ParticleGlow = 0.75, ParticleFlareRatio = 0.1,
                // A pale heart over the pupil heart leaves only its rim: an outlined heart
                DefaultSymbol = SymbolType.Heart, SymbolColor = "#FFC1D9", SymbolSize = 0.32,
                SymbolPositionX = 0.0, SymbolPositionY = 0.0, SymbolCount = 1, SymbolOpacity = 1.0,
                Highlights = HighlightStyles.Build(HighlightStyle.Heart)
            },
            new PresetDefinition
            {
                Name = "特殊: 桜",
                Description = "桜の花びらが舞う春色のソフトピンク",
                BaseColor = "#FF6090", InnerColor = "#FFB2D0", OuterColor = "#7A0035", BottomHighlightColor = "#FFF0F5",
                LimbalRingColor = "#3B0018", PupilColor = "#1F000C", RayColor = "#FFD6E7", ParticleColor = "#FFFFFF",
                RayCount = 64, RayIntensity = 0.6, ParticleCount = 70, ParticleGlow = 0.7, ParticleFlareRatio = 0.12,
                LightStrength = 0.25, LightAngle = 235,
                Symbols = new List<DecorativeSymbolItem>
                {
                    Symbol(SymbolType.SakuraPetal, "#FFF5F8", 0.17, 0.36, -0.42, rotation: 20),
                    Symbol(SymbolType.SakuraPetal, "#FFE3EE", 0.12, -0.40, 0.38, rotation: -35),
                    Symbol(SymbolType.SakuraPetal, "#FFF5F8", 0.08, 0.52, -0.12, rotation: 60, opacity: 0.7)
                },
                Highlights = HighlightStyles.Build(HighlightStyle.Window)
            },
            new PresetDefinition
            {
                Name = "特殊: キャットアイ",
                Description = "縦長の瞳孔を持つ、魅惑的なゴールドの猫目",
                BaseColor = "#FFC400", InnerColor = "#C6F000", OuterColor = "#B33A0A", BottomHighlightColor = "#FFFF8D",
                LimbalRingColor = "#260E04", PupilColor = "#0D0502", RayColor = "#F4FF81", ParticleColor = "#FFFFFF",
                PupilShape = PupilShape.CatSlit, PupilSizeRatio = 0.42,
                RayCount = 80, RayIntensity = 0.7, RayCurve = 0.0, ParticleCount = 40, ParticleGlow = 0.6,
                FiberNoise = 0.4, LightStrength = 0.3, LightAngle = 240,
                Highlights = HighlightStyles.Build(HighlightStyle.Sparkle)
            },
            new PresetDefinition
            {
                Name = "特殊: 蝶々",
                Description = "藤色から水色へ。蝶々の瞳孔と小さな蝶",
                BaseColor = "#7C6FE0", InnerColor = "#B8F0FF", OuterColor = "#2C1E78", BottomHighlightColor = "#C9F6FF",
                LimbalRingColor = "#140D40", PupilColor = "radial-gradient(#3A2A8C 0%, #0A0628 100%)",
                RayColor = "#E6F7FF", ParticleColor = "#FFFFFF",
                PupilShape = PupilShape.Butterfly, PupilSizeRatio = 0.44,
                RayCount = 64, RayIntensity = 0.55, ParticleCount = 90, ParticleGlow = 0.75, ParticleFlareRatio = 0.12,
                AuroraColor = "conic-gradient(from 270deg, #B388FF 0%, #80D8FF 50%, #B388FF 100%)", AuroraOpacity = 0.3,
                AuroraBlend = LayerBlend.Screen,
                Symbols = new List<DecorativeSymbolItem>
                {
                    Symbol(SymbolType.Butterfly, "#E0F7FF", 0.13, 0.42, -0.42, rotation: 15),
                    Symbol(SymbolType.Butterfly, "#F1E6FF", 0.09, -0.44, 0.40, rotation: -20, opacity: 0.75)
                },
                Highlights = HighlightStyles.Build(HighlightStyle.Classic)
            },
            new PresetDefinition
            {
                Name = "特殊: 三日月",
                Description = "真夜中の青に、金色の光の筋と三日月",
                BaseColor = "#1F2B6C", InnerColor = "#4A63B8", OuterColor = "#070B26", BottomHighlightColor = "#FFE7A3",
                LimbalRingColor = "#03061A", PupilColor = "#03051A", RayColor = "#FFE7A3", ParticleColor = "#FFF1C2",
                RayCount = 72, RayIntensity = 0.55, ParticleCount = 150, ParticleRadiusRatio = 0.9,
                ParticleGlow = 0.85, ParticleFlareRatio = 0.15, BottomHighlightIntensity = 0.5,
                Symbols = new List<DecorativeSymbolItem>
                {
                    Symbol(SymbolType.Moon, "#FFE08A", 0.2, 0.40, -0.40, rotation: -20, opacity: 0.95),
                    Symbol(SymbolType.Star4, "#FFF1C2", 0.08, 0.58, -0.12, opacity: 0.9)
                },
                Highlights = HighlightStyles.Build(HighlightStyle.Crescent, glow: 0.6)
            },
            new PresetDefinition
            {
                Name = "特殊: クリスタル",
                Description = "氷のような水色に、ダイヤの瞳孔・カット面・光の輪",
                BaseColor = "#7FC8F0", InnerColor = "#E6FAFF", OuterColor = "#24628E", BottomHighlightColor = "#F2FDFF",
                LimbalRingColor = "#0E3250", PupilColor = "radial-gradient(#F2FDFF 0%, #6EC6F0 45%, #1A5A8A 100%)",
                RayColor = "#FFFFFF", ParticleColor = "#FFFFFF",
                PupilShape = PupilShape.Diamond, PupilSizeRatio = 0.4, PupilFacetIntensity = 0.8, PupilFacetCount = 8,
                RayCount = 96, RayIntensity = 0.5, RayThickness = 0.9, ParticleCount = 100, ParticleGlow = 0.8,
                IrisFacetIntensity = 0.55, IrisFacetCount = 12, IrisFacetTableSize = 0.4, IrisFacetBlend = LayerBlend.SoftLight,
                LensPatternEnabled = true, LensRingCount = 5, LensRingIntensity = 0.35, LensRingColor = "#FFFFFF",
                Highlights = HighlightStyles.Build(HighlightStyle.Diamond)
            },

            // ---------------- 雰囲気: a basic look with its own glitter, pupil and highlights ----------------
            Tweak(Mood("A. ナチュラル (ピンク系)", "やわらかく自然なピンク。ラメは控えめ",
                "#E58BA7", "#FBD3DF", "#9A4762", "#FFEFF4", "#4A1A2A", "#2E1019", "#FBE0E8", HighlightStyle.Classic,
                particles: 40, glow: 0.5, highlightSoftness: 0.25),
                p => { p.FiberNoise = 0.35; }),
            Mood("B. 精霊 (ブルー系)", "光をまとったような幻想的なブルー",
                "#3D7BEA", "#BDEBFF", "#13308A", "#E4F7FF", "#07164A", "#050E30", "#DDF4FF", HighlightStyle.Window,
                particles: 150, glow: 0.7, highlightGlow: 0.5,
                aurora: "conic-gradient(from 270deg, #80D8FF 0%, #FFFFFF 33%, #82B1FF 66%, #80D8FF 100%)", auroraOpacity: 0.3),
            Tweak(Mood("C. アンニュイ (パープル系)", "けだるく落ち着いたスモーキーパープル。光の筋は下側だけ",
                "#7A5BA8", "#C9B5E6", "#2E1F4D", "#E6DCF2", "#170F29", "#0F0A1C", "#DCCFEE", HighlightStyle.Droplet,
                particles: 40, glow: 0.45, rays: 0.5, highlightSoftness: 0.35),
                p => { p.RayRangeWidth = 180; p.RayRangeFeather = 60; p.FiberNoise = 0.3; }),
            Mood("D. つんでれ (レッド系)", "強気なのに甘さもある鮮やかなレッド",
                "#E8364F", "#FF9AA8", "#7A0A1E", "#FFE0E4", "#3B0510", "#24030A", "#FFD1D8", HighlightStyle.Sparkle,
                particles: 80, glow: 0.7,
                pupilStretchX: 0.75, pupilStretchY: 1.25), // a tall oval pupil
            Tweak(Mood("E. 小悪魔 (赤紫系)", "ハートの瞳孔がのぞく、あやしい赤紫",
                "#C2185B", "#F06292", "#4A0628", "#FFD6E8", "#2A0216", "#1A010D", "#F8BBD0", HighlightStyle.Heart,
                particles: 90, glow: 0.8,
                pupil: PupilShape.Heart, facets: 0.6),
                p => { p.PupilSizeRatio = 0.38; }),
            Mood("F. 無邪気 (アクア系)", "くりっとした瞳孔の明るいアクア",
                "#22C3D6", "#B2F5FF", "#0A6B85", "#E0FDFF", "#033A48", "#022833", "#D2FBFF", HighlightStyle.ThreeDots,
                particles: 80, glow: 0.7,
                pupilSize: 0.38),
            Tweak(Mood("G. 儚げ (シルバー系)", "淡く透ける銀青色。外輪もやわらか",
                "#A8B8CC", "#EEF4FA", "#5A6B82", "#F7FBFF", "#3A4658", "#1C2430", "#FFFFFF", HighlightStyle.Window,
                particles: 120, glow: 0.6, rays: 0.45, pupilSize: 0.28, highlightSoftness: 0.3, highlightGlow: 0.3),
                p => { p.LimbalRingSoftness = 0.45; p.LimbalRingOpacity = 0.7; p.ParticleMinSize = 0.6; p.ParticleMaxSize = 1.6; p.LightStrength = 0.35; }),
            Tweak(Mood("H. クール (アイス系)", "小さな瞳孔に細かな光の筋。上側にカット面",
                "#1FA7D8", "#C8F4FF", "#073C66", "#E6FAFF", "#021C33", "#020F1C", "#E6FAFF", HighlightStyle.Sparkle,
                particles: 50, glow: 0.6, rays: 0.75, pupilSize: 0.26),
                p =>
                {
                    p.RayCount = 96; p.RayThickness = 0.9; p.RayCurve = 0.0;
                    p.IrisFacetIntensity = 0.35; p.IrisFacetCount = 12;
                    p.IrisFacetRangeAngle = 270; p.IrisFacetRangeWidth = 200; p.IrisFacetRangeFeather = 60;
                })
        };

        /// <summary>
        /// ベーシック: fiber rays, glowing glitter and a soft light from the upper left, in one hue with a round pupil.
        /// </summary>
        private static PresetDefinition Basic(string name, string description, string baseColor, string inner, string outer,
            string bottom, string limbal, string pupil, string ray, HighlightStyle highlights) => new()
        {
            Name = "ベーシック: " + name,
            Description = description,
            BaseColor = baseColor, InnerColor = inner, OuterColor = outer, BottomHighlightColor = bottom,
            LimbalRingColor = limbal, PupilColor = pupil, RayColor = ray, ParticleColor = "#FFFFFF",
            PupilSizeRatio = 0.32,
            RayCount = 64, RayIntensity = 0.65, ParticleCount = 75, ParticleRadiusRatio = 0.8,
            ParticleGlow = 0.7, ParticleFlareRatio = 0.15, BottomHighlightIntensity = 0.45,
            FiberNoise = 0.2, LightStrength = 0.25, LightAngle = 235,
            Highlights = HighlightStyles.Build(highlights)
        };

        /// <summary>雰囲気: a basic look with its own glitter amount and an optional pupil / layer twist.</summary>
        private static PresetDefinition Mood(string name, string description, string baseColor, string inner, string outer,
            string bottom, string limbal, string pupilColor, string ray, HighlightStyle highlights, int particles, double glow,
            double rays = 0.6, PupilShape pupil = PupilShape.Round, double pupilSize = 0.32, double facets = 0.0,
            string? aurora = null, double auroraOpacity = 0.0, double pupilStretchX = 1.0, double pupilStretchY = 1.0,
            double highlightSoftness = 0.0, double highlightGlow = 0.0) => new()
        {
            Name = "雰囲気: " + name,
            Description = description,
            BaseColor = baseColor, InnerColor = inner, OuterColor = outer, BottomHighlightColor = bottom,
            LimbalRingColor = limbal, PupilColor = pupilColor, RayColor = ray, ParticleColor = "#FFFFFF",
            PupilShape = pupil, PupilSizeRatio = pupilSize, PupilFacetIntensity = facets, PupilFacetCount = 8,
            PupilStretchLeft = pupilStretchX, PupilStretchRight = pupilStretchX,
            PupilStretchTop = pupilStretchY, PupilStretchBottom = pupilStretchY,
            AuroraColor = aurora, AuroraOpacity = auroraOpacity,
            RayCount = 60, RayIntensity = rays, ParticleCount = particles, ParticleRadiusRatio = 0.8,
            ParticleGlow = glow, ParticleFlareRatio = 0.12, LightStrength = 0.25, LightAngle = 235,
            Highlights = HighlightStyles.Build(highlights, softness: highlightSoftness, glow: highlightGlow)
        };

        /// <summary>Changes a few settings of a preset made by <see cref="Basic"/> or <see cref="Mood"/>.</summary>
        private static PresetDefinition Tweak(PresetDefinition preset, Action<PresetDefinition> change)
        {
            change(preset);
            return preset;
        }

        /// <summary>A symbol of a preset's symbol list.</summary>
        private static DecorativeSymbolItem Symbol(SymbolType type, string color, double size, double x, double y,
            double rotation = 0.0, double opacity = 0.9) => new()
        {
            Type = type, Color = color, Size = size, PositionX = x, PositionY = y, Rotation = rotation,
            Opacity = opacity, Count = 1, Enabled = true
        };

        public static void ApplyPreset(IrisParameters target, PresetDefinition preset)
        {
            target.BaseColor = preset.BaseColor;
            target.InnerColor = preset.InnerColor;
            target.OuterColor = preset.OuterColor;
            target.BottomHighlightColor = preset.BottomHighlightColor;
            target.LimbalRingColor = preset.LimbalRingColor;
            target.PupilColor = preset.PupilColor;
            target.RayColor = preset.RayColor;
            target.ParticleColor = preset.ParticleColor;
            target.PupilShape = preset.PupilShape;
            target.InnerGradientRadius = preset.InnerGradientRadius;
            target.OuterGradientRadius = preset.OuterGradientRadius;

            // Everything the preset leaves out goes back to the app's default, so nothing from the previously picked
            // preset (or from hand edits to these settings) stays behind
            var d = new IrisParameters();

            // Layers
            target.AuroraColor = preset.AuroraColor ?? d.AuroraColor;
            target.AuroraOpacity = preset.AuroraOpacity;
            target.NebulaColor = preset.NebulaColor ?? d.NebulaColor;
            target.NebulaOpacity = preset.NebulaOpacity;
            target.PupilFacetIntensity = preset.PupilFacetIntensity;

            // Look settings
            target.NebulaDensity = preset.NebulaDensity ?? d.NebulaDensity;
            target.NebulaSwirl = preset.NebulaSwirl ?? d.NebulaSwirl;
            target.PupilFacetCount = preset.PupilFacetCount ?? d.PupilFacetCount;
            target.PupilSizeRatio = preset.PupilSizeRatio ?? d.PupilSizeRatio;
            target.BottomHighlightIntensity = preset.BottomHighlightIntensity ?? d.BottomHighlightIntensity;
            target.RayCount = preset.RayCount ?? d.RayCount;
            target.RayIntensity = preset.RayIntensity ?? d.RayIntensity;
            target.ParticleCount = preset.ParticleCount ?? d.ParticleCount;
            target.ParticleRadiusRatio = preset.ParticleRadiusRatio ?? d.ParticleRadiusRatio;
            target.ParticleGlow = preset.ParticleGlow ?? d.ParticleGlow;
            target.ParticleFlareRatio = preset.ParticleFlareRatio ?? d.ParticleFlareRatio;

            // Detail settings
            target.LimbalRingThickness = preset.LimbalRingThickness ?? d.LimbalRingThickness;
            target.GlitterBlend = preset.GlitterBlend ?? d.GlitterBlend;
            target.AuroraBlend = preset.AuroraBlend ?? d.AuroraBlend;
            target.FiberNoiseScale = preset.FiberNoiseScale ?? d.FiberNoiseScale;
            target.FiberNoise = preset.FiberNoise ?? d.FiberNoise;
            target.LightStrength = preset.LightStrength ?? d.LightStrength;
            target.LightAngle = preset.LightAngle ?? d.LightAngle;
            target.LimbalRingOpacity = preset.LimbalRingOpacity ?? d.LimbalRingOpacity;
            target.LimbalRingSoftness = preset.LimbalRingSoftness ?? d.LimbalRingSoftness;
            target.PupilOpacity = preset.PupilOpacity ?? d.PupilOpacity;
            target.PupilSoftness = preset.PupilSoftness ?? d.PupilSoftness;
            target.PupilCornerRounding = preset.PupilCornerRounding ?? d.PupilCornerRounding;
            target.PupilStretchTop = preset.PupilStretchTop ?? d.PupilStretchTop;
            target.PupilStretchBottom = preset.PupilStretchBottom ?? d.PupilStretchBottom;
            target.PupilStretchLeft = preset.PupilStretchLeft ?? d.PupilStretchLeft;
            target.PupilStretchRight = preset.PupilStretchRight ?? d.PupilStretchRight;
            target.PupilStarCoreScale = preset.PupilStarCoreScale ?? d.PupilStarCoreScale;
            target.NebulaScale = preset.NebulaScale ?? d.NebulaScale;
            target.RayThickness = preset.RayThickness ?? d.RayThickness;
            target.RayLengthRatio = preset.RayLengthRatio ?? d.RayLengthRatio;
            target.RayRandomness = preset.RayRandomness ?? d.RayRandomness;
            target.RayInnerRadiusRatio = preset.RayInnerRadiusRatio ?? d.RayInnerRadiusRatio;
            target.RayTaper = preset.RayTaper ?? d.RayTaper;
            target.RayWidthVariation = preset.RayWidthVariation ?? d.RayWidthVariation;
            target.RayCurve = preset.RayCurve ?? d.RayCurve;
            target.RayFineLayer = preset.RayFineLayer ?? d.RayFineLayer;
            target.ParticleMinSize = preset.ParticleMinSize ?? d.ParticleMinSize;
            target.ParticleMaxSize = preset.ParticleMaxSize ?? d.ParticleMaxSize;
            target.ParticleOpacity = preset.ParticleOpacity ?? d.ParticleOpacity;
            target.ParticleFlareLength = preset.ParticleFlareLength ?? d.ParticleFlareLength;
            target.RayRangeAngle = preset.RayRangeAngle ?? d.RayRangeAngle;
            target.RayRangeWidth = preset.RayRangeWidth ?? d.RayRangeWidth;
            target.RayRangeFeather = preset.RayRangeFeather ?? d.RayRangeFeather;
            target.ParticleRangeAngle = preset.ParticleRangeAngle ?? d.ParticleRangeAngle;
            target.ParticleRangeWidth = preset.ParticleRangeWidth ?? d.ParticleRangeWidth;
            target.ParticleRangeFeather = preset.ParticleRangeFeather ?? d.ParticleRangeFeather;
            target.LensRangeAngle = preset.LensRangeAngle ?? d.LensRangeAngle;
            target.LensRangeWidth = preset.LensRangeWidth ?? d.LensRangeWidth;
            target.LensRangeFeather = preset.LensRangeFeather ?? d.LensRangeFeather;
            target.IrisFacetIntensity = preset.IrisFacetIntensity ?? d.IrisFacetIntensity;
            target.IrisFacetTableSize = preset.IrisFacetTableSize ?? d.IrisFacetTableSize;
            target.IrisFacetShadeAngle = preset.IrisFacetShadeAngle ?? d.IrisFacetShadeAngle;
            target.IrisFacetShadeBias = preset.IrisFacetShadeBias ?? d.IrisFacetShadeBias;
            target.IrisFacetRangeAngle = preset.IrisFacetRangeAngle ?? d.IrisFacetRangeAngle;
            target.IrisFacetRangeWidth = preset.IrisFacetRangeWidth ?? d.IrisFacetRangeWidth;
            target.IrisFacetRangeFeather = preset.IrisFacetRangeFeather ?? d.IrisFacetRangeFeather;
            target.IrisFacetCount = preset.IrisFacetCount ?? d.IrisFacetCount;
            target.IrisFacetBlend = preset.IrisFacetBlend ?? d.IrisFacetBlend;

            // Highlights (replaced in place, so the list on screen stays bound)
            ReplaceItems(target.Highlights, preset.Highlights ?? d.Highlights.ToList(), h => h.Clone());
            target.PupilRightEyeFlip = preset.PupilRightEyeFlip ?? d.PupilRightEyeFlip;
            target.LightRightEyeFlip = preset.LightRightEyeFlip ?? d.LightRightEyeFlip;

            // Lens pattern
            target.LensPatternEnabled = preset.LensPatternEnabled ?? d.LensPatternEnabled;
            target.LensRingCount = preset.LensRingCount ?? d.LensRingCount;
            target.LensRingIntensity = preset.LensRingIntensity ?? d.LensRingIntensity;
            target.LensRingWidth = preset.LensRingWidth ?? d.LensRingWidth;
            target.LensRingColor = preset.LensRingColor ?? d.LensRingColor;
            target.LensRefractionGlow = preset.LensRefractionGlow ?? d.LensRefractionGlow;

            // A preset made from a design brings back its whole symbol list
            if (preset.Symbols != null)
            {
                ReplaceItems(target.Symbols, preset.Symbols, s => s.Clone());
                return;
            }

            // The preset's symbol (or none) replaces the design's symbols: every other symbol is switched off, so a
            // symbol from another preset, e.g. the outline heart of ハート, does not stay behind
            bool hasSymbol = preset.DefaultSymbol != SymbolType.None;
            for (int i = hasSymbol ? 1 : 0; i < target.Symbols.Count; i++) target.Symbols[i].Enabled = false;
            if (!hasSymbol) return;

            DecorativeSymbolItem symbol;
            if (target.Symbols.Count > 0)
            {
                symbol = target.Symbols[0];
            }
            else
            {
                // ランダム生成などで記号が 0 個のときも、プリセットの記号を反映する
                symbol = new DecorativeSymbolItem();
                target.Symbols.Add(symbol);
            }
            symbol.Type = preset.DefaultSymbol;
            symbol.Color = preset.SymbolColor;
            symbol.Size = preset.SymbolSize ?? DefaultSymbolSize;
            symbol.PositionX = preset.SymbolPositionX ?? DefaultSymbolPositionX;
            symbol.PositionY = preset.SymbolPositionY ?? DefaultSymbolPositionY;
            symbol.Count = preset.SymbolCount ?? 1;
            symbol.Opacity = preset.SymbolOpacity ?? DefaultSymbolOpacity;
            symbol.Rotation = 0.0;
            symbol.CornerRounding = 0.0;
            symbol.StretchTop = symbol.StretchBottom = symbol.StretchLeft = symbol.StretchRight = 1.0;
            symbol.Blend = LayerBlend.Normal;
            symbol.Enabled = true;
        }

        /// <summary>Puts copies of <paramref name="source"/> into <paramref name="target"/> (skipping nulls, at most the design's item limit).</summary>
        private static void ReplaceItems<T>(ObservableCollection<T> target, IEnumerable<T?> source, Func<T, T> copy) where T : class
        {
            var copies = source.Where(x => x != null).Take(IrisParameters.MaxNestedItems).Select(x => copy(x!)).ToList();
            target.Clear();
            foreach (var item in copies) target.Add(item);
        }

        // Where a preset's symbol goes when the preset does not say: upper right of the pupil
        private const double DefaultSymbolSize = 0.18;
        private const double DefaultSymbolPositionX = 0.25;
        private const double DefaultSymbolPositionY = -0.15;
        private const double DefaultSymbolOpacity = 0.85;

        /// <summary>
        /// Makes a preset from a design: everything about its look (colors, pupil, rays, glitter, layers, limbal ring,
        /// highlights, symbols, lens pattern). Applying it brings the look back. Size, shape, tilt, seed, name and the
        /// odd-eye settings are not part of a preset. Color fills (gradients) are kept as they are.
        /// </summary>
        public static PresetDefinition FromDesign(IrisParameters design, string name, string description)
        {
            var preset = new PresetDefinition
            {
                Name = name,
                Description = description,
                BaseColor = design.BaseColor,
                InnerColor = design.InnerColor,
                OuterColor = design.OuterColor,
                BottomHighlightColor = design.BottomHighlightColor,
                LimbalRingColor = design.LimbalRingColor,
                PupilColor = design.PupilColor,
                RayColor = design.RayColor,
                ParticleColor = design.ParticleColor,
                PupilShape = design.PupilShape,
                InnerGradientRadius = design.InnerGradientRadius,
                OuterGradientRadius = design.OuterGradientRadius,

                AuroraColor = design.AuroraColor,
                AuroraOpacity = design.AuroraOpacity,
                NebulaColor = design.NebulaColor,
                NebulaOpacity = design.NebulaOpacity,
                PupilFacetIntensity = design.PupilFacetIntensity,

                NebulaDensity = design.NebulaDensity,
                NebulaSwirl = design.NebulaSwirl,
                PupilFacetCount = design.PupilFacetCount,
                PupilSizeRatio = design.PupilSizeRatio,
                BottomHighlightIntensity = design.BottomHighlightIntensity,
                RayCount = design.RayCount,
                RayIntensity = design.RayIntensity,
                ParticleCount = design.ParticleCount,
                ParticleRadiusRatio = design.ParticleRadiusRatio,
                ParticleGlow = design.ParticleGlow,
                ParticleFlareRatio = design.ParticleFlareRatio,

                LimbalRingThickness = design.LimbalRingThickness,
                GlitterBlend = design.GlitterBlend,
                AuroraBlend = design.AuroraBlend,
                FiberNoiseScale = design.FiberNoiseScale,
                FiberNoise = design.FiberNoise,
                LightStrength = design.LightStrength,
                LightAngle = design.LightAngle,
                LimbalRingOpacity = design.LimbalRingOpacity,
                LimbalRingSoftness = design.LimbalRingSoftness,
                PupilOpacity = design.PupilOpacity,
                PupilSoftness = design.PupilSoftness,
                PupilCornerRounding = design.PupilCornerRounding,
                PupilStretchTop = design.PupilStretchTop,
                PupilStretchBottom = design.PupilStretchBottom,
                PupilStretchLeft = design.PupilStretchLeft,
                PupilStretchRight = design.PupilStretchRight,
                PupilStarCoreScale = design.PupilStarCoreScale,
                NebulaScale = design.NebulaScale,
                RayThickness = design.RayThickness,
                RayLengthRatio = design.RayLengthRatio,
                RayRandomness = design.RayRandomness,
                RayInnerRadiusRatio = design.RayInnerRadiusRatio,
                RayTaper = design.RayTaper,
                RayWidthVariation = design.RayWidthVariation,
                RayCurve = design.RayCurve,
                RayFineLayer = design.RayFineLayer,
                ParticleMinSize = design.ParticleMinSize,
                ParticleMaxSize = design.ParticleMaxSize,
                ParticleOpacity = design.ParticleOpacity,
                ParticleFlareLength = design.ParticleFlareLength,
                RayRangeAngle = design.RayRangeAngle,
                RayRangeWidth = design.RayRangeWidth,
                RayRangeFeather = design.RayRangeFeather,
                ParticleRangeAngle = design.ParticleRangeAngle,
                ParticleRangeWidth = design.ParticleRangeWidth,
                ParticleRangeFeather = design.ParticleRangeFeather,
                LensRangeAngle = design.LensRangeAngle,
                LensRangeWidth = design.LensRangeWidth,
                LensRangeFeather = design.LensRangeFeather,
                IrisFacetIntensity = design.IrisFacetIntensity,
                IrisFacetTableSize = design.IrisFacetTableSize,
                IrisFacetShadeAngle = design.IrisFacetShadeAngle,
                IrisFacetShadeBias = design.IrisFacetShadeBias,
                IrisFacetRangeAngle = design.IrisFacetRangeAngle,
                IrisFacetRangeWidth = design.IrisFacetRangeWidth,
                IrisFacetRangeFeather = design.IrisFacetRangeFeather,
                IrisFacetCount = design.IrisFacetCount,
                IrisFacetBlend = design.IrisFacetBlend,

                Highlights = design.Highlights.Select(h => h.Clone()).ToList(),
                PupilRightEyeFlip = design.PupilRightEyeFlip,
                LightRightEyeFlip = design.LightRightEyeFlip,

                LensPatternEnabled = design.LensPatternEnabled,
                LensRingCount = design.LensRingCount,
                LensRingIntensity = design.LensRingIntensity,
                LensRingWidth = design.LensRingWidth,
                LensRingColor = design.LensRingColor,
                LensRefractionGlow = design.LensRefractionGlow,

                Symbols = design.Symbols.Select(s => s.Clone()).ToList()
            };
            return preset;
        }

        public static void ExportPalettesToFile(string filePath, IEnumerable<PresetDefinition> presets)
        {
            var opt = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(presets, opt);
            File.WriteAllText(filePath, json);
        }

        public static List<PresetDefinition>? ImportPalettesFromFile(string filePath)
        {
            string json = SafeFileReader.ReadAllText(filePath);
            return JsonSerializer.Deserialize<List<PresetDefinition>>(json);
        }
    }
}
