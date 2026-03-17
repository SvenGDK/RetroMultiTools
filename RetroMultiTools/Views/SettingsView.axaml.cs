using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Services;
using RetroMultiTools.Utilities;
using System.Globalization;
using System.Runtime.InteropServices;

namespace RetroMultiTools.Views;

public partial class SettingsView : UserControl
{
    private bool _isInitializing = true;
    private CancellationTokenSource? _downloadCts;

    public SettingsView()
    {
        InitializeComponent();
        PopulateLanguageCombo();
        LoadRetroArchPath();
        LoadControllerProfilesInfo();
        LoadDiscordSetting();
        LoadTraySetting();
        LoadBigPictureSettings();
        LoadUpdateSettings();
        _isInitializing = false;
    }

    private void PopulateLanguageCombo()
    {
        string currentCulture = LocalizationManager.Instance.Culture.Name;

        for (int i = 0; i < LocalizationManager.SupportedLanguages.Length; i++)
        {
            var (displayName, cultureName) = LocalizationManager.SupportedLanguages[i];
            LanguageCombo.Items.Add(new ComboBoxItem { Content = displayName, Tag = cultureName });

            if (cultureName == currentCulture)
                LanguageCombo.SelectedIndex = i;
        }

        // Default to English if no match found
        if (LanguageCombo.SelectedIndex < 0)
            LanguageCombo.SelectedIndex = 0;
    }

