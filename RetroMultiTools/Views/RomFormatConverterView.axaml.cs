using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class RomFormatConverterView : UserControl
{
    public RomFormatConverterView()
    {
        InitializeComponent();
    }

    private void ModeRadio_Checked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (InputLabel == null) return;
        if (sender is RadioButton rb && rb.IsChecked != true) return;

        bool isBatch = sender == BatchModeRadio;
        InputLabel.Text = isBatch ? "ROM Directory:" : "ROM File:";
        InputPathTextBox.Watermark = isBatch ? "Select a ROM directory..." : "Select a ROM file...";
        OutputLabel.Text = isBatch ? "Output Directory:" : "Output File:";
        OutputPathTextBox.Watermark = isBatch ? "Output directory..." : "Output file path...";
        InputPathTextBox.Text = string.Empty;
        OutputPathTextBox.Text = string.Empty;
        ConversionCombo.Items.Clear();
        StatusBorder.IsVisible = false;
        UpdateConvertButton();
    }

    private async void BrowseInput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        bool isBatch = BatchModeRadio.IsChecked == true;

        if (isBatch)
        {
            var path = await PickFolder("Select ROM Directory");
            if (path == null) return;
            InputPathTextBox.Text = path;
            PopulateBatchConversions();
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
                                       "*.pce","*.tg16",
                                       "*.ngp","*.ngc",
                                       "*.col","*.cv","*.int",
                                       "*.mx1","*.mx2",
                                       "*.sv","*.ccc",
                                       "*.iso","*.cue","*.3do",
                                       "*.chd","*.rvz","*.gcm" ]
                },
                FilePickerFileTypes.All
            ]);
            if (path == null) return;
            InputPathTextBox.Text = path;
            PopulateSingleConversions(path);
        }

        UpdateOutputPath();
        UpdateConvertButton();
    }

    private void PopulateSingleConversions(string filePath)
    {
        ConversionCombo.Items.Clear();

        try
        {
            var conversions = RomFormatConverter.GetAvailableConversions(filePath);
            foreach (var conv in conversions)
            {
                ConversionCombo.Items.Add(new ComboBoxItem
                {
                    Content = RomFormatConverter.GetConversionName(conv),
                    Tag = conv
                });
            }

            if (ConversionCombo.Items.Count > 0)
                ConversionCombo.SelectedIndex = 0;
        }
        catch (IOException)
        {
            ShowStatus("Unable to read file.", isError: true);
        }
    }

    private void PopulateBatchConversions()
    {
        ConversionCombo.Items.Clear();

        foreach (RomFormatConverter.ConversionType conv in Enum.GetValues<RomFormatConverter.ConversionType>())
        {
            ConversionCombo.Items.Add(new ComboBoxItem
            {
                Content = RomFormatConverter.GetConversionName(conv),
                Tag = conv
            });
        }

        if (ConversionCombo.Items.Count > 0)
            ConversionCombo.SelectedIndex = 0;
    }

    private void ConversionCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateOutputPath();
    }

    private void UpdateOutputPath()
    {
        if (string.IsNullOrEmpty(InputPathTextBox.Text)) return;

        bool isBatch = BatchModeRadio.IsChecked == true;
        if (isBatch)
        {
            OutputPathTextBox.Text = InputPathTextBox.Text + "_converted";
        }
        else
        {
            string dir = Path.GetDirectoryName(InputPathTextBox.Text) ?? "";
            string name = Path.GetFileNameWithoutExtension(InputPathTextBox.Text);
            string ext = Path.GetExtension(InputPathTextBox.Text);

            if (ConversionCombo.SelectedItem is ComboBoxItem item && item.Tag is RomFormatConverter.ConversionType convType)
            {
                if (convType == RomFormatConverter.ConversionType.ConvertToCHD)
                    ext = ".chd";
                else if (convType == RomFormatConverter.ConversionType.ConvertToRVZ)
                    ext = ".rvz";
            }

            OutputPathTextBox.Text = Path.Combine(dir, name + "_converted" + ext);
        }
    }

    private void UpdateConvertButton()
    {
        ConvertButton.IsEnabled = !string.IsNullOrEmpty(InputPathTextBox.Text) &&
                                  ConversionCombo.SelectedItem != null;
    }

    private async void BrowseOutput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        bool isBatch = BatchModeRadio.IsChecked == true;

        if (isBatch)
        {
            var path = await PickFolder("Select Output Directory");
            if (path != null)
                OutputPathTextBox.Text = path;
        }
        else
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Converted ROM As",
                SuggestedFileName = Path.GetFileName(OutputPathTextBox.Text ?? "converted.rom")
            });

            if (file != null)
                OutputPathTextBox.Text = file.Path.LocalPath;
        }
    }

    private async void ConvertButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string input = InputPathTextBox.Text ?? "";
        string output = OutputPathTextBox.Text ?? "";

        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(output))
        {
            ShowStatus("Please select input and output paths.", isError: true);
            return;
        }

        if (ConversionCombo.SelectedItem is not ComboBoxItem item || item.Tag is not RomFormatConverter.ConversionType convType)
        {
            ShowStatus("Please select a conversion type.", isError: true);
            return;
        }

        ConvertButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        StatusBorder.IsVisible = false;

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);
            bool isBatch = BatchModeRadio.IsChecked == true;

            if (isBatch)
            {
                var result = await RomFormatConverter.ConvertBatchAsync(input, output, convType, progress);
                ShowStatus($"✔ Batch conversion complete!\n{result.Summary}", isError: false);
            }
            else
            {
                await RomFormatConverter.ConvertAsync(input, output, convType, progress);
                ShowStatus($"✔ Conversion complete!\nOutput: {output}", isError: false);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            ShowStatus($"✘ Error: {ex.Message}", isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            ConvertButton.IsEnabled = true;
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
