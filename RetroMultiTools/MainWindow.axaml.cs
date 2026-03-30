using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using RetroMultiTools.Utilities;
using RetroMultiTools.Utilities.Patching;
using RetroMultiTools.Utilities.Verification;
using RetroMultiTools.Utilities.Conversion;
using RetroMultiTools.Utilities.RomManagement;
using RetroMultiTools.Utilities.Networking;
using RetroMultiTools.Views.Browsing;
using RetroMultiTools.Views.CheatsAndEmulation;
using RetroMultiTools.Views.Conversion;
using RetroMultiTools.Views.Dialogs;
using RetroMultiTools.Views.DiscTools;
using RetroMultiTools.Views.GamepadKeyMapper;
using RetroMultiTools.Views.Patching;
using RetroMultiTools.Views.RomManagement;
using RetroMultiTools.Views.Settings;
using RetroMultiTools.Views.UsbTools;
using RetroMultiTools.Views.Verification;
using RetroMultiTools.Views.Analogue;
using RetroMultiTools.Views.Mame;
using RetroMultiTools.Views.Mednafen;
using RetroMultiTools.Views.RetroArch;

namespace RetroMultiTools;

public partial class MainWindow : Window
{
    // Browse & Inspect
    private readonly BigPictureView _bigPictureView = new();
    private readonly BigPictureModernView _bigPictureModernView = new();
    private readonly HexViewerView _hexViewerView = new();
    private readonly RomBrowserView _browserView = new();
    private readonly RomInspectorView _inspectorView = new();

    // Patching & Conversion
    private readonly ArchiveManagerView _archiveManagerView = new();
    private readonly N64ConverterView _n64ConverterView = new();
    private readonly PatchCreatorView _patchCreatorView = new();
    private readonly RomFormatConverterView _formatConverterView = new();
    private readonly RomPatcherView _patcherView = new();
    private readonly SaveFileConverterView _saveFileConverterView = new();
    private readonly SplitRomAssemblerView _splitAssemblerView = new();

    // Analysis & Verification
    private readonly BatchHasherView _batchHasherView = new();
    private readonly ChecksumCalculatorView _checksumView = new();
    private readonly DatFilterView _datFilterView = new();
    private readonly DatVerifierView _datVerifierView = new();
    private readonly DumpVerifierView _dumpVerifierView = new();
    private readonly DuplicateFinderView _duplicateFinderView = new();
    private readonly GoodToolsIdentifierView _goodToolsIdentifierView = new();
    private readonly RomComparerView _comparerView = new();
    private readonly SecurityAnalyzerView _securityAnalyzerView = new();

    // Headers & Trimming
    private readonly BatchHeaderFixerView _batchHeaderFixerView = new();
    private readonly HeaderExporterView _headerExporterView = new();
    private readonly RomTrimmerView _romTrimmerView = new();
    private readonly SnesHeaderToolView _snesHeaderToolView = new();

    // Utilities
    private readonly CheatCodeView _cheatCodeView = new();
    private readonly DiscToolsView _discToolsView = new();
    private readonly EmulatorConfigView _emulatorConfigView = new();
    private readonly GamepadKeyMapperView _gamepadKeyMapperView = new();
    private readonly MetadataScraperView _metadataScraperView = new();
    private readonly RomOrganizerView _romOrganizerView = new();
    private readonly RomRenamerView _romRenamerView = new();
    private readonly UsbToolsView _usbToolsView = new();

    // RetroArch
    private readonly RetroAchievementsWriterView _retroAchievementsWriterView = new();
    private readonly RetroArchConfiguratorView _retroArchConfiguratorView = new();
    private readonly RetroArchIntegrationView _retroArchIntegrationView = new();
    private readonly RetroArchPlaylistView _retroArchPlaylistView = new();
    private readonly RetroArchShortcutView _retroArchShortcutView = new();

    // MAME
    private readonly MameChdConverterView _mameChdConverterView = new();
    private readonly MameChdVerifierView _mameChdVerifierView = new();
    private readonly MameConfiguratorView _mameConfiguratorView = new();
    private readonly MameDatEditorView _mameDatEditorView = new();
    private readonly MameDir2DatView _mameDir2DatView = new();
    private readonly MameIntegrationView _mameIntegrationView = new();
    private readonly MameRomAuditorView _mameAuditorView = new();
    private readonly MameSampleAuditorView _mameSampleAuditorView = new();
    private readonly MameSetRebuilderView _mameRebuilderView = new();

