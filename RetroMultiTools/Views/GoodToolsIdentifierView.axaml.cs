using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class GoodToolsIdentifierView : UserControl
{
    private static readonly IBrush StatusErrorBrush = new SolidColorBrush(Color.Parse("#F38BA8"));
    private static readonly IBrush StatusSuccessBrush = new SolidColorBrush(Color.Parse("#A6E3A1"));
    public GoodToolsIdentifierView()
    {
        InitializeComponent();
    }

    private void ModeRadio_Checked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (InputLabel == null) return;
        if (sender is RadioButton rb && rb.IsChecked != true) return;

        bool isBatch = sender == BatchModeRadio;
        InputLabel.Text = isBatch ? LocalizationManager.Instance["GoodTools_RomDirectory"] : LocalizationManager.Instance["GoodTools_RomFile"];
        InputPathTextBox.Watermark = isBatch ? LocalizationManager.Instance["GoodTools_SelectRomDirectory"] : LocalizationManager.Instance["GoodTools_SelectRomFile"];
        InputPathTextBox.Text = string.Empty;
        StatusBorder.IsVisible = false;
        ResultsBorder.IsVisible = false;
        UpdateIdentifyButton();
    }

    private async void BrowseInput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        bool isBatch = BatchModeRadio.IsChecked == true;

        if (isBatch)
        {
            var path = await PickFolder(LocalizationManager.Instance["GoodTools_SelectRomDir"]);
            if (path != null) InputPathTextBox.Text = path;
        }
        else
        {
            var path = await PickFile(LocalizationManager.Instance["GoodTools_SelectRomFileDialog"],
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
                                       "*.chd","*.rvz","*.gcm" ]
                },
                FilePickerFileTypes.All
            ]);
            if (path != null) InputPathTextBox.Text = path;
        }

        UpdateIdentifyButton();
    }

    private void UpdateIdentifyButton()
    {
        IdentifyButton.IsEnabled = !string.IsNullOrEmpty(InputPathTextBox.Text);
    }

    private async void IdentifyButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string input = InputPathTextBox.Text ?? "";
        if (string.IsNullOrEmpty(input))
        {
            ShowStatus(LocalizationManager.Instance["GoodTools_SelectFile"], isError: true);
            return;
        }

        IdentifyButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        StatusBorder.IsVisible = false;
        ResultsBorder.IsVisible = false;

        try
        {
            bool isBatch = BatchModeRadio.IsChecked == true;

            if (isBatch)
            {
                var progress = new Progress<string>(msg => ProgressText.Text = msg);
                var results = await GoodToolsIdentifier.IdentifyDirectoryAsync(input, progress);
                int withCodes = results.Count(r => r.HasCodes);
                int withoutCodes = results.Count - withCodes;

                ShowStatus($"✔ Identification complete!\n{withCodes} ROM(s) with GoodTools codes, {withoutCodes} without.", isError: false);

                var sb = new System.Text.StringBuilder();
                foreach (var r in results)
                {
                    string icon = r.HasCodes ? "✔" : "—";
                    sb.AppendLine($"{icon} {r.FileName}");
                    if (r.HasCodes)
                    {
                        foreach (var code in r.AllCodes)
                        {
                            string bracket = code.Type == GoodToolsCodeType.Standard ? $"[{code.Code}]" : $"({code.Code})";
                            sb.AppendLine($"   {bracket} = {code.Description}");
                        }
                    }
                    else
                    {
                        sb.AppendLine("   No GoodTools codes found.");
                    }
                }
                ResultsText.Text = sb.ToString();
                ResultsBorder.IsVisible = true;
            }
            else
            {
                string fileName = Path.GetFileName(input);
                var result = GoodToolsIdentifier.Identify(fileName);

                if (result.HasCodes)
                {
                    ShowStatus($"✔ GoodTools codes found in: {fileName}", isError: false);
                    ResultsText.Text = result.GetDetailedDescription();
                }
                else
                {
                    ShowStatus($"— No GoodTools codes found in: {fileName}", isError: false);
                    ResultsText.Text = LocalizationManager.Instance["GoodTools_NoCodesFound"];
                }

                ResultsBorder.IsVisible = true;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            ShowStatus($"✘ Error: {ex.Message}", isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            IdentifyButton.IsEnabled = true;
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
