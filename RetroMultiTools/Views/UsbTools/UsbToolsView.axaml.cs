using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using RetroMultiTools.Localization;
using RetroMultiTools.Utilities;
using RetroMultiTools.Utilities.UsbTools;
using RetroMultiTools.Views.Dialogs;

namespace RetroMultiTools.Views.UsbTools;

public partial class UsbToolsView : UserControl
{
    private static readonly IBrush StatusErrorBrush = new SolidColorBrush(Color.Parse("#F38BA8"));
    private static readonly IBrush StatusSuccessBrush = new SolidColorBrush(Color.Parse("#A6E3A1"));

    private CancellationTokenSource? _cts;
    private string _currentBrowsePath = "";
    private readonly DispatcherTimer _usbWatchTimer;
    private bool _isBusy;

    /// <summary>
    /// Snapshot of drive device IDs used by the USB watcher to detect changes.
    /// </summary>
    private HashSet<string> _knownDriveIds = new(StringComparer.OrdinalIgnoreCase);

    public UsbToolsView()
    {
        InitializeComponent();

        // Remove file systems that the host OS cannot natively format
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // macOS cannot natively format NTFS
            var ntfsItem = FileSystemCombo.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(i => i.Tag?.ToString() == "NTFS");
            if (ntfsItem != null)
                FileSystemCombo.Items.Remove(ntfsItem);
        }

        DriveCombo.SelectionChanged += DriveCombo_SelectionChanged;
        WriteImagePathTextBox.TextChanged += (_, _) => UpdateWriteImageButton();
        CreateImageOutputTextBox.TextChanged += (_, _) => UpdateCreateImageButton();

        // USB auto-detection timer (polls every 3 seconds)
        _usbWatchTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _usbWatchTimer.Tick += async (_, _) => await CheckForDriveChangesAsync();

        Loaded += async (_, _) =>
        {
            await RefreshDrivesAsync();
            _usbWatchTimer.Start();
        };

