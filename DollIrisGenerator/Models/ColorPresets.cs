using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using DollIrisGenerator.Services;

namespace DollIrisGenerator.Models
{
    public class PresetDefinition
    {
        public string Name { get; set; } = "";
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

        // Layers: always applied (0 = off), so picking a plain preset clears another preset's aurora / nebula / gem cut.
        // Palettes saved before these existed load as 0.
        public string? AuroraColor { get; set; }
        public double AuroraOpacity { get; set; }
        public string? NebulaColor { get; set; }
        public double NebulaOpacity { get; set; }
        public double PupilFacetIntensity { get; set; }

        // Look settings: applied only when the preset sets them (null = keep the design's own value)
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

        /// <summary>
        /// The preset's symbol replaces the design's symbols: every other symbol is switched off (all of them when the
        /// preset has none), so a symbol from another preset, e.g. the outline heart of 花びら, does not stay behind.
        /// </summary>
        public bool ReplaceSymbols { get; set; }
    }

    public static class ColorPresets
    {
        public static ObservableCollection<PresetDefinition> All { get; } = new()
        {
            new PresetDefinition
            {
                Name = "ブルー (サファイア)",
                Description = "透明感のある澄んだアニメ調ブルーアイ",
                BaseColor = "#2979FF",
                InnerColor = "#80D8FF",
                OuterColor = "#0D47A1",
                BottomHighlightColor = "#E1F5FE",
                LimbalRingColor = "#051636",
                PupilColor = "#060A18",
                RayColor = "#B3E5FC",
                ParticleColor = "#FFFFFF",
                DefaultSymbol = SymbolType.None
            },
            new PresetDefinition
            {
                Name = "グリーン (エメラルド)",
                Description = "深みのある鮮やかなエメラルドグリーン",
                BaseColor = "#00C853",
                InnerColor = "#B9F6CA",
                OuterColor = "#1B5E20",
                BottomHighlightColor = "#E8F5E9",
                LimbalRingColor = "#092E10",
                PupilColor = "#051A09",
                RayColor = "#CCFF90",
                ParticleColor = "#FFFFFF",
                DefaultSymbol = SymbolType.None
            },
            new PresetDefinition
            {
                Name = "レッド (ルビー)",
                Description = "情熱的で高貴な深紅のルビーアイ",
                BaseColor = "#D50000",
                InnerColor = "#FF8A80",
                OuterColor = "#5A0000",
                BottomHighlightColor = "#FFEBEE",
                LimbalRingColor = "#2B0000",
                PupilColor = "#140000",
                RayColor = "#FFCDD2",
                ParticleColor = "#FFE0B2",
                DefaultSymbol = SymbolType.None
            },
            new PresetDefinition
            {
                Name = "パープル (アメジスト)",
                Description = "神秘的で幻想的なヴァイオレットパープル",
                BaseColor = "#AA00FF",
                InnerColor = "#EA80FC",
                OuterColor = "#4A148C",
                BottomHighlightColor = "#F3E5F5",
                LimbalRingColor = "#1E053D",
                PupilColor = "#120224",
                RayColor = "#E1BEE7",
                ParticleColor = "#FFFFFF",
                DefaultSymbol = SymbolType.None
            },
            new PresetDefinition
            {
                Name = "ゴールド (琥珀・アンバー)",
                Description = "温かみのある輝く黄金・琥珀色",
                BaseColor = "#FFB300",
                InnerColor = "#FFE082",
                OuterColor = "#E65100",
                BottomHighlightColor = "#FFF8E1",
                LimbalRingColor = "#3E2723",
                PupilColor = "#1F1411",
                RayColor = "#FFF9C4",
                ParticleColor = "#FFFDE7",
                DefaultSymbol = SymbolType.None
            },
            new PresetDefinition
            {
                Name = "シルバー (クールグレー)",
                Description = "知的で澄明な白銀・モノトーンアイ",
                BaseColor = "#90A4AE",
                InnerColor = "#ECEFF1",
                OuterColor = "#37474F",
                BottomHighlightColor = "#FFFFFF",
                LimbalRingColor = "#1B2327",
                PupilColor = "#0F1417",
                RayColor = "#CFD8DC",
                ParticleColor = "#FFFFFF",
                DefaultSymbol = SymbolType.None
            },
            new PresetDefinition
            {
                Name = "ピンク (ローズクォーツ)",
                Description = "愛らしく華やかな桜ピンクアイ",
                BaseColor = "#FF4081",
                InnerColor = "#FF80AB",
                OuterColor = "#880E4F",
                BottomHighlightColor = "#FCE4EC",
                LimbalRingColor = "#3F0523",
                PupilColor = "#200211",
                RayColor = "#F8BBD0",
                ParticleColor = "#FFFFFF",
                DefaultSymbol = SymbolType.Heart,
                SymbolColor = "#FFFFFF"
            },
            new PresetDefinition
            {
                Name = "サクラ (桜舞う春風)",
                Description = "桜の花びら装飾が舞う春色のソフトピンクアイ",
                BaseColor = "#FF6090",
                InnerColor = "#FFB2D0",
                OuterColor = "#7A0035",
                BottomHighlightColor = "#FFF0F5",
                LimbalRingColor = "#3B0018",
                PupilColor = "#1F000C",
                RayColor = "#FFD6E7",
                ParticleColor = "#FFFFFF",
                DefaultSymbol = SymbolType.SakuraPetal,
                SymbolColor = "#FFF5F8"
            },
            new PresetDefinition
            {
                Name = "サンセット (夕暮れグラデ)",
                Description = "上部パープルと下部オレンジの夕暮れグラデーション",
                BaseColor = "#7C4DFF",
                InnerColor = "#FF9100",
                OuterColor = "#311B92",
                BottomHighlightColor = "#FFD180",
                LimbalRingColor = "#1A0933",
                PupilColor = "#100620",
                RayColor = "#FFE082",
                ParticleColor = "#FFF9C4",
                DefaultSymbol = SymbolType.Star4,
                SymbolColor = "#FFF9C4"
            },
            new PresetDefinition
            {
                Name = "ギャラクシー (夜空・星雲)",
                Description = "星屑が舞う宇宙・銀河のダークネイビー",
                BaseColor = "#304FFE",
                InnerColor = "#00E5FF",
                OuterColor = "#0D1B2A",
                BottomHighlightColor = "#84FFFF",
                LimbalRingColor = "#050B14",
                PupilColor = "#02060D",
                RayColor = "#80D8FF",
                ParticleColor = "#FFFFFF",
                DefaultSymbol = SymbolType.Moon,
                SymbolColor = "#E0F7FA"
            },
            new PresetDefinition
            {
                Name = "キャットアイ (猫目スリット)",
                Description = "縦長瞳孔を持つ魅惑的なゴールドキャットアイ",
                BaseColor = "#FFD600",
                InnerColor = "#AEEA00",
                OuterColor = "#BF360C",
                BottomHighlightColor = "#FFFF8D",
                LimbalRingColor = "#260E04",
                PupilColor = "#0D0502",
                RayColor = "#F4FF81",
                ParticleColor = "#FFFFFF",
                PupilShape = PupilShape.CatSlit,
                DefaultSymbol = SymbolType.None
            },

            // ---------------- ベーシックカラー ----------------
            Basic("クリアブルー", "澄んだ水色のガラスのようなクリアブルー",
                "#2E86FF", "#9BE1FF", "#0A2A8A", "#CFF3FF", "#06173F", "#071030", "#C9F0FF"),
            Basic("アクア", "南国の海のような明るいアクアブルー",
                "#12B5C9", "#9FF3F0", "#04506B", "#D6FFFB", "#02303C", "#03202A", "#C8FFF7"),
            Basic("ラベンダー", "やさしく透けるラベンダーパープル",
                "#8E63E0", "#D9C6FF", "#3B1C8C", "#EEE4FF", "#1E0E4A", "#140A33", "#E6DAFF"),
            Basic("ピンク", "甘く華やかなローズピンク",
                "#F06292", "#FFC1D9", "#8E1F4F", "#FFE6F0", "#4A0A27", "#3A0A20", "#FFD6E6"),
            Basic("レッド", "宝石のように深く輝くレッド",
                "#E0283C", "#FF8A8A", "#6E0612", "#FFD0D0", "#3A0309", "#2A0207", "#FFC2C2"),
            Basic("アンバー", "蜂蜜のようにとろける琥珀色",
                "#E08A1E", "#FFD27A", "#7A3A06", "#FFE9B8", "#3A1A03", "#2A1202", "#FFE3A8"),
            Basic("グリーン", "透明感のあるエメラルドグリーン",
                "#19A36B", "#8FF0C0", "#064D33", "#D5FFEA", "#022B1C", "#021F14", "#C6FFE3"),
            Basic("グレー", "銀色にきらめくクールグレー",
                "#7D8792", "#D3DAE2", "#2E343B", "#EEF2F6", "#15181C", "#0D0F12", "#E6EBF0"),

            // ---------------- 特殊デザイン ----------------
            new PresetDefinition
            {
                ReplaceSymbols = true,
                Name = "特殊: 星屑",
                Description = "濃紺の虹彩いっぱいに光る星屑が散らばる",
                BaseColor = "#1C2C8C", InnerColor = "#4E7CFF", OuterColor = "#070D33", BottomHighlightColor = "#8FB4FF",
                LimbalRingColor = "#03061A", PupilColor = "#02040F", RayColor = "#9FB8FF", ParticleColor = "#FFFFFF",
                RayCount = 48, RayIntensity = 0.35, ParticleCount = 230, ParticleRadiusRatio = 0.92,
                ParticleGlow = 0.7, ParticleFlareRatio = 0.15, BottomHighlightIntensity = 0.45
            },
            new PresetDefinition
            {
                ReplaceSymbols = true,
                Name = "特殊: オーロラ",
                Description = "ピンク・ブルー・パープルが瞳孔のまわりで移り変わる",
                BaseColor = "#6F7CE0", InnerColor = "#D7C8FF", OuterColor = "#2A2370", BottomHighlightColor = "#E3F6FF",
                LimbalRingColor = "#140F3A", PupilColor = "#0B0826", ParticleColor = "#FFFFFF",
                RayColor = "conic-gradient(from 270deg, #FFFFFF 0%, #FF80AB 33%, #80D8FF 66%, #FFFFFF 100%)",
                AuroraColor = IrisParameters.DefaultAuroraColor, AuroraOpacity = 0.55,
                RayCount = 64, RayIntensity = 0.65, ParticleCount = 90, ParticleGlow = 0.7, ParticleFlareRatio = 0.12
            },
            new PresetDefinition
            {
                ReplaceSymbols = true,
                Name = "特殊: 銀河",
                Description = "紫と青の星雲が渦巻く銀河",
                BaseColor = "#3D2A9A", InnerColor = "#7E57C2", OuterColor = "#12082E", BottomHighlightColor = "#B39DDB",
                LimbalRingColor = "#0A0420", PupilColor = "#05020F", RayColor = "#D1C4E9", ParticleColor = "#FFFFFF",
                NebulaColor = IrisParameters.DefaultNebulaColor, NebulaOpacity = 0.85, NebulaDensity = 0.55, NebulaSwirl = 0.5,
                RayCount = 48, RayIntensity = 0.3, ParticleCount = 170, ParticleRadiusRatio = 0.92,
                ParticleGlow = 0.8, ParticleFlareRatio = 0.12
            },
            new PresetDefinition
            {
                ReplaceSymbols = true,
                Name = "特殊: 夕焼け",
                Description = "オレンジから深紅へ沈む夕焼け色",
                BaseColor = "#F2683C", InnerColor = "#FFD08A", OuterColor = "#8A1E12", BottomHighlightColor = "#FFE1B0",
                LimbalRingColor = "#3D0A05", PupilColor = "radial-gradient(#C62828 0%, #4A0505 100%)", RayColor = "#FFE0B0",
                ParticleColor = "#FFF1D6", RayCount = 64, RayIntensity = 0.6, ParticleCount = 80, ParticleGlow = 0.6,
                BottomHighlightIntensity = 0.55
            },
            new PresetDefinition
            {
                ReplaceSymbols = true,
                Name = "特殊: 夜空",
                Description = "夜空の濃紺に小さなハートの瞳孔と星",
                BaseColor = "#142060", InnerColor = "#2E4BB0", OuterColor = "#050A24", BottomHighlightColor = "#5C7CFA",
                LimbalRingColor = "#02051A", PupilColor = "#060B2E", RayColor = "#90A4F8", ParticleColor = "#FFFFFF",
                PupilShape = PupilShape.Heart, PupilSizeRatio = 0.3, PupilFacetIntensity = 0.35, PupilFacetCount = 8,
                RayCount = 40, RayIntensity = 0.3, ParticleCount = 160, ParticleRadiusRatio = 0.9,
                ParticleGlow = 0.85, ParticleFlareRatio = 0.12,
                DefaultSymbol = SymbolType.Star4, SymbolColor = "#FFFFFF", SymbolSize = 0.12, SymbolPositionX = 0.35, SymbolPositionY = 0.25, SymbolCount = 1
            },
            new PresetDefinition
            {
                ReplaceSymbols = true,
                Name = "特殊: 宝石",
                Description = "ブルーの虹彩に、カットされた宝石のハート",
                BaseColor = "#2E8FC8", InnerColor = "#9FE6FF", OuterColor = "#0B3A6A", BottomHighlightColor = "#D8F6FF",
                LimbalRingColor = "#041A33", PupilColor = "radial-gradient(#B3F0FF 0%, #29B6F6 45%, #0B4F8A 100%)",
                RayColor = "#D6F5FF", ParticleColor = "#FFFFFF",
                PupilShape = PupilShape.Heart, PupilSizeRatio = 0.46, PupilFacetIntensity = 0.85, PupilFacetCount = 10,
                RayCount = 56, RayIntensity = 0.55, ParticleCount = 120, ParticleGlow = 0.8, ParticleFlareRatio = 0.15
            },
            new PresetDefinition
            {
                ReplaceSymbols = true,
                Name = "特殊: ハート",
                Description = "赤い虹彩の中心に、ルビーのようなカットのハート",
                BaseColor = "#E53950", InnerColor = "#FF8A9B", OuterColor = "#6A0418", BottomHighlightColor = "#FFD5DB",
                LimbalRingColor = "#3B0010", PupilColor = "radial-gradient(#FF1744 0%, #8B0016 100%)",
                RayColor = "#FFCDD2", ParticleColor = "#FFFFFF",
                PupilShape = PupilShape.Heart, PupilSizeRatio = 0.5, PupilFacetIntensity = 0.8, PupilFacetCount = 10,
                RayCount = 56, RayIntensity = 0.55, ParticleCount = 90, ParticleGlow = 0.7, ParticleFlareRatio = 0.12
            },
            new PresetDefinition
            {
                ReplaceSymbols = true,
                Name = "特殊: 花びら",
                Description = "淡いピンクに、ふちどりのハートが浮かぶ",
                BaseColor = "#F48FB1", InnerColor = "#FFE0EC", OuterColor = "#C2185B", BottomHighlightColor = "#FFF0F6",
                LimbalRingColor = "#6A0A33", PupilColor = "#C2185B", RayColor = "#FFFFFF", ParticleColor = "#FFFFFF",
                PupilShape = PupilShape.Heart, PupilSizeRatio = 0.46,
                RayCount = 64, RayIntensity = 0.5, ParticleCount = 110, ParticleGlow = 0.75, ParticleFlareRatio = 0.1,
                // A pale heart over the pupil heart leaves only its rim: an outlined heart
                DefaultSymbol = SymbolType.Heart, SymbolColor = "#FFC1D9", SymbolSize = 0.32,
                SymbolPositionX = 0.0, SymbolPositionY = 0.0, SymbolCount = 1, SymbolOpacity = 1.0
            },

            // ---------------- 雰囲気別おすすめ ----------------
            Mood("A. ナチュラル (ピンク系)", "やわらかく自然なピンク。ラメは控えめ",
                "#E58BA7", "#FBD3DF", "#9A4762", "#FFEFF4", "#4A1A2A", "#2E1019", "#FBE0E8", particles: 40, glow: 0.5),
            Mood("B. 精霊 (ブルー系)", "光をまとったような幻想的なブルー",
                "#3D7BEA", "#BDEBFF", "#13308A", "#E4F7FF", "#07164A", "#050E30", "#DDF4FF", particles: 150, glow: 0.7,
                aurora: "conic-gradient(from 270deg, #80D8FF 0%, #FFFFFF 33%, #82B1FF 66%, #80D8FF 100%)", auroraOpacity: 0.3),
            Mood("C. アンニュイ (パープル系)", "けだるく落ち着いたスモーキーパープル",
                "#7A5BA8", "#C9B5E6", "#2E1F4D", "#E6DCF2", "#170F29", "#0F0A1C", "#DCCFEE", particles: 40, glow: 0.45, rays: 0.4),
            Mood("D. つんでれ (レッド系)", "強気なのに甘さもある鮮やかなレッド",
                "#E8364F", "#FF9AA8", "#7A0A1E", "#FFE0E4", "#3B0510", "#24030A", "#FFD1D8", particles: 80, glow: 0.7,
                pupil: PupilShape.VerticalOval),
            Mood("E. 小悪魔 (赤紫系)", "ハートの瞳孔がのぞく、あやしい赤紫",
                "#C2185B", "#F06292", "#4A0628", "#FFD6E8", "#2A0216", "#1A010D", "#F8BBD0", particles: 90, glow: 0.8,
                pupil: PupilShape.Heart, facets: 0.6),
            Mood("F. 無邪気 (アクア系)", "くりっとした瞳孔の明るいアクア",
                "#22C3D6", "#B2F5FF", "#0A6B85", "#E0FDFF", "#033A48", "#022833", "#D2FBFF", particles: 80, glow: 0.7,
                pupilSize: 0.38)
        };

