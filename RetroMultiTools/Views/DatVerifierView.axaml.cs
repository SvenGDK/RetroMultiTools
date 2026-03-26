using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class DatVerifierView : UserControl
{
    private static readonly IBrush StatusErrorBrush = new SolidColorBrush(Color.Parse("#F38BA8"));
    private static readonly IBrush StatusSuccessBrush = new SolidColorBrush(Color.Parse("#A6E3A1"));

    private List<DatEntry>? _datEntries;

    public DatVerifierView()
    {
        InitializeComponent();
    }

    private void ModeRadio_Checked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (RomInputLabel == null) return;
        if (sender is RadioButton rb && rb.IsChecked != true) return;

        bool isBatch = sender == BatchModeRadio;
        var loc = LocalizationManager.Instance;
        RomInputLabel.Text = isBatch ? loc["Common_RomDirectory"] : loc["Common_RomFile"];
        RomInputTextBox.Watermark = isBatch ? loc["Common_SelectRomDirectory"] : loc["Common_SelectRomFile"];
        RomInputTextBox.Text = string.Empty;
        StatusBorder.IsVisible = false;
        ResultsBorder.IsVisible = false;
        UpdateVerifyButton();
    }

    private async void BrowseDat_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        var path = await PickFile(loc["DatVerifier_BrowseDatTitle"],
        [
            new FilePickerFileType("DAT Files") { Patterns = ["*.dat", "*.xml"] },
            FilePickerFileTypes.All
        ]);
        if (path == null) return;

        DatFileTextBox.Text = path;

        try
        {
            _datEntries = DatVerifier.LoadDatFile(path);
            DatInfoText.Text = string.Format(loc["DatVerifier_LoadedEntries"], _datEntries.Count);
            DatInfoPanel.IsVisible = true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            _datEntries = null;
            DatInfoText.Text = string.Format(loc["DatVerifier_LoadError"], ex.Message);
            DatInfoPanel.IsVisible = true;
        }

        UpdateVerifyButton();
    }

    private async void BrowseRom_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        bool isBatch = BatchModeRadio.IsChecked == true;
        var loc = LocalizationManager.Instance;

        if (isBatch)
        {
            var path = await PickFolder(loc["DatVerifier_BrowseFolderTitle"]);
            if (path != null) RomInputTextBox.Text = path;
        }
        else
        {
            var path = await PickFile(loc["DatVerifier_BrowseRomTitle"],
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
            if (path != null) RomInputTextBox.Text = path;
        }

        UpdateVerifyButton();
    }

    private void UpdateVerifyButton()
    {
        VerifyButton.IsEnabled = !string.IsNullOrEmpty(DatFileTextBox.Text) &&
                                 !string.IsNullOrEmpty(RomInputTextBox.Text) &&
                                 _datEntries != null;
    }

    private async void VerifyButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        if (_datEntries == null)
        {
            ShowStatus(loc["DatVerifier_LoadDatFirst"], isError: true);
            return;
        }

        string romInput = RomInputTextBox.Text ?? "";
        if (string.IsNullOrEmpty(romInput))
        {
            ShowStatus(loc["DatVerifier_SelectInput"], isError: true);
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
                var batchResult = await DatVerifier.VerifyDirectoryAsync(romInput, _datEntries, progress);
                ShowStatus(string.Format(loc["DatVerifier_VerificationComplete"], batchResult.Summary), isError: false);

                var lines = new System.Text.StringBuilder();
                foreach (var r in batchResult.Results)
                {
                    string icon = r.IsVerified ? "✔" : "✘";
                    lines.AppendLine($"{icon} {r.FileName}");
                    if (r.IsVerified)
                        lines.AppendLine($"   → {r.DatGameName}");
                    lines.AppendLine($"   CRC32: {r.CRC32}");
                }
                ResultsText.Text = lines.ToString();
                ResultsBorder.IsVisible = true;
            }
            else
            {
                var result = await DatVerifier.VerifyRomAsync(romInput, _datEntries, progress);

                string message;
                if (result.IsVerified)
                {
                    message = string.Format(loc["DatVerifier_RomVerified"],
                        result.DatGameName, result.DatRomName, result.CRC32, result.SHA1);
                }
                else
                {
                    message = string.Format(loc["DatVerifier_RomNotFound"],
                        result.CRC32, result.MD5, result.SHA1);
                }

                ShowStatus(message, isError: !result.IsVerified);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            ShowStatus(string.Format(loc["Common_ErrorFormat"], ex.Message), isError: true);
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
