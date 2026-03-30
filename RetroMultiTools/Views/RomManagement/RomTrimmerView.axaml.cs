using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Utilities;
using RetroMultiTools.Utilities.RomManagement;

namespace RetroMultiTools.Views.RomManagement;

public partial class RomTrimmerView : UserControl
{
    private static readonly IBrush StatusErrorBrush = new SolidColorBrush(Color.Parse("#F38BA8"));
    private static readonly IBrush StatusSuccessBrush = new SolidColorBrush(Color.Parse("#A6E3A1"));
    private static readonly IBrush StatusWarningBrush = new SolidColorBrush(Color.Parse("#F9E2AF"));

    private TrimAnalysis? _analysis;

    public RomTrimmerView()
    {
        InitializeComponent();
        OutputFileTextBox.TextChanged += (_, _) => UpdateTrimButton();
        DragDropHelper.EnableFileDrop(InputFileTextBox, path =>
        {
            AnalyzeFile(path);
            UpdateOutputPath();
        });
    }

    /// <summary>
    /// Pre-fills the input file path and triggers analysis.
    /// </summary>
    public void SetInputFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;
        InputFileTextBox.Text = filePath;
        AnalyzeFile(filePath);
        UpdateOutputPath();
    }

    private async void BrowseInput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFile(LocalizationManager.Instance["Trimmer_SelectRomFile"],
        [
            new FilePickerFileType("ROM Files")
            {
                Patterns = [ "*.nes","*.smc","*.sfc","*.z64","*.n64","*.v64",
                                   "*.gb","*.gbc","*.gba","*.vb","*.vboy",
                                   "*.sms","*.md","*.gen",
                                   "*.bin","*.32x","*.gg","*.a26","*.a52","*.a78",
                                   "*.j64","*.jag","*.lnx","*.lyx",
                                   "*.pce","*.tg16",
                                   "*.ngp","*.ngc",
                                   "*.col","*.cv","*.int",
                                   "*.mx1","*.mx2",
                                   "*.dsk","*.cdt","*.sna",
                                   "*.tap",
                                   "*.mo5","*.k7","*.fd",
                                   "*.sv","*.ccc",
                                   "*.iso","*.cue","*.3do",
                                   "*.chd","*.rvz","*.gcm" ]
            },
            FilePickerFileTypes.All
        ]);
        if (path == null) return;

        InputFileTextBox.Text = path;
        AnalyzeFile(path);
        UpdateOutputPath();
    }

    private void AnalyzeFile(string path)
    {
        try
        {
            _analysis = RomTrimmer.Analyze(path);

            OriginalSizeText.Text = string.Format(LocalizationManager.Instance["Trimmer_SizeBytes"], FileUtils.FormatFileSize(_analysis.OriginalSize), _analysis.OriginalSize.ToString("N0"));
            TrimmedSizeText.Text = string.Format(LocalizationManager.Instance["Trimmer_SizeBytes"], FileUtils.FormatFileSize(_analysis.TrimmedSize), _analysis.TrimmedSize.ToString("N0"));

            if (_analysis.SavedBytes > 0)
            {
                double pct = _analysis.OriginalSize > 0 ? (_analysis.SavedBytes * 100.0 / _analysis.OriginalSize) : 0;
                SavingsText.Text = string.Format(LocalizationManager.Instance["Trimmer_SavingsPercent"], FileUtils.FormatFileSize(_analysis.SavedBytes), pct.ToString("F1"));
                SavingsText.Foreground = StatusSuccessBrush;
            }
            else
            {
                SavingsText.Text = LocalizationManager.Instance["Trimmer_AlreadyTrimmed"];
                SavingsText.Foreground = StatusWarningBrush;
            }

            AnalysisPanel.IsVisible = true;
            UpdateTrimButton();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _analysis = null;
            SavingsText.Text = LocalizationManager.Instance["Trimmer_UnableToRead"];
            AnalysisPanel.IsVisible = true;
            UpdateTrimButton();
        }
    }

    private void UpdateTrimButton()
    {
        TrimButton.IsEnabled = _analysis?.SavedBytes > 0 &&
                               !string.IsNullOrEmpty(OutputFileTextBox.Text);
    }

    private void UpdateOutputPath()
    {
        if (string.IsNullOrEmpty(InputFileTextBox.Text)) return;

        string dir = Path.GetDirectoryName(InputFileTextBox.Text) ?? "";
        string name = Path.GetFileNameWithoutExtension(InputFileTextBox.Text);
        string ext = Path.GetExtension(InputFileTextBox.Text);
        OutputFileTextBox.Text = Path.Combine(dir, name + "_trimmed" + ext);
    }

    private async void BrowseOutput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = LocalizationManager.Instance["Trimmer_SaveAs"],
            SuggestedFileName = Path.GetFileName(OutputFileTextBox.Text ?? "trimmed_rom")
        });

        if (file != null)
            OutputFileTextBox.Text = file.Path.LocalPath;
    }

    private async void TrimButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string input = InputFileTextBox.Text ?? "";
        string output = OutputFileTextBox.Text ?? "";

        if (string.IsNullOrEmpty(input))
        {
            ShowStatus(LocalizationManager.Instance["Trimmer_SelectInputFile"], isError: true);
            return;
        }

        if (string.IsNullOrEmpty(output))
        {
            ShowStatus(LocalizationManager.Instance["Trimmer_SelectOutputFile"], isError: true);
            return;
        }

        TrimButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        StatusBorder.IsVisible = false;

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);
            await RomTrimmer.TrimAsync(input, output, progress);
            ShowStatus(string.Format(LocalizationManager.Instance["Trimmer_TrimComplete"], output, _analysis?.Summary ?? ""), isError: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowStatus(string.Format(LocalizationManager.Instance["Common_ErrorFormat"], ex.Message), isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            UpdateTrimButton();
        }
    }

    private void ShowStatus(string message, bool isError)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError ? StatusErrorBrush : StatusSuccessBrush;
        StatusBorder.IsVisible = true;
    }

    private async Task<string?> PickFile(string title, FilePickerFileType[] filters)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = filters
        });

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }
}
