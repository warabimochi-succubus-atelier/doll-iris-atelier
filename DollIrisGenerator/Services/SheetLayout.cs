using System;
using System.Collections.Generic;
using System.Linq;
using DollIrisGenerator.Models;

namespace DollIrisGenerator.Services
{
    /// <summary>
    /// A4 imposition shared by the PDF exporter, the A4 sheet preview and "fill sheet",
    /// so the preview always matches the printed result.
    /// Cells are sized from the LARGEST design on the sheet, so mixed sizes never overlap.
    /// All coordinates are in mm from the TOP-LEFT of the page.
    /// </summary>
    public sealed class SheetLayout
    {
        public const double PageWidthMm = 210.0;
        public const double PageHeightMm = 297.0;

        public IReadOnlyList<IrisParameters> Pairs { get; }
        public PrintSheetConfig Config { get; }
        public double CellEyeWidthMm { get; }
        public double CellEyeHeightMm { get; }
        public double PairTotalWidthMm { get; }
        public double PairTotalHeightMm { get; }
        public int Columns { get; }
        public int Rows { get; }
        public int PairsPerPage => Columns * Rows;

        // Cells actually filled on the fullest page (the first one); empty grid cells don't print anything
        private int UsedColumns => Math.Min(Columns, Math.Min(Pairs.Count, PairsPerPage));
        private int UsedRows => (int)Math.Ceiling(Math.Min(Pairs.Count, PairsPerPage) / (double)Columns);

        /// <summary>Size of the area the pairs actually occupy on a page (filled columns / rows plus their spacing).</summary>
        public double GridWidthMm => UsedColumns * PairTotalWidthMm + (UsedColumns - 1) * Config.ColumnSpacingMm;
        public double GridHeightMm => UsedRows * PairTotalHeightMm + (UsedRows - 1) * Config.RowSpacingMm;

        public double UsableWidthMm => PageWidthMm - Config.PageMarginLeftMm - Config.PageMarginRightMm;
        public double UsableHeightMm => PageHeightMm - Config.PageMarginTopMm - Config.PageMarginBottomMm;

        /// <summary>
        /// False when the sheet cannot be printed as laid out: a design larger than the page (possible via a
        /// hand-edited or shared JSON file) or a manual column / row count that does not fit the margins.
        /// </summary>
        public bool FitsOnPage => GridWidthMm <= UsableWidthMm + 1e-6 && GridHeightMm <= UsableHeightMm + 1e-6;
        public int PageCount => Math.Max(1, (int)Math.Ceiling(Pairs.Count / (double)PairsPerPage));

        public SheetLayout(IEnumerable<SheetItem> items, PrintSheetConfig config)
            : this(ExpandPairs(items), config) { }

        public SheetLayout(IReadOnlyList<IrisParameters> pairs, PrintSheetConfig config)
        {
            Pairs = pairs.Count > 0 ? pairs : new List<IrisParameters> { new IrisParameters() };
            Config = config;

            // Cells hold the eye's real footprint, which is larger than its size when an oval is tilted
            CellEyeWidthMm = Pairs.Max(p => GetEyeFootprintMm(p).W);
            CellEyeHeightMm = Pairs.Max(p => GetEyeFootprintMm(p).H);
            PairTotalWidthMm = (CellEyeWidthMm * 2.0) + config.PairSpacingXMm;
            PairTotalHeightMm = CellEyeHeightMm + (config.ShowLabels ? 5.5 : 1.0);

            if (config.AutoCalculateGrid)
            {
                double usableW_Mm = PageWidthMm - config.PageMarginLeftMm - config.PageMarginRightMm;
                double usableH_Mm = PageHeightMm - config.PageMarginTopMm - config.PageMarginBottomMm;
                Columns = Math.Max(1, (int)Math.Floor((usableW_Mm + config.ColumnSpacingMm) / (PairTotalWidthMm + config.ColumnSpacingMm)));
                Rows = Math.Max(1, (int)Math.Floor((usableH_Mm + config.RowSpacingMm) / (PairTotalHeightMm + config.RowSpacingMm)));
            }
            else
            {
                Columns = Math.Max(1, config.ManualColumns);
                Rows = Math.Max(1, config.ManualRows);
            }
        }

