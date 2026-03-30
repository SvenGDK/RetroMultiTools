using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Utilities;
using RetroMultiTools.Utilities.Patching;

namespace RetroMultiTools.Views.Patching;

public partial class RomPatcherView : UserControl
{
    private static readonly IBrush StatusErrorBrush = new SolidColorBrush(Color.Parse("#F38BA8"));
    private static readonly IBrush StatusSuccessBrush = new SolidColorBrush(Color.Parse("#A6E3A1"));
    public RomPatcherView()
    {
        InitializeComponent();
        OutputFileTextBox.TextChanged += (_, _) => UpdateApplyButton();
        DragDropHelper.EnableFileDrop(SourceRomTextBox, _ =>
        {
            UpdateOutputPath();
            UpdateApplyButton();
        });
        DragDropHelper.EnableFileDrop(PatchFileTextBox, _ =>
        {
            UpdateOutputPath();
            UpdateApplyButton();
        });
    }

    /// <summary>
    /// Pre-fills the source ROM file path.
    /// </summary>
    public void SetInputFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;
        SourceRomTextBox.Text = filePath;
        UpdateOutputPath();
        UpdateApplyButton();
    }

    private async void BrowseSourceRom_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var loc = LocalizationManager.Instance;
            var path = await PickFile(loc["Patcher_SelectSourceRom"],
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
            SourceRomTextBox.Text = path;
            UpdateOutputPath();
            UpdateApplyButton();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ShowStatus(string.Format(LocalizationManager.Instance["Patcher_Error"], ex.Message), isError: true);
        }
    }

    private async void BrowsePatchFile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var loc = LocalizationManager.Instance;
            var path = await PickFile(loc["Patcher_SelectPatchFile"],
            [
                new FilePickerFileType("Patch Files") { Patterns = ["*.ips", "*.bps", "*.xdelta", "*.vcdiff"] },
                FilePickerFileTypes.All
            ]);
            if (path == null) return;
            PatchFileTextBox.Text = path;
            UpdateOutputPath();
            UpdateApplyButton();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ShowStatus(string.Format(LocalizationManager.Instance["Patcher_Error"], ex.Message), isError: true);
        }
    }

    private async void BrowseOutputFile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var loc = LocalizationManager.Instance;
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = loc["Common_OutputFile"],
                SuggestedFileName = Path.GetFileName(OutputFileTextBox.Text ?? string.Empty) is { Length: > 0 } fn ? fn : "patched_rom"
            });

            if (file != null)
            {
                OutputFileTextBox.Text = file.Path.LocalPath;
                UpdateApplyButton();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ShowStatus(string.Format(LocalizationManager.Instance["Patcher_Error"], ex.Message), isError: true);
        }
    }

    private void UpdateOutputPath()
    {
        if (string.IsNullOrEmpty(SourceRomTextBox.Text)) return;
        var dir = Path.GetDirectoryName(SourceRomTextBox.Text) ?? "";
        var name = Path.GetFileNameWithoutExtension(SourceRomTextBox.Text) + "_patched";
        var ext = Path.GetExtension(SourceRomTextBox.Text);
        OutputFileTextBox.Text = Path.Combine(dir, name + ext);
    }

    private void UpdateApplyButton()
    {
        ApplyPatchButton.IsEnabled =
            !string.IsNullOrEmpty(SourceRomTextBox.Text) &&
            !string.IsNullOrEmpty(PatchFileTextBox.Text) &&
            !string.IsNullOrEmpty(OutputFileTextBox.Text);
    }

    private async void ApplyPatchButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        string source = SourceRomTextBox.Text ?? "";
        string patch = PatchFileTextBox.Text ?? "";
        string output = OutputFileTextBox.Text ?? "";

        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(patch) || string.IsNullOrEmpty(output))
        {
            ShowStatus(loc["Patcher_FillAllPaths"], isError: true);
            return;
        }

        ApplyPatchButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        ProgressText.Text = loc["Patcher_Applying"];
        StatusBorder.IsVisible = false;

        try
        {
            var ext = Path.GetExtension(patch).ToLowerInvariant();
            await Task.Run(() =>
            {
                if (ext == ".ips")
                    IpsPatcher.Apply(source, patch, output);
                else if (ext == ".bps")
                    BpsPatcher.Apply(source, patch, output);
                else if (ext is ".xdelta" or ".vcdiff")
                    XdeltaPatcher.Apply(source, patch, output);
                else
                    throw new NotSupportedException(
                        string.Format(loc["Patcher_UnsupportedFormat"], ext));
            });

            ShowStatus(string.Format(loc["Patcher_Success"], output), isError: false);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or NotSupportedException or UnauthorizedAccessException)
        {
            ShowStatus(string.Format(loc["Patcher_Error"], ex.Message), isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            UpdateApplyButton();
        }
    }

    private void ShowStatus(string message, bool isError)
    {
        PatchStatusText.Text = message;
        PatchStatusText.Foreground = isError ? StatusErrorBrush : StatusSuccessBrush;
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
