using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using RetroMultiTools.Utilities;
using RetroMultiTools.Utilities.Mame;

namespace RetroMultiTools.Views.Mame;

public partial class MameRomAuditorView : UserControl
{
    private static readonly IBrush StatusErrorBrush = new SolidColorBrush(Color.Parse("#F38BA8"));
    private static readonly IBrush StatusSuccessBrush = new SolidColorBrush(Color.Parse("#A6E3A1"));

    private List<MameMachine>? _machines;

    public MameRomAuditorView()
    {
        InitializeComponent();
    }

    private async void BrowseXml_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFile("Select MAME XML / DAT File",
        [
            new FilePickerFileType("MAME XML Files") { Patterns = ["*.xml", "*.dat"] },
            FilePickerFileTypes.All
        ]);
        if (path == null) return;

        XmlFileTextBox.Text = path;

        try
        {
            _machines = MameRomAuditor.LoadMameXml(path);
            XmlInfoText.Text = $"Loaded {_machines.Count} machines from MAME database.";
            XmlInfoPanel.IsVisible = true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            _machines = null;
            XmlInfoText.Text = $"Error loading MAME XML: {ex.Message}";
            XmlInfoPanel.IsVisible = true;
        }

        UpdateAuditButton();
    }

    private async void BrowseRomDir_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFolder("Select MAME ROM Directory");
        if (path != null) RomDirTextBox.Text = path;
        UpdateAuditButton();
    }

    private void UpdateAuditButton()
    {
        AuditButton.IsEnabled = !string.IsNullOrEmpty(XmlFileTextBox.Text) &&
                                !string.IsNullOrEmpty(RomDirTextBox.Text) &&
                                _machines != null;
    }

    private async void AuditButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_machines == null)
        {
            ShowStatus("Please load a MAME XML database first.", isError: true);
            return;
        }

        string romDir = RomDirTextBox.Text ?? "";
        if (string.IsNullOrEmpty(romDir))
        {
            ShowStatus("Please select a ROM directory.", isError: true);
            return;
        }

        AuditButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        StatusBorder.IsVisible = false;
        ResultsBorder.IsVisible = false;

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);

            var result = await MameRomAuditor.AuditDirectoryAsync(romDir, _machines, progress,
                searchRecursively: RecursiveCheckBox.IsChecked == true);

            ShowStatus($"✔ Audit complete!\n{result.Summary}", isError: false);

            var lines = new System.Text.StringBuilder();

            // Show good sets first, then incomplete, then bad
            foreach (var r in result.Results.OrderBy(r => r.Status))
            {
                string icon = r.Status switch
                {
                    MachineStatus.Good => "✔",
                    MachineStatus.Incomplete => "⚠",
                    MachineStatus.Bad => "✘",
                    _ => "?"
                };

                string clone = r.IsClone ? $" (clone of {r.ParentName})" : "";
                lines.AppendLine($"{icon} {r.MachineName}{clone}");
                lines.AppendLine($"   {r.Description}");
                lines.AppendLine($"   {r.StatusDetail}");

                if (r.Issues.Count > 0)
                {
                    foreach (var issue in r.Issues.Take(5))
                        lines.AppendLine($"   → {issue}");
                    if (r.Issues.Count > 5)
                        lines.AppendLine($"   → ... and {r.Issues.Count - 5} more issues");
                }
                lines.AppendLine();
            }

            if (result.MissingMachines.Count > 0)
            {
                lines.AppendLine($"--- Missing machines ({result.MissingMachines.Count}) ---");
                foreach (var name in result.MissingMachines.Take(20))
                    lines.AppendLine($"  ✘ {name}");
                if (result.MissingMachines.Count > 20)
                    lines.AppendLine($"  ... and {result.MissingMachines.Count - 20} more");
            }

            ResultsText.Text = lines.ToString();
            ResultsBorder.IsVisible = true;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            ShowStatus($"✘ Error: {ex.Message}", isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            AuditButton.IsEnabled = true;
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
