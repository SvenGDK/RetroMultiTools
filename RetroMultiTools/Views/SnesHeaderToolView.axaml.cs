using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class SnesHeaderToolView : UserControl
{
    private static readonly IBrush StatusErrorBrush = new SolidColorBrush(Color.Parse("#F38BA8"));
    private static readonly IBrush StatusSuccessBrush = new SolidColorBrush(Color.Parse("#A6E3A1"));
    private static readonly IBrush StatusWarningBrush = new SolidColorBrush(Color.Parse("#F9E2AF"));

    private bool _hasCopierHeader;

    public SnesHeaderToolView()
    {
        InitializeComponent();
    }

    private async void BrowseInput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFile(LocalizationManager.Instance["SnesHeader_SelectRom"],
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
                ? LocalizationManager.Instance["SnesHeader_CopierDetected"]
                : LocalizationManager.Instance["SnesHeader_NoCopier"];
            HeaderStatusText.Foreground = _hasCopierHeader
                ? StatusSuccessBrush
                : StatusWarningBrush;
            FileSizeText.Text = $"File size: {FileUtils.FormatFileSize(fileSize)} ({fileSize:N0} bytes)";
            HeaderStatusPanel.IsVisible = true;

            RemoveHeaderButton.IsEnabled = _hasCopierHeader;
            AddHeaderButton.IsEnabled = !_hasCopierHeader;
        }
        catch (IOException)
        {
            HeaderStatusText.Text = LocalizationManager.Instance["SnesHeader_UnableToRead"];
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
            Title = LocalizationManager.Instance["SnesHeader_SaveAs"],
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
            ShowStatus(LocalizationManager.Instance["SnesHeader_SelectInputFile"], isError: true);
            return;
        }

        if (string.IsNullOrEmpty(output))
        {
            ShowStatus(LocalizationManager.Instance["SnesHeader_SelectOutputFile"], isError: true);
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
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
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
