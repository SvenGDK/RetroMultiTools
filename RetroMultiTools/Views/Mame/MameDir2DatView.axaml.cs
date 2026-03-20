using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using RetroMultiTools.Utilities;
using RetroMultiTools.Utilities.Mame;

namespace RetroMultiTools.Views.Mame;

public partial class MameDir2DatView : UserControl
{
    private static readonly IBrush StatusErrorBrush = new SolidColorBrush(Color.Parse("#F38BA8"));
    private static readonly IBrush StatusSuccessBrush = new SolidColorBrush(Color.Parse("#A6E3A1"));

    private Dir2DatResult? _scanResult;

    public MameDir2DatView()
    {
        InitializeComponent();
    }

    private async void BrowseRomDir_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFolder("Select ROM Directory");
        if (path != null) RomDirTextBox.Text = path;
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        ScanButton.IsEnabled = !string.IsNullOrEmpty(RomDirTextBox.Text);
        ExportButton.IsEnabled = _scanResult != null && _scanResult.TotalGames > 0;
    }

    private async void ScanButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string romDir = RomDirTextBox.Text ?? "";
        if (string.IsNullOrEmpty(romDir))
        {
            ShowStatus("Please select a ROM directory.", isError: true);
            return;
        }

        ScanButton.IsEnabled = false;
        ExportButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        StatusBorder.IsVisible = false;
        ResultsBorder.IsVisible = false;
        _scanResult = null;

        try
        {
            var options = BuildOptions();
            var progress = new Progress<string>(msg => ProgressText.Text = msg);

            _scanResult = await MameDir2Dat.CreateDatAsync(romDir, options, progress);

            ShowStatus($"✔ Scan complete!\n{_scanResult.Summary}", isError: false);

            var lines = new System.Text.StringBuilder();
            int shown = 0;
            foreach (var game in _scanResult.Games.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (shown >= 200) break;
                lines.AppendLine($"📦 {game.Name} — {game.Roms.Count} ROM(s), {game.Disks.Count} disk(s)");
                foreach (var rom in game.Roms.Take(5))
                    lines.AppendLine($"   {rom.Name} ({rom.Size:N0} bytes, CRC: {rom.CRC32})");
                if (game.Roms.Count > 5)
                    lines.AppendLine($"   ... and {game.Roms.Count - 5} more ROMs");
                foreach (var disk in game.Disks)
                    lines.AppendLine($"   💿 {disk.Name}");
                lines.AppendLine();
                shown++;
            }

            if (_scanResult.Games.Count > 200)
                lines.AppendLine($"... and {_scanResult.Games.Count - 200} more games");

            ResultsText.Text = lines.ToString();
            ResultsBorder.IsVisible = _scanResult.Games.Count > 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ShowStatus($"✘ Error: {ex.Message}", isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            UpdateButtons();
        }
    }

    private async void ExportButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_scanResult == null || _scanResult.TotalGames == 0)
        {
            ShowStatus("No scan results to export. Run a scan first.", isError: true);
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        string defaultName = !string.IsNullOrEmpty(DatNameTextBox.Text)
            ? DatNameTextBox.Text + ".dat"
            : "dir2dat.dat";

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export DAT File",
            SuggestedFileName = defaultName,
            FileTypeChoices =
            [
                new FilePickerFileType("DAT Files") { Patterns = ["*.dat", "*.xml"] }
            ]
        });

        if (file == null) return;

        try
        {
            var options = BuildOptions();
            MameDir2Dat.ExportDat(_scanResult, file.Path.LocalPath, options);
            ShowStatus($"✔ DAT exported to: {file.Path.LocalPath}", isError: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowStatus($"✘ Error exporting DAT: {ex.Message}", isError: true);
        }
    }

    private Dir2DatOptions BuildOptions() => new()
    {
        DatName = DatNameTextBox.Text ?? "Dir2Dat",
        DatDescription = DatDescriptionTextBox.Text ?? "",
        DatAuthor = DatAuthorTextBox.Text ?? "",
        ComputeSHA1 = ComputeSha1CheckBox.IsChecked == true,
        ComputeMD5 = ComputeMd5CheckBox.IsChecked == true,
        IncludeLooseFiles = IncludeLooseCheckBox.IsChecked == true,
        IncludeChd = IncludeChdCheckBox.IsChecked == true
    };

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
