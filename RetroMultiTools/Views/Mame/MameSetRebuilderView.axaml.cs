using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Utilities.Mame;

namespace RetroMultiTools.Views.Mame;

public partial class MameSetRebuilderView : UserControl
{
    private static readonly IBrush StatusErrorBrush = new SolidColorBrush(Color.Parse("#F38BA8"));
    private static readonly IBrush StatusSuccessBrush = new SolidColorBrush(Color.Parse("#A6E3A1"));

    private List<MameMachine>? _machines;

    public MameSetRebuilderView()
    {
        InitializeComponent();
    }

    private async void BrowseXml_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        var path = await PickFile(loc["MameRebuilder_SelectXmlTitle"],
        [
            new FilePickerFileType(loc["MameRebuilder_XmlFileType"]) { Patterns = ["*.xml", "*.dat"] },
            FilePickerFileTypes.All
        ]);
        if (path == null) return;

        XmlFileTextBox.Text = path;

        try
        {
            _machines = MameRomAuditor.LoadMameXml(path);
            XmlInfoText.Text = string.Format(loc["MameRebuilder_LoadedMachines"], _machines.Count);
            XmlInfoPanel.IsVisible = true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            _machines = null;
            XmlInfoText.Text = string.Format(loc["MameRebuilder_LoadError"], ex.Message);
            XmlInfoPanel.IsVisible = true;
        }

        UpdateRebuildButton();
    }

    private async void BrowseSourceDir_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFolder(LocalizationManager.Instance["MameRebuilder_SelectSourceDirTitle"]);
        if (path != null) SourceDirTextBox.Text = path;
        UpdateRebuildButton();
    }

    private async void BrowseOutputDir_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFolder(LocalizationManager.Instance["MameRebuilder_SelectOutputDirTitle"]);
        if (path != null) OutputDirTextBox.Text = path;
        UpdateRebuildButton();
    }

    private void UpdateRebuildButton()
    {
        RebuildButton.IsEnabled = !string.IsNullOrEmpty(XmlFileTextBox.Text) &&
                                  !string.IsNullOrEmpty(SourceDirTextBox.Text) &&
                                  !string.IsNullOrEmpty(OutputDirTextBox.Text) &&
                                  _machines != null;
    }

    private async void RebuildButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        if (_machines == null)
        {
            ShowStatus(loc["MameRebuilder_LoadXmlFirst"], isError: true);
            return;
        }

        string sourceDir = SourceDirTextBox.Text ?? "";
        string outputDir = OutputDirTextBox.Text ?? "";

        if (string.IsNullOrEmpty(sourceDir) || string.IsNullOrEmpty(outputDir))
        {
            ShowStatus(loc["MameRebuilder_SelectDirectories"], isError: true);
            return;
        }

        RebuildButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        StatusBorder.IsVisible = false;
        ResultsBorder.IsVisible = false;

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);

            // Step 1: Index source directory
            var sourceIndex = await MameSetRebuilder.IndexSourceDirectoryAsync(sourceDir, progress);

            // Step 2: Rebuild sets
            var mode = ModeComboBox.SelectedIndex switch
            {
                1 => RebuildMode.NonMerged,
                2 => RebuildMode.Merged,
                _ => RebuildMode.Split
            };
            var options = new RebuildOptions
            {
                OverwriteExisting = OverwriteCheckBox.IsChecked == true,
                OnlyComplete = OnlyCompleteCheckBox.IsChecked == true,
                Mode = mode
            };

            var result = await MameSetRebuilder.RebuildAsync(_machines, sourceIndex, outputDir, options, progress);

            ShowStatus(string.Format(loc["MameRebuilder_RebuildComplete"], result.Summary), isError: false);

            var lines = new System.Text.StringBuilder();

            foreach (var set in result.RebuiltSets.OrderBy(s => !s.IsComplete).ThenBy(s => s.MachineName))
            {
                string icon = set.IsComplete ? "✔" : "⚠";
                lines.AppendLine($"{icon} {set.MachineName} — {set.Description}");
                lines.AppendLine($"   {string.Format(loc["MameRebuilder_RomsIncluded"], set.RomsIncluded)}");

                if (!set.IsComplete && set.MissingRomNames.Count > 0)
                {
                    lines.AppendLine($"   {string.Format(loc["MameRebuilder_MissingRomsLabel"], set.RomsMissing)}");
                    foreach (var name in set.MissingRomNames.Take(5))
                        lines.AppendLine($"   → {name}");
                    if (set.MissingRomNames.Count > 5)
                        lines.AppendLine($"   → {string.Format(loc["MameRebuilder_AndMore"], set.MissingRomNames.Count - 5)}");
                }
                lines.AppendLine();
            }

            ResultsText.Text = lines.ToString();
            ResultsBorder.IsVisible = result.RebuiltSets.Count > 0;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            ShowStatus(string.Format(loc["Common_ErrorFormat"], ex.Message), isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            RebuildButton.IsEnabled = true;
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
