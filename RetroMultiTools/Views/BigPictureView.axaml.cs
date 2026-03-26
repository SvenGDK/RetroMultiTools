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
using RetroMultiTools.Utilities.Mame;
using RetroMultiTools.Utilities.Mednafen;
using RetroMultiTools.Utilities.RetroArch;

namespace RetroMultiTools.Views;

public partial class BigPictureView : UserControl
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

    /// <summary>Maps ROM file paths to the Image control on each game card for thumbnail updates.</summary>
    private readonly Dictionary<string, Image> _cardImageMap = new();

    /// <summary>In-memory cache of card thumbnail bitmaps keyed by ROM file path.</summary>
    private readonly Dictionary<string, Bitmap> _thumbnailCache = new();

    /// <summary>Current card scale factor (0.5–2.0). Affects card width, height, and font sizes.</summary>
    private double _cardScale = 1.0;

    private const int BaseCardWidth = 180;
    private const int BaseCardHeight = 230;
    private const int CardSpacing = 16;
    private const int DetailPanelWidth = 360;
    private const int PageNavigationRows = 3;
    private const int PreloadMaxConcurrency = 3;
    private const double ScaleStep = 0.1;
    private const double ScaleMin = 0.5;
    private const double ScaleMax = 2.0;
    private const int SearchDebounceMs = 300;
    private const int LetterJumpDisplayMs = 800;
    private const int ScreensaverCycleMs = 5000;
    private const int RecentlyPlayedMaxCards = 10;
    private const int RecentlyPlayedCardWidth = 120;
    private const int RecentlyPlayedCardHeight = 80;
    private const int SystemCycleDisplayMs = 800;
    private const int GalleryIntervalMs = 3000;
    private const int ArtworkViewerImageCount = 3;

    // Cached brushes to avoid repeated allocations during card creation and selection
    private static readonly IBrush CardDefaultBackground = new SolidColorBrush(Color.Parse("#1E1E2E"));
    private static readonly IBrush CardSelectedBackground = new SolidColorBrush(Color.Parse("#313244"));
    private static readonly IBrush CardSelectedBorder = new SolidColorBrush(Color.Parse("#89B4FA"));
    private static readonly IBrush CardInitialForeground = new SolidColorBrush(Color.Parse("#1E1E2E"));
    private static readonly IBrush CardNameForeground = new SolidColorBrush(Color.Parse("#CDD6F4"));
    private static readonly IBrush CardSystemForeground = new SolidColorBrush(Color.Parse("#89B4FA"));
    private static readonly IBrush CardSizeForeground = new SolidColorBrush(Color.Parse("#6C7086"));
    private static readonly Dictionary<RomSystem, IBrush> SystemBrushCache = new();
    private static readonly Cursor HandCursor = new(StandardCursorType.Hand);

    // Cached brushes for ROM Info overlay to avoid per-row allocations
    private static readonly IBrush RomInfoSectionHeaderForeground = new SolidColorBrush(Color.Parse("#89B4FA"));
    private static readonly IBrush RomInfoSubHeaderForeground = new SolidColorBrush(Color.Parse("#F9E2AF"));
    private static readonly IBrush RomInfoLabelForeground = new SolidColorBrush(Color.Parse("#A6ADC8"));
    private static readonly IBrush RomInfoValueForeground = new SolidColorBrush(Color.Parse("#CDD6F4"));
    private static readonly IBrush RecentlyPlayedNameOverlayBackground = new SolidColorBrush(Color.Parse("#CC11111B"));

    // --- Screensaver / Attract Mode fields ---
    private DispatcherTimer? _inactivityTimer;
    private DispatcherTimer? _screensaverCycleTimer;
    private bool _screensaverActive;
    private int _lastScreensaverRomIndex = -1;

    // --- Letter Jump fields ---
    private CancellationTokenSource? _letterJumpCts;

    // --- Recently Played Bar fields ---
    private bool _recentlyPlayedBarVisible;

    // --- System Cycle fields ---
    private CancellationTokenSource? _systemCycleCts;

    // --- Gallery Mode fields ---
    private DispatcherTimer? _galleryTimer;
    private bool _galleryActive;

    // --- Artwork Viewer fields ---
    private Bitmap?[] _artworkViewerImages = new Bitmap?[ArtworkViewerImageCount];
    private string[] _artworkViewerLabels = new string[ArtworkViewerImageCount];
    private int _artworkViewerIndex;

    // --- Rating display ---
    private static readonly IBrush RatingStarForeground = new SolidColorBrush(Color.Parse("#F9E2AF"));
    private static readonly IBrush RatingEmptyForeground = new SolidColorBrush(Color.Parse("#45475A"));

    /// <summary>Effective card width based on current scale.</summary>
    private int CardWidth => (int)(BaseCardWidth * _cardScale);

    /// <summary>Effective card height based on current scale.</summary>
    private int CardHeight => (int)(BaseCardHeight * _cardScale);

    public BigPictureView()
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

    private void OnAttachedToVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        Focus();
        InitialiseGamepad();
    }

    private void OnDetachedFromVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        KeyDown -= OnKeyDownHandler;
        PointerMoved -= OnPointerActivity;
        PointerPressed -= OnPointerActivity;
        _searchDebounceCts?.Cancel();
        _searchDebounceCts?.Dispose();
        _searchDebounceCts = null;
        _checksumCts?.Cancel();
        _checksumCts?.Dispose();
        _checksumCts = null;
        _letterJumpCts?.Cancel();
        _letterJumpCts?.Dispose();
        _letterJumpCts = null;
        _systemCycleCts?.Cancel();
        _systemCycleCts?.Dispose();
        _systemCycleCts = null;
        StopGalleryMode();
        StopInactivityTimer();
        StopScreensaverCycleTimer();
        _screensaverActive = false;
        ScreensaverImage.Source = null;
        ArtworkViewerImage.Source = null;
        CancelArtworkPreload();
        CancelArtworkLoading();
        ClearArtworkImages();
        ClearArtworkViewerReferences();
        ClearThumbnailCache();
        ShutdownGamepad();
    }

    // ── Gamepad integration ────────────────────────────────────────────

    private void InitialiseGamepad()
    {
        if (!AppSettings.Instance.GamepadEnabled) return;

        var gp = GamepadService.Instance;

        // Subscribe events before Initialise() so that controllers
        // discovered during startup enumeration fire connected events.
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
            // SDL2 not available – remove handlers so we don't hold
            // references to a non-functional service.
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
        var loc = LocalizationManager.Instance;
        StatusText.Text = string.Format(loc["BigPicture_GamepadConnected"], name);
        UpdateControllerStatus();
        UpdateHelpOverlayGamepadVisibility();
        UpdateHintText();
    }

    private void OnControllerDisconnected()
    {
        var loc = LocalizationManager.Instance;
        StatusText.Text = loc["BigPicture_GamepadDisconnected"];
        UpdateControllerStatus();
        UpdateHelpOverlayGamepadVisibility();
        UpdateHintText();
    }

    private void UpdateControllerStatus()
    {
        var gp = GamepadService.Instance;
        var loc = LocalizationManager.Instance;

        if (!gp.IsAvailable)
        {
            GamepadStatusText.Text = string.Empty;
            return;
        }

        if (gp.IsControllerConnected)
            GamepadStatusText.Text = $"🎮 {gp.ControllerName}";
        else
            GamepadStatusText.Text = $"🎮 {loc["BigPicture_GamepadNone"]}";
    }

    /// <summary>
    /// Sets the status-bar hint text to controller button hints when a
    /// controller is connected, otherwise falls back to keyboard hints.
    /// </summary>
    private void UpdateHintText()
    {
        var gp = GamepadService.Instance;
        var loc = LocalizationManager.Instance;

        if (gp.IsAvailable && gp.IsControllerConnected)
            HintText.Text = loc["BigPicture_HintsPad"];
        else
            HintText.Text = loc["BigPicture_Hints"];
    }

    /// <summary>
    /// Handles mouse/pointer activity to reset the screensaver inactivity
    /// timer and dismiss the screensaver if it is currently active.
    /// </summary>
    private void OnPointerActivity(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        ResetInactivityTimer();

        if (_screensaverActive)
            DismissScreensaver();
    }

    private void OnGamepadAction(GamepadAction action)
    {
        ResetInactivityTimer();

        // Dismiss screensaver on any gamepad input
        if (_screensaverActive)
        {
            DismissScreensaver();
            return;
        }

        // When ROM info overlay is showing, only allow dismiss
        if (RomInfoOverlay.IsVisible)
        {
            if (action is GamepadAction.Back or GamepadAction.RomInfo)
                DismissRomInfoOverlay();
            return;
        }

        // When stats overlay is showing, only allow dismiss
        if (StatsOverlay.IsVisible)
        {
            if (action is GamepadAction.Back or GamepadAction.StatsOverlay)
                DismissStatsOverlay();
            return;
        }

        // When artwork viewer is showing, allow navigation and dismiss
        if (ArtworkViewerOverlay.IsVisible)
        {
            switch (action)
            {
                case GamepadAction.Back or GamepadAction.ArtworkViewer:
                    DismissArtworkViewer();
                    break;
                case GamepadAction.NavigateLeft:
                    CycleArtworkViewer(-1);
                    break;
                case GamepadAction.NavigateRight:
                    CycleArtworkViewer(1);
                    break;
            }
            return;
        }

        // When help overlay is showing, only allow dismiss
        if (HelpOverlay.IsVisible)
        {
            if (action is GamepadAction.Back or GamepadAction.Help)
                HelpOverlay.IsVisible = false;
            return;
        }

        // When gallery mode is active, Back should stop the gallery
        // instead of exiting Big Picture Mode (mirrors keyboard Escape behaviour).
        if (_galleryActive && action is GamepadAction.Back)
        {
            StopGalleryMode();
            return;
        }

        switch (action)
        {
            case GamepadAction.NavigateLeft:
                NavigateCards(-1);
                break;
            case GamepadAction.NavigateRight:
                NavigateCards(1);
                break;
            case GamepadAction.NavigateUp:
                NavigateCardsRow(-1);
                break;
            case GamepadAction.NavigateDown:
                NavigateCardsRow(1);
                break;
            case GamepadAction.Confirm:
                LaunchSelectedRom();
                break;
            case GamepadAction.Back:
                ExitBigPictureMode();
                break;
            case GamepadAction.ToggleFavorite:
                ToggleSelectedFavorite();
                break;
            case GamepadAction.Search:
                SearchBox.Focus();
                break;
            case GamepadAction.Help:
                ToggleHelpOverlay();
                break;
            case GamepadAction.RomInfo:
                ToggleRomInfoOverlay();
                break;
            case GamepadAction.RandomGame:
                SelectRandomGame();
                break;
            case GamepadAction.PageUp:
                NavigateCardsRow(-PageNavigationRows);
                break;
            case GamepadAction.PageDown:
                NavigateCardsRow(PageNavigationRows);
                break;
            case GamepadAction.Home:
                if (_filteredRoms.Count > 0) SelectCard(0);
                break;
            case GamepadAction.End:
                if (_filteredRoms.Count > 0) SelectCard(_filteredRoms.Count - 1);
                break;
            case GamepadAction.ZoomIn:
                ZoomIn();
                break;
            case GamepadAction.ZoomOut:
                ZoomOut();
                break;
            case GamepadAction.StatsOverlay:
                ToggleStatsOverlay();
                break;
            case GamepadAction.GalleryMode:
                ToggleGalleryMode();
                break;
            case GamepadAction.ArtworkViewer:
                ToggleArtworkViewer();
                break;
            case GamepadAction.CycleRating:
                CycleSelectedRating();
                break;
            case GamepadAction.RecentlyPlayed:
                ToggleRecentlyPlayedBar();
                break;
            case GamepadAction.SystemCycleForward:
                CycleSystemFilter(1);
                break;
            case GamepadAction.SystemCycleBackward:
                CycleSystemFilter(-1);
                break;
        }
    }

    /// <summary>
    /// Called by MainWindow to load a folder that was already open in the ROM Browser.
    /// </summary>
    public void LoadFolder(string folderPath, List<RomInfo>? existingRoms = null)
    {
        if (string.IsNullOrEmpty(folderPath)) return;

        _currentFolder = folderPath;

        if (existingRoms != null && existingRoms.Count > 0)
        {
            _allRoms = new List<RomInfo>(existingRoms);
            ApplyFilterAndSort();
            StatusText.Text = string.Format(LocalizationManager.Instance["BigPicture_LoadedRoms"],
                _allRoms.Count, _currentFolder);
            StartArtworkPreload(_allRoms);
            PopulateRecentlyPlayedBar();
        }
        else
        {
            ScanCurrentFolderFireAndForget();
        }
    }

    /// <summary>
    /// Starts a folder scan without awaiting, observing any exceptions to
    /// avoid unobserved task faults.
    /// </summary>
    private async void ScanCurrentFolderFireAndForget()
    {
        try
        {
            await ScanCurrentFolderAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[BigPicture] Folder scan failed: {ex.Message}");
        }
    }

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
        SortCombo.SelectedIndex = 0;
    }

    private async void BrowseFolderButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var loc = LocalizationManager.Instance;
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = loc["BigPicture_SelectRomFolder"],
                AllowMultiple = false
            });

            if (folders.Count == 0) return;

            _currentFolder = folders[0].Path.LocalPath;
            AppSettings.Instance.BigPictureRomFolder = _currentFolder;
            await ScanCurrentFolderAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[BigPicture] BrowseFolderButton_Click failed: {ex.Message}");
        }
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
            PopulateRecentlyPlayedBar();
        }
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
            if (!token.IsCancellationRequested)
                ApplyFilterAndSort();
        }
        catch (OperationCanceledException) { /* next keystroke cancelled this one */ }
    }

    private void ApplyFilterAndSort()
    {
        if (_suppressFilterUpdate) return;

        // Capture current selection before the filtered list changes so
        // RebuildGameCards can restore it in the new list.
        string? selectedPath = (_selectedIndex >= 0 && _selectedIndex < _filteredRoms.Count)
            ? _filteredRoms[_selectedIndex].FilePath
            : null;

        IEnumerable<RomInfo> filtered = _allRoms;

        // Favorites filter
        if (FavoritesFilterButton.IsChecked == true)
        {
            var favorites = AppSettings.Instance.Favorites;
            filtered = filtered.Where(r => favorites.Contains(r.FilePath));
        }

        // System filter
        if (SystemFilterCombo.SelectedItem is ComboBoxItem sysItem && sysItem.Tag is RomSystem selectedSystem)
        {
            filtered = filtered.Where(r => r.System == selectedSystem);
        }

        // Search filter
        string? searchText = SearchBox?.Text;
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            string search = searchText.Trim();
            filtered = filtered.Where(r =>
                r.FileName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                r.SystemName.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        // Sort
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
            _ => filtered.OrderBy(r => r.FileName, StringComparer.OrdinalIgnoreCase)
        };

        _filteredRoms = filtered.ToList();
        RebuildGameCards(selectedPath);

        bool hasRoms = _filteredRoms.Count > 0;
        EmptyState.IsVisible = !hasRoms;

        UpdateRomCount();

        if (_allRoms.Count > 0 && _filteredRoms.Count != _allRoms.Count)
            StatusText.Text = string.Format(LocalizationManager.Instance["BigPicture_ShowingFiltered"],
                _filteredRoms.Count, _allRoms.Count);
    }

    private static IEnumerable<RomInfo> SortByRecentlyPlayed(IEnumerable<RomInfo> roms)
    {
        var recentList = AppSettings.Instance.RecentlyPlayed;
        var recentIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < recentList.Count; i++)
            recentIndex[recentList[i]] = i;

        // Recently played first (by recency), then the rest alphabetically
        return roms.OrderBy(r => recentIndex.TryGetValue(r.FilePath, out int idx) ? idx : int.MaxValue)
                   .ThenBy(r => r.FileName, StringComparer.OrdinalIgnoreCase);
    }

    private void UpdateRomCount()
    {
        var loc = LocalizationManager.Instance;
        if (_allRoms.Count == 0)
        {
            RomCountText.Text = string.Empty;
        }
        else if (_filteredRoms.Count == _allRoms.Count)
        {
            RomCountText.Text = string.Format(loc["BigPicture_RomCount"], _allRoms.Count);
        }
        else
        {
            RomCountText.Text = string.Format(loc["BigPicture_RomCountFiltered"],
                _filteredRoms.Count, _allRoms.Count);
        }
    }

    private void RebuildGameCards(string? previousSelectionOverride = null)
    {
        // When called from ApplyFilterAndSort the selection was captured before
        // _filteredRoms changed.  When called from ZoomIn/ZoomOut the list is
        // unchanged so reading it here is still correct.
        string? previousSelection = previousSelectionOverride
            ?? ((_selectedIndex >= 0 && _selectedIndex < _filteredRoms.Count)
                ? _filteredRoms[_selectedIndex].FilePath
                : null);

        GameCardsPanel.Children.Clear();
        _cardImageMap.Clear();
        _selectedIndex = -1;

        for (int i = 0; i < _filteredRoms.Count; i++)
        {
            var rom = _filteredRoms[i];
            var card = CreateGameCard(rom, i);
            GameCardsPanel.Children.Add(card);
        }

        if (_filteredRoms.Count > 0)
        {
            // Try to re-select the previously selected ROM
            int restoredIndex = 0;
            if (previousSelection != null)
            {
                int found = _filteredRoms.FindIndex(r =>
                    string.Equals(r.FilePath, previousSelection, StringComparison.OrdinalIgnoreCase));
                if (found >= 0)
                    restoredIndex = found;
            }
            SelectCard(restoredIndex);
        }
        else
        {
            ClearDetailPanel();
        }
    }

    private Border CreateGameCard(RomInfo rom, int index)
    {
        // System icon letter (first letter of system name)
        string systemInitial = !string.IsNullOrEmpty(rom.SystemName)
            ? rom.SystemName[..1].ToUpperInvariant()
            : "?";

        int bannerHeight = (int)(120 * _cardScale);
        double nameFontSize = Math.Max(9, 12 * _cardScale);
        double metaFontSize = Math.Max(8, 10 * _cardScale);
        double initialFontSize = Math.Max(24, 48 * _cardScale);

        // Image control for the card thumbnail (box art)
        var cardImage = new Image
        {
            Stretch = Stretch.UniformToFill,
            IsVisible = false
        };

        // If a thumbnail was already cached in memory, use it immediately
        if (_thumbnailCache.TryGetValue(rom.FilePath, out var cached))
        {
            cardImage.Source = cached;
            cardImage.IsVisible = true;
        }

        // Register the image for async updates during artwork pre-loading
        _cardImageMap[rom.FilePath] = cardImage;

        var card = new Border
        {
            Width = CardWidth,
            Height = CardHeight,
            Background = CardDefaultBackground,
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(CardSpacing / 2.0),
            Cursor = HandCursor,
            Tag = index,
            Child = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    // System color banner with optional artwork overlay
                    new Border
                    {
                        Height = bannerHeight,
                        CornerRadius = new CornerRadius(8, 8, 0, 0),
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
                                    Foreground = CardInitialForeground,
                                    Opacity = 0.3,
                                    HorizontalAlignment = HorizontalAlignment.Center,
                                    VerticalAlignment = VerticalAlignment.Center
                                },
                                cardImage
                            }
                        }
                    },
                    // ROM name
                    new TextBlock
                    {
                        Text = Path.GetFileNameWithoutExtension(rom.FileName),
                        FontSize = nameFontSize,
                        Foreground = CardNameForeground,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxLines = 2,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(8, 0, 8, 0),
                        Height = (int)(34 * _cardScale)
                    },
                    // System name + size
                    new StackPanel
                    {
                        Margin = new Thickness(8, 0, 8, 6),
                        Children =
                        {
                            new TextBlock
                            {
                                Text = rom.SystemName,
                                FontSize = metaFontSize,
                                Foreground = CardSystemForeground,
                                TextTrimming = TextTrimming.CharacterEllipsis
                            },
                            new TextBlock
                            {
                                Text = rom.FileSizeFormatted,
                                FontSize = metaFontSize,
                                Foreground = CardSizeForeground
                            },
                            new TextBlock
                            {
                                Text = FormatRatingStars(AppSettings.Instance.GetRating(rom.FilePath)),
                                FontSize = metaFontSize,
                                Foreground = RatingStarForeground,
                                IsVisible = AppSettings.Instance.GetRating(rom.FilePath) > 0
                            }
                        }
                    }
                }
            }
        };

        card.PointerPressed += (_, _) =>
        {
            SelectCard(index);
            Focus();
        };

        card.DoubleTapped += (_, _) => LaunchSelectedRom();

        return card;
    }

    private static IBrush GetSystemBrush(RomSystem system)
    {
        if (SystemBrushCache.TryGetValue(system, out var cached))
            return cached;

        // Catppuccin color palette for different systems
        string color = system switch
        {
            RomSystem.NES or RomSystem.SNES or RomSystem.N64 or RomSystem.N64DD => "#F38BA8", // Red
            RomSystem.GameBoy or RomSystem.GameBoyColor => "#A6E3A1", // Green
            RomSystem.GameBoyAdvance => "#94E2D5", // Teal
            RomSystem.VirtualBoy => "#F38BA8", // Red
            RomSystem.NintendoDS or RomSystem.Nintendo3DS => "#EBA0AC", // Maroon
            RomSystem.GameCube or RomSystem.Wii => "#B4BEFE", // Lavender
            RomSystem.SegaMasterSystem or RomSystem.MegaDrive or RomSystem.SegaCD
                or RomSystem.Sega32X or RomSystem.GameGear
                or RomSystem.SegaSaturn or RomSystem.SegaDreamcast => "#89B4FA", // Blue
            RomSystem.Atari2600 or RomSystem.Atari5200 or RomSystem.Atari7800
                or RomSystem.AtariJaguar or RomSystem.AtariLynx or RomSystem.Atari800 => "#FAB387", // Peach
            RomSystem.PCEngine or RomSystem.NECPC88 => "#F9E2AF", // Yellow
            RomSystem.NeoGeoPocket or RomSystem.NeoGeo or RomSystem.NeoGeoCD => "#CBA6F7", // Mauve
            RomSystem.Arcade => "#F5C2E7", // Pink
            RomSystem.Panasonic3DO or RomSystem.PhilipsCDi => "#74C7EC", // Sapphire
            RomSystem.ColecoVision or RomSystem.Intellivision => "#89DCEB", // Sky
            RomSystem.MSX or RomSystem.MSX2 => "#F5E0DC", // Rosewater
            RomSystem.AmstradCPC or RomSystem.Oric or RomSystem.ThomsonMO5
                or RomSystem.ColorComputer => "#A6ADC8", // Subtext0
            RomSystem.WataraSupervision or RomSystem.FairchildChannelF => "#BAC2DE", // Subtext1
            RomSystem.AmigaCD32 => "#F2CDCD", // Flamingo
            RomSystem.TigerGameCom => "#F5C2E7", // Pink
            RomSystem.MemotechMTX => "#94E2D5", // Teal
            _ => "#585B70" // Surface2 (default grey)
        };
        var brush = new SolidColorBrush(Color.Parse(color));
        SystemBrushCache[system] = brush;
        return brush;
    }

    private void SelectCard(int index)
    {
        if (index < 0 || index >= _filteredRoms.Count) return;

        // Deselect previous
        if (_selectedIndex >= 0 && _selectedIndex < GameCardsPanel.Children.Count)
        {
            if (GameCardsPanel.Children[_selectedIndex] is Border prevCard)
            {
                prevCard.BorderBrush = null;
                prevCard.BorderThickness = new Thickness(0);
                prevCard.Background = CardDefaultBackground;
            }
        }

        _selectedIndex = index;

        // Highlight new
        if (GameCardsPanel.Children[_selectedIndex] is Border card)
        {
            card.BorderBrush = CardSelectedBorder;
            card.BorderThickness = new Thickness(3);
            card.Background = CardSelectedBackground;

            // Scroll into view
            card.BringIntoView();
        }

        // Update detail panel
        ShowDetailPanel(_filteredRoms[index]);
    }

    private void ShowDetailPanel(RomInfo rom)
    {
        var loc = LocalizationManager.Instance;
        DetailPanel.IsVisible = true;
        MainContentGrid.ColumnDefinitions[1].Width = new GridLength(DetailPanelWidth);

        DetailTitle.Text = Path.GetFileNameWithoutExtension(rom.FileName);
        DetailSystem.Text = rom.SystemName;
        DetailSize.Text = string.Format(loc["BigPicture_SizeFormat"], rom.FileSizeFormatted);
        DetailValid.Text = rom.IsValid
            ? loc["BigPicture_ValidRom"]
            : loc["BigPicture_InvalidRom"];
        DetailFilePath.Text = rom.FilePath;

        int playCount = AppSettings.Instance.GetPlayCount(rom.FilePath);
        DetailPlayCount.Text = string.Format(loc["BigPicture_PlayCount"], playCount);

        long playTimeSec = AppSettings.Instance.GetPlayTime(rom.FilePath);
        DetailPlayTime.Text = string.Format(loc["BigPicture_PlayTime"], FormatPlayTime(playTimeSec));

        int rating = AppSettings.Instance.GetRating(rom.FilePath);
        DetailRating.Text = rating > 0 ? string.Format(loc["BigPicture_RatingDisplay"], FormatRatingStars(rating), rating) : string.Format(loc["BigPicture_UnratedDisplay"], FormatRatingStars(rating), loc["BigPicture_Unrated"]);

        UpdateFavoriteButton(rom);

        bool canLaunch = RetroArchLauncher.IsSystemSupported(rom.System);
        DetailLaunchButton.IsEnabled = canLaunch;

        // Show the MAME launch button for Arcade ROMs
        bool canLaunchMame = MameLauncher.IsSystemSupported(rom.System);
        DetailLaunchMameButton.IsVisible = canLaunchMame;

        // Show the Mednafen launch button for supported systems
        bool canLaunchMednafen = MednafenLauncher.IsSystemSupported(rom.System);
        DetailLaunchMednafenButton.IsVisible = canLaunchMednafen;

        // Show BIOS notice for Neo Geo AES/MVS in the status bar
        if (rom.System == RomSystem.NeoGeo)
        {
            StatusText.Text = loc["Common_NeoGeoBiosNotice"];
        }

        _ = LoadArtworkAsync(rom);
    }

    private void ClearDetailPanel()
    {
        DetailPanel.IsVisible = false;
        MainContentGrid.ColumnDefinitions[1].Width = new GridLength(0);
        CancelArtworkLoading();
        ClearArtworkImages();
    }

    private async Task LoadArtworkAsync(RomInfo romInfo)
    {
        CancelArtworkLoading();

        _artworkCts = new CancellationTokenSource();
        var token = _artworkCts.Token;
        var loc = LocalizationManager.Instance;

        DetailArtworkStatus.Text = loc["BigPicture_ArtworkLoading"];
        ClearArtworkImages();

        try
        {
            var progress = new Progress<string>(msg =>
            {
                if (!token.IsCancellationRequested)
                    DetailArtworkStatus.Text = msg;
            });

            var artwork = await ArtworkService.FetchArtworkAsync(romInfo, progress, token);

            if (token.IsCancellationRequested) return;

            if (artwork.BoxArt != null)
                DetailBoxArt.Source = LoadBitmapFromBytes(artwork.BoxArt);

            if (artwork.Snap != null)
                DetailScreenshot.Source = LoadBitmapFromBytes(artwork.Snap);

            if (artwork.TitleScreen != null)
                DetailTitleScreen.Source = LoadBitmapFromBytes(artwork.TitleScreen);

            DetailArtworkStatus.Text = artwork.HasAnyArtwork
                ? loc["BigPicture_ArtworkLoaded"]
                : loc["BigPicture_ArtworkNotFound"];
        }
        catch (OperationCanceledException)
        {
            // Navigation away — ignore
        }
        catch (HttpRequestException ex)
        {
            if (!token.IsCancellationRequested)
                DetailArtworkStatus.Text = string.Format(loc["BigPicture_NetworkError"], ex.Message);
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
                DetailArtworkStatus.Text = string.Format(loc["BigPicture_ArtworkFailed"], ex.Message);
        }
    }

    private static Bitmap? LoadBitmapFromBytes(byte[] data)
    {
        if (data.Length == 0) return null;

        try
        {
            using var stream = new MemoryStream(data);
            return new Bitmap(stream);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException) { return null; }
    }

    private void CancelArtworkLoading()
    {
        _artworkCts?.Cancel();
        _artworkCts?.Dispose();
        _artworkCts = null;
    }

    private void ClearArtworkImages()
    {
        // Detach bitmaps from Image controls before disposing to prevent
        // Avalonia from rendering an already-disposed bitmap.
        var boxArt = DetailBoxArt.Source as Bitmap;
        var screenshot = DetailScreenshot.Source as Bitmap;
        var titleScreen = DetailTitleScreen.Source as Bitmap;
        DetailBoxArt.Source = null;
        DetailScreenshot.Source = null;
        DetailTitleScreen.Source = null;
        boxArt?.Dispose();
        screenshot?.Dispose();
        titleScreen?.Dispose();
    }

    /// <summary>
    /// Starts a background task that pre-loads artwork for all ROMs into the
    /// disk cache so that individual card selections can serve artwork instantly.
    /// </summary>
    private void StartArtworkPreload(List<RomInfo> roms)
    {
        CancelArtworkPreload();
        ClearThumbnailCache();

        if (roms.Count == 0) return;

        _preloadCts = new CancellationTokenSource();
        var token = _preloadCts.Token;
        var romsSnapshot = new List<RomInfo>(roms);

        PreloadArtworkFireAndForget(romsSnapshot, token);
    }

    /// <summary>
    /// Runs artwork pre-loading without awaiting, observing any exceptions to
    /// avoid unobserved task faults.
    /// </summary>
    private async void PreloadArtworkFireAndForget(List<RomInfo> roms, CancellationToken cancellationToken)
    {
        try
        {
            await PreloadArtworkAsync(roms, cancellationToken);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[BigPicture] Artwork preload failed: {ex.Message}");
        }
    }

    private async Task PreloadArtworkAsync(List<RomInfo> roms, CancellationToken cancellationToken)
    {
        var loc = LocalizationManager.Instance;
        int completed = 0;
        int total = roms.Count;

        using var semaphore = new SemaphoreSlim(PreloadMaxConcurrency);

        try
        {
            var tasks = roms.Select(async rom =>
            {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var artwork = await ArtworkService.FetchArtworkAsync(rom, progress: null, cancellationToken)
                        .ConfigureAwait(false);

                    // Update the card thumbnail when box art is available
                    if (artwork.BoxArt != null)
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            if (cancellationToken.IsCancellationRequested) return;

                            var bitmap = LoadBitmapFromBytes(artwork.BoxArt);
                            if (bitmap != null)
                            {
                                // Swap in the new bitmap before disposing the old one
                                // to avoid rendering a disposed image
                                Bitmap? old = null;
                                if (_thumbnailCache.TryGetValue(rom.FilePath, out var prev))
                                    old = prev;

                                // Cache for future card rebuilds (filter/sort changes)
                                _thumbnailCache[rom.FilePath] = bitmap;

                                // Update the visible card image if it still exists
                                if (_cardImageMap.TryGetValue(rom.FilePath, out var cardImage))
                                {
                                    cardImage.Source = bitmap;
                                    cardImage.IsVisible = true;
                                }

                                // Now safe to dispose the previous bitmap
                                old?.Dispose();
                            }
                        });
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine(
                        $"[BigPicture] Artwork preload failed for {rom.FileName}: {ex.Message}");
                }
                finally
                {
                    semaphore.Release();
                    int done = Interlocked.Increment(ref completed);
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            HintText.Text = string.Format(
                                loc["BigPicture_PreloadProgress"], done, total);
                        }
                    });
                }
            });

            await Task.WhenAll(tasks).ConfigureAwait(false);

            // Briefly show the completion message, then restore the keyboard hints
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    HintText.Text = loc["BigPicture_PreloadComplete"];
                    RestoreHintTextAfterDelay(cancellationToken);
                }
            });
        }
        catch (OperationCanceledException)
        {
            // Pre-loading cancelled — normal when navigating away or changing folder
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[BigPicture] Artwork preload error: {ex.Message}");
        }
    }

    private void CancelArtworkPreload()
    {
        _preloadCts?.Cancel();
        _preloadCts?.Dispose();
        _preloadCts = null;
    }

    /// <summary>
    /// Restores the keyboard shortcut hints in the status bar after a brief
    /// delay so the preload-complete message is visible momentarily.
    /// </summary>
    private async void RestoreHintTextAfterDelay(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(3000, cancellationToken);
            if (!cancellationToken.IsCancellationRequested)
            {
                UpdateHintText();
            }
        }
        catch (OperationCanceledException) { /* preload cancelled or navigated away */ }
    }

    private void ClearThumbnailCache()
    {
        // Detach bitmaps from any Image controls before disposing
        foreach (var cardImage in _cardImageMap.Values)
            cardImage.Source = null;

        foreach (var bitmap in _thumbnailCache.Values)
            bitmap.Dispose();
        _thumbnailCache.Clear();
    }

    private void UpdateFavoriteButton(RomInfo rom)
    {
        var loc = LocalizationManager.Instance;
        bool isFav = AppSettings.Instance.IsFavorite(rom.FilePath);
        DetailFavoriteButton.Content = isFav
            ? "★ " + loc["BigPicture_Unfavorite"]
            : "☆ " + loc["BigPicture_Favorite"];
    }

    private void FavoriteButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ToggleSelectedFavorite();
    }

    private void ToggleSelectedFavorite()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _filteredRoms.Count) return;
        var rom = _filteredRoms[_selectedIndex];

        if (AppSettings.Instance.IsFavorite(rom.FilePath))
            AppSettings.Instance.RemoveFavorite(rom.FilePath);
        else
            AppSettings.Instance.AddFavorite(rom.FilePath);

        UpdateFavoriteButton(rom);
    }

    private void RandomGameButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        SelectRandomGame();

    private void SelectRandomGame()
    {
        if (_filteredRoms.Count == 0) return;

        int randomIndex;
        if (_filteredRoms.Count == 1)
        {
            randomIndex = 0;
        }
        else
        {
            // Avoid re-selecting the same ROM
            do
            {
                randomIndex = Random.Shared.Next(_filteredRoms.Count);
            } while (randomIndex == _selectedIndex);
        }

        SelectCard(randomIndex);
        Focus();
    }

    private void RescanButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentFolder)) return;
        ScanCurrentFolderFireAndForget();
    }

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
            // Append BIOS notice for Neo Geo AES/MVS after launch
            if (rom.System == RomSystem.NeoGeo)
            {
                StatusText.Text = string.Format(loc["Common_NeoGeoBiosNoticeWithSeparator"], result.Message, loc["Common_NeoGeoBiosNotice"]);
            }

            HandleSuccessfulLaunch(rom, result.Process);
        }
    }

    private void LaunchButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => LaunchSelectedRom();

    private void LaunchMameButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => LaunchSelectedRomWithMame();

    private void LaunchMednafenButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => LaunchSelectedRomWithMednafen();

    private void LaunchSelectedRomWithMame()
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

        if (result.Success)
            HandleSuccessfulLaunch(rom, result.Process);
    }

    private void LaunchSelectedRomWithMednafen()
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

        if (result.Success)
            HandleSuccessfulLaunch(rom, result.Process);
    }

    /// <summary>
    /// Shared post-launch logic: records play tracking stats, refreshes
    /// the detail panel, and optionally minimizes to tray while the
    /// emulator process is running.
    /// </summary>
    private void HandleSuccessfulLaunch(RomInfo rom, System.Diagnostics.Process? process)
    {
        if (AppSettings.Instance.BigPicturePlayTrackingEnabled)
        {
            AppSettings.Instance.RecordRecentlyPlayed(rom.FilePath);
            AppSettings.Instance.IncrementPlayCount(rom.FilePath);

            // Refresh play count in the detail panel
            var loc = LocalizationManager.Instance;
            int playCount = AppSettings.Instance.GetPlayCount(rom.FilePath);
            DetailPlayCount.Text = string.Format(loc["BigPicture_PlayCount"], playCount);

            // Refresh recently played bar
            PopulateRecentlyPlayedBar();
        }

        if (process != null)
        {
            if (AppSettings.Instance.MinimizeToTrayOnLaunch)
            {
                MinimizeToTrayAndRestoreOnExit(process, rom.FilePath);
            }
            else
            {
                process.Dispose();
            }
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
                        System.Diagnostics.Trace.WriteLine($"[BigPicture] Process monitoring ended: {ex.Message}");
                    }
                }).ConfigureAwait(false);
                var elapsed = (long)(DateTime.UtcNow - startTime).TotalSeconds;
                if (AppSettings.Instance.BigPicturePlayTrackingEnabled)
                    AppSettings.Instance.AddPlayTime(romFilePath, elapsed);

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    mainWindow.RestoreFromTray();
                    DiscordRichPresence.ClearPresence();

                    // Refresh play time in the detail panel
                    if (_selectedIndex >= 0 && _selectedIndex < _filteredRoms.Count &&
                        string.Equals(_filteredRoms[_selectedIndex].FilePath, romFilePath, StringComparison.OrdinalIgnoreCase))
                    {
                        var loc = LocalizationManager.Instance;
                        long playTimeSec = AppSettings.Instance.GetPlayTime(romFilePath);
                        DetailPlayTime.Text = string.Format(loc["BigPicture_PlayTime"], FormatPlayTime(playTimeSec));
                    }
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[BigPicture] Minimize-to-tray failed: {ex.Message}");
        }
        finally
        {
            process.Dispose();
        }
    }

    private void ExitBigPictureButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ExitBigPictureMode();
    }

    private void ExitBigPictureMode()
    {
        // Defensive cleanup: stop gallery and dismiss screensaver before
        // leaving Big Picture Mode so timers don't fire after detach.
        StopGalleryMode();
        if (_screensaverActive)
            DismissScreensaver();

        // Stop the inactivity timer directly so it doesn't fire between now
        // and the DetachedFromVisualTree event. DismissScreensaver() restarts
        // the timer, so this must come after that call.
        StopInactivityTimer();

        if (TopLevel.GetTopLevel(this) is MainWindow mainWindow)
        {
            mainWindow.ExitBigPictureMode();
        }
    }

    // --- Grid Zoom ---

    private void ZoomIn()
    {
        double newScale = Math.Min(_cardScale + ScaleStep, ScaleMax);
        if (Math.Abs(newScale - _cardScale) < 0.001) return;
        _cardScale = Math.Round(newScale, 1);
        AppSettings.Instance.BigPictureCardScale = _cardScale;
        UpdateZoomLevelText();
        RebuildGameCards();
    }

    private void ZoomOut()
    {
        double newScale = Math.Max(_cardScale - ScaleStep, ScaleMin);
        if (Math.Abs(newScale - _cardScale) < 0.001) return;
        _cardScale = Math.Round(newScale, 1);
        AppSettings.Instance.BigPictureCardScale = _cardScale;
        UpdateZoomLevelText();
        RebuildGameCards();
    }

    private void UpdateZoomLevelText()
    {
        var loc = LocalizationManager.Instance;
        int pct = (int)Math.Round(_cardScale * 100);
        ZoomLevelText.Text = string.Format(loc["BigPicture_ZoomLevel"], pct);
    }

    // --- Help Overlay ---

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

        // Show controller button column when gamepad support is available
        UpdateHelpOverlayGamepadVisibility();
    }

    /// <summary>
    /// Updates the visibility of the gamepad help column based on whether
    /// SDL2 gamepad support is currently available.  Called once during
    /// PopulateHelpOverlay (initial setup) and again after InitialiseGamepad
    /// so the column appears even when SDL2 was not yet loaded at
    /// construction time.
    /// </summary>
    private void UpdateHelpOverlayGamepadVisibility()
    {
        bool showGamepad = GamepadService.Instance.IsAvailable;
        HelpGamepadHeader.IsVisible = showGamepad;
        HelpGamepadNav.IsVisible = showGamepad;
        HelpGamepadLaunch.IsVisible = showGamepad;
        HelpGamepadFav.IsVisible = showGamepad;
        HelpGamepadSearch.IsVisible = showGamepad;
        HelpGamepadExit.IsVisible = showGamepad;
        HelpGamepadHomeEnd.IsVisible = showGamepad;
        HelpGamepadPage.IsVisible = showGamepad;
        HelpGamepadZoom.IsVisible = showGamepad;
        HelpGamepadToggle.IsVisible = showGamepad;
        HelpGamepadRomInfo.IsVisible = showGamepad;
        HelpGamepadRandom.IsVisible = showGamepad;
        HelpGamepadRecentlyPlayed.IsVisible = showGamepad;
        HelpGamepadStats.IsVisible = showGamepad;
        HelpGamepadSystemCycle.IsVisible = showGamepad;
        HelpGamepadGallery.IsVisible = showGamepad;
        HelpGamepadArtworkViewer.IsVisible = showGamepad;
        HelpGamepadRating.IsVisible = showGamepad;
    }

    private void ToggleHelpOverlay()
    {
        // Stop gallery before showing the overlay so the timer does not
        // advance cards behind it.  No action needed when hiding.
        if (!HelpOverlay.IsVisible)
            StopGalleryMode();

        HelpOverlay.IsVisible = !HelpOverlay.IsVisible;
    }

    // --- ROM Info Overlay ---

    private void ToggleRomInfoOverlay()
    {
        if (RomInfoOverlay.IsVisible)
        {
            DismissRomInfoOverlay();
            return;
        }

        if (_selectedIndex < 0 || _selectedIndex >= _filteredRoms.Count)
            return;

        StopGalleryMode();
        PopulateRomInfoOverlay(_filteredRoms[_selectedIndex]);
        RomInfoOverlay.IsVisible = true;
    }

    private void DismissRomInfoOverlay()
    {
        RomInfoOverlay.IsVisible = false;
        _checksumCts?.Cancel();
        _checksumCts?.Dispose();
        _checksumCts = null;
    }

    private void PopulateRomInfoOverlay(RomInfo rom)
    {
        var loc = LocalizationManager.Instance;
        RomInfoOverlayTitle.Text = loc["BigPicture_RomInfoTitle"];
        RomInfoDismiss.Text = loc["BigPicture_RomInfoDismiss"];

        // --- ROM Details ---
        RomInfoDetailsSection.Children.Clear();
        AddRomInfoSectionHeader(RomInfoDetailsSection, loc["BigPicture_RomInfoDetails"]);
        AddRomInfoRow(RomInfoDetailsSection, loc["BigPicture_RomInfoFileName"],
            Path.GetFileNameWithoutExtension(rom.FileName));
        AddRomInfoRow(RomInfoDetailsSection, loc["BigPicture_RomInfoSystem"], rom.SystemName);
        AddRomInfoRow(RomInfoDetailsSection, loc["BigPicture_RomInfoFileSize"], rom.FileSizeFormatted);
        AddRomInfoRow(RomInfoDetailsSection, loc["BigPicture_RomInfoStatus"],
            rom.IsValid ? loc["BigPicture_ValidRom"] : loc["BigPicture_InvalidRom"]);

        if (rom.HeaderInfo.Count > 0)
        {
            foreach (var kvp in rom.HeaderInfo)
                AddRomInfoRow(RomInfoDetailsSection, kvp.Key, kvp.Value);
        }

        long playTimeSec = AppSettings.Instance.GetPlayTime(rom.FilePath);
        if (playTimeSec > 0)
            AddRomInfoRow(RomInfoDetailsSection, loc["BigPicture_PlayTimeLabel"], FormatPlayTime(playTimeSec));

        int rating = AppSettings.Instance.GetRating(rom.FilePath);
        AddRomInfoRow(RomInfoDetailsSection, loc["BigPicture_RatingLabel"],
            rating > 0 ? string.Format(loc["BigPicture_RatingDisplay"], FormatRatingStars(rating), rating) : loc["BigPicture_Unrated"]);

        // --- Checksums ---
        RomInfoChecksumsSection.Children.Clear();
        AddRomInfoSectionHeader(RomInfoChecksumsSection, loc["BigPicture_RomInfoChecksums"]);

        string computing = loc["BigPicture_RomInfoComputing"];
        var crc32Text = AddRomInfoRow(RomInfoChecksumsSection, "CRC32", computing);
        var md5Text = AddRomInfoRow(RomInfoChecksumsSection, "MD5", computing);
        var sha1Text = AddRomInfoRow(RomInfoChecksumsSection, "SHA-1", computing);
        var sha256Text = AddRomInfoRow(RomInfoChecksumsSection, "SHA-256", computing);

        _checksumCts?.Cancel();
        _checksumCts?.Dispose();
        _checksumCts = new CancellationTokenSource();
        var token = _checksumCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                var result = await ChecksumCalculator.CalculateAsync(rom.FilePath).ConfigureAwait(false);
                if (token.IsCancellationRequested) return;

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (token.IsCancellationRequested) return;
                    crc32Text.Text = result.CRC32;
                    md5Text.Text = result.MD5;
                    sha1Text.Text = result.SHA1;
                    sha256Text.Text = result.SHA256;
                });
            }
            catch (Exception) when (token.IsCancellationRequested)
            {
                // Cancelled — ignore
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FileNotFoundException)
            {
                string error = loc["BigPicture_RomInfoChecksumError"];
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (token.IsCancellationRequested) return;
                    crc32Text.Text = error;
                    md5Text.Text = error;
                    sha1Text.Text = error;
                    sha256Text.Text = error;
                });
            }
        }, token);

        // --- GoodTools Codes ---
        RomInfoGoodToolsSection.Children.Clear();
        AddRomInfoSectionHeader(RomInfoGoodToolsSection, loc["BigPicture_RomInfoGoodTools"]);

        var gtResult = GoodToolsIdentifier.Identify(rom.FileName);
        if (!gtResult.HasCodes)
        {
            AddRomInfoRow(RomInfoGoodToolsSection, "", loc["BigPicture_RomInfoNoGoodTools"]);
        }
        else
        {
            if (gtResult.CountryCodes.Count > 0)
            {
                AddRomInfoSubHeader(RomInfoGoodToolsSection, loc["BigPicture_RomInfoCountryCodes"]);
                foreach (var code in gtResult.CountryCodes)
                    AddRomInfoRow(RomInfoGoodToolsSection, $"({code.Code})", code.Description);
            }

            if (gtResult.StandardCodes.Count > 0)
            {
                AddRomInfoSubHeader(RomInfoGoodToolsSection, loc["BigPicture_RomInfoStandardCodes"]);
                foreach (var code in gtResult.StandardCodes)
                {
                    string bracket = code.InParentheses ? $"({code.Code})" : $"[{code.Code}]";
                    AddRomInfoRow(RomInfoGoodToolsSection, bracket, code.Description);
                }
            }

            if (gtResult.GoodGenCodes.Count > 0)
            {
                AddRomInfoSubHeader(RomInfoGoodToolsSection, loc["BigPicture_RomInfoGoodGenCodes"]);
                foreach (var code in gtResult.GoodGenCodes)
                    AddRomInfoRow(RomInfoGoodToolsSection, $"({code.Code})", code.Description);
            }
        }
    }

    private static void AddRomInfoSectionHeader(StackPanel parent, string text)
    {
        parent.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            Foreground = RomInfoSectionHeaderForeground,
            Margin = new Thickness(0, 4, 0, 4)
        });
    }

    private static void AddRomInfoSubHeader(StackPanel parent, string text)
    {
        parent.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = RomInfoSubHeaderForeground,
            Margin = new Thickness(0, 4, 0, 2)
        });
    }

    private static TextBlock AddRomInfoRow(StackPanel parent, string label, string value)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        if (!string.IsNullOrEmpty(label))
        {
            panel.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 13,
                FontWeight = FontWeight.SemiBold,
                Foreground = RomInfoLabelForeground
            });
        }

        var valueBlock = new TextBlock
        {
            Text = value,
            FontSize = 13,
            Foreground = RomInfoValueForeground,
            TextWrapping = TextWrapping.Wrap
        };
        panel.Children.Add(valueBlock);
        parent.Children.Add(panel);
        return valueBlock;
    }

    // --- Keyboard & Gamepad Navigation ---
    // Native SDL2 game controller support provides autoconfig (plug & play).
    // Keyboard input is handled via OnKeyDownHandler; gamepad input is handled
    // via OnGamepadAction which is wired to GamepadService.ActionTriggered.

    private void OnKeyDownHandler(object? sender, KeyEventArgs e)
    {
        ResetInactivityTimer();

        // Dismiss screensaver on any key press
        if (_screensaverActive)
        {
            DismissScreensaver();
            e.Handled = true;
            return;
        }

        // Dismiss ROM info overlay
        if (RomInfoOverlay.IsVisible)
        {
            if (e.Key is Key.I or Key.Escape)
            {
                DismissRomInfoOverlay();
                e.Handled = true;
            }
            else
            {
                e.Handled = true;
            }
            return;
        }

        // Dismiss help overlay on any key except the toggle keys
        if (HelpOverlay.IsVisible)
        {
            if (e.Key is Key.H or Key.OemQuestion or Key.Escape)
            {
                HelpOverlay.IsVisible = false;
                e.Handled = true;
            }
            else
            {
                // Swallow all other keys while overlay is visible
                e.Handled = true;
            }
            return;
        }

        // Dismiss stats overlay
        if (StatsOverlay.IsVisible)
        {
            if (e.Key is Key.C or Key.Escape)
            {
                DismissStatsOverlay();
                e.Handled = true;
            }
            else
            {
                e.Handled = true;
            }
            return;
        }

        // Dismiss artwork viewer overlay
        if (ArtworkViewerOverlay.IsVisible)
        {
            if (e.Key is Key.V or Key.Escape)
            {
                DismissArtworkViewer();
                e.Handled = true;
            }
            else if (e.Key == Key.Left)
            {
                CycleArtworkViewer(-1);
                e.Handled = true;
            }
            else if (e.Key == Key.Right)
            {
                CycleArtworkViewer(1);
                e.Handled = true;
            }
            else
            {
                e.Handled = true;
            }
            return;
        }

        // When search box is focused, only handle Escape to return focus to the grid
        if (SearchBox.IsFocused)
        {
            if (e.Key == Key.Escape)
            {
                Focus();
                e.Handled = true;
            }
            return;
        }

        // Stop gallery mode on any user-initiated interaction key.
        // Overlay dismiss blocks above return early, so this only runs
        // when no overlay is visible and the key reaches normal handling.
        if (_galleryActive && e.Key is Key.Left or Key.Right or Key.Up or Key.Down
            or Key.Home or Key.End or Key.PageUp or Key.PageDown
            or Key.Escape or Key.Back or Key.Enter or Key.Space
            or Key.F or Key.R or Key.P
            or Key.V or Key.D0 or Key.D1 or Key.D2 or Key.D3 or Key.D4 or Key.D5
            or Key.NumPad0 or Key.NumPad1 or Key.NumPad2 or Key.NumPad3 or Key.NumPad4 or Key.NumPad5)
        {
            StopGalleryMode();

            // Escape / Back should only stop the gallery on the first press,
            // not also exit Big Picture Mode.
            if (e.Key is Key.Escape or Key.Back)
            {
                e.Handled = true;
                return;
            }
        }

        switch (e.Key)
        {
            case Key.Left:
                NavigateCards(-1);
                e.Handled = true;
                break;

            case Key.Right:
                NavigateCards(1);
                e.Handled = true;
                break;

            case Key.Up:
                NavigateCardsRow(-1);
                e.Handled = true;
                break;

            case Key.Down:
                NavigateCardsRow(1);
                e.Handled = true;
                break;

            case Key.Enter:
            case Key.Space:
                LaunchSelectedRom();
                e.Handled = true;
                break;

            case Key.Escape:
            case Key.Back:
                ExitBigPictureMode();
                e.Handled = true;
                break;

            case Key.Tab:
                SearchBox.Focus();
                e.Handled = true;
                break;

            case Key.Home:
                if (_filteredRoms.Count > 0) SelectCard(0);
                e.Handled = true;
                break;

            case Key.End:
                if (_filteredRoms.Count > 0) SelectCard(_filteredRoms.Count - 1);
                e.Handled = true;
                break;

            case Key.PageUp:
                NavigateCardsRow(-PageNavigationRows);
                e.Handled = true;
                break;

            case Key.PageDown:
                NavigateCardsRow(PageNavigationRows);
                e.Handled = true;
                break;

            case Key.F:
                ToggleSelectedFavorite();
                e.Handled = true;
                break;

            case Key.OemPlus:
            case Key.Add:
                ZoomIn();
                e.Handled = true;
                break;

            case Key.OemMinus:
            case Key.Subtract:
                ZoomOut();
                e.Handled = true;
                break;

            case Key.H:
            case Key.OemQuestion:
                ToggleHelpOverlay();
                e.Handled = true;
                break;

            case Key.I:
                ToggleRomInfoOverlay();
                e.Handled = true;
                break;

            case Key.R:
                SelectRandomGame();
                e.Handled = true;
                break;

            case Key.P:
                ToggleRecentlyPlayedBar();
                e.Handled = true;
                break;

            case Key.C:
                ToggleStatsOverlay();
                e.Handled = true;
                break;

            case Key.OemOpenBrackets:
                CycleSystemFilter(-1);
                e.Handled = true;
                break;

            case Key.OemCloseBrackets:
                CycleSystemFilter(1);
                e.Handled = true;
                break;

            case Key.G:
                ToggleGalleryMode();
                e.Handled = true;
                break;

            case Key.V:
                ToggleArtworkViewer();
                e.Handled = true;
                break;

            case Key.D0: case Key.NumPad0:
                RateSelectedGame(0);
                e.Handled = true;
                break;
            case Key.D1: case Key.NumPad1:
                RateSelectedGame(1);
                e.Handled = true;
                break;
            case Key.D2: case Key.NumPad2:
                RateSelectedGame(2);
                e.Handled = true;
                break;
            case Key.D3: case Key.NumPad3:
                RateSelectedGame(3);
                e.Handled = true;
                break;
            case Key.D4: case Key.NumPad4:
                RateSelectedGame(4);
                e.Handled = true;
                break;
            case Key.D5: case Key.NumPad5:
                RateSelectedGame(5);
                e.Handled = true;
                break;

            default:
                // Quick letter jump: A–Z keys that are NOT handled by explicit
                // cases above (F, G, H, I, P, R, V already have dedicated actions and
                // will never reach this default branch).
                if (e.Key >= Key.A && e.Key <= Key.Z)
                {
                    char letter = (char)('A' + (e.Key - Key.A));
                    JumpToLetter(letter);
                    e.Handled = true;
                }
                break;
        }
    }

    private void NavigateCards(int delta)
    {
        if (_galleryActive) StopGalleryMode();
        if (_filteredRoms.Count == 0) return;

        int newIndex = _selectedIndex + delta;
        if (newIndex >= 0 && newIndex < _filteredRoms.Count)
            SelectCard(newIndex);
    }

    private void NavigateCardsRow(int rowDelta)
    {
        if (_galleryActive) StopGalleryMode();
        if (_filteredRoms.Count == 0) return;

        // Estimate cards per row based on panel width
        double panelWidth = GameCardsPanel.Bounds.Width;
        if (panelWidth <= 0)
        {
            double available = Bounds.Width;
            // Only subtract detail panel width when it is actually visible
            if (DetailPanel.IsVisible)
                available -= DetailPanelWidth;
            // Ensure the fallback width is positive
            panelWidth = Math.Max(available, CardWidth + CardSpacing);
        }
        int cardsPerRow = Math.Max(1, (int)(panelWidth / (CardWidth + CardSpacing)));

        int newIndex = _selectedIndex + (rowDelta * cardsPerRow);
        // Clamp to valid range so navigating past the last/first row still
        // moves the selection to the last/first card instead of doing nothing.
        newIndex = Math.Clamp(newIndex, 0, _filteredRoms.Count - 1);
        if (newIndex != _selectedIndex)
            SelectCard(newIndex);
    }

    /// <summary>
    /// Initialises the inactivity timer that triggers the screensaver after
    /// a configurable number of minutes with no user input.
    /// </summary>
    private void InitialiseInactivityTimer()
    {
        int timeoutMinutes = AppSettings.Instance.BigPictureScreensaverTimeout;
        if (timeoutMinutes <= 0) return;

        _inactivityTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMinutes(timeoutMinutes)
        };
        _inactivityTimer.Tick += OnInactivityTimerTick;
        _inactivityTimer.Start();
    }

    /// <summary>
    /// Resets the inactivity timer so the screensaver countdown restarts
    /// from zero. Called on every user interaction.
    /// </summary>
    private void ResetInactivityTimer()
    {
        if (_inactivityTimer is not { IsEnabled: true }) return;
        _inactivityTimer.Stop();
        _inactivityTimer.Start();
    }

    private void StopInactivityTimer()
    {
        if (_inactivityTimer != null)
        {
            _inactivityTimer.Stop();
            _inactivityTimer.Tick -= OnInactivityTimerTick;
            _inactivityTimer = null;
        }
    }

    private void OnInactivityTimerTick(object? sender, EventArgs e) => StartScreensaver();

    /// <summary>
    /// Activates the screensaver overlay and begins cycling through random
    /// game artwork at a fixed interval.
    /// </summary>
    private void StartScreensaver()
    {
        if (_screensaverActive || _allRoms.Count == 0) return;

        StopGalleryMode();

        // Dismiss any active overlays before showing the screensaver
        if (HelpOverlay.IsVisible) HelpOverlay.IsVisible = false;
        if (RomInfoOverlay.IsVisible) DismissRomInfoOverlay();
        if (StatsOverlay.IsVisible) DismissStatsOverlay();
        if (ArtworkViewerOverlay.IsVisible) DismissArtworkViewer();
        LetterJumpOverlay.IsVisible = false;
        SystemCycleOverlay.IsVisible = false;
        _letterJumpCts?.Cancel();
        _systemCycleCts?.Cancel();

        _screensaverActive = true;

        // Stop the inactivity timer while the screensaver is active to avoid
        // wasteful ticks that call StartScreensaver() only to return early.
        StopInactivityTimer();

        var loc = LocalizationManager.Instance;
        ScreensaverDismiss.Text = loc["BigPicture_ScreensaverDismiss"];
        ScreensaverOverlay.IsVisible = true;

        CycleScreensaverArtwork();

        // Defensive: stop any leftover cycle timer before creating a new one
        StopScreensaverCycleTimer();
        _screensaverCycleTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(ScreensaverCycleMs)
        };
        _screensaverCycleTimer.Tick += OnScreensaverCycleTimerTick;
        _screensaverCycleTimer.Start();
    }

    /// <summary>
    /// Selects a random ROM and displays its box-art (or a placeholder)
    /// in the screensaver overlay.
    /// </summary>
    private void CycleScreensaverArtwork()
    {
        if (_allRoms.Count == 0) return;

        int index;
        if (_allRoms.Count == 1)
        {
            index = 0;
        }
        else
        {
            // Avoid showing the same ROM twice in a row
            do
            {
                index = Random.Shared.Next(_allRoms.Count);
            } while (index == _lastScreensaverRomIndex);
        }
        _lastScreensaverRomIndex = index;

        var rom = _allRoms[index];
        ScreensaverTitle.Text = Path.GetFileNameWithoutExtension(rom.FileName);
        ScreensaverSystem.Text = rom.SystemName;

        // Use cached thumbnail if available; otherwise clear the image
        if (_thumbnailCache.TryGetValue(rom.FilePath, out var bitmap))
        {
            ScreensaverImage.Source = bitmap;
        }
        else
        {
            ScreensaverImage.Source = null;
        }
    }

    /// <summary>
    /// Dismisses the screensaver and returns to the normal Big Picture view.
    /// </summary>
    private void DismissScreensaver()
    {
        if (!_screensaverActive) return;
        _screensaverActive = false;
        _lastScreensaverRomIndex = -1;

        StopScreensaverCycleTimer();
        ScreensaverOverlay.IsVisible = false;
        ScreensaverImage.Source = null;

        // Restart the inactivity timer so the screensaver can activate again
        // after the next period of inactivity.
        InitialiseInactivityTimer();
    }

    private void StopScreensaverCycleTimer()
    {
        if (_screensaverCycleTimer != null)
        {
            _screensaverCycleTimer.Stop();
            _screensaverCycleTimer.Tick -= OnScreensaverCycleTimerTick;
            _screensaverCycleTimer = null;
        }
    }

    private void OnScreensaverCycleTimerTick(object? sender, EventArgs e) => CycleScreensaverArtwork();

    /// <summary>
    /// Jumps to the first ROM in the filtered list whose display name
    /// starts with the given letter. Shows a brief on-screen indicator.
    /// </summary>
    private void JumpToLetter(char letter)
    {
        if (_filteredRoms.Count == 0) return;

        string prefix = letter.ToString();
        int index = _filteredRoms.FindIndex(r =>
            Path.GetFileNameWithoutExtension(r.FileName)
                .StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        if (index >= 0)
            SelectCard(index);

        ShowLetterJumpIndicator(letter);
    }

    /// <summary>
    /// Shows the letter jump indicator briefly on screen, then auto-hides.
    /// </summary>
    private async void ShowLetterJumpIndicator(char letter)
    {
        try
        {
            _letterJumpCts?.Cancel();
            _letterJumpCts?.Dispose();
            _letterJumpCts = new CancellationTokenSource();
            var token = _letterJumpCts.Token;

            LetterJumpText.Text = letter.ToString();
            LetterJumpOverlay.IsVisible = true;

            await Task.Delay(LetterJumpDisplayMs, token);
            if (!token.IsCancellationRequested)
                LetterJumpOverlay.IsVisible = false;
        }
        catch (OperationCanceledException)
        {
            // A new letter was pressed before the timeout — the new call
            // will manage the overlay visibility.
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[BigPicture] ShowLetterJumpIndicator failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Toggles the recently played bar visibility on/off.
    /// </summary>
    private void ToggleRecentlyPlayedBar()
    {
        _recentlyPlayedBarVisible = !_recentlyPlayedBarVisible;

        if (_recentlyPlayedBarVisible)
            PopulateRecentlyPlayedBar();
        else
            RecentlyPlayedBar.IsVisible = false;
    }

    /// <summary>
    /// Populates the recently played quick-access bar with mini-cards
    /// for the most recently launched ROMs that exist in the current
    /// collection. Shows the bar only if there is at least one match.
    /// </summary>
    private void PopulateRecentlyPlayedBar()
    {
        if (!_recentlyPlayedBarVisible) return;

        var loc = LocalizationManager.Instance;
        RecentlyPlayedBarTitle.Text = string.Format(loc["BigPicture_RecentlyPlayedBarIcon"], loc["BigPicture_RecentlyPlayedBar"]);

        var recentPaths = AppSettings.Instance.RecentlyPlayed;
        var romLookup = new Dictionary<string, RomInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var rom in _allRoms)
            romLookup.TryAdd(rom.FilePath, rom);

        RecentlyPlayedPanel.Children.Clear();

        int added = 0;
        foreach (string path in recentPaths)
        {
            if (added >= RecentlyPlayedMaxCards) break;
            if (!romLookup.TryGetValue(path, out var rom)) continue;

            var card = CreateRecentlyPlayedCard(rom);
            RecentlyPlayedPanel.Children.Add(card);
            added++;
        }

        if (added == 0)
        {
            RecentlyPlayedPanel.Children.Add(new TextBlock
            {
                Text = loc["BigPicture_NoRecentlyPlayed"],
                Foreground = CardSizeForeground,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0)
            });
        }

        RecentlyPlayedBar.IsVisible = true;
    }

    /// <summary>
    /// Finds a ROM by file path in the current filtered list and selects it.
    /// If the ROM is not visible because of active filters, the filters are
    /// cleared so the ROM becomes visible before selecting.
    /// </summary>
    private void SelectRecentlyPlayedRom(RomInfo rom)
    {
        Predicate<RomInfo> match = r =>
            string.Equals(r.FilePath, rom.FilePath, StringComparison.OrdinalIgnoreCase);

        int idx = _filteredRoms.FindIndex(match);

        if (idx < 0)
        {
            // ROM not visible due to active filters — reset all filters.
            // Suppress individual ApplyFilterAndSort triggers so we only
            // rebuild the grid once after all filters are cleared.
            _suppressFilterUpdate = true;
            SearchBox.Text = string.Empty;
            SystemFilterCombo.SelectedIndex = 0;
            FavoritesFilterButton.IsChecked = false;
            _suppressFilterUpdate = false;

            // Cancel any pending debounced search since we apply all filters below
            _searchDebounceCts?.Cancel();

            ApplyFilterAndSort();

            idx = _filteredRoms.FindIndex(match);
        }

        if (idx >= 0)
            SelectCard(idx);
    }

    /// <summary>
    /// Creates a compact card for the recently played bar with optional
    /// thumbnail artwork and the ROM's display name.
    /// </summary>
    private Border CreateRecentlyPlayedCard(RomInfo rom)
    {
        var cardImage = new Image
        {
            Stretch = Stretch.UniformToFill,
            IsVisible = false
        };

        if (_thumbnailCache.TryGetValue(rom.FilePath, out var cached))
        {
            cardImage.Source = cached;
            cardImage.IsVisible = true;
        }

        string systemInitial = !string.IsNullOrEmpty(rom.SystemName)
            ? rom.SystemName[..1].ToUpperInvariant()
            : "?";

        var card = new Border
        {
            Width = RecentlyPlayedCardWidth,
            Height = RecentlyPlayedCardHeight,
            Background = CardDefaultBackground,
            CornerRadius = new CornerRadius(6),
            Cursor = HandCursor,
            ClipToBounds = true,
            Child = new Grid
            {
                Children =
                {
                    // System colour banner or artwork
                    new Border
                    {
                        Background = GetSystemBrush(rom.System),
                        Child = new Grid
                        {
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = systemInitial,
                                    FontSize = 28,
                                    FontWeight = FontWeight.Bold,
                                    Foreground = CardInitialForeground,
                                    Opacity = 0.3,
                                    HorizontalAlignment = HorizontalAlignment.Center,
                                    VerticalAlignment = VerticalAlignment.Center
                                },
                                cardImage
                            }
                        }
                    },
                    // Semi-transparent name overlay at the bottom
                    new Border
                    {
                        Background = RecentlyPlayedNameOverlayBackground,
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Padding = new Thickness(6, 3),
                        Child = new TextBlock
                        {
                            Text = Path.GetFileNameWithoutExtension(rom.FileName),
                            FontSize = 10,
                            Foreground = CardNameForeground,
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            MaxLines = 1
                        }
                    }
                }
            }
        };

        card.PointerPressed += (_, _) =>
        {
            SelectRecentlyPlayedRom(rom);
            Focus();
        };

        card.DoubleTapped += (_, _) =>
        {
            SelectRecentlyPlayedRom(rom);
            LaunchSelectedRom();
        };

        return card;
    }

    private void ToggleStatsOverlay()
    {
        if (StatsOverlay.IsVisible)
        {
            DismissStatsOverlay();
        }
        else
        {
            StopGalleryMode();
            PopulateStatsOverlay();
            StatsOverlay.IsVisible = true;
        }
    }

    private void DismissStatsOverlay()
    {
        StatsOverlay.IsVisible = false;
    }

    private void PopulateStatsOverlay()
    {
        var loc = LocalizationManager.Instance;
        StatsOverlayTitle.Text = loc["BigPicture_StatsTitle"];
        StatsDismiss.Text = loc["BigPicture_StatsDismiss"];

        StatsContent.Children.Clear();

        // Total ROMs
        AddStatsRow(StatsContent, loc["BigPicture_StatsTotalRoms"], _allRoms.Count.ToString("N0"));

        // Filtered ROMs (if filtering is active)
        if (_filteredRoms.Count != _allRoms.Count)
            AddStatsRow(StatsContent, loc["BigPicture_StatsFilteredRoms"], _filteredRoms.Count.ToString("N0"));

        // Favorites count
        var favorites = AppSettings.Instance.Favorites;
        int favCount = _allRoms.Count(r => favorites.Contains(r.FilePath));
        AddStatsRow(StatsContent, loc["BigPicture_StatsFavorites"], favCount.ToString("N0"));

        // Total file size
        long totalSize = _allRoms.Sum(r => r.FileSize);
        AddStatsRow(StatsContent, loc["BigPicture_StatsTotalSize"], FormatFileSize(totalSize));

        // Average file size
        if (_allRoms.Count > 0)
        {
            long avgSize = totalSize / _allRoms.Count;
            AddStatsRow(StatsContent, loc["BigPicture_StatsAvgSize"], FormatFileSize(avgSize));
        }

        // Total play time
        long totalPlayTime = 0;
        int ratedCount = 0;
        double ratingSum = 0;
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

        // Separator
        StatsContent.Children.Add(new Border
        {
            Background = CardSelectedBackground,
            Height = 1,
            Margin = new Thickness(0, 8)
        });

        // Per-system breakdown header
        StatsContent.Children.Add(new TextBlock
        {
            Text = loc["BigPicture_StatsPerSystem"],
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            Foreground = RomInfoSectionHeaderForeground,
            Margin = new Thickness(0, 4)
        });

        // Per-system breakdown
        var systemGroups = _allRoms
            .GroupBy(r => r.SystemName)
            .OrderByDescending(g => g.Count())
            .ToList();

        foreach (var group in systemGroups)
        {
            long groupSize = group.Sum(r => r.FileSize);
            string detail = $"{group.Count():N0}  ({FormatFileSize(groupSize)})";
            AddStatsRow(StatsContent, group.Key, detail);
        }
    }

    private static void AddStatsRow(StackPanel parent, string label, string value)
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(0, 2)
        };

        var labelBlock = new TextBlock
        {
            Text = label,
            FontSize = 14,
            Foreground = RomInfoLabelForeground,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(labelBlock, 0);

        var valueBlock = new TextBlock
        {
            Text = value,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            Foreground = RomInfoValueForeground,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(24, 0, 0, 0)
        };
        Grid.SetColumn(valueBlock, 1);

        row.Children.Add(labelBlock);
        row.Children.Add(valueBlock);
        parent.Children.Add(row);
    }

    private static readonly string[] FileSizeUnits = ["B", "KB", "MB", "GB", "TB"];

    private static string FormatFileSize(long bytes)
    {
        double size = bytes;
        int unitIndex = 0;
        while (size >= 1024 && unitIndex < FileSizeUnits.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }
        return $"{size:F1} {FileSizeUnits[unitIndex]}";
    }

    /// <summary>
    /// Cycles the system filter dropdown forward or backward, wrapping
    /// around at the ends. Shows a brief on-screen indicator with the
    /// selected system name.
    /// </summary>
    private void CycleSystemFilter(int direction)
    {
        if (SystemFilterCombo.Items.Count == 0) return;

        StopGalleryMode();

        int count = SystemFilterCombo.Items.Count;
        int current = SystemFilterCombo.SelectedIndex;
        int next = (current + direction + count) % count;

        SystemFilterCombo.SelectedIndex = next;

        // Read the display name from the newly selected item
        string systemName = (SystemFilterCombo.SelectedItem as ComboBoxItem)?.Content?.ToString()
                            ?? string.Empty;

        ShowSystemCycleIndicator(systemName);
    }

    /// <summary>
    /// Shows the system cycle indicator briefly on screen, then auto-hides.
    /// </summary>
    private async void ShowSystemCycleIndicator(string systemName)
    {
        try
        {
            _systemCycleCts?.Cancel();
            _systemCycleCts?.Dispose();
            _systemCycleCts = new CancellationTokenSource();
            var token = _systemCycleCts.Token;

            SystemCycleText.Text = systemName;
            SystemCycleOverlay.IsVisible = true;

            await Task.Delay(SystemCycleDisplayMs, token);
            if (!token.IsCancellationRequested)
                SystemCycleOverlay.IsVisible = false;
        }
        catch (OperationCanceledException)
        {
            // A new cycle was triggered before the timeout — the new call
            // will manage the overlay visibility.
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[BigPicture] ShowSystemCycleIndicator failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Toggles the auto-scroll gallery mode on or off. When active,
    /// the view automatically advances to the next card at a fixed
    /// interval, wrapping to the beginning when the end is reached.
    /// </summary>
    private void ToggleGalleryMode()
    {
        if (_galleryActive)
        {
            StopGalleryMode();
        }
        else
        {
            StartGalleryMode();
        }
    }

    private void StartGalleryMode()
    {
        if (_filteredRoms.Count == 0) return;

        // Defensive: fully dispose any existing timer to prevent event-handler
        // leaks if this method is reached while a timer is already running.
        if (_galleryTimer != null)
        {
            _galleryTimer.Stop();
            _galleryTimer.Tick -= OnGalleryTimerTick;
            _galleryTimer = null;
        }
        _galleryActive = true;

        var loc = LocalizationManager.Instance;
        StatusText.Text = loc["BigPicture_GalleryActive"];

        _galleryTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(GalleryIntervalMs)
        };
        _galleryTimer.Tick += OnGalleryTimerTick;
        _galleryTimer.Start();
    }

    private void StopGalleryMode()
    {
        if (!_galleryActive) return;
        _galleryActive = false;

        if (_galleryTimer != null)
        {
            _galleryTimer.Stop();
            _galleryTimer.Tick -= OnGalleryTimerTick;
            _galleryTimer = null;
        }

        var loc = LocalizationManager.Instance;
        StatusText.Text = loc["BigPicture_GalleryStopped"];
    }

    private void OnGalleryTimerTick(object? sender, EventArgs e) => GalleryAdvance();

    private void GalleryAdvance()
    {
        if (_filteredRoms.Count == 0)
        {
            StopGalleryMode();
            return;
        }

        int next = _selectedIndex + 1;
        if (next >= _filteredRoms.Count)
            next = 0;

        SelectCard(next);
    }

    /// <summary>
    /// Formats a total number of seconds into a human-readable playtime
    /// string (e.g. "2h 15m", "45m", "< 1m").
    /// </summary>
    private static string FormatPlayTime(long totalSeconds)
    {
        if (totalSeconds <= 0) return "—";
        if (totalSeconds < 60) return "< 1m";

        long hours = totalSeconds / 3600;
        long minutes = (totalSeconds % 3600) / 60;

        if (hours > 0)
            return $"{hours}h {minutes}m";
        return $"{minutes}m";
    }

    private void ToggleArtworkViewer()
    {
        if (ArtworkViewerOverlay.IsVisible)
        {
            DismissArtworkViewer();
            return;
        }

        if (_selectedIndex < 0 || _selectedIndex >= _filteredRoms.Count)
            return;

        StopGalleryMode();
        OpenArtworkViewer(_filteredRoms[_selectedIndex]);
    }

    private void OpenArtworkViewer(RomInfo rom)
    {
        var loc = LocalizationManager.Instance;

        // Collect available artwork from the detail panel images
        _artworkViewerImages[0] = DetailBoxArt.Source as Bitmap;
        _artworkViewerImages[1] = DetailScreenshot.Source as Bitmap;
        _artworkViewerImages[2] = DetailTitleScreen.Source as Bitmap;

        _artworkViewerLabels[0] = loc["Browser_BoxArt"];
        _artworkViewerLabels[1] = loc["Browser_Screenshot"];
        _artworkViewerLabels[2] = loc["Browser_TitleScreen"];

        // Bail out if no artwork is available
        bool hasAny = false;
        for (int i = 0; i < ArtworkViewerImageCount; i++)
        {
            if (_artworkViewerImages[i] != null) { hasAny = true; break; }
        }
        if (!hasAny) return;

        ArtworkViewerTitle.Text = Path.GetFileNameWithoutExtension(rom.FileName);
        ArtworkViewerDismiss.Text = loc["BigPicture_ArtworkViewerDismiss"];

        // Start at the first available image
        _artworkViewerIndex = 0;
        for (int i = 0; i < ArtworkViewerImageCount; i++)
        {
            if (_artworkViewerImages[i] != null)
            {
                _artworkViewerIndex = i;
                break;
            }
        }

        UpdateArtworkViewerDisplay();
        ArtworkViewerOverlay.IsVisible = true;
    }

    private void CycleArtworkViewer(int direction)
    {
        // Skip null (unloaded) images when cycling
        int next = _artworkViewerIndex;
        for (int i = 0; i < ArtworkViewerImageCount; i++)
        {
            next = (next + direction + ArtworkViewerImageCount) % ArtworkViewerImageCount;
            if (_artworkViewerImages[next] != null)
                break;
        }
        _artworkViewerIndex = next;
        UpdateArtworkViewerDisplay();
    }

    private void UpdateArtworkViewerDisplay()
    {
        ArtworkViewerImage.Source = _artworkViewerImages[_artworkViewerIndex];
        ArtworkViewerLabel.Text = _artworkViewerLabels[_artworkViewerIndex];

        // Count only available (non-null) images and compute the 1-based position
        int available = 0;
        int position = 0;
        for (int i = 0; i < ArtworkViewerImageCount; i++)
        {
            if (_artworkViewerImages[i] != null)
            {
                available++;
                if (i == _artworkViewerIndex)
                    position = available;
            }
        }
        ArtworkViewerCounter.Text = string.Format(LocalizationManager.Instance["BigPicture_ArtworkCounter"], position, available);
    }

    private void DismissArtworkViewer()
    {
        ArtworkViewerOverlay.IsVisible = false;
        // Don't dispose images — they belong to the detail panel
        ArtworkViewerImage.Source = null;
        ClearArtworkViewerReferences();
    }

    /// <summary>
    /// Clears the artwork viewer image reference array so it does not hold
    /// stale references to bitmaps that may be disposed elsewhere.
    /// </summary>
    private void ClearArtworkViewerReferences()
    {
        Array.Clear(_artworkViewerImages);
    }

    private void RateSelectedGame(int rating)
    {
        if (!AppSettings.Instance.BigPictureRatingsEnabled) return;
        if (_selectedIndex < 0 || _selectedIndex >= _filteredRoms.Count) return;

        var rom = _filteredRoms[_selectedIndex];
        int current = AppSettings.Instance.GetRating(rom.FilePath);

        // Pressing the same rating again clears it
        if (current == rating)
            AppSettings.Instance.SetRating(rom.FilePath, 0);
        else
            AppSettings.Instance.SetRating(rom.FilePath, rating);

        // Refresh detail panel and the affected card
        RefreshRatingDisplay(rom);
    }

    private void CycleSelectedRating()
    {
        if (!AppSettings.Instance.BigPictureRatingsEnabled) return;
        if (_selectedIndex < 0 || _selectedIndex >= _filteredRoms.Count) return;

        var rom = _filteredRoms[_selectedIndex];
        int current = AppSettings.Instance.GetRating(rom.FilePath);
        // Cycle: 0→1→2→3→4→5→0 (clear after 5)
        int next = current >= 5 ? 0 : current + 1;
        AppSettings.Instance.SetRating(rom.FilePath, next);

        // Refresh detail panel and the affected card
        RefreshRatingDisplay(rom);
    }

    /// <summary>
    /// Refreshes the rating display in the detail panel and the selected card
    /// without rebuilding the entire card grid.
    /// </summary>
    private void RefreshRatingDisplay(RomInfo rom)
    {
        var loc = LocalizationManager.Instance;
        int newRating = AppSettings.Instance.GetRating(rom.FilePath);

        DetailRating.Text = FormatRatingStars(newRating) +
            (newRating > 0 ? $" ({newRating}/5)" : $" ({loc["BigPicture_Unrated"]})");

        // Update just the rating TextBlock on the selected card
        if (_selectedIndex >= 0 && _selectedIndex < GameCardsPanel.Children.Count &&
            GameCardsPanel.Children[_selectedIndex] is Border card &&
            card.Child is StackPanel cardStack &&
            cardStack.Children.Count >= 3 &&
            cardStack.Children[2] is StackPanel metaPanel &&
            metaPanel.Children.Count >= 3 &&
            metaPanel.Children[2] is TextBlock ratingBlock)
        {
            ratingBlock.Text = FormatRatingStars(newRating);
            ratingBlock.IsVisible = newRating > 0;
        }
    }

    /// <summary>
    /// Returns a star string representation for a rating (e.g. "★★★☆☆" for 3).
    /// </summary>
    private static string FormatRatingStars(int rating)
    {
        if (rating <= 0) return "☆☆☆☆☆";
        return new string('★', Math.Min(rating, 5)) + new string('☆', Math.Max(0, 5 - rating));
    }
}
