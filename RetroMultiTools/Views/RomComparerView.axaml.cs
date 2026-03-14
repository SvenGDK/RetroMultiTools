using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class RomComparerView : UserControl
{
    public RomComparerView()
    {
        InitializeComponent();
    }

    private async void BrowseFile1_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFile("Select First File");
        if (path == null) return;
        File1TextBox.Text = path;
        UpdateCompareButton();
    }

    private async void BrowseFile2_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFile("Select Second File");
        if (path == null) return;
        File2TextBox.Text = path;
        UpdateCompareButton();
    }

    private void UpdateCompareButton()
    {
        CompareButton.IsEnabled =
            !string.IsNullOrEmpty(File1TextBox.Text) &&
            !string.IsNullOrEmpty(File2TextBox.Text);
    }

    private async void CompareButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string file1 = File1TextBox.Text ?? "";
        string file2 = File2TextBox.Text ?? "";

        if (string.IsNullOrEmpty(file1) || string.IsNullOrEmpty(file2))
        {
            ShowStatus("Please select both files.", isError: true);
            return;
        }

        CompareButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        ResultPanel.IsVisible = false;
        StatusBorder.IsVisible = false;

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);
            var result = await RomComparer.CompareAsync(file1, file2, progress);

            if (result.Identical)
            {
                MatchStatusText.Text = "✔ Files are identical";
                MatchStatusText.Foreground = new Avalonia.Media.SolidColorBrush(
                    Avalonia.Media.Color.Parse("#A6E3A1"));
            }
            else
            {
                MatchStatusText.Text = "✘ Files differ";
                MatchStatusText.Foreground = new Avalonia.Media.SolidColorBrush(
                    Avalonia.Media.Color.Parse("#F38BA8"));
            }

            Size1Text.Text = FileUtils.FormatFileSize(result.FileSize1);
            Size2Text.Text = FileUtils.FormatFileSize(result.FileSize2);
            DiffCountText.Text = result.DifferingByteCount.ToString("N0");
            FirstMismatchText.Text = result.FirstMismatchOffset >= 0
                ? $"0x{result.FirstMismatchOffset:X} ({result.FirstMismatchOffset:N0})"
                : "N/A";

            ResultPanel.IsVisible = true;
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
            CompareButton.IsEnabled = true;
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

    private async Task<string?> PickFile(string title)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter =
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
            ]
        });

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }
}
