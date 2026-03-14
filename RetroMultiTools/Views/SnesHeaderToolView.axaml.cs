using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class SnesHeaderToolView : UserControl
{
    private bool _hasCopierHeader;

    public SnesHeaderToolView()
    {
        InitializeComponent();
    }

    private async void BrowseInput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFile("Select SNES ROM",
        [
            new FilePickerFileType("SNES ROM Files") { Patterns = ["*.smc", "*.sfc"] },
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
            _hasCopierHeader = SnesHeaderTool.HasCopierHeader(path);
            long fileSize = new FileInfo(path).Length;

            HeaderStatusText.Text = _hasCopierHeader
                ? "✔ Copier header detected (512 bytes)"
                : "✘ No copier header present";
            HeaderStatusText.Foreground = _hasCopierHeader
                ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#A6E3A1"))
                : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#F9E2AF"));
            FileSizeText.Text = $"File size: {FileUtils.FormatFileSize(fileSize)} ({fileSize:N0} bytes)";
            HeaderStatusPanel.IsVisible = true;

            RemoveHeaderButton.IsEnabled = _hasCopierHeader;
            AddHeaderButton.IsEnabled = !_hasCopierHeader;
        }
        catch (IOException)
        {
            HeaderStatusText.Text = "Unable to read file";
            HeaderStatusPanel.IsVisible = true;
            RemoveHeaderButton.IsEnabled = false;
            AddHeaderButton.IsEnabled = false;
        }
    }

    private void UpdateOutputPath()
    {
        if (string.IsNullOrEmpty(InputFileTextBox.Text)) return;

        string dir = Path.GetDirectoryName(InputFileTextBox.Text) ?? "";
        string name = Path.GetFileNameWithoutExtension(InputFileTextBox.Text);
        string ext = Path.GetExtension(InputFileTextBox.Text);
        string suffix = _hasCopierHeader ? "_noheader" : "_withheader";
        OutputFileTextBox.Text = Path.Combine(dir, name + suffix + ext);
    }

    private async void BrowseOutput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Output ROM As",
            SuggestedFileName = Path.GetFileName(OutputFileTextBox.Text ?? "output.sfc")
        });

        if (file != null)
            OutputFileTextBox.Text = file.Path.LocalPath;
    }

    private async void RemoveHeader_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RunAction(async (input, output, progress) =>
            await SnesHeaderTool.RemoveHeaderAsync(input, output, progress),
            "remove");
    }

    private async void AddHeader_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RunAction(async (input, output, progress) =>
            await SnesHeaderTool.AddHeaderAsync(input, output, progress),
            "add");
    }

    private async Task RunAction(
        Func<string, string, IProgress<string>, Task> action,
        string actionName)
    {
        string input = InputFileTextBox.Text ?? "";
        string output = OutputFileTextBox.Text ?? "";

        if (string.IsNullOrEmpty(input))
        {
            ShowStatus("Please select an input ROM file.", isError: true);
            return;
        }

        if (string.IsNullOrEmpty(output))
        {
            ShowStatus("Please specify an output file path.", isError: true);
            return;
        }

        RemoveHeaderButton.IsEnabled = false;
        AddHeaderButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        StatusBorder.IsVisible = false;

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);
            await action(input, output, progress);
            ShowStatus($"✔ Header {actionName} complete!\nOutput: {output}", isError: false);
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
            if (!string.IsNullOrEmpty(InputFileTextBox.Text))
                AnalyzeFile(InputFileTextBox.Text);
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
