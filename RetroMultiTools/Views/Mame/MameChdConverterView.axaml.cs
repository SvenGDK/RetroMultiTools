using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Utilities.Mame;

namespace RetroMultiTools.Views.Mame;

public partial class MameChdConverterView : UserControl
{
    private static readonly IBrush StatusErrorBrush = new SolidColorBrush(Color.Parse("#F38BA8"));
    private static readonly IBrush StatusSuccessBrush = new SolidColorBrush(Color.Parse("#A6E3A1"));

    public MameChdConverterView()
    {
        InitializeComponent();
    }

    private void OperationRadio_Checked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (InputLabel == null) return;
        if (sender is RadioButton rb && rb.IsChecked != true) return;

        UpdateLabels();
        InputTextBox.Text = string.Empty;
        OutputTextBox.Text = string.Empty;
        StatusBorder.IsVisible = false;
        UpdateConvertButton();
    }

    private void ModeRadio_Checked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (InputLabel == null) return;
        if (sender is RadioButton rb && rb.IsChecked != true) return;

        UpdateLabels();
        InputTextBox.Text = string.Empty;
        OutputTextBox.Text = string.Empty;
        StatusBorder.IsVisible = false;
        UpdateConvertButton();
    }

    private void UpdateLabels()
    {
        bool isCompress = CompressRadio.IsChecked == true;
        bool isBatch = BatchModeRadio.IsChecked == true;

        if (isCompress)
        {
            InputLabel.Text = isBatch
                ? LocalizationManager.Instance["MameChdConv_DiscImageDir"]
                : LocalizationManager.Instance["MameChdConv_DiscImageFile"];
            InputTextBox.Watermark = isBatch
                ? LocalizationManager.Instance["MameChdConv_SelectDiscDir"]
                : LocalizationManager.Instance["MameChdConv_SelectDiscFile"];
        }
        else
        {
            InputLabel.Text = isBatch
                ? LocalizationManager.Instance["MameChdConv_ChdDir"]
                : LocalizationManager.Instance["MameChdConv_ChdFile"];
            InputTextBox.Watermark = isBatch
                ? LocalizationManager.Instance["MameChdConv_SelectChdDir"]
                : LocalizationManager.Instance["MameChdConv_SelectChdFile"];
        }

        OutputLabel.Text = isBatch
            ? LocalizationManager.Instance["MameChdConv_OutputDir"]
            : LocalizationManager.Instance["MameChdConv_OutputFile"];
        OutputFormatPanel.IsVisible = !isCompress;
    }

    private async void BrowseInput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        bool isCompress = CompressRadio.IsChecked == true;
        bool isBatch = BatchModeRadio.IsChecked == true;

        if (isBatch)
        {
            var path = await PickFolder(isCompress ? LocalizationManager.Instance["MameChdConv_SelectDiscDirTitle"] : LocalizationManager.Instance["MameChdConv_SelectChdDirTitle"]);
            if (path != null) InputTextBox.Text = path;
        }
        else
        {
            if (isCompress)
            {
                var path = await PickFile(LocalizationManager.Instance["MameChdConv_SelectDiscFileTitle"],
                [
                    new FilePickerFileType(LocalizationManager.Instance["MameChdConv_DiscImageFileType"]) { Patterns = ["*.cue", "*.iso", "*.bin", "*.gdi", "*.cdi", "*.toc"] },
                    FilePickerFileTypes.All
                ]);
                if (path != null)
                {
                    InputTextBox.Text = path;
                    UpdateOutputPath(path, true);
                }
            }
            else
            {
                var path = await PickFile(LocalizationManager.Instance["MameChdConv_SelectChdFileTitle"],
                [
                    new FilePickerFileType(LocalizationManager.Instance["MameChdConv_ChdFileType"]) { Patterns = ["*.chd"] },
                    FilePickerFileTypes.All
                ]);
                if (path != null)
                {
                    InputTextBox.Text = path;
                    UpdateOutputPath(path, false);
                }
            }
        }

        UpdateConvertButton();
    }

    private void UpdateOutputPath(string inputPath, bool isCompress)
    {
        string dir = Path.GetDirectoryName(inputPath) ?? "";
        if (isCompress)
        {
            string outputName = Path.ChangeExtension(Path.GetFileName(inputPath), ".chd");
            OutputTextBox.Text = Path.Combine(dir, outputName);
        }
        else
        {
            string ext = GetSelectedOutputExtension();
            string outputName = Path.ChangeExtension(Path.GetFileName(inputPath), ext);
            OutputTextBox.Text = Path.Combine(dir, outputName);
        }
    }

    private void OutputFormatCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CompressRadio == null || CompressRadio.IsChecked == true) return;
        if (BatchModeRadio.IsChecked == true) return;

        string current = OutputTextBox.Text ?? "";
        if (string.IsNullOrEmpty(current)) return;

        string ext = GetSelectedOutputExtension();
        OutputTextBox.Text = Path.ChangeExtension(current, ext);
    }

    private string GetSelectedOutputExtension() => OutputFormatCombo.SelectedIndex switch
    {
        1 => ".cue",
        2 => ".gdi",
        _ => ".bin"
    };

    private async void BrowseOutput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        bool isBatch = BatchModeRadio.IsChecked == true;

        if (isBatch)
        {
            var path = await PickFolder(LocalizationManager.Instance["MameChdConv_SelectOutputDirTitle"]);
            if (path != null) OutputTextBox.Text = path;
        }
        else
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            bool isCompress = CompressRadio.IsChecked == true;
            string defaultExt = isCompress ? ".chd" : ".bin";
            string suggestedName = Path.GetFileName(OutputTextBox.Text ?? ("output" + defaultExt));

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = loc["MameChdConv_SaveDialogTitle"],
                SuggestedFileName = suggestedName
            });

            if (file != null)
                OutputTextBox.Text = file.Path.LocalPath;
        }

        UpdateConvertButton();
    }

    private void UpdateConvertButton()
    {
        ConvertButton.IsEnabled = !string.IsNullOrEmpty(InputTextBox.Text) &&
                                   !string.IsNullOrEmpty(OutputTextBox.Text);
    }

    private async void ConvertButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        string input = InputTextBox.Text ?? "";
        string output = OutputTextBox.Text ?? "";

        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(output))
        {
            ShowStatus(loc["MameChdConv_SelectInputOutput"], isError: true);
            return;
        }

        ConvertButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        StatusBorder.IsVisible = false;

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);
            bool isCompress = CompressRadio.IsChecked == true;
            bool isBatch = BatchModeRadio.IsChecked == true;

            if (isCompress)
            {
                var options = new ChdCompressOptions { Force = true };

                if (isBatch)
                {
                    var result = await MameChdConverter.CompressBatchAsync(input, output, options, progress);
                    ShowStatus(string.Format(loc["MameChdConv_BatchCompressComplete"], result.Summary), isError: false);
                }
                else
                {
                    var result = await MameChdConverter.CompressAsync(input, output, options, progress);
                    ShowStatus(string.Format(loc["MameChdConv_CompressComplete"], result.Summary, result.OutputPath), isError: false);
                }
            }
            else
            {
                var outputFormat = OutputFormatCombo.SelectedIndex switch
                {
                    1 => ChdOutputFormat.Cue,
                    2 => ChdOutputFormat.Gdi,
                    _ => ChdOutputFormat.Bin
                };
                var options = new ChdDecompressOptions { OutputFormat = outputFormat, Force = true };

                if (isBatch)
                {
                    var result = await MameChdConverter.DecompressBatchAsync(input, output, options, progress);
                    ShowStatus(string.Format(loc["MameChdConv_BatchDecompressComplete"], result.Summary), isError: false);
                }
                else
                {
                    var result = await MameChdConverter.DecompressAsync(input, output, options, progress);
                    ShowStatus(string.Format(loc["MameChdConv_DecompressComplete"], result.OutputPath), isError: false);
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
            ConvertButton.IsEnabled = true;
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
