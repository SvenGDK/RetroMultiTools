using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class ArchiveManagerView : UserControl
{
    private static readonly IBrush StatusErrorBrush = new SolidColorBrush(Color.Parse("#F38BA8"));
    private static readonly IBrush StatusSuccessBrush = new SolidColorBrush(Color.Parse("#A6E3A1"));

    private bool IsExtractMode => ExtractRadio.IsChecked == true;
    private bool IsBatchMode => BatchModeRadio.IsChecked == true;

    public ArchiveManagerView()
    {
        InitializeComponent();
    }

    // ── Operation / Mode switching ──────────────────────────────────────

    private void OperationRadio_Checked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (InputLabel == null) return;
        if (sender is RadioButton rb && rb.IsChecked != true) return;
        RefreshLabels();
    }

    private void ModeRadio_Checked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (InputLabel == null) return;
        if (sender is RadioButton rb && rb.IsChecked != true) return;
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        var loc = LocalizationManager.Instance;
        bool extract = IsExtractMode;
        bool batch = IsBatchMode;

        // Input label
        if (extract)
        {
            InputLabel.Text = batch ? loc["Archive_ArchiveDirectory"] : loc["Archive_ArchiveFile"];
            InputPathTextBox.Watermark = batch ? loc["Archive_SelectArchiveDirectory"] : loc["Archive_SelectArchive"];
            SingleModeRadio.Content = loc["Archive_SingleFile"];
        }
        else
        {
            InputLabel.Text = batch ? loc["Archive_RomDirectory"] : loc["Archive_RomFiles"];
            InputPathTextBox.Watermark = batch ? loc["Archive_SelectRomDirectory"] : loc["Archive_SelectRomFiles"];
            SingleModeRadio.Content = loc["Archive_SingleFile"];
        }

        // Output label
        OutputLabel.Text = extract ? loc["Archive_OutputDir"] : loc["Archive_OutputPath"];

        // Action button
        ActionButton.Content = extract ? loc["Archive_Extract"] : loc["Archive_Create"];

        // Create batch option
        CreateBatchOptionPanel.IsVisible = !extract && batch;

        InputPathTextBox.Text = string.Empty;
        OutputPathTextBox.Text = string.Empty;
        StatusBorder.IsVisible = false;
        ResultsBorder.IsVisible = false;
        UpdateActionButton();
    }

    // ── Browsing ────────────────────────────────────────────────────────

    private async void BrowseInput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        bool batch = IsBatchMode;

        if (batch)
        {
            string title = IsExtractMode ? loc["Archive_SelectArchiveDir"] : loc["Archive_SelectRomDir"];
            var path = await PickFolder(title);
            if (path != null) InputPathTextBox.Text = path;
        }
        else if (IsExtractMode)
        {
            var path = await PickFile(loc["Archive_SelectArchiveFile"],
            [
                new FilePickerFileType("Archives (ZIP, RAR, 7z, GZip)") { Patterns = ["*.zip", "*.rar", "*.7z", "*.gz"] },
                FilePickerFileTypes.All
            ]);
            if (path != null)
            {
                InputPathTextBox.Text = path;
                ShowArchiveContents(path);
            }
        }
        else
        {
            var paths = await PickFiles(loc["Archive_SelectRomFilesTitle"]);
            if (paths != null && paths.Length > 0)
                InputPathTextBox.Text = string.Join("; ", paths);
        }

        UpdateActionButton();
    }

    private void ShowArchiveContents(string archivePath)
    {
        try
        {
            var entries = ArchiveManager.ListEntries(archivePath);
            if (entries.Count == 0)
            {
                ResultsText.Text = LocalizationManager.Instance["Archive_NoRomsFound"];
            }
            else
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine(string.Format(LocalizationManager.Instance["Archive_FoundRoms"], entries.Count));
                foreach (var entry in entries)
                    sb.AppendLine($"  {entry.Summary}");
                ResultsText.Text = sb.ToString();
            }
            ResultsBorder.IsVisible = true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException)
        {
            ResultsText.Text = string.Format(LocalizationManager.Instance["Archive_ReadError"], ex.Message);
            ResultsBorder.IsVisible = true;
        }
    }

    private async void BrowseOutput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;

        if (IsExtractMode || (CreateRadio.IsChecked == true && IsBatchMode))
        {
            var path = await PickFolder(loc["Archive_SelectOutputDir"]);
            if (path != null)
                OutputPathTextBox.Text = path;
        }
        else
        {
            // Create single: pick save file
            var path = await PickSaveFile(loc["Archive_SelectOutputFile"],
            [
                new FilePickerFileType("ZIP Archives") { Patterns = ["*.zip"] },
                FilePickerFileTypes.All
            ]);
            if (path != null)
                OutputPathTextBox.Text = path;
        }

        UpdateActionButton();
    }

    private void UpdateActionButton()
    {
        ActionButton.IsEnabled = !string.IsNullOrEmpty(InputPathTextBox.Text) &&
                                 !string.IsNullOrEmpty(OutputPathTextBox.Text);
    }

    // ── Action ──────────────────────────────────────────────────────────

    private async void ActionButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string input = InputPathTextBox.Text ?? "";
        string output = OutputPathTextBox.Text ?? "";

        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(output))
        {
            ShowStatus(LocalizationManager.Instance["Archive_SelectInputOutput"], isError: true);
            return;
        }

        ActionButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        StatusBorder.IsVisible = false;

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);

            if (IsExtractMode)
            {
                ArchiveOperationResult result;
                if (IsBatchMode)
                    result = await ArchiveManager.ExtractBatchAsync(input, output, progress);
                else
                    result = await ArchiveManager.ExtractAsync(input, output, progress);

                ShowStatus($"{LocalizationManager.Instance["Archive_ExtractComplete"]}\n{result.Summary}", isError: false);
            }
            else
            {
                ArchiveOperationResult result;
                if (IsBatchMode)
                {
                    bool onePerFile = OnePerFileCheckBox.IsChecked == true;
                    result = await ArchiveManager.CreateBatchAsync(input, output, onePerFile, progress);
                }
                else
                {
                    var files = input.Split("; ", StringSplitOptions.RemoveEmptyEntries);
                    result = await ArchiveManager.CreateArchiveAsync(output, files, progress);
                }

                ShowStatus($"{LocalizationManager.Instance["Archive_CreateComplete"]}\n{result.Summary}", isError: false);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException
                                       or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            ShowStatus(string.Format(LocalizationManager.Instance["Common_ErrorFormat"], ex.Message), isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            ActionButton.IsEnabled = true;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────

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

        return files.Count > 0 ? files[0].Path.LocalPath : null;
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
            ? files.Select(f => f.Path.LocalPath).ToArray()
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

        return file?.Path.LocalPath;
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

        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }
}
