using System;
using System.Collections.Generic;

namespace DollIrisGenerator.Models
{
    public enum PdfTemplateType
    {
        Custom,             // 自由指定 (自動計算/手動指定)
        Scale1_3_Doll,      // 1/3 ドール向け (SD, DD: アイ 20-24mm = 虹彩 10-12mm)
        Scale1_4_Doll,      // 1/4 ドール向け (MDD, Kumako: アイ 16-20mm = 虹彩 8-10mm)
        Scale1_6_Doll,      // 1/6 ドール向け (オビツ, ピュアニーモ: アイ 10-14mm = 虹彩 5-7mm)
        CirclePunchGrid,    // 円形クラフトパンチ専用グリッド (等間隔余裕配置)
        ColorTrialSheet     // 色味確認・試作用グラデーションテストシート
    }

    public class PdfTemplatePreset
    {
        public PdfTemplateType Type { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public double MarginTopMm { get; set; } = 15.0;
        public double MarginBottomMm { get; set; } = 15.0;
        public double MarginLeftMm { get; set; } = 15.0;
        public double MarginRightMm { get; set; } = 15.0;
        public double PairSpacingXMm { get; set; } = 3.0;
        public double ColumnSpacingMm { get; set; } = 6.0;
        public double RowSpacingMm { get; set; } = 8.0;
        public bool AutoGrid { get; set; } = true;
        public int Columns { get; set; } = 6;
        public int Rows { get; set; } = 11;
        public bool ShowCutLines { get; set; } = true;
        public bool ShowCenterMarks { get; set; } = true;
        public bool ShowTrimGuides { get; set; } = true;
        public bool ShowLabels { get; set; } = true;

        public static List<PdfTemplatePreset> All { get; } = new()
        {
            new PdfTemplatePreset
            {
                Type = PdfTemplateType.Custom,
                Name = "標準レイアウト (自動面付け)",
                Description = "サイズと余白から最適な列数・行数を自動計算して配置します",
                AutoGrid = true
            },
            new PdfTemplatePreset
            {
                Type = PdfTemplateType.Scale1_3_Doll,
                Name = "1/3 ドール向けシート (SD / DD / 虹彩 10〜12mm)",
                Description = "虹彩 10〜12mm（アイ 20〜24mm）向け。レジン盛りやカボション貼りの余白を広めに確保 (4列×8行)",
                MarginTopMm = 18.0, MarginBottomMm = 18.0, MarginLeftMm = 18.0, MarginRightMm = 18.0,
                PairSpacingXMm = 5.0, ColumnSpacingMm = 12.0, RowSpacingMm = 14.0,
                AutoGrid = false, Columns = 4, Rows = 8
            },
            new PdfTemplatePreset
            {
                Type = PdfTemplateType.Scale1_4_Doll,
                Name = "1/4 ドール向けシート (MDD / 虹彩 8〜10mm)",
                Description = "虹彩 8〜10mm（アイ 16〜20mm）向けの高密度配置 (5列×10行)",
                MarginTopMm = 15.0, MarginBottomMm = 15.0, MarginLeftMm = 15.0, MarginRightMm = 15.0,
                PairSpacingXMm = 4.0, ColumnSpacingMm = 8.0, RowSpacingMm = 10.0,
                AutoGrid = false, Columns = 5, Rows = 10
            },
            new PdfTemplatePreset
            {
                Type = PdfTemplateType.Scale1_6_Doll,
                Name = "1/6 ドール向けシート (オビツ / 虹彩 5〜7mm)",
                Description = "虹彩 5〜7mm（アイ 10〜14mm）の小型アイ用。多数のアイを一括出力可能 (8列×13行)",
                MarginTopMm = 12.0, MarginBottomMm = 12.0, MarginLeftMm = 12.0, MarginRightMm = 12.0,
                PairSpacingXMm = 3.0, ColumnSpacingMm = 6.0, RowSpacingMm = 7.0,
                AutoGrid = false, Columns = 8, Rows = 13
            },
            new PdfTemplatePreset
            {
                Type = PdfTemplateType.CirclePunchGrid,
                Name = "クラフトパンチ専用グリッド (穴あけ器用)",
                Description = "市販の小型円形パンチ（虹彩 12mm まで）の挿入口に合わせて周囲余白を等間隔に確保 (4列×7行)",
                MarginTopMm = 20.0, MarginBottomMm = 20.0, MarginLeftMm = 20.0, MarginRightMm = 20.0,
                PairSpacingXMm = 8.0, ColumnSpacingMm = 12.0, RowSpacingMm = 16.0,
                AutoGrid = false, Columns = 4, Rows = 7,
                ShowCutLines = true, ShowCenterMarks = true, ShowTrimGuides = true
            },
            new PdfTemplatePreset
            {
                Type = PdfTemplateType.ColorTrialSheet,
                Name = "カラー見本・試作用シート",
                Description = "光沢紙等でのプリンター発色確認用。テスト用トンボと余白を広めにとった配置",
                MarginTopMm = 20.0, MarginBottomMm = 20.0, MarginLeftMm = 20.0, MarginRightMm = 20.0,
                PairSpacingXMm = 5.0, ColumnSpacingMm = 8.0, RowSpacingMm = 10.0,
                AutoGrid = true,
                ShowCutLines = true, ShowCenterMarks = true, ShowTrimGuides = true, ShowLabels = true
            }
        };

        public void ApplyTo(PrintSheetConfig target)
        {
            target.PageMarginTopMm = this.MarginTopMm;
            target.PageMarginBottomMm = this.MarginBottomMm;
            target.PageMarginLeftMm = this.MarginLeftMm;
            target.PageMarginRightMm = this.MarginRightMm;
            target.PairSpacingXMm = this.PairSpacingXMm;
            target.ColumnSpacingMm = this.ColumnSpacingMm;
            target.RowSpacingMm = this.RowSpacingMm;
            target.AutoCalculateGrid = this.AutoGrid;
            target.ManualColumns = this.Columns;
            target.ManualRows = this.Rows;
            target.ShowCutLines = this.ShowCutLines;
            target.ShowCenterMarks = this.ShowCenterMarks;
            target.ShowTrimGuides = this.ShowTrimGuides;
            target.ShowLabels = this.ShowLabels;
        }
    }
}
