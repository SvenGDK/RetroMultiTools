using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Models;
using RetroMultiTools.Utilities;
using RetroMultiTools.Utilities.RetroArch;
using System.Runtime.InteropServices;

namespace RetroMultiTools.Views.RetroArch;

public partial class RetroArchShortcutView : UserControl
{
    public RetroArchShortcutView()
    {
        InitializeComponent();
        PopulateSystemCombo();
        PopulateCoreCombo();
    }

    private void PopulateSystemCombo()
    {
        foreach (RomSystem system in Enum.GetValues<RomSystem>())
        {
            if (system == RomSystem.Unknown) continue;
            if (RetroArchLauncher.GetCoreName(system) == null) continue;
            SystemCombo.Items.Add(new ComboBoxItem { Content = system.ToString(), Tag = system });
        }
    }

    private void PopulateCoreCombo()
    {
        foreach (var (coreName, displayName) in RetroArchShortcutCreator.GetAvailableCores())
        {
            CoreCombo.Items.Add(new ComboBoxItem { Content = $"{coreName} — {displayName}", Tag = coreName });
        }
    }

    private void SystemCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (SystemCombo.SelectedItem is ComboBoxItem item && item.Tag is RomSystem system)
        {
            string? defaultCore = RetroArchLauncher.GetCoreName(system);
            if (defaultCore != null)
            {
                // Auto-select the matching core
                for (int i = 0; i < CoreCombo.Items.Count; i++)
                {
                    if (CoreCombo.Items[i] is ComboBoxItem ci && ci.Tag is string cn && cn == defaultCore)
                    {
                        CoreCombo.SelectedIndex = i;
                        break;
                    }
                }
            }

            // Auto-fill shortcut name from ROM name if available
            if (string.IsNullOrEmpty(ShortcutNameTextBox.Text) && !string.IsNullOrEmpty(RomPathTextBox.Text))
            {
                ShortcutNameTextBox.Text = Path.GetFileNameWithoutExtension(RomPathTextBox.Text);
            }
        }
    }

    private async void BrowseRomButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = LocalizationManager.Instance["RAShortcut_SelectRom"],
            AllowMultiple = false,
            FileTypeFilter = [FilePickerFileTypes.All]
        });

        if (files.Count == 0) return;

        RomPathTextBox.Text = files[0].Path.LocalPath;

        // Auto-fill shortcut name
        if (string.IsNullOrEmpty(ShortcutNameTextBox.Text))
        {
            ShortcutNameTextBox.Text = Path.GetFileNameWithoutExtension(files[0].Path.LocalPath);
        }
    }

    private async void BrowseOutputDirButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = LocalizationManager.Instance["RAShortcut_SelectOutputDir"],
            AllowMultiple = false
        });

        if (folders.Count > 0)
            OutputDirTextBox.Text = folders[0].Path.LocalPath;
    }

    private async void BrowseIconButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var fileTypes = new List<FilePickerFileType>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            fileTypes.Add(new FilePickerFileType("Icon Files") { Patterns = ["*.ico", "*.exe"] });
        }
        else
        {
            fileTypes.Add(new FilePickerFileType("Image Files") { Patterns = ["*.png", "*.svg", "*.xpm"] });
        }
        fileTypes.Add(FilePickerFileTypes.All);

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = LocalizationManager.Instance["RAShortcut_SelectIcon"],
            AllowMultiple = false,
            FileTypeFilter = fileTypes
        });

        if (files.Count > 0)
            IconPathTextBox.Text = files[0].Path.LocalPath;
    }

    private async void BrowseRetroArchOverrideButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var fileTypes = new List<FilePickerFileType>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            fileTypes.Add(new FilePickerFileType("RetroArch Executable") { Patterns = ["retroarch.exe"] });
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            fileTypes.Add(new FilePickerFileType("RetroArch Application")
            {
                Patterns = ["retroarch", "*.app"],
                AppleUniformTypeIdentifiers = ["com.apple.application-bundle", "public.unix-executable"]
            });
        }
        else
        {
            fileTypes.Add(new FilePickerFileType("RetroArch Executable") { Patterns = ["retroarch"] });
        }
        fileTypes.Add(FilePickerFileTypes.All);

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = LocalizationManager.Instance["Settings_SelectRetroArchExecutable"],
            AllowMultiple = false,
            FileTypeFilter = fileTypes
        });

        if (files.Count > 0)
            RetroArchOverrideTextBox.Text = files[0].Path.LocalPath;
    }

    private void CreateShortcutButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;

        string name = ShortcutNameTextBox.Text?.Trim() ?? "";
        string romPath = RomPathTextBox.Text?.Trim() ?? "";

        RomSystem system = RomSystem.Unknown;
        if (SystemCombo.SelectedItem is ComboBoxItem sysItem && sysItem.Tag is RomSystem s)
            system = s;

        string coreName = "";
        if (CoreCombo.SelectedItem is ComboBoxItem coreItem && coreItem.Tag is string cn)
            coreName = cn;

        var config = new RetroArchShortcutCreator.ShortcutConfig
        {
            Name = name,
            RomPath = romPath,
            System = system,
            CoreName = coreName,
            IconPath = IconPathTextBox.Text?.Trim() ?? "",
            OutputDirectory = OutputDirTextBox.Text?.Trim() ?? "",
            RetroArchPath = RetroArchOverrideTextBox.Text?.Trim() ?? "",
            Fullscreen = FullscreenCheckBox.IsChecked == true,
            ExtraArguments = ExtraArgsTextBox.Text?.Trim() ?? "",
        };

        var result = RetroArchShortcutCreator.CreateShortcut(config);

        StatusText.Text = result.Success
            ? $"✔ {result.Message}"
            : $"✘ {result.Message}";
    }
}
