using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class DuplicateFinderView : UserControl
{
    public DuplicateFinderView()
    {
        InitializeComponent();
    }

    private async void BrowseFolder_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select ROM Folder to Scan",
            AllowMultiple = false
        });

        if (folders.Count == 0) return;

        FolderPathTextBox.Text = folders[0].Path.LocalPath;
        ScanButton.IsEnabled = true;
    }

    private async void ScanButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string folder = FolderPathTextBox.Text ?? "";
        if (string.IsNullOrEmpty(folder)) return;

        ScanButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        SummaryPanel.IsVisible = false;
        DuplicateList.ItemsSource = null;

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);
            var (groups, totalFiles) = await DuplicateFinder.FindDuplicatesAsync(folder, progress);
            var result = DuplicateFinder.BuildResult(groups, totalFiles);

            // Build display items
            var displayItems = groups.Select(g => new DuplicateDisplayItem
            {
                Header = $"CRC32: {g.Hash} — {g.FilePaths.Count} copies",
                Files = g.FilePaths.ToList()
            }).ToList();

            DuplicateList.ItemsSource = displayItems;

            SummaryText.Text = groups.Count > 0
                ? $"Found {groups.Count} duplicate group(s). {FileUtils.FormatFileSize(result.WastedBytes)} wasted by duplicates."
                : "No duplicates found.";
            SummaryPanel.IsVisible = true;
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
            ScanButton.IsEnabled = true;
        }
    }
}

public class DuplicateDisplayItem
{
    public string Header { get; set; } = string.Empty;
    public List<string> Files { get; set; } = [];
}
