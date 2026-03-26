using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Models;
using RetroMultiTools.Services;
using RetroMultiTools.Utilities;
using RetroMultiTools.Utilities.Mame;
using RetroMultiTools.Utilities.Mednafen;
using RetroMultiTools.Utilities.RetroArch;

namespace RetroMultiTools.Views;

public partial class RomBrowserView : UserControl
{
    private List<RomInfo> _allRoms = new();
    private string _currentFolder = string.Empty;
    private CancellationTokenSource? _artworkCts;
    private CancellationTokenSource? _scanCts;
    private List<RomInfo>? _pendingDeleteRoms;

    public RomBrowserView()
    {
        InitializeComponent();
        PopulateSystemFilter();
        DetachedFromVisualTree += (_, _) =>
        {
            _scanCts?.Cancel();
            _scanCts?.Dispose();
            _scanCts = null;
            CancelArtworkLoading();
            ClearArtworkImages();
        };
    }

    private void PopulateSystemFilter()
    {
        SystemFilterCombo.Items.Clear();
        SystemFilterCombo.Items.Add(new ComboBoxItem { Content = LocalizationManager.Instance["Browser_AllSystems"], Tag = "all" });
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
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = LocalizationManager.Instance["Browser_SelectFolderTitle"],
                AllowMultiple = false
            });

            if (folders.Count == 0) return;

            _currentFolder = folders[0].Path.LocalPath;
            FolderPathText.Text = _currentFolder;
            await ScanCurrentFolder();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[RomBrowser] BrowseFolderButton_Click failed: {ex.Message}");
        }
    }

    private async Task ScanCurrentFolder()
    {
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        var token = _scanCts.Token;

        StatusText.Text = LocalizationManager.Instance["Browser_Scanning"];
        ScanProgressBar.IsVisible = true;
        BrowseFolderButton.IsEnabled = false;
        ClearArtwork();

        try
        {
            var progress = new Progress<string>(msg =>
            {
                if (!token.IsCancellationRequested)
                    StatusText.Text = msg;
            });
            _allRoms = await Task.Run(() =>
            {
                var roms = RomOrganizer.ScanDirectory(_currentFolder, progress);
                foreach (var rom in roms)
                {
                    token.ThrowIfCancellationRequested();
                    var gtResult = GoodToolsIdentifier.Identify(rom.FileName);
                    rom.GoodToolsCodes = gtResult.GetSummary();
                    if (gtResult.HasCodes)
                        rom.GoodToolsCodesDescription = gtResult.GetDetailedDescription();
                }
                return roms;
            }, token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText.Text = string.Format(LocalizationManager.Instance["Browser_ScanError"], ex.Message);
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
            StatusText.Text = string.Format(LocalizationManager.Instance["Browser_FoundRoms"], _allRoms.Count);
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
            StatusText.Text = string.Format(LocalizationManager.Instance["Browser_ShowingFiltered"], result.Count, _allRoms.Count);
        else if (_allRoms.Count > 0)
            StatusText.Text = string.Format(LocalizationManager.Instance["Browser_FoundRoms"], _allRoms.Count);
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
        bool canLaunchMame = MameLauncher.IsSystemSupported(selectedRom.System);
        bool canLaunchMednafen = MednafenLauncher.IsSystemSupported(selectedRom.System);

        ContextConvertItem.IsVisible = canConvert;
        ContextTrimItem.IsVisible = canTrim;
        ContextExportHeaderItem.IsVisible = canExportHeader;
        ContextLaunchMameItem.IsVisible = canLaunchMame;
        ContextLaunchMednafenItem.IsVisible = canLaunchMednafen;
    }

    private void UpdateButtonStates()
    {
        bool hasFolderSelected = !string.IsNullOrEmpty(_currentFolder);
        bool hasRoms = _allRoms.Count > 0;
        bool hasSelection = RomDataGrid.SelectedItems.Count > 0;
        bool isArcadeSelected = RomDataGrid.SelectedItem is RomInfo rom && MameLauncher.IsSystemSupported(rom.System);
        bool isMednafenSelected = RomDataGrid.SelectedItem is RomInfo mednafenRom && MednafenLauncher.IsSystemSupported(mednafenRom.System);

        AddRomButton.IsEnabled = hasFolderSelected;
        CopyRomButton.IsEnabled = hasSelection;
        MoveRomButton.IsEnabled = hasSelection;
        DeleteRomButton.IsEnabled = hasSelection;
        SendToRemoteButton.IsEnabled = hasSelection;
        HostShareButton.IsEnabled = hasFolderSelected || hasSelection;
        LaunchRetroArchButton.IsEnabled = hasSelection;
        LaunchMameButton.IsEnabled = isArcadeSelected;
        LaunchMednafenButton.IsEnabled = isMednafenSelected;
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
        ArtworkStatusText.Text = LocalizationManager.Instance["Browser_ArtworkLoading"];
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
                ? LocalizationManager.Instance["Browser_ArtworkLoaded"]
                : LocalizationManager.Instance["Browser_ArtworkNotFound"];
        }
        catch (TaskCanceledException)
        {
            // User navigated away — ignore
        }
        catch (HttpRequestException ex)
        {
            if (!token.IsCancellationRequested)
                ArtworkStatusText.Text = string.Format(LocalizationManager.Instance["Browser_NetworkError"], ex.Message);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[RomBrowser] Artwork load failed: {ex.Message}");
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
        // Detach bitmaps from Image controls before disposing to prevent
        // Avalonia from rendering an already-disposed bitmap.
        var boxArt = BoxArtImage.Source as Bitmap;
        var snap = SnapImage.Source as Bitmap;
        var titleScreen = TitleScreenImage.Source as Bitmap;
        BoxArtImage.Source = null;
        SnapImage.Source = null;
        TitleScreenImage.Source = null;
        boxArt?.Dispose();
        snap?.Dispose();
        titleScreen?.Dispose();
    }

    private async void AddRomButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(_currentFolder)) return;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = LocalizationManager.Instance["Browser_SelectRomFilesTitle"],
                AllowMultiple = true,
                FileTypeFilter =
                [
                    new FilePickerFileType(LocalizationManager.Instance["Browser_RomFilesDescription"])
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
                                           "*.sv","*.ccc",
                                           "*.zip","*.rar","*.7z" ]
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

            StatusText.Text = string.Format(LocalizationManager.Instance["Browser_AddedRoms"], added) + (failed > 0 ? string.Format(LocalizationManager.Instance["Browser_FailedCount"], failed) : "");
            await ScanCurrentFolder();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[RomBrowser] AddRomButton_Click failed: {ex.Message}");
        }
    }

    private async void CopyRomButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var selectedRoms = GetSelectedRoms();
            if (selectedRoms.Count == 0) return;

            var destFolder = await PickFolder(LocalizationManager.Instance["Browser_CopyDestinationTitle"]);
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

            StatusText.Text = string.Format(LocalizationManager.Instance["Browser_CopiedRoms"], copied, destFolder) + (failed > 0 ? string.Format(LocalizationManager.Instance["Browser_FailedCount"], failed) : "");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[RomBrowser] CopyRomButton_Click failed: {ex.Message}");
        }
    }

    private async void MoveRomButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var selectedRoms = GetSelectedRoms();
            if (selectedRoms.Count == 0) return;

            var destFolder = await PickFolder(LocalizationManager.Instance["Browser_MoveDestinationTitle"]);
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

            StatusText.Text = string.Format(LocalizationManager.Instance["Browser_MovedRoms"], moved, destFolder) + (failed > 0 ? string.Format(LocalizationManager.Instance["Browser_FailedCount"], failed) : "");

            if (moved > 0)
                await ScanCurrentFolder();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[RomBrowser] MoveRomButton_Click failed: {ex.Message}");
        }
    }

    private void DeleteRomButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var selectedRoms = GetSelectedRoms();
            if (selectedRoms.Count == 0) return;

            _pendingDeleteRoms = selectedRoms;
            ConfirmDeleteText.Text = string.Format(
                LocalizationManager.Instance["Browser_ConfirmDeletePrompt"], selectedRoms.Count);
            ConfirmDeletePanel.IsVisible = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[RomBrowser] DeleteRomButton_Click failed: {ex.Message}");
        }
    }

    private async void ConfirmDelete_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            ConfirmDeletePanel.IsVisible = false;
            var romsToDelete = _pendingDeleteRoms;
            _pendingDeleteRoms = null;
            if (romsToDelete == null || romsToDelete.Count == 0) return;

            int deleted = 0;
            int failed = 0;

            foreach (var rom in romsToDelete)
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

            StatusText.Text = string.Format(LocalizationManager.Instance["Browser_DeletedRoms"], deleted) + (failed > 0 ? string.Format(LocalizationManager.Instance["Browser_FailedCount"], failed) : "");

            if (deleted > 0)
                await ScanCurrentFolder();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[RomBrowser] ConfirmDelete_Click failed: {ex.Message}");
        }
        finally
        {
            _pendingDeleteRoms = null;
        }
    }

    private void CancelDelete_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            ConfirmDeletePanel.IsVisible = false;
            _pendingDeleteRoms = null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[RomBrowser] CancelDelete_Click failed: {ex.Message}");
        }
    }

    private void LaunchRetroArchButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (RomDataGrid.SelectedItem is not RomInfo selectedRom) return;

        if (!RetroArchLauncher.IsSystemSupported(selectedRom.System))
        {
            StatusText.Text = string.Format(LocalizationManager.Instance["Browser_SystemNotSupported"], selectedRom.SystemName);
            return;
        }

        StatusText.Text = string.Format(LocalizationManager.Instance["Browser_LaunchingRom"], selectedRom.FileName, RetroArchLauncher.GetCoreDisplayName(selectedRom.System));
        var result = RetroArchLauncher.Launch(selectedRom.FilePath, selectedRom.System);
        StatusText.Text = result.Message;

        if (result.Success)
        {
            AppSettings.Instance.RecordRecentlyPlayed(selectedRom.FilePath);
            AppSettings.Instance.IncrementPlayCount(selectedRom.FilePath);

            // Append BIOS notice for Neo Geo AES/MVS after successful launch
            if (selectedRom.System == RomSystem.NeoGeo)
            {
                StatusText.Text = string.Format(LocalizationManager.Instance["Common_NeoGeoBiosNoticeWithSeparator"], result.Message, LocalizationManager.Instance["Common_NeoGeoBiosNotice"]);
            }

            if (result.Process != null)
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
    }

    private void LaunchMameButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (RomDataGrid.SelectedItem is not RomInfo selectedRom) return;

        if (!MameLauncher.IsSystemSupported(selectedRom.System))
        {
            StatusText.Text = string.Format(
                LocalizationManager.Instance["Browser_MameSystemNotSupported"], selectedRom.SystemName);
            return;
        }

        StatusText.Text = string.Format(
            LocalizationManager.Instance["Browser_LaunchingMame"], selectedRom.FileName);
        var result = MameLauncher.Launch(selectedRom.FilePath, selectedRom.System);
        StatusText.Text = result.Message;

        if (result.Success)
        {
            AppSettings.Instance.RecordRecentlyPlayed(selectedRom.FilePath);
            AppSettings.Instance.IncrementPlayCount(selectedRom.FilePath);

            if (result.Process != null)
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
    }

    private void LaunchMednafenButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (RomDataGrid.SelectedItem is not RomInfo selectedRom) return;

        if (!MednafenLauncher.IsSystemSupported(selectedRom.System))
        {
            StatusText.Text = string.Format(
                LocalizationManager.Instance["Browser_MednafenSystemNotSupported"], selectedRom.SystemName);
            return;
        }

        StatusText.Text = string.Format(
            LocalizationManager.Instance["Browser_LaunchingMednafen"], selectedRom.FileName);
        var result = MednafenLauncher.Launch(selectedRom.FilePath, selectedRom.System);
        StatusText.Text = result.Message;

        if (result.Success)
        {
            AppSettings.Instance.RecordRecentlyPlayed(selectedRom.FilePath);
            AppSettings.Instance.IncrementPlayCount(selectedRom.FilePath);

            if (result.Process != null)
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
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[RomBrowser] Minimize-to-tray failed: {ex.Message}");
        }
        finally
        {
            process.Dispose();
        }
    }

    private async void OrganizeButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            if (_allRoms.Count == 0) return;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is not Window parentWindow) return;

            var modeDialog = new OrganizeModeWindow();
            var moveChoice = await modeDialog.ShowDialog<bool?>(parentWindow);
            if (moveChoice is null) return;

            bool moveFiles = moveChoice.Value;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = LocalizationManager.Instance["Browser_SelectOrganizeOutputTitle"]
            });

            if (folders.Count == 0) return;

            string outputDir = folders[0].Path.LocalPath;
            StatusText.Text = LocalizationManager.Instance["Browser_Organizing"];

            // Disable all file-mutation buttons to prevent concurrent operations
            OrganizeButton.IsEnabled = false;
            AddRomButton.IsEnabled = false;
            CopyRomButton.IsEnabled = false;
            MoveRomButton.IsEnabled = false;
            DeleteRomButton.IsEnabled = false;

            try
            {
                var result = await Task.Run(() => RomOrganizer.OrganizeBySystem(_allRoms, outputDir, moveFiles, systemFilter: null));
                StatusText.Text = string.Format(LocalizationManager.Instance["Browser_OrganizeComplete"], outputDir, result.Summary);

                if (moveFiles && result.Processed > 0)
                    await ScanCurrentFolder();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                StatusText.Text = string.Format(LocalizationManager.Instance["Browser_OrganizeError"], ex.Message);
            }
            finally
            {
                UpdateButtonStates();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[RomBrowser] OrganizeButton_Click failed: {ex.Message}");
        }
    }

    private async void SendToRemoteButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var selectedRoms = GetSelectedRoms();
            if (selectedRoms.Count == 0) return;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is not Window parentWindow) return;

            var dialog = new SendToRemoteWindow(selectedRoms);
            await dialog.ShowDialog(parentWindow);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[RomBrowser] SendToRemoteButton_Click failed: {ex.Message}");
        }
    }

    private async void HostShareButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
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
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[RomBrowser] HostShareButton_Click failed: {ex.Message}");
        }
    }

    private async void HostShareSelectedButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var selectedRoms = GetSelectedRoms();
            if (selectedRoms.Count == 0) return;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is not Window parentWindow) return;

            var dialog = new HostRomsWindow(selectedRoms);
            await dialog.ShowDialog(parentWindow);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[RomBrowser] HostShareSelectedButton_Click failed: {ex.Message}");
        }
    }

    private void ContextLaunch_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        LaunchRetroArchButton_Click(sender, e);
    }

    private void ContextLaunchMame_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        LaunchMameButton_Click(sender, e);
    }

    private void ContextLaunchMednafen_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        LaunchMednafenButton_Click(sender, e);
    }

    private void ContextConvert_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (RomDataGrid.SelectedItem is not RomInfo selectedRom) return;

        StatusText.Text = string.Format(LocalizationManager.Instance["Browser_UseFormatConverter"], selectedRom.FileName);
    }

    private void ContextTrim_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (RomDataGrid.SelectedItem is not RomInfo selectedRom) return;

        StatusText.Text = string.Format(LocalizationManager.Instance["Browser_UseTrimmer"], selectedRom.FileName);
    }

    private async void ContextExportHeader_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            if (RomDataGrid.SelectedItem is not RomInfo selectedRom) return;

            var destFolder = await PickFolder(LocalizationManager.Instance["Browser_ExportDestinationTitle"]);
            if (destFolder == null) return;

            try
            {
                string outputPath = Path.Combine(destFolder, Path.GetFileNameWithoutExtension(selectedRom.FileName) + "_header.txt");
                await RomHeaderExporter.ExportSingleAsync(selectedRom.FilePath, outputPath);
                StatusText.Text = string.Format(LocalizationManager.Instance["Browser_HeaderExported"], outputPath);
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException)
            {
                StatusText.Text = string.Format(LocalizationManager.Instance["Browser_ExportError"], ex.Message);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[RomBrowser] ContextExportHeader_Click failed: {ex.Message}");
        }
    }

    private void ContextVerify_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (RomDataGrid.SelectedItem is not RomInfo selectedRom) return;

        StatusText.Text = string.Format(LocalizationManager.Instance["Browser_UseVerifier"], selectedRom.FileName);
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
