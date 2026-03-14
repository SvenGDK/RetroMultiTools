using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class MameSampleAuditorView : UserControl
{
    private List<MameSampleSet>? _sampleSets;

    public MameSampleAuditorView()
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
            _sampleSets = MameSampleAuditor.LoadSampleRequirements(path);
            XmlInfoText.Text = $"Loaded {_sampleSets.Count} machines with sample requirements.";
            XmlInfoPanel.IsVisible = true;
        }
        catch (InvalidOperationException ex)
        {
            _sampleSets = null;
            XmlInfoText.Text = $"Error loading MAME XML: {ex.Message}";
            XmlInfoPanel.IsVisible = true;
        }
        catch (IOException ex)
        {
            _sampleSets = null;
            XmlInfoText.Text = $"Error loading MAME XML: {ex.Message}";
            XmlInfoPanel.IsVisible = true;
        }

        UpdateAuditButton();
    }

    private async void BrowseSampleDir_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFolder("Select MAME Samples Directory");
        if (path != null) SampleDirTextBox.Text = path;
        UpdateAuditButton();
    }

    private void UpdateAuditButton()
    {
        AuditButton.IsEnabled = !string.IsNullOrEmpty(XmlFileTextBox.Text) &&
                                !string.IsNullOrEmpty(SampleDirTextBox.Text) &&
                                _sampleSets != null;
    }

    private async void AuditButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_sampleSets == null)
        {
            ShowStatus("Please load a MAME XML database first.", isError: true);
            return;
        }

        string sampleDir = SampleDirTextBox.Text ?? "";
        if (string.IsNullOrEmpty(sampleDir))
        {
            ShowStatus("Please select a samples directory.", isError: true);
            return;
        }

        AuditButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        StatusBorder.IsVisible = false;
        ResultsBorder.IsVisible = false;

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);

            var result = await MameSampleAuditor.AuditDirectoryAsync(sampleDir, _sampleSets, progress);

            ShowStatus($"✔ Audit complete!\n{result.Summary}", isError: false);

            var lines = new System.Text.StringBuilder();

            foreach (var r in result.Results.OrderBy(r => r.Status))
            {
                string icon = r.Status switch
                {
                    SampleSetStatus.Good => "✔",
                    SampleSetStatus.Incomplete => "⚠",
                    SampleSetStatus.Bad => "✘",
                    _ => "?"
                };

                lines.AppendLine($"{icon} {r.SetName}");
                if (!string.IsNullOrEmpty(r.Description) && r.Description != r.SetName)
                    lines.AppendLine($"   {r.Description}");
                lines.AppendLine($"   {r.StatusDetail}");

                if (r.MissingSamples.Count > 0)
                {
                    foreach (var sample in r.MissingSamples.Take(5))
                        lines.AppendLine($"   → Missing: {sample}");
                    if (r.MissingSamples.Count > 5)
                        lines.AppendLine($"   → ... and {r.MissingSamples.Count - 5} more missing");
                }
                lines.AppendLine();
            }

            if (result.MissingSets.Count > 0)
            {
                lines.AppendLine($"--- Missing sample sets ({result.MissingSets.Count}) ---");
                foreach (var name in result.MissingSets.Take(20))
                    lines.AppendLine($"  ✘ {name}");
                if (result.MissingSets.Count > 20)
                    lines.AppendLine($"  ... and {result.MissingSets.Count - 20} more");
            }

            ResultsText.Text = lines.ToString();
            ResultsBorder.IsVisible = true;
        }
        catch (IOException ex)
        {
            ShowStatus($"✘ Error: {ex.Message}", isError: true);
        }
        catch (InvalidOperationException ex)
        {
            ShowStatus($"✘ Error: {ex.Message}", isError: true);
        }
        catch (UnauthorizedAccessException ex)
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
        StatusText.Foreground = isError
            ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#F38BA8"))
            : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#A6E3A1"));
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
