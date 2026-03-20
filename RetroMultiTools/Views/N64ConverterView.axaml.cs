using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class N64ConverterView : UserControl
{
    private static readonly IBrush StatusErrorBrush = new SolidColorBrush(Color.Parse("#F38BA8"));
    private static readonly IBrush StatusSuccessBrush = new SolidColorBrush(Color.Parse("#A6E3A1"));
    public N64ConverterView()
    {
        InitializeComponent();
    }

    private async void BrowseInput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFile(LocalizationManager.Instance["N64_SelectRom"],
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
                DetectedFormatText.Text = LocalizationManager.Instance["N64_UnknownFormat"];
                DetectedFormatPanel.IsVisible = true;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            DetectedFormatText.Text = LocalizationManager.Instance["N64_UnableToRead"];
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
            Title = LocalizationManager.Instance["N64_SaveAs"],
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
            ShowStatus(LocalizationManager.Instance["N64_SelectInputFile"], isError: true);
            return;
        }

        if (string.IsNullOrEmpty(output))
        {
            ShowStatus(LocalizationManager.Instance["N64_SelectOutputFile"], isError: true);
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
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
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
}
