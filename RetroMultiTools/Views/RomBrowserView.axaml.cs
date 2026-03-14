using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
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
        DetachedFromVisualTree += (_, _) => CancelArtworkLoading();
    }

    private void PopulateSystemFilter()
    {
        SystemFilterCombo.Items.Clear();
        SystemFilterCombo.Items.Add(new ComboBoxItem { Content = "All Systems", Tag = "all" });
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
        StatusText.Text = "Scanning...";
        ScanProgressBar.IsVisible = true;
        BrowseFolderButton.IsEnabled = false;
        ClearArtwork();

        try
        {
            var progress = new Progress<string>(msg => StatusText.Text = msg);
            _allRoms = await Task.Run(() => RomOrganizer.ScanDirectory(_currentFolder, progress));
        }
        catch (IOException ex)
        {
            StatusText.Text = $"Scan error: {ex.Message}";
            _allRoms = new();
        }
        catch (UnauthorizedAccessException ex)
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

    private void ApplyFilter()
    {
        if (SystemFilterCombo.SelectedItem is ComboBoxItem item && item.Tag is RomSystem selectedSystem)
        {
            RomDataGrid.ItemsSource = _allRoms.Where(r => r.System == selectedSystem).ToList();
        }
        else
        {
            RomDataGrid.ItemsSource = _allRoms;
        }
    }

    private void RomDataGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateButtonStates();

        if (ShowArtworkCheck.IsChecked == true && RomDataGrid.SelectedItem is RomInfo selectedRom)
        {
            _ = LoadArtworkAsync(selectedRom);
        }
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
        ArtworkStatusText.Text = "Loading artwork...";
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
                ArtworkStatusText.Text = "Failed to load artwork.";
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
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (IOException)
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
        ArtworkRomName.Text = "Select a ROM to view artwork";
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
            catch (IOException)
            {
                failed++;
            }
            catch (UnauthorizedAccessException)
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
            catch (IOException)
            {
                failed++;
            }
            catch (UnauthorizedAccessException)
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
            catch (IOException)
            {
                failed++;
            }
            catch (UnauthorizedAccessException)
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
            catch (IOException)
            {
                failed++;
            }
            catch (UnauthorizedAccessException)
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
        StatusText.Text = "Organizing...";
        OrganizeButton.IsEnabled = false;

        try
        {
            var result = await Task.Run(() => RomOrganizer.OrganizeBySystem(_allRoms, outputDir));
            StatusText.Text = $"Organized into {outputDir}: {result.Summary}";
        }
        catch (IOException ex)
        {
            StatusText.Text = $"Organize error: {ex.Message}";
        }
        catch (UnauthorizedAccessException ex)
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
}