    // Mednafen
    private readonly MednafenConfiguratorView _mednafenConfiguratorView = new();
    private readonly MednafenIntegrationView _mednafenIntegrationView = new();

    // Analogue
    private readonly Analogue3DView _analogue3DView = new();
    private readonly AnalogueMegaSgView _analogueMegaSgView = new();
    private readonly AnalogueNtSuperNtView _analogueNtSuperNtView = new();
    private readonly AnaloguePocketView _analoguePocketView = new();

    // Settings
    private readonly SettingsView _settingsView = new();

    private WindowState _stateBeforeMinimize = WindowState.Normal;
    private bool _isInBigPictureMode;
    private Dictionary<TreeViewItem, bool>? _initialExpansionStates;

    /// <summary>
    /// Raised when <see cref="IsMinimizedToTray"/> changes.
    /// </summary>
    public event Action<bool>? IsMinimizedToTrayChanged;

    public MainWindow()
    {
        InitializeComponent();
        CacheInitialExpansionStates();
        SelectNavItem("browser");
        UpdateTitle();
        Localization.LocalizationManager.Instance.PropertyChanged += (_, _) => UpdateTitle();
        KeyDown += MainWindow_KeyDown;
    }

    private void MainWindow_KeyDown(object? sender, KeyEventArgs e)
    {
        // Ctrl+F / Ctrl+K — focus the sidebar search box
        if (e.KeyModifiers == KeyModifiers.Control &&
            (e.Key == Key.F || e.Key == Key.K))
        {
            if (Sidebar.IsVisible)
            {
                NavSearchBox.Focus();
                NavSearchBox.SelectAll();
                e.Handled = true;
            }
        }
        // F1 — show the guided tour
        else if (e.Key == Key.F1 && e.KeyModifiers == KeyModifiers.None)
        {
            ShowTour();
            e.Handled = true;
        }
        // Escape — clear search box if focused, or return focus to content
        else if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
        {
            if (NavSearchBox.IsFocused)
            {
                if (!string.IsNullOrEmpty(NavSearchBox.Text))
                    NavSearchBox.Text = "";
                else
                    MainContent.Focus();
                e.Handled = true;
            }
        }
    }

    private void UpdateTitle()
    {
        Title = $"{Localization.LocalizationManager.Instance["AppTitle"]} {AppUpdater.GetCurrentVersion()}";
    }

    /// <summary>
    /// Navigates to the view identified by the given tag string.
    /// Used by the NativeMenu to switch views from the application menu.
    /// </summary>
    public void NavigateToView(string tag)
    {
        var content = ResolveView(tag);
        if (content != null)
        {
            MainContent.Content = content;
            SelectNavItem(tag);
        }
    }

    /// <summary>
    /// Navigates to the view identified by the given tag and pre-fills it with a file path.
    /// Supported views: inspector, hexviewer, formatconv, trimmer, checksum, goodtools, patcher.
    /// </summary>
    public void NavigateToViewWithFile(string tag, string filePath)
    {
        NavigateToView(tag);

        switch (tag)
        {
            case "inspector":
                _inspectorView.SetInputFile(filePath);
                break;
            case "hexviewer":
                _hexViewerView.SetInputFile(filePath);
                break;
            case "formatconv":
                _formatConverterView.SetInputFile(filePath);
                break;
            case "trimmer":
                _romTrimmerView.SetInputFile(filePath);
                break;
            case "checksum":
                _checksumView.SetInputFile(filePath);
                break;
            case "goodtools":
                _goodToolsIdentifierView.SetInputFile(filePath);
                break;
            case "patcher":
                _patcherView.SetInputFile(filePath);
                break;
        }
    }

