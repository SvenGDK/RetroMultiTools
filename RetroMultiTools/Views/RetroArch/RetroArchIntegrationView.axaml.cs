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

        var fileTypes = new List<FilePickerFileType>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            fileTypes.Add(new FilePickerFileType("RetroArch Application")
            {
                Patterns = ["retroarch", "*.app"],
                AppleUniformTypeIdentifiers = ["com.apple.application-bundle", "public.unix-executable"]
            });
        }
        else
        {
            fileTypes.Add(new FilePickerFileType("RetroArch Executable")
            {
                Patterns = [RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "retroarch.exe" : "retroarch"]
            });
        }

        fileTypes.Add(FilePickerFileTypes.All);

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = LocalizationManager.Instance["Settings_SelectRetroArchExecutable"],
            AllowMultiple = false,
            FileTypeFilter = fileTypes
        });

        if (files.Count == 0) return;

        string selectedPath = files[0].Path.LocalPath;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            string trimmed = selectedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (trimmed.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
            {
                string resolved = RetroArchLauncher.ResolveRetroArchPath(trimmed);
                if (File.Exists(resolved))
                {
                    AppSettings.Instance.RetroArchPath = trimmed;
                    RetroArchPathTextBox.Text = trimmed;
                    RetroArchStatusText.Text = LocalizationManager.Instance["Settings_RetroArchPathSaved"];
                    return;
                }

                RetroArchStatusText.Text = LocalizationManager.Instance["Settings_RetroArchInvalidBundle"];
                return;
            }
        }

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
