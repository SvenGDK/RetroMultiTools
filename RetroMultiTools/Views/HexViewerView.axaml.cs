using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class HexViewerView : UserControl
{
    private string? _currentFile;
    private HexViewData? _currentData;

    public HexViewerView()
    {
        InitializeComponent();
    }

    private async void BrowseFile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select File to View",
            AllowMultiple = false,
            FileTypeFilter = [FilePickerFileTypes.All]
        });

        if (files.Count == 0) return;

        _currentFile = files[0].Path.LocalPath;
        FilePathTextBox.Text = _currentFile;
        LoadPage(0);
    }

    private void LoadPage(long offset)
    {
        if (_currentFile == null) return;

        try
        {
            _currentData = HexViewer.LoadPage(_currentFile, offset);
            HexDisplay.ItemsSource = _currentData.FormattedLines;

            FileSizeText.Text = $"Size: {FileUtils.FormatFileSize(_currentData.FileSize)} ({_currentData.FileSize:N0} bytes)";
            PageInfoText.Text = $"Offset: 0x{_currentData.Offset:X8} — Page {_currentData.CurrentPage + 1} of {_currentData.TotalPages}";
            OffsetTextBox.Text = _currentData.Offset.ToString("X8");

            InfoPanel.IsVisible = true;
            NavPanel.IsVisible = true;
            SearchPanel.IsVisible = true;

            PrevButton.IsEnabled = _currentData.Offset > 0;
            FirstButton.IsEnabled = _currentData.Offset > 0;
            NextButton.IsEnabled = _currentData.Offset + HexViewer.DefaultPageSize < _currentData.FileSize;
            LastButton.IsEnabled = _currentData.Offset + HexViewer.DefaultPageSize < _currentData.FileSize;
        }
        catch (IOException ex)
        {
            HexDisplay.ItemsSource = new[] { $"Error: {ex.Message}" };
        }
        catch (UnauthorizedAccessException ex)
        {
            HexDisplay.ItemsSource = new[] { $"Error: {ex.Message}" };
        }
    }

    private void FirstPage_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        LoadPage(0);
    }

    private void PrevPage_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_currentData == null) return;
        long newOffset = Math.Max(0, _currentData.Offset - HexViewer.DefaultPageSize);
        LoadPage(newOffset);
    }

    private void NextPage_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_currentData == null) return;
        long newOffset = _currentData.Offset + HexViewer.DefaultPageSize;
        if (newOffset < _currentData.FileSize)
            LoadPage(newOffset);
    }

    private void LastPage_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_currentData == null) return;
        long lastOffset = Math.Max(0, _currentData.FileSize - HexViewer.DefaultPageSize);
        lastOffset = (lastOffset / HexViewer.DefaultPageSize) * HexViewer.DefaultPageSize;
        LoadPage(lastOffset);
    }

    private void GoToOffset_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string hex = OffsetTextBox.Text?.Trim() ?? "";
        if (long.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out long offset))
        {
            if (_currentData != null && offset >= _currentData.FileSize)
            {
                SearchResultText.Text = $"Offset 0x{offset:X8} is beyond end of file (0x{_currentData.FileSize:X8}).";
                return;
            }
            LoadPage(offset);
        }
    }

    private async void Search_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_currentFile == null) return;
        string hex = SearchTextBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(hex))
        {
            SearchResultText.Text = "Enter hex bytes to search.";
            return;
        }

        try
        {
            byte[] pattern = HexViewer.ParseHexString(hex);
            long startOffset = _currentData?.Offset ?? 0;

            SearchButton.IsEnabled = false;
            SearchResultText.Text = "Searching...";

            var results = await Task.Run(() => HexViewer.SearchBytes(_currentFile, pattern, startOffset));

            if (results.Count > 0)
            {
                SearchResultText.Text = $"Found {results.Count} match(es). First at 0x{results[0]:X8}";
                LoadPage(results[0]);
            }
            else
            {
                SearchResultText.Text = "No matches found.";
            }
        }
        catch (FormatException)
        {
            SearchResultText.Text = "Invalid hex string.";
        }
        finally
        {
            SearchButton.IsEnabled = true;
        }
    }
}
