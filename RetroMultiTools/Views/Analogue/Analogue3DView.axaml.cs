using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Utilities;
using RetroMultiTools.Utilities.Analogue;

namespace RetroMultiTools.Views.Analogue;

public partial class Analogue3DView : UserControl
{
    private static readonly IBrush StatusErrorBrush = new SolidColorBrush(Color.Parse("#F38BA8"));
    private static readonly IBrush StatusSuccessBrush = new SolidColorBrush(Color.Parse("#A6E3A1"));
    private static readonly IBrush ResultLabelBrush = new SolidColorBrush(Color.Parse("#A6ADC8"));
    private static readonly IBrush ResultValueBrush = new SolidColorBrush(Color.Parse("#CDD6F4"));

    private string _sdRoot = string.Empty;
    private List<Analogue3DManager.GamePakInfo> _gamePaks = [];
    private Analogue3DManager.GamePakInfo? _selectedPak;
    private Button? _selectedPakButton;

    public Analogue3DView()
    {
        InitializeComponent();
    }

    // ── SD Card Selection ──────────────────────────────────────────────

    private async void BrowseSdCard_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        var path = await PickFolder(loc["Analogue3D_BrowseSdCardTitle"]);
        if (path == null) return;

        if (!Analogue3DManager.ValidateSdCard(path))
        {
            ShowStatus(loc["Analogue3D_InvalidSdCard"], isError: true);
            return;
        }

