using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Services;
using RetroMultiTools.Utilities;
using RetroMultiTools.Utilities.RetroArch;
using System.Runtime.InteropServices;

namespace RetroMultiTools.Views.RetroArch;

public partial class RetroArchIntegrationView : UserControl
{
    private CancellationTokenSource? _downloadCts;

    public RetroArchIntegrationView()
    {
        InitializeComponent();
        LoadRetroArchPath();
        LoadControllerProfilesInfo();
    }

    // ── RetroArch Configuration ────────────────────────────────────────

    private void LoadRetroArchPath()
    {
        string path = AppSettings.Instance.RetroArchPath;
        if (!string.IsNullOrEmpty(path))
        {
            string resolved = RetroArchLauncher.ResolveRetroArchPath(path);
            RetroArchPathTextBox.Text = path;
            RetroArchStatusText.Text = File.Exists(resolved)
                ? LocalizationManager.Instance["Settings_RetroArchFound"]
                : LocalizationManager.Instance["Settings_RetroArchFileNotFound"];
        }
        else if (RetroArchLauncher.IsRetroArchAvailable())
        {
            string detected = RetroArchLauncher.GetRetroArchExecutablePath();
            RetroArchPathTextBox.Text = detected;
            RetroArchStatusText.Text = LocalizationManager.Instance["Settings_RetroArchAutoDetected"];
        }
    }

