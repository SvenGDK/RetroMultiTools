using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class RomTrimmerView : UserControl
{
    private TrimAnalysis? _analysis;

    public RomTrimmerView()
    {
        InitializeComponent();
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

            OriginalSizeText.Text = $"{FileUtils.FormatFileSize(_analysis.OriginalSize)} ({_analysis.OriginalSize:N0} bytes)";
            TrimmedSizeText.Text = $"{FileUtils.FormatFileSize(_analysis.TrimmedSize)} ({_analysis.TrimmedSize:N0} bytes)";

            if (_analysis.SavedBytes > 0)
            {
                double pct = _analysis.OriginalSize > 0 ? (_analysis.SavedBytes * 100.0 / _analysis.OriginalSize) : 0;
                SavingsText.Text = $"{FileUtils.FormatFileSize(_analysis.SavedBytes)} ({pct:F1}%)";
                SavingsText.Foreground = new Avalonia.Media.SolidColorBrush(
                    Avalonia.Media.Color.Parse("#A6E3A1"));
            }
            else
            {
                SavingsText.Text = LocalizationManager.Instance["Trimmer_AlreadyTrimmed"];
                SavingsText.Foreground = new Avalonia.Media.SolidColorBrush(
                    Avalonia.Media.Color.Parse("#F9E2AF"));
            }

            AnalysisPanel.IsVisible = true;
            TrimButton.IsEnabled = _analysis.SavedBytes > 0;
        }
        catch (IOException)
        {
            _analysis = null;
            SavingsText.Text = LocalizationManager.Instance["Trimmer_UnableToRead"];
            AnalysisPanel.IsVisible = true;
            TrimButton.IsEnabled = false;
        }
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
            ShowStatus($"✔ Trim complete!\nOutput: {output}\n{_analysis?.Summary ?? ""}", isError: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowStatus($"✘ Error: {ex.Message}", isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            TrimButton.IsEnabled = _analysis?.SavedBytes > 0;
        }
    }

    private void ShowStatus(string message, bool isError)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError
            ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#F38BA8"))
            : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#A6E3A1"));
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
