using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using RetroMultiTools.Localization;
using RetroMultiTools.Models;
using RetroMultiTools.Services;
using RetroMultiTools.Utilities;
using RetroMultiTools.Utilities.Integrations;
using RetroMultiTools.Utilities.RomManagement;
using RetroMultiTools.Utilities.Verification;
using RetroMultiTools.Utilities.Mame;
using RetroMultiTools.Utilities.Mednafen;
using RetroMultiTools.Utilities.RetroArch;

namespace RetroMultiTools.Views.Browsing;

/// <summary>
/// Modern Big Picture Mode with hero spotlight and horizontal carousel layout.
/// Provides all the same features as the Classic view with a more visually
/// stunning, streaming-service-inspired interface.
/// </summary>
public partial class BigPictureModernView : UserControl
{
    private List<RomInfo> _allRoms = [];
    private List<RomInfo> _filteredRoms = [];
    private string _currentFolder = string.Empty;
    private int _selectedIndex = -1;
    private CancellationTokenSource? _artworkCts;
    private CancellationTokenSource? _preloadCts;
    private CancellationTokenSource? _searchDebounceCts;
    private CancellationTokenSource? _checksumCts;
    private bool _suppressFilterUpdate;

    /// <summary>Maps ROM file paths to the Image control on each carousel card.</summary>
    private readonly Dictionary<string, List<Image>> _cardImageMap = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>In-memory cache of card thumbnail bitmaps keyed by ROM file path.</summary>
    private readonly Dictionary<string, Bitmap> _thumbnailCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Current card scale factor (0.5-2.0).</summary>
    private double _cardScale = 1.0;

    // ── Dimensions ──
    private const int BaseCarouselCardWidth = 200;
    private const int BaseCarouselCardHeight = 280;
    private const int CarouselCardSpacing = 14;
    private const int PreloadMaxConcurrency = 3;
    private const double ScaleStep = 0.1;
    private const double ScaleMin = 0.5;
    private const double ScaleMax = 2.0;
    private const int SearchDebounceMs = 300;
    private const int LetterJumpDisplayMs = 800;
    private const int ScreensaverCycleMs = 5000;
    private const int SystemCycleDisplayMs = 800;
    private const int GalleryIntervalMs = 3000;
    private const int ArtworkViewerImageCount = 3;
    private const int MaxCarouselItemsPerRow = 50;
    private const int RecentlyPlayedMax = 15;

    // ── Carousel Row Tracking ──
    private int _focusedRowIndex;
    private int _focusedColIndex;
    private readonly List<CarouselRow> _carouselRows = [];

    // ── Cached brushes ──
    private static readonly IBrush CardDefaultBg = new SolidColorBrush(Color.Parse("#1A1A2E"));
    private static readonly IBrush CardHoverBg = new SolidColorBrush(Color.Parse("#252540"));
    private static readonly IBrush CardSelectedBorder = new SolidColorBrush(Color.Parse("#7C5CFC"));
    private static readonly IBrush CardNameFg = new SolidColorBrush(Color.Parse("#E2E0F0"));
    private static readonly IBrush CardSystemFg = new SolidColorBrush(Color.Parse("#7C5CFC"));
    private static readonly IBrush CardSizeFg = new SolidColorBrush(Color.Parse("#5A5A7A"));
    private static readonly IBrush CardInitialFg = new SolidColorBrush(Color.Parse("#1A1A2E"));
    private static readonly IBrush RatingStarFg = new SolidColorBrush(Color.Parse("#FFC857"));
    private static readonly IBrush RowHeaderFg = new SolidColorBrush(Color.Parse("#E2E0F0"));
    private static readonly IBrush RowCountFg = new SolidColorBrush(Color.Parse("#5A5A7A"));
    private static readonly IBrush SectionHeaderFg = new SolidColorBrush(Color.Parse("#7C5CFC"));
    private static readonly IBrush SubHeaderFg = new SolidColorBrush(Color.Parse("#FFC857"));
    private static readonly IBrush LabelFg = new SolidColorBrush(Color.Parse("#9A9ABE"));
    private static readonly IBrush ValueFg = new SolidColorBrush(Color.Parse("#E2E0F0"));
    private static readonly IBrush OverlayBorderBrush = new SolidColorBrush(Color.Parse("#2A2A40"));
    private static readonly Cursor HandCursor = new(StandardCursorType.Hand);

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<RomSystem, IBrush> SystemBrushCache = new();

    // ── Screensaver fields ──
    private DispatcherTimer? _inactivityTimer;
    private DispatcherTimer? _screensaverCycleTimer;
    private bool _screensaverActive;
    private int _lastScreensaverRomIndex = -1;

    // ── Letter Jump ──
    private CancellationTokenSource? _letterJumpCts;

    // ── System Cycle ──
    private CancellationTokenSource? _systemCycleCts;

    // ── Gallery Mode ──
    private DispatcherTimer? _galleryTimer;
    private bool _galleryActive;

    // ── Artwork Viewer ──
    private Bitmap?[] _artworkViewerImages = new Bitmap?[ArtworkViewerImageCount];
    private string[] _artworkViewerLabels = new string[ArtworkViewerImageCount];
    private int _artworkViewerIndex;

    // ── Hero artwork cache for the selected ROM (box art, snap & title screen) ──
    /// <summary>Box art bitmap loaded by LoadHeroArtworkAsync (NOT from thumbnail cache).</summary>
    private Bitmap? _heroBoxArtBitmap;
    private Bitmap? _heroSnapBitmap;
    private Bitmap? _heroTitleScreenBitmap;

    // ── Computed card dimensions ──
    private int CardWidth => (int)(BaseCarouselCardWidth * _cardScale);
    private int CardHeight => (int)(BaseCarouselCardHeight * _cardScale);
    /// <summary>Horizontal distance to scroll per mouse wheel notch (one card + gap).</summary>
    private int WheelScrollStep => CardWidth + CarouselCardSpacing;

    public BigPictureModernView()
    {
        InitializeComponent();
        PopulateSystemFilter();
        PopulateSortCombo();
        _cardScale = AppSettings.Instance.BigPictureCardScale;
        UpdateZoomLevelText();
        PopulateHelpOverlay();
        InitialiseInactivityTimer();
        KeyDown += OnKeyDownHandler;
        PointerMoved += OnPointerActivity;
        PointerPressed += OnPointerActivity;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    // ══════════════════════════════════════════════════════════════
    //  Lifecycle
    // ══════════════════════════════════════════════════════════════

    private void OnAttachedToVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        Focus();
        InitialiseGamepad();
    }

