using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Utilities;
using RetroMultiTools.Utilities.RetroArch;

namespace RetroMultiTools.Views;

public partial class EmulatorConfigView : UserControl
{
    private static readonly IBrush ErrorBrush = new SolidColorBrush(Color.Parse("#F38BA8"));
    private static readonly IBrush SuccessBrush = new SolidColorBrush(Color.Parse("#A6E3A1"));

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
        ExportToRetroArchButton.IsVisible = emulator == EmulatorConfigGenerator.Emulator.RetroArch;
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

    private bool GetEnableCheats(EmulatorConfigGenerator.Emulator emulator) => emulator switch
    {
        EmulatorConfigGenerator.Emulator.RetroArch => EnableCheatsCheck.IsChecked == true,
        EmulatorConfigGenerator.Emulator.Mesen => MesenEnableCheatsCheck.IsChecked == true,
        EmulatorConfigGenerator.Emulator.Snes9x => Snes9xEnableCheatsCheck.IsChecked == true,
        EmulatorConfigGenerator.Emulator.Project64 => Project64EnableCheatsCheck.IsChecked == true,
        EmulatorConfigGenerator.Emulator.MGBA => MGBAEnableCheatsCheck.IsChecked == true,
        EmulatorConfigGenerator.Emulator.KegaFusion => KegaEnableCheatsCheck.IsChecked == true,
        EmulatorConfigGenerator.Emulator.Mednafen => MednafenEnableCheatsCheck.IsChecked == true,
        EmulatorConfigGenerator.Emulator.Stella => StellaEnableCheatsCheck.IsChecked == true,
        EmulatorConfigGenerator.Emulator.FCEUX => FCEUXEnableCheatsCheck.IsChecked == true,
        EmulatorConfigGenerator.Emulator.MAME => MAMEEnableCheatsCheck.IsChecked == true,
        _ => false
    };

