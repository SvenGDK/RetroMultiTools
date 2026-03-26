using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class PatchCreatorView : UserControl
{
    private static readonly IBrush StatusErrorBrush = new SolidColorBrush(Color.Parse("#F38BA8"));
    private static readonly IBrush StatusSuccessBrush = new SolidColorBrush(Color.Parse("#A6E3A1"));

    private PatchAnalysis? _analysis;

    public PatchCreatorView()
    {
        InitializeComponent();
    }

    private async void BrowseOriginal_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var loc = LocalizationManager.Instance;
            var path = await PickFile(loc["PatchCreator_SelectOriginal"]);
            if (path == null) return;
            OriginalFileTextBox.Text = path;
            await AnalyzeAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"BrowseOriginal_Click error: {ex}");
        }
    }

    private async void BrowseModified_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var loc = LocalizationManager.Instance;
            var path = await PickFile(loc["PatchCreator_SelectModified"]);
            if (path == null) return;
            ModifiedFileTextBox.Text = path;
            await AnalyzeAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"BrowseModified_Click error: {ex}");
        }
    }

    private async Task AnalyzeAsync()
    {
        string original = OriginalFileTextBox.Text ?? "";
        string modified = ModifiedFileTextBox.Text ?? "";

        if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(modified))
            return;

        try
        {
            _analysis = await Task.Run(() => PatchCreator.Analyze(original, modified));

            OriginalSizeText.Text = FileUtils.FormatFileSize(_analysis.OriginalSize);
            ModifiedSizeText.Text = FileUtils.FormatFileSize(_analysis.ModifiedSize);
            DiffCountText.Text = _analysis.IsIdentical
                ? LocalizationManager.Instance["PatchCreator_FilesIdentical"]
                : string.Format(LocalizationManager.Instance["PatchCreator_BytesDiffer"], _analysis.DifferingBytes.ToString("N0"));
            FormatText.Text = _analysis.CanCreateIps ? "IPS" : LocalizationManager.Instance["PatchCreator_FilesTooLargeIps"];

            AnalysisPanel.IsVisible = true;
            CreateButton.IsEnabled = !_analysis.IsIdentical && _analysis.CanCreateIps;

            if (string.IsNullOrEmpty(OutputFileTextBox.Text))
            {
                string dir = Path.GetDirectoryName(modified) ?? "";
                string name = Path.GetFileNameWithoutExtension(modified);
                OutputFileTextBox.Text = Path.Combine(dir, name + ".ips");
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            var loc = LocalizationManager.Instance;
            ShowStatus(string.Format(loc["PatchCreator_AnalyzeError"], ex.Message), isError: true);
        }
    }

    private async void BrowseOutput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var loc = LocalizationManager.Instance;
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = loc["PatchCreator_SaveDialogTitle"],
                SuggestedFileName = Path.GetFileName(OutputFileTextBox.Text ?? "patch.ips"),
                FileTypeChoices =
                [
                    new FilePickerFileType("IPS Patch") { Patterns = ["*.ips"] }
                ]
            });

            if (file != null)
                OutputFileTextBox.Text = file.Path.LocalPath;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"BrowseOutput_Click error: {ex}");
        }
    }

    private async void CreateButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        string original = OriginalFileTextBox.Text ?? "";
        string modified = ModifiedFileTextBox.Text ?? "";
        string output = OutputFileTextBox.Text ?? "";

        if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(modified) || string.IsNullOrEmpty(output))
        {
            ShowStatus(loc["PatchCreator_FillAllPaths"], isError: true);
            return;
        }

        CreateButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        StatusBorder.IsVisible = false;

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);
            await PatchCreator.CreateIpsAsync(original, modified, output, progress, loc);
            var patchSize = new FileInfo(output).Length;
            ShowStatus(string.Format(loc["PatchCreator_PatchCreated"], output, FileUtils.FormatFileSize(patchSize)), isError: false);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            ShowStatus(string.Format(loc["Common_ErrorFormat"], ex.Message), isError: true);
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
        StatusText.Foreground = isError ? StatusErrorBrush : StatusSuccessBrush;
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
