using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class RomRenamerView : UserControl
{
    private static readonly IBrush StatusErrorBrush = new SolidColorBrush(Color.Parse("#F38BA8"));
    private static readonly IBrush StatusSuccessBrush = new SolidColorBrush(Color.Parse("#A6E3A1"));
    private static readonly IBrush StatusWarningBrush = new SolidColorBrush(Color.Parse("#F9E2AF"));

    private List<RenamePreview>? _previews;

    public RomRenamerView()
    {
        InitializeComponent();
    }

    private async void BrowseInput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        if (BatchMode.IsChecked == true)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = loc["Renamer_SelectFolderTitle"],
                AllowMultiple = false
            });

            if (folders.Count == 0) return;
            InputPathTextBox.Text = folders[0].Path.LocalPath;
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
                                       "*.sv","*.ccc",
                                       "*.iso","*.cue","*.3do",
                                       "*.chd","*.rvz","*.gcm" ]
                },
                FilePickerFileTypes.All
            ]);
            if (path == null) return;
            InputPathTextBox.Text = path;
        }

        PreviewButton.IsEnabled = true;
        _previews = null;
        ApplyButton.IsVisible = false;
        PreviewList.ItemsSource = null;
    }

    private async void PreviewButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string input = InputPathTextBox.Text ?? "";
        if (string.IsNullOrEmpty(input)) return;

        PreviewButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        StatusBorder.IsVisible = false;
        ApplyButton.IsVisible = false;
        PreviewList.ItemsSource = null;

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);

            if (BatchMode.IsChecked == true)
            {
                _previews = await RomRenamer.PreviewBatchRenameAsync(input, progress);
            }
            else
            {
                var preview = await Task.Run(() => RomRenamer.PreviewRename(input));
                _previews = [preview];
            }

            var displayItems = _previews
                .Where(p => p.WouldChange)
                .Select(p => new RenameDisplayItem
                {
                    OriginalName = p.OriginalName,
                    NewName = p.NewName,
                    Arrow = "  →",
                    System = p.DetectedSystem
                }).ToList();

            PreviewList.ItemsSource = displayItems;

            int changeCount = _previews.Count(p => p.WouldChange);
            StatusText.Text = changeCount > 0
                ? string.Format(LocalizationManager.Instance["Renamer_PreviewSummary"], changeCount, _previews.Count)
                : LocalizationManager.Instance["Renamer_AllMatch"];
            StatusText.Foreground = changeCount > 0 ? StatusSuccessBrush : StatusWarningBrush;
            StatusBorder.IsVisible = true;
            ApplyButton.IsVisible = changeCount > 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var loc = LocalizationManager.Instance;
            StatusText.Text = string.Format(loc["Common_ErrorFormat"], ex.Message);
            StatusText.Foreground = StatusErrorBrush;
            StatusBorder.IsVisible = true;
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            PreviewButton.IsEnabled = true;
        }
    }

    private async void ApplyButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_previews == null || _previews.Count == 0) return;
        var loc = LocalizationManager.Instance;

        ApplyButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);
            int renamed = await Task.Run(() => RomRenamer.ApplyBatchRename(_previews, progress));

            StatusText.Text = string.Format(loc["Renamer_RenameComplete"], renamed);
            StatusText.Foreground = StatusSuccessBrush;
            StatusBorder.IsVisible = true;
            ApplyButton.IsVisible = false;
            PreviewList.ItemsSource = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText.Text = string.Format(loc["Common_ErrorFormat"], ex.Message);
            StatusText.Foreground = StatusErrorBrush;
            StatusBorder.IsVisible = true;
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            ApplyButton.IsEnabled = true;
        }
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
}

public class RenameDisplayItem
{
    public string OriginalName { get; set; } = string.Empty;
    public string NewName { get; set; } = string.Empty;
    public string Arrow { get; set; } = string.Empty;
    public string System { get; set; } = string.Empty;
}
