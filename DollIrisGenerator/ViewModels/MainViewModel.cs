using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using DollIrisGenerator.Controls;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;

namespace DollIrisGenerator.ViewModels
{
    public partial class MainViewModel : INotifyPropertyChanged
    {
        private IrisParameters _currentIris;
        private PresetDefinition? _selectedPreset;
        private OddEyeTemplate? _selectedOddEyeTemplate;
        private PdfTemplatePreset? _selectedPdfTemplate;
        private PreviewMode _previewMode = PreviewMode.Pair;
        // The pair view and the A4 sheet view each keep their own zoom
        private double _pairZoomScale = WpfIrisRenderer.DefaultPreviewZoom;
        private double _sheetZoomScale = WpfIrisRenderer.DefaultSheetPreviewZoom;
        private bool _showCutLines = true;
        private bool _showCenterMarks = true;
        private PrintSheetConfig _sheetConfig;
        private ObservableCollection<SheetItem> _sheetItems = new();
        private SheetItem? _selectedSheetItem;
        private string _statusMessage = "準備完了";

        // History Management (Undo / Redo Stack)
        private ObservableCollection<HistoryItem> _historyItems = new();
        // Each cell of the split preview has its own stacks; these point at the selected cell's (see MainViewModel.Compare.cs)
        private List<IrisParameters> _undoStack = new();
        private List<IrisParameters> _redoStack = new();
        private bool _isInternalStateChange = false;

        // Manual edits (sliders, colors, text) are grouped into one undo step: the state before the first
        // change is pushed immediately, and the step is closed after a short pause with no further changes.
        private static readonly TimeSpan ManualEditCommitDelay = TimeSpan.FromMilliseconds(700);
        private readonly DispatcherTimer _manualEditTimer;
        private IrisParameters _undoBaseline;
        private bool _manualEditPending = false;
        private bool _suppressManualEditTracking = false; // set while a command applies its own changes
        private readonly List<string?> _manualEditProperties = new(); // what the pending manual edit changed, for its history title

        // Favorites Management
        private ObservableCollection<FavoriteItem> _favorites = new();
        private FavoriteItem? _selectedFavorite;
        private readonly FavoritesStore _favoritesStore;
        private string? _favoritesLoadProblem;

        // Iris presets: the built-in ones, then the ones the user added (saved to user-presets.json)
        private readonly ObservableCollection<PresetDefinition> _presets = new(ColorPresets.All);
        private readonly UserPresetStore? _userPresetStore;
        private string? _userPresetsLoadProblem;
        private string _presetNameDraft = "";

        // A4 print sheet persistence (saved on exit, restored on start)
        private readonly PrintSheetStore? _printSheetStore;
        private string? _printSheetLoadProblem;

        // Dialogs and file flows
        private readonly IDialogService _dialogs;
        private readonly DesignFileWorkflow _files;

        public IrisParameters CurrentIris
        {
            get => _currentIris;
            set
            {
                if (_currentIris != null)
                {
                    _currentIris.PropertyChanged -= OnIrisPropertyChanged;
                }
                _currentIris = value;
                if (_currentIris != null)
                {
                    _currentIris.PropertyChanged += OnIrisPropertyChanged;
                    // Undo, loads and new designs replace the instance: the selected cell keeps showing it
                    if (_selectedSlot != null) _selectedSlot.Iris = _currentIris;
                }
                ResetManualEditTracking();
                OnPropertyChanged();
            }
        }

        public ObservableCollection<PresetDefinition> Presets => _presets;

        public PresetDefinition? SelectedPreset
        {
            get => _selectedPreset;
            set
            {
                _selectedPreset = value;
                OnPropertyChanged();
                OnSelectedPresetChanged();
                if (value != null)
                {
                    PushUndo();
                    ColorPresets.ApplyPreset(CurrentIris, value);
                    RecordHistory($"プリセット適用: {value.Name}");
                    StatusMessage = $"プリセット「{value.Name}」を適用しました。";
                }
            }
        }

