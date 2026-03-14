using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class PatchCreatorView : UserControl
{
    private PatchAnalysis? _analysis;

    public PatchCreatorView()
    {
        InitializeComponent();
    }

    private async void BrowseOriginal_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFile("Select Original ROM File");
        if (path == null) return;
        OriginalFileTextBox.Text = path;
        TryAnalyze();
    }

    private async void BrowseModified_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFile("Select Modified ROM File");
        if (path == null) return;
        ModifiedFileTextBox.Text = path;
        TryAnalyze();
    }

    private void TryAnalyze()
    {
        string original = OriginalFileTextBox.Text ?? "";
        string modified = ModifiedFileTextBox.Text ?? "";

        if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(modified))
            return;

        try
        {
            _analysis = PatchCreator.Analyze(original, modified);

            OriginalSizeText.Text = FileUtils.FormatFileSize(_analysis.OriginalSize);
            ModifiedSizeText.Text = FileUtils.FormatFileSize(_analysis.ModifiedSize);
            DiffCountText.Text = _analysis.IsIdentical
                ? "Files are identical"
                : $"{_analysis.DifferingBytes:N0} bytes differ";
            FormatText.Text = _analysis.CanCreateIps ? "IPS" : "Files too large for IPS";

            AnalysisPanel.IsVisible = true;
            CreateButton.IsEnabled = !_analysis.IsIdentical && _analysis.CanCreateIps;

            if (string.IsNullOrEmpty(OutputFileTextBox.Text))
            {
                string dir = Path.GetDirectoryName(modified) ?? "";
                string name = Path.GetFileNameWithoutExtension(modified);
                OutputFileTextBox.Text = Path.Combine(dir, name + ".ips");
            }
        }
        catch (IOException ex)
        {
            ShowStatus($"✘ Error analyzing: {ex.Message}", isError: true);
        }
        catch (InvalidOperationException ex)
        {
            ShowStatus($"✘ Error analyzing: {ex.Message}", isError: true);
        }
        catch (UnauthorizedAccessException ex)
        {
            ShowStatus($"✘ Error analyzing: {ex.Message}", isError: true);
        }
    }

    private async void BrowseOutput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save IPS Patch As",
            SuggestedFileName = Path.GetFileName(OutputFileTextBox.Text ?? "patch.ips"),
            FileTypeChoices =
            [
                new FilePickerFileType("IPS Patch") { Patterns = ["*.ips"] }
            ]
        });

        if (file != null)
            OutputFileTextBox.Text = file.Path.LocalPath;
    }

    private async void CreateButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string original = OriginalFileTextBox.Text ?? "";
        string modified = ModifiedFileTextBox.Text ?? "";
        string output = OutputFileTextBox.Text ?? "";

        if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(modified) || string.IsNullOrEmpty(output))
        {
            ShowStatus("Please fill in all file paths.", isError: true);
            return;
        }

        CreateButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        StatusBorder.IsVisible = false;

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);
            await PatchCreator.CreateIpsAsync(original, modified, output, progress);
            var patchSize = new FileInfo(output).Length;
            ShowStatus($"✔ IPS patch created!\nOutput: {output}\nPatch size: {FileUtils.FormatFileSize(patchSize)}", isError: false);
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
            CreateButton.IsEnabled = _analysis != null && !_analysis.IsIdentical && _analysis.CanCreateIps;
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

    private async Task<string?> PickFile(string title)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [FilePickerFileTypes.All]
        });

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }
}
