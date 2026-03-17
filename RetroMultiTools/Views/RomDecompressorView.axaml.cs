using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class RomDecompressorView : UserControl
{
    public RomDecompressorView()
    {
        InitializeComponent();
    }

    private void ModeRadio_Checked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (InputLabel == null) return;
        if (sender is RadioButton rb && rb.IsChecked != true) return;

        bool isBatch = sender == BatchModeRadio;
        InputLabel.Text = isBatch ? "ROM Directory:" : "Compressed File:";
        OutputLabel.Text = isBatch ? "Output Directory:" : "Output File:";
        InputPathTextBox.Watermark = isBatch ? "Select a directory..." : "Select a compressed ROM file...";
        InputPathTextBox.Text = string.Empty;
        OutputPathTextBox.Text = string.Empty;
        StatusBorder.IsVisible = false;
        UpdateDecompressButton();
    }

    private async void BrowseInput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        bool isBatch = BatchModeRadio.IsChecked == true;

        if (isBatch)
        {
            var path = await PickFolder("Select ROM Directory");
            if (path != null) InputPathTextBox.Text = path;
        }
        else
        {
            var path = await PickFile("Select Compressed ROM",
            [
                new FilePickerFileType("Compressed ROMs") { Patterns = ["*.gz"] },
                FilePickerFileTypes.All
            ]);
            if (path != null)
            {
                InputPathTextBox.Text = path;
                UpdateOutputPath(path);
            }
        }

        UpdateDecompressButton();
    }

    private void UpdateOutputPath(string inputPath)
    {
        // Remove .gz extension for output
        string dir = Path.GetDirectoryName(inputPath) ?? "";
        string outputName = Path.GetFileNameWithoutExtension(inputPath);
        if (string.IsNullOrEmpty(outputName)) outputName = "decompressed_rom";
        OutputPathTextBox.Text = Path.Combine(dir, outputName);
    }

    private async void BrowseOutput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        bool isBatch = BatchModeRadio.IsChecked == true;

        if (isBatch)
        {
            var path = await PickFolder("Select Output Directory");
            if (path != null) OutputPathTextBox.Text = path;
        }
        else
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Decompressed ROM As",
                SuggestedFileName = Path.GetFileName(OutputPathTextBox.Text ?? "decompressed_rom")
            });

            if (file != null)
                OutputPathTextBox.Text = file.Path.LocalPath;
        }

        UpdateDecompressButton();
    }

    private void UpdateDecompressButton()
    {
        DecompressButton.IsEnabled = !string.IsNullOrEmpty(InputPathTextBox.Text) &&
                                     !string.IsNullOrEmpty(OutputPathTextBox.Text);
    }

    private async void DecompressButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string input = InputPathTextBox.Text ?? "";
        string output = OutputPathTextBox.Text ?? "";

        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(output))
        {
            ShowStatus("Please select input and output paths.", isError: true);
            return;
        }

        DecompressButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        StatusBorder.IsVisible = false;

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);
            bool isBatch = BatchModeRadio.IsChecked == true;

            if (isBatch)
            {
                var result = await RomDecompressor.DecompressBatchAsync(input, output, progress);
                ShowStatus($"✔ Batch decompression complete!\n{result.Summary}", isError: false);
            }
            else
            {
                var result = await RomDecompressor.DecompressAsync(input, output, progress);
                ShowStatus($"✔ Decompression complete!\n{result.Summary}\nOutput: {output}", isError: false);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            ShowStatus($"✘ Error: {ex.Message}", isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            DecompressButton.IsEnabled = true;
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
