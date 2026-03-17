using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class DatFilterView : UserControl
{
    private List<DatEntry>? _datEntries;
    private List<DatEntry>? _filteredEntries;

    public DatFilterView()
    {
        InitializeComponent();
    }

    private async void BrowseDat_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFile("Select DAT File",
        [
            new FilePickerFileType("DAT Files") { Patterns = ["*.dat", "*.xml"] },
            FilePickerFileTypes.All
        ]);
        if (path == null) return;

        DatFileTextBox.Text = path;

        try
        {
            _datEntries = DatVerifier.LoadDatFile(path);
            DatInfoText.Text = $"Loaded {_datEntries.Count} ROM entries from DAT file.";
            DatInfoPanel.IsVisible = true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            _datEntries = null;
            DatInfoText.Text = $"Error loading DAT file: {ex.Message}";
            DatInfoPanel.IsVisible = true;
        }

        UpdateFilterButton();
    }

    private void UpdateFilterButton()
    {
        FilterButton.IsEnabled = !string.IsNullOrEmpty(DatFileTextBox.Text) && _datEntries != null;
    }

    private async void FilterButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_datEntries == null)
        {
            ShowStatus("Please load a DAT file first.", isError: true);
            return;
        }

        FilterButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        StatusBorder.IsVisible = false;
        ResultsBorder.IsVisible = false;
        ExportButton.IsVisible = false;

        try
        {
            var options = BuildOptions();
            var progress = new Progress<string>(msg => ProgressText.Text = msg);

            var (filtered, stats) = await DatFilter.FilterAsync(_datEntries, options, progress);
            _filteredEntries = filtered;

            ShowStatus(
                $"✔ Filtering complete!\n\n" +
                $"Original entries: {stats.OriginalCount}\n" +
                $"Excluded by category: {stats.ExcludedByCategory}\n" +
                $"Excluded by 1G1R: {stats.ExcludedBy1G1R}\n" +
                $"Remaining entries: {stats.FilteredCount}",
                isError: false);

            var lines = new System.Text.StringBuilder();
            int shown = Math.Min(filtered.Count, 200);
            for (int i = 0; i < shown; i++)
                lines.AppendLine(filtered[i].GameName);

            if (filtered.Count > shown)
                lines.AppendLine($"... and {filtered.Count - shown} more entries");

            ResultsText.Text = lines.ToString();
            ResultsBorder.IsVisible = true;
            ExportButton.IsVisible = filtered.Count > 0;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            ShowStatus($"✘ Error: {ex.Message}", isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            FilterButton.IsEnabled = true;
        }
    }

    private async void ExportButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_filteredEntries == null || _filteredEntries.Count == 0) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Filtered DAT",
            SuggestedFileName = "filtered.dat",
            FileTypeChoices =
            [
                new FilePickerFileType("DAT Files") { Patterns = ["*.dat"] },
                new FilePickerFileType("XML Files") { Patterns = ["*.xml"] }
            ]
        });

        if (file == null) return;

        string outputPath = file.Path.LocalPath;

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);
            string datName = Path.GetFileNameWithoutExtension(DatFileTextBox.Text ?? "filtered") + " (Filtered)";
            await DatFilter.ExportFilteredDat(_filteredEntries, outputPath, datName,
                $"Filtered by RetroMultiTools — {_filteredEntries.Count} entries", progress);
            ShowStatus($"✔ Exported {_filteredEntries.Count} entries to:\n{outputPath}", isError: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowStatus($"✘ Export error: {ex.Message}", isError: true);
        }
    }

    private DatFilterOptions BuildOptions()
    {
        var options = new DatFilterOptions
        {
            ExcludeDemos = ExcludeDemos.IsChecked == true,
            ExcludeBetas = ExcludeBetas.IsChecked == true,
            ExcludePrototypes = ExcludePrototypes.IsChecked == true,
            ExcludeSamples = ExcludeSamples.IsChecked == true,
            ExcludeUnlicensed = ExcludeUnlicensed.IsChecked == true,
            ExcludeBIOS = ExcludeBIOS.IsChecked == true,
            ExcludeApplications = ExcludeApplications.IsChecked == true,
            ExcludePirateEditions = ExcludePirate.IsChecked == true,
            Enable1G1R = Enable1G1R.IsChecked == true,
            PreferRevisions = PreferRevisions.IsChecked == true
        };

        string regionText = RegionTextBox.Text ?? "";
        if (!string.IsNullOrWhiteSpace(regionText))
        {
            options.RegionPriority = regionText
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        string langText = LanguageTextBox.Text ?? "";
        if (!string.IsNullOrWhiteSpace(langText))
        {
            options.LanguagePriority = langText
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        return options;
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