        // Odd-Eye Templates
        public List<OddEyeTemplate> OddEyeTemplates => OddEyeTemplate.Presets;

        public OddEyeTemplate? SelectedOddEyeTemplate
        {
            get => _selectedOddEyeTemplate;
            set
            {
                _selectedOddEyeTemplate = value;
                OnPropertyChanged();
                if (value != null)
                {
                    PushUndo();
                    ApplyOddEyeTemplate(value);
                    RecordHistory($"オッドアイテンプレート適用: {value.Name}");
                    StatusMessage = $"オッドアイテンプレート「{value.Name}」を適用しました。";
                }
            }
        }

        // PDF Layout Templates
        public List<PdfTemplatePreset> PdfTemplates => PdfTemplatePreset.All;

        public PdfTemplatePreset? SelectedPdfTemplate
        {
            get => _selectedPdfTemplate;
            set
            {
                _selectedPdfTemplate = value;
                OnPropertyChanged();
                if (value != null)
                {
                    value.ApplyTo(SheetConfig);
                    StatusMessage = $"PDFテンプレート「{value.Name}」を適用しました。";
                }
            }
        }

        public PreviewMode PreviewMode
        {
            get => _previewMode;
            set
            {
                _previewMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ZoomScale)); // the status bar now shows the other view's zoom
            }
        }

        /// <summary>Zoom of the 左右ペア view (shared by every cell of the split preview).</summary>
        public double PairZoomScale
        {
            get => _pairZoomScale;
            set
            {
                _pairZoomScale = ClampZoom(value);
                OnPropertyChanged();
                if (PreviewMode == PreviewMode.Pair) OnPropertyChanged(nameof(ZoomScale));
            }
        }

        /// <summary>Zoom of the A4 シート view.</summary>
        public double SheetZoomScale
        {
            get => _sheetZoomScale;
            set
            {
                _sheetZoomScale = ClampZoom(value);
                OnPropertyChanged();
                if (PreviewMode == PreviewMode.A4Sheet) OnPropertyChanged(nameof(ZoomScale));
            }
        }

        /// <summary>Zoom of the view currently shown (what the zoom buttons and the status bar act on).</summary>
        public double ZoomScale
        {
            get => PreviewMode == PreviewMode.A4Sheet ? SheetZoomScale : PairZoomScale;
            set
            {
                if (PreviewMode == PreviewMode.A4Sheet) SheetZoomScale = value;
                else PairZoomScale = value;
            }
        }

        private static double ClampZoom(double value) =>
            Math.Clamp(value, WpfIrisRenderer.MinPreviewZoom, WpfIrisRenderer.MaxPreviewZoom);

        public bool ShowCutLines
        {
            get => _showCutLines;
            set { _showCutLines = value; OnPropertyChanged(); }
        }

        public bool ShowCenterMarks
        {
            get => _showCenterMarks;
            set { _showCenterMarks = value; OnPropertyChanged(); }
        }

        public PrintSheetConfig SheetConfig
        {
            get => _sheetConfig;
            set { _sheetConfig = value; OnPropertyChanged(); }
        }

        public ObservableCollection<SheetItem> SheetItems
        {
            get => _sheetItems;
            set
            {
                _sheetItems.CollectionChanged -= OnSheetItemsCollectionChanged;
                foreach (var item in _sheetItems) item.PropertyChanged -= OnSheetItemPropertyChanged;
                _sheetItems = value;
                HookSheetItems();
                OnPropertyChanged();
                OnSheetTotalsChanged();
            }
        }

        /// <summary>Number of rows (designs) on the A4 sheet.</summary>
        public int SheetRowCount => _sheetItems.Count;

        /// <summary>Sum of the quantities (pairs) of every row on the A4 sheet.</summary>
        public int SheetTotalPairs => _sheetItems.Sum(i => i.PairCount);

        private void HookSheetItems()
        {
            _sheetItems.CollectionChanged += OnSheetItemsCollectionChanged;
            foreach (var item in _sheetItems) item.PropertyChanged += OnSheetItemPropertyChanged;
        }

        private void OnSheetItemsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Clear (Reset) gives no OldItems; the cleared rows are no longer reachable, so their handlers do no harm
            if (e.OldItems != null) foreach (SheetItem item in e.OldItems) item.PropertyChanged -= OnSheetItemPropertyChanged;
            if (e.NewItems != null) foreach (SheetItem item in e.NewItems) item.PropertyChanged += OnSheetItemPropertyChanged;
            OnSheetTotalsChanged();
        }

        private void OnSheetItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SheetItem.PairCount)) OnSheetTotalsChanged();
        }

        private void OnSheetTotalsChanged()
        {
            OnPropertyChanged(nameof(SheetRowCount));
            OnPropertyChanged(nameof(SheetTotalPairs));
        }

        public SheetItem? SelectedSheetItem
        {
            get => _selectedSheetItem;
            set { _selectedSheetItem = value; OnPropertyChanged(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        // History & Favorites Properties
        public ObservableCollection<HistoryItem> HistoryItems => _historyItems;

        public ObservableCollection<FavoriteItem> Favorites => _favorites;

        public FavoriteItem? SelectedFavorite
        {
            get => _selectedFavorite;
            set { _selectedFavorite = value; OnPropertyChanged(); }
        }

        private bool _isExportingPdf = false;
        public bool IsExportingPdf
        {
            get => _isExportingPdf;
            private set
            {
                _isExportingPdf = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested(); // re-enable / disable the PDF buttons
            }
        }

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        // Commands
        public ICommand NewDesignCommand { get; }
        public ICommand RandomizeSeedCommand { get; }
        public ICommand RandomizeAttributesCommand { get; }
        public ICommand ZoomInCommand { get; }
        public ICommand ZoomOutCommand { get; }
        public ICommand ZoomResetCommand { get; }
        public ICommand ZoomRealSizeCommand { get; }
        public ICommand AddHighlightCommand { get; }
        public ICommand RemoveHighlightCommand { get; }
        public ICommand AddSymbolCommand { get; }
        public ICommand RemoveSymbolCommand { get; }
        public ICommand AddCurrentToSheetCommand { get; }
        public ICommand FillSheetWithCurrentCommand { get; }
        public ICommand RemoveSelectedSheetItemCommand { get; }
        public ICommand ClearSheetCommand { get; }
        public ICommand ExportPdfCommand { get; }
        public ICommand ExportSvgCommand { get; }
        public ICommand ExportPng600Command { get; }
        public ICommand ExportPng1200Command { get; }

        // New Feature Commands
        public ICommand AddCurrentAsPresetCommand { get; }
        public ICommand RenamePresetCommand { get; }
        public ICommand RemovePresetCommand { get; }
        public ICommand ExportPresetsCommand { get; }
        public ICommand ImportPresetsCommand { get; }
        public ICommand CopySvgCommand { get; }
        public ICommand UndoCommand { get; }
        public ICommand RedoCommand { get; }
        public ICommand RestoreHistoryCommand { get; }
        public ICommand ClearHistoryCommand { get; }
        public ICommand AddFavoriteCommand { get; }
        public ICommand RemoveFavoriteCommand { get; }
        public ICommand LoadFavoriteCommand { get; }
        public ICommand DuplicateFavoriteCommand { get; }
        public ICommand AddFavoritesToSheetCommand { get; }
        public ICommand ExportFavoritesCommand { get; }
        public ICommand ImportFavoritesCommand { get; }

        public MainViewModel() : this(new WpfDialogService(), new FavoritesStore(), new PrintSheetStore(), new UserPresetStore())
        {
        }

        /// <param name="printSheetStore">Saves and restores the A4 print sheet. Null keeps the sheet in memory only.</param>
        /// <param name="userPresetStore">Saves and restores the presets the user added. Null keeps them in memory only.</param>
        public MainViewModel(IDialogService dialogs, FavoritesStore favoritesStore, PrintSheetStore? printSheetStore = null,
            UserPresetStore? userPresetStore = null)
        {
            _dialogs = dialogs;
            _favoritesStore = favoritesStore;
            _printSheetStore = printSheetStore;
            _userPresetStore = userPresetStore;
            _files = new DesignFileWorkflow(dialogs, status => StatusMessage = status);

            _currentIris = new IrisParameters();
            _currentIris.PropertyChanged += OnIrisPropertyChanged;
            _undoBaseline = _currentIris.Clone();
            _manualEditTimer = new DispatcherTimer { Interval = ManualEditCommitDelay };
            _manualEditTimer.Tick += (_, _) => CommitManualEdit();
            _sheetConfig = new PrintSheetConfig();
            InitializeCompareSlots();

            // The presets the user added last time, after the built-in ones
            LoadUserPresets();

            // Default preset
            _selectedPreset = Presets[0];
            _presetNameDraft = _selectedPreset.Name;
            _selectedPdfTemplate = PdfTemplates[0];

            HookSheetItems();

            // Initialize default sheet with 1 pair of starting iris
            _sheetItems.Add(new SheetItem(_currentIris.Clone(), 1));

            // Restore the A4 print sheet saved on the last exit, replacing the default sheet above
            if (_printSheetStore != null)
            {
                var loadedSheet = _printSheetStore.Load();
                if (loadedSheet.State != null) ApplyPrintSheetState(loadedSheet.State);
                _printSheetLoadProblem = loadedSheet.Problem;
            }

            // Load persisted favorites if exists
            var loadedFavorites = _favoritesStore.LoadPersisted();
            foreach (var favorite in loadedFavorites.Favorites)
            {
                _favorites.Add(favorite);
            }
            _favoritesLoadProblem = loadedFavorites.Problem;

            // Setup base commands
            NewDesignCommand = new RelayCommand(OnNewDesign);
            RandomizeSeedCommand = new RelayCommand(OnRandomizeSeed);
            RandomizeAttributesCommand = new RelayCommand(OnRandomizeAttributes);
            ZoomInCommand = new RelayCommand(() => ZoomScale *= 1.25);
            ZoomOutCommand = new RelayCommand(() => ZoomScale /= 1.25);
            // Each view goes back to its own starting zoom (pair 360%, A4 sheet 100%)
            ZoomResetCommand = new RelayCommand(() => ZoomScale = PreviewMode == PreviewMode.A4Sheet
                ? WpfIrisRenderer.DefaultSheetPreviewZoom
                : WpfIrisRenderer.DefaultPreviewZoom);
            ZoomRealSizeCommand = new RelayCommand(() =>
            {
                ZoomScale = 1.0;
                StatusMessage = "実寸表示 (100% mm) に設定しました。画面上で原寸大を確認できます。";
            });

            AddHighlightCommand = new RelayCommand(OnAddHighlight);
            RemoveHighlightCommand = new RelayCommand(OnRemoveHighlight);
            AddSymbolCommand = new RelayCommand(OnAddSymbol);
            RemoveSymbolCommand = new RelayCommand(OnRemoveSymbol);

            AddCurrentToSheetCommand = new RelayCommand(OnAddCurrentToSheet);
            FillSheetWithCurrentCommand = new RelayCommand(OnFillSheetWithCurrent);
            RemoveSelectedSheetItemCommand = new RelayCommand(OnRemoveSelectedSheetItem);
            ClearSheetCommand = new RelayCommand(OnClearSheet);

            ExportPdfCommand = new RelayCommand(OnExportPdf, () => !IsExportingPdf);
            ExportSvgCommand = new RelayCommand(() => _files.ExportSvg(CurrentIris, GenerateSvg));
            ExportPng600Command = new RelayCommand(() => OnExportPng(600.0));
            ExportPng1200Command = new RelayCommand(() => OnExportPng(1200.0));

            // Setup extension commands
            AddCurrentAsPresetCommand = new RelayCommand(OnAddCurrentAsPreset);
            RenamePresetCommand = new RelayCommand(OnRenamePreset, () => IsSelectedPresetUserMade);
            RemovePresetCommand = new RelayCommand(OnRemovePreset, () => IsSelectedPresetUserMade);
            ExportPresetsCommand = new RelayCommand(OnExportPresets);
            ImportPresetsCommand = new RelayCommand(OnImportPresets);
            CopySvgCommand = new RelayCommand(OnCopySvg);

            UndoCommand = new RelayCommand(OnUndo, () => CanUndo);
            RedoCommand = new RelayCommand(OnRedo, () => CanRedo);
            RestoreHistoryCommand = new RelayCommand(OnRestoreHistory);
            ClearHistoryCommand = new RelayCommand(() => { ResetManualEditTracking(); _historyItems.Clear(); ClearAllUndoHistory(); OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); });

            AddFavoriteCommand = new RelayCommand(OnAddFavorite);
            RemoveFavoriteCommand = new RelayCommand(OnRemoveFavorite);
            LoadFavoriteCommand = new RelayCommand(OnLoadFavorite);
            DuplicateFavoriteCommand = new RelayCommand(OnDuplicateFavorite);
            AddFavoritesToSheetCommand = new RelayCommand(OnAddFavoritesToSheet, CanAddFavoritesToSheet);
            ExportFavoritesCommand = new RelayCommand(OnExportFavorites);
            ImportFavoritesCommand = new RelayCommand(OnImportFavorites);

            // Record initial history state
            RecordHistory("初期状態作成");
        }

        private async void OnExportPdf()
        {
            if (IsExportingPdf) return;
            await _files.ExportPdfAsync(SheetItems, CurrentIris, SheetConfig, busy => IsExportingPdf = busy);
        }

        private void OnExportPng(double dpi) =>
            _files.ExportPng(CurrentIris, dpi, SheetConfig.PairSpacingXMm, ShowCutLines, ShowCenterMarks);

        private void OnIrisPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(CurrentIris));
            if (!_isInternalStateChange && !_suppressManualEditTracking)
            {
                TrackManualEdit(e.PropertyName);
            }
        }

        private void TrackManualEdit(string? propertyName)
        {
            if (!_manualEditPending)
            {
                AddUndoSnapshot(_undoBaseline);
                _manualEditPending = true;
                _manualEditProperties.Clear();
            }
            _manualEditProperties.Add(propertyName);
            // Restart the pause timer: a whole slider drag becomes a single undo step
            _manualEditTimer.Stop();
            _manualEditTimer.Start();
        }

        private void CommitManualEdit()
        {
            if (!_manualEditPending) return;
            _manualEditTimer.Stop();
            _manualEditPending = false;
            string action = ManualEditDescriber.Describe(_manualEditProperties);
            _manualEditProperties.Clear();
            // Adjusting the same setting of the same cell again (several drags, typing a name) updates its one entry
            RecordHistory(action, mergeKey: $"{_selectedSlot.Number}|{action}");
        }

        private void ResetManualEditTracking()
        {
            _manualEditTimer?.Stop();
            _manualEditPending = false;
            _suppressManualEditTracking = false;
            if (_currentIris != null) _undoBaseline = _currentIris.Clone();
        }

        private void AddUndoSnapshot(IrisParameters snapshot)
        {
            _undoStack.Add(snapshot);
            if (_undoStack.Count > 40) _undoStack.RemoveAt(0);
            _redoStack.Clear();
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        }

        private void PushUndo()
        {
            if (_isInternalStateChange) return;
            CommitManualEdit();
            AddUndoSnapshot(CurrentIris.Clone());
            // The command's own changes are covered by this snapshot; RecordHistory re-enables tracking
            _suppressManualEditTracking = true;
        }

        private void OnUndo()
        {
            CommitManualEdit();
            if (_undoStack.Count > 0)
            {
                _isInternalStateChange = true;
                try
                {
                    _redoStack.Add(CurrentIris.Clone());
                    var previous = _undoStack[^1];
                    _undoStack.RemoveAt(_undoStack.Count - 1);
                    CurrentIris = previous;
                    StatusMessage = "デザインを1つ前の状態に戻しました (元に戻す)。";
                }
                finally
                {
                    _isInternalStateChange = false;
                    OnPropertyChanged(nameof(CanUndo));
                    OnPropertyChanged(nameof(CanRedo));
                }
            }
        }

        private void OnRedo()
        {
            CommitManualEdit();
            if (_redoStack.Count > 0)
            {
                _isInternalStateChange = true;
                try
                {
                    _undoStack.Add(CurrentIris.Clone());
                    var next = _redoStack[^1];
                    _redoStack.RemoveAt(_redoStack.Count - 1);
                    CurrentIris = next;
                    StatusMessage = "元に戻したデザインの変更をやり直しました (やり直す)。";
                }
                finally
                {
                    _isInternalStateChange = false;
                    OnPropertyChanged(nameof(CanUndo));
                    OnPropertyChanged(nameof(CanRedo));
                }
            }
        }

        /// <param name="mergeKey">
        /// When the newest entry has the same key, it is replaced instead of adding another one
        /// (undo still steps through every edit; only the timeline is kept short).
        /// </param>
        private void RecordHistory(string action, string? mergeKey = null)
        {
            var item = new HistoryItem(CurrentIris, action) { MergeKey = mergeKey };
            if (mergeKey != null && _historyItems.Count > 0 && _historyItems[0].MergeKey == mergeKey)
            {
                _historyItems[0] = item;
            }
            else
            {
                _historyItems.Insert(0, item);
            }
            if (_historyItems.Count > 50) _historyItems.RemoveAt(_historyItems.Count - 1);

            // This state is now the starting point for the next manual edit
            _suppressManualEditTracking = false;
            _undoBaseline = CurrentIris.Clone();
        }

        private void OnRestoreHistory(object? param)
        {
            if (param is HistoryItem item)
            {
                PushUndo();
                _isInternalStateChange = true;
                try
                {
                    CurrentIris = item.Snapshot.Clone();
                    StatusMessage = $"履歴「{item.Title}」({item.Timestamp:HH:mm:ss}) の状態に復元しました。";
                }
                finally
                {
                    _isInternalStateChange = false;
                }
            }
        }

        public void ApplyOddEyeTemplate(OddEyeTemplate template)
        {
            CurrentIris.IsOddEye = true;
            CurrentIris.BaseColor = template.LeftBaseColor;
            CurrentIris.InnerColor = template.LeftInnerColor;
            CurrentIris.OuterColor = template.LeftOuterColor;
            CurrentIris.RayColor = template.LeftRayColor;

            CurrentIris.OddEyeBaseColor = template.RightBaseColor;
            CurrentIris.OddEyeInnerColor = template.RightInnerColor;
            CurrentIris.OddEyeOuterColor = template.RightOuterColor;
            CurrentIris.OddEyeRayColor = template.RightRayColor;
        }

        private string GenerateSvg() =>
            SvgExporter.GeneratePairSvg(CurrentIris, includePrintGuides: ShowCutLines, pairSpacingMm: SheetConfig.PairSpacingXMm);

        private void OnCopySvg()
        {
            try
            {
                Clipboard.SetText(GenerateSvg());
                StatusMessage = "SVG ベクターコードをクリップボードにコピーしました。";
            }
            catch (Exception ex)
            {
                _dialogs.ShowWarning($"コピーに失敗しました: {ex.Message}", "エラー");
            }
        }

        /// <summary>
        /// 「🆕 新しいデザイン」: replaces the selected cell's design with the start-up design (the app's defaults).
        /// It does not depend on the preset shown in the 基本 tab, so the result is always the same.
        /// </summary>
        private void OnNewDesign()
        {
            PushUndo();
            CurrentIris = new IrisParameters();
            RecordHistory("新しいデザイン");
            string where = IsCompareSplit ? $"枠 {_selectedSlot.Number} のデザインを" : "デザインを";
            StatusMessage = $"{where}起動時と同じ初期状態にしました。前のデザインは ↩ 元に戻す (Ctrl+Z) で戻せます。";
        }

        private void OnRandomizeSeed()
        {
            PushUndo();
            CurrentIris.Seed = Random.Shared.Next(1, 999999);
            RecordHistory($"シード変更: {CurrentIris.Seed}");
            StatusMessage = $"乱数シードを変更しました: {CurrentIris.Seed}";
        }

        private void OnRandomizeAttributes()
        {
            var locks = RandomizeLocks;
            if (locks == RandomizeLocks.All)
            {
                // Nothing would change, so do not add an empty undo step
                StatusMessage = "すべての属性がロックされているため、ランダム生成で変わる項目がありません。「基本」タブの「すべて解除」か、各設定枠の「🔒 ランダム生成で変えない」でロックを外してください。";
                return;
            }

            PushUndo();
            _isInternalStateChange = true;
            try
            {
                double preservedSizeMm = CurrentIris.SizeMm;
                IrisShape preservedShape = CurrentIris.Shape;
                double preservedAspectRatio = CurrentIris.AspectRatio;
                double preservedTiltAngle = CurrentIris.TiltAngle;

                IrisRandomizer.RandomizeAttributes(CurrentIris, locks);

                // Explicit guarantee that size and dimensions are preserved
                CurrentIris.SizeMm = preservedSizeMm;
                CurrentIris.Shape = preservedShape;
                CurrentIris.AspectRatio = preservedAspectRatio;
                CurrentIris.TiltAngle = preservedTiltAngle;

                RecordHistory($"属性ランダム生成: {CurrentIris.Name}");
                string kept = locks == RandomizeLocks.None ? "" : $"・ロック中の項目 ({RandomizeLocksInfo.Describe(locks)})";
                StatusMessage = $"サイズ ({preservedSizeMm:F1}mm) と寸法{kept}を保持したまま属性をランダム生成しました: {CurrentIris.Name}";
            }
            finally
            {
                _isInternalStateChange = false;
                OnPropertyChanged(nameof(CurrentIris));
            }
        }

        private void OnAddHighlight()
        {
            PushUndo();
            CurrentIris.Highlights.Add(new HighlightItem
            {
                Shape = HighlightShape.Circle,
                PositionX = 0.25,
                PositionY = 0.25,
                Size = 0.15,
                Color = "#FFFFFF",
                Opacity = 0.85,
                Enabled = true
            });
            RecordHistory("ハイライト追加");
            StatusMessage = "新しいハイライトを追加しました。";
        }

        private void OnRemoveHighlight(object? param)
        {
            if (param is HighlightItem item)
            {
                PushUndo();
                CurrentIris.Highlights.Remove(item);
                RecordHistory("ハイライト削除");
                StatusMessage = "ハイライトを削除しました。";
            }
        }

        private void OnAddSymbol()
        {
            PushUndo();
            CurrentIris.Symbols.Add(new DecorativeSymbolItem
            {
                Type = SymbolType.Star4,
                Count = 1,
                Size = 0.18,
                PositionX = 0.25,
                PositionY = -0.15,
                Color = "#FFFFFF",
                Opacity = 0.85,
                Enabled = true
            });
            RecordHistory("装飾記号追加");
            StatusMessage = "装飾記号を追加しました。";
        }

        private void OnRemoveSymbol(object? param)
        {
            if (param is DecorativeSymbolItem item)
            {
                PushUndo();
                CurrentIris.Symbols.Remove(item);
                RecordHistory("装飾記号削除");
                StatusMessage = "装飾記号を削除しました。";
            }
        }

        private void OnAddCurrentToSheet()
        {
            SheetItems.Add(new SheetItem(CurrentIris.Clone(), 1));
            StatusMessage = $"現在のデザイン「{CurrentIris.Name}」をA4印刷シートに追加しました (計 {SheetItems.Count} 種)。";
        }

        /// <summary>
        /// Fills the empty slots of the sheet while keeping every design already on it.
        /// The current design is added first when it is not on the sheet yet (it may enlarge the cells and so
        /// lower the capacity), then the free slots of the last page are shared in proportion to the quantities.
        /// A sheet that is already exactly full is left unchanged.
        /// </summary>
        private void OnFillSheetWithCurrent()
        {
            if (SheetItems.Count == 0)
            {
                var design = CurrentIris.Clone();
                var single = new SheetLayout(new List<IrisParameters> { design }, SheetConfig);
                SheetItems.Add(new SheetItem(design, single.PairsPerPage));
                StatusMessage = $"A4シートを現在のデザインで満杯 ({single.Columns}列 × {single.Rows}行 = 計 {single.PairsPerPage} セット) に配置しました。";
                return;
            }

            // Cells are sized from the largest design, so the capacity depends on WHICH designs are on the sheet, not on their quantities
            var before = new SheetLayout(SheetItems, SheetConfig);
            int totalBefore = SheetItems.Sum(i => i.PairCount);
            if (totalBefore == SheetFill.TargetPairs(totalBefore, before.PairsPerPage))
            {
                StatusMessage = $"A4シートはすでに満杯です ({before.Columns}列 × {before.Rows}行 × {before.PageCount} ページ = 計 {totalBefore} セット)。" +
                                "デザインを入れ替えるには数量を減らすか、行を削除してください。";
                return;
            }

            string currentKey = JsonSerializer.Serialize(CurrentIris);
            bool addedCurrent = !SheetItems.Any(i => JsonSerializer.Serialize(i.Parameters) == currentKey);
            if (addedCurrent) SheetItems.Add(new SheetItem(CurrentIris.Clone(), 1));

            var layout = new SheetLayout(SheetItems, SheetConfig);
            var counts = SheetItems.Select(i => i.PairCount).ToList();
            int total = counts.Sum();
            int target = SheetFill.TargetPairs(total, layout.PairsPerPage);
            var filled = SheetFill.Distribute(counts, target, SheetItem.MaxPairCount);
            for (int i = 0; i < filled.Length; i++)
            {
                if (filled[i] != counts[i]) SheetItems[i].PairCount = filled[i];
            }

            int filledTotal = filled.Sum();
            int pages = SheetFill.TargetPairs(filledTotal, layout.PairsPerPage) / layout.PairsPerPage;
            string pageText = pages > 1 ? $" × {pages} ページ" : "";
            string addedText = addedCurrent ? $"現在のデザイン「{CurrentIris.Name}」を加え、" : "";
            string message = $"A4シートを満杯 ({layout.Columns}列 × {layout.Rows}行{pageText} = 計 {filledTotal} セット) に配置しました。" +
                             $"{addedText}{SheetItems.Count} 種のデザインに空き {filledTotal - totalBefore} 枠を数量の比率で配分しています。";
            if (filledTotal < target)
            {
                message = $"A4シートを {filledTotal} / {target} セットまで配置しました。{addedText}すべての行が上限 {SheetItem.MaxPairCount} セットに達したため、" +
                          $"残り {target - filledTotal} 枠は空いています。";
            }
            StatusMessage = message;
        }

        private void OnRemoveSelectedSheetItem(object? param)
        {
            // The row's ✕ button passes its own item; fall back to the list selection
            var target = param as SheetItem ?? SelectedSheetItem;
            if (target != null)
            {
                SheetItems.Remove(target);
                StatusMessage = "シートからアイテムを削除しました。";
            }
        }

        private void OnClearSheet()
        {
            SheetItems.Clear();
            StatusMessage = "A4印刷シートをクリアしました。";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
