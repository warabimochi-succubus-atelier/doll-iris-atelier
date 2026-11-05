using System;
using System.Collections.Generic;

namespace DollIrisGenerator.Models
{
    public class OddEyeTemplate
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        
        // Left Eye Colors
        public string LeftBaseColor { get; set; } = "#2979FF";
        public string LeftInnerColor { get; set; } = "#80D8FF";
        public string LeftOuterColor { get; set; } = "#0D47A1";
        public string LeftRayColor { get; set; } = "#B3E5FC";

        // Right Eye Colors
        public string RightBaseColor { get; set; } = "#FFB300";
        public string RightInnerColor { get; set; } = "#FFE082";
        public string RightOuterColor { get; set; } = "#E65100";
        public string RightRayColor { get; set; } = "#FFF9C4";

        public static List<OddEyeTemplate> Presets { get; } = new()
        {
            new OddEyeTemplate
            {
                Name = "サファイア ＆ ゴールド (蒼金)",
                Description = "王道の神秘的な青と金のオッドアイ",
                LeftBaseColor = "#2979FF", LeftInnerColor = "#80D8FF", LeftOuterColor = "#0D47A1", LeftRayColor = "#B3E5FC",
                RightBaseColor = "#FFB300", RightInnerColor = "#FFE082", RightOuterColor = "#E65100", RightRayColor = "#FFF9C4"
            },
            new OddEyeTemplate
            {
                Name = "ルビー ＆ サファイア (紅蒼)",
                Description = "対比が美しい真紅と青のオッドアイ",
                LeftBaseColor = "#D50000", LeftInnerColor = "#FF8A80", LeftOuterColor = "#5A0000", LeftRayColor = "#FFCDD2",
                RightBaseColor = "#2979FF", RightInnerColor = "#80D8FF", RightOuterColor = "#0D47A1", RightRayColor = "#B3E5FC"
            },
            new OddEyeTemplate
            {
                Name = "アメジスト ＆ エメラルド (紫翠)",
                Description = "ファンタジー感あふれる高貴な紫と深緑",
                LeftBaseColor = "#AA00FF", LeftInnerColor = "#EA80FC", LeftOuterColor = "#4A148C", LeftRayColor = "#E1BEE7",
                RightBaseColor = "#00C853", RightInnerColor = "#B9F6CA", RightOuterColor = "#1B5E20", RightRayColor = "#CCFF90"
            },
            new OddEyeTemplate
            {
                Name = "ローズピンク ＆ シルバー (桜銀)",
                Description = "可憐で儚げな桜色と白銀のオッドアイ",
                LeftBaseColor = "#FF4081", LeftInnerColor = "#FF80AB", LeftOuterColor = "#880E4F", LeftRayColor = "#FCE4EC",
                RightBaseColor = "#90A4AE", RightInnerColor = "#ECEFF1", RightOuterColor = "#37474F", RightRayColor = "#CFD8DC"
            },
            new OddEyeTemplate
            {
                Name = "アンバー ＆ キャットグリーン (猫目双色)",
                Description = "野性味と気品を兼ね備えた琥珀色と若草色の瞳",
                LeftBaseColor = "#FF9100", LeftInnerColor = "#FFD180", LeftOuterColor = "#BF360C", LeftRayColor = "#FFE082",
                RightBaseColor = "#7CB342", RightInnerColor = "#C5E1A5", RightOuterColor = "#33691E", RightRayColor = "#DCEDC8"
            }
        };
    }
}
