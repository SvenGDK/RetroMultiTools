using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class MameChdVerifierView : UserControl
{
    public MameChdVerifierView()
    {
        InitializeComponent();
    }

    private void ModeRadio_Checked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (InputLabel == null) return;
        if (sender is RadioButton rb && rb.IsChecked != true) return;

        bool isBatch = sender == BatchModeRadio;
        InputLabel.Text = isBatch ? "CHD Directory:" : "CHD File:";
        InputTextBox.Watermark = isBatch ? "Select a directory containing CHD files..." : "Select a CHD file...";
        InputTextBox.Text = string.Empty;
        StatusBorder.IsVisible = false;
        ResultsBorder.IsVisible = false;
        VerifyButton.IsEnabled = false;
    }

    private async void BrowseInput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        bool isBatch = BatchModeRadio.IsChecked == true;

        if (isBatch)
        {
            var path = await PickFolder("Select CHD Directory");
            if (path != null) InputTextBox.Text = path;
        }
        else
        {
            var path = await PickFile("Select CHD File",
            [
                new FilePickerFileType("CHD Files") { Patterns = ["*.chd"] },
                FilePickerFileTypes.All
            ]);
            if (path != null) InputTextBox.Text = path;
        }

        VerifyButton.IsEnabled = !string.IsNullOrEmpty(InputTextBox.Text);
    }

    private async void VerifyButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string input = InputTextBox.Text ?? "";
        if (string.IsNullOrEmpty(input))
        {
            ShowStatus("Please select a CHD file or directory.", isError: true);
            return;
        }

        VerifyButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        StatusBorder.IsVisible = false;
        ResultsBorder.IsVisible = false;

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);
            bool isBatch = BatchModeRadio.IsChecked == true;

            if (isBatch)
            {
                var batchResult = await MameChdVerifier.VerifyDirectoryAsync(input, progress);
                ShowStatus($"✔ Verification complete!\n{batchResult.Summary}", isError: false);

                var lines = new System.Text.StringBuilder();
                foreach (var r in batchResult.Results)
                {
                    string icon = r.IsValid ? "✔" : "✘";
                    lines.AppendLine($"{icon} {r.FileName}");

                    if (r.IsValid)
                    {
                        lines.AppendLine($"   Version: CHD v{r.Version}  |  Compression: {r.Compression}");
                        lines.AppendLine($"   Logical size: {FormatSize(r.LogicalSize)}  |  Hunk size: {FormatSize(r.HunkSize)}");
                        if (!string.IsNullOrEmpty(r.SHA1))
                            lines.AppendLine($"   SHA-1: {r.SHA1}");
                    }
                    else
                    {
                        lines.AppendLine($"   Error: {r.Error}");
                    }
                    lines.AppendLine();
                }
                ResultsText.Text = lines.ToString();
                ResultsBorder.IsVisible = true;
            }
            else
            {
                var result = await MameChdVerifier.VerifyAsync(input, progress);

                if (result.IsValid)
                {
                    string message = $"✔ Valid CHD file\n\n" +
                                     $"Version:      CHD v{result.Version}\n" +
                                     $"Compression:  {result.Compression}\n" +
                                     $"Logical size: {FormatSize(result.LogicalSize)}\n" +
                                     $"Hunk size:    {FormatSize(result.HunkSize)}\n" +
                                     $"File size:    {FormatSize(result.FileSize)}\n";

                    if (result.UnitSize > 0)
                        message += $"Unit size:    {FormatSize(result.UnitSize)}\n";

                    if (!string.IsNullOrEmpty(result.SHA1))
                        message += $"\nSHA-1:     {result.SHA1}";
                    if (!string.IsNullOrEmpty(result.RawSHA1) && result.RawSHA1 != new string('0', 40))
                        message += $"\nRaw SHA-1: {result.RawSHA1}";
                    if (result.HasParent)
                        message += $"\nParent SHA-1: {result.ParentSHA1}";

                    ShowStatus(message, isError: false);
                }
                else
                {
                    ShowStatus($"✘ Invalid CHD file\n\n{result.Error}", isError: true);
                }
            }
        }
        catch (IOException ex)
        {
            ShowStatus($"✘ Error: {ex.Message}", isError: true);
        }
        catch (InvalidOperationException ex)
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
            VerifyButton.IsEnabled = true;
        }
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / (double)(1L << 30):F2} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):F2} MB",
        >= 1L << 10 => $"{bytes / (double)(1L << 10):F2} KB",
        _ => $"{bytes} bytes"
    };

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
