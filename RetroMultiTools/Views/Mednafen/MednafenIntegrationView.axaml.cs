using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Services;
using RetroMultiTools.Utilities.Mednafen;
using System.Runtime.InteropServices;

namespace RetroMultiTools.Views.Mednafen;

public partial class MednafenIntegrationView : UserControl
{
    public MednafenIntegrationView()
    {
        InitializeComponent();
        LoadMednafenPath();
    }

    // ── Mednafen Configuration ──────────────────────────────────────────

    private void LoadMednafenPath()
    {
        string path = AppSettings.Instance.MednafenPath;
        if (!string.IsNullOrEmpty(path))
        {
            string resolved = MednafenLauncher.ResolveMednafenPath(path);
            MednafenPathTextBox.Text = path;
            MednafenStatusText.Text = File.Exists(resolved)
                ? LocalizationManager.Instance["Settings_MednafenFound"]
                : LocalizationManager.Instance["Settings_MednafenFileNotFound"];
        }
        else if (MednafenLauncher.IsMednafenAvailable())
        {
            string detected = MednafenLauncher.GetMednafenExecutablePath();
            MednafenPathTextBox.Text = detected;
            MednafenStatusText.Text = LocalizationManager.Instance["Settings_MednafenAutoDetected"];
        }
    }

    private async void BrowseMednafenButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var fileTypes = new List<FilePickerFileType>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            fileTypes.Add(new FilePickerFileType("Mednafen Application")
            {
                Patterns = ["mednafen", "*.app"],
                AppleUniformTypeIdentifiers = ["com.apple.application-bundle", "public.unix-executable"]
            });
        }
        else
        {
            fileTypes.Add(new FilePickerFileType("Mednafen Executable")
            {
                Patterns = [RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "mednafen.exe" : "mednafen"]
            });
        }

        fileTypes.Add(FilePickerFileTypes.All);

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = LocalizationManager.Instance["Settings_SelectMednafenExecutable"],
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
                string resolved = MednafenLauncher.ResolveMednafenPath(trimmed);
                if (File.Exists(resolved))
                {
                    AppSettings.Instance.MednafenPath = trimmed;
                    MednafenPathTextBox.Text = trimmed;
                    MednafenStatusText.Text = LocalizationManager.Instance["Settings_MednafenPathSaved"];
                    return;
                }

                MednafenStatusText.Text = LocalizationManager.Instance["Settings_MednafenInvalidBundle"];
                return;
            }
        }

        string exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "mednafen.exe" : "mednafen";
        string fileName = Path.GetFileName(selectedPath);

        if (!string.Equals(fileName, exeName, StringComparison.OrdinalIgnoreCase))
        {
            MednafenStatusText.Text = string.Format(LocalizationManager.Instance["Settings_MednafenNotSelectedFile"], exeName);
            return;
        }

        AppSettings.Instance.MednafenPath = selectedPath;
        MednafenPathTextBox.Text = selectedPath;
        MednafenStatusText.Text = LocalizationManager.Instance["Settings_MednafenPathSaved"];
    }

    private void DetectMednafenButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (MednafenLauncher.IsMednafenAvailable())
        {
            string detected = MednafenLauncher.GetMednafenExecutablePath();
            AppSettings.Instance.MednafenPath = detected;
            MednafenPathTextBox.Text = detected;
            MednafenStatusText.Text = LocalizationManager.Instance["Settings_MednafenAutoDetectedSaved"];
        }
        else
        {
            MednafenStatusText.Text = LocalizationManager.Instance["Settings_MednafenNotFound"];
        }
    }

    private void DownloadMednafenButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (MednafenLauncher.OpenDownloadPage())
        {
            MednafenStatusText.Text = LocalizationManager.Instance["Settings_MednafenDownloadPageOpened"];
        }
        else
        {
            MednafenStatusText.Text = string.Format(LocalizationManager.Instance["Settings_MednafenCouldNotOpenBrowser"], MednafenLauncher.GetDownloadUrl());
        }
    }

    private void ClearMednafenButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        AppSettings.Instance.MednafenPath = string.Empty;
        MednafenPathTextBox.Text = string.Empty;
        MednafenStatusText.Text = LocalizationManager.Instance["Settings_MednafenPathCleared"];
    }
}
