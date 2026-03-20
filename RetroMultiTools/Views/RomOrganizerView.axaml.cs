using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using RetroMultiTools.Detection;
using RetroMultiTools.Models;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class RomOrganizerView : UserControl
{
    private static readonly IBrush StatusErrorBrush = new SolidColorBrush(Color.Parse("#F38BA8"));
    private static readonly IBrush StatusSuccessBrush = new SolidColorBrush(Color.Parse("#A6E3A1"));
    private static readonly IBrush StatusWarningBrush = new SolidColorBrush(Color.Parse("#F9E2AF"));

    private List<RomInfo>? _scannedRoms;

    public RomOrganizerView()
    {
        InitializeComponent();
        PopulateSystemFilter();
    }

    private void PopulateSystemFilter()
    {
        var items = new List<string> { "All Systems" };
        foreach (RomSystem system in Enum.GetValues<RomSystem>())
        {
            if (system == RomSystem.Unknown) continue;
            items.Add(RomDetector.GetSystemDisplayName(system));
        }
        SystemFilterComboBox.ItemsSource = items;
        SystemFilterComboBox.SelectedIndex = 0;
    }

    private async void BrowseSource_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select ROM Source Folder",
            AllowMultiple = false
        });

        if (folders.Count == 0) return;
        SourcePathTextBox.Text = folders[0].Path.LocalPath;
        UpdateScanButtonState();
        ResetResults();
    }

    private async void BrowseOutput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Output Folder",
            AllowMultiple = false
        });

        if (folders.Count == 0) return;
        OutputPathTextBox.Text = folders[0].Path.LocalPath;
        UpdateScanButtonState();
    }

    private async void ScanButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string sourcePath = SourcePathTextBox.Text ?? "";
        if (string.IsNullOrWhiteSpace(sourcePath)) return;

        ScanButton.IsEnabled = false;
        OrganizeButton.IsVisible = false;
        ProgressPanel.IsVisible = true;
        StatusBorder.IsVisible = false;
        RomList.ItemsSource = null;

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);
            _scannedRoms = await Task.Run(() => RomOrganizer.ScanDirectory(sourcePath, progress));

            var displayRoms = GetFilteredRoms();
            RomList.ItemsSource = displayRoms;

            StatusText.Text = displayRoms.Count > 0
                ? $"Found {displayRoms.Count} ROM(s) across {displayRoms.Select(r => r.SystemName).Distinct().Count()} system(s)."
                : "No ROMs found in the selected folder.";
            StatusText.Foreground = displayRoms.Count > 0 ? StatusSuccessBrush : StatusWarningBrush;
            StatusBorder.IsVisible = true;
            OrganizeButton.IsVisible = displayRoms.Count > 0 && !string.IsNullOrWhiteSpace(OutputPathTextBox.Text);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText.Text = $"✘ Error: {ex.Message}";
            StatusText.Foreground = StatusErrorBrush;
            StatusBorder.IsVisible = true;
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            ScanButton.IsEnabled = true;
        }
    }

    private async void OrganizeButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_scannedRoms == null || _scannedRoms.Count == 0) return;

        string outputPath = OutputPathTextBox.Text ?? "";
        if (string.IsNullOrWhiteSpace(outputPath)) return;

        OrganizeButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        StatusBorder.IsVisible = false;

        try
        {
            bool moveFiles = MoveMode.IsChecked == true;
            RomSystem? systemFilter = GetSelectedSystemFilter();
            var progress = new Progress<string>(msg => ProgressText.Text = msg);

            var result = await Task.Run(() =>
                RomOrganizer.OrganizeBySystem(_scannedRoms, outputPath, moveFiles, systemFilter, progress));

            StatusText.Text = $"✔ {result.Summary}";
            StatusText.Foreground = result.Failed > 0 ? StatusWarningBrush : StatusSuccessBrush;
            StatusBorder.IsVisible = true;

            if (moveFiles && result.Processed > 0)
            {
                _scannedRoms = null;
                RomList.ItemsSource = null;
                OrganizeButton.IsVisible = false;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText.Text = $"✘ Error: {ex.Message}";
            StatusText.Foreground = StatusErrorBrush;
            StatusBorder.IsVisible = true;
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            OrganizeButton.IsEnabled = true;
        }
    }

    private void UpdateScanButtonState()
    {
        ScanButton.IsEnabled = !string.IsNullOrWhiteSpace(SourcePathTextBox.Text);
    }

    private void ResetResults()
    {
        _scannedRoms = null;
        OrganizeButton.IsVisible = false;
        StatusBorder.IsVisible = false;
        RomList.ItemsSource = null;
    }

    private List<RomInfo> GetFilteredRoms()
    {
        if (_scannedRoms == null) return [];
        var systemFilter = GetSelectedSystemFilter();
        return systemFilter.HasValue
            ? _scannedRoms.Where(r => r.System == systemFilter.Value).ToList()
            : _scannedRoms;
    }

    private RomSystem? GetSelectedSystemFilter()
    {
        if (SystemFilterComboBox.SelectedIndex <= 0) return null;

        var systems = Enum.GetValues<RomSystem>().Where(s => s != RomSystem.Unknown).ToArray();
        int index = SystemFilterComboBox.SelectedIndex - 1;
        return index >= 0 && index < systems.Length ? systems[index] : null;
    }
}