    private void LanguageCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        if (LanguageCombo.SelectedItem is ComboBoxItem item && item.Tag is string cultureName)
        {
            LocalizationManager.Instance.Culture = new CultureInfo(cultureName);
        }
    }

    private void LoadRetroArchPath()
    {
        string path = AppSettings.Instance.RetroArchPath;
        if (!string.IsNullOrEmpty(path))
        {
            string resolved = RetroArchLauncher.ResolveRetroArchPath(path);
            RetroArchPathTextBox.Text = path;
            RetroArchStatusText.Text = File.Exists(resolved) ? LocalizationManager.Instance["Settings_RetroArchFound"] : LocalizationManager.Instance["Settings_RetroArchFileNotFound"];
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

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = LocalizationManager.Instance["Settings_SelectRetroArchExecutable"],
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("RetroArch Executable")
                {
                    Patterns = [RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "retroarch.exe" : "retroarch"]
                },
                FilePickerFileTypes.All
            ]
        });

        if (files.Count == 0) return;

        string selectedPath = files[0].Path.LocalPath;

        // On macOS, accept .app bundles and resolve to the executable inside
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) &&
            selectedPath.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
        {
            string resolved = RetroArchLauncher.ResolveRetroArchPath(selectedPath);
            if (File.Exists(resolved))
            {
                AppSettings.Instance.RetroArchPath = selectedPath;
                RetroArchPathTextBox.Text = selectedPath;
                RetroArchStatusText.Text = LocalizationManager.Instance["Settings_RetroArchPathSaved"];
                return;
            }

            RetroArchStatusText.Text = LocalizationManager.Instance["Settings_RetroArchInvalidBundle"];
            return;
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
            RetroArchStatusText.Text = string.Format(LocalizationManager.Instance["Settings_RetroArchCouldNotOpenBrowser"], RetroArchLauncher.GetDownloadUrl());
        }
    }

    private void ClearRetroArchButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        AppSettings.Instance.RetroArchPath = string.Empty;
        RetroArchPathTextBox.Text = string.Empty;
        RetroArchStatusText.Text = LocalizationManager.Instance["Settings_RetroArchPathCleared"];
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

    private void OpenGamepadToolButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var window = new GamepadMapperWindow();
        var topLevel = TopLevel.GetTopLevel(this) as Window;
        if (topLevel != null)
            window.ShowDialog(topLevel);
        else
            window.Show();
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

                // Refresh the info display
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
                $"[SettingsView] Controller profiles download error: {ex.Message}");
            ControllerProfilesStatusText.Text =
                $"✘ {loc["Settings_ControllerProfilesFailed"]}";
        }
        finally
        {
            DownloadControllerProfilesButton.IsEnabled = true;
        }
    }

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

        CoreDownloadStatusText.Text = string.Format(LocalizationManager.Instance["Settings_CoresFound"], cores.Count, installed, missing);
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

            CoreDownloadStatusText.Text = string.Format(LocalizationManager.Instance["Settings_CoresDownloadComplete"], downloaded, failed);

            // Refresh the core list
            CheckCoresButton_Click(null, e);
        }
        catch (OperationCanceledException)
        {
            CoreDownloadStatusText.Text = LocalizationManager.Instance["Settings_CoresDownloadCancelled"];
        }
        catch (HttpRequestException ex)
        {
            CoreDownloadStatusText.Text = string.Format(LocalizationManager.Instance["Settings_CoresNetworkError"], ex.Message);
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

    private void LoadDiscordSetting()
    {
        DiscordRichPresenceCheck.IsChecked = AppSettings.Instance.DiscordRichPresenceEnabled;
    }

    private void DiscordRichPresenceCheck_Changed(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isInitializing) return;
        AppSettings.Instance.DiscordRichPresenceEnabled = DiscordRichPresenceCheck.IsChecked == true;
    }

    private void LoadTraySetting()
    {
        MinimizeToTrayOnLaunchCheck.IsChecked = AppSettings.Instance.MinimizeToTrayOnLaunch;
    }

    private void MinimizeToTrayOnLaunchCheck_Changed(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isInitializing) return;
        AppSettings.Instance.MinimizeToTrayOnLaunch = MinimizeToTrayOnLaunchCheck.IsChecked == true;
    }

    private void LoadBigPictureSettings()
    {
        StartInBigPictureModeCheck.IsChecked = AppSettings.Instance.StartInBigPictureMode;
        string folder = AppSettings.Instance.BigPictureRomFolder;
        if (!string.IsNullOrEmpty(folder))
            BigPictureRomFolderTextBox.Text = folder;
    }

    private void StartInBigPictureModeCheck_Changed(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isInitializing) return;
        AppSettings.Instance.StartInBigPictureMode = StartInBigPictureModeCheck.IsChecked == true;
    }

    private async void BrowseBigPictureFolderButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = LocalizationManager.Instance["Settings_SelectRomFolderBigPicture"],
            AllowMultiple = false
        });

        if (folders.Count == 0) return;

        string path = folders[0].Path.LocalPath;
        BigPictureRomFolderTextBox.Text = path;
        AppSettings.Instance.BigPictureRomFolder = path;
    }

    private AppUpdater.UpdateInfo? _pendingUpdate;
    private CancellationTokenSource? _updateCts;

    private void LoadUpdateSettings()
    {
        CheckForUpdatesOnStartupCheck.IsChecked = AppSettings.Instance.CheckForUpdatesOnStartup;
        VersionText.Text = string.Format(
            Localization.LocalizationManager.Instance["Settings_UpdateCurrentVersion"],
            AppUpdater.GetCurrentVersion());
    }

    private void CheckForUpdatesOnStartupCheck_Changed(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isInitializing) return;
        AppSettings.Instance.CheckForUpdatesOnStartup = CheckForUpdatesOnStartupCheck.IsChecked == true;
    }

    private async void CheckForUpdatesButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        CheckForUpdatesButton.IsEnabled = false;
        InstallUpdateButton.IsVisible = false;
        UpdateStatusText.Text = Localization.LocalizationManager.Instance["Settings_UpdateChecking"];

        try
        {
            var update = await AppUpdater.CheckForUpdateAsync();

            if (update is null)
            {
                _pendingUpdate = null;
                UpdateStatusText.Text = Localization.LocalizationManager.Instance["Settings_UpdateUpToDate"];
            }
            else
            {
                _pendingUpdate = update;
                UpdateStatusText.Text = string.Format(
                    Localization.LocalizationManager.Instance["Settings_UpdateAvailableMessage"],
                    update.CurrentVersion, update.NewVersion);

                if (!string.IsNullOrEmpty(update.DownloadUrl))
                {
                    InstallUpdateButton.IsVisible = true;
                }
                else
                {
                    AppUpdater.OpenReleasePage(update.ReleaseUrl);
                }
            }
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Trace.WriteLine($"[SettingsView] Update check network error: {ex.Message}");
            UpdateStatusText.Text = $"✘ {Localization.LocalizationManager.Instance["Settings_UpdateNetworkError"]}";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[SettingsView] Update check failed: {ex.Message}");
            UpdateStatusText.Text = $"✘ {Localization.LocalizationManager.Instance["Settings_UpdateNetworkError"]}";
        }
        finally
        {
            CheckForUpdatesButton.IsEnabled = true;
        }
    }

    private async void InstallUpdateButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_pendingUpdate?.DownloadUrl is null) return;

        InstallUpdateButton.IsEnabled = false;
        CheckForUpdatesButton.IsEnabled = false;
        CancelUpdateButton.IsVisible = true;
        UpdateProgressBar.IsVisible = true;
        UpdateProgressBar.Value = 0;

        _updateCts?.Dispose();
        _updateCts = new CancellationTokenSource();

        try
        {
            UpdateStatusText.Text = Localization.LocalizationManager.Instance["Settings_UpdateDownloading"];
            var progress = new Progress<int>(p =>
            {
                UpdateProgressBar.Value = p;
                UpdateStatusText.Text = string.Format(
                    Localization.LocalizationManager.Instance["Settings_UpdateDownloadingPercent"], p);
            });

            string zipPath = await AppUpdater.DownloadUpdateAsync(
                _pendingUpdate.DownloadUrl, progress, _updateCts.Token);

            UpdateStatusText.Text = Localization.LocalizationManager.Instance["Settings_UpdateInstalling"];

            if (AppUpdater.LaunchUpdaterAndExit(zipPath))
            {
                // Shut down the application so the updater can replace files
                if (Avalonia.Application.Current?.ApplicationLifetime is
                    Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown();
                }
            }
            else
            {
                UpdateStatusText.Text = Localization.LocalizationManager.Instance["Settings_UpdateInstallError"];
            }
        }
        catch (OperationCanceledException)
        {
            UpdateStatusText.Text = Localization.LocalizationManager.Instance["Settings_UpdateCancelled"];
            UpdateProgressBar.IsVisible = false;
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Trace.WriteLine($"[SettingsView] Update download error: {ex.Message}");
            UpdateStatusText.Text = $"✘ {Localization.LocalizationManager.Instance["Settings_UpdateDownloadError"]}";
            UpdateProgressBar.IsVisible = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[SettingsView] Update install error: {ex.Message}");
            UpdateStatusText.Text = $"✘ {Localization.LocalizationManager.Instance["Settings_UpdateInstallError"]}";
            UpdateProgressBar.IsVisible = false;
        }
        finally
        {
            InstallUpdateButton.IsEnabled = true;
            CheckForUpdatesButton.IsEnabled = true;
            CancelUpdateButton.IsVisible = false;
            _updateCts?.Dispose();
            _updateCts = null;
        }
    }

    private void CancelUpdateButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _updateCts?.Cancel();
    }

    private sealed class CoreDisplayItem
    {
        public string StatusIcon { get; set; } = "";
        public string CoreName { get; set; } = "";
        public string DisplayName { get; set; } = "";
    }
}
