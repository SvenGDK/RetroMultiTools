using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using RetroMultiTools.Utilities;
using RetroMultiTools.Utilities.Analogue;

namespace RetroMultiTools.Views.Analogue;

public partial class AnaloguePocketView : UserControl
{
    private static readonly IBrush StatusErrorBrush = new SolidColorBrush(Color.Parse("#F38BA8"));
    private static readonly IBrush StatusSuccessBrush = new SolidColorBrush(Color.Parse("#A6E3A1"));
    private static readonly IBrush ResultLabelBrush = new SolidColorBrush(Color.Parse("#A6ADC8"));
    private static readonly IBrush ResultValueBrush = new SolidColorBrush(Color.Parse("#CDD6F4"));

    private string _sdRoot = string.Empty;

    public AnaloguePocketView()
    {
        InitializeComponent();
    }

    // ── SD Card Selection ──────────────────────────────────────────────

    private async void BrowseSdCard_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFolder("Select Analogue Pocket SD Card");
        if (path == null) return;

        if (!AnaloguePocketManager.ValidateSdCard(path))
        {
            ShowStatus("⚠ The selected folder does not appear to be a valid Analogue Pocket SD card. " +
                        "Expected directories like Cores/, Platforms/, or Assets/.", isError: true);
            return;
        }

