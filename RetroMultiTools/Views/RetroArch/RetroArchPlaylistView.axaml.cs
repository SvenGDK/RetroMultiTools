using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Models;
using RetroMultiTools.Utilities;
using RetroMultiTools.Utilities.RetroArch;

namespace RetroMultiTools.Views.RetroArch;

public partial class RetroArchPlaylistView : UserControl
{
    private RetroArchPlaylistCreator.Playlist? _currentPlaylist;
    private CancellationTokenSource? _thumbnailCts;

    public RetroArchPlaylistView()
    {
        InitializeComponent();
        PopulateSystemCombo();
    }

    private void PopulateSystemCombo()
    {
        foreach (var (system, dbName) in RetroArchPlaylistCreator.GetAllDbMappings())
        {
            SystemCombo.Items.Add(new ComboBoxItem { Content = $"{system} — {dbName}", Tag = system });
        }
    }

    private void RefreshPlaylistDisplay()
    {
        if (_currentPlaylist == null)
        {
            PlaylistItemListBox.ItemsSource = null;
            return;
        }

        PlaylistItemListBox.ItemsSource = _currentPlaylist.Items
            .Select(i => new PlaylistDisplayItem
            {
                Label = i.Label,
                DbName = i.DbName,
            })
            .ToList();
    }