    /// <summary>
    /// Finds and selects the TreeViewItem with the given tag in the navigation tree.
    /// </summary>
    private void SelectNavItem(string tag)
    {
        foreach (var category in NavTreeView.Items.OfType<TreeViewItem>())
        {
            foreach (var child in category.Items.OfType<TreeViewItem>())
            {
                if (child.Tag?.ToString() == tag)
                {
                    category.IsExpanded = true;
                    NavTreeView.SelectedItem = child;
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Returns true when the window is currently hidden in the system tray.
    /// </summary>
    public bool IsMinimizedToTray { get; private set; }

    /// <summary>
    /// Minimizes the window to the system tray.
    /// </summary>
    public void MinimizeToTray()
    {
        if (IsMinimizedToTray) return;
        _stateBeforeMinimize = WindowState;
        IsMinimizedToTray = true;
        ShowInTaskbar = false;
        WindowState = WindowState.Minimized;
        Hide();
        IsMinimizedToTrayChanged?.Invoke(true);
    }

    /// <summary>
    /// Restores the window from the system tray.
    /// Does nothing if the window is already visible.
    /// </summary>
    public void RestoreFromTray()
    {
        if (!IsMinimizedToTray) return;
        IsMinimizedToTray = false;
        Show();
        ShowInTaskbar = true;
        WindowState = _stateBeforeMinimize;
        Activate();
        IsMinimizedToTrayChanged?.Invoke(false);
    }

    /// <summary>
    /// Enters Big Picture Mode: hides sidebar and title bar, shows the
    /// fullscreen-friendly ROM library, and makes the window fullscreen.
    /// </summary>
    public void EnterBigPictureMode(string folderPath = "", List<Models.RomInfo>? roms = null)
    {
        if (_isInBigPictureMode) return;
        _isInBigPictureMode = true;

        // Hide sidebar + title bar
        TitleBar.IsVisible = false;
        Sidebar.IsVisible = false;
        MainContent.Padding = new Avalonia.Thickness(0);

        // Show the appropriate Big Picture view based on style setting
        bool useModern = Services.AppSettings.Instance.BigPictureStyle == "Modern";
        if (useModern)
        {
            _bigPictureModernView.LoadFolder(folderPath, roms);
            MainContent.Content = _bigPictureModernView;
        }
        else
        {
            _bigPictureView.LoadFolder(folderPath, roms);
            MainContent.Content = _bigPictureView;
        }

        WindowState = WindowState.FullScreen;
    }

    /// <summary>
    /// Exits Big Picture Mode and returns to the normal windowed ROM Browser.
    /// </summary>
    public void ExitBigPictureMode()
    {
        if (!_isInBigPictureMode) return;
        _isInBigPictureMode = false;

        // Restore sidebar + title bar
        TitleBar.IsVisible = true;
        Sidebar.IsVisible = true;
        MainContent.Padding = new Avalonia.Thickness(16);

        // Navigate back to the ROM Browser
        MainContent.Content = _browserView;
        SelectNavItem("browser");

        WindowState = WindowState.Normal;
    }

    private void NavTreeView_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (NavTreeView.SelectedItem is TreeViewItem item && item.Tag != null)
        {
            var content = ResolveView(item.Tag?.ToString());
            if (content != null)
                MainContent.Content = content;
        }
    }

    private void NavSearchBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        string query = NavSearchBox.Text?.Trim() ?? "";
        bool hasQuery = query.Length > 0;
        bool anyResultVisible = false;

        foreach (var category in NavTreeView.Items.OfType<TreeViewItem>())
        {
            bool anyCategoryChildVisible = false;

            // When the query matches the category name, show all its children
            bool categoryMatches = hasQuery &&
                ExtractItemText(category).Contains(query, System.StringComparison.OrdinalIgnoreCase);

            foreach (var child in category.Items.OfType<TreeViewItem>())
            {
                if (hasQuery)
                {
                    string text = ExtractItemText(child);
                    bool matches = categoryMatches ||
                        text.Contains(query, System.StringComparison.OrdinalIgnoreCase);
                    child.IsVisible = matches;
                    if (matches) anyCategoryChildVisible = true;
                }
                else
                {
                    child.IsVisible = true;
                    anyCategoryChildVisible = true;
                }
            }

            category.IsVisible = anyCategoryChildVisible;
            if (hasQuery && anyCategoryChildVisible)
                category.IsExpanded = true;
            if (anyCategoryChildVisible) anyResultVisible = true;
        }

        // Restore initial expansion states when search is cleared
        if (!hasQuery)
            RestoreInitialExpansionStates();

        // Show or hide the "no results" indicator
        NavNoResultsText.IsVisible = hasQuery && !anyResultVisible;
    }

    /// <summary>
    /// Extracts the display text from a TreeViewItem header.
    /// Handles StackPanel > TextBlock (child items), plain TextBlock (categories),
    /// and falls back to <see cref="object.ToString"/> for other header types.
    /// </summary>
    private static string ExtractItemText(TreeViewItem item)
    {
        if (item.Header is TextBlock directTb)
            return directTb.Text ?? "";

        if (item.Header is StackPanel sp)
        {
            var tb = sp.Children.OfType<TextBlock>().FirstOrDefault();
            if (tb != null) return tb.Text ?? "";
        }

        return item.Header?.ToString() ?? "";
    }

    /// <summary>
    /// Caches the XAML-defined expansion state of each category so it can
    /// be restored when the search filter is cleared.
    /// </summary>
    private void CacheInitialExpansionStates()
    {
        _initialExpansionStates = new Dictionary<TreeViewItem, bool>();
        foreach (var category in NavTreeView.Items.OfType<TreeViewItem>())
            _initialExpansionStates[category] = category.IsExpanded;
    }

    /// <summary>
    /// Restores category expansion states to the values captured at startup.
    /// </summary>
    private void RestoreInitialExpansionStates()
    {
        if (_initialExpansionStates == null) return;
        foreach (var (category, expanded) in _initialExpansionStates)
            category.IsExpanded = expanded;
    }

    /// <summary>
    /// Shows the guided tour overlay.
    /// </summary>
    public void ShowTour()
    {
        TourOverlayControl.StartTour();
    }

    private object? ResolveView(string? tag)
    {
        return tag switch
        {
            // Browse & Inspect
            "bigpicture" => Services.AppSettings.Instance.BigPictureStyle == "Modern"
                ? _bigPictureModernView : _bigPictureView,
            "browser" => _browserView,
            "hexviewer" => _hexViewerView,
            "inspector" => _inspectorView,

            // Patching & Conversion
            "formatconv" => _formatConverterView,
            "n64conv" => _n64ConverterView,
            "patcher" => _patcherView,
            "patchcreator" => _patchCreatorView,
            "saveconv" => _saveFileConverterView,
            "splitrom" => _splitAssemblerView,

            // Analysis & Verification
            "batchhash" => _batchHasherView,
            "checksum" => _checksumView,
            "comparer" => _comparerView,
            "datfilter" => _datFilterView,
            "datverifier" => _datVerifierView,
            "dumpverifier" => _dumpVerifierView,
            "duplicates" => _duplicateFinderView,
            "goodtools" => _goodToolsIdentifierView,
            "security" => _securityAnalyzerView,

            // Headers & Trimming
            "export" => _headerExporterView,
            "headerfixer" => _batchHeaderFixerView,
            "snesheader" => _snesHeaderToolView,
            "trimmer" => _romTrimmerView,

            // Utilities
            "archives" => _archiveManagerView,
            "cheatcodes" => _cheatCodeView,
            "disctools" => _discToolsView,
            "emuconfig" => _emulatorConfigView,
            "gamepadkeymapper" => _gamepadKeyMapperView,
            "metascraper" => _metadataScraperView,
            "romorganizer" => _romOrganizerView,
            "romrenamer" => _romRenamerView,
            "usbtools" => _usbToolsView,

            // RetroArch
            "raachievements" => _retroAchievementsWriterView,
            "raconfigurator" => _retroArchConfiguratorView,
            "raintegration" => _retroArchIntegrationView,
            "raplaylist" => _retroArchPlaylistView,
            "rashortcut" => _retroArchShortcutView,

            // MAME
            "mamechdconv" => _mameChdConverterView,
            "mamechd" => _mameChdVerifierView,
            "mameconfigurator" => _mameConfiguratorView,
            "mamedateditor" => _mameDatEditorView,
            "mamedir2dat" => _mameDir2DatView,
            "mameintegration" => _mameIntegrationView,
            "mameauditor" => _mameAuditorView,
            "mamesamples" => _mameSampleAuditorView,
            "mamerebuilder" => _mameRebuilderView,

            // Mednafen
            "mednafenconfigurator" => _mednafenConfiguratorView,
            "mednafenintegration" => _mednafenIntegrationView,

            // Analogue
            "analogue3d" => _analogue3DView,
            "analoguemegasg" => _analogueMegaSgView,
            "analoguentsupernt" => _analogueNtSuperNtView,
            "analoguepocket" => _analoguePocketView,

            // Settings
            "settings" => _settingsView,

            _ => _browserView
        };
    }
}
