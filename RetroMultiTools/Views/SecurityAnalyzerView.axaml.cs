using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class SecurityAnalyzerView : UserControl
{
    private static readonly IBrush StatusErrorBrush = new SolidColorBrush(Color.Parse("#F38BA8"));
    private static readonly IBrush StatusSuccessBrush = new SolidColorBrush(Color.Parse("#A6E3A1"));

    public SecurityAnalyzerView()
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
        UpdateAnalyzeButton();
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

        UpdateAnalyzeButton();
    }

    private void UpdateAnalyzeButton()
    {
        AnalyzeButton.IsEnabled = !string.IsNullOrEmpty(InputPathTextBox.Text);
    }

    private async void AnalyzeButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string input = InputPathTextBox.Text ?? "";
        if (string.IsNullOrEmpty(input))
        {
            ShowStatus("Please select a ROM file or directory.", isError: true);
            return;
        }

        AnalyzeButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        StatusBorder.IsVisible = false;
        ResultsBorder.IsVisible = false;

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);
            bool isBatch = BatchModeRadio.IsChecked == true;

            if (isBatch)
            {
                var results = await SecurityAnalyzer.AnalyzeBatchAsync(input, progress);
                int total = results.Sum(r => r.Features.Count);

                ShowStatus($"✔ Analysis complete!\n{results.Count} ROM(s) analyzed, {total} security feature(s) detected.", isError: false);

                var sb = new System.Text.StringBuilder();
                foreach (var result in results)
                {
                    sb.AppendLine($"═══ {result.FileName} [{result.System}] ═══");
                    if (result.Features.Count == 0)
                    {
                        sb.AppendLine("  No security features detected.");
                    }
                    else
                    {
                        foreach (var feature in result.Features)
                        {
                            sb.AppendLine($"  [{feature.CategoryName}] {feature.Name}");
                            sb.AppendLine($"    {feature.Description}");
                        }
                    }
                    sb.AppendLine();
                }
                ResultsText.Text = sb.ToString();
                ResultsBorder.IsVisible = true;
            }
            else
            {
                var result = await SecurityAnalyzer.AnalyzeAsync(input, progress);

                ShowStatus(result.Features.Count > 0
                    ? $"✔ {result.FileName}: {result.Features.Count} security feature(s) detected."
                    : $"✔ {result.FileName}: No security features detected.", isError: false);

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"File:   {result.FileName}");
                sb.AppendLine($"System: {result.System}");
                sb.AppendLine();

                if (result.Features.Count == 0)
                {
                    sb.AppendLine("No security features detected.");
                }
                else
                {
                    var grouped = result.Features.GroupBy(f => f.Category);
                    foreach (var group in grouped)
                    {
                        sb.AppendLine($"── {group.First().CategoryName} ──");
                        foreach (var feature in group)
                        {
                            sb.AppendLine($"  {feature.Name}");
                            sb.AppendLine($"    {feature.Description}");
                        }
                        sb.AppendLine();
                    }
                }

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
            AnalyzeButton.IsEnabled = true;
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
