using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class N64ConverterView : UserControl
{
    public N64ConverterView()
    {
        InitializeComponent();
    }

    private async void BrowseInput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFile("Select N64 ROM",
        [
            new FilePickerFileType("N64 ROM Files") { Patterns = ["*.z64", "*.n64", "*.v64"] },
            FilePickerFileTypes.All
        ]);
        if (path == null) return;

        InputFileTextBox.Text = path;
        DetectSourceFormat(path);
        UpdateOutputPath();
        UpdateConvertButton();
    }

    private void DetectSourceFormat(string path)
    {
        try
        {
            var format = N64FormatConverter.DetectFormat(path);
            if (format != null)
            {
                DetectedFormatText.Text = N64FormatConverter.FormatName(format.Value);
                DetectedFormatPanel.IsVisible = true;
            }
            else
            {
                DetectedFormatText.Text = "Unknown — not a recognized N64 ROM";
                DetectedFormatPanel.IsVisible = true;
            }
        }
        catch (IOException)
        {
            DetectedFormatText.Text = "Unable to read file";
            DetectedFormatPanel.IsVisible = true;
        }
        catch (UnauthorizedAccessException)
        {
            DetectedFormatText.Text = "Unable to read file";
            DetectedFormatPanel.IsVisible = true;
        }
    }

    private void UpdateOutputPath()
    {
        if (string.IsNullOrEmpty(InputFileTextBox.Text)) return;

        var targetFormat = GetSelectedTargetFormat();
        string ext = N64FormatConverter.FormatExtension(targetFormat);
        string dir = Path.GetDirectoryName(InputFileTextBox.Text) ?? "";
        string name = Path.GetFileNameWithoutExtension(InputFileTextBox.Text);
        OutputFileTextBox.Text = Path.Combine(dir, name + ext);
    }

    private void UpdateConvertButton()
    {
        ConvertButton.IsEnabled = !string.IsNullOrEmpty(InputFileTextBox.Text);
    }

    private void TargetFormatCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (InputFileTextBox == null) return;
        UpdateOutputPath();
    }

    private N64FormatConverter.N64Format GetSelectedTargetFormat()
    {
        if (TargetFormatCombo.SelectedItem is ComboBoxItem item)
        {
            return item.Tag?.ToString() switch
            {
                "n64" => N64FormatConverter.N64Format.LittleEndian,
                "v64" => N64FormatConverter.N64Format.ByteSwapped,
                _ => N64FormatConverter.N64Format.BigEndian
            };
        }
        return N64FormatConverter.N64Format.BigEndian;
    }

    private async void BrowseOutput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Converted ROM As",
            SuggestedFileName = Path.GetFileName(OutputFileTextBox.Text ?? "converted.z64")
        });

        if (file != null)
            OutputFileTextBox.Text = file.Path.LocalPath;
    }

    private async void ConvertButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string input = InputFileTextBox.Text ?? "";
        string output = OutputFileTextBox.Text ?? "";

        if (string.IsNullOrEmpty(input))
        {
            ShowStatus("Please select an input ROM file.", isError: true);
            return;
        }

        if (string.IsNullOrEmpty(output))
        {
            ShowStatus("Please specify an output file path.", isError: true);
            return;
        }

        ConvertButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        StatusBorder.IsVisible = false;

        try
        {
            var targetFormat = GetSelectedTargetFormat();
            var progress = new Progress<string>(msg => ProgressText.Text = msg);
            await N64FormatConverter.ConvertAsync(input, output, targetFormat, progress);

            ShowStatus($"✔ Conversion complete!\nOutput: {output}", isError: false);
        }
        catch (IOException ex)
        {
            ShowStatus($"✘ Error: {ex.Message}", isError: true);
        }
        catch (InvalidDataException ex)
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
}
