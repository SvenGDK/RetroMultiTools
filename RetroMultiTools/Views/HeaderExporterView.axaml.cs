using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class HeaderExporterView : UserControl
{
    public HeaderExporterView()
    {
        InitializeComponent();
    }

    private void ModeRadio_Checked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (InputLabel == null) return; // Not yet initialized
        if (sender is RadioButton rb && rb.IsChecked != true) return;

        bool isBatch = sender == BatchModeRadio;
        InputLabel.Text = isBatch ? "ROM Directory:" : "ROM File:";
        InputPathTextBox.Watermark = isBatch ? "Select a ROM directory..." : "Select a ROM file...";
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
            var path = await PickFolder("Select ROM Directory");
            if (path == null) return;
            InputPathTextBox.Text = path;
        }
        else
        {
            var path = await PickFile("Select ROM File",
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
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Report As",
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
        string input = InputPathTextBox.Text ?? "";
        string output = OutputFileTextBox.Text ?? "";

        if (string.IsNullOrEmpty(input))
        {
            ShowStatus("Please select an input ROM file or directory.", isError: true);
            return;
        }

        if (string.IsNullOrEmpty(output))
        {
            ShowStatus("Please specify an output file path.", isError: true);
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

            ShowStatus($"✔ Export complete!\nOutput: {output}", isError: false);
        }
        catch (IOException ex)
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
            ExportButton.IsEnabled = true;
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
