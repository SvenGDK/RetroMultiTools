using Avalonia.Controls;
using RetroMultiTools.Utilities;
using RetroMultiTools.Views;
using RetroMultiTools.Views.Analogue;
using RetroMultiTools.Views.Mame;
using RetroMultiTools.Views.Mednafen;
using RetroMultiTools.Views.RetroArch;

namespace RetroMultiTools;

public partial class MainWindow : Window
{
    // Browse & Inspect
    private readonly BigPictureView _bigPictureView = new();
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
    private readonly EmulatorConfigView _emulatorConfigView = new();
    private readonly GamepadKeyMapperView _gamepadKeyMapperView = new();
    private readonly MetadataScraperView _metadataScraperView = new();
    private readonly RomOrganizerView _romOrganizerView = new();
    private readonly RomRenamerView _romRenamerView = new();

    // RetroArch
    private readonly RetroAchievementsWriterView _retroAchievementsWriterView = new();
    private readonly RetroArchIntegrationView _retroArchIntegrationView = new();
    private readonly RetroArchPlaylistView _retroArchPlaylistView = new();
    private readonly RetroArchShortcutView _retroArchShortcutView = new();

    // MAME
    private readonly MameChdConverterView _mameChdConverterView = new();
    private readonly MameChdVerifierView _mameChdVerifierView = new();
    private readonly MameDatEditorView _mameDatEditorView = new();
    private readonly MameDir2DatView _mameDir2DatView = new();
    private readonly MameIntegrationView _mameIntegrationView = new();
    private readonly MameRomAuditorView _mameAuditorView = new();
    private readonly MameSampleAuditorView _mameSampleAuditorView = new();
    private readonly MameSetRebuilderView _mameRebuilderView = new();

    // Mednafen
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

    /// <summary>
    /// Raised when <see cref="IsMinimizedToTray"/> changes.
    /// </summary>
    public event Action<bool>? IsMinimizedToTrayChanged;

    public MainWindow()
    {
        InitializeComponent();
        NavListBox.SelectedIndex = 1;
        UpdateTitle();
        Localization.LocalizationManager.Instance.PropertyChanged += (_, _) => UpdateTitle();
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
            // Sync the sidebar selection
            for (int i = 0; i < NavListBox.Items.Count; i++)
            {
                if (NavListBox.Items[i] is ListBoxItem item && item.Tag?.ToString() == tag)
                {
                    NavListBox.SelectedIndex = i;
                    break;
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

        // Show Big Picture view
        _bigPictureView.LoadFolder(folderPath, roms);
        MainContent.Content = _bigPictureView;

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
        NavListBox.SelectedIndex = 1;

        WindowState = WindowState.Normal;
    }

    private void NavListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (NavListBox.SelectedItem is ListBoxItem item)
        {
            var content = ResolveView(item.Tag?.ToString());
            if (content != null)
                MainContent.Content = content;
        }
    }

    private object? ResolveView(string? tag)
    {
        return tag switch
        {
            // Browse & Inspect
            "bigpicture" => _bigPictureView,
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
            "emuconfig" => _emulatorConfigView,
            "gamepadkeymapper" => _gamepadKeyMapperView,
            "metascraper" => _metadataScraperView,
            "romorganizer" => _romOrganizerView,
            "romrenamer" => _romRenamerView,

            // RetroArch
            "raachievements" => _retroAchievementsWriterView,
            "raintegration" => _retroArchIntegrationView,
            "raplaylist" => _retroArchPlaylistView,
            "rashortcut" => _retroArchShortcutView,

            // MAME
            "mamechdconv" => _mameChdConverterView,
            "mamechd" => _mameChdVerifierView,
            "mamedateditor" => _mameDatEditorView,
            "mamedir2dat" => _mameDir2DatView,
            "mameintegration" => _mameIntegrationView,
            "mameauditor" => _mameAuditorView,
            "mamesamples" => _mameSampleAuditorView,
            "mamerebuilder" => _mameRebuilderView,

            // Mednafen
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