    private async void BrowseRetroArchButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        string? selectedPath = null;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // On macOS, .app bundles are directories and cannot be selected via
            // OpenFilePickerAsync (Avalonia limitation – see Avalonia #18080).
            // Use a folder picker so the user can select .app bundles or the
            // folder containing the executable.  If the folder picker also cannot
            // select the .app, the user can paste the path directly in the text
            // box and press Enter.
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = LocalizationManager.Instance["Settings_SelectRetroArchExecutable"],
                AllowMultiple = false
            });

            if (folders.Count == 0) return;

            selectedPath = Uri.UnescapeDataString(folders[0].Path.LocalPath);

            // Process the macOS path
            if (TryAcceptRetroArchPath(selectedPath))
                return;

            RetroArchStatusText.Text = LocalizationManager.Instance["Settings_RetroArchNotFound"];
            return;
        }

        // Non-macOS: use a standard file picker
        var fileTypes = new List<FilePickerFileType>
        {
            new(LocalizationManager.Instance["RAIntegration_RetroArchExecutable"])
            {
                Patterns = [RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "retroarch.exe" : "retroarch"]
            },
            FilePickerFileTypes.All
        };

        var pickedFiles = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = LocalizationManager.Instance["Settings_SelectRetroArchExecutable"],
            AllowMultiple = false,
            FileTypeFilter = fileTypes
        });

        if (pickedFiles.Count == 0) return;

        selectedPath = Uri.UnescapeDataString(pickedFiles[0].Path.LocalPath);

        string exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "retroarch.exe" : "retroarch";
        string fileName = Path.GetFileName(selectedPath);

        if (!string.Equals(fileName, exeName, StringComparison.OrdinalIgnoreCase))
        {
            RetroArchStatusText.Text = string.Format(LocalizationManager.Instance["Settings_RetroArchNotSelectedFile"], exeName);
            return;
        }

        AppSettings.Instance.RetroArchPath = selectedPath;
        RetroArchPathTextBox.Text = selectedPath;
        RetroArchStatusText.Text = LocalizationManager.Instance["Settings_RetroArchPathSaved"];
    }

    /// <summary>
    /// Allows the user to paste or type a path and press Enter to validate it.
    /// This is the primary path-entry mechanism on macOS where the native file
    /// picker cannot select .app bundles.
    /// </summary>
    private void RetroArchPathTextBox_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key != Avalonia.Input.Key.Enter) return;

        string text = RetroArchPathTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            RetroArchStatusText.Text = LocalizationManager.Instance["Settings_RetroArchNotFound"];
            return;
        }

        if (TryAcceptRetroArchPath(text))
            return;

        RetroArchStatusText.Text = LocalizationManager.Instance["Settings_RetroArchNotFound"];
    }

    /// <summary>
    /// Validates a candidate path and, when it resolves to a RetroArch executable,
    /// saves it to settings.  Returns true when the path was accepted.
    /// </summary>
    private bool TryAcceptRetroArchPath(string candidatePath)
    {
        string trimmed = candidatePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string resolved = RetroArchLauncher.ResolveRetroArchPath(trimmed);

        if (File.Exists(resolved))
        {
            // On macOS, prefer storing the .app bundle path for a cleaner UX.
            // For example, if the user selected /Applications/ (because the
            // folder picker could not select .app directly), display and store
            // /Applications/RetroArch.app instead of /Applications/.
            string storedPath = trimmed;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string? bundleRoot = AppBundleHelper.GetAppBundleRoot(resolved);
                if (bundleRoot != null)
                    storedPath = bundleRoot;
            }

            AppSettings.Instance.RetroArchPath = storedPath;
            RetroArchPathTextBox.Text = storedPath;
            RetroArchStatusText.Text = LocalizationManager.Instance["Settings_RetroArchPathSaved"];
            return true;
        }

        return false;
    }

    private void DetectRetroArchButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (RetroArchLauncher.IsRetroArchAvailable())
        {
            string detected = RetroArchLauncher.GetRetroArchExecutablePath();
            AppSettings.Instance.RetroArchPath = detected;
            RetroArchPathTextBox.Text = detected;
            RetroArchStatusText.Text = LocalizationManager.Instance["Settings_RetroArchAutoDetectedSaved"];
        }
        else
        {
            RetroArchStatusText.Text = LocalizationManager.Instance["Settings_RetroArchNotFound"];
        }
    }

    private void DownloadRetroArchButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (RetroArchLauncher.OpenDownloadPage())
        {
            RetroArchStatusText.Text = LocalizationManager.Instance["Settings_RetroArchDownloadPageOpened"];
        }
        else
        {
            RetroArchStatusText.Text = string.Format(
                LocalizationManager.Instance["Settings_RetroArchCouldNotOpenBrowser"],
                RetroArchLauncher.GetDownloadUrl());
        }
    }

    private void ClearRetroArchButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        AppSettings.Instance.RetroArchPath = string.Empty;
        RetroArchPathTextBox.Text = string.Empty;
        RetroArchStatusText.Text = LocalizationManager.Instance["Settings_RetroArchPathCleared"];
    }

    // ── Core Downloader ────────────────────────────────────────────────

    private void CheckCoresButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!RetroArchLauncher.IsRetroArchAvailable())
        {
            CoreDownloadStatusText.Text = LocalizationManager.Instance["Settings_RetroArchNotConfigured"];
            return;
        }

        var cores = RetroArchCoreDownloader.GetAvailableCores();
        int installed = cores.Count(c => c.IsInstalled);
        int missing = cores.Count - installed;

        CoresList.ItemsSource = cores.Select(c => new CoreDisplayItem
        {
            StatusIcon = c.IsInstalled ? "✔" : "✘",
            CoreName = c.CoreName,
            DisplayName = c.DisplayName
        }).ToList();

        CoreDownloadStatusText.Text = string.Format(
            LocalizationManager.Instance["Settings_CoresFound"], cores.Count, installed, missing);
    }

    private async void DownloadMissingCoresButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!RetroArchLauncher.IsRetroArchAvailable())
        {
            CoreDownloadStatusText.Text = LocalizationManager.Instance["Settings_RetroArchNotConfigured"];
            return;
        }

        DownloadMissingCoresButton.IsEnabled = false;
        CheckCoresButton.IsEnabled = false;
        CancelDownloadButton.IsVisible = true;

        _downloadCts?.Dispose();
        _downloadCts = new CancellationTokenSource();

        try
        {
            var progress = new Progress<string>(msg => CoreDownloadStatusText.Text = msg);
            var (downloaded, failed) = await RetroArchCoreDownloader.DownloadAllMissingCoresAsync(
                progress, _downloadCts.Token);

            CoreDownloadStatusText.Text = string.Format(
                LocalizationManager.Instance["Settings_CoresDownloadComplete"], downloaded, failed);

            // Refresh the core list
            CheckCoresButton_Click(null, e);
        }
        catch (OperationCanceledException)
        {
            CoreDownloadStatusText.Text = LocalizationManager.Instance["Settings_CoresDownloadCancelled"];
        }
        catch (HttpRequestException ex)
        {
            CoreDownloadStatusText.Text = string.Format(
                LocalizationManager.Instance["Settings_CoresNetworkError"], ex.Message);
        }
        finally
        {
            DownloadMissingCoresButton.IsEnabled = true;
            CheckCoresButton.IsEnabled = true;
            CancelDownloadButton.IsVisible = false;
            _downloadCts?.Dispose();
            _downloadCts = null;
        }
    }

    private void CancelDownloadButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _downloadCts?.Cancel();
    }

    // ── Controller Profiles ────────────────────────────────────────────

    private void LoadControllerProfilesInfo()
    {
        var loc = LocalizationManager.Instance;
        var info = ControllerProfileDownloader.GetInstalledInfo();
        if (info.Exists)
        {
            string date = info.LastModifiedUtc?.ToLocalTime().ToString("g") ?? "?";
            ControllerProfilesInfoText.Text = string.Format(
                loc["Settings_ControllerProfilesInstalled"], info.MappingCount, date);
        }
        else
        {
            ControllerProfilesInfoText.Text = loc["Settings_ControllerProfilesNotInstalled"];
        }
    }

    private async void DownloadControllerProfilesButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        DownloadControllerProfilesButton.IsEnabled = false;
        ControllerProfilesStatusText.Text = loc["Settings_ControllerProfilesDownloading"];

        try
        {
            var progress = new Progress<string>(msg =>
                ControllerProfilesStatusText.Text = msg);

            var result = await ControllerProfileDownloader.DownloadProfilesAsync(
                progress, CancellationToken.None);

            if (result.Success)
            {
                ControllerProfilesStatusText.Text = string.Format(
                    loc["Settings_ControllerProfilesSuccess"],
                    result.MappingCount, result.DestinationsWritten);

                LoadControllerProfilesInfo();
            }
            else
            {
                ControllerProfilesStatusText.Text = loc["Settings_ControllerProfilesFailed"];
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine(
                $"[RetroArchIntegrationView] Controller profiles download error: {ex.Message}");
            ControllerProfilesStatusText.Text =
                $"✘ {loc["Settings_ControllerProfilesFailed"]}";
        }
        finally
        {
            DownloadControllerProfilesButton.IsEnabled = true;
        }
    }

    private void OpenGamepadToolButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var window = new GamepadMapperWindow();
        var topLevel = TopLevel.GetTopLevel(this) as Window;
        if (topLevel != null)
            window.ShowDialog(topLevel);
        else
            window.Show();
    }

    private sealed class CoreDisplayItem
    {
        public string StatusIcon { get; set; } = "";
        public string CoreName { get; set; } = "";
        public string DisplayName { get; set; } = "";
    }
}
