using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
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

    public Analogue3DView()
    {
        InitializeComponent();
    }

    // ── SD Card Selection ──────────────────────────────────────────────

    private async void BrowseSdCard_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFolder("Select Analogue 3D SD Card");
        if (path == null) return;

        if (!Analogue3DManager.ValidateSdCard(path))
        {
            ShowStatus("⚠ The selected folder does not appear to be a valid Analogue 3D SD card. " +
                        "Expected directories like System/ or N64/.", isError: true);
            return;
        }

        _sdRoot = path;
        SdCardPathTextBox.Text = path;
        ScanPaksButton.IsEnabled = true;
        SetArtworkButton.IsEnabled = true;
        ShowStatus("✔ Analogue 3D SD card loaded successfully.", isError: false);
    }

    // ── Scan Game Paks ─────────────────────────────────────────────────

    private async void ScanPaks_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ScanPaksButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        ProgressText.Text = "Scanning N64 Game Paks...";
        SettingsEditorBorder.IsVisible = false;

        try
        {
            _gamePaks = await Analogue3DManager.ListGamePaksAsync(_sdRoot);
            PaksPanel.Children.Clear();
            PaksBorder.IsVisible = true;

            if (_gamePaks.Count == 0)
            {
                ShowStatus("No N64 Game Paks found on the SD card. Add .z64/.n64/.v64 files to the N64/ folder.", isError: false);
                PaksBorder.IsVisible = false;
            }
            else
            {
                ShowStatus($"✔ Found {_gamePaks.Count} Game Pak(s). Click a game to edit per-game settings.", isError: false);

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
                                    Text = $"{(string.IsNullOrEmpty(pak.InternalName) ? "Unknown" : pak.InternalName)}  |  " +
                                           $"Code: {(string.IsNullOrEmpty(pak.GameCode) ? "—" : pak.GameCode)}  |  " +
                                           $"{pak.SizeFormatted}  |  " +
                                           $"Art:{artIcon}  Disp:{dispIcon}  HW:{hwIcon}",
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
            ShowStatus($"✘ Error scanning Game Paks: {ex.Message}", isError: true);
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
        if (sender is not Button btn || btn.Tag is not Analogue3DManager.GamePakInfo pak)
            return;

        _selectedPak = pak;
        SettingsGameNameText.Text = $"Settings for: {pak.FileName}";
        SettingsEditorBorder.IsVisible = true;

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
            ShowStatus($"⚠ Could not load settings: {ex.Message}", isError: true);
        }
    }

    private async void SaveSettings_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_selectedPak == null)
        {
            ShowStatus("✘ No game selected. Click a Game Pak to select it.", isError: true);
            return;
        }

        SaveSettingsButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        ProgressText.Text = "Saving settings...";

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

            ShowStatus($"✔ Settings saved for {_selectedPak.FileName}", isError: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowStatus($"✘ Error saving settings: {ex.Message}", isError: true);
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
        if (_selectedPak == null)
        {
            ShowStatus("⚠ Select a Game Pak first by clicking 'Scan Game Paks' then clicking a game.", isError: true);
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Label Artwork Image",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("PNG Images") { Patterns = ["*.png"] },
                FilePickerFileTypes.All
            ]
        });

        if (files.Count == 0) return;

        ProgressPanel.IsVisible = true;
        ProgressText.Text = "Setting label artwork...";

        try
        {
            await Analogue3DManager.SetLabelArtworkAsync(_sdRoot, _selectedPak.FileName, files[0].Path.LocalPath);
            ShowStatus($"✔ Label artwork set for {_selectedPak.FileName}", isError: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FileNotFoundException)
        {
            ShowStatus($"✘ Error setting artwork: {ex.Message}", isError: true);
        }
        finally
        {
            ProgressPanel.IsVisible = false;
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────

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
