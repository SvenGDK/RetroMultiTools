using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using RetroMultiTools.Utilities;
using RetroMultiTools.Utilities.Analogue;

namespace RetroMultiTools.Views.Analogue;

public partial class AnalogueMegaSgView : UserControl
{
    private static readonly IBrush StatusErrorBrush = new SolidColorBrush(Color.Parse("#F38BA8"));
    private static readonly IBrush StatusSuccessBrush = new SolidColorBrush(Color.Parse("#A6E3A1"));

    private static readonly SaveConversion[] ConversionMap =
    [
        SaveConversion.SwapEndian16,
        SaveConversion.SwapEndian32,
        SaveConversion.PadToPowerOfTwo,
        SaveConversion.TrimTrailingZeros,
        SaveConversion.TrimTrailingFF,
        SaveConversion.SrmToSav,
        SaveConversion.SavToSrm,
    ];

    private static readonly string[] ConversionExtensions =
    [
        "",     // SwapEndian16
        "",     // SwapEndian32
        "",     // PadToPowerOfTwo
        "",     // TrimTrailingZeros
        "",     // TrimTrailingFF
        ".sav", // SrmToSav
        ".srm", // SavToSrm
    ];

    private string _sdRoot = string.Empty;
    private SaveFileInfo? _saveFileInfo;

    public AnalogueMegaSgView()
    {
        InitializeComponent();
    }

    // ── SD Card Selection ──────────────────────────────────────────────

    private async void BrowseSdCard_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFolder("Select Analogue Mega SG SD Card");
        if (path != null)
        {
            _sdRoot = path;
            SdCardPathTextBox.Text = path;
            GenerateFontButton.IsEnabled = true;
            ShowStatus("✔ SD card path set.", isError: false);
        }
    }

    // ── Font Generator ─────────────────────────────────────────────────

    private async void BrowseFontImage_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Font Source Image (128×128 BMP)",
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
        const AnalogueFontGenerator.ConsoleTarget target = AnalogueFontGenerator.ConsoleTarget.MegaSg;

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

            ShowStatus($"✔ Font generated successfully at {outputPath}", isError: false);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            ShowStatus($"✘ Error generating font: {ex.Message}", isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            GenerateFontButton.IsEnabled = true;
        }
    }

    // ── Saves-Manager ──────────────────────────────────────────────────

    private async void BrowseSaveFile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Save File",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Save Files") { Patterns = ["*.sav", "*.srm", "*.eep", "*.fla", "*.sra"] },
                FilePickerFileTypes.All
            ]
        });

        if (files.Count == 0) return;

        string path = files[0].Path.LocalPath;
        SaveFileTextBox.Text = path;

        try
        {
            _saveFileInfo = SaveFileConverter.Analyze(path);
            SaveFileSizeText.Text = _saveFileInfo.FileSizeFormatted;
            SaveDetectedTypeText.Text = _saveFileInfo.DetectedType;
            SavePowerOfTwoText.Text = _saveFileInfo.IsPowerOfTwo ? "Yes ✔" : "No ✘";
            SaveInfoPanel.IsVisible = true;
            ConvertSaveButton.IsEnabled = true;
        }
        catch (Exception ex) when (ex is IOException or FileNotFoundException)
        {
            ShowStatus($"✘ Error analyzing save file: {ex.Message}", isError: true);
            SaveInfoPanel.IsVisible = false;
            ConvertSaveButton.IsEnabled = false;
        }
    }

    private async void ConvertSave_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_saveFileInfo == null || string.IsNullOrEmpty(SaveFileTextBox.Text))
        {
            ShowStatus("✘ Please select a save file first.", isError: true);
            return;
        }

        int selectedIndex = ConversionComboBox.SelectedIndex;
        if (selectedIndex < 0 || selectedIndex >= ConversionMap.Length)
        {
            ShowStatus("✘ Please select a conversion type.", isError: true);
            return;
        }

        var conversion = ConversionMap[selectedIndex];
        string inputPath = _saveFileInfo.FilePath;

        // Build output path
        string dir = Path.GetDirectoryName(inputPath) ?? string.Empty;
        string baseName = Path.GetFileNameWithoutExtension(inputPath);
        string ext = ConversionExtensions[selectedIndex];
        if (string.IsNullOrEmpty(ext))
            ext = Path.GetExtension(inputPath);
        string outputPath = Path.Combine(dir, baseName + "_converted" + ext);

        ConvertSaveButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);
            await SaveFileConverter.ConvertAsync(inputPath, outputPath, conversion, progress);
            ShowStatus($"✔ Save converted successfully: {Path.GetFileName(outputPath)}", isError: false);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            ShowStatus($"✘ Error converting save: {ex.Message}", isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            ConvertSaveButton.IsEnabled = true;
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────

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
