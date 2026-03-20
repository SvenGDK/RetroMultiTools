using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class SaveFileConverterView : UserControl
{
    private static readonly IBrush StatusErrorBrush = new SolidColorBrush(Color.Parse("#F38BA8"));
    private static readonly IBrush StatusSuccessBrush = new SolidColorBrush(Color.Parse("#A6E3A1"));

    private SaveFileInfo? _fileInfo;

    private static readonly string[] ConversionExtensions =
    [
        "",     // SwapEndian16 - keep original
        "",     // SwapEndian32 - keep original
        "",     // PadToPowerOfTwo - keep original
        "",     // TrimTrailingZeros - keep original
        "",     // TrimTrailingFF - keep original
        ".sav", // SrmToSav
        ".srm", // SavToSrm
    ];

    public SaveFileConverterView()
    {
        InitializeComponent();
    }

    private async void BrowseInput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFile("Select Save File",
        [
            new FilePickerFileType("Save Files")
            {
                Patterns = ["*.sav", "*.srm", "*.eep", "*.fla", "*.sra"]
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
            _fileInfo = SaveFileConverter.Analyze(path);
            FileSizeText.Text = $"{_fileInfo.FileSizeFormatted} ({_fileInfo.FileSize:N0} bytes)";
            DetectedTypeText.Text = _fileInfo.DetectedType;
            PowerOfTwoText.Text = _fileInfo.IsPowerOfTwo ? "Yes" : "No";

            InfoPanel.IsVisible = true;
            ConvertButton.IsEnabled = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _fileInfo = null;
            InfoPanel.IsVisible = false;
            ConvertButton.IsEnabled = false;
            ShowStatus($"✘ Could not analyze file: {ex.Message}", isError: true);
        }
    }

    private void UpdateOutputPath()
    {
        if (string.IsNullOrEmpty(InputFileTextBox.Text)) return;

        string dir = Path.GetDirectoryName(InputFileTextBox.Text) ?? "";
        string name = Path.GetFileNameWithoutExtension(InputFileTextBox.Text);

        int selectedIndex = ConversionComboBox.SelectedIndex;
        string ext = selectedIndex >= 0 && selectedIndex < ConversionExtensions.Length
                     && !string.IsNullOrEmpty(ConversionExtensions[selectedIndex])
            ? ConversionExtensions[selectedIndex]
            : Path.GetExtension(InputFileTextBox.Text);

        OutputFileTextBox.Text = Path.Combine(dir, name + "_converted" + ext);
    }

    private async void BrowseOutput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Converted File As",
            SuggestedFileName = Path.GetFileName(OutputFileTextBox.Text ?? "converted.sav")
        });

        if (file != null)
            OutputFileTextBox.Text = file.Path.LocalPath;
    }

    private async void ConvertButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string input = InputFileTextBox.Text ?? "";
        string output = OutputFileTextBox.Text ?? "";

        if (string.IsNullOrEmpty(input))
        {
            ShowStatus("Please select an input save file.", isError: true);
            return;
        }
        if (string.IsNullOrEmpty(output))
        {
            ShowStatus("Please specify an output file path.", isError: true);
            return;
        }

        if (ConversionComboBox.SelectedIndex < 0)
        {
            ShowStatus("Please select a conversion type.", isError: true);
            return;
        }

        var conversion = (SaveConversion)ConversionComboBox.SelectedIndex;

        ConvertButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        StatusBorder.IsVisible = false;

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);
            await SaveFileConverter.ConvertAsync(input, output, conversion, progress);

            var outputInfo = new FileInfo(output);
            ShowStatus($"✔ Conversion complete!\nOutput: {output}\nSize: {FileUtils.FormatFileSize(outputInfo.Length)}", isError: false);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            ShowStatus($"✘ Error: {ex.Message}", isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            ConvertButton.IsEnabled = true;
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
