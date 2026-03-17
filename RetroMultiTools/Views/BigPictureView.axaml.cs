using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Models;
using RetroMultiTools.Services;
using RetroMultiTools.Utilities;

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
        KeyDown += OnKeyDownHandler;
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
        _searchDebounceCts?.Cancel();
        _searchDebounceCts?.Dispose();
        _searchDebounceCts = null;
        _checksumCts?.Cancel();
        _checksumCts?.Dispose();
        _checksumCts = null;
        CancelArtworkPreload();
        CancelArtworkLoading();
        ClearArtworkImages();
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

    private void OnGamepadAction(GamepadAction action)
    {
        // When ROM info overlay is showing, only allow dismiss
        if (RomInfoOverlay.IsVisible)
        {
            if (action is GamepadAction.Back or GamepadAction.RomInfo)
                DismissRomInfoOverlay();
            return;
        }

        // When help overlay is showing, only allow dismiss
        if (HelpOverlay.IsVisible)
        {
            if (action is GamepadAction.Back or GamepadAction.Help)
                HelpOverlay.IsVisible = false;
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
        SortCombo.SelectedIndex = 0;
    }

    private async void BrowseFolderButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
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

        UpdateFavoriteButton(rom);

        bool canLaunch = RetroArchLauncher.IsSystemSupported(rom.System);
        DetailLaunchButton.IsEnabled = canLaunch;

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
        (DetailBoxArt.Source as Bitmap)?.Dispose();
        (DetailScreenshot.Source as Bitmap)?.Dispose();
        (DetailTitleScreen.Source as Bitmap)?.Dispose();
        DetailBoxArt.Source = null;
        DetailScreenshot.Source = null;
        DetailTitleScreen.Source = null;
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
            AppSettings.Instance.RecordRecentlyPlayed(rom.FilePath);
            AppSettings.Instance.IncrementPlayCount(rom.FilePath);

            // Refresh play count in the detail panel
            int playCount = AppSettings.Instance.GetPlayCount(rom.FilePath);
            DetailPlayCount.Text = string.Format(loc["BigPicture_PlayCount"], playCount);

            if (result.Process != null)
            {
                if (AppSettings.Instance.MinimizeToTrayOnLaunch)
                {
                    MinimizeToTrayAndRestoreOnExit(result.Process);
                }
                else
                {
                    result.Process.Dispose();
                }
            }
        }
    }

    private void LaunchButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => LaunchSelectedRom();

    private async void MinimizeToTrayAndRestoreOnExit(System.Diagnostics.Process process)
    {
        try
        {
            if (TopLevel.GetTopLevel(this) is MainWindow mainWindow)
            {
                mainWindow.MinimizeToTray();

                await Task.Run(() =>
                {
                    try { process.WaitForExit(); }
                    catch (InvalidOperationException ex)
                    {
                        System.Diagnostics.Trace.WriteLine($"[BigPicture] Process monitoring ended: {ex.Message}");
                    }
                });

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    mainWindow.RestoreFromTray();
                    DiscordRichPresence.ClearPresence();
                });
            }
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
    }

    private void ToggleHelpOverlay()
    {
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
        }
    }

    private void NavigateCards(int delta)
    {
        if (_filteredRoms.Count == 0) return;

        int newIndex = _selectedIndex + delta;
        if (newIndex >= 0 && newIndex < _filteredRoms.Count)
            SelectCard(newIndex);
    }

    private void NavigateCardsRow(int rowDelta)
    {
        if (_filteredRoms.Count == 0) return;

        // Estimate cards per row based on panel width
        double panelWidth = GameCardsPanel.Bounds.Width;
        if (panelWidth <= 0)
        {
            double available = Bounds.Width;
            // Only subtract detail panel width when it is actually visible
            if (DetailPanel.IsVisible)
                available -= DetailPanelWidth;
            panelWidth = available;
        }
        int cardsPerRow = Math.Max(1, (int)(panelWidth / (CardWidth + CardSpacing)));

        int newIndex = _selectedIndex + (rowDelta * cardsPerRow);
        // Clamp to valid range so navigating past the last/first row still
        // moves the selection to the last/first card instead of doing nothing.
        newIndex = Math.Clamp(newIndex, 0, _filteredRoms.Count - 1);
        if (newIndex != _selectedIndex)
            SelectCard(newIndex);
    }
}
