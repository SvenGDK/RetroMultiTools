using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Services;
using RetroMultiTools.Utilities.Mame;
using System.Runtime.InteropServices;

namespace RetroMultiTools.Views.Mame;

public partial class MameIntegrationView : UserControl
{
    public MameIntegrationView()
    {
        InitializeComponent();
        LoadMamePath();
    }

    // ── MAME Configuration ─────────────────────────────────────────────

    private void LoadMamePath()
    {
        string path = AppSettings.Instance.MamePath;
        if (!string.IsNullOrEmpty(path))
        {
            string resolved = MameLauncher.ResolveMamePath(path);
            MamePathTextBox.Text = path;
            MameStatusText.Text = File.Exists(resolved)
                ? LocalizationManager.Instance["Settings_MameFound"]
                : LocalizationManager.Instance["Settings_MameFileNotFound"];
        }
        else if (MameLauncher.IsMameAvailable())
        {
            string detected = MameLauncher.GetMameExecutablePath();
            MamePathTextBox.Text = detected;
            MameStatusText.Text = LocalizationManager.Instance["Settings_MameAutoDetected"];
        }
    }

    private async void BrowseMameButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var fileTypes = new List<FilePickerFileType>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            fileTypes.Add(new FilePickerFileType(LocalizationManager.Instance["MameIntegration_MameApplication"])
            {
                Patterns = ["mame", "*.app"],
                AppleUniformTypeIdentifiers = ["com.apple.application-bundle", "public.unix-executable"]
            });
        }
        else
        {
            fileTypes.Add(new FilePickerFileType(LocalizationManager.Instance["MameIntegration_MameExecutable"])
            {
                Patterns = [RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "mame.exe" : "mame"]
            });
        }

        fileTypes.Add(FilePickerFileTypes.All);

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = LocalizationManager.Instance["Settings_SelectMameExecutable"],
            AllowMultiple = false,
            FileTypeFilter = fileTypes
        });

        if (files.Count == 0) return;

        string selectedPath = files[0].Path.LocalPath;

        // On macOS, accept .app bundles and resolve to the executable inside
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            string trimmed = selectedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (trimmed.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
            {
                string resolved = MameLauncher.ResolveMamePath(trimmed);
                if (File.Exists(resolved))
                {
                    AppSettings.Instance.MamePath = trimmed;
                    MamePathTextBox.Text = trimmed;
                    MameStatusText.Text = LocalizationManager.Instance["Settings_MamePathSaved"];
                    return;
                }

                MameStatusText.Text = LocalizationManager.Instance["Settings_MameInvalidBundle"];
                return;
            }
        }

        string exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "mame.exe" : "mame";
        string fileName = Path.GetFileName(selectedPath);

        if (!string.Equals(fileName, exeName, StringComparison.OrdinalIgnoreCase))
        {
            MameStatusText.Text = string.Format(LocalizationManager.Instance["Settings_MameNotSelectedFile"], exeName);
            return;
        }

        AppSettings.Instance.MamePath = selectedPath;
        MamePathTextBox.Text = selectedPath;
        MameStatusText.Text = LocalizationManager.Instance["Settings_MamePathSaved"];
    }

    private void DetectMameButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (MameLauncher.IsMameAvailable())
        {
            string detected = MameLauncher.GetMameExecutablePath();
            AppSettings.Instance.MamePath = detected;
            MamePathTextBox.Text = detected;
            MameStatusText.Text = LocalizationManager.Instance["Settings_MameAutoDetectedSaved"];
        }
        else
        {
            MameStatusText.Text = LocalizationManager.Instance["Settings_MameNotFound"];
        }
    }

    private void DownloadMameButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (MameLauncher.OpenDownloadPage())
        {
            MameStatusText.Text = LocalizationManager.Instance["Settings_MameDownloadPageOpened"];
        }
        else
        {
            MameStatusText.Text = string.Format(LocalizationManager.Instance["Settings_MameCouldNotOpenBrowser"], MameLauncher.GetDownloadUrl());
        }
    }

    private void ClearMameButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        AppSettings.Instance.MamePath = string.Empty;
        MamePathTextBox.Text = string.Empty;
        MameStatusText.Text = LocalizationManager.Instance["Settings_MamePathCleared"];
    }
}
