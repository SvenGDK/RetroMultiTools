using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Utilities;
using RetroMultiTools.Utilities.RetroArch;

namespace RetroMultiTools.Views.RetroArch;

public partial class RetroAchievementsWriterView : UserControl
{
    private RetroAchievementsWriter.AchievementSet _currentSet;

    public RetroAchievementsWriterView()
    {
        InitializeComponent();
        _currentSet = RetroAchievementsWriter.CreateNew("", 0, 0);
        PopulateConsoleCombo();
        PopulateTypeCombo();
    }

    private void PopulateConsoleCombo()
    {
        foreach (var (id, name) in RetroAchievementsWriter.Consoles)
        {
            ConsoleCombo.Items.Add(new ComboBoxItem { Content = $"{name} ({id})", Tag = id });
        }
    }

    private void PopulateTypeCombo()
    {
        AchTypeCombo.Items.Add(new ComboBoxItem { Content = "Standard", Tag = "" });
        AchTypeCombo.Items.Add(new ComboBoxItem { Content = "Missable", Tag = "missable" });
        AchTypeCombo.Items.Add(new ComboBoxItem { Content = "Progression", Tag = "progression" });
        AchTypeCombo.Items.Add(new ComboBoxItem { Content = "Win Condition", Tag = "win_condition" });
    }

    private void RefreshAchievementList()
    {
        AchievementListBox.ItemsSource = _currentSet.Achievements
            .Select(a => new AchievementDisplayItem
            {
                Id = a.Id,
                Title = a.Title,
                Points = $"{a.Points} pts",
                Type = string.IsNullOrEmpty(a.Type) ? "standard" : a.Type,
            })
            .ToList();
    }

    private void AddAchievementButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;

