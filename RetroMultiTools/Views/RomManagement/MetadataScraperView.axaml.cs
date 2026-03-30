using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Utilities;
using RetroMultiTools.Utilities.RomManagement;

namespace RetroMultiTools.Views.RomManagement;

public partial class MetadataScraperView : UserControl
{
    private static readonly IBrush StatusErrorBrush = new SolidColorBrush(Color.Parse("#F38BA8"));
    private static readonly IBrush StatusSuccessBrush = new SolidColorBrush(Color.Parse("#A6E3A1"));
    public MetadataScraperView()
    {
        InitializeComponent();
        OutputFileTextBox.TextChanged += (_, _) => UpdateScrapeButton();
        DragDropHelper.EnableFileDrop(InputDirTextBox, _ =>
        {
            UpdateOutputPath();
            UpdateScrapeButton();
        }, acceptDirectories: true);
    }

    private async void BrowseInput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFolder(LocalizationManager.Instance["Common_SelectRomDirectoryTitle"]);
        if (path == null) return;

        InputDirTextBox.Text = path;
        UpdateOutputPath();
        UpdateScrapeButton();
    }

    private void FormatRadio_Checked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (OutputFileTextBox == null) return;
        if (sender is not RadioButton rb || rb.IsChecked != true) return;
        UpdateOutputPath(isCsvOverride: sender == CsvFormatRadio);
    }

    private void UpdateOutputPath(bool? isCsvOverride = null)
    {
        if (string.IsNullOrEmpty(InputDirTextBox.Text)) return;

        bool isCsv = isCsvOverride ?? (CsvFormatRadio.IsChecked == true);
        string ext = isCsv ? ".csv" : ".txt";
        string dirName = Path.GetFileName(InputDirTextBox.Text.TrimEnd(Path.DirectorySeparatorChar));
        string parentDir = Path.GetDirectoryName(InputDirTextBox.Text) ?? InputDirTextBox.Text;
        OutputFileTextBox.Text = Path.Combine(parentDir, dirName + "_metadata" + ext);
    }

    private void UpdateScrapeButton()
    {
        ScrapeButton.IsEnabled = !string.IsNullOrEmpty(InputDirTextBox.Text) &&
                                 !string.IsNullOrEmpty(OutputFileTextBox.Text);
    }

    private async void BrowseOutput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = loc["MetaScraper_SaveDialogTitle"],
            SuggestedFileName = Path.GetFileName(OutputFileTextBox.Text ?? "metadata.csv")
        });

        if (file != null)
            OutputFileTextBox.Text = file.Path.LocalPath;
    }

    private async void ScrapeButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        string inputDir = InputDirTextBox.Text ?? "";
        string outputFile = OutputFileTextBox.Text ?? "";

        if (string.IsNullOrEmpty(inputDir))
        {
            ShowStatus(loc["MetaScraper_SelectRomDir"], isError: true);
            return;
        }

        if (string.IsNullOrEmpty(outputFile))
        {
            ShowStatus(loc["MetaScraper_SelectOutputPath"], isError: true);
            return;
        }

        // Ensure correct extension
        bool isCsv = CsvFormatRadio.IsChecked == true;
        string expectedExt = isCsv ? ".csv" : ".txt";
        if (!outputFile.EndsWith(expectedExt, StringComparison.OrdinalIgnoreCase))
            outputFile = Path.ChangeExtension(outputFile, expectedExt);

        ScrapeButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        StatusBorder.IsVisible = false;
        SummaryBorder.IsVisible = false;

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);
            bool includeChecksums = IncludeChecksumsCheck.IsChecked == true;

            var metadata = await MetadataScraper.ScrapeDirectoryAsync(inputDir, includeChecksums, progress);

            if (metadata.Count == 0)
            {
                ShowStatus(loc["MetaScraper_NoRomsFound"], isError: true);
                return;
            }

            ((IProgress<string>)progress).Report("Exporting results...");

            if (isCsv)
                await MetadataScraper.ExportToCsvAsync(metadata, outputFile, progress);
            else
                await MetadataScraper.ExportToTextAsync(metadata, outputFile, progress);

            ShowStatus(string.Format(loc["MetaScraper_ScrapeComplete"], metadata.Count, outputFile), isError: false);

            // Show summary
            var systemGroups = metadata.GroupBy(m => m.System).OrderByDescending(g => g.Count());
            var summaryLines = new System.Text.StringBuilder();
            summaryLines.AppendLine(loc["MetaScraper_SummaryBySystem"]);
            foreach (var group in systemGroups)
                summaryLines.AppendLine($"  {group.Key}: {group.Count()} ROM(s)");

            int valid = metadata.Count(m => m.IsValid);
            int errors = metadata.Count(m => m.Error != null);
            int invalid = metadata.Count(m => !m.IsValid && m.Error == null);
            summaryLines.AppendLine();
            summaryLines.AppendLine(string.Format(loc["MetaScraper_SummaryStats"], valid, invalid, errors));

            SummaryText.Text = summaryLines.ToString();
            SummaryBorder.IsVisible = true;
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException or IOException or UnauthorizedAccessException)
        {
            ShowStatus(string.Format(loc["Common_ErrorFormat"], ex.Message), isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            ScrapeButton.IsEnabled = true;
        }
    }

    private void ShowStatus(string message, bool isError)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError ? StatusErrorBrush : StatusSuccessBrush;
        StatusBorder.IsVisible = true;
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