    private async void BrowseRomDirButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = LocalizationManager.Instance["RAPlaylist_SelectRomDir"],
            AllowMultiple = false
        });

        if (folders.Count == 0) return;

        RomDirectoryTextBox.Text = folders[0].Path.LocalPath;
    }

    private async void BuildPlaylistButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;

        string romDir = RomDirectoryTextBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(romDir) || !Directory.Exists(romDir))
        {
            PlaylistStatusText.Text = loc["RAPlaylist_InvalidRomDir"];
            return;
        }

        if (SystemCombo.SelectedItem is not ComboBoxItem selected || selected.Tag is not RomSystem system)
        {
            PlaylistStatusText.Text = loc["RAPlaylist_SelectSystemFirst"];
            return;
        }

        bool recursive = RecursiveCheckBox.IsChecked == true;

        BuildPlaylistButton.IsEnabled = false;
        var progress = new Progress<string>(msg => PlaylistStatusText.Text = msg);

        try
        {
            _currentPlaylist = await RetroArchPlaylistCreator.BuildFromDirectoryAsync(
                romDir, system, recursive: recursive, progress: progress);

            RefreshPlaylistDisplay();
            PlaylistStatusText.Text = string.Format(loc["RAPlaylist_Built"], _currentPlaylist.Items.Count);
        }
        catch (Exception ex)
        {
            PlaylistStatusText.Text = $"✘ {ex.Message}";
        }
        finally
        {
            BuildPlaylistButton.IsEnabled = true;
        }
    }

    private async void LoadPlaylistButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = LocalizationManager.Instance["RAPlaylist_LoadTitle"],
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("RetroArch Playlist") { Patterns = ["*.lpl"] },
                FilePickerFileTypes.All
            ]
        });

        if (files.Count == 0) return;

        try
        {
            _currentPlaylist = await RetroArchPlaylistCreator.LoadAsync(files[0].Path.LocalPath);
            if (_currentPlaylist != null)
            {
                RefreshPlaylistDisplay();
                PlaylistStatusText.Text = string.Format(
                    LocalizationManager.Instance["RAPlaylist_Loaded"], _currentPlaylist.Items.Count);
            }
            else
            {
                PlaylistStatusText.Text = LocalizationManager.Instance["RAPlaylist_LoadFailed"];
            }
        }
        catch (Exception ex)
        {
            PlaylistStatusText.Text = $"✘ {ex.Message}";
        }
    }

    private async void SavePlaylistButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_currentPlaylist == null || _currentPlaylist.Items.Count == 0)
        {
            PlaylistStatusText.Text = LocalizationManager.Instance["RAPlaylist_NothingToSave"];
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        // Determine suggested name from the first item's DB name
        string suggestedName = "playlist.lpl";
        if (_currentPlaylist.Items.Count > 0 && !string.IsNullOrEmpty(_currentPlaylist.Items[0].DbName))
            suggestedName = _currentPlaylist.Items[0].DbName;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = LocalizationManager.Instance["RAPlaylist_SaveTitle"],
            SuggestedFileName = suggestedName,
            FileTypeChoices = [new FilePickerFileType("RetroArch Playlist") { Patterns = ["*.lpl"] }]
        });

        if (file == null) return;

        try
        {
            await RetroArchPlaylistCreator.SaveAsync(_currentPlaylist, file.Path.LocalPath);
            PlaylistStatusText.Text = string.Format(
                LocalizationManager.Instance["RAPlaylist_Saved"], _currentPlaylist.Items.Count);
        }
        catch (Exception ex)
        {
            PlaylistStatusText.Text = $"✘ {ex.Message}";
        }
    }

    private void RemoveItemButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_currentPlaylist == null) return;

        int index = PlaylistItemListBox.SelectedIndex;
        if (RetroArchPlaylistCreator.RemoveItem(_currentPlaylist, index))
        {
            RefreshPlaylistDisplay();
            PlaylistStatusText.Text = LocalizationManager.Instance["RAPlaylist_ItemRemoved"];
        }
    }

    private async void DownloadThumbnailsButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;

        if (_currentPlaylist == null || _currentPlaylist.Items.Count == 0)
        {
            ThumbnailStatusText.Text = loc["RAPlaylist_NothingToDownload"];
            return;
        }

        string? thumbDir = RetroArchPlaylistCreator.GetRetroArchThumbnailsDirectory();
        if (string.IsNullOrEmpty(thumbDir))
        {
            ThumbnailStatusText.Text = loc["RAPlaylist_RetroArchNotConfigured"];
            return;
        }

        var categories = new List<string>();
        if (BoxartCheckBox.IsChecked == true) categories.Add("Named_Boxarts");
        if (SnapsCheckBox.IsChecked == true) categories.Add("Named_Snaps");
        if (TitlesCheckBox.IsChecked == true) categories.Add("Named_Titles");

        if (categories.Count == 0)
        {
            ThumbnailStatusText.Text = loc["RAPlaylist_SelectCategories"];
            return;
        }

        DownloadThumbnailsButton.IsEnabled = false;
        CancelThumbnailsButton.IsVisible = true;

        _thumbnailCts?.Dispose();
        _thumbnailCts = new CancellationTokenSource();

        bool overwrite = OverwriteCheckBox.IsChecked == true;

        try
        {
            var progress = new Progress<string>(msg => ThumbnailStatusText.Text = msg);
            var (downloaded, failed, skipped) = await RetroArchPlaylistCreator.DownloadThumbnailsAsync(
                _currentPlaylist, thumbDir, [.. categories], overwrite, progress, _thumbnailCts.Token);

            ThumbnailStatusText.Text = string.Format(
                loc["RAPlaylist_ThumbnailsComplete"], downloaded, failed, skipped);
        }
        catch (OperationCanceledException)
        {
            ThumbnailStatusText.Text = loc["RAPlaylist_ThumbnailsCancelled"];
        }
        catch (Exception ex)
        {
            ThumbnailStatusText.Text = $"✘ {ex.Message}";
        }
        finally
        {
            DownloadThumbnailsButton.IsEnabled = true;
            CancelThumbnailsButton.IsVisible = false;
            _thumbnailCts?.Dispose();
            _thumbnailCts = null;
        }
    }

    private void CancelThumbnailsButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _thumbnailCts?.Cancel();
    }

    private sealed class PlaylistDisplayItem
    {
        public string Label { get; set; } = "";
        public string DbName { get; set; } = "";
    }
}
