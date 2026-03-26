using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Services;
using RetroMultiTools.Utilities;
using System.Globalization;

namespace RetroMultiTools.Views;

public partial class SettingsView : UserControl
{
    private bool _isInitializing = true;

    public SettingsView()
    {
        InitializeComponent();
        PopulateLanguageCombo();
        LoadDiscordSetting();
        LoadTraySetting();
        LoadBigPictureSettings();
        LoadGamepadSettings();
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
        BigPictureCardScaleSlider.Value = AppSettings.Instance.BigPictureCardScale;
        BigPictureCardScaleValueText.Text = AppSettings.Instance.BigPictureCardScale.ToString("F1") + "×";
        BigPictureScreensaverSlider.Value = AppSettings.Instance.BigPictureScreensaverTimeout;
        UpdateScreensaverValueText(AppSettings.Instance.BigPictureScreensaverTimeout);
        BigPicturePlayTrackingCheck.IsChecked = AppSettings.Instance.BigPicturePlayTrackingEnabled;
        BigPictureRatingsCheck.IsChecked = AppSettings.Instance.BigPictureRatingsEnabled;
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

    private void BigPictureCardScaleSlider_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isInitializing) return;
        double val = e.NewValue;
        AppSettings.Instance.BigPictureCardScale = val;
        BigPictureCardScaleValueText.Text = val.ToString("F1") + "×";
    }

    private void BigPictureScreensaverSlider_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isInitializing) return;
        int val = (int)e.NewValue;
        AppSettings.Instance.BigPictureScreensaverTimeout = val;
        UpdateScreensaverValueText(val);
    }

    private void UpdateScreensaverValueText(int minutes)
    {
        if (minutes == 0)
            BigPictureScreensaverValueText.Text = LocalizationManager.Instance["Settings_BigPictureScreensaverOff"];
        else
            BigPictureScreensaverValueText.Text = string.Format(LocalizationManager.Instance["Settings_ScreensaverMinutes"], minutes);
    }

    private void BigPicturePlayTrackingCheck_Changed(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isInitializing) return;
        AppSettings.Instance.BigPicturePlayTrackingEnabled = BigPicturePlayTrackingCheck.IsChecked == true;
    }

    private void BigPictureRatingsCheck_Changed(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isInitializing) return;
        AppSettings.Instance.BigPictureRatingsEnabled = BigPictureRatingsCheck.IsChecked == true;
    }

    // ── Gamepad ────────────────────────────────────────────────────────

    private void LoadGamepadSettings()
    {
        GamepadEnabledCheck.IsChecked = AppSettings.Instance.GamepadEnabled;
        GamepadDeadZoneSlider.Value = AppSettings.Instance.GamepadDeadZone;
        GamepadDeadZoneValueText.Text = AppSettings.Instance.GamepadDeadZone.ToString("F2");
    }

    private void GamepadEnabledCheck_Changed(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isInitializing) return;
        AppSettings.Instance.GamepadEnabled = GamepadEnabledCheck.IsChecked == true;
    }

    private void GamepadDeadZoneSlider_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isInitializing) return;
        double val = e.NewValue;
        AppSettings.Instance.GamepadDeadZone = val;
        GamepadDeadZoneValueText.Text = val.ToString("F2");
        GamepadService.Instance.SetDeadZone(val);
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
        ReleaseNotesBorder.IsVisible = false;
        UpdateStatusText.Text = Localization.LocalizationManager.Instance["Settings_UpdateChecking"];

        try
        {
            var update = await AppUpdater.CheckForUpdateAsync();

            if (update is null)
            {
                _pendingUpdate = null;
                UpdateStatusText.Text = Localization.LocalizationManager.Instance["Settings_UpdateUpToDate"];
                ReleaseNotesBorder.IsVisible = false;
            }
            else
            {
                _pendingUpdate = update;
                UpdateStatusText.Text = string.Format(
                    Localization.LocalizationManager.Instance["Settings_UpdateAvailableMessage"],
                    update.CurrentVersion, update.NewVersion);

                // Show release notes as rendered Markdown
                if (!string.IsNullOrWhiteSpace(update.ReleaseNotes))
                {
                    ReleaseNotesViewer.Markdown = update.ReleaseNotes;
                    ReleaseNotesBorder.IsVisible = true;
                }
                else
                {
                    ReleaseNotesBorder.IsVisible = false;
                }

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
            UpdateStatusText.Text = Localization.LocalizationManager.Instance["Settings_UpdateNetworkError"];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[SettingsView] Update check failed: {ex.Message}");
            UpdateStatusText.Text = Localization.LocalizationManager.Instance["Settings_UpdateNetworkError"];
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
                UpdateProgressBar.IsVisible = false;
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
            UpdateStatusText.Text = Localization.LocalizationManager.Instance["Settings_UpdateDownloadError"];
            UpdateProgressBar.IsVisible = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[SettingsView] Update install error: {ex.Message}");
            UpdateStatusText.Text = Localization.LocalizationManager.Instance["Settings_UpdateInstallError"];
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
}
