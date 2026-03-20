using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using RetroMultiTools.Utilities;
using RetroMultiTools.Utilities.Mame;

namespace RetroMultiTools.Views.Mame;

public partial class MameDatEditorView : UserControl
{
    private static readonly IBrush StatusErrorBrush = new SolidColorBrush(Color.Parse("#F38BA8"));
    private static readonly IBrush StatusSuccessBrush = new SolidColorBrush(Color.Parse("#A6E3A1"));
    private static readonly IBrush GameItemBrush = new SolidColorBrush(Color.Parse("#CDD6F4"));
    private static readonly IBrush GameItemOverflowBrush = new SolidColorBrush(Color.Parse("#6C7086"));

    private DatDocument? _datDoc;
    private List<DatGameEntry> _displayedGames = [];

    public MameDatEditorView()
    {
        InitializeComponent();
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
            _datDoc = MameDatEditor.LoadDat(path);

            // Populate header fields
            HeaderNameTextBox.Text = _datDoc.Header.Name;
            HeaderDescTextBox.Text = _datDoc.Header.Description;
            HeaderVersionTextBox.Text = _datDoc.Header.Version;
            HeaderAuthorTextBox.Text = _datDoc.Header.Author;

            var stats = MameDatEditor.GetStats(_datDoc);
            DatInfoText.Text = $"📋 {stats.Summary}";
            DatInfoBorder.IsVisible = true;

            // Show editing sections
            HeaderSection.IsVisible = true;
            SearchSection.IsVisible = true;
            GameActionsPanel.IsVisible = true;
            SavePanel.IsVisible = true;

            // Populate game list
            RefreshGameList();

            ShowStatus($"✔ DAT file loaded successfully.", isError: false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or FileNotFoundException)
        {
            ShowStatus($"✘ Error loading DAT file: {ex.Message}", isError: true);
            _datDoc = null;
            DatInfoBorder.IsVisible = false;
            HeaderSection.IsVisible = false;
            SearchSection.IsVisible = false;
            GameActionsPanel.IsVisible = false;
            GameListBorder.IsVisible = false;
            GameDetailBorder.IsVisible = false;
            SavePanel.IsVisible = false;
        }
    }

