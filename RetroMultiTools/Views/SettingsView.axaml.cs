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
            RetroArchPathTextBox.Text = path;
            RetroArchStatusText.Text = File.Exists(path) ? "✔ RetroArch found." : "✘ File not found at configured path.";
        }
        else if (RetroArchLauncher.IsRetroArchAvailable())
        {
            string detected = RetroArchLauncher.GetRetroArchExecutablePath();
            RetroArchPathTextBox.Text = detected;
            RetroArchStatusText.Text = "✔ RetroArch auto-detected.";
        }
    }

    private async void BrowseRetroArchButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select RetroArch Executable",
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
        string exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "retroarch.exe" : "retroarch";
        string fileName = Path.GetFileName(selectedPath);

        if (!string.Equals(fileName, exeName, StringComparison.OrdinalIgnoreCase))
        {
            RetroArchStatusText.Text = $"✘ Selected file is not '{exeName}'.";
            return;
        }

        AppSettings.Instance.RetroArchPath = selectedPath;
        RetroArchPathTextBox.Text = selectedPath;
        RetroArchStatusText.Text = "✔ RetroArch path saved.";
    }

    private void DetectRetroArchButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (RetroArchLauncher.IsRetroArchAvailable())
        {
            string detected = RetroArchLauncher.GetRetroArchExecutablePath();
            AppSettings.Instance.RetroArchPath = detected;
            RetroArchPathTextBox.Text = detected;
            RetroArchStatusText.Text = "✔ RetroArch auto-detected and saved.";
        }
        else
        {
            RetroArchStatusText.Text = "✘ RetroArch not found. Please browse manually or download it.";
        }
    }

    private void DownloadRetroArchButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (RetroArchLauncher.OpenDownloadPage())
        {
            RetroArchStatusText.Text = "RetroArch download page opened. After installing, use Auto-Detect or Browse to configure the path.";
        }
        else
        {
            RetroArchStatusText.Text = $"Could not open browser. Please visit: {RetroArchLauncher.GetDownloadUrl()}";
        }
    }

    private void ClearRetroArchButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        AppSettings.Instance.RetroArchPath = string.Empty;
        RetroArchPathTextBox.Text = string.Empty;
        RetroArchStatusText.Text = "RetroArch path cleared.";
    }

    private void CheckCoresButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!RetroArchLauncher.IsRetroArchAvailable())
        {
            CoreDownloadStatusText.Text = "✘ RetroArch not found. Configure the path first.";
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

        CoreDownloadStatusText.Text = $"Found {cores.Count} core(s): {installed} installed, {missing} missing.";
    }

    private async void DownloadMissingCoresButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!RetroArchLauncher.IsRetroArchAvailable())
        {
            CoreDownloadStatusText.Text = "✘ RetroArch not found. Configure the path first.";
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

            CoreDownloadStatusText.Text = $"✔ Download complete. {downloaded} installed, {failed} failed.";

            // Refresh the core list
            CheckCoresButton_Click(null, e);
        }
        catch (OperationCanceledException)
        {
            CoreDownloadStatusText.Text = "Download cancelled.";
        }
        catch (HttpRequestException ex)
        {
            CoreDownloadStatusText.Text = $"✘ Network error: {ex.Message}";
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

    private sealed class CoreDisplayItem
    {
        public string StatusIcon { get; set; } = "";
        public string CoreName { get; set; } = "";
        public string DisplayName { get; set; } = "";
    }
}
