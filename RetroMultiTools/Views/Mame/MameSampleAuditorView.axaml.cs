using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Utilities.Mame;

namespace RetroMultiTools.Views.Mame;

public partial class MameSampleAuditorView : UserControl
{
    private static readonly IBrush StatusErrorBrush = new SolidColorBrush(Color.Parse("#F38BA8"));
    private static readonly IBrush StatusSuccessBrush = new SolidColorBrush(Color.Parse("#A6E3A1"));

    private List<MameSampleSet>? _sampleSets;

    public MameSampleAuditorView()
    {
        InitializeComponent();
    }

    private async void BrowseXml_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        var path = await PickFile(loc["MameSamples_SelectXmlTitle"],
        [
            new FilePickerFileType(loc["MameSamples_XmlFileType"]) { Patterns = ["*.xml", "*.dat"] },
            FilePickerFileTypes.All
        ]);
        if (path == null) return;

        XmlFileTextBox.Text = path;

        try
        {
            _sampleSets = MameSampleAuditor.LoadSampleRequirements(path);
            XmlInfoText.Text = string.Format(loc["MameSamples_LoadedSets"], _sampleSets.Count);
            XmlInfoPanel.IsVisible = true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            _sampleSets = null;
            XmlInfoText.Text = string.Format(loc["MameSamples_LoadError"], ex.Message);
            XmlInfoPanel.IsVisible = true;
        }

        UpdateAuditButton();
    }

    private async void BrowseSampleDir_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFolder(LocalizationManager.Instance["MameSamples_SelectSampleDirTitle"]);
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
        var loc = LocalizationManager.Instance;
        if (_sampleSets == null)
        {
            ShowStatus(loc["MameSamples_LoadXmlFirst"], isError: true);
            return;
        }

        string sampleDir = SampleDirTextBox.Text ?? "";
        if (string.IsNullOrEmpty(sampleDir))
        {
            ShowStatus(loc["MameSamples_SelectSamplesDir"], isError: true);
            return;
        }

        AuditButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        StatusBorder.IsVisible = false;
        ResultsBorder.IsVisible = false;

        try
        {
            var progress = new Progress<string>(msg => ProgressText.Text = msg);

            var result = await MameSampleAuditor.AuditDirectoryAsync(sampleDir, _sampleSets, progress,
                searchRecursively: RecursiveCheckBox.IsChecked == true);

            ShowStatus(string.Format(loc["MameSamples_AuditComplete"], result.Summary), isError: false);

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
                        lines.AppendLine($"   → {string.Format(loc["MameSamples_MissingSample"], sample)}");
                    if (r.MissingSamples.Count > 5)
                        lines.AppendLine($"   → {string.Format(loc["MameSamples_MoreMissing"], r.MissingSamples.Count - 5)}");
                }
                lines.AppendLine();
            }

            if (result.MissingSets.Count > 0)
            {
                lines.AppendLine(string.Format(loc["MameSamples_MissingSetsHeader"], result.MissingSets.Count));
                foreach (var name in result.MissingSets.Take(20))
                    lines.AppendLine($"  ✘ {name}");
                if (result.MissingSets.Count > 20)
                    lines.AppendLine($"  {string.Format(loc["MameSamples_AndMore"], result.MissingSets.Count - 20)}");
            }

            ResultsText.Text = lines.ToString();
            ResultsBorder.IsVisible = true;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            ShowStatus(string.Format(loc["Common_ErrorFormat"], ex.Message), isError: true);
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