    private void RefreshGameList(string? searchQuery = null)
    {
        if (_datDoc == null) return;

        _displayedGames = string.IsNullOrWhiteSpace(searchQuery)
            ? _datDoc.Games.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase).ToList()
            : MameDatEditor.SearchGames(_datDoc, searchQuery)
                .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase).ToList();

        GameListBox.Items.Clear();
        int shown = 0;
        foreach (var game in _displayedGames)
        {
            if (shown >= 500) break;
            string label = string.IsNullOrEmpty(game.Description) || game.Description == game.Name
                ? game.Name
                : $"{game.Name} — {game.Description}";

            if (!string.IsNullOrEmpty(game.CloneOf))
                label += $" (clone of {game.CloneOf})";

            GameListBox.Items.Add(new ListBoxItem
            {
                Content = label,
                Tag = game.Name,
                Foreground = GameItemBrush
            });
            shown++;
        }

        if (_displayedGames.Count > 500)
        {
            GameListBox.Items.Add(new ListBoxItem
            {
                Content = $"... and {_displayedGames.Count - 500} more (use search to narrow results)",
                IsEnabled = false,
                Foreground = GameItemOverflowBrush
            });
        }

        GameListBorder.IsVisible = true;
        GameDetailBorder.IsVisible = false;
        RemoveGameButton.IsEnabled = false;
    }

    private void SearchButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        RefreshGameList(SearchTextBox.Text);
    }

    private void SearchTextBox_KeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            RefreshGameList(SearchTextBox.Text);
    }

    private void GameListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (GameListBox.SelectedItem is not ListBoxItem item || item.Tag == null)
        {
            GameDetailBorder.IsVisible = false;
            RemoveGameButton.IsEnabled = false;
            return;
        }

        string gameName = item.Tag.ToString() ?? "";
        var game = _datDoc?.Games.FirstOrDefault(g =>
            g.Name.Equals(gameName, StringComparison.OrdinalIgnoreCase));

        if (game == null)
        {
            GameDetailBorder.IsVisible = false;
            RemoveGameButton.IsEnabled = false;
            return;
        }

        // Populate game detail fields
        GameNameTextBox.Text = game.Name;
        GameDescTextBox.Text = game.Description;
        GameYearTextBox.Text = game.Year;
        GameMfgTextBox.Text = game.Manufacturer;
        GameCloneOfTextBox.Text = game.CloneOf;

        // Show ROM list
        var romLines = new System.Text.StringBuilder();
        foreach (var rom in game.Roms)
        {
            romLines.AppendLine($"  {rom.Name} ({rom.Size:N0} bytes)");
            if (!string.IsNullOrEmpty(rom.CRC))
                romLines.AppendLine($"    CRC: {rom.CRC}");
            if (!string.IsNullOrEmpty(rom.SHA1))
                romLines.AppendLine($"    SHA1: {rom.SHA1}");
        }

        foreach (var disk in game.Disks)
        {
            romLines.AppendLine($"  💿 {disk.Name}");
            if (!string.IsNullOrEmpty(disk.SHA1))
                romLines.AppendLine($"    SHA1: {disk.SHA1}");
        }

        if (game.Roms.Count == 0 && game.Disks.Count == 0)
            romLines.AppendLine("  (no ROMs or disks)");

        RomListText.Text = romLines.ToString();
        GameDetailBorder.IsVisible = true;
        RemoveGameButton.IsEnabled = true;
    }

    private void ApplyGameButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_datDoc == null) return;

        string originalName = (GameListBox.SelectedItem as ListBoxItem)?.Tag?.ToString() ?? "";
        var game = _datDoc.Games.FirstOrDefault(g =>
            g.Name.Equals(originalName, StringComparison.OrdinalIgnoreCase));

        if (game == null)
        {
            ShowStatus("✘ No game selected.", isError: true);
            return;
        }

        game.Name = GameNameTextBox.Text ?? game.Name;
        game.Description = GameDescTextBox.Text ?? game.Description;
        game.Year = GameYearTextBox.Text ?? game.Year;
        game.Manufacturer = GameMfgTextBox.Text ?? game.Manufacturer;
        game.CloneOf = GameCloneOfTextBox.Text ?? game.CloneOf;

        RefreshGameList(SearchTextBox.Text);
        UpdateDatInfo();
        ShowStatus($"✔ Game '{game.Name}' updated.", isError: false);
    }

    private void AddGameButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_datDoc == null) return;

        string newName = $"new_game_{_datDoc.Games.Count + 1}";
        MameDatEditor.AddGame(_datDoc, newName);

        RefreshGameList(SearchTextBox.Text);
        UpdateDatInfo();
        ShowStatus($"✔ New game '{newName}' added. Select it to edit.", isError: false);
    }

    private void RemoveGameButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_datDoc == null) return;

        string gameName = (GameListBox.SelectedItem as ListBoxItem)?.Tag?.ToString() ?? "";
        if (string.IsNullOrEmpty(gameName)) return;

        if (MameDatEditor.RemoveGame(_datDoc, gameName))
        {
            RefreshGameList(SearchTextBox.Text);
            UpdateDatInfo();
            ShowStatus($"✔ Game '{gameName}' removed.", isError: false);
        }
    }

    private void UpdateDatInfo()
    {
        if (_datDoc == null) return;

        // Apply header changes
        _datDoc.Header.Name = HeaderNameTextBox.Text ?? _datDoc.Header.Name;
        _datDoc.Header.Description = HeaderDescTextBox.Text ?? _datDoc.Header.Description;
        _datDoc.Header.Version = HeaderVersionTextBox.Text ?? _datDoc.Header.Version;
        _datDoc.Header.Author = HeaderAuthorTextBox.Text ?? _datDoc.Header.Author;

        var stats = MameDatEditor.GetStats(_datDoc);
        DatInfoText.Text = $"📋 {stats.Summary}";
    }

    private void SaveButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_datDoc == null) return;

        UpdateDatInfo();

        try
        {
            string outputPath = _datDoc.FilePath;
            MameDatEditor.SaveDat(_datDoc, outputPath);
            ShowStatus($"✔ DAT file saved to {outputPath}", isError: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowStatus($"✘ Error saving: {ex.Message}", isError: true);
        }
    }

    private async void SaveAsButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_datDoc == null) return;

        UpdateDatInfo();

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        string suggestedName = !string.IsNullOrEmpty(_datDoc.Header.Name)
            ? _datDoc.Header.Name + ".dat"
            : "edited.dat";

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save DAT File As",
            SuggestedFileName = suggestedName,
            FileTypeChoices =
            [
                new FilePickerFileType("DAT Files") { Patterns = ["*.dat"] },
                new FilePickerFileType("XML Files") { Patterns = ["*.xml"] }
            ]
        });

        if (file == null) return;

        try
        {
            string outputPath = file.Path.LocalPath;
            MameDatEditor.SaveDat(_datDoc, outputPath);
            _datDoc.FilePath = outputPath;
            DatFileTextBox.Text = outputPath;
            ShowStatus($"✔ DAT file saved to {outputPath}", isError: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowStatus($"✘ Error saving: {ex.Message}", isError: true);
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
}
