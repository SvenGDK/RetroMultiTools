using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Utilities;
using RetroMultiTools.Utilities.Analogue;

namespace RetroMultiTools.Views.Analogue;

public partial class AnalogueNtSuperNtView : UserControl
{
    private static readonly IBrush StatusErrorBrush = new SolidColorBrush(Color.Parse("#F38BA8"));
    private static readonly IBrush StatusSuccessBrush = new SolidColorBrush(Color.Parse("#A6E3A1"));

    private string _sdRoot = string.Empty;

    public AnalogueNtSuperNtView()
    {
        InitializeComponent();
    }

    private async void BrowseSdCard_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        var path = await PickFolder(loc["AnalogueNtSuperNt_BrowseSdCardTitle"]);
        if (path == null) return;

        if (!Directory.Exists(Path.Combine(path, "System")))
        {
            ShowStatus(loc["AnalogueNtSuperNt_InvalidSdCard"], isError: true);
            return;
        }

        _sdRoot = path;
        SdCardPathTextBox.Text = path;
        GenerateFontButton.IsEnabled = true;
        ShowStatus(loc["AnalogueNtSuperNt_SdCardPathSet"], isError: false);
    }

    private async void BrowseFontImage_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = loc["Analogue_FontSourceDialogTitle"],
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("BMP Images") { Patterns = ["*.bmp"] },
                FilePickerFileTypes.All
            ]
        });

        if (files.Count > 0)
            FontImageTextBox.Text = files[0].Path.LocalPath;
    }

    private async void GenerateFont_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        var target = SuperNtRadio.IsChecked == true
            ? AnalogueFontGenerator.ConsoleTarget.SuperNt
            : AnalogueFontGenerator.ConsoleTarget.Nt;

        string fontDir = Path.Combine(_sdRoot, AnalogueFontGenerator.GetFontDirectory(target));
        string outputPath = Path.Combine(fontDir, AnalogueFontGenerator.GetDefaultFontFileName(target));

        GenerateFontButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);
            string imagePath = FontImageTextBox.Text ?? string.Empty;

            if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
            {
                await AnalogueFontGenerator.GenerateFontFromImageAsync(
                    imagePath, outputPath, target, progress);
            }
            else
            {
                await AnalogueFontGenerator.GenerateDefaultFontAsync(outputPath, target, progress);
            }

            ShowStatus(string.Format(loc["Analogue_FontGenerated"], outputPath), isError: false);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            ShowStatus(string.Format(loc["Analogue_FontError"], ex.Message), isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            GenerateFontButton.IsEnabled = true;
        }
    }

    private async void BrowseNesRom_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = loc["AnalogueNtSuperNt_SelectNesRomTitle"],
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("NES ROMs") { Patterns = ["*.nes"] },
                FilePickerFileTypes.All
            ]
        });

        if (files.Count > 0)
        {
            NesRomTextBox.Text = files[0].Path.LocalPath;
            RepairHeaderButton.IsEnabled = true;
        }
    }

    private async void RepairHeader_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        string romPath = NesRomTextBox.Text ?? string.Empty;
        if (string.IsNullOrEmpty(romPath) || !File.Exists(romPath))
        {
            ShowStatus(loc["AnalogueNtSuperNt_InvalidNesRom"], isError: true);
            return;
        }

        RepairHeaderButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        ProgressText.Text = loc["AnalogueNtSuperNt_RepairingHeader"];

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);

            // Write to a temp file first to avoid corrupting the original if the write fails
            string tempPath = romPath + ".tmp";
            string result = await BatchHeaderFixer.FixSingleAsync(romPath, tempPath, progress);

            // Replace original only on success
            if (File.Exists(tempPath))
            {
                File.Move(tempPath, romPath, overwrite: true);
            }

            ShowStatus(string.Format(loc["Common_SuccessFormat"], result), isError: false);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            // Clean up temp file on failure
            string tempPath = romPath + ".tmp";
            try { if (File.Exists(tempPath)) File.Delete(tempPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            ShowStatus(string.Format(loc["AnalogueNtSuperNt_RepairError"], ex.Message), isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            RepairHeaderButton.IsEnabled = true;
        }
    }

    private void ShowStatus(string message, bool isError)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError ? StatusErrorBrush : StatusSuccessBrush;
        StatusBorder.IsVisible = true;
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
