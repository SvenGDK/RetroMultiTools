using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class BatchHeaderFixerView : UserControl
{
    public BatchHeaderFixerView()
    {
        InitializeComponent();
    }

    private void ModeRadio_Checked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (InputLabel == null) return;
        if (sender is RadioButton rb && rb.IsChecked != true) return;

        bool isBatch = sender == BatchModeRadio;
        InputLabel.Text = isBatch ? "ROM Directory:" : "ROM File:";
        InputPathTextBox.Watermark = isBatch ? "Select a ROM directory..." : "Select a ROM file (.nes, .smc, .sfc, .gb, .gbc, .gba, .md, .gen, .sms, .gg, .z64, .n64, .v64, .32x, .a78, .lnx, .pce, .tg16, .vb, .vboy, .ngp, .ngc, .j64, .jag, .mx1, .mx2, .col, .cv, .sv, .nds, .int)...";
        OutputLabel.Text = isBatch ? "Output Directory:" : "Output File:";
        OutputPathTextBox.Watermark = isBatch ? "Output directory..." : "Output file path...";
        InputPathTextBox.Text = string.Empty;
        OutputPathTextBox.Text = string.Empty;
        StatusBorder.IsVisible = false;
        DetailsBorder.IsVisible = false;
        UpdateFixButton();
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
                new FilePickerFileType("Supported ROM Files") { Patterns = ["*.nes", "*.smc", "*.sfc", "*.gb", "*.gbc", "*.gba", "*.md", "*.gen", "*.sms", "*.gg", "*.z64", "*.n64", "*.v64", "*.32x", "*.a78", "*.lnx", "*.pce", "*.tg16", "*.vb", "*.vboy", "*.ngp", "*.ngc", "*.j64", "*.jag", "*.mx1", "*.mx2", "*.col", "*.cv", "*.sv", "*.nds", "*.int"] },
                FilePickerFileTypes.All
            ]);
            if (path == null) return;
            InputPathTextBox.Text = path;
        }

        UpdateOutputPath();
        UpdateFixButton();
    }

    private void UpdateOutputPath()
    {
        if (string.IsNullOrEmpty(InputPathTextBox.Text)) return;

        bool isBatch = BatchModeRadio.IsChecked == true;
        if (isBatch)
        {
            OutputPathTextBox.Text = InputPathTextBox.Text + "_fixed";
        }
        else
        {
            string dir = Path.GetDirectoryName(InputPathTextBox.Text) ?? "";
            string name = Path.GetFileNameWithoutExtension(InputPathTextBox.Text);
            string ext = Path.GetExtension(InputPathTextBox.Text);
            OutputPathTextBox.Text = Path.Combine(dir, name + "_fixed" + ext);
        }
    }

    private void UpdateFixButton()
    {
        FixButton.IsEnabled = !string.IsNullOrEmpty(InputPathTextBox.Text);
    }

    private async void BrowseOutput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        bool isBatch = BatchModeRadio.IsChecked == true;

        if (isBatch)
        {
            var path = await PickFolder("Select Output Directory");
            if (path != null) OutputPathTextBox.Text = path;
        }
        else
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Fixed ROM As",
                SuggestedFileName = Path.GetFileName(OutputPathTextBox.Text ?? "fixed.rom")
            });

            if (file != null)
                OutputPathTextBox.Text = file.Path.LocalPath;
        }
    }

    private async void FixButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string input = InputPathTextBox.Text ?? "";
        string output = OutputPathTextBox.Text ?? "";

        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(output))
        {
            ShowStatus("Please select input and output paths.", isError: true);
            return;
        }

        FixButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        StatusBorder.IsVisible = false;
        DetailsBorder.IsVisible = false;

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);
            bool isBatch = BatchModeRadio.IsChecked == true;

            if (isBatch)
            {
                var result = await BatchHeaderFixer.FixDirectoryAsync(input, output, progress);
                ShowStatus($"✔ Batch fix complete!\n{result.Summary}", isError: false);

                if (result.Details.Count > 0)
                {
                    DetailsText.Text = string.Join("\n", result.Details);
                    DetailsBorder.IsVisible = true;
                }
            }
            else
            {
                string resultMsg = await BatchHeaderFixer.FixSingleAsync(input, output, progress);
                ShowStatus(resultMsg, isError: false);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            ShowStatus($"✘ Error: {ex.Message}", isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            FixButton.IsEnabled = true;
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
