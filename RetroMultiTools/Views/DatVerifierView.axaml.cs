using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
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
        RomInputLabel.Text = isBatch ? "ROM Directory:" : "ROM File:";
        RomInputTextBox.Watermark = isBatch ? "Select a ROM directory..." : "Select a ROM file...";
        RomInputTextBox.Text = string.Empty;
        StatusBorder.IsVisible = false;
        ResultsBorder.IsVisible = false;
        UpdateVerifyButton();
    }

    private async void BrowseDat_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFile("Select DAT File",
        [
            new FilePickerFileType("DAT Files") { Patterns = ["*.dat", "*.xml"] },
            FilePickerFileTypes.All
        ]);
        if (path == null) return;

        DatFileTextBox.Text = path;

        try
        {
            _datEntries = DatVerifier.LoadDatFile(path);
            DatInfoText.Text = $"Loaded {_datEntries.Count} ROM entries from DAT file.";
            DatInfoPanel.IsVisible = true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            _datEntries = null;
            DatInfoText.Text = $"Error loading DAT file: {ex.Message}";
            DatInfoPanel.IsVisible = true;
        }

        UpdateVerifyButton();
    }

    private async void BrowseRom_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        bool isBatch = BatchModeRadio.IsChecked == true;

        if (isBatch)
        {
            var path = await PickFolder("Select ROM Directory");
            if (path != null) RomInputTextBox.Text = path;
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
        if (_datEntries == null)
        {
            ShowStatus("Please load a DAT file first.", isError: true);
            return;
        }

        string romInput = RomInputTextBox.Text ?? "";
        if (string.IsNullOrEmpty(romInput))
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
                var batchResult = await DatVerifier.VerifyDirectoryAsync(romInput, _datEntries, progress);
                ShowStatus($"✔ Verification complete!\n{batchResult.Summary}", isError: false);

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
                    message = $"✔ ROM Verified!\n\n" +
                              $"DAT Game: {result.DatGameName}\n" +
                              $"DAT ROM:  {result.DatRomName}\n" +
                              $"CRC32:    {result.CRC32}\n" +
                              $"SHA-1:    {result.SHA1}";
                }
                else
                {
                    message = $"✘ ROM not found in DAT database.\n\n" +
                              $"CRC32:  {result.CRC32}\n" +
                              $"MD5:    {result.MD5}\n" +
                              $"SHA-1:  {result.SHA1}\n\n" +
                              "This ROM may be modified, a bad dump, or not in this DAT file.";
                }

                ShowStatus(message, isError: !result.IsVerified);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
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
