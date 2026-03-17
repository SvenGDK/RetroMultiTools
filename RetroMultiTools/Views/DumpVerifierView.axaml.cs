using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class DumpVerifierView : UserControl
{
    private static readonly IBrush StatusErrorBrush = new SolidColorBrush(Color.Parse("#F38BA8"));
    private static readonly IBrush StatusSuccessBrush = new SolidColorBrush(Color.Parse("#A6E3A1"));

    public DumpVerifierView()
    {
        InitializeComponent();
    }

    private void ModeRadio_Checked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (InputLabel == null) return;
        if (sender is RadioButton rb && rb.IsChecked != true) return;

        bool isBatch = sender == BatchModeRadio;
        InputLabel.Text = isBatch
            ? LocalizationManager.Instance["Common_RomDirectory"]
            : LocalizationManager.Instance["Common_RomFile"];
        InputPathTextBox.Watermark = isBatch
            ? LocalizationManager.Instance["Common_SelectRomDirectory"]
            : LocalizationManager.Instance["Common_SelectRomFile"];
        InputPathTextBox.Text = string.Empty;
        StatusBorder.IsVisible = false;
        ResultsBorder.IsVisible = false;
        UpdateVerifyButton();
    }

    private async void BrowseInput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        bool isBatch = BatchModeRadio.IsChecked == true;

        if (isBatch)
        {
            var path = await PickFolder("Select ROM Directory");
            if (path != null) InputPathTextBox.Text = path;
        }
        else
        {
            var path = await PickFile("Select ROM File",
            [
                new FilePickerFileType("ROM Files")
                {
                    Patterns = [ "*.nes","*.smc","*.sfc","*.z64","*.n64","*.v64",
                                       "*.gb","*.gbc","*.gba","*.vb","*.vboy",
                                       "*.sms","*.md","*.gen",
                                       "*.bin","*.32x","*.gg","*.a26","*.a52","*.a78",
                                       "*.j64","*.jag","*.lnx","*.lyx",
                                       "*.pce","*.tg16",
                                       "*.ngp","*.ngc",
                                       "*.col","*.cv","*.int",
                                       "*.mx1","*.mx2",
                                       "*.dsk","*.cdt","*.sna",
                                       "*.tap",
                                       "*.mo5","*.k7","*.fd",
                                       "*.sv","*.ccc",
                                       "*.iso","*.cue","*.3do",
                                       "*.cdi","*.gdi",
                                       "*.chd","*.rvz","*.gcm",
                                       "*.atr","*.xex","*.car","*.cas",
                                       "*.d88","*.t88",
                                       "*.ndd",
                                       "*.nds",
                                       "*.3ds","*.cia",
                                       "*.neo",
                                       "*.chf",
                                       "*.tgc",
                                       "*.mtx","*.run" ]
                },
                FilePickerFileTypes.All
            ]);
            if (path != null) InputPathTextBox.Text = path;
        }

        UpdateVerifyButton();
    }

    private void UpdateVerifyButton()
    {
        VerifyButton.IsEnabled = !string.IsNullOrEmpty(InputPathTextBox.Text);
    }

    private async void VerifyButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string input = InputPathTextBox.Text ?? "";
        if (string.IsNullOrEmpty(input))
        {
            ShowStatus("Please select a ROM file or directory.", isError: true);
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
                var results = await DumpVerifier.VerifyDirectoryAsync(input, progress);
                int good = results.Count(r => r.IsGoodDump);
                int bad = results.Count - good;

                ShowStatus($"✔ Verification complete!\n{good} good dumps, {bad} with potential issues.", isError: false);

                var sb = new System.Text.StringBuilder();
                foreach (var r in results)
                {
                    string icon = r.IsGoodDump ? "✔" : "⚠";
                    sb.AppendLine($"{icon} {r.FileName} [{r.System}]");
                    sb.AppendLine($"   {r.Status}");
                    foreach (var issue in r.Issues)
                        sb.AppendLine($"   • {issue}");
                }
                ResultsText.Text = sb.ToString();
                ResultsBorder.IsVisible = true;
            }
            else
            {
                var result = await DumpVerifier.VerifyAsync(input, progress);

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"File:   {result.FileName}");
                sb.AppendLine($"System: {result.System}");
                sb.AppendLine($"Size:   {FileUtils.FormatFileSize(result.FileSize)}");
                sb.AppendLine($"Status: {result.Status}");

                if (result.Issues.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Issues found:");
                    foreach (var issue in result.Issues)
                        sb.AppendLine($"  • {issue}");
                }

                ShowStatus(result.IsGoodDump
                    ? $"✔ {result.FileName} appears to be a good dump!"
                    : $"⚠ {result.FileName} has potential issues.", isError: !result.IsGoodDump);

                ResultsText.Text = sb.ToString();
                ResultsBorder.IsVisible = true;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowStatus($"✘ Error: {ex.Message}", isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            VerifyButton.IsEnabled = true;
        }
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
