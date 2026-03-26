using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class DatFilterView : UserControl
{
    private static readonly IBrush StatusErrorBrush = new SolidColorBrush(Color.Parse("#F38BA8"));
    private static readonly IBrush StatusSuccessBrush = new SolidColorBrush(Color.Parse("#A6E3A1"));

    private List<DatEntry>? _datEntries;
    private List<DatEntry>? _filteredEntries;

    public DatFilterView()
    {
        InitializeComponent();
    }

    private async void BrowseDat_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        var path = await PickFile(loc["DatFilter_BrowseDatTitle"],
        [
            new FilePickerFileType("DAT Files") { Patterns = ["*.dat", "*.xml"] },
            FilePickerFileTypes.All
        ]);
        if (path == null) return;

        DatFileTextBox.Text = path;

        try
        {
            _datEntries = DatVerifier.LoadDatFile(path);
            DatInfoText.Text = string.Format(loc["DatFilter_LoadedEntries"], _datEntries.Count);
            DatInfoPanel.IsVisible = true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            _datEntries = null;
            DatInfoText.Text = string.Format(loc["DatFilter_LoadError"], ex.Message);
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
        var loc = LocalizationManager.Instance;
        if (_datEntries == null)
        {
            ShowStatus(loc["DatFilter_LoadDatFirst"], isError: true);
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
                string.Format(loc["DatFilter_FilterComplete"],
                    stats.OriginalCount, stats.ExcludedByCategory, stats.ExcludedBy1G1R, stats.FilteredCount),
                isError: false);

            var lines = new System.Text.StringBuilder();
            int shown = Math.Min(filtered.Count, 200);
            for (int i = 0; i < shown; i++)
                lines.AppendLine(filtered[i].GameName);

            if (filtered.Count > shown)
                lines.AppendLine(string.Format(loc["DatFilter_MoreEntries"], filtered.Count - shown));

            ResultsText.Text = lines.ToString();
            ResultsBorder.IsVisible = true;
            ExportButton.IsVisible = filtered.Count > 0;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            ShowStatus(string.Format(loc["Common_ErrorFormat"], ex.Message), isError: true);
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
        var loc = LocalizationManager.Instance;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = loc["DatFilter_ExportDialogTitle"],
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
            string datName = Path.GetFileNameWithoutExtension(DatFileTextBox.Text ?? "filtered") + loc["DatFilter_FilteredSuffix"];
            await DatFilter.ExportFilteredDat(_filteredEntries, outputPath, datName,
                string.Format(loc["DatFilter_FilteredComment"], _filteredEntries.Count), progress);
            ShowStatus(string.Format(loc["DatFilter_ExportComplete"], _filteredEntries.Count, outputPath), isError: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowStatus(string.Format(loc["DatFilter_ExportError"], ex.Message), isError: true);
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
