using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Utilities.Mame;

namespace RetroMultiTools.Views.Mame;

public partial class MameChdVerifierView : UserControl
{
    private static readonly IBrush StatusErrorBrush = new SolidColorBrush(Color.Parse("#F38BA8"));
    private static readonly IBrush StatusSuccessBrush = new SolidColorBrush(Color.Parse("#A6E3A1"));
    public MameChdVerifierView()
    {
        InitializeComponent();
    }

    private void ModeRadio_Checked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (InputLabel == null) return;
        if (sender is RadioButton rb && rb.IsChecked != true) return;

        bool isBatch = sender == BatchModeRadio;
        var loc = LocalizationManager.Instance;
        InputLabel.Text = isBatch ? loc["MameChd_ChdDirectory"] : loc["MameChd_ChdFileLabel"];
        InputTextBox.Watermark = isBatch ? loc["MameChd_SelectChdDir"] : loc["MameChd_SelectChdFile"];
        InputTextBox.Text = string.Empty;
        StatusBorder.IsVisible = false;
        ResultsBorder.IsVisible = false;
        VerifyButton.IsEnabled = false;
    }

    private async void BrowseInput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        bool isBatch = BatchModeRadio.IsChecked == true;

        if (isBatch)
        {
            var path = await PickFolder(LocalizationManager.Instance["MameChd_SelectChdDirTitle"]);
            if (path != null) InputTextBox.Text = path;
        }
        else
        {
            var path = await PickFile(LocalizationManager.Instance["MameChd_SelectChdFileTitle"],
            [
                new FilePickerFileType(LocalizationManager.Instance["MameChd_ChdFileType"]) { Patterns = ["*.chd"] },
                FilePickerFileTypes.All
            ]);
            if (path != null) InputTextBox.Text = path;
        }

        VerifyButton.IsEnabled = !string.IsNullOrEmpty(InputTextBox.Text);
    }

    private async void VerifyButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        string input = InputTextBox.Text ?? "";
        if (string.IsNullOrEmpty(input))
        {
            ShowStatus(loc["MameChd_SelectInput"], isError: true);
            return;
        }

        VerifyButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        StatusBorder.IsVisible = false;
        ResultsBorder.IsVisible = false;

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);
            bool isBatch = BatchModeRadio.IsChecked == true;

            if (isBatch)
            {
                var batchResult = await MameChdVerifier.VerifyDirectoryAsync(input, progress);
                ShowStatus(string.Format(loc["MameChd_VerificationComplete"], batchResult.Summary), isError: false);

                var lines = new System.Text.StringBuilder();
                foreach (var r in batchResult.Results)
                {
                    string icon = r.IsValid ? "✔" : "✘";
                    lines.AppendLine($"{icon} {r.FileName}");

                    if (r.IsValid)
                    {
                        lines.AppendLine(string.Format(loc["MameChd_BatchVersionComp"], r.Version, r.Compression));
                        lines.AppendLine(string.Format(loc["MameChd_BatchSizeInfo"], FormatSize(r.LogicalSize), FormatSize(r.HunkSize)));
                        if (!string.IsNullOrEmpty(r.SHA1))
                            lines.AppendLine(string.Format(loc["MameChd_BatchSha1"], r.SHA1));
                    }
                    else
                    {
                        lines.AppendLine(string.Format(loc["MameChd_BatchError"], r.Error));
                    }
                    lines.AppendLine();
                }
                ResultsText.Text = lines.ToString();
                ResultsBorder.IsVisible = true;
            }
            else
            {
                var result = await MameChdVerifier.VerifyAsync(input, progress);

                if (result.IsValid)
                {
                    string message = loc["MameChd_ValidFile"] + "\n\n" +
                                     string.Format(loc["MameChd_VersionInfo"], result.Version) + "\n" +
                                     string.Format(loc["MameChd_CompressionInfo"], result.Compression) + "\n" +
                                     string.Format(loc["MameChd_LogicalSizeInfo"], FormatSize(result.LogicalSize)) + "\n" +
                                     string.Format(loc["MameChd_HunkSizeInfo"], FormatSize(result.HunkSize)) + "\n" +
                                     string.Format(loc["MameChd_FileSizeInfo"], FormatSize(result.FileSize)) + "\n";

                    if (result.UnitSize > 0)
                        message += string.Format(loc["MameChd_UnitSizeInfo"], FormatSize(result.UnitSize)) + "\n";

                    if (!string.IsNullOrEmpty(result.SHA1))
                        message += "\n" + string.Format(loc["MameChd_Sha1Info"], result.SHA1);
                    if (!string.IsNullOrEmpty(result.RawSHA1) && result.RawSHA1 != new string('0', 40))
                        message += "\n" + string.Format(loc["MameChd_RawSha1Info"], result.RawSHA1);
                    if (result.HasParent)
                        message += "\n" + string.Format(loc["MameChd_ParentSha1Info"], result.ParentSHA1);

                    ShowStatus(message, isError: false);
                }
                else
                {
                    ShowStatus(string.Format(loc["MameChd_InvalidChd"], result.Error), isError: true);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            ShowStatus(string.Format(loc["Common_ErrorFormat"], ex.Message), isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            VerifyButton.IsEnabled = true;
        }
    }

    private static string FormatSize(long bytes) => ChdConvertResult.FormatSize(bytes);

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
