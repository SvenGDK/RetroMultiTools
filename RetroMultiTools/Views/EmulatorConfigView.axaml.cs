using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class EmulatorConfigView : UserControl
{
    public EmulatorConfigView()
    {
        InitializeComponent();
        AudioVolumeSlider.ValueChanged += (s, e) => AudioVolumeText.Text = $"{(int)AudioVolumeSlider.Value}%";
        UpdateEmulatorSpecificPanels();
    }

    private EmulatorConfigGenerator.Emulator GetSelectedEmulator()
    {
        if (EmulatorCombo?.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            return Enum.TryParse<EmulatorConfigGenerator.Emulator>(tag, out var emulator)
                ? emulator
                : EmulatorConfigGenerator.Emulator.RetroArch;
        }
        return EmulatorConfigGenerator.Emulator.RetroArch;
    }

    private void EmulatorCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateEmulatorSpecificPanels();
    }

    private void UpdateEmulatorSpecificPanels()
    {
        if (RetroArchOptionsPanel == null) return; // Not yet initialized
        var emulator = GetSelectedEmulator();
        RetroArchOptionsPanel.IsVisible = emulator == EmulatorConfigGenerator.Emulator.RetroArch;
        MesenOptionsPanel.IsVisible = emulator == EmulatorConfigGenerator.Emulator.Mesen;
        Snes9xOptionsPanel.IsVisible = emulator == EmulatorConfigGenerator.Emulator.Snes9x;
        Project64OptionsPanel.IsVisible = emulator == EmulatorConfigGenerator.Emulator.Project64;
        MGBAOptionsPanel.IsVisible = emulator == EmulatorConfigGenerator.Emulator.MGBA;
        KegaFusionOptionsPanel.IsVisible = emulator == EmulatorConfigGenerator.Emulator.KegaFusion;
        MednafenOptionsPanel.IsVisible = emulator == EmulatorConfigGenerator.Emulator.Mednafen;
        StellaOptionsPanel.IsVisible = emulator == EmulatorConfigGenerator.Emulator.Stella;
        FCEUXOptionsPanel.IsVisible = emulator == EmulatorConfigGenerator.Emulator.FCEUX;
        MAMEOptionsPanel.IsVisible = emulator == EmulatorConfigGenerator.Emulator.MAME;
    }

    private string GetSelectedRegion()
    {
        var emulator = GetSelectedEmulator();
        ComboBox? regionCombo = emulator switch
        {
            EmulatorConfigGenerator.Emulator.Mesen => MesenRegionCombo,
            EmulatorConfigGenerator.Emulator.Snes9x => Snes9xRegionCombo,
            EmulatorConfigGenerator.Emulator.KegaFusion => KegaRegionCombo,
            EmulatorConfigGenerator.Emulator.FCEUX => FCEUXRegionCombo,
            _ => null
        };
        return (regionCombo?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Auto";
    }

    private async void BrowseRomDir_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFolder("Select ROM Directory");
        if (path != null) RomDirTextBox.Text = path;
    }

    private async void BrowseSaveDir_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFolder("Select Save Directory");
        if (path != null) SaveDirTextBox.Text = path;
    }

    private async void BrowseStateDir_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFolder("Select Save States Directory");
        if (path != null) StateDirTextBox.Text = path;
    }

    private async void BrowseScreenshotDir_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFolder("Select Screenshot Directory");
        if (path != null) ScreenshotDirTextBox.Text = path;
    }

    private async void BrowseOutput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var emulator = GetSelectedEmulator();
        string ext = EmulatorConfigGenerator.GetConfigExtension(emulator);
        string name = EmulatorConfigGenerator.GetEmulatorName(emulator).Replace(" ", "_").Replace("/", "_");

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Configuration File",
            SuggestedFileName = $"{name}_config{ext}"
        });

        if (file != null)
            OutputFileTextBox.Text = file.Path.LocalPath;
    }

    private async void GenerateButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string output = OutputFileTextBox.Text ?? "";
        if (string.IsNullOrEmpty(output))
        {
            // Auto-generate output path
            var emulator = GetSelectedEmulator();
            string ext = EmulatorConfigGenerator.GetConfigExtension(emulator);
            string name = EmulatorConfigGenerator.GetEmulatorName(emulator).Replace(" ", "_").Replace("/", "_");
            string dir = !string.IsNullOrEmpty(RomDirTextBox.Text)
                ? Path.GetDirectoryName(RomDirTextBox.Text) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                : Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            output = Path.Combine(dir, $"{name}_config{ext}");
            OutputFileTextBox.Text = output;
        }

        GenerateButton.IsEnabled = false;
        ShowStatus("Generating configuration...", isError: false);

        try
        {
            var emulator = GetSelectedEmulator();
            var options = new EmulatorConfigOptions
            {
                Emulator = emulator,
                RomDirectory = RomDirTextBox.Text ?? "",
                SaveDirectory = SaveDirTextBox.Text ?? "",
                StateDirectory = StateDirTextBox.Text ?? "",
                ScreenshotDirectory = ScreenshotDirTextBox.Text ?? "",
                Fullscreen = FullscreenCheck.IsChecked == true,
                SmoothVideo = SmoothVideoCheck.IsChecked == true,
                IntegerScaling = IntegerScalingCheck.IsChecked == true,
                EnableShaders = ShadersCheck.IsChecked == true,
                EnableRewind = RewindCheck.IsChecked == true,
                VSync = VSyncCheck.IsChecked == true,
                AudioVolume = (int)AudioVolumeSlider.Value,
                ShowFPS = ShowFPSCheck.IsChecked == true,
                AutoSaveState = AutoSaveCheck.IsChecked == true,
                // RetroArch-specific
                VideoDriver = (VideoDriverCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "gl",
                MenuDriver = (MenuDriverCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ozone",
                RunAheadFrames = (int)(RunAheadFramesInput.Value ?? 0),
                ThreadedVideo = ThreadedVideoCheck.IsChecked == true,
                EnableNotifications = EnableNotificationsCheck.IsChecked == true,
                // Cheats: only the visible panel's checkbox applies to the selected emulator
                EnableCheats = emulator == EmulatorConfigGenerator.Emulator.MAME
                    ? MAMEEnableCheatsCheck.IsChecked == true
                    : EnableCheatsCheck.IsChecked == true,
                // Region from the currently visible region combo
                Region = GetSelectedRegion(),
                // Sprite limit: only the visible panel's checkbox applies to the selected emulator
                RemoveSpriteLimit = emulator == EmulatorConfigGenerator.Emulator.FCEUX
                    ? FCEUXRemoveSpriteLimitCheck.IsChecked == true
                    : MesenRemoveSpriteLimitCheck.IsChecked == true,
                Overclock = MesenOverclockCheck.IsChecked == true,
                // Snes9x
                TurboSpeed = (int)(TurboSpeedInput.Value ?? 2),
                SuperFXOverclock = SuperFXOverclockCheck.IsChecked == true,
                // Project64
                CpuCore = (CpuCoreCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Recompiler",
                CounterFactor = (int)(CounterFactorInput.Value ?? 2),
                // mGBA
                AudioSync = AudioSyncCheck.IsChecked == true,
                UseBios = UseBiosCheck.IsChecked == true,
                FastForwardSpeed = (int)(FastForwardSpeedInput.Value ?? 4),
                // Stella
                Palette = (PaletteCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "standard",
                Phosphor = PhosphorCheck.IsChecked == true,
                // MAME
                SkipGameInfo = SkipGameInfoCheck.IsChecked == true,
                SkipWarnings = SkipWarningsCheck.IsChecked == true,
                // Mednafen
                CdImageMemoryCache = CdImageMemoryCacheCheck.IsChecked == true,
                // Kega Fusion
                PerfectSync = PerfectSyncCheck.IsChecked == true,
                // FCEUX
                NewPPU = NewPPUCheck.IsChecked == true,
            };

            var progress = new Progress<string>(msg => StatusText.Text = msg);
            await EmulatorConfigGenerator.GenerateAsync(options, output, progress);

            ShowStatus($"✔ Configuration generated!\nOutput: {output}", isError: false);
        }
        catch (IOException ex)
        {
            ShowStatus($"✘ Error: {ex.Message}", isError: true);
        }
        catch (UnauthorizedAccessException ex)
        {
            ShowStatus($"✘ Error: {ex.Message}", isError: true);
        }
        catch (InvalidOperationException ex)
        {
            ShowStatus($"✘ Error: {ex.Message}", isError: true);
        }
        finally
        {
            GenerateButton.IsEnabled = true;
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
