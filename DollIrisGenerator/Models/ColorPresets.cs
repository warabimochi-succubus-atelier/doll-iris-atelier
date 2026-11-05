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
            }
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

            if (preset.DefaultSymbol != SymbolType.None)
            {
                if (target.Symbols.Count > 0)
                {
                    target.Symbols[0].Type = preset.DefaultSymbol;
                    target.Symbols[0].Color = preset.SymbolColor;
                    target.Symbols[0].Enabled = true;
                }
                else
                {
                    // ランダム生成などで記号が 0 個のときも、プリセットの記号を反映する
                    target.Symbols.Add(new DecorativeSymbolItem
                    {
                        Type = preset.DefaultSymbol,
                        Count = 1,
                        Size = 0.18,
                        PositionX = 0.25,
                        PositionY = -0.15,
                        Color = preset.SymbolColor,
                        Opacity = 0.85,
                        Enabled = true
                    });
                }
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
