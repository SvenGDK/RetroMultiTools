using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class SplitRomAssemblerView : UserControl
{
    private List<string>? _detectedParts;

    public SplitRomAssemblerView()
    {
        InitializeComponent();
    }

    private async void BrowseInput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFile("Select First Split Part",
        [
            new FilePickerFileType("Split ROM Parts") { Patterns = ["*.001", "*.part1", "*.z01"] },
            FilePickerFileTypes.All
        ]);
        if (path == null) return;

        InputFileTextBox.Text = path;
        DetectParts(path);
        UpdateOutputPath(path);
    }

    private void DetectParts(string firstPartPath)
    {
        try
        {
            _detectedParts = SplitRomAssembler.DetectParts(firstPartPath);

            if (_detectedParts.Count == 0)
            {
                PartsText.Text = "No split pattern detected for this file.";
                PartsPanel.IsVisible = true;
                AssembleButton.IsEnabled = false;
            }
            else
            {
                var sb = new System.Text.StringBuilder();
                long totalSize = 0;
                foreach (var part in _detectedParts)
                {
                    long size = new FileInfo(part).Length;
                    totalSize += size;
                    sb.AppendLine($"  {Path.GetFileName(part)} ({FileUtils.FormatFileSize(size)})");
                }
                sb.AppendLine($"\nTotal: {_detectedParts.Count} parts, {FileUtils.FormatFileSize(totalSize)}");
                PartsText.Text = sb.ToString();
                PartsPanel.IsVisible = true;
                AssembleButton.IsEnabled = _detectedParts.Count > 1;
            }
        }
        catch (IOException ex)
        {
            _detectedParts = null;
            PartsText.Text = $"Unable to read file: {ex.Message}";
            PartsPanel.IsVisible = true;
            AssembleButton.IsEnabled = false;
        }
    }

    private void UpdateOutputPath(string firstPartPath)
    {
        // Generate output name by removing split suffix
        string dir = Path.GetDirectoryName(firstPartPath) ?? "";
        string fileName = Path.GetFileName(firstPartPath);

        // Remove the split extension (e.g. .001, .part1, .z01)
        int lastDot = fileName.LastIndexOf('.');
        if (lastDot > 0)
            fileName = fileName[..lastDot];

        OutputFileTextBox.Text = Path.Combine(dir, fileName);
    }

    private async void BrowseOutput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Assembled ROM As",
            SuggestedFileName = Path.GetFileName(OutputFileTextBox.Text ?? "assembled_rom")
        });

        if (file != null)
            OutputFileTextBox.Text = file.Path.LocalPath;
    }

    private async void AssembleButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string input = InputFileTextBox.Text ?? "";
        string output = OutputFileTextBox.Text ?? "";

        if (string.IsNullOrEmpty(input))
        {
            ShowStatus("Please select the first split part file.", isError: true);
            return;
        }

        if (string.IsNullOrEmpty(output))
        {
            ShowStatus("Please specify an output file path.", isError: true);
            return;
        }

        AssembleButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        StatusBorder.IsVisible = false;

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);
            var result = await SplitRomAssembler.AssembleAsync(input, output, progress);
            ShowStatus($"✔ Assembly complete!\n{result.Summary}\nOutput: {output}", isError: false);
        }
        catch (IOException ex)
        {
            ShowStatus($"✘ Error: {ex.Message}", isError: true);
        }
        catch (InvalidOperationException ex)
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
            AssembleButton.IsEnabled = _detectedParts is { Count: > 1 };
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