        /// <summary>ベーシックカラー: round pupil, fiber rays and glowing glitter in one hue.</summary>
        private static PresetDefinition Basic(string name, string description, string baseColor, string inner, string outer,
            string bottom, string limbal, string pupil, string ray) => new()
        {
            ReplaceSymbols = true,
            Name = "ベーシック: " + name,
            Description = description,
            BaseColor = baseColor, InnerColor = inner, OuterColor = outer, BottomHighlightColor = bottom,
            LimbalRingColor = limbal, PupilColor = pupil, RayColor = ray, ParticleColor = "#FFFFFF",
            PupilShape = PupilShape.Round, PupilSizeRatio = 0.32,
            RayCount = 64, RayIntensity = 0.65, ParticleCount = 75, ParticleRadiusRatio = 0.8,
            ParticleGlow = 0.7, ParticleFlareRatio = 0.15, BottomHighlightIntensity = 0.45
        };

        /// <summary>雰囲気別おすすめ: a basic look with its own glitter amount and an optional pupil / layer twist.</summary>
        private static PresetDefinition Mood(string name, string description, string baseColor, string inner, string outer,
            string bottom, string limbal, string pupilColor, string ray, int particles, double glow, double rays = 0.6,
            PupilShape pupil = PupilShape.Round, double pupilSize = 0.32, double facets = 0.0,
            string? aurora = null, double auroraOpacity = 0.0) => new()
        {
            ReplaceSymbols = true,
            Name = "雰囲気: " + name,
            Description = description,
            BaseColor = baseColor, InnerColor = inner, OuterColor = outer, BottomHighlightColor = bottom,
            LimbalRingColor = limbal, PupilColor = pupilColor, RayColor = ray, ParticleColor = "#FFFFFF",
            PupilShape = pupil, PupilSizeRatio = pupilSize, PupilFacetIntensity = facets, PupilFacetCount = 8,
            AuroraColor = aurora, AuroraOpacity = auroraOpacity,
            RayCount = 60, RayIntensity = rays, ParticleCount = particles, ParticleRadiusRatio = 0.8,
            ParticleGlow = glow, ParticleFlareRatio = 0.12
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

            // Layers
            if (preset.AuroraColor != null) target.AuroraColor = preset.AuroraColor;
            target.AuroraOpacity = preset.AuroraOpacity;
            if (preset.NebulaColor != null) target.NebulaColor = preset.NebulaColor;
            target.NebulaOpacity = preset.NebulaOpacity;
            target.PupilFacetIntensity = preset.PupilFacetIntensity;

            // Look settings the preset chose
            if (preset.NebulaDensity is { } nd) target.NebulaDensity = nd;
            if (preset.NebulaSwirl is { } ns) target.NebulaSwirl = ns;
            if (preset.PupilFacetCount is { } fc) target.PupilFacetCount = fc;
            if (preset.PupilSizeRatio is { } ps) target.PupilSizeRatio = ps;
            if (preset.BottomHighlightIntensity is { } bh) target.BottomHighlightIntensity = bh;
            if (preset.RayCount is { } rc) target.RayCount = rc;
            if (preset.RayIntensity is { } ri) target.RayIntensity = ri;
            if (preset.ParticleCount is { } pc) target.ParticleCount = pc;
            if (preset.ParticleRadiusRatio is { } pr) target.ParticleRadiusRatio = pr;
            if (preset.ParticleGlow is { } pg) target.ParticleGlow = pg;
            if (preset.ParticleFlareRatio is { } pf) target.ParticleFlareRatio = pf;

            if (preset.ReplaceSymbols)
            {
                for (int i = preset.DefaultSymbol != SymbolType.None ? 1 : 0; i < target.Symbols.Count; i++) target.Symbols[i].Enabled = false;
            }

            if (preset.DefaultSymbol != SymbolType.None)
            {
                DecorativeSymbolItem symbol;
                if (target.Symbols.Count > 0)
                {
                    symbol = target.Symbols[0];
                    symbol.Type = preset.DefaultSymbol;
                    symbol.Color = preset.SymbolColor;
                    symbol.Enabled = true;
                }
                else
                {
                    // ランダム生成などで記号が 0 個のときも、プリセットの記号を反映する
                    symbol = new DecorativeSymbolItem
                    {
                        Type = preset.DefaultSymbol,
                        Count = 1,
                        Size = 0.18,
                        PositionX = 0.25,
                        PositionY = -0.15,
                        Color = preset.SymbolColor,
                        Opacity = 0.85,
                        Enabled = true
                    };
                    target.Symbols.Add(symbol);
                }
                if (preset.SymbolSize is { } ss) symbol.Size = ss;
                if (preset.SymbolPositionX is { } sx) symbol.PositionX = sx;
                if (preset.SymbolPositionY is { } sy) symbol.PositionY = sy;
                if (preset.SymbolCount is { } sc) symbol.Count = sc;
                if (preset.SymbolOpacity is { } so) symbol.Opacity = so;
            }
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
