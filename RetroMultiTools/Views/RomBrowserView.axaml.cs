using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Models;
using RetroMultiTools.Services;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class RomBrowserView : UserControl
{
    private List<RomInfo> _allRoms = new();
    private string _currentFolder = string.Empty;
    private CancellationTokenSource? _artworkCts;

    public RomBrowserView()
    {
        InitializeComponent();
        PopulateSystemFilter();
        DetachedFromVisualTree += (_, _) =>
        {
            CancelArtworkLoading();
            ClearArtworkImages();
        };
    }

    private void PopulateSystemFilter()
    {
        SystemFilterCombo.Items.Clear();
        SystemFilterCombo.Items.Add(new ComboBoxItem { Content = LocalizationManager.Instance["BigPicture_AllSystems"], Tag = "all" });
        foreach (RomSystem sys in Enum.GetValues<RomSystem>())
        {
            if (sys == RomSystem.Unknown) continue;
            SystemFilterCombo.Items.Add(new ComboBoxItem
            {
                Content = RomOrganizer.GetSystemDisplayName(sys),
                Tag = sys
            });
        }
        SystemFilterCombo.SelectedIndex = 0;
    }

    private async void BrowseFolderButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select ROM Folder",
            AllowMultiple = false
        });

        if (folders.Count == 0) return;

        _currentFolder = folders[0].Path.LocalPath;
        FolderPathText.Text = _currentFolder;
        await ScanCurrentFolder();
    }

    private async Task ScanCurrentFolder()
    {
        StatusText.Text = LocalizationManager.Instance["BigPicture_Scanning"];
        ScanProgressBar.IsVisible = true;
        BrowseFolderButton.IsEnabled = false;
        ClearArtwork();

        try
        {
            var progress = new Progress<string>(msg => StatusText.Text = msg);
            _allRoms = await Task.Run(() =>
            {
                var roms = RomOrganizer.ScanDirectory(_currentFolder, progress);
                foreach (var rom in roms)
                {
                    var gtResult = GoodToolsIdentifier.Identify(rom.FileName);
                    rom.GoodToolsCodes = gtResult.GetSummary();
                    if (gtResult.HasCodes)
                        rom.GoodToolsCodesDescription = gtResult.GetDetailedDescription();
                }
                return roms;
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText.Text = $"Scan error: {ex.Message}";
            _allRoms = new();
        }
        finally
        {
            ScanProgressBar.IsVisible = false;
            BrowseFolderButton.IsEnabled = true;
        }

        ApplyFilter();
        UpdateButtonStates();
        if (_allRoms.Count > 0)
            StatusText.Text = $"Found {_allRoms.Count} ROM(s).";
    }

    private void SystemFilterCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void SearchTextBox_TextChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        IEnumerable<RomInfo> filtered = _allRoms;

        if (SystemFilterCombo.SelectedItem is ComboBoxItem item && item.Tag is RomSystem selectedSystem)
        {
            filtered = filtered.Where(r => r.System == selectedSystem);
        }

        string? searchText = SearchTextBox?.Text;
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            string search = searchText.Trim();
            filtered = filtered.Where(r =>
                r.FileName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                r.SystemName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                r.GoodToolsCodes.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                r.FileSizeFormatted.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var result = filtered.ToList();
        RomDataGrid.ItemsSource = result;

        if (_allRoms.Count > 0 && result.Count != _allRoms.Count)
            StatusText.Text = $"Showing {result.Count} of {_allRoms.Count} ROM(s).";
        else if (_allRoms.Count > 0)
            StatusText.Text = $"Found {_allRoms.Count} ROM(s).";
    }

    private void RomDataGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateButtonStates();
        UpdateContextMenuForSelection();

        if (ShowArtworkCheck.IsChecked == true && RomDataGrid.SelectedItem is RomInfo selectedRom)
        {
            _ = LoadArtworkAsync(selectedRom);
        }
    }

    private void UpdateContextMenuForSelection()
    {
        if (RomDataGrid.SelectedItem is not RomInfo selectedRom) return;

        // Show system-specific context menu items
        bool canConvert = selectedRom.System is RomSystem.N64 or RomSystem.SNES or RomSystem.NES;
        bool canTrim = selectedRom.System is RomSystem.NES or RomSystem.SNES or RomSystem.N64
                    or RomSystem.GameBoy or RomSystem.GameBoyColor or RomSystem.GameBoyAdvance;
        bool canExportHeader = selectedRom.System is RomSystem.NES or RomSystem.SNES or RomSystem.N64
                    or RomSystem.GameBoy or RomSystem.GameBoyColor or RomSystem.GameBoyAdvance
                    or RomSystem.MegaDrive or RomSystem.SegaMasterSystem;

        ContextConvertItem.IsVisible = canConvert;
        ContextTrimItem.IsVisible = canTrim;
        ContextExportHeaderItem.IsVisible = canExportHeader;
    }

    private void UpdateButtonStates()
    {
        bool hasFolderSelected = !string.IsNullOrEmpty(_currentFolder);
        bool hasRoms = _allRoms.Count > 0;
        bool hasSelection = RomDataGrid.SelectedItems.Count > 0;

        AddRomButton.IsEnabled = hasFolderSelected;
        CopyRomButton.IsEnabled = hasSelection;
        MoveRomButton.IsEnabled = hasSelection;
        DeleteRomButton.IsEnabled = hasSelection;
        SendToRemoteButton.IsEnabled = hasSelection;
        HostShareButton.IsEnabled = hasFolderSelected || hasSelection;
        LaunchRetroArchButton.IsEnabled = hasSelection;
        OrganizeButton.IsEnabled = hasRoms;
    }

    private void ShowArtworkCheck_Changed(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        bool show = ShowArtworkCheck.IsChecked == true;
        ArtworkPanel.IsVisible = show;
        ContentGrid.ColumnDefinitions[1].Width = show ? new GridLength(280) : new GridLength(0);

        if (show && RomDataGrid.SelectedItem is RomInfo selectedRom)
        {
            _ = LoadArtworkAsync(selectedRom);
        }
        else if (!show)
        {
            CancelArtworkLoading();
            ClearArtwork();
        }
    }

    private async Task LoadArtworkAsync(RomInfo romInfo)
    {
        CancelArtworkLoading();

        _artworkCts = new CancellationTokenSource();
        var token = _artworkCts.Token;

        ArtworkRomName.Text = romInfo.FileName;
        ArtworkStatusText.Text = LocalizationManager.Instance["BigPicture_ArtworkLoading"];
        ClearArtworkImages();

        try
        {
            var progress = new Progress<string>(msg =>
            {
                if (!token.IsCancellationRequested)
                    ArtworkStatusText.Text = msg;
            });

            var artwork = await ArtworkService.FetchArtworkAsync(romInfo, progress, token);

            if (token.IsCancellationRequested) return;

            if (artwork.BoxArt != null)
                BoxArtImage.Source = LoadBitmapFromBytes(artwork.BoxArt);

            if (artwork.Snap != null)
                SnapImage.Source = LoadBitmapFromBytes(artwork.Snap);

            if (artwork.TitleScreen != null)
                TitleScreenImage.Source = LoadBitmapFromBytes(artwork.TitleScreen);

            ArtworkStatusText.Text = artwork.HasAnyArtwork
                ? "Artwork loaded."
                : "No artwork found for this ROM.";
        }
        catch (TaskCanceledException)
        {
            // User navigated away — ignore
        }
        catch (HttpRequestException ex)
        {
            if (!token.IsCancellationRequested)
                ArtworkStatusText.Text = $"Network error: {ex.Message}";
        }
        catch (Exception)
        {
            if (!token.IsCancellationRequested)
                ArtworkStatusText.Text = LocalizationManager.Instance["Browser_ArtworkFailed"];
        }
    }

    private static Bitmap? LoadBitmapFromBytes(byte[] data)
    {
        if (data.Length == 0)
            return null;

        try
        {
            using var stream = new MemoryStream(data);
            return new Bitmap(stream);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            return null;
        }
    }

    private void CancelArtworkLoading()
    {
        _artworkCts?.Cancel();
        _artworkCts?.Dispose();
        _artworkCts = null;
    }

    private void ClearArtwork()
    {
        ArtworkRomName.Text = LocalizationManager.Instance["Browser_SelectRomForArtwork"];
        ArtworkStatusText.Text = string.Empty;
        ClearArtworkImages();
    }

    private void ClearArtworkImages()
    {
        (BoxArtImage.Source as Bitmap)?.Dispose();
        (SnapImage.Source as Bitmap)?.Dispose();
        (TitleScreenImage.Source as Bitmap)?.Dispose();
        BoxArtImage.Source = null;
        SnapImage.Source = null;
        TitleScreenImage.Source = null;
    }

    private async void AddRomButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentFolder)) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select ROM Files to Add",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("ROM Files")
                {
                    Patterns = [ "*.nes","*.smc","*.sfc","*.z64","*.n64","*.v64",
                                       "*.gb","*.gbc","*.gba","*.vb","*.vboy",
                                       "*.sms","*.md","*.gen",
                                       "*.bin","*.32x","*.gg","*.a26","*.a52","*.a78",
                                       "*.j64","*.jag","*.lnx","*.lyx",
                                       "*.pce","*.tg16","*.iso","*.cue","*.3do",
                                       "*.chd","*.rvz","*.gcm",
                                       "*.ngp","*.ngc",
                                       "*.col","*.cv","*.int",
                                       "*.mx1","*.mx2",
                                       "*.dsk","*.cdt","*.sna",
                                       "*.tap",
                                       "*.mo5","*.k7","*.fd",
                                       "*.sv","*.ccc" ]
                },
                FilePickerFileTypes.All
            ]
        });

        if (files.Count == 0) return;

        int added = 0;
        int failed = 0;

        foreach (var file in files)
        {
            try
            {
                string sourcePath = file.Path.LocalPath;
                string destPath = Path.Combine(_currentFolder, Path.GetFileName(sourcePath));

                if (!string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destPath), StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(sourcePath, destPath, overwrite: false);
                    added++;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failed++;
            }
        }

        StatusText.Text = $"Added {added} ROM(s)" + (failed > 0 ? $", {failed} failed." : ".");
        await ScanCurrentFolder();
    }

    private async void CopyRomButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var selectedRoms = GetSelectedRoms();
        if (selectedRoms.Count == 0) return;

        var destFolder = await PickFolder("Select Destination Folder for Copy");
        if (destFolder == null) return;

        int copied = 0;
        int failed = 0;

        foreach (var rom in selectedRoms)
        {
            try
            {
                RomOrganizer.CopyRom(rom.FilePath, destFolder);
                copied++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failed++;
            }
        }

        StatusText.Text = $"Copied {copied} ROM(s) to {destFolder}" + (failed > 0 ? $", {failed} failed." : ".");
    }

    private async void MoveRomButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var selectedRoms = GetSelectedRoms();
        if (selectedRoms.Count == 0) return;

        var destFolder = await PickFolder("Select Destination Folder for Move");
        if (destFolder == null) return;

        int moved = 0;
        int failed = 0;

        foreach (var rom in selectedRoms)
        {
            try
            {
                RomOrganizer.MoveRom(rom.FilePath, destFolder);
                moved++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failed++;
            }
        }

        StatusText.Text = $"Moved {moved} ROM(s) to {destFolder}" + (failed > 0 ? $", {failed} failed." : ".");

        if (moved > 0)
            await ScanCurrentFolder();
    }

    private async void DeleteRomButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var selectedRoms = GetSelectedRoms();
        if (selectedRoms.Count == 0) return;

        int deleted = 0;
        int failed = 0;

        foreach (var rom in selectedRoms)
        {
            try
            {
                RomOrganizer.DeleteRom(rom.FilePath);
                deleted++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failed++;
            }
        }

        StatusText.Text = $"Deleted {deleted} ROM(s)" + (failed > 0 ? $", {failed} failed." : ".");

        if (deleted > 0)
            await ScanCurrentFolder();
    }

    private void LaunchRetroArchButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (RomDataGrid.SelectedItem is not RomInfo selectedRom) return;

        if (!RetroArchLauncher.IsSystemSupported(selectedRom.System))
        {
            StatusText.Text = $"System '{selectedRom.SystemName}' is not supported for RetroArch launch.";
            return;
        }

        StatusText.Text = $"Launching {selectedRom.FileName} with {RetroArchLauncher.GetCoreDisplayName(selectedRom.System)}...";
        var result = RetroArchLauncher.Launch(selectedRom.FilePath, selectedRom.System);
        StatusText.Text = result.Message;

        if (result.Success && result.Process != null)
        {
            if (AppSettings.Instance.MinimizeToTrayOnLaunch)
            {
                MinimizeToTrayAndRestoreOnExit(result.Process);
            }
            else
            {
                result.Process.Dispose();
            }
        }
    }

    private async void MinimizeToTrayAndRestoreOnExit(System.Diagnostics.Process process)
    {
        try
        {
            if (TopLevel.GetTopLevel(this) is MainWindow mainWindow)
            {
                mainWindow.MinimizeToTray();

                await System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        process.WaitForExit();
                    }
                    catch (InvalidOperationException ex)
                    {
                        System.Diagnostics.Trace.WriteLine(
                            $"[RomBrowser] Process monitoring ended: {ex.Message}");
                    }
                });

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    mainWindow.RestoreFromTray();
                    DiscordRichPresence.ClearPresence();
                });
            }
        }
        finally
        {
            process.Dispose();
        }
    }

    private async void OrganizeButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_allRoms.Count == 0) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Output Folder for Organized ROMs"
        });

        if (folders.Count == 0) return;

        string outputDir = folders[0].Path.LocalPath;
        StatusText.Text = LocalizationManager.Instance["Browser_Organizing"];
        OrganizeButton.IsEnabled = false;

        try
        {
            var result = await Task.Run(() => RomOrganizer.OrganizeBySystem(_allRoms, outputDir));
            StatusText.Text = $"Organized into {outputDir}: {result.Summary}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText.Text = $"Organize error: {ex.Message}";
        }
        finally
        {
            OrganizeButton.IsEnabled = true;
        }
    }

    private async void SendToRemoteButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var selectedRoms = GetSelectedRoms();
        if (selectedRoms.Count == 0) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not Window parentWindow) return;

        var dialog = new SendToRemoteWindow(selectedRoms);
        await dialog.ShowDialog(parentWindow);
    }

    private async void HostShareButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not Window parentWindow) return;

        var selectedRoms = GetSelectedRoms();
        HostRomsWindow dialog;

        if (selectedRoms.Count > 0)
        {
            dialog = new HostRomsWindow(selectedRoms);
        }
        else if (!string.IsNullOrEmpty(_currentFolder))
        {
            dialog = new HostRomsWindow(_currentFolder);
        }
        else
        {
            return;
        }

        await dialog.ShowDialog(parentWindow);
    }

    private async void HostShareSelectedButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var selectedRoms = GetSelectedRoms();
        if (selectedRoms.Count == 0) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not Window parentWindow) return;

        var dialog = new HostRomsWindow(selectedRoms);
        await dialog.ShowDialog(parentWindow);
    }

    private void ContextLaunch_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        LaunchRetroArchButton_Click(sender, e);
    }

    private void ContextConvert_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (RomDataGrid.SelectedItem is not RomInfo selectedRom) return;

        StatusText.Text = $"To convert {selectedRom.FileName}, use the ROM Format Converter tool.";
    }

    private void ContextTrim_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (RomDataGrid.SelectedItem is not RomInfo selectedRom) return;

        StatusText.Text = $"To trim {selectedRom.FileName}, use the ROM Trimmer tool.";
    }

    private async void ContextExportHeader_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (RomDataGrid.SelectedItem is not RomInfo selectedRom) return;

        var destFolder = await PickFolder("Select Output Folder for Header Export");
        if (destFolder == null) return;

        try
        {
            string outputPath = Path.Combine(destFolder, Path.GetFileNameWithoutExtension(selectedRom.FileName) + "_header.txt");
            await RomHeaderExporter.ExportSingleAsync(selectedRom.FilePath, outputPath);
            StatusText.Text = $"Header exported to {outputPath}";
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            StatusText.Text = $"Export error: {ex.Message}";
        }
    }

    private void ContextVerify_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (RomDataGrid.SelectedItem is not RomInfo selectedRom) return;

        StatusText.Text = $"To verify {selectedRom.FileName}, use the DAT Verifier or Dump Verifier tool.";
    }

    private List<RomInfo> GetSelectedRoms()
    {
        var selected = new List<RomInfo>();
        foreach (var item in RomDataGrid.SelectedItems)
        {
            if (item is RomInfo rom)
                selected.Add(rom);
        }
        return selected;
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

    private void BigPictureModeButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is MainWindow mainWindow)
        {
            mainWindow.EnterBigPictureMode(_currentFolder, _allRoms);
        }
    }
}