        Unloaded += (_, _) => _usbWatchTimer.Stop();
    }

    // ── USB Detection & Auto-Refresh ────────────────────────────────────

    private async void RefreshDrives_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RefreshDrivesAsync();
    }

    private async Task RefreshDrivesAsync()
    {
        var progress = new Progress<string>(msg => ProgressText.Text = msg);
        ProgressPanel.IsVisible = true;

        try
        {
            var drives = await UsbToolsHelper.DetectDrivesAsync(progress);

            DriveCombo.Items.Clear();
            _knownDriveIds.Clear();

            foreach (var drive in drives)
            {
                DriveCombo.Items.Add(new ComboBoxItem
                {
                    Content = drive.DisplayName,
                    Tag = drive
                });
                _knownDriveIds.Add(drive.DeviceId);
            }

            if (drives.Count > 0)
                DriveCombo.SelectedIndex = 0;
            else
                ShowStatus(LocalizationManager.Instance["UsbTools_NoDrivesFound"], isError: false);
        }
        catch (Exception ex)
        {
            ShowStatus(string.Format(
                LocalizationManager.Instance["UsbTools_DriveDetectError"], ex.Message), isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
        }

        UpdateAllButtons();
    }

    /// <summary>
    /// Periodically checks whether USB drives have been plugged in or removed.
    /// If the set of drives changes, performs a full refresh.
    /// Skipped while an operation is in progress to avoid interference.
    /// </summary>
    private async Task CheckForDriveChangesAsync()
    {
        if (_isBusy) return;

        try
        {
            var drives = await UsbToolsHelper.DetectDrivesAsync();
            var currentIds = new HashSet<string>(
                drives.Select(d => d.DeviceId), StringComparer.OrdinalIgnoreCase);

            if (!currentIds.SetEquals(_knownDriveIds))
            {
                await RefreshDrivesAsync();
            }
        }
        catch
        {
            // Silently ignore detection errors during polling
        }
    }

    private void DriveCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateAllButtons();

        // Auto-navigate to USB root in file explorer
        var drive = GetSelectedDrive();
        if (drive != null && !string.IsNullOrEmpty(drive.MountPoint))
        {
            NavigateToPath(drive.MountPoint);
            SetExplorerEnabled(true);
        }
        else
        {
            _currentBrowsePath = "";
            CurrentPathTextBox.Text = "";
            FileListBox.Items.Clear();
            SetExplorerEnabled(false);
        }
    }

    private UsbDriveInfo? GetSelectedDrive()
    {
        return (DriveCombo.SelectedItem as ComboBoxItem)?.Tag as UsbDriveInfo;
    }

    private string GetSelectedDeviceId()
    {
        return GetSelectedDrive()?.DeviceId ?? "";
    }

    // ── File Explorer ───────────────────────────────────────────────────

    private void NavigateToPath(string path)
    {
        _currentBrowsePath = path;
        CurrentPathTextBox.Text = path;
        RefreshFileList();
    }

    private void RefreshFileList()
    {
        FileListBox.Items.Clear();

        if (string.IsNullOrEmpty(_currentBrowsePath) || !Directory.Exists(_currentBrowsePath))
            return;

        var entries = UsbToolsHelper.GetDirectoryContents(_currentBrowsePath);
        foreach (var entry in entries)
        {
            string icon = entry.IsDirectory ? "📁" : "📄";
            string sizeStr = entry.IsDirectory ? "" : UsbToolsHelper.FormatSize(entry.Size);
            string display = string.IsNullOrEmpty(sizeStr)
                ? $"{icon}  {entry.Name}"
                : $"{icon}  {entry.Name}  ({sizeStr})";

            FileListBox.Items.Add(new ListBoxItem
            {
                Content = display,
                Tag = entry
            });
        }

        UpdateFileButtons();
    }

    private void RefreshFiles_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        RefreshFileList();
    }

    private void NavigateUp_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentBrowsePath)) return;

        var drive = GetSelectedDrive();
        if (drive == null || string.IsNullOrEmpty(drive.MountPoint)) return;

        string? parent = Path.GetDirectoryName(_currentBrowsePath);
        if (parent == null) return;

        // Don't navigate above the USB mount point.
        // Use full-path comparison to avoid prefix mismatches
        // (e.g. /media/usb vs /media/usb2).
        string normalizedParent = Path.GetFullPath(parent);
        string normalizedMount = Path.GetFullPath(drive.MountPoint);

        if (normalizedParent.Length >= normalizedMount.Length
            && normalizedParent.StartsWith(normalizedMount, StringComparison.OrdinalIgnoreCase)
            && (normalizedParent.Length == normalizedMount.Length
                || normalizedMount.EndsWith(Path.DirectorySeparatorChar)
                || normalizedParent[normalizedMount.Length] == Path.DirectorySeparatorChar))
        {
            NavigateToPath(parent);
        }
    }

    private void FileListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateFileButtons();
    }

    private void FileListBox_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        var entry = GetSelectedFileEntry();
        if (entry is { IsDirectory: true })
        {
            NavigateToPath(entry.FullPath);
        }
    }

    private UsbFileEntry? GetSelectedFileEntry()
    {
        return (FileListBox.SelectedItem as ListBoxItem)?.Tag as UsbFileEntry;
    }

    private void UpdateFileButtons()
    {
        bool hasSelection = GetSelectedFileEntry() != null;
        DeleteButton.IsEnabled = hasSelection;
        MoveButton.IsEnabled = hasSelection;
        ContextDelete.IsEnabled = hasSelection;
        ContextMove.IsEnabled = hasSelection;
    }

    /// <summary>
    /// Enables or disables the file explorer toolbar buttons.
    /// Called when a drive is selected or deselected, based on mount point availability.
    /// </summary>
    private void SetExplorerEnabled(bool enabled)
    {
        AddFilesButton.IsEnabled = enabled;
        NewFolderButton.IsEnabled = enabled;
        NavigateUpButton.IsEnabled = enabled;
        ContextAddFiles.IsEnabled = enabled;
        ContextNewFolder.IsEnabled = enabled;

        if (!enabled)
        {
            DeleteButton.IsEnabled = false;
            MoveButton.IsEnabled = false;
            ContextDelete.IsEnabled = false;
            ContextMove.IsEnabled = false;
        }
    }

    private async void AddFiles_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isBusy || string.IsNullOrEmpty(_currentBrowsePath)) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = LocalizationManager.Instance["UsbTools_AddFiles"],
            AllowMultiple = true,
            FileTypeFilter = [FilePickerFileTypes.All]
        });

        if (files.Count == 0) return;

        var paths = files.Select(f => Uri.UnescapeDataString(f.Path.LocalPath)).ToArray();

        _isBusy = true;
        _cts = new CancellationTokenSource();
        ProgressPanel.IsVisible = true;
        CancelButton.IsVisible = true;

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);
            var result = await UsbToolsHelper.AddFilesAsync(paths, _currentBrowsePath, progress, _cts.Token);
            ShowStatus(result.Message, isError: !result.IsSuccess);
            RefreshFileList();
        }
        catch (OperationCanceledException)
        {
            ShowStatus(LocalizationManager.Instance["UsbTools_OperationCancelled"], isError: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowStatus(string.Format(
                LocalizationManager.Instance["Common_ErrorFormat"], ex.Message), isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            CancelButton.IsVisible = false;
            _isBusy = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private async void NewFolder_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isBusy || string.IsNullOrEmpty(_currentBrowsePath)) return;

        var dialog = new NewFolderDialog();
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is Window parentWindow)
        {
            string? folderName = await dialog.ShowDialog(parentWindow);
            if (!string.IsNullOrWhiteSpace(folderName))
            {
                string newPath = Path.Combine(_currentBrowsePath, folderName);
                var result = UsbToolsHelper.CreateFolder(newPath);
                ShowStatus(result.Message, isError: !result.IsSuccess);
                RefreshFileList();
            }
        }
    }

    private async void Delete_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isBusy) return;

        var entry = GetSelectedFileEntry();
        if (entry == null) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not Window parentWindow) return;

        // Confirm deletion
        var confirmDialog = new ConfirmDialog(
            LocalizationManager.Instance["UsbTools_ConfirmDelete"],
            string.Format(LocalizationManager.Instance["UsbTools_ConfirmDeleteMessage"], entry.Name));

        bool confirmed = await confirmDialog.ShowDialog(parentWindow);
        if (!confirmed) return;

        var result = UsbToolsHelper.DeleteEntry(entry.FullPath);
        ShowStatus(result.Message, isError: !result.IsSuccess);
        RefreshFileList();
    }

    private async void Move_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isBusy) return;

        var entry = GetSelectedFileEntry();
        if (entry == null) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = LocalizationManager.Instance["UsbTools_SelectMoveDestination"],
            AllowMultiple = false
        });

        if (folders.Count == 0) return;

        string destDir = Uri.UnescapeDataString(folders[0].Path.LocalPath);
        var result = UsbToolsHelper.MoveEntry(entry.FullPath, destDir);
        ShowStatus(result.Message, isError: !result.IsSuccess);
        RefreshFileList();
    }

    // ── Format USB ──────────────────────────────────────────────────────

    private async void FormatButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string deviceId = GetSelectedDeviceId();
        if (string.IsNullOrEmpty(deviceId)) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not Window parentWindow) return;

        // Confirm format
        var drive = GetSelectedDrive();
        var confirmDialog = new ConfirmDialog(
            LocalizationManager.Instance["UsbTools_ConfirmFormat"],
            string.Format(LocalizationManager.Instance["UsbTools_ConfirmFormatMessage"],
                drive?.DisplayName ?? deviceId));

        bool confirmed = await confirmDialog.ShowDialog(parentWindow);
        if (!confirmed) return;

        string fileSystem = (FileSystemCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "FAT32";
        string partScheme = (PartitionSchemeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "MBR";
        string volumeLabel = FormatVolumeLabelTextBox.Text ?? "USB_DRIVE";

        await RunOperationAsync(async (progress, ct) =>
            await UsbToolsHelper.FormatDriveAsync(deviceId, fileSystem, partScheme, volumeLabel, progress, ct));

        // Refresh drives after formatting
        await RefreshDrivesAsync();
    }

    // ── Write Image ─────────────────────────────────────────────────────

    private async void BrowseWriteImage_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        var path = await PickFile(loc["UsbTools_SelectImageFile"],
        [
            new FilePickerFileType(loc["UsbTools_ImageFiles"])
            {
                Patterns = UsbToolsHelper.ImageFilterPatterns
            },
            FilePickerFileTypes.All
        ]);

        if (path != null)
            WriteImagePathTextBox.Text = path;

        UpdateWriteImageButton();
    }

    private async void WriteImageButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string deviceId = GetSelectedDeviceId();
        string imagePath = WriteImagePathTextBox.Text ?? "";
        if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(imagePath)) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not Window parentWindow) return;

        // Confirm write
        var drive = GetSelectedDrive();
        var confirmDialog = new ConfirmDialog(
            LocalizationManager.Instance["UsbTools_ConfirmWrite"],
            string.Format(LocalizationManager.Instance["UsbTools_ConfirmWriteMessage"],
                Path.GetFileName(imagePath), drive?.DisplayName ?? deviceId));

        bool confirmed = await confirmDialog.ShowDialog(parentWindow);
        if (!confirmed) return;

        await RunOperationAsync(async (progress, ct) =>
            await UsbToolsHelper.WriteImageAsync(imagePath, deviceId, progress, ct));
    }

    // ── Create Image ────────────────────────────────────────────────────

    private async void BrowseCreateImageOutput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        var path = await PickSaveFile(loc["UsbTools_SelectOutputImage"],
        [
            new FilePickerFileType(loc["UsbTools_ImgFileType"]) { Patterns = ["*.img"] },
            new FilePickerFileType(loc["UsbTools_IsoFileType"]) { Patterns = ["*.iso"] },
            FilePickerFileTypes.All
        ]);

        if (path != null)
            CreateImageOutputTextBox.Text = path;

        UpdateCreateImageButton();
    }

    private async void CreateImageButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string deviceId = GetSelectedDeviceId();
        string outputPath = CreateImageOutputTextBox.Text ?? "";
        if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(outputPath)) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not Window parentWindow) return;

        // Confirm create image
        var drive = GetSelectedDrive();
        var confirmDialog = new ConfirmDialog(
            LocalizationManager.Instance["UsbTools_ConfirmCreateImage"],
            string.Format(LocalizationManager.Instance["UsbTools_ConfirmCreateImageMessage"],
                drive?.DisplayName ?? deviceId));

        bool confirmed = await confirmDialog.ShowDialog(parentWindow);
        if (!confirmed) return;

        await RunOperationAsync(async (progress, ct) =>
            await UsbToolsHelper.CreateImageFromDriveAsync(deviceId, outputPath, progress, ct));
    }

    // ── Shared Operation Runner ─────────────────────────────────────────

    private async Task RunOperationAsync(
        Func<IProgress<string>, CancellationToken, Task<UsbOperationResult>> operation)
    {
        SetBusyState(true);
        _cts = new CancellationTokenSource();

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);
            var result = await operation(progress, _cts.Token);
            ShowStatus(result.Message, isError: !result.IsSuccess);
        }
        catch (OperationCanceledException)
        {
            ShowStatus(LocalizationManager.Instance["UsbTools_OperationCancelled"], isError: false);
        }
        catch (Exception ex)
        {
            ShowStatus(string.Format(
                LocalizationManager.Instance["Common_ErrorFormat"], ex.Message), isError: true);
        }
        finally
        {
            SetBusyState(false);
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _cts?.Cancel();
    }

    // ── UI State ────────────────────────────────────────────────────────

    private void SetBusyState(bool busy)
    {
        _isBusy = busy;
        ProgressPanel.IsVisible = busy;
        CancelButton.IsVisible = busy;
        FormatButton.IsEnabled = !busy && DriveCombo.SelectedItem != null;
        WriteImageButton.IsEnabled = !busy && DriveCombo.SelectedItem != null
                                     && !string.IsNullOrEmpty(WriteImagePathTextBox.Text);
        CreateImageButton.IsEnabled = !busy && DriveCombo.SelectedItem != null
                                      && !string.IsNullOrEmpty(CreateImageOutputTextBox.Text);
        if (busy) StatusBorder.IsVisible = false;
    }

    private void UpdateAllButtons()
    {
        bool hasDrive = DriveCombo.SelectedItem != null;
        FormatButton.IsEnabled = hasDrive;
        UpdateWriteImageButton();
        UpdateCreateImageButton();
    }

    private void UpdateWriteImageButton()
    {
        WriteImageButton.IsEnabled = DriveCombo.SelectedItem != null
                                     && !string.IsNullOrEmpty(WriteImagePathTextBox.Text);
    }

    private void UpdateCreateImageButton()
    {
        CreateImageButton.IsEnabled = DriveCombo.SelectedItem != null
                                      && !string.IsNullOrEmpty(CreateImageOutputTextBox.Text);
    }

    private void ShowStatus(string message, bool isError)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError ? StatusErrorBrush : StatusSuccessBrush;
        StatusBorder.IsVisible = true;
    }

    // ── File Picker Helpers ─────────────────────────────────────────────

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

        return files.Count > 0 ? Uri.UnescapeDataString(files[0].Path.LocalPath) : null;
    }

    private async Task<string?> PickSaveFile(string title, FilePickerFileType[] filters)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return null;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            FileTypeChoices = filters
        });

        return file != null ? Uri.UnescapeDataString(file.Path.LocalPath) : null;
    }
}