    private async void BrowseRomDir_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFolder(LocalizationManager.Instance["EmuConfig_SelectRomDir"]);
        if (path != null) RomDirTextBox.Text = path;
    }

    private async void BrowseSaveDir_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFolder(LocalizationManager.Instance["EmuConfig_SelectSaveDir"]);
        if (path != null) SaveDirTextBox.Text = path;
    }

    private async void BrowseStateDir_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFolder(LocalizationManager.Instance["EmuConfig_SelectStateDir"]);
        if (path != null) StateDirTextBox.Text = path;
    }

    private async void BrowseScreenshotDir_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFolder(LocalizationManager.Instance["EmuConfig_SelectScreenshotDir"]);
        if (path != null) ScreenshotDirTextBox.Text = path;
    }

    private async void BrowseOutput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var emulator = GetSelectedEmulator();
        string ext = EmulatorConfigGenerator.GetConfigExtension(emulator);
        string name = EmulatorConfigGenerator.GetEmulatorName(emulator).Replace(" ", "_").Replace("/", "_");

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = loc["EmuConfig_SaveDialogTitle"],
            SuggestedFileName = $"{name}_config{ext}"
        });

        if (file != null)
            OutputFileTextBox.Text = file.Path.LocalPath;
    }

    private EmulatorConfigOptions BuildOptions()
    {
        var emulator = GetSelectedEmulator();
        return new EmulatorConfigOptions
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
            AudioDriver = (AudioDriverCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "auto",
            InputDriver = (InputDriverCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "auto",
            RunAheadFrames = (int)(RunAheadFramesInput.Value ?? 0),
            ThreadedVideo = ThreadedVideoCheck.IsChecked == true,
            EnableNotifications = EnableNotificationsCheck.IsChecked == true,
            AudioSyncRetroArch = AudioSyncRetroArchCheck.IsChecked == true,
            ConfigSaveOnExit = ConfigSaveOnExitCheck.IsChecked == true,
            PauseOnUnfocus = PauseOnUnfocusCheck.IsChecked == true,
            // Cheats: read from the active emulator's panel
            EnableCheats = GetEnableCheats(emulator),
            // Region from the currently visible region combo
            Region = GetSelectedRegion(),
            // Sprite limit: only the visible panel's checkbox applies
            RemoveSpriteLimit = emulator == EmulatorConfigGenerator.Emulator.FCEUX
                ? FCEUXRemoveSpriteLimitCheck.IsChecked == true
                : MesenRemoveSpriteLimitCheck.IsChecked == true,
            Overclock = MesenOverclockCheck.IsChecked == true,
            // Snes9x
            TurboSpeed = (int)(TurboSpeedInput.Value ?? 2),
            SuperFXOverclock = SuperFXOverclockCheck.IsChecked == true,
            BlockInvalidVram = BlockInvalidVramCheck.IsChecked == true,
            DynamicRateControl = DynamicRateControlCheck.IsChecked == true,
            // Project64
            CpuCore = (CpuCoreCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Recompiler",
            CounterFactor = (int)(CounterFactorInput.Value ?? 2),
            DisplaySpeed = DisplaySpeedCheck.IsChecked == true,
            // mGBA
            AudioSync = AudioSyncCheck.IsChecked == true,
            UseBios = UseBiosCheck.IsChecked == true,
            FastForwardSpeed = (int)(FastForwardSpeedInput.Value ?? 4),
            FrameSkip = (int)(FrameSkipInput.Value ?? 0),
            AllowOpposingDirections = AllowOpposingDirectionsCheck.IsChecked == true,
            // Stella
            Palette = (PaletteCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "standard",
            Phosphor = PhosphorCheck.IsChecked == true,
            TvEffects = (int)(TvEffectsInput.Value ?? 0),
            // MAME
            SkipGameInfo = SkipGameInfoCheck.IsChecked == true,
            SkipWarnings = SkipWarningsCheck.IsChecked == true,
            // Mednafen
            CdImageMemoryCache = CdImageMemoryCacheCheck.IsChecked == true,
            SoundBufferSize = (int)(SoundBufferSizeInput.Value ?? 32),
            MednafenVideoDriver = (MednafenVideoDriverCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "opengl",
            // Kega Fusion
            PerfectSync = PerfectSyncCheck.IsChecked == true,
            SramAutoSave = SramAutoSaveCheck.IsChecked == true,
            // FCEUX
            NewPPU = NewPPUCheck.IsChecked == true,
            SoundQuality = (int)(SoundQualityInput.Value ?? 1),
            GameGenie = GameGenieCheck.IsChecked == true,
        };
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
            string romDir = RomDirTextBox.Text ?? "";
            string dir = !string.IsNullOrEmpty(romDir) && Directory.Exists(romDir)
                ? romDir
                : Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            output = Path.Combine(dir, $"{name}_config{ext}");
            OutputFileTextBox.Text = output;
        }

        GenerateButton.IsEnabled = false;
        var loc = LocalizationManager.Instance;
        ShowStatus(loc["EmuConfig_Generating"], isError: false);

        try
        {
            var options = BuildOptions();
            var progress = new Progress<string>(msg => StatusText.Text = msg);
            await EmulatorConfigGenerator.GenerateAsync(options, output, progress);

            ShowStatus(string.Format(loc["EmuConfig_Generated"], output), isError: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ShowStatus(string.Format(loc["Common_ErrorFormat"], ex.Message), isError: true);
        }
        finally
        {
            GenerateButton.IsEnabled = true;
        }
    }

    private async void ExportToRetroArch_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ExportToRetroArchButton.IsEnabled = false;
        var loc = LocalizationManager.Instance;

        try
        {
            string? configPath = RetroArchLauncher.GetRetroArchConfigFilePath();
            if (configPath == null)
            {
                ShowStatus(loc["EmuConfig_RetroArchNotDetected"], isError: true);
                return;
            }

            var options = BuildOptions();
            var progress = new Progress<string>(msg => StatusText.Text = msg);
            await EmulatorConfigGenerator.GenerateAsync(options, configPath, progress);

            ShowStatus(string.Format(loc["EmuConfig_ExportedToRetroArch"], configPath), isError: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ShowStatus(string.Format(loc["EmuConfig_ExportError"], ex.Message), isError: true);
        }
        finally
        {
            ExportToRetroArchButton.IsEnabled = true;
        }
    }

    private void ShowStatus(string message, bool isError)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError ? ErrorBrush : SuccessBrush;
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
