using Avalonia.Controls;
using RetroMultiTools.Views;

namespace RetroMultiTools;

public partial class MainWindow : Window
{
    private readonly RomBrowserView _browserView = new();
    private readonly RomInspectorView _inspectorView = new();
    private readonly RomPatcherView _patcherView = new();
    private readonly ChecksumCalculatorView _checksumView = new();
    private readonly RomComparerView _comparerView = new();
    private readonly N64ConverterView _n64ConverterView = new();
    private readonly HeaderExporterView _headerExporterView = new();
    private readonly DuplicateFinderView _duplicateFinderView = new();
    private readonly SnesHeaderToolView _snesHeaderToolView = new();
    private readonly RomTrimmerView _romTrimmerView = new();
    private readonly RomFormatConverterView _formatConverterView = new();
    private readonly ZipRomExtractorView _zipExtractorView = new();
    private readonly SplitRomAssemblerView _splitAssemblerView = new();
    private readonly RomDecompressorView _decompressorView = new();
    private readonly BatchHeaderFixerView _batchHeaderFixerView = new();
    private readonly CheatCodeView _cheatCodeView = new();
    private readonly DatVerifierView _datVerifierView = new();
    private readonly DatFilterView _datFilterView = new();
    private readonly DumpVerifierView _dumpVerifierView = new();
    private readonly SecurityAnalyzerView _securityAnalyzerView = new();
    private readonly EmulatorConfigView _emulatorConfigView = new();
    private readonly MetadataScraperView _metadataScraperView = new();
    private readonly RomRenamerView _romRenamerView = new();
    private readonly PatchCreatorView _patchCreatorView = new();
    private readonly HexViewerView _hexViewerView = new();
    private readonly SaveFileConverterView _saveFileConverterView = new();
    private readonly BatchHasherView _batchHasherView = new();
    private readonly SettingsView _settingsView = new();
    private readonly MameRomAuditorView _mameAuditorView = new();
    private readonly MameChdVerifierView _mameChdVerifierView = new();
    private readonly MameSetRebuilderView _mameRebuilderView = new();
    private readonly MameDir2DatView _mameDir2DatView = new();
    private readonly MameSampleAuditorView _mameSampleAuditorView = new();
    private readonly BigPictureView _bigPictureView = new();
    private readonly GoodToolsIdentifierView _goodToolsIdentifierView = new();

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
            "browser" => _browserView,
            "inspector" => _inspectorView,
            "patcher" => _patcherView,
            "checksum" => _checksumView,
            "comparer" => _comparerView,
            "n64conv" => _n64ConverterView,
            "export" => _headerExporterView,
            "duplicates" => _duplicateFinderView,
            "snesheader" => _snesHeaderToolView,
            "trimmer" => _romTrimmerView,
            "formatconv" => _formatConverterView,
            "zipextract" => _zipExtractorView,
            "splitrom" => _splitAssemblerView,
            "decompress" => _decompressorView,
            "headerfixer" => _batchHeaderFixerView,
            "cheatcodes" => _cheatCodeView,
            "datverifier" => _datVerifierView,
            "datfilter" => _datFilterView,
            "dumpverifier" => _dumpVerifierView,
            "security" => _securityAnalyzerView,
            "emuconfig" => _emulatorConfigView,
            "metascraper" => _metadataScraperView,
            "romrenamer" => _romRenamerView,
            "patchcreator" => _patchCreatorView,
            "hexviewer" => _hexViewerView,
            "saveconv" => _saveFileConverterView,
            "batchhash" => _batchHasherView,
            "settings" => _settingsView,
            "mameauditor" => _mameAuditorView,
            "mamechd" => _mameChdVerifierView,
            "mamerebuilder" => _mameRebuilderView,
            "mamedir2dat" => _mameDir2DatView,
            "mamesamples" => _mameSampleAuditorView,
            "bigpicture" => _bigPictureView,
            "goodtools" => _goodToolsIdentifierView,
            _ => _browserView
        };
    }
}
