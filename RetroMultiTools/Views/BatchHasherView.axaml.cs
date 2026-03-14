using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class BatchHasherView : UserControl
{
    private List<BatchHashResult>? _results;

    public BatchHasherView()
    {
        InitializeComponent();
    }

    private async void BrowseFolder_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select ROM Folder",
            AllowMultiple = false
        });

        if (folders.Count == 0) return;

        FolderPathTextBox.Text = folders[0].Path.LocalPath;
        HashButton.IsEnabled = true;
    }

    private async void HashButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string folder = FolderPathTextBox.Text ?? "";
        if (string.IsNullOrEmpty(folder)) return;

        HashButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        SummaryPanel.IsVisible = false;
        ExportButton.IsVisible = false;
        ResultsList.ItemsSource = null;

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);
            _results = await BatchHasher.HashDirectoryAsync(
                folder,
                IncludeMd5.IsChecked == true,
                IncludeSha1.IsChecked == true,
                IncludeSha256.IsChecked == true,
                progress);

            var displayItems = _results.Select(r => new HashDisplayItem
            {
                FileName = r.FileName,
                SizeDisplay = $"Size: {r.FileSizeFormatted}",
                CRC32Display = $"CRC32: {r.CRC32}",
                MD5Display = r.MD5 != null ? $"MD5:   {r.MD5}" : "",
                SHA1Display = r.SHA1 != null ? $"SHA1:  {r.SHA1}" : "",
                SHA256Display = r.SHA256 != null ? $"SHA256: {r.SHA256}" : "",
                HasMD5 = r.MD5 != null,
                HasSHA1 = r.SHA1 != null,
                HasSHA256 = r.SHA256 != null,
            }).ToList();

            ResultsList.ItemsSource = displayItems;

            long totalSize = _results.Sum(r => r.FileSize);
            SummaryText.Text = $"Hashed {_results.Count} file(s) — Total size: {FileUtils.FormatFileSize(totalSize)}";
            SummaryPanel.IsVisible = true;
            ExportButton.IsVisible = _results.Count > 0;
        }
        catch (IOException ex)
        {
            SummaryText.Text = $"✘ Error: {ex.Message}";
            SummaryPanel.IsVisible = true;
        }
        catch (UnauthorizedAccessException ex)
        {
            SummaryText.Text = $"✘ Error: {ex.Message}";
            SummaryPanel.IsVisible = true;
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            HashButton.IsEnabled = true;
        }
    }

    private async void ExportButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_results == null || _results.Count == 0) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Hash Results",
            SuggestedFileName = "hash_report.csv",
            FileTypeChoices =
            [
                new FilePickerFileType("CSV") { Patterns = ["*.csv"] },
                new FilePickerFileType("Text Report") { Patterns = ["*.txt"] },
                new FilePickerFileType("SFV Checksum") { Patterns = ["*.sfv"] },
                new FilePickerFileType("MD5 Sum") { Patterns = ["*.md5"] },
            ]
        });

        if (file == null) return;

        string path = file.Path.LocalPath;
        string ext = Path.GetExtension(path).ToLowerInvariant();

        var format = ext switch
        {
            ".csv" => BatchHashExportFormat.Csv,
            ".sfv" => BatchHashExportFormat.SfvChecksum,
            ".md5" => BatchHashExportFormat.Md5Sum,
            _ => BatchHashExportFormat.Text,
        };

        try
        {
            await BatchHasher.ExportResultsAsync(_results, path, format);
            SummaryText.Text = $"✔ Exported to: {path}";
        }
        catch (IOException ex)
        {
            SummaryText.Text = $"✘ Export error: {ex.Message}";
        }
        catch (UnauthorizedAccessException ex)
        {
            SummaryText.Text = $"✘ Export error: {ex.Message}";
        }
    }
}

public class HashDisplayItem
{
    public string FileName { get; set; } = string.Empty;
    public string SizeDisplay { get; set; } = string.Empty;
    public string CRC32Display { get; set; } = string.Empty;
    public string MD5Display { get; set; } = string.Empty;
    public string SHA1Display { get; set; } = string.Empty;
    public string SHA256Display { get; set; } = string.Empty;
    public bool HasMD5 { get; set; }
    public bool HasSHA1 { get; set; }
    public bool HasSHA256 { get; set; }
}