        public static List<IrisParameters> ExpandPairs(IEnumerable<SheetItem> items)
        {
            var list = new List<IrisParameters>();
            foreach (var item in items)
            {
                for (int i = 0; i < item.PairCount; i++) list.Add(item.Parameters);
            }
            return list;
        }

        /// <summary>Width / height of the iris ellipse itself (before tilt). Used for cut-line radii.</summary>
        public static (double W, double H) GetEyeSizeMm(IrisParameters p)
        {
            double w = p.SizeMm;
            double h = p.SizeMm;
            if (p.Shape == IrisShape.VerticalOval) h *= p.AspectRatio;
            else if (p.Shape == IrisShape.HorizontalOval) w *= p.AspectRatio;
            return (w, h);
        }

        /// <summary>
        /// Axis-aligned bounding box of the eye as drawn, i.e. including the oval's tilt.
        /// Every image, canvas and sheet cell must be at least this big or a tilted oval gets clipped.
        /// (The right eye uses the mirrored tilt, which has the same bounding box.)
        /// </summary>
        public static (double W, double H) GetEyeFootprintMm(IrisParameters p)
        {
            var (w, h) = GetEyeSizeMm(p);
            if (Math.Abs(p.TiltAngle) < 0.05 || Math.Abs(w - h) < 1e-9) return (w, h);

            double a = w / 2.0, b = h / 2.0;
            double t = p.TiltAngle * Math.PI / 180.0;
            double cos = Math.Cos(t), sin = Math.Sin(t);
            double halfW = Math.Sqrt(a * a * cos * cos + b * b * sin * sin);
            double halfH = Math.Sqrt(a * a * sin * sin + b * b * cos * cos);
            return (halfW * 2.0, halfH * 2.0);
        }

        /// <summary>Top-left of the cell holding the pair at <paramref name="slotOnPage"/> (row-major).</summary>
        public (double X, double Y) GetCellOriginMm(int slotOnPage)
        {
            int r = slotOnPage / Columns;
            int c = slotOnPage % Columns;
            return (Config.PageMarginLeftMm + c * (PairTotalWidthMm + Config.ColumnSpacingMm),
                    Config.PageMarginTopMm + r * (PairTotalHeightMm + Config.RowSpacingMm));
        }

        public const double ScaleMarkLengthMm = 10.0;

        // Distance of the horizontal bar from the bottom paper edge: far enough to stay clear of
        // the non-printable border most printers have (typically 3-5 mm).
        public const double ScaleMarkBottomOffsetMm = 7.0;

        /// <summary>
        /// Where the 1 cm reference mark goes: an L shape centred at the bottom of the page, below the grid.
        /// Corner = the point where the horizontal bar (to the right) and vertical bar (upward) meet,
        /// measured from the page TOP-LEFT. The vertical bar is only included when the space below the
        /// grid can hold it without touching the eyes or their labels.
        /// </summary>
        public (double CornerX, double CornerY, bool IncludeVertical) GetScaleMarkPosition()
        {
            double gridBottom = Config.PageMarginTopMm + Rows * PairTotalHeightMm + (Rows - 1) * Config.RowSpacingMm;
            double freeBelowGrid = PageHeightMm - gridBottom;
            const double clearanceMm = 3.0;
            bool includeVertical = freeBelowGrid >= ScaleMarkBottomOffsetMm + ScaleMarkLengthMm + clearanceMm;

            double cornerX = (PageWidthMm - ScaleMarkLengthMm) / 2.0;
            double cornerY = PageHeightMm - ScaleMarkBottomOffsetMm;
            return (cornerX, cornerY, includeVertical);
        }

        /// <summary>Eye centres for a slot. Each eye is centred in a cell-sized box, so rows and columns stay aligned.</summary>
        public (double LeftX, double RightX, double Y) GetEyeCentersMm(int slotOnPage)
        {
            var (x, y) = GetCellOriginMm(slotOnPage);
            double leftX = x + CellEyeWidthMm / 2.0;
            double rightX = x + CellEyeWidthMm + Config.PairSpacingXMm + CellEyeWidthMm / 2.0;
            return (leftX, rightX, y + CellEyeHeightMm / 2.0);
        }
    }
}
