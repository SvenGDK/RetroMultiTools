using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class HeaderExporterView : UserControl
{
    private static readonly IBrush StatusErrorBrush = new SolidColorBrush(Color.Parse("#F38BA8"));
    private static readonly IBrush StatusSuccessBrush = new SolidColorBrush(Color.Parse("#A6E3A1"));
    public HeaderExporterView()
    {
        InitializeComponent();
    }

    private void ModeRadio_Checked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (InputLabel == null) return; // Not yet initialized
        if (sender is RadioButton rb && rb.IsChecked != true) return;

        bool isBatch = sender == BatchModeRadio;
        InputLabel.Text = isBatch ? LocalizationManager.Instance["HeaderExport_RomDirectory"] : LocalizationManager.Instance["HeaderExport_RomFile"];
        InputPathTextBox.Watermark = isBatch ? LocalizationManager.Instance["HeaderExport_SelectRomDirectory"] : LocalizationManager.Instance["HeaderExport_SelectRomFile"];
        InputPathTextBox.Text = string.Empty;
        OutputFileTextBox.Text = string.Empty;
        StatusBorder.IsVisible = false;
        UpdateExportButton();
    }

    private void FormatRadio_Checked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (OutputFileTextBox == null) return;
        if (sender is RadioButton rb && rb.IsChecked != true) return;
        UpdateOutputPath();
    }

    private async void BrowseInput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        bool isBatch = BatchModeRadio.IsChecked == true;

        if (isBatch)
        {
            var path = await PickFolder(LocalizationManager.Instance["HeaderExport_SelectRomDirTitle"]);
            if (path == null) return;
            InputPathTextBox.Text = path;
        }
        else
        {
            var path = await PickFile(LocalizationManager.Instance["HeaderExport_SelectRomFileTitle"],
            [
                new FilePickerFileType("ROM Files")
                {
                    Patterns = [ "*.nes","*.smc","*.sfc","*.z64","*.n64","*.v64",
                                       "*.gb","*.gbc","*.gba","*.vb","*.vboy",
                                       "*.sms","*.md","*.gen",
                                       "*.bin","*.32x","*.gg","*.a26","*.a52","*.a78",
                                       "*.j64","*.jag","*.lnx","*.lyx",
                                       "*.pce","*.tg16","*.iso","*.cue","*.3do",
                                       "*.chd","*.rvz","*.gcm",
                                       "*.ngp","*.ngc",
                                       "*.col","*.cv","*.int",
                                       "*.mx1","*.mx2",
                                       "*.dsk","*.cdt","*.sna",
                                       "*.tap",
                                       "*.mo5","*.k7","*.fd",
                                       "*.sv","*.ccc" ]
                },
                FilePickerFileTypes.All
            ]);
            if (path == null) return;
            InputPathTextBox.Text = path;
        }

        UpdateOutputPath();
        UpdateExportButton();
    }

    private async void BrowseOutput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = loc["HeaderExport_SaveDialogTitle"],
            SuggestedFileName = Path.GetFileName(OutputFileTextBox.Text ?? "rom_report.txt")
        });

        if (file != null)
            OutputFileTextBox.Text = file.Path.LocalPath;
    }

    private void UpdateOutputPath()
    {
        if (string.IsNullOrEmpty(InputPathTextBox.Text)) return;

        bool isCsv = CsvFormatRadio.IsChecked == true;
        string ext = isCsv ? ".csv" : ".txt";

        bool isBatch = BatchModeRadio.IsChecked == true;
        if (isBatch)
        {
            string dirName = Path.GetFileName(InputPathTextBox.Text.TrimEnd(Path.DirectorySeparatorChar));
            string parentDir = Path.GetDirectoryName(InputPathTextBox.Text) ?? InputPathTextBox.Text;
            OutputFileTextBox.Text = Path.Combine(parentDir, dirName + "_report" + ext);
        }
        else
        {
            string dir = Path.GetDirectoryName(InputPathTextBox.Text) ?? "";
            string name = Path.GetFileNameWithoutExtension(InputPathTextBox.Text);
            OutputFileTextBox.Text = Path.Combine(dir, name + "_report" + ext);
        }
    }

    private void UpdateExportButton()
    {
        ExportButton.IsEnabled = !string.IsNullOrEmpty(InputPathTextBox.Text);
    }

    private async void ExportButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        string input = InputPathTextBox.Text ?? "";
        string output = OutputFileTextBox.Text ?? "";

        if (string.IsNullOrEmpty(input))
        {
            ShowStatus(loc["HeaderExport_SelectInput"], isError: true);
            return;
        }

        if (string.IsNullOrEmpty(output))
        {
            ShowStatus(loc["HeaderExport_SelectOutputPath"], isError: true);
            return;
        }

        // Ensure output has the correct extension
        bool isCsv = CsvFormatRadio.IsChecked == true;
        string expectedExt = isCsv ? ".csv" : ".txt";
        if (!output.EndsWith(expectedExt, StringComparison.OrdinalIgnoreCase))
            output = Path.ChangeExtension(output, expectedExt);

        ExportButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        StatusBorder.IsVisible = false;

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);
            bool isBatch = BatchModeRadio.IsChecked == true;

            if (isBatch)
                await RomHeaderExporter.ExportBatchAsync(input, output, progress);
            else
                await RomHeaderExporter.ExportSingleAsync(input, output, progress);

            ShowStatus(string.Format(loc["HeaderExport_ExportComplete"], output), isError: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowStatus(string.Format(loc["Common_ErrorFormat"], ex.Message), isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            ExportButton.IsEnabled = true;
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