        _sdRoot = path;
        SdCardPathTextBox.Text = path;
        ScanPaksButton.IsEnabled = true;
        ShowStatus(loc["Analogue3D_SdCardLoaded"], isError: false);
    }

    // ── Scan Game Paks ─────────────────────────────────────────────────

    private async void ScanPaks_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        ScanPaksButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        ProgressText.Text = loc["Analogue3D_ScanningGamePaks"];
        SettingsEditorBorder.IsVisible = false;

        try
        {
            _gamePaks = await Analogue3DManager.ListGamePaksAsync(_sdRoot);
            PaksPanel.Children.Clear();
            PaksBorder.IsVisible = true;

            if (_gamePaks.Count == 0)
            {
                ShowStatus(loc["Analogue3D_NoGamePaksFound"], isError: false);
                PaksBorder.IsVisible = false;
            }
            else
            {
                ShowStatus(string.Format(loc["Analogue3D_FoundGamePaks"], _gamePaks.Count), isError: false);

                foreach (var pak in _gamePaks)
                {
                    string artIcon = pak.HasLabelArt ? "🖼" : "⬜";
                    string dispIcon = pak.HasDisplaySettings ? "🖥" : "⬜";
                    string hwIcon = pak.HasHardwareSettings ? "⚙" : "⬜";

                    var pakButton = new Button
                    {
                        Padding = new Avalonia.Thickness(10, 8),
                        Margin = new Avalonia.Thickness(0, 0, 0, 4),
                        HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                        Tag = pak,
                        Content = new StackPanel
                        {
                            Spacing = 2,
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = pak.FileName,
                                    FontWeight = Avalonia.Media.FontWeight.SemiBold,
                                    Foreground = ResultValueBrush
                                },
                                new TextBlock
                                {
                                    Text = $"{(string.IsNullOrEmpty(pak.InternalName) ? LocalizationManager.Instance["Common_Unknown"] : pak.InternalName)}  |  " +
                                           string.Format(LocalizationManager.Instance["Analogue3D_GameCode"], string.IsNullOrEmpty(pak.GameCode) ? "—" : pak.GameCode) + "  |  " +
                                           $"{pak.SizeFormatted}  |  " +
                                           string.Format(LocalizationManager.Instance["Analogue3D_ArtLabel"], artIcon) + "  " + string.Format(LocalizationManager.Instance["Analogue3D_DispLabel"], dispIcon) + "  " + string.Format(LocalizationManager.Instance["Analogue3D_HwLabel"], hwIcon),
                                    FontSize = 11,
                                    Foreground = ResultLabelBrush
                                }
                            }
                        }
                    };
                    pakButton.Click += PakButton_Click;
                    PaksPanel.Children.Add(pakButton);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowStatus(string.Format(loc["Analogue3D_ScanError"], ex.Message), isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            ScanPaksButton.IsEnabled = true;
        }
    }

    // ── Per-Game Settings ──────────────────────────────────────────────

    private async void PakButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        if (sender is not Button btn || btn.Tag is not Analogue3DManager.GamePakInfo pak)
            return;

        _selectedPak = pak;
        _selectedPakButton = btn;
        SettingsGameNameText.Text = string.Format(loc["Analogue3D_SettingsFor"], pak.FileName);
        SettingsEditorBorder.IsVisible = true;
        SetArtworkButton.IsEnabled = true;
        RemoveArtworkButton.IsEnabled = pak.HasLabelArt;

        // Load existing settings
        try
        {
            var display = await Analogue3DManager.LoadDisplaySettingsAsync(_sdRoot, pak.FileName);
            var hardware = await Analogue3DManager.LoadHardwareSettingsAsync(_sdRoot, pak.FileName);

            // Set display controls
            SetComboByContent(ResolutionCombo, display.Resolution);
            SetComboByContent(AspectRatioCombo, display.AspectRatio);
            SetComboByContent(SmoothingCombo, display.Smoothing);
            CropOverscanCheck.IsChecked = display.CropOverscan;

            // Set hardware controls
            ExpansionPakCheck.IsChecked = hardware.ExpansionPak;
            RumblePakCheck.IsChecked = hardware.RumblePak;
            CpuOverclockCheck.IsChecked = hardware.CpuOverclock;
            SetComboByContent(ControllerPakCombo, hardware.ControllerPak);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowStatus(string.Format(loc["Analogue3D_LoadSettingsError"], ex.Message), isError: true);
        }
    }

    private async void SaveSettings_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        if (_selectedPak == null)
        {
            ShowStatus(loc["Analogue3D_NoGameSelected"], isError: true);
            return;
        }

        SaveSettingsButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        ProgressText.Text = loc["Analogue3D_SavingSettings"];

        try
        {
            var display = new Analogue3DManager.DisplaySettings
            {
                Resolution = GetComboContent(ResolutionCombo) ?? "auto",
                AspectRatio = GetComboContent(AspectRatioCombo) ?? "auto",
                Smoothing = GetComboContent(SmoothingCombo) ?? "off",
                CropOverscan = CropOverscanCheck.IsChecked == true
            };

            var hardware = new Analogue3DManager.HardwareSettings
            {
                ExpansionPak = ExpansionPakCheck.IsChecked == true,
                RumblePak = RumblePakCheck.IsChecked == true,
                CpuOverclock = CpuOverclockCheck.IsChecked == true,
                ControllerPak = GetComboContent(ControllerPakCombo) ?? "auto"
            };

            await Analogue3DManager.SaveDisplaySettingsAsync(_sdRoot, _selectedPak.FileName, display);
            await Analogue3DManager.SaveHardwareSettingsAsync(_sdRoot, _selectedPak.FileName, hardware);

            // Update the cached pak info so the list reflects the new state
            _selectedPak.HasDisplaySettings = true;
            _selectedPak.HasHardwareSettings = true;
            UpdateSelectedPakButton();

            ShowStatus(string.Format(loc["Analogue3D_SettingsSaved"], _selectedPak.FileName), isError: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowStatus(string.Format(loc["Analogue3D_SaveError"], ex.Message), isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            SaveSettingsButton.IsEnabled = true;
        }
    }

    // ── Label Artwork ──────────────────────────────────────────────────

    private async void SetArtwork_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        if (_selectedPak == null)
        {
            ShowStatus(loc["Analogue3D_SelectGamePakFirst"], isError: true);
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = loc["Analogue3D_SelectArtworkTitle"],
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("PNG Images") { Patterns = ["*.png"] },
                FilePickerFileTypes.All
            ]
        });

        if (files.Count == 0) return;

        ProgressPanel.IsVisible = true;
        ProgressText.Text = loc["Analogue3D_SettingArtwork"];

        try
        {
            await Analogue3DManager.SetLabelArtworkAsync(_sdRoot, _selectedPak.FileName, files[0].Path.LocalPath);
            _selectedPak.HasLabelArt = true;
            RemoveArtworkButton.IsEnabled = true;
            UpdateSelectedPakButton();
            ShowStatus(string.Format(loc["Analogue3D_ArtworkSet"], _selectedPak.FileName), isError: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FileNotFoundException)
        {
            ShowStatus(string.Format(loc["Analogue3D_ArtworkError"], ex.Message), isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
        }
    }

    // ── Remove Label Artwork ──────────────────────────────────────────

    private async void RemoveArtwork_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        if (_selectedPak == null)
        {
            ShowStatus(loc["Analogue3D_SelectGamePakFirst"], isError: true);
            return;
        }

        if (!_selectedPak.HasLabelArt)
        {
            ShowStatus(loc["Analogue3D_NoArtworkToRemove"], isError: false);
            return;
        }

        ProgressPanel.IsVisible = true;
        ProgressText.Text = loc["Analogue3D_RemovingArtwork"];

        try
        {
            await Analogue3DManager.RemoveLabelArtworkAsync(_sdRoot, _selectedPak.FileName);
            _selectedPak.HasLabelArt = false;
            RemoveArtworkButton.IsEnabled = false;
            UpdateSelectedPakButton();
            ShowStatus(string.Format(loc["Analogue3D_ArtworkRemoved"], _selectedPak.FileName), isError: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowStatus(string.Format(loc["Analogue3D_RemoveArtworkError"], ex.Message), isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
        }
    }

    // ── Delete Game Pak ────────────────────────────────────────────────

    private async void DeletePak_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        if (_selectedPak == null)
        {
            ShowStatus(loc["Analogue3D_NoGameSelected"], isError: true);
            return;
        }

        // Show confirmation panel instead of deleting immediately
        ConfirmDeleteText.Text = string.Format(loc["Analogue3D_ConfirmDeleteMessage"], _selectedPak.FileName);
        ConfirmDeletePanel.IsVisible = true;
        DeletePakButton.IsEnabled = false;
    }

    private void CancelDelete_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ConfirmDeletePanel.IsVisible = false;
        DeletePakButton.IsEnabled = true;
    }

    private async void ConfirmDelete_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        ConfirmDeletePanel.IsVisible = false;

        if (_selectedPak == null)
            return;

        ProgressPanel.IsVisible = true;
        ProgressText.Text = loc["Analogue3D_DeletingGamePak"];

        try
        {
            await Analogue3DManager.DeleteGamePakAsync(_sdRoot, _selectedPak);
            ShowStatus(string.Format(loc["Analogue3D_GamePakDeleted"], _selectedPak.FileName), isError: false);

            // Remove the button from the list and clear selection
            if (_selectedPakButton != null)
            {
                PaksPanel.Children.Remove(_selectedPakButton);
                _selectedPakButton = null;
            }
            _gamePaks.Remove(_selectedPak);
            _selectedPak = null;
            SettingsEditorBorder.IsVisible = false;
            SetArtworkButton.IsEnabled = false;
            RemoveArtworkButton.IsEnabled = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowStatus(string.Format(loc["Analogue3D_DeleteError"], ex.Message), isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            DeletePakButton.IsEnabled = true;
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private void UpdateSelectedPakButton()
    {
        if (_selectedPakButton == null || _selectedPak == null)
            return;

        string artIcon = _selectedPak.HasLabelArt ? "🖼" : "⬜";
        string dispIcon = _selectedPak.HasDisplaySettings ? "🖥" : "⬜";
        string hwIcon = _selectedPak.HasHardwareSettings ? "⚙" : "⬜";

        string internalName = string.IsNullOrEmpty(_selectedPak.InternalName) ? LocalizationManager.Instance["Common_Unknown"] : _selectedPak.InternalName;
        string gameCode = string.IsNullOrEmpty(_selectedPak.GameCode) ? "—" : _selectedPak.GameCode;

        // The pak button Content is a StackPanel with [0]=filename, [1]=detail line
        const int detailLineIndex = 1;
        if (_selectedPakButton.Content is StackPanel sp && sp.Children.Count > detailLineIndex
            && sp.Children[detailLineIndex] is TextBlock detailText)
        {
            detailText.Text = $"{internalName}  |  " +
                              string.Format(LocalizationManager.Instance["Analogue3D_GameCode"], gameCode) + "  |  " +
                              $"{_selectedPak.SizeFormatted}  |  " +
                              string.Format(LocalizationManager.Instance["Analogue3D_ArtLabel"], artIcon) + "  " + string.Format(LocalizationManager.Instance["Analogue3D_DispLabel"], dispIcon) + "  " + string.Format(LocalizationManager.Instance["Analogue3D_HwLabel"], hwIcon);
        }
    }

    private static void SetComboByContent(ComboBox combo, string value)
    {
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is ComboBoxItem item && item.Content?.ToString() == value)
            {
                combo.SelectedIndex = i;
                return;
            }
        }
        combo.SelectedIndex = 0; // default to first item
    }

    private static string? GetComboContent(ComboBox combo)
    {
        return (combo.SelectedItem as ComboBoxItem)?.Content?.ToString();
    }

    private void ShowStatus(string message, bool isError)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError ? StatusErrorBrush : StatusSuccessBrush;
        StatusBorder.IsVisible = true;
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
