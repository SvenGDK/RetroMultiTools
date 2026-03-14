using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class RomPatcherView : UserControl
{
    public RomPatcherView()
    {
        InitializeComponent();
    }

    private async void BrowseSourceRom_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFile("Select Source ROM",
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

    private async void BrowsePatchFile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFile("Select Patch File",
        [
            new FilePickerFileType("Patch Files") { Patterns = ["*.ips", "*.bps"] },
            FilePickerFileTypes.All
        ]);
        if (path == null) return;
        PatchFileTextBox.Text = path;
        UpdateOutputPath();
        UpdateApplyButton();
    }

    private async void BrowseOutputFile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Patched ROM As",
            SuggestedFileName = Path.GetFileName(OutputFileTextBox.Text ?? string.Empty) is { Length: > 0 } fn ? fn : "patched_rom"
        });

        if (file != null)
            OutputFileTextBox.Text = file.Path.LocalPath;
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
            !string.IsNullOrEmpty(PatchFileTextBox.Text);
    }

    private async void ApplyPatchButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string source = SourceRomTextBox.Text ?? "";
        string patch = PatchFileTextBox.Text ?? "";
        string output = OutputFileTextBox.Text ?? "";

        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(patch) || string.IsNullOrEmpty(output))
        {
            ShowStatus("Please fill in all file paths.", isError: true);
            return;
        }

        ApplyPatchButton.IsEnabled = false;
        ShowStatus("Applying patch...", isError: false);

        try
        {
            var ext = Path.GetExtension(patch).ToLowerInvariant();
            await Task.Run(() =>
            {
                if (ext == ".ips")
                    IpsPatcher.Apply(source, patch, output);
                else if (ext == ".bps")
                    BpsPatcher.Apply(source, patch, output);
                else
                    throw new NotSupportedException($"Unsupported patch format: {ext}");
            });

            ShowStatus($"✔ Patch applied successfully!\nOutput: {output}", isError: false);
        }
        catch (IOException ex)
        {
            ShowStatus($"✘ Error: {ex.Message}", isError: true);
        }
        catch (InvalidDataException ex)
        {
            ShowStatus($"✘ Error: {ex.Message}", isError: true);
        }
        catch (InvalidOperationException ex)
        {
            ShowStatus($"✘ Error: {ex.Message}", isError: true);
        }
        catch (NotSupportedException ex)
        {
            ShowStatus($"✘ Error: {ex.Message}", isError: true);
        }
        catch (UnauthorizedAccessException ex)
        {
            ShowStatus($"✘ Error: {ex.Message}", isError: true);
        }
        finally
        {
            ApplyPatchButton.IsEnabled = true;
        }
    }

    private void ShowStatus(string message, bool isError)
    {
        PatchStatusText.Text = message;
        PatchStatusText.Foreground = isError
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
