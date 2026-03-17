using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class ZipRomExtractorView : UserControl
{
    private static readonly IBrush StatusErrorBrush = new SolidColorBrush(Color.Parse("#F38BA8"));
    private static readonly IBrush StatusSuccessBrush = new SolidColorBrush(Color.Parse("#A6E3A1"));

    public ZipRomExtractorView()
    {
        InitializeComponent();
    }

    private void ModeRadio_Checked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (InputLabel == null) return;
        if (sender is RadioButton rb && rb.IsChecked != true) return;

        var loc = LocalizationManager.Instance;
        bool isBatch = sender == BatchModeRadio;
        InputLabel.Text = isBatch
            ? loc["ZipExtract_ZipDirectory"]
            : loc["ZipExtract_ZipFile"];
        InputPathTextBox.Watermark = isBatch
            ? loc["ZipExtract_SelectZipDirectory"]
            : loc["ZipExtract_SelectZip"];
        InputPathTextBox.Text = string.Empty;
        StatusBorder.IsVisible = false;
        ResultsBorder.IsVisible = false;
        UpdateExtractButton();
    }

    private async void BrowseInput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        bool isBatch = BatchModeRadio.IsChecked == true;

        if (isBatch)
        {
            var path = await PickFolder(LocalizationManager.Instance["ZipExtract_SelectZipDir"]);
            if (path != null) InputPathTextBox.Text = path;
        }
        else
        {
            var path = await PickFile(LocalizationManager.Instance["ZipExtract_SelectZipFile"],
            [
                new FilePickerFileType("ZIP Archives") { Patterns = ["*.zip"] },
                FilePickerFileTypes.All
            ]);
            if (path != null)
            {
                InputPathTextBox.Text = path;
                ShowZipContents(path);
            }
        }

        UpdateExtractButton();
    }

    private void ShowZipContents(string zipPath)
    {
        try
        {
            var entries = ZipRomExtractor.ListRoms(zipPath);
            if (entries.Count == 0)
            {
                ResultsText.Text = LocalizationManager.Instance["ZipExtract_NoRomsFound"];
            }
            else
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine(string.Format(LocalizationManager.Instance["ZipExtract_FoundRoms"], entries.Count));
                foreach (var entry in entries)
                    sb.AppendLine($"  {entry.Summary}");
                ResultsText.Text = sb.ToString();
            }
            ResultsBorder.IsVisible = true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            ResultsText.Text = $"Unable to read ZIP file: {ex.Message}";
            ResultsBorder.IsVisible = true;
        }
    }

    private async void BrowseOutput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFolder(LocalizationManager.Instance["ZipExtract_SelectOutputDir"]);
        if (path != null)
            OutputPathTextBox.Text = path;
        UpdateExtractButton();
    }

    private void UpdateExtractButton()
    {
        ExtractButton.IsEnabled = !string.IsNullOrEmpty(InputPathTextBox.Text) &&
                                  !string.IsNullOrEmpty(OutputPathTextBox.Text);
    }

    private async void ExtractButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string input = InputPathTextBox.Text ?? "";
        string output = OutputPathTextBox.Text ?? "";

        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(output))
        {
            ShowStatus(LocalizationManager.Instance["ZipExtract_SelectInputOutput"], isError: true);
            return;
        }

        ExtractButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        StatusBorder.IsVisible = false;

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);
            bool isBatch = BatchModeRadio.IsChecked == true;

            ZipExtractionResult result;
            if (isBatch)
                result = await ZipRomExtractor.ExtractBatchAsync(input, output, progress);
            else
                result = await ZipRomExtractor.ExtractAsync(input, output, progress);

            ShowStatus($"✔ Extraction complete!\n{result.Summary}", isError: false);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            ShowStatus($"✘ Error: {ex.Message}", isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            ExtractButton.IsEnabled = true;
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

    private async Task<string?> PickFolder(string title)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return null;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }
}