        _sdRoot = path;
        SdCardPathTextBox.Text = path;
        SetButtonsEnabled(true);
        ShowStatus("✔ Pocket SD card loaded successfully.", isError: false);
    }

    private void SetButtonsEnabled(bool enabled)
    {
        BrowseCoresButton.IsEnabled = enabled;
        ExportScreenshotsButton.IsEnabled = enabled;
        BackupSavesButton.IsEnabled = enabled;
        RestoreSavesButton.IsEnabled = enabled;
        OpenGameFoldersButton.IsEnabled = enabled;
        ManageSaveStatesButton.IsEnabled = enabled;
        ExportGbCameraButton.IsEnabled = enabled;
        AutoCopyFilesButton.IsEnabled = enabled;
        LibraryImageGenButton.IsEnabled = enabled;
    }

    // ── Browse & Install Cores ─────────────────────────────────────────

    private async void BrowseCores_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SetButtonsEnabled(false);
        ProgressPanel.IsVisible = true;
        ProgressText.Text = "Scanning installed cores...";

        try
        {
            var cores = await AnaloguePocketManager.ListCoresAsync(_sdRoot);

            ResultsPanel.Children.Clear();
            ResultsBorder.IsVisible = true;

            if (cores.Count == 0)
            {
                ShowStatus("No installed cores found on the SD card.", isError: false);
            }
            else
            {
                ShowStatus($"✔ Found {cores.Count} installed core(s).", isError: false);

                foreach (var core in cores)
                {
                    var corePanel = new Border
                    {
                        Background = Brushes.Transparent,
                        Padding = new Avalonia.Thickness(8, 6),
                        Margin = new Avalonia.Thickness(0, 0, 0, 4),
                        CornerRadius = new Avalonia.CornerRadius(4),
                        Child = new StackPanel
                        {
                            Spacing = 2,
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = $"{core.Author}.{core.CoreName}",
                                    FontWeight = Avalonia.Media.FontWeight.SemiBold,
                                    Foreground = ResultValueBrush
                                },
                                new TextBlock
                                {
                                    Text = $"Platform: {(string.IsNullOrEmpty(core.Platform) ? "Unknown" : core.Platform)}  |  " +
                                           $"Version: {(string.IsNullOrEmpty(core.Version) ? "Unknown" : core.Version)}  |  " +
                                           $"Size: {core.SizeFormatted}",
                                    FontSize = 12,
                                    Foreground = ResultLabelBrush
                                }
                            }
                        }
                    };

                    if (!string.IsNullOrEmpty(core.Description))
                    {
                        ((StackPanel)corePanel.Child).Children.Add(new TextBlock
                        {
                            Text = core.Description,
                            FontSize = 11,
                            Foreground = ResultLabelBrush,
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap
                        });
                    }

                    ResultsPanel.Children.Add(corePanel);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowStatus($"✘ Error scanning cores: {ex.Message}", isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            SetButtonsEnabled(true);
        }
    }

    // ── Export Screenshots ──────────────────────────────────────────────

    private async void ExportScreenshots_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var outputDir = await PickFolder("Select Output Folder for Screenshots");
        if (outputDir == null) return;

        SetButtonsEnabled(false);
        ProgressPanel.IsVisible = true;
        ResultsBorder.IsVisible = false;

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);
            int count = await AnaloguePocketManager.ExportScreenshotsAsync(_sdRoot, outputDir, progress);
            ShowStatus(count > 0
                ? $"✔ Exported {count} screenshot(s) to {outputDir}"
                : "No screenshots found on the SD card.", isError: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowStatus($"✘ Error exporting screenshots: {ex.Message}", isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            SetButtonsEnabled(true);
        }
    }

    // ── Backup & Restore Saves ─────────────────────────────────────────

    private async void BackupSaves_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var backupDir = await PickFolder("Select Backup Destination Folder");
        if (backupDir == null) return;

        SetButtonsEnabled(false);
        ProgressPanel.IsVisible = true;
        ResultsBorder.IsVisible = false;

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);
            int count = await AnaloguePocketManager.BackupSavesAsync(_sdRoot, backupDir, progress);
            ShowStatus(count > 0
                ? $"✔ Backed up {count} save file(s) to {backupDir}"
                : "No save files found on the SD card.", isError: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowStatus($"✘ Error backing up saves: {ex.Message}", isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            SetButtonsEnabled(true);
        }
    }

    private async void RestoreSaves_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var backupDir = await PickFolder("Select Backup Folder to Restore From");
        if (backupDir == null) return;

        SetButtonsEnabled(false);
        ProgressPanel.IsVisible = true;
        ResultsBorder.IsVisible = false;

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);
            int count = await AnaloguePocketManager.RestoreSavesAsync(_sdRoot, backupDir, progress);
            ShowStatus(count > 0
                ? $"✔ Restored {count} save file(s) to the Pocket."
                : "No save files found in the backup folder.", isError: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowStatus($"✘ Error restoring saves: {ex.Message}", isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            SetButtonsEnabled(true);
        }
    }

    // ── Open Game Folders ──────────────────────────────────────────────

    private void OpenGameFolders_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var folders = AnaloguePocketManager.GetGameFolders(_sdRoot);

        ResultsPanel.Children.Clear();
        ResultsBorder.IsVisible = true;

        if (folders.Count == 0)
        {
            ShowStatus("No game folders found under Assets/.", isError: false);
            ResultsBorder.IsVisible = false;
            return;
        }

        ShowStatus($"✔ Found {folders.Count} platform folder(s). Click to open.", isError: false);

        foreach (var (name, path) in folders)
        {
            var button = new Button
            {
                Content = $"📁 {name}",
                Padding = new Avalonia.Thickness(12, 6),
                Margin = new Avalonia.Thickness(0, 0, 0, 4),
                Tag = path
            };
            button.Click += (_, _) => OpenInFileExplorer(path);
            ResultsPanel.Children.Add(button);
        }
    }

    private static void OpenInFileExplorer(string path)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true })?.Dispose();
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                Process.Start("open", path)?.Dispose();
            else
                Process.Start("xdg-open", path)?.Dispose();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Silently fail if the file explorer cannot be opened
        }
    }

    // ── Manage Save States ─────────────────────────────────────────────

    private async void ManageSaveStates_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RefreshSaveStatesAsync();
    }

    private async Task RefreshSaveStatesAsync()
    {
        SetButtonsEnabled(false);
        ProgressPanel.IsVisible = true;
        ProgressText.Text = "Scanning save states...";

        try
        {
            var states = await AnaloguePocketManager.ListSaveStatesAsync(_sdRoot);

            ResultsPanel.Children.Clear();
            ResultsBorder.IsVisible = true;

            if (states.Count == 0)
            {
                ShowStatus("No save states found on the SD card.", isError: false);
                ResultsBorder.IsVisible = false;
            }
            else
            {
                ShowStatus($"✔ Found {states.Count} save state(s). Select states to delete.", isError: false);

                var checkboxes = new List<CheckBox>();
                foreach (var state in states)
                {
                    var cb = new CheckBox
                    {
                        Content = $"{state.FileName}  ({state.SizeFormatted}, {state.Platform}, {state.LastModified:yyyy-MM-dd HH:mm})",
                        Foreground = ResultValueBrush,
                        Tag = state.FullPath,
                        FontSize = 12
                    };
                    checkboxes.Add(cb);
                    ResultsPanel.Children.Add(cb);
                }

                // Add select all / delete buttons
                var buttonPanel = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    Spacing = 8,
                    Margin = new Avalonia.Thickness(0, 8, 0, 0)
                };

                var selectAllBtn = new Button { Content = "Select All", Padding = new Avalonia.Thickness(12, 6) };
                selectAllBtn.Click += (_, _) =>
                {
                    foreach (var cb in checkboxes)
                        cb.IsChecked = true;
                };

                var deselectBtn = new Button { Content = "Deselect All", Padding = new Avalonia.Thickness(12, 6) };
                deselectBtn.Click += (_, _) =>
                {
                    foreach (var cb in checkboxes)
                        cb.IsChecked = false;
                };

                var deleteBtn = new Button
                {
                    Content = "Delete Selected",
                    Padding = new Avalonia.Thickness(12, 6),
                    Foreground = StatusErrorBrush
                };
                deleteBtn.Click += async (_, _) =>
                {
                    var selected = checkboxes
                        .Where(cb => cb.IsChecked == true && cb.Tag is string)
                        .Select(cb => (string)cb.Tag!)
                        .ToList();

                    if (selected.Count == 0)
                    {
                        ShowStatus("No save states selected.", isError: false);
                        return;
                    }

                    var progress = new Progress<string>(msg => ProgressText.Text = msg);
                    ProgressPanel.IsVisible = true;
                    int deleted = await AnaloguePocketManager.DeleteSaveStatesAsync(selected, progress);
                    ProgressPanel.IsVisible = false;
                    ShowStatus($"✔ Deleted {deleted} save state(s).", isError: false);

                    // Refresh the list
                    await RefreshSaveStatesAsync();
                };

                buttonPanel.Children.Add(selectAllBtn);
                buttonPanel.Children.Add(deselectBtn);
                buttonPanel.Children.Add(deleteBtn);
                ResultsPanel.Children.Insert(0, buttonPanel);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowStatus($"✘ Error scanning save states: {ex.Message}", isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            SetButtonsEnabled(true);
        }
    }

    // ── Export GB Camera Photos ─────────────────────────────────────────

    private async void ExportGbCamera_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        // Pick a .sav file (GB Camera save)
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Game Boy Camera Save File",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Save Files") { Patterns = ["*.sav"] },
                FilePickerFileTypes.All
            ]
        });

        if (files.Count == 0) return;
        string savPath = files[0].Path.LocalPath;

        var outputDir = await PickFolder("Select Output Folder for GB Camera Photos");
        if (outputDir == null) return;

        SetButtonsEnabled(false);
        ProgressPanel.IsVisible = true;
        ResultsBorder.IsVisible = false;

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);
            int count = await AnaloguePocketManager.ExportGbCameraPhotosAsync(savPath, outputDir, progress);
            ShowStatus(count > 0
                ? $"✔ Exported {count} GB Camera photo(s) to {outputDir}"
                : "No photos found in the save file (slots may be empty).", isError: false);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            ShowStatus($"✘ Error exporting GB Camera photos: {ex.Message}", isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            SetButtonsEnabled(true);
        }
    }

    // ── Auto-Copy Files ────────────────────────────────────────────────

    private async void AutoCopyFiles_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var sourceDir = await PickFolder("Select Source Folder to Copy From");
        if (sourceDir == null) return;

        SetButtonsEnabled(false);
        ProgressPanel.IsVisible = true;
        ResultsBorder.IsVisible = false;

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);
            int count = await AnaloguePocketManager.AutoCopyFilesAsync(sourceDir, _sdRoot, progress);
            ShowStatus(count > 0
                ? $"✔ Copied {count} file(s) to the Pocket SD card."
                : "No files found in the source folder.", isError: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            ShowStatus($"✘ Error copying files: {ex.Message}", isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            SetButtonsEnabled(true);
        }
    }

    // ── Library Image Generator ────────────────────────────────────────

    private async void LibraryImageGen_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SetButtonsEnabled(false);
        ProgressPanel.IsVisible = true;
        ResultsBorder.IsVisible = false;

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);
            int count = await AnaloguePocketManager.GenerateLibraryImagesAsync(_sdRoot, progress);
            ShowStatus(count > 0
                ? $"✔ Generated {count} library image(s)."
                : "All ROMs already have library images, or no ROMs found.", isError: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowStatus($"✘ Error generating library images: {ex.Message}", isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            SetButtonsEnabled(true);
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────

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
