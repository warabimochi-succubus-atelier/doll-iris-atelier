using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DollIrisGenerator.Models
{
    public class PrintSheetConfig : INotifyPropertyChanged
    {
        private double _pageMarginLeftMm = 15.0;
        private double _pageMarginTopMm = 15.0;
        private double _pageMarginRightMm = 15.0;
        private double _pageMarginBottomMm = 15.0;
        private double _pairSpacingXMm = 4.0;   // gap between left & right eye in a pair
        private double _columnSpacingMm = 10.0; // gap between different pairs horizontally
        private double _rowSpacingMm = 12.0;    // gap between rows vertically

        private bool _showCutLines = true;      // カットライン (裁断補助線)
        private string _cutLineColor = "#A0A0A0";
        private double _cutLineThicknessMm = 0.15;

        private bool _showCenterMarks = true;   // センターマーク (レジン盛り・カボション芯出し用十字)
        private string _centerMarkColor = "#707070";
        private double _centerMarkLengthMm = 3.0;

        private bool _showTrimGuides = true;    // トリムガイド (トンボ)
        private bool _showLabels = true;        // パターン名・サイズ表記 (例: "20.0mm #1")

        private bool _autoCalculateGrid = true;
        private int _manualColumns = 4;
        private int _manualRows = 6;

        public double PageMarginLeftMm
        {
            get => _pageMarginLeftMm;
            set { _pageMarginLeftMm = Math.Max(5.0, value); OnPropertyChanged(); }
        }

        public double PageMarginTopMm
        {
            get => _pageMarginTopMm;
            set { _pageMarginTopMm = Math.Max(5.0, value); OnPropertyChanged(); }
        }

        public double PageMarginRightMm
        {
            get => _pageMarginRightMm;
            set { _pageMarginRightMm = Math.Max(5.0, value); OnPropertyChanged(); }
        }

        public double PageMarginBottomMm
        {
            get => _pageMarginBottomMm;
            set { _pageMarginBottomMm = Math.Max(5.0, value); OnPropertyChanged(); }
        }

        public double PairSpacingXMm
        {
            get => _pairSpacingXMm;
            set { _pairSpacingXMm = Math.Max(1.0, value); OnPropertyChanged(); }
        }

        public double ColumnSpacingMm
        {
            get => _columnSpacingMm;
            set { _columnSpacingMm = Math.Max(2.0, value); OnPropertyChanged(); }
        }

        public double RowSpacingMm
        {
            get => _rowSpacingMm;
            set { _rowSpacingMm = Math.Max(4.0, value); OnPropertyChanged(); }
        }

        public bool ShowCutLines
        {
            get => _showCutLines;
            set { _showCutLines = value; OnPropertyChanged(); }
        }

        public string CutLineColor
        {
            get => _cutLineColor;
            set { _cutLineColor = value; OnPropertyChanged(); }
        }

        public double CutLineThicknessMm
        {
            get => _cutLineThicknessMm;
            set { _cutLineThicknessMm = Math.Max(0.05, value); OnPropertyChanged(); }
        }

        public bool ShowCenterMarks
        {
            get => _showCenterMarks;
            set { _showCenterMarks = value; OnPropertyChanged(); }
        }

        public string CenterMarkColor
        {
            get => _centerMarkColor;
            set { _centerMarkColor = value; OnPropertyChanged(); }
        }

        public double CenterMarkLengthMm
        {
            get => _centerMarkLengthMm;
            set { _centerMarkLengthMm = Math.Max(1.0, value); OnPropertyChanged(); }
        }

        public bool ShowTrimGuides
        {
            get => _showTrimGuides;
            set { _showTrimGuides = value; OnPropertyChanged(); }
        }

        // 1 cm reference mark at the bottom of each page, to check the print was not scaled
        private bool _showScaleMark = true;
        public bool ShowScaleMark
        {
            get => _showScaleMark;
            set { _showScaleMark = value; OnPropertyChanged(); }
        }

        public bool ShowLabels
        {
            get => _showLabels;
            set { _showLabels = value; OnPropertyChanged(); }
        }

        public bool AutoCalculateGrid
        {
            get => _autoCalculateGrid;
            set { _autoCalculateGrid = value; OnPropertyChanged(); }
        }

        public int ManualColumns
        {
            get => _manualColumns;
            set { _manualColumns = Math.Max(1, Math.Min(12, value)); OnPropertyChanged(); }
        }

        public int ManualRows
        {
            get => _manualRows;
            set { _manualRows = Math.Max(1, Math.Min(20, value)); OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }

    public class SheetItem : INotifyPropertyChanged
    {
        private IrisParameters _parameters;
        private int _pairCount = 1;

        public SheetItem(IrisParameters parameters, int pairCount = 1)
        {
            _parameters = parameters;
            _pairCount = pairCount;
        }

        public IrisParameters Parameters
        {
            get => _parameters;
            set { _parameters = value; OnPropertyChanged(); }
        }

        /// <summary>Upper limit of the quantity box; "fill sheet" never raises an existing row above it either.</summary>
        public const int MaxPairCount = 100;

        public int PairCount
        {
            get => _pairCount;
            set { _pairCount = Math.Max(1, Math.Min(MaxPairCount, value)); OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
