using System.Text;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Utilities;
using RetroMultiTools.Utilities.DiscTools;

namespace RetroMultiTools.Views.DiscTools;

public partial class DiscToolsView : UserControl
{
    private static readonly IBrush StatusErrorBrush = new SolidColorBrush(Color.Parse("#F38BA8"));
    private static readonly IBrush StatusSuccessBrush = new SolidColorBrush(Color.Parse("#A6E3A1"));

    private CancellationTokenSource? _cts;

    private enum Operation { BurnImage, BurnFiles, ImageFromDisc, ImageFromFiles }

    private Operation CurrentOperation
    {
        get
        {
            if (BurnFilesRadio.IsChecked == true) return Operation.BurnFiles;
            if (ImageFromDiscRadio.IsChecked == true) return Operation.ImageFromDisc;
            if (ImageFromFilesRadio.IsChecked == true) return Operation.ImageFromFiles;
            return Operation.BurnImage;
        }
    }

    public DiscToolsView()
    {
        InitializeComponent();
        DriveCombo.SelectionChanged += (_, _) => UpdateActionButton();
        InputPathTextBox.TextChanged += (_, _) => UpdateActionButton();
        OutputPathTextBox.TextChanged += (_, _) => UpdateActionButton();
        Loaded += async (_, _) => await RefreshDrivesAsync();
        DragDropHelper.EnableFileDrop(InputPathTextBox, _ => UpdateActionButton(), acceptDirectories: true);
    }

    // ── Operation switching ─────────────────────────────────────────────

    private void OperationRadio_Checked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (InputLabel == null) return; // guard during init
        if (sender is not RadioButton rb || rb.IsChecked != true) return;

        // Derive operation from sender to avoid reading stale RadioButton states
        // (the previously-checked RadioButton may not yet be unchecked when this fires)
        var op = sender == BurnFilesRadio ? Operation.BurnFiles
               : sender == ImageFromDiscRadio ? Operation.ImageFromDisc
               : sender == ImageFromFilesRadio ? Operation.ImageFromFiles
               : Operation.BurnImage;

