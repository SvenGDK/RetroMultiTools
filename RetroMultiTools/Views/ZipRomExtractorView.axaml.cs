using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class ZipRomExtractorView : UserControl
{
    public ZipRomExtractorView()
    {
        InitializeComponent();
    }

    private void ModeRadio_Checked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (InputLabel == null) return;
        if (sender is RadioButton rb && rb.IsChecked != true) return;

        bool isBatch = sender == BatchModeRadio;
        InputLabel.Text = isBatch ? "ZIP Directory:" : "ZIP File:";
        InputPathTextBox.Watermark = isBatch ? "Select a directory of ZIP files..." : "Select a ZIP file...";
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
            var path = await PickFolder("Select Directory of ZIP Files");
            if (path != null) InputPathTextBox.Text = path;
        }
        else
        {
            var path = await PickFile("Select ZIP File",
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
                ResultsText.Text = "No ROM files found in this ZIP archive.";
            }
            else
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Found {entries.Count} ROM file(s):");
                foreach (var entry in entries)
                    sb.AppendLine($"  {entry.Summary}");
                ResultsText.Text = sb.ToString();
            }
            ResultsBorder.IsVisible = true;
        }
        catch (IOException ex)
        {
            ResultsText.Text = $"Unable to read ZIP file: {ex.Message}";
            ResultsBorder.IsVisible = true;
        }
    }

    private async void BrowseOutput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFolder("Select Output Directory");
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
            ShowStatus("Please select input and output paths.", isError: true);
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
        catch (IOException ex)
        {
            ShowStatus($"✘ Error: {ex.Message}", isError: true);
        }
        catch (InvalidDataException ex)
        {
            ShowStatus($"✘ Error: {ex.Message}", isError: true);
        }
        catch (UnauthorizedAccessException ex)
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
