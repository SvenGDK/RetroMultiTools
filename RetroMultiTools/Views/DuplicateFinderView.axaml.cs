using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class DuplicateFinderView : UserControl
{
    private List<DuplicateGroup> _lastGroups = [];
    private CancellationTokenSource? _operationCts;

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
            Title = LocalizationManager.Instance["Duplicate_SelectFolder"],
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
        CancelButton.IsVisible = true;
        ProgressPanel.IsVisible = true;
        SummaryPanel.IsVisible = false;
        DeletePanel.IsVisible = false;
        ConfirmDeletePanel.IsVisible = false;
        DuplicateList.ItemsSource = null;
        _lastGroups = [];

        _operationCts?.Dispose();
        _operationCts = new CancellationTokenSource();

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);
            var (groups, totalFiles) = await DuplicateFinder.FindDuplicatesAsync(folder, progress, _operationCts.Token);
            var result = DuplicateFinder.BuildResult(groups, totalFiles);
            _lastGroups = groups;

            // Build display items
            var displayItems = groups.Select(g => new DuplicateDisplayItem
            {
                Header = $"CRC32: {g.Hash} — {g.FilePaths.Count} copies",
                Files = g.FilePaths.ToList()
            }).ToList();

            DuplicateList.ItemsSource = displayItems;

            SummaryText.Text = groups.Count > 0
                ? string.Format(LocalizationManager.Instance["Duplicate_FoundGroups"], groups.Count, FileUtils.FormatFileSize(result.WastedBytes))
                : LocalizationManager.Instance["Duplicate_NoDuplicates"];
            SummaryPanel.IsVisible = true;
            DeletePanel.IsVisible = groups.Count > 0;
        }
        catch (OperationCanceledException)
        {
            SummaryText.Text = LocalizationManager.Instance["Duplicate_ScanCancelled"];
            SummaryPanel.IsVisible = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SummaryText.Text = $"✘ Error: {ex.Message}";
            SummaryPanel.IsVisible = true;
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            CancelButton.IsVisible = false;
            ScanButton.IsEnabled = true;
        }
    }

    private void DeleteButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_lastGroups.Count == 0) return;

        int duplicateFileCount = _lastGroups.Sum(g => g.FilePaths.Count - 1);
        var result = DuplicateFinder.BuildResult(_lastGroups, 0);

        ConfirmDeleteText.Text = string.Format(LocalizationManager.Instance["Duplicate_ConfirmDelete"], duplicateFileCount, FileUtils.FormatFileSize(result.WastedBytes));
        DeletePanel.IsVisible = false;
        ConfirmDeletePanel.IsVisible = true;
    }

    private async void ConfirmDelete_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_lastGroups.Count == 0) return;

        ConfirmDeletePanel.IsVisible = false;
        CancelButton.IsVisible = true;
        ProgressPanel.IsVisible = true;
        ScanButton.IsEnabled = false;

        _operationCts?.Dispose();
        _operationCts = new CancellationTokenSource();

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);
            var (deletedCount, freedBytes) = await DuplicateFinder.DeleteDuplicatesAsync(_lastGroups, progress, _operationCts.Token);

            SummaryText.Text = string.Format(LocalizationManager.Instance["Duplicate_DeletedFiles"], deletedCount, FileUtils.FormatFileSize(freedBytes));
            DuplicateList.ItemsSource = null;
            _lastGroups = [];
            DeletePanel.IsVisible = false;
        }
        catch (OperationCanceledException)
        {
            SummaryText.Text = LocalizationManager.Instance["Duplicate_DeletionCancelled"];
            DeletePanel.IsVisible = _lastGroups.Count > 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SummaryText.Text = $"✘ Error deleting files: {ex.Message}";
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            CancelButton.IsVisible = false;
            ScanButton.IsEnabled = true;
        }
    }

    private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _operationCts?.Cancel();
    }

    private void CancelDelete_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ConfirmDeletePanel.IsVisible = false;
        DeletePanel.IsVisible = _lastGroups.Count > 0;
    }
}

public class DuplicateDisplayItem
{
    public string Header { get; set; } = string.Empty;
    public List<string> Files { get; set; } = [];
}