    private void OnDetachedFromVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        AttachedToVisualTree -= OnAttachedToVisualTree;
        DetachedFromVisualTree -= OnDetachedFromVisualTree;
        KeyDown -= OnKeyDownHandler;
        PointerMoved -= OnPointerActivity;
        PointerPressed -= OnPointerActivity;
        _searchDebounceCts?.Cancel(); _searchDebounceCts?.Dispose(); _searchDebounceCts = null;
        _checksumCts?.Cancel(); _checksumCts?.Dispose(); _checksumCts = null;
        _letterJumpCts?.Cancel(); _letterJumpCts?.Dispose(); _letterJumpCts = null;
        _systemCycleCts?.Cancel(); _systemCycleCts?.Dispose(); _systemCycleCts = null;
        StopGalleryMode();
        StopInactivityTimer();
        StopScreensaverCycleTimer();
        _screensaverActive = false;
        ScreensaverImage.Source = null;
        ArtworkViewerImage.Source = null;
        CancelArtworkPreload();
        CancelArtworkLoading();
        ClearHeroArtwork();
        ClearHeroExtraArtwork();
        ClearArtworkViewerReferences();
        ClearThumbnailCache();
        _cardImageMap.Clear();
        ShutdownGamepad();
    }

    // ══════════════════════════════════════════════════════════════
    //  Gamepad Integration
    // ══════════════════════════════════════════════════════════════

    private void InitialiseGamepad()
    {
        if (!AppSettings.Instance.GamepadEnabled) return;
        var gp = GamepadService.Instance;
        gp.ActionTriggered += OnGamepadAction;
        gp.ControllerConnected += OnControllerConnected;
        gp.ControllerDisconnected += OnControllerDisconnected;
        gp.Initialise();
        if (gp.IsAvailable)
        {
            gp.SetDeadZone(AppSettings.Instance.GamepadDeadZone);
            UpdateControllerStatus();
            UpdateHelpOverlayGamepadVisibility();
            UpdateHintText();
        }
        else
        {
            gp.ActionTriggered -= OnGamepadAction;
            gp.ControllerConnected -= OnControllerConnected;
            gp.ControllerDisconnected -= OnControllerDisconnected;
        }
    }

    private void ShutdownGamepad()
    {
        var gp = GamepadService.Instance;
        gp.ActionTriggered -= OnGamepadAction;
        gp.ControllerConnected -= OnControllerConnected;
        gp.ControllerDisconnected -= OnControllerDisconnected;
        gp.Shutdown();
    }

    private void OnControllerConnected(string name)
    {
        StatusText.Text = string.Format(LocalizationManager.Instance["BigPicture_GamepadConnected"], name);
        UpdateControllerStatus();
        UpdateHelpOverlayGamepadVisibility();
        UpdateHintText();
    }

    private void OnControllerDisconnected()
    {
        StatusText.Text = LocalizationManager.Instance["BigPicture_GamepadDisconnected"];
        UpdateControllerStatus();
        UpdateHelpOverlayGamepadVisibility();
        UpdateHintText();
    }

    private void UpdateControllerStatus()
    {
        var gp = GamepadService.Instance;
        if (!gp.IsAvailable) { GamepadStatusText.Text = string.Empty; return; }
        GamepadStatusText.Text = gp.IsControllerConnected
            ? $"\U0001f3ae {gp.ControllerName}"
            : $"\U0001f3ae {LocalizationManager.Instance["BigPicture_GamepadNone"]}";
    }

    private void UpdateHintText()
    {
        var gp = GamepadService.Instance;
        var loc = LocalizationManager.Instance;
        HintText.Text = (gp.IsAvailable && gp.IsControllerConnected)
            ? loc["BigPicture_HintsPad"] : loc["BigPicture_Hints"];
    }

    private void OnPointerActivity(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        ResetInactivityTimer();
        if (_screensaverActive) DismissScreensaver();
    }

    private void OnGamepadAction(GamepadAction action)
    {
        ResetInactivityTimer();
        if (_screensaverActive) { DismissScreensaver(); return; }
        if (RomInfoOverlay.IsVisible) { if (action is GamepadAction.Back or GamepadAction.RomInfo) DismissRomInfoOverlay(); return; }
        if (StatsOverlay.IsVisible) { if (action is GamepadAction.Back or GamepadAction.StatsOverlay) DismissStatsOverlay(); return; }
        if (ArtworkViewerOverlay.IsVisible)
        {
            switch (action)
            {
                case GamepadAction.Back or GamepadAction.ArtworkViewer: DismissArtworkViewer(); break;
                case GamepadAction.NavigateLeft: CycleArtworkViewer(-1); break;
                case GamepadAction.NavigateRight: CycleArtworkViewer(1); break;
            }
            return;
        }
        if (HelpOverlay.IsVisible) { if (action is GamepadAction.Back or GamepadAction.Help) HelpOverlay.IsVisible = false; return; }
        if (_galleryActive && action is GamepadAction.Back) { StopGalleryMode(); return; }

        switch (action)
        {
            case GamepadAction.NavigateLeft: NavigateHorizontal(-1); break;
            case GamepadAction.NavigateRight: NavigateHorizontal(1); break;
            case GamepadAction.NavigateUp: NavigateVertical(-1); break;
            case GamepadAction.NavigateDown: NavigateVertical(1); break;
            case GamepadAction.Confirm: LaunchSelectedRom(); break;
            case GamepadAction.Back: ExitBigPictureMode(); break;
            case GamepadAction.ToggleFavorite: ToggleSelectedFavorite(); break;
            case GamepadAction.Search: SearchBox.Focus(); break;
            case GamepadAction.Help: ToggleHelpOverlay(); break;
            case GamepadAction.RomInfo: ToggleRomInfoOverlay(); break;
            case GamepadAction.RandomGame: SelectRandomGame(); break;
            case GamepadAction.PageUp: NavigateVertical(-3); break;
            case GamepadAction.PageDown: NavigateVertical(3); break;
            case GamepadAction.Home: SelectFirstGame(); break;
            case GamepadAction.End: SelectLastGame(); break;
            case GamepadAction.ZoomIn: ZoomIn(); break;
            case GamepadAction.ZoomOut: ZoomOut(); break;
            case GamepadAction.StatsOverlay: ToggleStatsOverlay(); break;
            case GamepadAction.GalleryMode: ToggleGalleryMode(); break;
            case GamepadAction.ArtworkViewer: ToggleArtworkViewer(); break;
            case GamepadAction.CycleRating: CycleSelectedRating(); break;
            case GamepadAction.RecentlyPlayed: ScrollToRecentlyPlayedRow(); break;
            case GamepadAction.SystemCycleForward: CycleSystemFilter(1); break;
            case GamepadAction.SystemCycleBackward: CycleSystemFilter(-1); break;
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  Public API
    // ══════════════════════════════════════════════════════════════

    public void LoadFolder(string folderPath, List<RomInfo>? existingRoms = null)
    {
        if (string.IsNullOrEmpty(folderPath)) return;
        _currentFolder = folderPath;

        // Reset UI state for the new folder
        CancelArtworkPreload();
        CancelArtworkLoading();
        _selectedIndex = -1;
        _focusedRowIndex = 0;
        _focusedColIndex = 0;
        _suppressFilterUpdate = true;
        SearchBox.Text = string.Empty;
        SystemFilterCombo.SelectedIndex = 0;
        FavoritesFilterButton.IsChecked = false;
        _suppressFilterUpdate = false;
        ClearHeroSection();
        ClearThumbnailCache();

        if (existingRoms != null && existingRoms.Count > 0)
        {
            _allRoms = new List<RomInfo>(existingRoms);
            ApplyFilterAndSort();
            StatusText.Text = string.Format(LocalizationManager.Instance["BigPicture_LoadedRoms"],
                _allRoms.Count, _currentFolder);
            StartArtworkPreload(_allRoms);
        }
        else
        {
            ScanCurrentFolderFireAndForget();
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  Folder Scanning
    // ══════════════════════════════════════════════════════════════

    private async void ScanCurrentFolderFireAndForget()
    {
        try { await ScanCurrentFolderAsync(); }
        catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"[BigPictureModern] Scan failed: {ex.Message}"); }
    }

    private async Task ScanCurrentFolderAsync()
    {
        var loc = LocalizationManager.Instance;
        StatusText.Text = loc["BigPicture_Scanning"];
        ScanProgressBar.IsVisible = true;
        BrowseFolderButton.IsEnabled = false;
        RescanButton.IsEnabled = false;
        try
        {
            var progress = new Progress<string>(msg => StatusText.Text = msg);
            _allRoms = await Task.Run(() => RomOrganizer.ScanDirectory(_currentFolder, progress));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText.Text = string.Format(loc["BigPicture_ScanError"], ex.Message);
            _allRoms = [];
        }
        finally
        {
            ScanProgressBar.IsVisible = false;
            BrowseFolderButton.IsEnabled = true;
            RescanButton.IsEnabled = true;
        }
        ApplyFilterAndSort();
        if (_allRoms.Count > 0)
        {
            StatusText.Text = string.Format(loc["BigPicture_FoundRoms"], _allRoms.Count, _currentFolder);
            StartArtworkPreload(_allRoms);
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  Filter / Sort / Search
    // ══════════════════════════════════════════════════════════════

    private void PopulateSystemFilter()
    {
        SystemFilterCombo.Items.Clear();
        SystemFilterCombo.Items.Add(new ComboBoxItem
        {
            Content = LocalizationManager.Instance["BigPicture_AllSystems"],
            Tag = "all"
        });
        foreach (RomSystem sys in Enum.GetValues<RomSystem>())
        {
            if (sys == RomSystem.Unknown) continue;
            SystemFilterCombo.Items.Add(new ComboBoxItem
            {
                Content = RomOrganizer.GetSystemDisplayName(sys),
                Tag = sys
            });
        }
        SystemFilterCombo.SelectedIndex = 0;
    }

    private void PopulateSortCombo()
    {
        var loc = LocalizationManager.Instance;
        SortCombo.Items.Clear();
        SortCombo.Items.Add(new ComboBoxItem { Content = loc["BigPicture_SortNameAsc"], Tag = "name_asc" });
        SortCombo.Items.Add(new ComboBoxItem { Content = loc["BigPicture_SortNameDesc"], Tag = "name_desc" });
        SortCombo.Items.Add(new ComboBoxItem { Content = loc["BigPicture_SortSystem"], Tag = "system" });
        SortCombo.Items.Add(new ComboBoxItem { Content = loc["BigPicture_SortSizeAsc"], Tag = "size_asc" });
        SortCombo.Items.Add(new ComboBoxItem { Content = loc["BigPicture_SortSizeDesc"], Tag = "size_desc" });
        SortCombo.Items.Add(new ComboBoxItem { Content = loc["BigPicture_SortRecentlyPlayed"], Tag = "recent" });
        SortCombo.Items.Add(new ComboBoxItem { Content = loc["BigPicture_SortRating"], Tag = "rating" });
        SortCombo.Items.Add(new ComboBoxItem { Content = loc["BigPicture_SortPlayCount"], Tag = "play_count" });
        SortCombo.SelectedIndex = 0;
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e) => DebouncedSearch();
    private void SystemFilterCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e) => ApplyFilterAndSort();
    private void SortCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e) => ApplyFilterAndSort();
    private void FavoritesFilterButton_Changed(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ApplyFilterAndSort();

    private async void DebouncedSearch()
    {
        _searchDebounceCts?.Cancel();
        _searchDebounceCts?.Dispose();
        _searchDebounceCts = new CancellationTokenSource();
        var token = _searchDebounceCts.Token;
        try
        {
            await Task.Delay(SearchDebounceMs, token);
            if (!token.IsCancellationRequested) ApplyFilterAndSort();
        }
        catch (OperationCanceledException) { }
    }

    private void ApplyFilterAndSort()
    {
        if (_suppressFilterUpdate) return;

        string? selectedPath = (_selectedIndex >= 0 && _selectedIndex < _filteredRoms.Count)
            ? _filteredRoms[_selectedIndex].FilePath : null;

        IEnumerable<RomInfo> filtered = _allRoms;

        if (FavoritesFilterButton.IsChecked == true)
        {
            var favorites = AppSettings.Instance.Favorites;
            filtered = filtered.Where(r => favorites.Contains(r.FilePath));
        }
        if (SystemFilterCombo.SelectedItem is ComboBoxItem sysItem && sysItem.Tag is RomSystem selectedSystem)
            filtered = filtered.Where(r => r.System == selectedSystem);

        string? searchText = SearchBox?.Text;
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            string search = searchText.Trim();
            filtered = filtered.Where(r =>
                r.FileName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                r.SystemName.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        string sortTag = (SortCombo?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "name_asc";
        filtered = sortTag switch
        {
            "name_desc" => filtered.OrderByDescending(r => r.FileName, StringComparer.OrdinalIgnoreCase),
            "system" => filtered.OrderBy(r => r.SystemName, StringComparer.OrdinalIgnoreCase)
                                .ThenBy(r => r.FileName, StringComparer.OrdinalIgnoreCase),
            "size_asc" => filtered.OrderBy(r => r.FileSize),
            "size_desc" => filtered.OrderByDescending(r => r.FileSize),
            "recent" => SortByRecentlyPlayed(filtered),
            "rating" => filtered.OrderByDescending(r => AppSettings.Instance.GetRating(r.FilePath))
                                .ThenBy(r => r.FileName, StringComparer.OrdinalIgnoreCase),
            "play_count" => filtered.OrderByDescending(r => AppSettings.Instance.GetPlayCount(r.FilePath))
                                    .ThenBy(r => r.FileName, StringComparer.OrdinalIgnoreCase),
            _ => filtered.OrderBy(r => r.FileName, StringComparer.OrdinalIgnoreCase)
        };

        _filteredRoms = filtered.ToList();
        RebuildCarousels(selectedPath);
        EmptyState.IsVisible = _filteredRoms.Count == 0;
        UpdateRomCount();

        if (_allRoms.Count > 0 && _filteredRoms.Count != _allRoms.Count)
            StatusText.Text = string.Format(LocalizationManager.Instance["BigPicture_ShowingFiltered"],
                _filteredRoms.Count, _allRoms.Count);
    }

    private static IEnumerable<RomInfo> SortByRecentlyPlayed(IEnumerable<RomInfo> roms)
    {
        var recentList = AppSettings.Instance.RecentlyPlayed;
        var recentIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < recentList.Count; i++) recentIndex[recentList[i]] = i;
        return roms.OrderBy(r => recentIndex.TryGetValue(r.FilePath, out int idx) ? idx : int.MaxValue)
                   .ThenBy(r => r.FileName, StringComparer.OrdinalIgnoreCase);
    }

    private void UpdateRomCount()
    {
        var loc = LocalizationManager.Instance;
        if (_allRoms.Count == 0) { RomCountText.Text = string.Empty; return; }
        RomCountText.Text = _filteredRoms.Count == _allRoms.Count
            ? string.Format(loc["BigPicture_RomCount"], _allRoms.Count)
            : string.Format(loc["BigPicture_RomCountFiltered"], _filteredRoms.Count, _allRoms.Count);
    }

    // ══════════════════════════════════════════════════════════════
    //  Carousel Building
    // ══════════════════════════════════════════════════════════════

    private enum CarouselRowKind { RecentlyPlayed, Favorites, TopRated, MostPlayed, AllGames, System }

    private sealed class CarouselRow
    {
        public CarouselRowKind Kind { get; init; }
        public string Title { get; init; } = "";
        public List<RomInfo> Roms { get; init; } = [];
        public StackPanel CardPanel { get; init; } = null!;
        public int GlobalStartIndex { get; set; }
    }

    private void RebuildCarousels(string? previousSelection = null)
    {
        // Null out Image.Source references before clearing to allow bitmaps to be collected
        foreach (var images in _cardImageMap.Values)
            foreach (var img in images) img.Source = null;
        CarouselContainer.Children.Clear();
        _cardImageMap.Clear();
        _carouselRows.Clear();
        _selectedIndex = -1;
        _focusedRowIndex = 0;
        _focusedColIndex = 0;

        if (_filteredRoms.Count == 0)
        {
            ClearHeroSection();
            return;
        }

        var loc = LocalizationManager.Instance;
        int globalIndex = 0;

        // ── Row 1: Recently Played ──
        var recentPaths = AppSettings.Instance.RecentlyPlayed;
        var recentRoms = new List<RomInfo>();
        var romLookup = new Dictionary<string, RomInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in _filteredRoms) romLookup.TryAdd(r.FilePath, r);
        foreach (string p in recentPaths)
        {
            if (recentRoms.Count >= RecentlyPlayedMax) break;
            if (romLookup.TryGetValue(p, out var rom)) recentRoms.Add(rom);
        }
        if (recentRoms.Count > 0)
        {
            var row = BuildCarouselRow(CarouselRowKind.RecentlyPlayed, loc["BigPictureModern_RecentlyPlayed"], recentRoms, globalIndex);
            _carouselRows.Add(row);
            CarouselContainer.Children.Add(BuildCarouselRowUI(row));
            globalIndex += recentRoms.Count;
        }

        // ── Row 2: Favorites ──
        var favSet = AppSettings.Instance.Favorites;
        var favRoms = _filteredRoms.Where(r => favSet.Contains(r.FilePath)).ToList();
        if (favRoms.Count > 0)
        {
            var row = BuildCarouselRow(CarouselRowKind.Favorites, loc["BigPictureModern_Favorites"], favRoms, globalIndex);
            _carouselRows.Add(row);
            CarouselContainer.Children.Add(BuildCarouselRowUI(row));
            globalIndex += favRoms.Count;
        }

        // ── Row 3: Top Rated ──
        var topRated = _filteredRoms
            .Where(r => AppSettings.Instance.GetRating(r.FilePath) > 0)
            .OrderByDescending(r => AppSettings.Instance.GetRating(r.FilePath))
            .Take(MaxCarouselItemsPerRow)
            .ToList();
        if (topRated.Count > 0)
        {
            var row = BuildCarouselRow(CarouselRowKind.TopRated, loc["BigPictureModern_TopRated"], topRated, globalIndex);
            _carouselRows.Add(row);
            CarouselContainer.Children.Add(BuildCarouselRowUI(row));
            globalIndex += topRated.Count;
        }

        // ── Row 4: Most Played ──
        var mostPlayed = _filteredRoms
            .Where(r => AppSettings.Instance.GetPlayCount(r.FilePath) > 0)
            .OrderByDescending(r => AppSettings.Instance.GetPlayCount(r.FilePath))
            .Take(MaxCarouselItemsPerRow)
            .ToList();
        if (mostPlayed.Count > 0)
        {
            var row = BuildCarouselRow(CarouselRowKind.MostPlayed, loc["BigPictureModern_MostPlayed"], mostPlayed, globalIndex);
            _carouselRows.Add(row);
            CarouselContainer.Children.Add(BuildCarouselRowUI(row));
            globalIndex += mostPlayed.Count;
        }

        // ── Row 5: All Games ──
        var allRow = BuildCarouselRow(CarouselRowKind.AllGames,
            $"{loc["BigPictureModern_AllGames"]} ({_filteredRoms.Count})",
            _filteredRoms, globalIndex);
        _carouselRows.Add(allRow);
        CarouselContainer.Children.Add(BuildCarouselRowUI(allRow));
        globalIndex += _filteredRoms.Count;

        // ── Per-System Rows ──
        var systemGroups = _filteredRoms
            .GroupBy(r => r.System)
            .Where(g => g.Key != RomSystem.Unknown)
            .OrderBy(g => g.First().SystemName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var group in systemGroups)
        {
            var groupList = group.ToList();
            if (groupList.Count < 2) continue; // Skip systems with only 1 ROM
            var sysRow = BuildCarouselRow(CarouselRowKind.System,
                $"{groupList[0].SystemName} ({groupList.Count})",
                groupList, globalIndex);
            _carouselRows.Add(sysRow);
            CarouselContainer.Children.Add(BuildCarouselRowUI(sysRow));
            globalIndex += groupList.Count;
        }

        // Select the first game (or restore previous)
        if (_filteredRoms.Count > 0)
        {
            int restoreIndex = 0;
            if (previousSelection != null)
            {
                int found = _filteredRoms.FindIndex(r =>
                    string.Equals(r.FilePath, previousSelection, StringComparison.OrdinalIgnoreCase));
                if (found >= 0) restoreIndex = found;
            }
            SelectGameByFilteredIndex(restoreIndex);
        }
    }

    private CarouselRow BuildCarouselRow(CarouselRowKind kind, string title, List<RomInfo> roms, int globalStartIndex)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = CarouselCardSpacing };
        var row = new CarouselRow { Kind = kind, Title = title, Roms = roms, CardPanel = panel, GlobalStartIndex = globalStartIndex };
        // Cards capture 'row' in a closure for click handling only – the reference
        // is not dereferenced during construction, so passing it before the panel
        // is fully populated is safe.
        for (int i = 0; i < roms.Count; i++)
        {
            var card = CreateCarouselCard(roms[i], i, row);
            panel.Children.Add(card);
        }
        return row;
    }

    private Border BuildCarouselRowUI(CarouselRow row)
    {
        var scrollViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Padding = new Thickness(48, 0, 48, 4),
            Content = row.CardPanel
        };

        // Enable horizontal scroll with the mouse wheel on carousel rows
        scrollViewer.PointerWheelChanged += (_, e) =>
        {
            double delta = e.Delta.Y;
            if (Math.Abs(delta) < 0.001) return;
            scrollViewer.Offset = scrollViewer.Offset.WithX(
                scrollViewer.Offset.X - delta * WheelScrollStep);
            e.Handled = true;
        };

        return new Border
        {
            Padding = new Thickness(0, 4, 0, 8),
            Child = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    // Row header
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 12,
                        Margin = new Thickness(48, 0, 48, 0),
                        Children =
                        {
                            new TextBlock
                            {
                                Text = row.Title,
                                FontSize = 20,
                                FontWeight = FontWeight.Bold,
                                Foreground = RowHeaderFg,
                                VerticalAlignment = VerticalAlignment.Center
                            }
                        }
                    },
                    scrollViewer
                }
            }
        };
    }

    private Border CreateCarouselCard(RomInfo rom, int localIndex, CarouselRow ownerRow)
    {
        string systemInitial = !string.IsNullOrEmpty(rom.SystemName)
            ? rom.SystemName[..1].ToUpperInvariant() : "?";

        int bannerHeight = (int)(160 * _cardScale);
        double nameFontSize = Math.Max(10, 13 * _cardScale);
        double metaFontSize = Math.Max(9, 11 * _cardScale);
        double initialFontSize = Math.Max(28, 52 * _cardScale);

        var cardImage = new Image { Stretch = Stretch.UniformToFill, IsVisible = false };
        if (_thumbnailCache.TryGetValue(rom.FilePath, out var cached))
        {
            cardImage.Source = cached;
            cardImage.IsVisible = true;
        }

        // Track card images
        if (!_cardImageMap.TryGetValue(rom.FilePath, out var imageList))
        {
            imageList = [];
            _cardImageMap[rom.FilePath] = imageList;
        }
        imageList.Add(cardImage);

        int rating = AppSettings.Instance.GetRating(rom.FilePath);
        int playCount = AppSettings.Instance.GetPlayCount(rom.FilePath);

        var card = new Border
        {
            Width = CardWidth,
            Height = CardHeight,
            Background = CardDefaultBg,
            CornerRadius = new CornerRadius(12),
            Margin = new Thickness(0),
            Cursor = HandCursor,
            ClipToBounds = true,
            Tag = rom,
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new Border
                    {
                        Height = bannerHeight,
                        CornerRadius = new CornerRadius(12, 12, 0, 0),
                        Background = GetSystemBrush(rom.System),
                        ClipToBounds = true,
                        Child = new Grid
                        {
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = systemInitial,
                                    FontSize = initialFontSize,
                                    FontWeight = FontWeight.Bold,
                                    Foreground = CardInitialFg,
                                    Opacity = 0.2,
                                    HorizontalAlignment = HorizontalAlignment.Center,
                                    VerticalAlignment = VerticalAlignment.Center
                                },
                                cardImage
                            }
                        }
                    },
                    new TextBlock
                    {
                        Text = Path.GetFileNameWithoutExtension(rom.FileName),
                        FontSize = nameFontSize,
                        Foreground = CardNameFg,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxLines = 2,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(10, 4, 10, 0),
                        Height = (int)(36 * _cardScale)
                    },
                    new StackPanel
                    {
                        Margin = new Thickness(10, 0, 10, 8),
                        Spacing = 2,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = rom.SystemName,
                                FontSize = metaFontSize,
                                Foreground = CardSystemFg,
                                TextTrimming = TextTrimming.CharacterEllipsis
                            },
                            new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                Spacing = 8,
                                Children =
                                {
                                    new TextBlock
                                    {
                                        Text = rom.FileSizeFormatted,
                                        FontSize = metaFontSize,
                                        Foreground = CardSizeFg
                                    },
                                    new TextBlock
                                    {
                                        Text = $"\u25B6 {playCount}",
                                        FontSize = metaFontSize,
                                        Foreground = CardSystemFg,
                                        IsVisible = playCount > 0
                                    }
                                }
                            },
                            new TextBlock
                            {
                                Text = FormatRatingStars(rating),
                                FontSize = metaFontSize,
                                Foreground = RatingStarFg,
                                IsVisible = rating > 0
                            }
                        }
                    }
                }
            }
        };

        card.PointerEntered += (_, _) =>
        {
            // Only show hover effect if this card is not the currently selected/highlighted one
            if (card.BorderBrush != CardSelectedBorder)
                card.Background = CardHoverBg;
        };

        card.PointerExited += (_, _) =>
        {
            if (card.BorderBrush != CardSelectedBorder)
                card.Background = CardDefaultBg;
        };

        card.PointerPressed += (s, pe) =>
        {
            if (pe.GetCurrentPoint(card).Properties.IsRightButtonPressed)
            {
                SelectRomFromCarousel(rom, ownerRow);
                ShowCardContextMenu(card, rom);
            }
            else
            {
                SelectRomFromCarousel(rom, ownerRow);
            }
            Focus();
        };

        card.DoubleTapped += (_, _) => LaunchSelectedRom();

        return card;
    }

    private void ShowCardContextMenu(Border card, RomInfo rom)
    {
        var loc = LocalizationManager.Instance;
        var menu = new ContextMenu();

        var launchItem = new MenuItem { Header = loc["BigPicture_Launch"] };
        launchItem.Click += (_, _) => LaunchSelectedRom();
        menu.Items.Add(launchItem);

        if (MameLauncher.IsSystemSupported(rom.System))
        {
            var mameItem = new MenuItem { Header = loc["BigPicture_LaunchMame"] };
            mameItem.Click += (_, _) => LaunchSelectedWithMame();
            menu.Items.Add(mameItem);
        }

        if (MednafenLauncher.IsSystemSupported(rom.System))
        {
            var mednafenItem = new MenuItem { Header = loc["BigPicture_LaunchMednafen"] };
            mednafenItem.Click += (_, _) => LaunchSelectedWithMednafen();
            menu.Items.Add(mednafenItem);
        }

        menu.Items.Add(new Separator());

        bool isFav = AppSettings.Instance.IsFavorite(rom.FilePath);
        var favItem = new MenuItem { Header = isFav ? $"\u2605 {loc["BigPicture_HelpFavorite"]}" : $"\u2606 {loc["BigPicture_HelpFavorite"]}" };
        favItem.Click += (_, _) => ToggleSelectedFavorite();
        menu.Items.Add(favItem);

        int rating = AppSettings.Instance.GetRating(rom.FilePath);
        var ratingItem = new MenuItem { Header = $"{loc["BigPicture_RatingLabel"]} {FormatRatingStars(rating)}" };
        ratingItem.Click += (_, _) => CycleSelectedRating();
        menu.Items.Add(ratingItem);

        menu.Items.Add(new Separator());

        var infoItem = new MenuItem { Header = loc["BigPicture_RomInfoTitle"] };
        infoItem.Click += (_, _) => ToggleRomInfoOverlay();
        menu.Items.Add(infoItem);

        var artItem = new MenuItem { Header = loc["BigPicture_HelpArtworkViewer"] };
        artItem.Click += (_, _) => ToggleArtworkViewer();
        menu.Items.Add(artItem);

        menu.Items.Add(new Separator());

        var openLocItem = new MenuItem { Header = loc["BigPicture_OpenFileLocation"] };
        openLocItem.Click += (_, _) => OpenFileLocation(rom.FilePath);
        menu.Items.Add(openLocItem);

        var copyPathItem = new MenuItem { Header = loc["BigPicture_CopyFilePath"] };
        copyPathItem.Click += async (_, _) => await CopyToClipboardAsync(rom.FilePath);
        menu.Items.Add(copyPathItem);

        card.ContextMenu = menu;
        menu.Open(card);
    }

    // ══════════════════════════════════════════════════════════════
    //  Selection & Hero Spotlight
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Selects a ROM from a carousel card click. Clears filters if needed
    /// to make the ROM visible in the filtered list.
    /// When <paramref name="clickedRow"/> is provided, the highlight stays in
    /// that row (e.g. Recently Played, Favorites). When null, the default
    /// AllGames row is preferred for consistent keyboard navigation.
    /// </summary>
    private void SelectRomFromCarousel(RomInfo rom, CarouselRow? clickedRow = null)
    {
        Predicate<RomInfo> match = r =>
            string.Equals(r.FilePath, rom.FilePath, StringComparison.OrdinalIgnoreCase);
        int idx = _filteredRoms.FindIndex(match);
        if (idx < 0)
        {
            _suppressFilterUpdate = true;
            SearchBox.Text = string.Empty;
            SystemFilterCombo.SelectedIndex = 0;
            FavoritesFilterButton.IsChecked = false;
            _suppressFilterUpdate = false;
            _searchDebounceCts?.Cancel();
            ApplyFilterAndSort();
            idx = _filteredRoms.FindIndex(match);
        }
        if (idx >= 0) SelectGameByFilteredIndex(idx, clickedRow);
    }

    private void SelectGameByFilteredIndex(int index, CarouselRow? preferredRow = null)
    {
        if (index < 0 || index >= _filteredRoms.Count) return;

        // Deselect previous card highlights
        DeselectAllCards();

        _selectedIndex = index;
        var rom = _filteredRoms[index];

        // Find which row and column this ROM belongs to
        LocateRomInCarousels(rom, preferredRow);

        // Highlight the card in the focused row
        HighlightFocusedCard();

        // Ensure the focused row is scrolled into view
        ScrollToRow(_focusedRowIndex);

        // Update Hero
        UpdateHeroSpotlight(rom);
    }

    private void LocateRomInCarousels(RomInfo rom, CarouselRow? preferredRow = null)
    {
        // If a preferred row is specified (e.g. from a card click), try to use it first.
        if (preferredRow != null)
        {
            int prefIdx = _carouselRows.IndexOf(preferredRow);
            if (prefIdx >= 0)
            {
                for (int c = 0; c < preferredRow.Roms.Count; c++)
                {
                    if (string.Equals(preferredRow.Roms[c].FilePath, rom.FilePath, StringComparison.OrdinalIgnoreCase))
                    {
                        _focusedRowIndex = prefIdx;
                        _focusedColIndex = c;
                        return;
                    }
                }
            }
        }

        // Prefer the "All Games" row so that horizontal navigation stays
        // inside the canonical row that contains every filtered ROM.
        int fallbackRow = -1, fallbackCol = -1;
        for (int r = 0; r < _carouselRows.Count; r++)
        {
            var row = _carouselRows[r];
            for (int c = 0; c < row.Roms.Count; c++)
            {
                if (!string.Equals(row.Roms[c].FilePath, rom.FilePath, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (row.Kind == CarouselRowKind.AllGames)
                {
                    _focusedRowIndex = r;
                    _focusedColIndex = c;
                    return;
                }
                if (fallbackRow < 0) { fallbackRow = r; fallbackCol = c; }
            }
        }
        if (fallbackRow >= 0) { _focusedRowIndex = fallbackRow; _focusedColIndex = fallbackCol; return; }
        // ROM not found in any row – reset to safe defaults
        _focusedRowIndex = 0;
        _focusedColIndex = 0;
    }

    private void DeselectAllCards()
    {
        foreach (var row in _carouselRows)
        {
            foreach (var child in row.CardPanel.Children)
            {
                if (child is Border card)
                {
                    card.BorderBrush = null;
                    card.BorderThickness = new Thickness(0);
                    card.Background = CardDefaultBg;
                }
            }
        }
    }

    private void HighlightFocusedCard()
    {
        if (_focusedRowIndex < 0 || _focusedRowIndex >= _carouselRows.Count) return;
        var row = _carouselRows[_focusedRowIndex];
        if (_focusedColIndex < 0 || _focusedColIndex >= row.CardPanel.Children.Count) return;

        if (row.CardPanel.Children[_focusedColIndex] is Border card)
        {
            card.BorderBrush = CardSelectedBorder;
            card.BorderThickness = new Thickness(3);
            card.Background = CardHoverBg;
            card.BringIntoView();
        }
    }

    private void UpdateHeroSpotlight(RomInfo rom)
    {
        var loc = LocalizationManager.Instance;
        HeroSection.IsVisible = true;

        HeroTitle.Text = Path.GetFileNameWithoutExtension(rom.FileName);
        HeroSystemText.Text = rom.SystemName;

        // Set system badge color
        HeroSystemBadge.Background = GetSystemBrush(rom.System);

        int rating = AppSettings.Instance.GetRating(rom.FilePath);
        HeroRating.Text = rating > 0 ? FormatRatingStars(rating) : "";

        long playTimeSec = AppSettings.Instance.GetPlayTime(rom.FilePath);
        HeroPlayTime.Text = playTimeSec > 0 ? FormatPlayTime(playTimeSec) : "";

        int playCount = AppSettings.Instance.GetPlayCount(rom.FilePath);
        HeroPlayCount.Text = playCount > 0 ? string.Format(loc["BigPictureModern_Plays"], playCount) : "";

        HeroSize.Text = rom.FileSizeFormatted;
        HeroValid.Text = rom.IsValid ? loc["BigPicture_ValidRom"] : loc["BigPicture_InvalidRom"];

        // Favorite button
        bool isFav = AppSettings.Instance.IsFavorite(rom.FilePath);
        HeroFavoriteButton.Content = isFav ? "\u2605" : "\u2606";

        // Launch buttons
        bool canLaunch = RetroArchLauncher.IsSystemSupported(rom.System);
        HeroLaunchButton.IsEnabled = canLaunch;
        HeroLaunchMameButton.IsVisible = MameLauncher.IsSystemSupported(rom.System);
        HeroLaunchMednafenButton.IsVisible = MednafenLauncher.IsSystemSupported(rom.System);

        // Neo Geo BIOS notice
        if (rom.System == RomSystem.NeoGeo)
            StatusText.Text = loc["Common_NeoGeoBiosNotice"];

        // Box art placeholder
        string systemInitial = !string.IsNullOrEmpty(rom.SystemName)
            ? rom.SystemName[..1].ToUpperInvariant() : "?";
        HeroBoxArtPlaceholder.Text = systemInitial;

        // Try to show cached thumbnail in hero
        if (_thumbnailCache.TryGetValue(rom.FilePath, out var cached))
        {
            HeroBoxArt.Source = cached;
            HeroBgImage.Source = cached;
            HeroBoxArtPlaceholder.IsVisible = false;
        }
        else
        {
            HeroBoxArt.Source = null;
            HeroBgImage.Source = null;
            HeroBoxArtPlaceholder.IsVisible = true;
        }

        // Load full artwork async
        _ = LoadHeroArtworkAsync(rom);
    }

    private async Task LoadHeroArtworkAsync(RomInfo romInfo)
    {
        CancelArtworkLoading();
        ClearHeroExtraArtwork();
        _artworkCts = new CancellationTokenSource();
        var token = _artworkCts.Token;
        try
        {
            var artwork = await ArtworkService.FetchArtworkAsync(romInfo, progress: null, token);
            if (token.IsCancellationRequested) return;
            if (artwork.BoxArt != null)
            {
                var bmp = LoadBitmapFromBytes(artwork.BoxArt);
                if (bmp != null)
                {
                    var old = _heroBoxArtBitmap;
                    _heroBoxArtBitmap = bmp;
                    HeroBoxArt.Source = bmp;
                    HeroBgImage.Source = bmp;
                    HeroBoxArtPlaceholder.IsVisible = false;
                    old?.Dispose();
                }
            }
            // Cache snap and title screen for the artwork viewer
            if (artwork.Snap != null)
            {
                var old = _heroSnapBitmap;
                _heroSnapBitmap = LoadBitmapFromBytes(artwork.Snap);
                old?.Dispose();
            }
            if (artwork.TitleScreen != null)
            {
                var old = _heroTitleScreenBitmap;
                _heroTitleScreenBitmap = LoadBitmapFromBytes(artwork.TitleScreen);
                old?.Dispose();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[BigPictureModern] Hero artwork failed: {ex.Message}");
        }
    }

    private void ClearHeroExtraArtwork()
    {
        _heroBoxArtBitmap?.Dispose();
        _heroBoxArtBitmap = null;
        _heroSnapBitmap?.Dispose();
        _heroSnapBitmap = null;
        _heroTitleScreenBitmap?.Dispose();
        _heroTitleScreenBitmap = null;
    }

    private void ClearHeroSection()
    {
        HeroSection.IsVisible = false;
        HeroTitle.Text = "";
        HeroSystemText.Text = "";
        HeroRating.Text = "";
        HeroPlayTime.Text = "";
        HeroPlayCount.Text = "";
        HeroSize.Text = "";
        HeroValid.Text = "";
        ClearHeroArtwork();
        ClearHeroExtraArtwork();
    }

    private void ClearHeroArtwork()
    {
        HeroBoxArt.Source = null;
        HeroBgImage.Source = null;
    }

    // ══════════════════════════════════════════════════════════════
    //  Navigation (Keyboard + Gamepad)
    // ══════════════════════════════════════════════════════════════

    private void NavigateHorizontal(int delta)
    {
        if (_galleryActive) StopGalleryMode();
        if (_carouselRows.Count == 0) return;

        var row = _carouselRows[_focusedRowIndex];
        if (row.Roms.Count == 0) return;
        int newCol = _focusedColIndex + delta;
        if (newCol < 0) newCol = 0;
        if (newCol >= row.Roms.Count) newCol = row.Roms.Count - 1;
        if (newCol == _focusedColIndex) return;

        DeselectAllCards();
        _focusedColIndex = newCol;

        var rom = row.Roms[_focusedColIndex];
        int idx = _filteredRoms.FindIndex(r =>
            string.Equals(r.FilePath, rom.FilePath, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0) _selectedIndex = idx;

        HighlightFocusedCard();
        ScrollToRow(_focusedRowIndex);
        UpdateHeroSpotlight(rom);
    }

    private void NavigateVertical(int delta)
    {
        if (_galleryActive) StopGalleryMode();
        if (_carouselRows.Count == 0) return;

        int newRow = _focusedRowIndex + delta;
        if (newRow < 0) newRow = 0;
        if (newRow >= _carouselRows.Count) newRow = _carouselRows.Count - 1;
        if (newRow == _focusedRowIndex) return;

        DeselectAllCards();
        _focusedRowIndex = newRow;

        var row = _carouselRows[_focusedRowIndex];
        if (row.Roms.Count == 0) return;
        _focusedColIndex = Math.Min(_focusedColIndex, row.Roms.Count - 1);
        if (_focusedColIndex < 0) _focusedColIndex = 0;

        var rom = row.Roms[_focusedColIndex];
        int idx = _filteredRoms.FindIndex(r =>
            string.Equals(r.FilePath, rom.FilePath, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0) _selectedIndex = idx;

        HighlightFocusedCard();
        UpdateHeroSpotlight(rom);

        // Scroll the row into view
        ScrollToRow(_focusedRowIndex);
    }

    private void ScrollToRow(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= CarouselContainer.Children.Count) return;
        if (CarouselContainer.Children[rowIndex] is Border rowBorder)
            rowBorder.BringIntoView();
    }

    private void ScrollToRecentlyPlayedRow()
    {
        for (int i = 0; i < _carouselRows.Count; i++)
        {
            if (_carouselRows[i].Kind == CarouselRowKind.RecentlyPlayed)
            {
                var row = _carouselRows[i];
                if (row.Roms.Count > 0)
                {
                    // Navigate to and select the first ROM in the Recently Played row
                    DeselectAllCards();
                    _focusedRowIndex = i;
                    _focusedColIndex = 0;
                    var rom = row.Roms[0];
                    int idx = _filteredRoms.FindIndex(r =>
                        string.Equals(r.FilePath, rom.FilePath, StringComparison.OrdinalIgnoreCase));
                    if (idx >= 0) _selectedIndex = idx;
                    HighlightFocusedCard();
                    ScrollToRow(i);
                    UpdateHeroSpotlight(rom);
                }
                return;
            }
        }
        ScrollToRow(0);
    }

    private void SelectFirstGame()
    {
        if (_filteredRoms.Count > 0)
            SelectGameByFilteredIndex(0);
    }

    private void SelectLastGame()
    {
        if (_filteredRoms.Count > 0)
            SelectGameByFilteredIndex(_filteredRoms.Count - 1);
    }

    // ══════════════════════════════════════════════════════════════
    //  Actions (Launch, Favorite, Rate, etc.)
    // ══════════════════════════════════════════════════════════════

    private void LaunchSelectedRom()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _filteredRoms.Count) return;
        var rom = _filteredRoms[_selectedIndex];
        var loc = LocalizationManager.Instance;
        if (!RetroArchLauncher.IsSystemSupported(rom.System))
        {
            StatusText.Text = string.Format(loc["BigPicture_SystemNotSupported"], rom.SystemName);
            return;
        }
        StatusText.Text = string.Format(loc["BigPicture_LaunchingRom"],
            rom.FileName, RetroArchLauncher.GetCoreDisplayName(rom.System));
        var result = RetroArchLauncher.Launch(rom.FilePath, rom.System);
        StatusText.Text = result.Message;
        if (result.Success)
        {
            if (rom.System == RomSystem.NeoGeo)
                StatusText.Text = string.Format(loc["Common_NeoGeoBiosNoticeWithSeparator"],
                    result.Message, loc["Common_NeoGeoBiosNotice"]);
            HandleSuccessfulLaunch(rom, result.Process);
        }
    }

    private void LaunchButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => LaunchSelectedRom();

    private void LaunchMameButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => LaunchSelectedWithMame();

    private void LaunchSelectedWithMame()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _filteredRoms.Count) return;
        var rom = _filteredRoms[_selectedIndex];
        var loc = LocalizationManager.Instance;
        if (!MameLauncher.IsSystemSupported(rom.System))
        {
            StatusText.Text = string.Format(loc["Browser_MameSystemNotSupported"], rom.SystemName);
            return;
        }
        StatusText.Text = string.Format(loc["Browser_LaunchingMame"], rom.FileName);
        var result = MameLauncher.Launch(rom.FilePath, rom.System);
        StatusText.Text = result.Message;
        if (result.Success) HandleSuccessfulLaunch(rom, result.Process);
    }

    private void LaunchMednafenButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => LaunchSelectedWithMednafen();

    private void LaunchSelectedWithMednafen()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _filteredRoms.Count) return;
        var rom = _filteredRoms[_selectedIndex];
        var loc = LocalizationManager.Instance;
        if (!MednafenLauncher.IsSystemSupported(rom.System))
        {
            StatusText.Text = string.Format(loc["Browser_MednafenSystemNotSupported"], rom.SystemName);
            return;
        }
        StatusText.Text = string.Format(loc["Browser_LaunchingMednafen"], rom.FileName);
        var result = MednafenLauncher.Launch(rom.FilePath, rom.System);
        StatusText.Text = result.Message;
        if (result.Success) HandleSuccessfulLaunch(rom, result.Process);
    }

    private void HandleSuccessfulLaunch(RomInfo rom, System.Diagnostics.Process? process)
    {
        DiscordRichPresence.UpdatePresence(rom.FileName, rom.System);
        if (AppSettings.Instance.BigPicturePlayTrackingEnabled)
        {
            AppSettings.Instance.RecordRecentlyPlayed(rom.FilePath);
            AppSettings.Instance.IncrementPlayCount(rom.FilePath);
            var loc = LocalizationManager.Instance;
            int playCount = AppSettings.Instance.GetPlayCount(rom.FilePath);
            HeroPlayCount.Text = string.Format(loc["BigPictureModern_Plays"], playCount);
        }
        if (process != null)
        {
            if (AppSettings.Instance.MinimizeToTrayOnLaunch)
                MinimizeToTrayAndRestoreOnExit(process, rom.FilePath);
            else
                process.Dispose();
        }
    }

    private async void MinimizeToTrayAndRestoreOnExit(System.Diagnostics.Process process, string romFilePath)
    {
        try
        {
            if (TopLevel.GetTopLevel(this) is MainWindow mainWindow)
            {
                mainWindow.MinimizeToTray();
                var startTime = DateTime.UtcNow;
                await Task.Run(() =>
                {
                    try { process.WaitForExit(); }
                    catch (InvalidOperationException ex)
                    {
                        System.Diagnostics.Trace.WriteLine($"[BigPictureModern] Process monitoring ended: {ex.Message}");
                    }
                }).ConfigureAwait(false);
                var elapsed = (long)(DateTime.UtcNow - startTime).TotalSeconds;
                if (AppSettings.Instance.BigPicturePlayTrackingEnabled)
                    AppSettings.Instance.AddPlayTime(romFilePath, elapsed);
                Dispatcher.UIThread.Post(() =>
                {
                    mainWindow.RestoreFromTray();
                    DiscordRichPresence.ClearPresence();
                    if (_selectedIndex >= 0 && _selectedIndex < _filteredRoms.Count &&
                        string.Equals(_filteredRoms[_selectedIndex].FilePath, romFilePath, StringComparison.OrdinalIgnoreCase))
                    {
                        long playTimeSec = AppSettings.Instance.GetPlayTime(romFilePath);
                        HeroPlayTime.Text = FormatPlayTime(playTimeSec);
                    }
                    // Refresh carousel rows so Recently Played and Most Played reflect the new session
                    ApplyFilterAndSort();
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[BigPictureModern] Minimize-to-tray failed: {ex.Message}");
        }
        finally { process.Dispose(); }
    }

    private void FavoriteButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ToggleSelectedFavorite();

    private void InfoButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ToggleRomInfoOverlay();

    private void ToggleSelectedFavorite()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _filteredRoms.Count) return;
        var rom = _filteredRoms[_selectedIndex];
        if (AppSettings.Instance.IsFavorite(rom.FilePath))
            AppSettings.Instance.RemoveFavorite(rom.FilePath);
        else
            AppSettings.Instance.AddFavorite(rom.FilePath);
        bool isFav = AppSettings.Instance.IsFavorite(rom.FilePath);
        HeroFavoriteButton.Content = isFav ? "\u2605" : "\u2606";
        // Rebuild carousels so the Favorites row reflects the change
        ApplyFilterAndSort();
    }

    private void RandomGameButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => SelectRandomGame();

    private void SelectRandomGame()
    {
        if (_filteredRoms.Count == 0) return;
        int randomIndex;
        if (_filteredRoms.Count == 1) { randomIndex = 0; }
        else { do { randomIndex = Random.Shared.Next(_filteredRoms.Count); } while (randomIndex == _selectedIndex); }
        SelectGameByFilteredIndex(randomIndex);
        Focus();
    }

    private void RescanButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentFolder)) return;
        ScanCurrentFolderFireAndForget();
    }

    private async void BrowseFolderButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = LocalizationManager.Instance["BigPicture_SelectRomFolder"],
                AllowMultiple = false
            });
            if (folders.Count == 0) return;
            _currentFolder = folders[0].Path.LocalPath;
            AppSettings.Instance.BigPictureRomFolder = _currentFolder;
            await ScanCurrentFolderAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[BigPictureModern] BrowseFolder failed: {ex.Message}");
        }
    }

    private void ExitBigPictureButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ExitBigPictureMode();

    private void ExitBigPictureMode()
    {
        StopGalleryMode();
        if (_screensaverActive) DismissScreensaver();
        StopInactivityTimer();
        if (TopLevel.GetTopLevel(this) is MainWindow mainWindow)
            mainWindow.ExitBigPictureMode();
    }

    private static void OpenFileLocation(string filePath)
    {
        try
        {
            string? directory = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return;
            if (OperatingSystem.IsWindows())
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    ArgumentList = { "/select,", filePath },
                    UseShellExecute = true
                })?.Dispose();
            }
            else if (OperatingSystem.IsMacOS())
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "open",
                    ArgumentList = { "-R", filePath }
                })?.Dispose();
            }
            else
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "xdg-open",
                    ArgumentList = { directory }
                })?.Dispose();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[BigPictureModern] OpenFileLocation failed: {ex.Message}");
        }
    }

    private async Task CopyToClipboardAsync(string text)
    {
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(text);
                StatusText.Text = LocalizationManager.Instance["BigPicture_CopiedToClipboard"];
            }
        }
        catch (Exception ex)
        {
            // Clipboard access can fail on some platforms (e.g. Wayland without focus)
            System.Diagnostics.Trace.WriteLine($"[BigPictureModern] Clipboard write failed: {ex.Message}");
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  Zoom
    // ══════════════════════════════════════════════════════════════

    private void ZoomIn()
    {
        double newScale = Math.Min(_cardScale + ScaleStep, ScaleMax);
        if (Math.Abs(newScale - _cardScale) < 0.001) return;
        _cardScale = Math.Round(newScale, 1);
        AppSettings.Instance.BigPictureCardScale = _cardScale;
        UpdateZoomLevelText();
        RebuildCarousels();
    }

    private void ZoomOut()
    {
        double newScale = Math.Max(_cardScale - ScaleStep, ScaleMin);
        if (Math.Abs(newScale - _cardScale) < 0.001) return;
        _cardScale = Math.Round(newScale, 1);
        AppSettings.Instance.BigPictureCardScale = _cardScale;
        UpdateZoomLevelText();
        RebuildCarousels();
    }

    private void UpdateZoomLevelText()
    {
        int pct = (int)Math.Round(_cardScale * 100);
        ZoomLevelText.Text = string.Format(LocalizationManager.Instance["BigPicture_ZoomLevel"], pct);
    }

    // ══════════════════════════════════════════════════════════════
    //  Help Overlay
    // ══════════════════════════════════════════════════════════════

    private void PopulateHelpOverlay()
    {
        var loc = LocalizationManager.Instance;
        HelpOverlayTitle.Text = loc["BigPicture_HelpTitle"];
        HelpNav.Text = loc["BigPicture_HelpNavigate"];
        HelpLaunch.Text = loc["BigPicture_HelpLaunch"];
        HelpFav.Text = loc["BigPicture_HelpFavorite"];
        HelpSearch.Text = loc["BigPicture_HelpSearch"];
        HelpExit.Text = loc["BigPicture_HelpExit"];
        HelpHomeEnd.Text = loc["BigPicture_HelpHomeEnd"];
        HelpPage.Text = loc["BigPicture_HelpPage"];
        HelpZoom.Text = loc["BigPicture_HelpZoom"];
        HelpToggle.Text = loc["BigPicture_HelpToggleHelp"];
        HelpRomInfo.Text = loc["BigPicture_HelpRomInfo"];
        HelpRandom.Text = loc["BigPicture_HelpRandom"];
        HelpLetterJump.Text = loc["BigPicture_HelpLetterJump"];
        HelpRecentlyPlayed.Text = loc["BigPicture_HelpRecentlyPlayed"];
        HelpStats.Text = loc["BigPicture_HelpStats"];
        HelpSystemCycle.Text = loc["BigPicture_HelpSystemCycle"];
        HelpGallery.Text = loc["BigPicture_HelpGallery"];
        HelpArtworkViewer.Text = loc["BigPicture_HelpArtworkViewer"];
        HelpRating.Text = loc["BigPicture_HelpRating"];
        HelpPlayTime.Text = loc["BigPicture_HelpPlayTime"];
        HelpScreensaver.Text = loc["BigPicture_HelpScreensaver"];
        HelpDismiss.Text = loc["BigPicture_HelpDismiss"];
        UpdateHelpOverlayGamepadVisibility();
    }

    private void UpdateHelpOverlayGamepadVisibility()
    {
        bool show = GamepadService.Instance.IsAvailable;
        HelpGamepadHeader.IsVisible = show;
        HelpGamepadNav.IsVisible = show;
        HelpGamepadLaunch.IsVisible = show;
        HelpGamepadFav.IsVisible = show;
        HelpGamepadSearch.IsVisible = show;
        HelpGamepadExit.IsVisible = show;
        HelpGamepadHomeEnd.IsVisible = show;
        HelpGamepadPage.IsVisible = show;
        HelpGamepadZoom.IsVisible = show;
        HelpGamepadToggle.IsVisible = show;
        HelpGamepadRomInfo.IsVisible = show;
        HelpGamepadRandom.IsVisible = show;
        HelpGamepadRecentlyPlayed.IsVisible = show;
        HelpGamepadStats.IsVisible = show;
        HelpGamepadSystemCycle.IsVisible = show;
        HelpGamepadGallery.IsVisible = show;
        HelpGamepadArtworkViewer.IsVisible = show;
        HelpGamepadRating.IsVisible = show;
    }

    private void ToggleHelpOverlay()
    {
        if (!HelpOverlay.IsVisible) StopGalleryMode();
        HelpOverlay.IsVisible = !HelpOverlay.IsVisible;
    }

    // ══════════════════════════════════════════════════════════════
    //  ROM Info Overlay
    // ══════════════════════════════════════════════════════════════

    private void ToggleRomInfoOverlay()
    {
        if (RomInfoOverlay.IsVisible) { DismissRomInfoOverlay(); return; }
        if (_selectedIndex < 0 || _selectedIndex >= _filteredRoms.Count) return;
        StopGalleryMode();
        PopulateRomInfoOverlay(_filteredRoms[_selectedIndex]);
        RomInfoOverlay.IsVisible = true;
    }

    private void DismissRomInfoOverlay()
    {
        RomInfoOverlay.IsVisible = false;
        _checksumCts?.Cancel(); _checksumCts?.Dispose(); _checksumCts = null;
    }

    private void PopulateRomInfoOverlay(RomInfo rom)
    {
        var loc = LocalizationManager.Instance;
        RomInfoOverlayTitle.Text = loc["BigPicture_RomInfoTitle"];
        RomInfoDismiss.Text = loc["BigPicture_RomInfoDismiss"];

        RomInfoDetailsSection.Children.Clear();
        AddSectionHeader(RomInfoDetailsSection, loc["BigPicture_RomInfoDetails"]);
        AddInfoRow(RomInfoDetailsSection, loc["BigPicture_RomInfoFileName"], Path.GetFileNameWithoutExtension(rom.FileName));
        AddInfoRow(RomInfoDetailsSection, loc["BigPicture_RomInfoSystem"], rom.SystemName);
        AddInfoRow(RomInfoDetailsSection, loc["BigPicture_RomInfoFileSize"], rom.FileSizeFormatted);
        AddInfoRow(RomInfoDetailsSection, loc["BigPicture_RomInfoStatus"],
            rom.IsValid ? loc["BigPicture_ValidRom"] : loc["BigPicture_InvalidRom"]);
        if (rom.HeaderInfo?.Count > 0)
            foreach (var kvp in rom.HeaderInfo) AddInfoRow(RomInfoDetailsSection, kvp.Key, kvp.Value);
        long pt = AppSettings.Instance.GetPlayTime(rom.FilePath);
        if (pt > 0) AddInfoRow(RomInfoDetailsSection, loc["BigPicture_PlayTimeLabel"], FormatPlayTime(pt));
        int rating = AppSettings.Instance.GetRating(rom.FilePath);
        AddInfoRow(RomInfoDetailsSection, loc["BigPicture_RatingLabel"],
            rating > 0 ? string.Format(loc["BigPicture_RatingDisplay"], FormatRatingStars(rating), rating) : loc["BigPicture_Unrated"]);

        // Checksums
        RomInfoChecksumsSection.Children.Clear();
        AddSectionHeader(RomInfoChecksumsSection, loc["BigPicture_RomInfoChecksums"]);
        string computing = loc["BigPicture_RomInfoComputing"];
        var crc32Text = AddInfoRow(RomInfoChecksumsSection, "CRC32", computing);
        var md5Text = AddInfoRow(RomInfoChecksumsSection, "MD5", computing);
        var sha1Text = AddInfoRow(RomInfoChecksumsSection, "SHA-1", computing);
        var sha256Text = AddInfoRow(RomInfoChecksumsSection, "SHA-256", computing);
        _checksumCts?.Cancel(); _checksumCts?.Dispose();
        _checksumCts = new CancellationTokenSource();
        var token = _checksumCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var result = await ChecksumCalculator.CalculateAsync(rom.FilePath).ConfigureAwait(false);
                if (token.IsCancellationRequested) return;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (token.IsCancellationRequested) return;
                    crc32Text.Text = result.CRC32; md5Text.Text = result.MD5;
                    sha1Text.Text = result.SHA1; sha256Text.Text = result.SHA256;
                });
            }
            catch (Exception) when (token.IsCancellationRequested) { }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FileNotFoundException)
            {
                string error = loc["BigPicture_RomInfoChecksumError"];
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (token.IsCancellationRequested) return;
                    crc32Text.Text = error; md5Text.Text = error; sha1Text.Text = error; sha256Text.Text = error;
                });
            }
        }, token);

        // GoodTools
        RomInfoGoodToolsSection.Children.Clear();
        AddSectionHeader(RomInfoGoodToolsSection, loc["BigPicture_RomInfoGoodTools"]);
        var gtResult = GoodToolsIdentifier.Identify(rom.FileName);
        if (!gtResult.HasCodes)
        {
            AddInfoRow(RomInfoGoodToolsSection, "", loc["BigPicture_RomInfoNoGoodTools"]);
        }
        else
        {
            if (gtResult.CountryCodes.Count > 0)
            {
                AddSubHeader(RomInfoGoodToolsSection, loc["BigPicture_RomInfoCountryCodes"]);
                foreach (var code in gtResult.CountryCodes)
                    AddInfoRow(RomInfoGoodToolsSection, $"({code.Code})", code.Description);
            }
            if (gtResult.StandardCodes.Count > 0)
            {
                AddSubHeader(RomInfoGoodToolsSection, loc["BigPicture_RomInfoStandardCodes"]);
                foreach (var code in gtResult.StandardCodes)
                    AddInfoRow(RomInfoGoodToolsSection, code.InParentheses ? $"({code.Code})" : $"[{code.Code}]", code.Description);
            }
            if (gtResult.GoodGenCodes.Count > 0)
            {
                AddSubHeader(RomInfoGoodToolsSection, loc["BigPicture_RomInfoGoodGenCodes"]);
                foreach (var code in gtResult.GoodGenCodes)
                    AddInfoRow(RomInfoGoodToolsSection, $"({code.Code})", code.Description);
            }
        }
    }

    private static void AddSectionHeader(StackPanel parent, string text) =>
        parent.Children.Add(new TextBlock { Text = text, FontSize = 16, FontWeight = FontWeight.SemiBold, Foreground = SectionHeaderFg, Margin = new Thickness(0, 4) });

    private static void AddSubHeader(StackPanel parent, string text) =>
        parent.Children.Add(new TextBlock { Text = text, FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = SubHeaderFg, Margin = new Thickness(0, 4, 0, 2) });

    private static TextBlock AddInfoRow(StackPanel parent, string label, string value)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        if (!string.IsNullOrEmpty(label))
            panel.Children.Add(new TextBlock { Text = label, FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = LabelFg });
        var valueBlock = new TextBlock { Text = value, FontSize = 13, Foreground = ValueFg, TextWrapping = TextWrapping.Wrap };
        panel.Children.Add(valueBlock);
        parent.Children.Add(panel);
        return valueBlock;
    }

    // ══════════════════════════════════════════════════════════════
    //  Stats Overlay
    // ══════════════════════════════════════════════════════════════

    private void ToggleStatsOverlay()
    {
        if (StatsOverlay.IsVisible) { DismissStatsOverlay(); return; }
        StopGalleryMode();
        PopulateStatsOverlay();
        StatsOverlay.IsVisible = true;
    }

    private void DismissStatsOverlay() => StatsOverlay.IsVisible = false;

    private void PopulateStatsOverlay()
    {
        var loc = LocalizationManager.Instance;
        StatsOverlayTitle.Text = loc["BigPicture_StatsTitle"];
        StatsDismiss.Text = loc["BigPicture_StatsDismiss"];
        StatsContent.Children.Clear();

        AddStatsRow(StatsContent, loc["BigPicture_StatsTotalRoms"], _allRoms.Count.ToString("N0"));
        if (_filteredRoms.Count != _allRoms.Count)
            AddStatsRow(StatsContent, loc["BigPicture_StatsFilteredRoms"], _filteredRoms.Count.ToString("N0"));

        var favorites = AppSettings.Instance.Favorites;
        int favCount = _allRoms.Count(r => favorites.Contains(r.FilePath));
        AddStatsRow(StatsContent, loc["BigPicture_StatsFavorites"], favCount.ToString("N0"));

        long totalSize = _allRoms.Sum(r => r.FileSize);
        AddStatsRow(StatsContent, loc["BigPicture_StatsTotalSize"], FormatFileSize(totalSize));
        if (_allRoms.Count > 0)
            AddStatsRow(StatsContent, loc["BigPicture_StatsAvgSize"], FormatFileSize(totalSize / _allRoms.Count));

        long totalPlayTime = 0; int ratedCount = 0; double ratingSum = 0;
        foreach (var r in _allRoms)
        {
            totalPlayTime += AppSettings.Instance.GetPlayTime(r.FilePath);
            int rt = AppSettings.Instance.GetRating(r.FilePath);
            if (rt > 0) { ratedCount++; ratingSum += rt; }
        }
        AddStatsRow(StatsContent, loc["BigPicture_StatsTotalPlayTime"], FormatPlayTime(totalPlayTime));
        if (ratedCount > 0)
            AddStatsRow(StatsContent, loc["BigPicture_StatsAvgRating"],
                $"{(ratingSum / ratedCount):F1} / 5  ({ratedCount} {loc["BigPicture_StatsRated"]})");

        StatsContent.Children.Add(new Border { Background = OverlayBorderBrush, Height = 1, Margin = new Thickness(0, 8) });
        StatsContent.Children.Add(new TextBlock { Text = loc["BigPicture_StatsPerSystem"], FontSize = 16, FontWeight = FontWeight.SemiBold, Foreground = SectionHeaderFg, Margin = new Thickness(0, 4) });

        foreach (var group in _allRoms.GroupBy(r => r.SystemName).OrderByDescending(g => g.Count()))
        {
            long gs = group.Sum(r => r.FileSize);
            AddStatsRow(StatsContent, group.Key, $"{group.Count():N0}  ({FormatFileSize(gs)})");
        }
    }

    private static void AddStatsRow(StackPanel parent, string label, string value)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 2) };
        var lb = new TextBlock { Text = label, FontSize = 14, Foreground = LabelFg, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(lb, 0);
        var vb = new TextBlock { Text = value, FontSize = 14, FontWeight = FontWeight.SemiBold, Foreground = ValueFg, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(24, 0, 0, 0) };
        Grid.SetColumn(vb, 1);
        row.Children.Add(lb); row.Children.Add(vb);
        parent.Children.Add(row);
    }

    // ══════════════════════════════════════════════════════════════
    //  Artwork Preloading
    // ══════════════════════════════════════════════════════════════

    private void StartArtworkPreload(List<RomInfo> roms)
    {
        CancelArtworkPreload();
        ClearThumbnailCache();
        if (roms.Count == 0) return;
        _preloadCts = new CancellationTokenSource();
        PreloadArtworkFireAndForget(new List<RomInfo>(roms), _preloadCts.Token);
    }

    private async void PreloadArtworkFireAndForget(List<RomInfo> roms, CancellationToken ct)
    {
        try { await PreloadArtworkAsync(roms, ct); }
        catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"[BigPictureModern] Preload failed: {ex.Message}"); }
    }

    private async Task PreloadArtworkAsync(List<RomInfo> roms, CancellationToken ct)
    {
        var loc = LocalizationManager.Instance;
        int completed = 0, total = roms.Count;
        using var sem = new SemaphoreSlim(PreloadMaxConcurrency);
        try
        {
            var tasks = roms.Select(async rom =>
            {
                await sem.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    var artwork = await ArtworkService.FetchArtworkAsync(rom, progress: null, ct).ConfigureAwait(false);
                    if (artwork.BoxArt != null)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            if (ct.IsCancellationRequested) return;
                            var bitmap = LoadBitmapFromBytes(artwork.BoxArt);
                            if (bitmap != null)
                            {
                                Bitmap? old = _thumbnailCache.TryGetValue(rom.FilePath, out var prev) ? prev : null;
                                _thumbnailCache[rom.FilePath] = bitmap;
                                if (_cardImageMap.TryGetValue(rom.FilePath, out var images))
                                {
                                    foreach (var img in images) { img.Source = bitmap; img.IsVisible = true; }
                                }
                                // Update hero if this is the selected ROM
                                if (_selectedIndex >= 0 && _selectedIndex < _filteredRoms.Count &&
                                    string.Equals(_filteredRoms[_selectedIndex].FilePath, rom.FilePath, StringComparison.OrdinalIgnoreCase))
                                {
                                    HeroBoxArt.Source = bitmap;
                                    HeroBgImage.Source = bitmap;
                                    HeroBoxArtPlaceholder.IsVisible = false;
                                }
                                old?.Dispose();
                            }
                        });
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"[BigPictureModern] Preload {rom.FileName}: {ex.Message}"); }
                finally
                {
                    sem.Release();
                    int done = Interlocked.Increment(ref completed);
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (!ct.IsCancellationRequested)
                            HintText.Text = string.Format(loc["BigPicture_PreloadProgress"], done, total);
                    });
                }
            });
            await Task.WhenAll(tasks).ConfigureAwait(false);
            Dispatcher.UIThread.Post(() =>
            {
                if (!ct.IsCancellationRequested)
                {
                    HintText.Text = loc["BigPicture_PreloadComplete"];
                    RestoreHintTextAfterDelay(ct);
                }
            });
        }
        catch (OperationCanceledException) { }
    }

    private void CancelArtworkPreload() { _preloadCts?.Cancel(); _preloadCts?.Dispose(); _preloadCts = null; }
    private void CancelArtworkLoading() { _artworkCts?.Cancel(); _artworkCts?.Dispose(); _artworkCts = null; }

    private async void RestoreHintTextAfterDelay(CancellationToken ct)
    {
        try { await Task.Delay(3000, ct); if (!ct.IsCancellationRequested) UpdateHintText(); }
        catch (OperationCanceledException) { }
    }

    private void ClearThumbnailCache()
    {
        foreach (var images in _cardImageMap.Values)
            foreach (var img in images) img.Source = null;
        foreach (var bitmap in _thumbnailCache.Values) bitmap.Dispose();
        _thumbnailCache.Clear();
    }

    private static Bitmap? LoadBitmapFromBytes(byte[] data)
    {
        if (data.Length == 0) return null;
        try { using var stream = new MemoryStream(data); return new Bitmap(stream); }
        catch (Exception ex) when (ex is InvalidOperationException or IOException) { return null; }
    }

    // ══════════════════════════════════════════════════════════════
    //  Screensaver / Attract Mode
    // ══════════════════════════════════════════════════════════════

    private void InitialiseInactivityTimer()
    {
        int timeout = AppSettings.Instance.BigPictureScreensaverTimeout;
        if (timeout <= 0) return;
        _inactivityTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMinutes(timeout) };
        _inactivityTimer.Tick += OnInactivityTick;
        _inactivityTimer.Start();
    }

    private void ResetInactivityTimer()
    {
        if (_inactivityTimer is not { IsEnabled: true }) return;
        _inactivityTimer.Stop(); _inactivityTimer.Start();
    }

    private void StopInactivityTimer()
    {
        if (_inactivityTimer != null) { _inactivityTimer.Stop(); _inactivityTimer.Tick -= OnInactivityTick; _inactivityTimer = null; }
    }

    private void OnInactivityTick(object? sender, EventArgs e) => StartScreensaver();

    private void StartScreensaver()
    {
        if (_screensaverActive || _allRoms.Count == 0) return;
        _screensaverActive = true;
        StopGalleryMode();
        if (HelpOverlay.IsVisible) HelpOverlay.IsVisible = false;
        if (RomInfoOverlay.IsVisible) DismissRomInfoOverlay();
        if (StatsOverlay.IsVisible) DismissStatsOverlay();
        if (ArtworkViewerOverlay.IsVisible) DismissArtworkViewer();
        LetterJumpOverlay.IsVisible = false;
        SystemCycleOverlay.IsVisible = false;
        _letterJumpCts?.Cancel(); _systemCycleCts?.Cancel();
        StopInactivityTimer();
        ScreensaverDismiss.Text = LocalizationManager.Instance["BigPicture_ScreensaverDismiss"];
        ScreensaverOverlay.IsVisible = true;
        CycleScreensaverArtwork();
        StopScreensaverCycleTimer();
        _screensaverCycleTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(ScreensaverCycleMs) };
        _screensaverCycleTimer.Tick += OnScreensaverCycleTick;
        _screensaverCycleTimer.Start();
    }

    private void CycleScreensaverArtwork()
    {
        if (_allRoms.Count == 0) return;
        int index;
        if (_allRoms.Count == 1) index = 0;
        else { do { index = Random.Shared.Next(_allRoms.Count); } while (index == _lastScreensaverRomIndex); }
        _lastScreensaverRomIndex = index;
        var rom = _allRoms[index];
        ScreensaverTitle.Text = Path.GetFileNameWithoutExtension(rom.FileName);
        ScreensaverSystem.Text = rom.SystemName;
        ScreensaverImage.Source = _thumbnailCache.TryGetValue(rom.FilePath, out var bmp) ? bmp : null;
    }

    private void DismissScreensaver()
    {
        if (!_screensaverActive) return;
        _screensaverActive = false; _lastScreensaverRomIndex = -1;
        StopScreensaverCycleTimer();
        ScreensaverOverlay.IsVisible = false;
        ScreensaverImage.Source = null;
        InitialiseInactivityTimer();
    }

    private void StopScreensaverCycleTimer()
    {
        if (_screensaverCycleTimer != null) { _screensaverCycleTimer.Stop(); _screensaverCycleTimer.Tick -= OnScreensaverCycleTick; _screensaverCycleTimer = null; }
    }

    private void OnScreensaverCycleTick(object? sender, EventArgs e) => CycleScreensaverArtwork();

    // ══════════════════════════════════════════════════════════════
    //  Letter Jump
    // ══════════════════════════════════════════════════════════════

    private void JumpToLetter(char letter)
    {
        if (_filteredRoms.Count == 0) return;
        string prefix = letter.ToString();
        int index = _filteredRoms.FindIndex(r =>
            Path.GetFileNameWithoutExtension(r.FileName).StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        bool found = index >= 0;
        if (found) SelectGameByFilteredIndex(index);
        ShowLetterJumpIndicator(letter, found);
    }

    private async void ShowLetterJumpIndicator(char letter, bool matched)
    {
        try
        {
            _letterJumpCts?.Cancel(); _letterJumpCts?.Dispose();
            _letterJumpCts = new CancellationTokenSource();
            var token = _letterJumpCts.Token;
            LetterJumpText.Text = letter.ToString();
            LetterJumpText.Opacity = matched ? 1.0 : 0.35;
            LetterJumpOverlay.IsVisible = true;
            await Task.Delay(LetterJumpDisplayMs, token);
            if (!token.IsCancellationRequested) LetterJumpOverlay.IsVisible = false;
        }
        catch (OperationCanceledException) { }
    }

    // ══════════════════════════════════════════════════════════════
    //  System Cycle
    // ══════════════════════════════════════════════════════════════

    private void CycleSystemFilter(int direction)
    {
        if (SystemFilterCombo.Items.Count == 0) return;
        StopGalleryMode();
        int count = SystemFilterCombo.Items.Count;
        SystemFilterCombo.SelectedIndex = (SystemFilterCombo.SelectedIndex + direction + count) % count;
        string name = (SystemFilterCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        ShowSystemCycleIndicator(name);
    }

    private async void ShowSystemCycleIndicator(string name)
    {
        try
        {
            _systemCycleCts?.Cancel(); _systemCycleCts?.Dispose();
            _systemCycleCts = new CancellationTokenSource();
            var token = _systemCycleCts.Token;
            SystemCycleText.Text = name;
            SystemCycleOverlay.IsVisible = true;
            await Task.Delay(SystemCycleDisplayMs, token);
            if (!token.IsCancellationRequested) SystemCycleOverlay.IsVisible = false;
        }
        catch (OperationCanceledException) { }
    }

    // ══════════════════════════════════════════════════════════════
    //  Gallery Mode
    // ══════════════════════════════════════════════════════════════

    private void ToggleGalleryMode()
    {
        if (_galleryActive) StopGalleryMode(); else StartGalleryMode();
    }

    private void StartGalleryMode()
    {
        if (_filteredRoms.Count == 0) return;
        if (_galleryTimer != null) { _galleryTimer.Stop(); _galleryTimer.Tick -= OnGalleryTick; _galleryTimer = null; }
        _galleryActive = true;
        StatusText.Text = LocalizationManager.Instance["BigPicture_GalleryActive"];
        _galleryTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(GalleryIntervalMs) };
        _galleryTimer.Tick += OnGalleryTick;
        _galleryTimer.Start();
    }

    private void StopGalleryMode()
    {
        if (!_galleryActive) return;
        _galleryActive = false;
        if (_galleryTimer != null) { _galleryTimer.Stop(); _galleryTimer.Tick -= OnGalleryTick; _galleryTimer = null; }
        StatusText.Text = LocalizationManager.Instance["BigPicture_GalleryStopped"];
    }

    private void OnGalleryTick(object? sender, EventArgs e) => GalleryAdvance();

    private void GalleryAdvance()
    {
        if (_filteredRoms.Count == 0) { StopGalleryMode(); return; }
        ResetInactivityTimer();
        int current = Math.Max(0, _selectedIndex);
        int next = (current + 1) % _filteredRoms.Count;
        SelectGameByFilteredIndex(next);
    }

    // ══════════════════════════════════════════════════════════════
    //  Artwork Viewer
    // ══════════════════════════════════════════════════════════════

    private void ToggleArtworkViewer()
    {
        if (ArtworkViewerOverlay.IsVisible) { DismissArtworkViewer(); return; }
        if (_selectedIndex < 0 || _selectedIndex >= _filteredRoms.Count) return;
        StopGalleryMode();
        OpenArtworkViewer(_filteredRoms[_selectedIndex]);
    }

    private void OpenArtworkViewer(RomInfo rom)
    {
        var loc = LocalizationManager.Instance;
        // Use cached thumbnails and hero artwork for the viewer
        var bmp = _thumbnailCache.TryGetValue(rom.FilePath, out var cached) ? cached : null;
        _artworkViewerImages[0] = bmp;
        _artworkViewerImages[1] = _heroSnapBitmap;
        _artworkViewerImages[2] = _heroTitleScreenBitmap;
        _artworkViewerLabels[0] = loc["Browser_BoxArt"];
        _artworkViewerLabels[1] = loc["Browser_Screenshot"];
        _artworkViewerLabels[2] = loc["Browser_TitleScreen"];
        // Require at least one image to open the viewer
        if (bmp == null && _heroSnapBitmap == null && _heroTitleScreenBitmap == null) return;
        ArtworkViewerTitle.Text = Path.GetFileNameWithoutExtension(rom.FileName);
        ArtworkViewerDismiss.Text = loc["BigPicture_ArtworkViewerDismiss"];
        // Start on the first available image
        _artworkViewerIndex = 0;
        if (_artworkViewerImages[0] == null)
        {
            for (int i = 1; i < ArtworkViewerImageCount; i++)
            {
                if (_artworkViewerImages[i] != null) { _artworkViewerIndex = i; break; }
            }
        }
        UpdateArtworkViewerDisplay();
        ArtworkViewerOverlay.IsVisible = true;
    }

    private void CycleArtworkViewer(int dir)
    {
        int next = _artworkViewerIndex;
        for (int i = 0; i < ArtworkViewerImageCount; i++)
        {
            next = (next + dir + ArtworkViewerImageCount) % ArtworkViewerImageCount;
            if (_artworkViewerImages[next] != null) break;
        }
        _artworkViewerIndex = next;
        UpdateArtworkViewerDisplay();
    }

    private void UpdateArtworkViewerDisplay()
    {
        ArtworkViewerImage.Source = _artworkViewerImages[_artworkViewerIndex];
        ArtworkViewerLabel.Text = _artworkViewerLabels[_artworkViewerIndex];
        int avail = 0, pos = 0;
        for (int i = 0; i < ArtworkViewerImageCount; i++)
        {
            if (_artworkViewerImages[i] != null) { avail++; if (i == _artworkViewerIndex) pos = avail; }
        }
        ArtworkViewerCounter.Text = string.Format(LocalizationManager.Instance["BigPicture_ArtworkCounter"], pos, avail);
    }

    private void DismissArtworkViewer()
    {
        ArtworkViewerOverlay.IsVisible = false;
        ArtworkViewerImage.Source = null;
        ClearArtworkViewerReferences();
    }

    private void ClearArtworkViewerReferences()
    {
        // The array references bitmaps owned by _thumbnailCache and _heroSnap/_heroTitleScreen,
        // so we must NOT dispose them here – just release the references.
        Array.Clear(_artworkViewerImages);
    }

    // ══════════════════════════════════════════════════════════════
    //  Rating
    // ══════════════════════════════════════════════════════════════

    private void RateSelectedGame(int rating)
    {
        if (!AppSettings.Instance.BigPictureRatingsEnabled) return;
        if (_selectedIndex < 0 || _selectedIndex >= _filteredRoms.Count) return;
        var rom = _filteredRoms[_selectedIndex];
        int current = AppSettings.Instance.GetRating(rom.FilePath);
        AppSettings.Instance.SetRating(rom.FilePath, current == rating ? 0 : rating);
        RefreshRatingDisplay(rom);
        // Rebuild carousels so the TopRated row and card stars reflect the change
        ApplyFilterAndSort();
    }

    private void CycleSelectedRating()
    {
        if (!AppSettings.Instance.BigPictureRatingsEnabled) return;
        if (_selectedIndex < 0 || _selectedIndex >= _filteredRoms.Count) return;
        var rom = _filteredRoms[_selectedIndex];
        int current = AppSettings.Instance.GetRating(rom.FilePath);
        AppSettings.Instance.SetRating(rom.FilePath, current >= 5 ? 0 : current + 1);
        RefreshRatingDisplay(rom);
        // Rebuild carousels so the TopRated row and card stars reflect the change
        ApplyFilterAndSort();
    }

    private void RefreshRatingDisplay(RomInfo rom)
    {
        int newRating = AppSettings.Instance.GetRating(rom.FilePath);
        HeroRating.Text = newRating > 0 ? FormatRatingStars(newRating) : "";
    }

    // ══════════════════════════════════════════════════════════════
    //  Keyboard Handler
    // ══════════════════════════════════════════════════════════════

    private void OnKeyDownHandler(object? sender, KeyEventArgs e)
    {
        ResetInactivityTimer();
        if (_screensaverActive) { DismissScreensaver(); e.Handled = true; return; }
        if (RomInfoOverlay.IsVisible) { if (e.Key is Key.I or Key.Escape) DismissRomInfoOverlay(); e.Handled = true; return; }
        if (HelpOverlay.IsVisible) { if (e.Key is Key.H or Key.OemQuestion or Key.Escape) HelpOverlay.IsVisible = false; e.Handled = true; return; }
        if (StatsOverlay.IsVisible) { if (e.Key is Key.C or Key.Escape) DismissStatsOverlay(); e.Handled = true; return; }
        if (ArtworkViewerOverlay.IsVisible)
        {
            if (e.Key is Key.V or Key.Escape) DismissArtworkViewer();
            else if (e.Key == Key.Left) CycleArtworkViewer(-1);
            else if (e.Key == Key.Right) CycleArtworkViewer(1);
            e.Handled = true;
            return;
        }
        if (SearchBox.IsFocused) { if (e.Key == Key.Escape) { Focus(); e.Handled = true; } return; }
        if (_galleryActive && e.Key is Key.Left or Key.Right or Key.Up or Key.Down
            or Key.Home or Key.End or Key.PageUp or Key.PageDown
            or Key.Escape or Key.Back or Key.Enter or Key.Space
            or Key.F or Key.R or Key.P or Key.V
            or Key.D0 or Key.D1 or Key.D2 or Key.D3 or Key.D4 or Key.D5
            or Key.NumPad0 or Key.NumPad1 or Key.NumPad2 or Key.NumPad3 or Key.NumPad4 or Key.NumPad5)
        {
            StopGalleryMode();
            e.Handled = true;
            return;
        }

        switch (e.Key)
        {
            case Key.Left: NavigateHorizontal(-1); e.Handled = true; break;
            case Key.Right: NavigateHorizontal(1); e.Handled = true; break;
            case Key.Up: NavigateVertical(-1); e.Handled = true; break;
            case Key.Down: NavigateVertical(1); e.Handled = true; break;
            case Key.Enter or Key.Space: LaunchSelectedRom(); e.Handled = true; break;
            case Key.Escape or Key.Back: ExitBigPictureMode(); e.Handled = true; break;
            case Key.Tab: SearchBox.Focus(); e.Handled = true; break;
            case Key.Home: SelectFirstGame(); e.Handled = true; break;
            case Key.End: SelectLastGame(); e.Handled = true; break;
            case Key.PageUp: NavigateVertical(-3); e.Handled = true; break;
            case Key.PageDown: NavigateVertical(3); e.Handled = true; break;
            case Key.F: ToggleSelectedFavorite(); e.Handled = true; break;
            case Key.OemPlus or Key.Add: ZoomIn(); e.Handled = true; break;
            case Key.OemMinus or Key.Subtract: ZoomOut(); e.Handled = true; break;
            case Key.H or Key.OemQuestion: ToggleHelpOverlay(); e.Handled = true; break;
            case Key.I: ToggleRomInfoOverlay(); e.Handled = true; break;
            case Key.R: SelectRandomGame(); e.Handled = true; break;
            case Key.P: ScrollToRecentlyPlayedRow(); e.Handled = true; break;
            case Key.C: ToggleStatsOverlay(); e.Handled = true; break;
            case Key.OemOpenBrackets: CycleSystemFilter(-1); e.Handled = true; break;
            case Key.OemCloseBrackets: CycleSystemFilter(1); e.Handled = true; break;
            case Key.G: ToggleGalleryMode(); e.Handled = true; break;
            case Key.V: ToggleArtworkViewer(); e.Handled = true; break;
            case Key.D0 or Key.NumPad0: RateSelectedGame(0); e.Handled = true; break;
            case Key.D1 or Key.NumPad1: RateSelectedGame(1); e.Handled = true; break;
            case Key.D2 or Key.NumPad2: RateSelectedGame(2); e.Handled = true; break;
            case Key.D3 or Key.NumPad3: RateSelectedGame(3); e.Handled = true; break;
            case Key.D4 or Key.NumPad4: RateSelectedGame(4); e.Handled = true; break;
            case Key.D5 or Key.NumPad5: RateSelectedGame(5); e.Handled = true; break;
            default:
                if (e.Key >= Key.A && e.Key <= Key.Z)
                {
                    char letter = (char)('A' + (e.Key - Key.A));
                    JumpToLetter(letter);
                    e.Handled = true;
                }
                break;
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  Helpers
    // ══════════════════════════════════════════════════════════════

    private static string FormatPlayTime(long totalSeconds)
    {
        if (totalSeconds <= 0) return "\u2014";
        if (totalSeconds < 60) return "< 1m";
        long hours = totalSeconds / 3600, minutes = (totalSeconds % 3600) / 60;
        return hours > 0 ? $"{hours}h {minutes}m" : $"{minutes}m";
    }

    private static string FormatRatingStars(int rating)
    {
        if (rating <= 0) return "\u2606\u2606\u2606\u2606\u2606";
        return new string('\u2605', Math.Min(rating, 5)) + new string('\u2606', Math.Max(0, 5 - rating));
    }

    private static readonly string[] FileSizeUnits = ["B", "KB", "MB", "GB", "TB"];
    private static string FormatFileSize(long bytes)
    {
        double size = bytes; int u = 0;
        while (size >= 1024 && u < FileSizeUnits.Length - 1) { size /= 1024; u++; }
        return $"{size:F1} {FileSizeUnits[u]}";
    }

    private static IBrush GetSystemBrush(RomSystem system)
    {
        return SystemBrushCache.GetOrAdd(system, static sys =>
        {
            string color = sys switch
            {
                RomSystem.NES or RomSystem.SNES or RomSystem.N64 or RomSystem.N64DD => "#FF6B8A",
                RomSystem.GameBoy or RomSystem.GameBoyColor => "#6BE3A0",
                RomSystem.GameBoyAdvance => "#5CE0D2",
                RomSystem.VirtualBoy => "#FF6B8A",
                RomSystem.NintendoDS or RomSystem.Nintendo3DS => "#F08090",
                RomSystem.GameCube or RomSystem.Wii => "#9B8CFF",
                RomSystem.SegaMasterSystem or RomSystem.MegaDrive or RomSystem.SegaCD
                    or RomSystem.Sega32X or RomSystem.GameGear
                    or RomSystem.SegaSaturn or RomSystem.SegaDreamcast => "#5B9AFF",
                RomSystem.Atari2600 or RomSystem.Atari5200 or RomSystem.Atari7800
                    or RomSystem.AtariJaguar or RomSystem.AtariLynx or RomSystem.Atari800 => "#FF9B6B",
                RomSystem.PCEngine or RomSystem.NECPC88 => "#FFD86B",
                RomSystem.NeoGeoPocket or RomSystem.NeoGeo or RomSystem.NeoGeoCD => "#B38CFF",
                RomSystem.Arcade => "#FF8CCE",
                RomSystem.Panasonic3DO or RomSystem.PhilipsCDi => "#5BC8FF",
                RomSystem.ColecoVision or RomSystem.Intellivision => "#5BDDFF",
                RomSystem.MSX or RomSystem.MSX2 => "#FFD0C0",
                RomSystem.AmstradCPC or RomSystem.Oric or RomSystem.ThomsonMO5
                    or RomSystem.ColorComputer => "#8A8AAA",
                RomSystem.WataraSupervision or RomSystem.FairchildChannelF => "#A0A0C0",
                RomSystem.AmigaCD32 => "#FFB0B0",
                RomSystem.TigerGameCom => "#FF8CCE",
                RomSystem.MemotechMTX => "#5CE0D2",
                _ => "#4A4A6A"
            };
            return new SolidColorBrush(Color.Parse(color));
        });
    }
}
