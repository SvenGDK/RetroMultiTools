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

    public MainWindow()
    {
        InitializeComponent();
        NavListBox.SelectedIndex = 1;
    }

    private void NavListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (NavListBox.SelectedItem is ListBoxItem item)
        {
            MainContent.Content = item.Tag?.ToString() switch
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
                _ => _browserView
            };
        }
    }
}
