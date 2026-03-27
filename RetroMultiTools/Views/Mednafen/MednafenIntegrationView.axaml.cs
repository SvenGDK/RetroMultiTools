using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Services;
using RetroMultiTools.Utilities;
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
                Title = LocalizationManager.Instance["Settings_SelectMednafenExecutable"],
                AllowMultiple = false
            });

            if (folders.Count == 0) return;

            selectedPath = Uri.UnescapeDataString(folders[0].Path.LocalPath);

            if (TryAcceptMednafenPath(selectedPath))
                return;

            MednafenStatusText.Text = LocalizationManager.Instance["Settings_MednafenNotFound"];
            return;
        }

        // Non-macOS: standard file picker
        var fileTypes = new List<FilePickerFileType>
        {
            new(LocalizationManager.Instance["MednafenIntegration_MednafenExecutable"])
            {
                Patterns = [RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "mednafen.exe" : "mednafen"]
            },
            FilePickerFileTypes.All
        };

        var pickedFiles = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = LocalizationManager.Instance["Settings_SelectMednafenExecutable"],
            AllowMultiple = false,
            FileTypeFilter = fileTypes
        });

        if (pickedFiles.Count == 0) return;

        selectedPath = Uri.UnescapeDataString(pickedFiles[0].Path.LocalPath);

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

    /// <summary>
    /// Allows the user to paste or type a path and press Enter to validate it.
    /// </summary>
    private void MednafenPathTextBox_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key != Avalonia.Input.Key.Enter) return;

        string text = MednafenPathTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            MednafenStatusText.Text = LocalizationManager.Instance["Settings_MednafenNotFound"];
            return;
        }

        if (TryAcceptMednafenPath(text))
            return;

        MednafenStatusText.Text = LocalizationManager.Instance["Settings_MednafenNotFound"];
    }

    /// <summary>
    /// Validates a candidate path and, when it resolves to a Mednafen executable,
    /// saves it to settings.  Returns true when the path was accepted.
    /// </summary>
    private bool TryAcceptMednafenPath(string candidatePath)
    {
        string trimmed = candidatePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string resolved = MednafenLauncher.ResolveMednafenPath(trimmed);

        if (File.Exists(resolved))
        {
            // On macOS, prefer storing the .app bundle path for a cleaner UX.
            string storedPath = trimmed;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string? bundleRoot = AppBundleHelper.GetAppBundleRoot(resolved);
                if (bundleRoot != null)
                    storedPath = bundleRoot;
            }

            AppSettings.Instance.MednafenPath = storedPath;
            MednafenPathTextBox.Text = storedPath;
            MednafenStatusText.Text = LocalizationManager.Instance["Settings_MednafenPathSaved"];
            return true;
        }

        return false;
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