        RefreshLayout(op);
    }

    private void RefreshLayout(Operation? overrideOp = null)
    {
        var loc = LocalizationManager.Instance;
        var op = overrideOp ?? CurrentOperation;

        // Reset
        InputPathTextBox.Text = string.Empty;
        OutputPathTextBox.Text = string.Empty;
        StatusBorder.IsVisible = false;

        switch (op)
        {
            case Operation.BurnImage:
                DrivePanel.IsVisible = true;
                InputPanel.IsVisible = true;
                InputLabel.Text = loc["DiscTools_ImageFile"];
                InputPathTextBox.Watermark = loc["DiscTools_SelectImageFile"];
                BrowseFolderButton.IsVisible = false;
                OutputPanel.IsVisible = false;
                VolumeLabelPanel.IsVisible = false;
                SpeedPanel.IsVisible = true;
                VerifyPanel.IsVisible = true;
                ActionButton.Content = loc["DiscTools_Burn"];
                break;

            case Operation.BurnFiles:
                DrivePanel.IsVisible = true;
                InputPanel.IsVisible = true;
                InputLabel.Text = loc["DiscTools_FilesOrFolders"];
                InputPathTextBox.Watermark = loc["DiscTools_SelectFilesOrFolders"];
                BrowseFolderButton.IsVisible = true;
                OutputPanel.IsVisible = false;
                VolumeLabelPanel.IsVisible = true;
                SpeedPanel.IsVisible = true;
                VerifyPanel.IsVisible = false;
                ActionButton.Content = loc["DiscTools_Burn"];
                break;

            case Operation.ImageFromDisc:
                DrivePanel.IsVisible = true;
                InputPanel.IsVisible = false;
                OutputPanel.IsVisible = true;
                OutputLabel.Text = loc["DiscTools_OutputFile"];
                OutputPathTextBox.Watermark = loc["DiscTools_SelectOutputImage"];
                VolumeLabelPanel.IsVisible = false;
                SpeedPanel.IsVisible = false;
                VerifyPanel.IsVisible = false;
                ActionButton.Content = loc["DiscTools_CreateImage"];
                break;

            case Operation.ImageFromFiles:
                DrivePanel.IsVisible = false;
                InputPanel.IsVisible = true;
                InputLabel.Text = loc["DiscTools_FilesOrFolders"];
                InputPathTextBox.Watermark = loc["DiscTools_SelectFilesOrFolders"];
                BrowseFolderButton.IsVisible = true;
                OutputPanel.IsVisible = true;
                OutputLabel.Text = loc["DiscTools_OutputFile"];
                OutputPathTextBox.Watermark = loc["DiscTools_SelectOutputImage"];
                VolumeLabelPanel.IsVisible = true;
                SpeedPanel.IsVisible = false;
                VerifyPanel.IsVisible = false;
                ActionButton.Content = loc["DiscTools_CreateImage"];
                break;
        }

        UpdateActionButton();
    }

    // ── Drive Detection ─────────────────────────────────────────────────

    private async void RefreshDrives_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RefreshDrivesAsync();
    }

    private async Task RefreshDrivesAsync()
    {
        var progress = new Progress<string>(msg => ProgressText.Text = msg);
        ProgressPanel.IsVisible = true;

        try
        {
            var drives = await DiscBurner.DetectDrivesAsync(progress);
            DriveCombo.Items.Clear();
            foreach (var drive in drives)
            {
                DriveCombo.Items.Add(new ComboBoxItem
                {
                    Content = drive.DisplayName,
                    Tag = drive.DeviceId
                });
            }

            if (drives.Count > 0)
                DriveCombo.SelectedIndex = 0;
            else
                ShowStatus(LocalizationManager.Instance["DiscTools_NoDrivesFound"], isError: false);
        }
        catch (Exception ex)
        {
            ShowStatus(string.Format(
                LocalizationManager.Instance["DiscTools_DriveDetectError"], ex.Message), isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
        }

        UpdateActionButton();
    }

    // ── Browsing ────────────────────────────────────────────────────────

    private async void BrowseInput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        var op = CurrentOperation;

        switch (op)
        {
            case Operation.BurnImage:
            {
                var path = await PickFile(loc["DiscTools_SelectImageFile"],
                [
                    new FilePickerFileType(loc["DiscTools_ImageFiles"])
                    {
                        Patterns = DiscBurner.BurnableImageFilterPatterns
                    },
                    FilePickerFileTypes.All
                ]);
                if (path != null)
                    InputPathTextBox.Text = path;
                break;
            }

            case Operation.BurnFiles:
            case Operation.ImageFromFiles:
            {
                var paths = await PickFiles(loc["DiscTools_SelectFilesOrFolders"]);
                if (paths != null && paths.Length > 0)
                    InputPathTextBox.Text = string.Join("; ", paths);
                break;
            }
        }

        UpdateActionButton();
    }

    private async void BrowseInputFolder_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var folder = await PickFolder(LocalizationManager.Instance["DiscTools_SelectFolder"]);
        if (folder != null)
        {
            string existing = InputPathTextBox.Text ?? "";
            InputPathTextBox.Text = string.IsNullOrWhiteSpace(existing)
                ? folder
                : existing + "; " + folder;
        }

        UpdateActionButton();
    }

    private async void BrowseOutput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        var path = await PickSaveFile(loc["DiscTools_SelectOutputImage"],
        [
            new FilePickerFileType(loc["DiscTools_IsoImageType"]) { Patterns = ["*.iso"] },
            new FilePickerFileType(loc["DiscTools_BinCueImageType"]) { Patterns = ["*.bin"] },
            FilePickerFileTypes.All
        ]);

        if (path != null)
            OutputPathTextBox.Text = path;

        UpdateActionButton();
    }

    private void UpdateActionButton()
    {
        var op = CurrentOperation;
        bool hasInput = !string.IsNullOrEmpty(InputPathTextBox.Text);
        bool hasOutput = !string.IsNullOrEmpty(OutputPathTextBox.Text);
        bool hasDrive = DriveCombo.SelectedItem != null;

        ActionButton.IsEnabled = op switch
        {
            Operation.BurnImage => hasInput && hasDrive,
            Operation.BurnFiles => hasInput && hasDrive,
            Operation.ImageFromDisc => hasOutput && hasDrive,
            Operation.ImageFromFiles => hasInput && hasOutput,
            _ => false
        };
    }

    // ── Action ──────────────────────────────────────────────────────────

    private async void ActionButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var op = CurrentOperation;
        string input = InputPathTextBox.Text ?? "";
        string output = OutputPathTextBox.Text ?? "";
        string deviceId = (DriveCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
        string volumeLabel = SanitizeVolumeLabel(VolumeLabelTextBox.Text ?? "RETRO_DISC");
        int speed = GetSelectedSpeed();
        bool verify = VerifyCheckBox.IsChecked == true;

        ActionButton.IsEnabled = false;
        CancelButton.IsVisible = true;
        ProgressPanel.IsVisible = true;
        StatusBorder.IsVisible = false;

        _cts = new CancellationTokenSource();

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);
            DiscOperationResult result;

            switch (op)
            {
                case Operation.BurnImage:
                    result = await DiscBurner.BurnImageAsync(input, deviceId, speed, verify, progress, _cts.Token);
                    break;
                case Operation.BurnFiles:
                {
                    var paths = ParseInputPaths(input);
                    result = await DiscBurner.BurnFilesAsync(paths, deviceId, volumeLabel, speed, progress, _cts.Token);
                    break;
                }
                case Operation.ImageFromDisc:
                    result = await DiscBurner.CreateImageFromDiscAsync(deviceId, output, progress, _cts.Token);
                    break;
                case Operation.ImageFromFiles:
                {
                    var paths = ParseInputPaths(input);
                    result = await DiscBurner.CreateImageFromFilesAsync(paths, output, volumeLabel, progress, _cts.Token);
                    break;
                }
                default:
                    result = DiscOperationResult.Failure("Unknown operation.");
                    break;
            }

            ShowStatus(result.Message, isError: !result.IsSuccess);
        }
        catch (OperationCanceledException)
        {
            ShowStatus(LocalizationManager.Instance["DiscTools_OperationCancelled"], isError: false);
        }
        catch (Exception ex)
        {
            ShowStatus(string.Format(LocalizationManager.Instance["Common_ErrorFormat"], ex.Message), isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            CancelButton.IsVisible = false;
            ActionButton.IsEnabled = true;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _cts?.Cancel();
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private int GetSelectedSpeed()
    {
        if (SpeedCombo.SelectedItem is ComboBoxItem item && item.Tag is string tagStr)
            return int.TryParse(tagStr, out int s) ? s : 0;
        return 0;
    }

    private static string[] ParseInputPaths(string input)
    {
        return input.Split("; ", StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToArray();
    }

    /// <summary>
    /// Sanitizes a volume label for ISO 9660 compatibility:
    /// uppercase A-Z, digits, underscore only, max 32 characters.
    /// </summary>
    private static string SanitizeVolumeLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return "RETRO_DISC";

        var sb = new StringBuilder(Math.Min(label.Length, 32));
        foreach (char c in label)
        {
            if (sb.Length >= 32) break;
            if (char.IsAsciiLetterOrDigit(c) || c == '_')
                sb.Append(char.ToUpperInvariant(c));
            else if (c == ' ' || c == '-')
                sb.Append('_');
        }

        return sb.Length > 0 ? sb.ToString() : "RETRO_DISC";
    }

    private void ShowStatus(string message, bool isError)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError ? StatusErrorBrush : StatusSuccessBrush;
        StatusBorder.IsVisible = true;
    }

    private async Task<string?> PickFile(string title, FilePickerFileType[] filters)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = filters
        });

        return files.Count > 0 ? Uri.UnescapeDataString(files[0].Path.LocalPath) : null;
    }

    private async Task<string[]?> PickFiles(string title)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = true,
            FileTypeFilter = [FilePickerFileTypes.All]
        });

        return files.Count > 0
            ? files.Select(f => Uri.UnescapeDataString(f.Path.LocalPath)).ToArray()
            : null;
    }

    private async Task<string?> PickSaveFile(string title, FilePickerFileType[] filters)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return null;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            FileTypeChoices = filters
        });

        return file != null ? Uri.UnescapeDataString(file.Path.LocalPath) : null;
    }

    private async Task<string?> PickFolder(string title)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return null;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return folders.Count > 0 ? Uri.UnescapeDataString(folders[0].Path.LocalPath) : null;
    }
}
