using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class ChecksumCalculatorView : UserControl
{
    public ChecksumCalculatorView()
    {
        InitializeComponent();
    }

    private async void BrowseFile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFile("Select File",
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
        FilePathTextBox.Text = path;
        CalculateButton.IsEnabled = true;
    }

    private async void CalculateButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string filePath = FilePathTextBox.Text ?? "";
        if (string.IsNullOrEmpty(filePath))
        {
            ShowStatus("Please select a file first.", isError: true);
            return;
        }

        CalculateButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        ResultPanel.IsVisible = false;
        StatusBorder.IsVisible = false;

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);
            var result = await ChecksumCalculator.CalculateAsync(filePath, progress);

            FileSizeText.Text = FileUtils.FormatFileSize(result.FileSize);
            Crc32Text.Text = result.CRC32;
            Md5Text.Text = result.MD5;
            Sha1Text.Text = result.SHA1;
            Sha256Text.Text = result.SHA256;

            ResultPanel.IsVisible = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowStatus($"✘ Error: {ex.Message}", isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            CalculateButton.IsEnabled = true;
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
