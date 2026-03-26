using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
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
            Title = LocalizationManager.Instance["Hex_SelectFile"],
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

            var loc = LocalizationManager.Instance;
            FileSizeText.Text = string.Format(loc["Hex_SizeFormat"],
                FileUtils.FormatFileSize(_currentData.FileSize), _currentData.FileSize);
            PageInfoText.Text = string.Format(loc["Hex_PageInfoFormat"],
                _currentData.Offset.ToString("X8"), _currentData.CurrentPage + 1, _currentData.TotalPages);
            OffsetTextBox.Text = _currentData.Offset.ToString("X8");

            InfoPanel.IsVisible = true;
            NavPanel.IsVisible = true;
            SearchPanel.IsVisible = true;

            PrevButton.IsEnabled = _currentData.Offset > 0;
            FirstButton.IsEnabled = _currentData.Offset > 0;
            NextButton.IsEnabled = _currentData.Offset + HexViewer.DefaultPageSize < _currentData.FileSize;
            LastButton.IsEnabled = _currentData.Offset + HexViewer.DefaultPageSize < _currentData.FileSize;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            HexDisplay.ItemsSource = new[] { string.Format(LocalizationManager.Instance["Hex_ErrorFormat"], ex.Message) };
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
                SearchResultText.Text = string.Format(LocalizationManager.Instance["Hex_OffsetBeyondEnd"],
                    offset.ToString("X8"), _currentData.FileSize.ToString("X8"));
                return;
            }
            LoadPage(offset);
        }
        else if (!string.IsNullOrEmpty(hex))
        {
            SearchResultText.Text = LocalizationManager.Instance["Hex_InvalidOffset"];
        }
    }

    private async void Search_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_currentFile == null) return;
        string hex = SearchTextBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(hex))
        {
            SearchResultText.Text = LocalizationManager.Instance["Hex_EnterSearchBytes"];
            return;
        }

        try
        {
            byte[] pattern = HexViewer.ParseHexString(hex);
            long startOffset = _currentData?.Offset ?? 0;

            SearchButton.IsEnabled = false;
            SearchResultText.Text = LocalizationManager.Instance["Hex_Searching"];

            var results = await Task.Run(() => HexViewer.SearchBytes(_currentFile, pattern, startOffset));

            if (results.Count > 0)
            {
                SearchResultText.Text = string.Format(LocalizationManager.Instance["Hex_FoundMatches"],
                    results.Count, results[0].ToString("X8"));
                LoadPage(results[0]);
            }
            else if (startOffset > 0)
            {
                // Wrap around: search from the beginning up to the current offset
                SearchResultText.Text = LocalizationManager.Instance["Hex_SearchingFromStart"];
                var wrapResults = await Task.Run(() => HexViewer.SearchBytes(_currentFile, pattern, 0));

                if (wrapResults.Count > 0)
                {
                    SearchResultText.Text = string.Format(LocalizationManager.Instance["Hex_FoundMatchesWrapped"],
                        wrapResults.Count, wrapResults[0].ToString("X8"));
                    LoadPage(wrapResults[0]);
                }
                else
                {
                    SearchResultText.Text = LocalizationManager.Instance["Hex_NoMatches"];
                }
            }
            else
            {
                SearchResultText.Text = LocalizationManager.Instance["Hex_NoMatches"];
            }
        }
        catch (FormatException)
        {
            SearchResultText.Text = LocalizationManager.Instance["Hex_InvalidHex"];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SearchResultText.Text = string.Format(LocalizationManager.Instance["Hex_ErrorFormat"], ex.Message);
        }
        finally
        {
            SearchButton.IsEnabled = true;
        }
    }
}
