using System;
using System.Collections.Generic;

namespace DollIrisGenerator.Models
{
    public enum PdfTemplateType
    {
        Custom,             // 自由指定 (自動計算/手動指定)
        Scale1_3_Doll,      // 1/3 ドール向け (SD, DD, SmartDoll 20-24mm)
        Scale1_4_Doll,      // 1/4 ドール向け (MDD, Kumako 16-18mm)
        Scale1_6_Doll,      // 1/6 ドール向け (オビツ, ピュアニーモ 10-14mm)
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
        public double PairSpacingXMm { get; set; } = 4.0;
        public double ColumnSpacingMm { get; set; } = 10.0;
        public double RowSpacingMm { get; set; } = 12.0;
        public bool AutoGrid { get; set; } = true;
        public int Columns { get; set; } = 4;
        public int Rows { get; set; } = 6;
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
                Name = "1/3 ドール向けシート (SD / DD / 20〜24mm)",
                Description = "20mm〜24mmアイに最適化。レジン盛りやカボション貼りの余白を広めに確保 (3列×4行)",
                MarginTopMm = 18.0, MarginBottomMm = 18.0, MarginLeftMm = 18.0, MarginRightMm = 18.0,
                PairSpacingXMm = 6.0, ColumnSpacingMm = 16.0, RowSpacingMm = 18.0,
                AutoGrid = false, Columns = 3, Rows = 4
            },
            new PdfTemplatePreset
            {
                Type = PdfTemplateType.Scale1_4_Doll,
                Name = "1/4 ドール向けシート (MDD / 16〜18mm)",
                Description = "16mm〜18mmアイに最適化された高密度配置 (4列×6行)",
                MarginTopMm = 15.0, MarginBottomMm = 15.0, MarginLeftMm = 15.0, MarginRightMm = 15.0,
                PairSpacingXMm = 4.0, ColumnSpacingMm = 10.0, RowSpacingMm = 12.0,
                AutoGrid = false, Columns = 4, Rows = 6
            },
            new PdfTemplatePreset
            {
                Type = PdfTemplateType.Scale1_6_Doll,
                Name = "1/6 ドール向けシート (オビツ / 10〜14mm)",
                Description = "10mm〜14mm小型アイ用。多数のアイを一括出力可能 (5列×8行)",
                MarginTopMm = 12.0, MarginBottomMm = 12.0, MarginLeftMm = 12.0, MarginRightMm = 12.0,
                PairSpacingXMm = 3.0, ColumnSpacingMm = 8.0, RowSpacingMm = 8.0,
                AutoGrid = false, Columns = 5, Rows = 8
            },
            new PdfTemplatePreset
            {
                Type = PdfTemplateType.CirclePunchGrid,
                Name = "クラフトパンチ専用グリッド (穴あけ器用)",
                Description = "市販の円形パンチ（18mm/20mm等）の挿入口に合わせて周囲余白を等間隔に確保",
                MarginTopMm = 20.0, MarginBottomMm = 20.0, MarginLeftMm = 20.0, MarginRightMm = 20.0,
                PairSpacingXMm = 10.0, ColumnSpacingMm = 18.0, RowSpacingMm = 20.0,
                AutoGrid = false, Columns = 3, Rows = 5,
                ShowCutLines = true, ShowCenterMarks = true, ShowTrimGuides = true
            },
            new PdfTemplatePreset
            {
                Type = PdfTemplateType.ColorTrialSheet,
                Name = "カラー見本・試作用シート",
                Description = "光沢紙等でのプリンター発色確認用。テスト用トンボと余白を広めにとった配置",
                MarginTopMm = 20.0, MarginBottomMm = 20.0, MarginLeftMm = 20.0, MarginRightMm = 20.0,
                PairSpacingXMm = 5.0, ColumnSpacingMm = 12.0, RowSpacingMm = 14.0,
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
