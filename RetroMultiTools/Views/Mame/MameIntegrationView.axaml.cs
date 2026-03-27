using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Services;
using RetroMultiTools.Utilities;
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
                Title = LocalizationManager.Instance["Settings_SelectMameExecutable"],
                AllowMultiple = false
            });

            if (folders.Count == 0) return;

            selectedPath = Uri.UnescapeDataString(folders[0].Path.LocalPath);

            if (TryAcceptMamePath(selectedPath))
                return;

            MameStatusText.Text = LocalizationManager.Instance["Settings_MameNotFound"];
            return;
        }

        // Non-macOS: standard file picker
        var fileTypes = new List<FilePickerFileType>
        {
            new(LocalizationManager.Instance["MameIntegration_MameExecutable"])
            {
                Patterns = [RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "mame.exe" : "mame"]
            },
            FilePickerFileTypes.All
        };

        var pickedFiles = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = LocalizationManager.Instance["Settings_SelectMameExecutable"],
            AllowMultiple = false,
            FileTypeFilter = fileTypes
        });

        if (pickedFiles.Count == 0) return;

        selectedPath = Uri.UnescapeDataString(pickedFiles[0].Path.LocalPath);

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

    /// <summary>
    /// Allows the user to paste or type a path and press Enter to validate it.
    /// </summary>
    private void MamePathTextBox_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key != Avalonia.Input.Key.Enter) return;

        string text = MamePathTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            MameStatusText.Text = LocalizationManager.Instance["Settings_MameNotFound"];
            return;
        }

        if (TryAcceptMamePath(text))
            return;

        MameStatusText.Text = LocalizationManager.Instance["Settings_MameNotFound"];
    }

    /// <summary>
    /// Validates a candidate path and, when it resolves to a MAME executable,
    /// saves it to settings.  Returns true when the path was accepted.
    /// </summary>
    private bool TryAcceptMamePath(string candidatePath)
    {
        string trimmed = candidatePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string resolved = MameLauncher.ResolveMamePath(trimmed);

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

            AppSettings.Instance.MamePath = storedPath;
            MamePathTextBox.Text = storedPath;
            MameStatusText.Text = LocalizationManager.Instance["Settings_MamePathSaved"];
            return true;
        }

        return false;
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