        string title = AchTitleTextBox.Text?.Trim() ?? "";
        string description = AchDescriptionTextBox.Text?.Trim() ?? "";
        string memAddr = AchMemAddrTextBox.Text?.Trim() ?? "";
        string author = AchAuthorTextBox.Text?.Trim() ?? "";
        string badge = AchBadgeTextBox.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(title))
        {
            StatusText.Text = loc["RAAchievements_TitleRequired"];
            return;
        }

        if (!int.TryParse(AchPointsTextBox.Text?.Trim(), out int points) || points < 0 || points > 100)
        {
            StatusText.Text = loc["RAAchievements_InvalidPoints"];
            return;
        }

        if (string.IsNullOrEmpty(memAddr))
        {
            StatusText.Text = loc["RAAchievements_MemAddrRequired"];
            return;
        }

        string type = (AchTypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";

        RetroAchievementsWriter.AddAchievement(
            _currentSet, title, description, points, memAddr, type, author, badge);

        RefreshAchievementList();

        // Clear input fields
        AchTitleTextBox.Text = "";
        AchDescriptionTextBox.Text = "";
        AchPointsTextBox.Text = "";
        AchMemAddrTextBox.Text = "";

        StatusText.Text = string.Format(loc["RAAchievements_AchievementAdded"], title);
    }

    private void RemoveAchievementButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (AchievementListBox.SelectedItem is not AchievementDisplayItem selected)
        {
            StatusText.Text = LocalizationManager.Instance["RAAchievements_SelectToRemove"];
            return;
        }

        RetroAchievementsWriter.RemoveAchievement(_currentSet, selected.Id);
        RefreshAchievementList();
        StatusText.Text = string.Format(LocalizationManager.Instance["RAAchievements_AchievementRemoved"], selected.Title);
    }

    private void NewSetButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string gameTitle = GameTitleTextBox.Text?.Trim() ?? "";
        int.TryParse(GameIdTextBox.Text?.Trim(), out int gameId);

        int consoleId = 0;
        if (ConsoleCombo.SelectedItem is ComboBoxItem item && item.Tag is int cid)
            consoleId = cid;

        _currentSet = RetroAchievementsWriter.CreateNew(gameTitle, gameId, consoleId);
        RefreshAchievementList();
        StatusText.Text = LocalizationManager.Instance["RAAchievements_NewSetCreated"];
    }

    private async void LoadButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = LocalizationManager.Instance["RAAchievements_LoadTitle"],
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }, FilePickerFileTypes.All]
        });

        if (files.Count == 0) return;

        try
        {
            var loaded = await RetroAchievementsWriter.LoadAsync(files[0].Path.LocalPath);
            if (loaded != null)
            {
                _currentSet = loaded;
                GameTitleTextBox.Text = loaded.Title;
                GameIdTextBox.Text = loaded.GameId.ToString();

                // Select console in combo
                for (int i = 0; i < ConsoleCombo.Items.Count; i++)
                {
                    if (ConsoleCombo.Items[i] is ComboBoxItem ci && ci.Tag is int cid && cid == loaded.ConsoleId)
                    {
                        ConsoleCombo.SelectedIndex = i;
                        break;
                    }
                }

                RefreshAchievementList();
                StatusText.Text = string.Format(LocalizationManager.Instance["RAAchievements_Loaded"], loaded.Achievements.Count);
            }
            else
            {
                StatusText.Text = LocalizationManager.Instance["RAAchievements_LoadFailed"];
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"✘ {ex.Message}";
        }
    }

    private async void SaveButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Update set from current UI
        _currentSet.Title = GameTitleTextBox.Text?.Trim() ?? "";
        int.TryParse(GameIdTextBox.Text?.Trim(), out int gameId);
        _currentSet.GameId = gameId;

        if (ConsoleCombo.SelectedItem is ComboBoxItem item && item.Tag is int cid)
            _currentSet.ConsoleId = cid;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = LocalizationManager.Instance["RAAchievements_SaveTitle"],
            SuggestedFileName = $"{SanitizeFileName(_currentSet.Title)}-achievements.json",
            FileTypeChoices = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }]
        });

        if (file == null) return;

        try
        {
            await RetroAchievementsWriter.SaveAsync(_currentSet, file.Path.LocalPath);
            StatusText.Text = string.Format(LocalizationManager.Instance["RAAchievements_Saved"], _currentSet.Achievements.Count);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"✘ {ex.Message}";
        }
    }

    private async void ExportTextButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _currentSet.Title = GameTitleTextBox.Text?.Trim() ?? "";
        int.TryParse(GameIdTextBox.Text?.Trim(), out int gameId);
        _currentSet.GameId = gameId;

        if (ConsoleCombo.SelectedItem is ComboBoxItem item && item.Tag is int cid)
            _currentSet.ConsoleId = cid;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = LocalizationManager.Instance["RAAchievements_ExportTitle"],
            SuggestedFileName = $"{SanitizeFileName(_currentSet.Title)}-achievements.txt",
            FileTypeChoices = [new FilePickerFileType("Text") { Patterns = ["*.txt"] }]
        });

        if (file == null) return;

        try
        {
            string text = RetroAchievementsWriter.ExportAsLocalText(_currentSet);
            await File.WriteAllTextAsync(file.Path.LocalPath, text);
            StatusText.Text = LocalizationManager.Instance["RAAchievements_Exported"];
        }
        catch (Exception ex)
        {
            StatusText.Text = $"✘ {ex.Message}";
        }
    }

    private void ValidateButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _currentSet.Title = GameTitleTextBox.Text?.Trim() ?? "";
        int.TryParse(GameIdTextBox.Text?.Trim(), out int gameId);
        _currentSet.GameId = gameId;

        if (ConsoleCombo.SelectedItem is ComboBoxItem item && item.Tag is int cid)
            _currentSet.ConsoleId = cid;

        var issues = RetroAchievementsWriter.Validate(_currentSet);
        if (issues.Count == 0)
        {
            StatusText.Text = LocalizationManager.Instance["RAAchievements_ValidationPassed"];
        }
        else
        {
            StatusText.Text = string.Join("\n", issues);
        }
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "achievements";

        char[] invalid = Path.GetInvalidFileNameChars();
        var result = new System.Text.StringBuilder(name.Length);
        foreach (char c in name)
        {
            result.Append(invalid.Contains(c) ? '_' : c);
        }
        return result.ToString().Trim();
    }

    private sealed class AchievementDisplayItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Points { get; set; } = "";
        public string Type { get; set; } = "";
    }
}
