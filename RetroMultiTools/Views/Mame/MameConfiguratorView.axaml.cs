using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using RetroMultiTools.Localization;
using RetroMultiTools.Utilities.Mame;
using System.Globalization;

namespace RetroMultiTools.Views.Mame;

/// <summary>
/// Full MAME Configurator view that loads, edits, and saves mame.ini files.
/// Supports all MAME configuration options and is compatible with all MAME
/// installations on Windows, macOS, and Linux (x64 + arm64).
/// </summary>
public partial class MameConfiguratorView : UserControl
{
    private static readonly IBrush StatusErrorBrush = new SolidColorBrush(Color.Parse("#F38BA8"));
    private static readonly IBrush StatusSuccessBrush = new SolidColorBrush(Color.Parse("#A6E3A1"));

    private MameIniParser? _parser;
    private readonly DispatcherTimer _statusTimer;
    private List<(Border Section, string SearchableText)>? _searchIndex;
    private bool _isDirty;
    private bool _isPopulating;
    private bool _changeTrackingAttached;

    public MameConfiguratorView()
    {
        InitializeComponent();
        InitializeComboBoxes();
        AttachSliderHandlers();
        SearchTextBox.TextChanged += SearchTextBox_TextChanged;

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _statusTimer.Tick += (_, _) =>
        {
            StatusBorder.IsVisible = false;
            _statusTimer.Stop();
        };
    }

    // ── ComboBox initialization ───────────────────────────────────────

    private void InitializeComboBoxes()
    {
        // Video backends
        Cfg_video.ItemsSource = new[] { "auto", "bgfx", "d3d", "gdi", "opengl", "soft", "accel" };
        Cfg_video.SelectedItem = "auto";

        // Monitor provider
        Cfg_monitorprovider.ItemsSource = new[] { "auto", "sdl", "win32", "dxgi" };
        Cfg_monitorprovider.SelectedItem = "auto";

        // Sound backends
        Cfg_sound.ItemsSource = new[] { "auto", "dsound", "coreaudio", "sdl", "xaudio2", "portaudio", "pulseaudio", "none" };
        Cfg_sound.SelectedItem = "auto";

        // Sample rates
        Cfg_samplerate.ItemsSource = new[] { "11025", "22050", "44100", "48000", "96000" };
        Cfg_samplerate.SelectedItem = "48000";

        // Input device types (used by multiple auto-enable combos)
        string[] deviceTypes = ["keyboard", "mouse", "joystick", "lightgun", "none"];

        Cfg_paddle_device.ItemsSource = deviceTypes;
        Cfg_adstick_device.ItemsSource = deviceTypes;
        Cfg_pedal_device.ItemsSource = deviceTypes;
        Cfg_dial_device.ItemsSource = deviceTypes;
        Cfg_trackball_device.ItemsSource = deviceTypes;
        Cfg_lightgun_device.ItemsSource = deviceTypes;
        Cfg_positional_device.ItemsSource = deviceTypes;
        Cfg_mouse_device.ItemsSource = deviceTypes;

        // Input providers
        string[] inputProviders = ["auto", "sdl", "win32", "dinput", "xinput", "rawinput", "x11", "udev", "none"];

        Cfg_keyboardprovider.ItemsSource = inputProviders;
        Cfg_mouseprovider.ItemsSource = inputProviders;
        Cfg_lightgunprovider.ItemsSource = inputProviders;
        Cfg_joystickprovider.ItemsSource = inputProviders;

        // BGFX backends
        Cfg_bgfx_backend.ItemsSource = new[] { "auto", "d3d9", "d3d11", "d3d12", "opengl", "vulkan", "metal" };
        Cfg_bgfx_backend.SelectedItem = "auto";

        // UI mode
        Cfg_ui.ItemsSource = new[] { "cabinet", "simple" };
        Cfg_ui.SelectedItem = "cabinet";
    }

    private void AttachSliderHandlers()
    {
        Cfg_brightness.PropertyChanged += (_, e) =>
        {
            if (e.Property == Slider.ValueProperty)
                Cfg_brightness_label.Text = Cfg_brightness.Value.ToString("F2", CultureInfo.InvariantCulture);
        };
        Cfg_contrast.PropertyChanged += (_, e) =>
        {
            if (e.Property == Slider.ValueProperty)
                Cfg_contrast_label.Text = Cfg_contrast.Value.ToString("F2", CultureInfo.InvariantCulture);
        };
        Cfg_gamma.PropertyChanged += (_, e) =>
        {
            if (e.Property == Slider.ValueProperty)
                Cfg_gamma_label.Text = Cfg_gamma.Value.ToString("F2", CultureInfo.InvariantCulture);
        };
        Cfg_pause_brightness.PropertyChanged += (_, e) =>
        {
            if (e.Property == Slider.ValueProperty)
                Cfg_pause_brightness_label.Text = Cfg_pause_brightness.Value.ToString("F2", CultureInfo.InvariantCulture);
        };
        Cfg_volume.PropertyChanged += (_, e) =>
        {
            if (e.Property == Slider.ValueProperty)
                Cfg_volume_label.Text = $"{(int)Cfg_volume.Value} dB";
        };

        // Initialize labels
        Cfg_brightness_label.Text = "1.00";
        Cfg_contrast_label.Text = "1.00";
        Cfg_gamma_label.Text = "1.00";
        Cfg_pause_brightness_label.Text = "0.65";
        Cfg_volume_label.Text = "0 dB";
    }

    // ── File selection ────────────────────────────────────────────────

    private async void BrowseIniButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isDirty && !await ConfirmDiscardChanges()) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var loc = LocalizationManager.Instance;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = loc["MameConfig_SelectIniTitle"],
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(loc["MameConfig_IniFileType"]) { Patterns = ["*.ini"] },
                FilePickerFileTypes.All
            ]
        });

        if (files.Count == 0) return;

        string path = files[0].Path.LocalPath;
        LoadIniFile(path);
    }

    private async void AutoDetectIniButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isDirty && !await ConfirmDiscardChanges()) return;

        var loc = LocalizationManager.Instance;
        string? iniPath = MameIniParser.FindMameIni();

        if (iniPath != null)
        {
            LoadIniFile(iniPath);
        }
        else
        {
            ShowStatus(loc["MameConfig_NoIniFound"], isError: true);
        }
    }

    // ── Load / Save ──────────────────────────────────────────────────

    private void LoadIniFile(string path)
    {
        var loc = LocalizationManager.Instance;

        try
        {
            _parser = MameIniParser.Load(path);
            IniFilePathTextBox.Text = path;
            PopulateUI();
            ConfigTabs.IsVisible = true;
            ActionPanel.IsVisible = true;
            SearchBorder.IsVisible = true;
            BuildSearchIndex();
            AttachChangeTracking();
            ClearDirty();
            ShowStatus(string.Format(loc["MameConfig_Loaded"], _parser.Count, path), isError: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            ShowStatus(string.Format(loc["MameConfig_LoadError"], ex.Message), isError: true);
            _parser = null;
            ConfigTabs.IsVisible = false;
            ActionPanel.IsVisible = false;
            SearchBorder.IsVisible = false;
        }
    }

    private void SaveButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        if (_parser == null) return;

        CollectFromUI();

        try
        {
            _parser.Save();
            ClearDirty();
            ShowStatus(string.Format(loc["MameConfig_Saved"], _parser.FilePath), isError: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            ShowStatus(string.Format(loc["MameConfig_SaveError"], ex.Message), isError: true);
        }
    }

    private async void SaveAsButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        if (_parser == null) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        CollectFromUI();

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = loc["MameConfig_SaveDialogTitle"],
            SuggestedFileName = "mame.ini",
            FileTypeChoices =
            [
                new FilePickerFileType(loc["MameConfig_IniFileType"]) { Patterns = ["*.ini"] }
            ]
        });

        if (file == null) return;

        try
        {
            string outputPath = file.Path.LocalPath;
            _parser.Save(outputPath);
            IniFilePathTextBox.Text = outputPath;
            ClearDirty();
            ShowStatus(string.Format(loc["MameConfig_Saved"], outputPath), isError: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            ShowStatus(string.Format(loc["MameConfig_SaveError"], ex.Message), isError: true);
        }
    }

    private async void ResetDefaultsButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is Window parentWindow)
        {
            var confirmDialog = new Views.Dialogs.ConfirmDialog(
                loc["MameConfig_ConfirmResetTitle"],
                loc["MameConfig_ConfirmResetMessage"]);
            bool confirmed = await confirmDialog.ShowDialog(parentWindow);
            if (!confirmed) return;
        }

        string? filePath = _parser?.FilePath;

        _parser = MameIniParser.CreateWithDefaults(filePath);
        PopulateUI();
        BuildSearchIndex();
        ClearDirty();
        ShowStatus(loc["MameConfig_DefaultsRestored"], isError: false);
    }

    private async void CreateNewButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;

        if (_isDirty && !await ConfirmDiscardChanges()) return;

        string defaultPath = MameIniParser.GetDefaultIniPath();

        _parser = MameIniParser.CreateWithDefaults(defaultPath);
        IniFilePathTextBox.Text = defaultPath;
        PopulateUI();
        ConfigTabs.IsVisible = true;
        ActionPanel.IsVisible = true;
        SearchBorder.IsVisible = true;
        BuildSearchIndex();
        AttachChangeTracking();
        ClearDirty();
        ShowStatus(string.Format(loc["MameConfig_NewCreated"], defaultPath), isError: false);
    }

    // ── UI ↔ Parser synchronization ──────────────────────────────────

    private void PopulateUI()
    {
        if (_parser == null) return;

        _isPopulating = true;

        // ── Paths ──
        SetText(Cfg_homepath, _parser.GetString("homepath", "."));
        SetText(Cfg_rompath, _parser.GetString("rompath", "roms"));
        SetText(Cfg_hashpath, _parser.GetString("hashpath", "hash"));
        SetText(Cfg_samplepath, _parser.GetString("samplepath", "samples"));
        SetText(Cfg_artpath, _parser.GetString("artpath", "artwork"));
        SetText(Cfg_ctrlrpath, _parser.GetString("ctrlrpath", "ctrlr"));
        SetText(Cfg_inipath, _parser.GetString("inipath", ".;ini"));
        SetText(Cfg_fontpath, _parser.GetString("fontpath", "."));
        SetText(Cfg_cheatpath, _parser.GetString("cheatpath", "cheat"));
        SetText(Cfg_crosshairpath, _parser.GetString("crosshairpath", "crosshair"));
        SetText(Cfg_pluginspath, _parser.GetString("pluginspath", "plugins"));
        SetText(Cfg_languagepath, _parser.GetString("languagepath", "language"));
        SetText(Cfg_swpath, _parser.GetString("swpath", "software"));

        // ── Output Directories ──
        SetText(Cfg_cfg_directory, _parser.GetString("cfg_directory", "cfg"));
        SetText(Cfg_nvram_directory, _parser.GetString("nvram_directory", "nvram"));
        SetText(Cfg_input_directory, _parser.GetString("input_directory", "inp"));
        SetText(Cfg_state_directory, _parser.GetString("state_directory", "sta"));
        SetText(Cfg_snapshot_directory, _parser.GetString("snapshot_directory", "snap"));
        SetText(Cfg_diff_directory, _parser.GetString("diff_directory", "diff"));
        SetText(Cfg_comment_directory, _parser.GetString("comment_directory", "comments"));
        SetText(Cfg_share_directory, _parser.GetString("share_directory", "share"));

        // ── Video ──
        SetCombo(Cfg_video, _parser.GetString("video", "auto"));
        Cfg_numscreens.Value = _parser.GetInt("numscreens", 1);
        Cfg_window.IsChecked = _parser.GetBool("window");
        Cfg_maximize.IsChecked = _parser.GetBool("maximize", true);
        Cfg_waitvsync.IsChecked = _parser.GetBool("waitvsync");
        Cfg_syncrefresh.IsChecked = _parser.GetBool("syncrefresh");
        SetCombo(Cfg_monitorprovider, _parser.GetString("monitorprovider", "auto"));

        // Screen
        Cfg_brightness.Value = _parser.GetDouble("brightness", 1.0);
        Cfg_contrast.Value = _parser.GetDouble("contrast", 1.0);
        Cfg_gamma.Value = _parser.GetDouble("gamma", 1.0);
        Cfg_pause_brightness.Value = _parser.GetDouble("pause_brightness", 0.65);
        SetText(Cfg_effect, _parser.GetString("effect", "none"));

        // Rotation
        Cfg_rotate.IsChecked = _parser.GetBool("rotate", true);
        Cfg_ror.IsChecked = _parser.GetBool("ror");
        Cfg_rol.IsChecked = _parser.GetBool("rol");
        Cfg_autoror.IsChecked = _parser.GetBool("autoror");
        Cfg_autorol.IsChecked = _parser.GetBool("autorol");
        Cfg_flipx.IsChecked = _parser.GetBool("flipx");
        Cfg_flipy.IsChecked = _parser.GetBool("flipy");

        // Artwork
        Cfg_artwork_crop.IsChecked = _parser.GetBool("artwork_crop");
        SetText(Cfg_fallback_artwork, _parser.GetString("fallback_artwork"));
        SetText(Cfg_override_artwork, _parser.GetString("override_artwork"));

        // Vector
        Cfg_beam_width_min.Value = (decimal)_parser.GetDouble("beam_width_min", 1.0);
        Cfg_beam_width_max.Value = (decimal)_parser.GetDouble("beam_width_max", 1.0);
        Cfg_beam_dot_size.Value = (decimal)_parser.GetDouble("beam_dot_size", 1.0);
        Cfg_beam_intensity_weight.Value = (decimal)_parser.GetDouble("beam_intensity_weight", 0);
        Cfg_flicker.Value = _parser.GetInt("flicker", 0);

        // OSD Video
        Cfg_switchres.IsChecked = _parser.GetBool("switchres");
        Cfg_filter.IsChecked = _parser.GetBool("filter", true);
        Cfg_prescale.Value = _parser.GetInt("prescale", 1);
        SetText(Cfg_screen, _parser.GetString("screen", "auto"));
        SetText(Cfg_aspect, _parser.GetString("aspect", "auto"));
        SetText(Cfg_resolution, _parser.GetString("resolution", "auto"));
        SetText(Cfg_view, _parser.GetString("view", "auto"));

        // Per-window video
        SetText(Cfg_screen0, _parser.GetString("screen0", "auto"));
        SetText(Cfg_aspect0, _parser.GetString("aspect0", "auto"));
        SetText(Cfg_resolution0, _parser.GetString("resolution0", "auto"));
        SetText(Cfg_view0, _parser.GetString("view0", "auto"));
        SetText(Cfg_screen1, _parser.GetString("screen1", "auto"));
        SetText(Cfg_aspect1, _parser.GetString("aspect1", "auto"));
        SetText(Cfg_resolution1, _parser.GetString("resolution1", "auto"));
        SetText(Cfg_view1, _parser.GetString("view1", "auto"));
        SetText(Cfg_screen2, _parser.GetString("screen2", "auto"));
        SetText(Cfg_aspect2, _parser.GetString("aspect2", "auto"));
        SetText(Cfg_resolution2, _parser.GetString("resolution2", "auto"));
        SetText(Cfg_view2, _parser.GetString("view2", "auto"));
        SetText(Cfg_screen3, _parser.GetString("screen3", "auto"));
        SetText(Cfg_aspect3, _parser.GetString("aspect3", "auto"));
        SetText(Cfg_resolution3, _parser.GetString("resolution3", "auto"));
        SetText(Cfg_view3, _parser.GetString("view3", "auto"));

        // BGFX
        SetText(Cfg_bgfx_path, _parser.GetString("bgfx_path", "bgfx"));
        SetCombo(Cfg_bgfx_backend, _parser.GetString("bgfx_backend", "auto"));
        Cfg_bgfx_debug.IsChecked = _parser.GetBool("bgfx_debug");
        SetText(Cfg_bgfx_screen_chains, _parser.GetString("bgfx_screen_chains", "default"));
        SetText(Cfg_bgfx_shadow_mask, _parser.GetString("bgfx_shadow_mask", "slot-mask.png"));
        SetText(Cfg_bgfx_lut, _parser.GetString("bgfx_lut"));
        SetText(Cfg_bgfx_avi_name, _parser.GetString("bgfx_avi_name", "auto"));

        // ── Sound ──
        SetCombo(Cfg_sound, _parser.GetString("sound", "auto"));
        SetCombo(Cfg_samplerate, _parser.GetString("samplerate", "48000"));
        Cfg_samples.IsChecked = _parser.GetBool("samples", true);
        Cfg_volume.Value = _parser.GetInt("volume", 0);
        Cfg_compressor.IsChecked = _parser.GetBool("compressor", true);
        Cfg_speaker_report.Value = _parser.GetInt("speaker_report", 0);
        Cfg_audio_latency.Value = _parser.GetInt("audio_latency", 1);

        // ── Input ──
        Cfg_coin_lockout.IsChecked = _parser.GetBool("coin_lockout", true);
        Cfg_coin_impulse.Value = _parser.GetInt("coin_impulse", 0);
        SetText(Cfg_ctrlr, _parser.GetString("ctrlr"));
        Cfg_mouse.IsChecked = _parser.GetBool("mouse");
        Cfg_joystick.IsChecked = _parser.GetBool("joystick", true);
        Cfg_lightgun.IsChecked = _parser.GetBool("lightgun");
        Cfg_multikeyboard.IsChecked = _parser.GetBool("multikeyboard");
        Cfg_multimouse.IsChecked = _parser.GetBool("multimouse");
        Cfg_steadykey.IsChecked = _parser.GetBool("steadykey");
        Cfg_ui_active.IsChecked = _parser.GetBool("ui_active");
        Cfg_offscreen_reload.IsChecked = _parser.GetBool("offscreen_reload");
        SetText(Cfg_joystick_map, _parser.GetString("joystick_map", "auto"));
        Cfg_joystick_deadzone.Value = (decimal)_parser.GetDouble("joystick_deadzone", 0.15);
        Cfg_joystick_saturation.Value = (decimal)_parser.GetDouble("joystick_saturation", 0.85);
        Cfg_joystick_threshold.Value = (decimal)_parser.GetDouble("joystick_threshold", 0.3);
        Cfg_natural.IsChecked = _parser.GetBool("natural");
        Cfg_joystick_contradictory.IsChecked = _parser.GetBool("joystick_contradictory");

        // Auto-enable devices
        SetCombo(Cfg_paddle_device, _parser.GetString("paddle_device", "keyboard"));
        SetCombo(Cfg_adstick_device, _parser.GetString("adstick_device", "keyboard"));
        SetCombo(Cfg_pedal_device, _parser.GetString("pedal_device", "keyboard"));
        SetCombo(Cfg_dial_device, _parser.GetString("dial_device", "keyboard"));
        SetCombo(Cfg_trackball_device, _parser.GetString("trackball_device", "keyboard"));
        SetCombo(Cfg_lightgun_device, _parser.GetString("lightgun_device", "keyboard"));
        SetCombo(Cfg_positional_device, _parser.GetString("positional_device", "keyboard"));
        SetCombo(Cfg_mouse_device, _parser.GetString("mouse_device", "mouse"));

        // OSD Input providers
        SetCombo(Cfg_keyboardprovider, _parser.GetString("keyboardprovider", "auto"));
        SetCombo(Cfg_mouseprovider, _parser.GetString("mouseprovider", "auto"));
        SetCombo(Cfg_lightgunprovider, _parser.GetString("lightgunprovider", "auto"));
        SetCombo(Cfg_joystickprovider, _parser.GetString("joystickprovider", "auto"));

        // ── Performance ──
        Cfg_autoframeskip.IsChecked = _parser.GetBool("autoframeskip");
        Cfg_frameskip.Value = _parser.GetInt("frameskip", 0);
        Cfg_seconds_to_run.Value = _parser.GetInt("seconds_to_run", 0);
        Cfg_throttle.IsChecked = _parser.GetBool("throttle", true);
        Cfg_sleep.IsChecked = _parser.GetBool("sleep", true);
        Cfg_speed.Value = (decimal)_parser.GetDouble("speed", 1.0);
        Cfg_refreshspeed.IsChecked = _parser.GetBool("refreshspeed");
        Cfg_lowlatency.IsChecked = _parser.GetBool("lowlatency");

        // ── State/Playback ──
        SetText(Cfg_state, _parser.GetString("state"));
        Cfg_autosave.IsChecked = _parser.GetBool("autosave");
        Cfg_rewind.IsChecked = _parser.GetBool("rewind");
        Cfg_rewind_capacity.Value = _parser.GetInt("rewind_capacity", 100);
        SetText(Cfg_playback, _parser.GetString("playback"));
        SetText(Cfg_record, _parser.GetString("record"));
        Cfg_exit_after_playback.IsChecked = _parser.GetBool("exit_after_playback");
        SetText(Cfg_mngwrite, _parser.GetString("mngwrite"));
        SetText(Cfg_aviwrite, _parser.GetString("aviwrite"));
        SetText(Cfg_wavwrite, _parser.GetString("wavwrite"));
        SetText(Cfg_snapname, _parser.GetString("snapname", "%g/%i"));
        SetText(Cfg_snapsize, _parser.GetString("snapsize", "auto"));
        SetText(Cfg_snapview, _parser.GetString("snapview", "internal"));
        Cfg_snapbilinear.IsChecked = _parser.GetBool("snapbilinear", true);
        SetText(Cfg_statename, _parser.GetString("statename", "%g"));
        Cfg_burnin.IsChecked = _parser.GetBool("burnin");

        // ── Misc ──
        Cfg_readconfig.IsChecked = _parser.GetBool("readconfig", true);
        Cfg_drc.IsChecked = _parser.GetBool("drc", true);
        Cfg_drc_use_c.IsChecked = _parser.GetBool("drc_use_c");
        SetText(Cfg_bios, _parser.GetString("bios"));
        Cfg_cheat.IsChecked = _parser.GetBool("cheat");
        Cfg_skip_gameinfo.IsChecked = _parser.GetBool("skip_gameinfo");
        Cfg_skip_warnings.IsChecked = _parser.GetBool("skip_warnings");
        SetText(Cfg_uifont, _parser.GetString("uifont", "default"));
        SetCombo(Cfg_ui, _parser.GetString("ui", "cabinet"));
        SetText(Cfg_ramsize, _parser.GetString("ramsize"));
        Cfg_confirm_quit.IsChecked = _parser.GetBool("confirm_quit");
        Cfg_ui_mouse.IsChecked = _parser.GetBool("ui_mouse", true);
        Cfg_nvram_save.IsChecked = _parser.GetBool("nvram_save", true);

        // Language
        SetText(Cfg_language, _parser.GetString("language"));

        // Debugging
        Cfg_verbose.IsChecked = _parser.GetBool("verbose");
        Cfg_log.IsChecked = _parser.GetBool("log");
        Cfg_oslog.IsChecked = _parser.GetBool("oslog");
        Cfg_debug.IsChecked = _parser.GetBool("debug");
        Cfg_update_in_pause.IsChecked = _parser.GetBool("update_in_pause");
        SetText(Cfg_debugscript, _parser.GetString("debugscript"));
        Cfg_debuglog.IsChecked = _parser.GetBool("debuglog");

        // Communication
        SetText(Cfg_comm_localhost, _parser.GetString("comm_localhost", "0.0.0.0"));
        SetText(Cfg_comm_localport, _parser.GetString("comm_localport", "15112"));
        SetText(Cfg_comm_remotehost, _parser.GetString("comm_remotehost", "127.0.0.1"));
        SetText(Cfg_comm_remoteport, _parser.GetString("comm_remoteport", "15112"));
        Cfg_comm_framesync.IsChecked = _parser.GetBool("comm_framesync");

        // Scripting
        SetText(Cfg_autoboot_command, _parser.GetString("autoboot_command"));
        Cfg_autoboot_delay.Value = _parser.GetInt("autoboot_delay", 0);
        SetText(Cfg_autoboot_script, _parser.GetString("autoboot_script"));
        Cfg_console.IsChecked = _parser.GetBool("console");
        Cfg_plugins.IsChecked = _parser.GetBool("plugins", true);
        SetText(Cfg_plugin, _parser.GetString("plugin"));
        SetText(Cfg_noplugin, _parser.GetString("noplugin"));

        // HTTP Server
        Cfg_http.IsChecked = _parser.GetBool("http");
        Cfg_http_port.Value = _parser.GetInt("http_port", 8080);
        SetText(Cfg_http_root, _parser.GetString("http_root", "web"));

        _isPopulating = false;
    }

    private void CollectFromUI()
    {
        if (_parser == null) return;

        // ── Paths ──
        _parser.SetString("homepath", Cfg_homepath.Text ?? ".");
        _parser.SetString("rompath", Cfg_rompath.Text ?? "roms");
        _parser.SetString("hashpath", Cfg_hashpath.Text ?? "hash");
        _parser.SetString("samplepath", Cfg_samplepath.Text ?? "samples");
        _parser.SetString("artpath", Cfg_artpath.Text ?? "artwork");
        _parser.SetString("ctrlrpath", Cfg_ctrlrpath.Text ?? "ctrlr");
        _parser.SetString("inipath", Cfg_inipath.Text ?? ".;ini");
        _parser.SetString("fontpath", Cfg_fontpath.Text ?? ".");
        _parser.SetString("cheatpath", Cfg_cheatpath.Text ?? "cheat");
        _parser.SetString("crosshairpath", Cfg_crosshairpath.Text ?? "crosshair");
        _parser.SetString("pluginspath", Cfg_pluginspath.Text ?? "plugins");
        _parser.SetString("languagepath", Cfg_languagepath.Text ?? "language");
        _parser.SetString("swpath", Cfg_swpath.Text ?? "software");

        // ── Output Directories ──
        _parser.SetString("cfg_directory", Cfg_cfg_directory.Text ?? "cfg");
        _parser.SetString("nvram_directory", Cfg_nvram_directory.Text ?? "nvram");
        _parser.SetString("input_directory", Cfg_input_directory.Text ?? "inp");
        _parser.SetString("state_directory", Cfg_state_directory.Text ?? "sta");
        _parser.SetString("snapshot_directory", Cfg_snapshot_directory.Text ?? "snap");
        _parser.SetString("diff_directory", Cfg_diff_directory.Text ?? "diff");
        _parser.SetString("comment_directory", Cfg_comment_directory.Text ?? "comments");
        _parser.SetString("share_directory", Cfg_share_directory.Text ?? "share");

        // ── Video ──
        _parser.SetString("video", GetCombo(Cfg_video, "auto"));
        _parser.SetInt("numscreens", (int)(Cfg_numscreens.Value ?? 1));
        _parser.SetBool("window", Cfg_window.IsChecked == true);
        _parser.SetBool("maximize", Cfg_maximize.IsChecked == true);
        _parser.SetBool("waitvsync", Cfg_waitvsync.IsChecked == true);
        _parser.SetBool("syncrefresh", Cfg_syncrefresh.IsChecked == true);
        _parser.SetString("monitorprovider", GetCombo(Cfg_monitorprovider, "auto"));

        // Screen
        _parser.SetDouble("brightness", Cfg_brightness.Value);
        _parser.SetDouble("contrast", Cfg_contrast.Value);
        _parser.SetDouble("gamma", Cfg_gamma.Value);
        _parser.SetDouble("pause_brightness", Cfg_pause_brightness.Value);
        _parser.SetString("effect", Cfg_effect.Text ?? "none");

        // Rotation
        _parser.SetBool("rotate", Cfg_rotate.IsChecked == true);
        _parser.SetBool("ror", Cfg_ror.IsChecked == true);
        _parser.SetBool("rol", Cfg_rol.IsChecked == true);
        _parser.SetBool("autoror", Cfg_autoror.IsChecked == true);
        _parser.SetBool("autorol", Cfg_autorol.IsChecked == true);
        _parser.SetBool("flipx", Cfg_flipx.IsChecked == true);
        _parser.SetBool("flipy", Cfg_flipy.IsChecked == true);

        // Artwork
        _parser.SetBool("artwork_crop", Cfg_artwork_crop.IsChecked == true);
        _parser.SetString("fallback_artwork", Cfg_fallback_artwork.Text ?? "");
        _parser.SetString("override_artwork", Cfg_override_artwork.Text ?? "");

        // Vector
        _parser.SetDouble("beam_width_min", (double)(Cfg_beam_width_min.Value ?? 1.0m));
        _parser.SetDouble("beam_width_max", (double)(Cfg_beam_width_max.Value ?? 1.0m));
        _parser.SetDouble("beam_dot_size", (double)(Cfg_beam_dot_size.Value ?? 1.0m));
        _parser.SetDouble("beam_intensity_weight", (double)(Cfg_beam_intensity_weight.Value ?? 0m));
        _parser.SetInt("flicker", (int)(Cfg_flicker.Value ?? 0));

        // OSD Video
        _parser.SetBool("switchres", Cfg_switchres.IsChecked == true);
        _parser.SetBool("filter", Cfg_filter.IsChecked == true);
        _parser.SetInt("prescale", (int)(Cfg_prescale.Value ?? 1));
        _parser.SetString("screen", Cfg_screen.Text ?? "auto");
        _parser.SetString("aspect", Cfg_aspect.Text ?? "auto");
        _parser.SetString("resolution", Cfg_resolution.Text ?? "auto");
        _parser.SetString("view", Cfg_view.Text ?? "auto");

        // Per-window video
        _parser.SetString("screen0", Cfg_screen0.Text ?? "auto");
        _parser.SetString("aspect0", Cfg_aspect0.Text ?? "auto");
        _parser.SetString("resolution0", Cfg_resolution0.Text ?? "auto");
        _parser.SetString("view0", Cfg_view0.Text ?? "auto");
        _parser.SetString("screen1", Cfg_screen1.Text ?? "auto");
        _parser.SetString("aspect1", Cfg_aspect1.Text ?? "auto");
        _parser.SetString("resolution1", Cfg_resolution1.Text ?? "auto");
        _parser.SetString("view1", Cfg_view1.Text ?? "auto");
        _parser.SetString("screen2", Cfg_screen2.Text ?? "auto");
        _parser.SetString("aspect2", Cfg_aspect2.Text ?? "auto");
        _parser.SetString("resolution2", Cfg_resolution2.Text ?? "auto");
        _parser.SetString("view2", Cfg_view2.Text ?? "auto");
        _parser.SetString("screen3", Cfg_screen3.Text ?? "auto");
        _parser.SetString("aspect3", Cfg_aspect3.Text ?? "auto");
        _parser.SetString("resolution3", Cfg_resolution3.Text ?? "auto");
        _parser.SetString("view3", Cfg_view3.Text ?? "auto");

        // BGFX
        _parser.SetString("bgfx_path", Cfg_bgfx_path.Text ?? "bgfx");
        _parser.SetString("bgfx_backend", GetCombo(Cfg_bgfx_backend, "auto"));
        _parser.SetBool("bgfx_debug", Cfg_bgfx_debug.IsChecked == true);
        _parser.SetString("bgfx_screen_chains", Cfg_bgfx_screen_chains.Text ?? "default");
        _parser.SetString("bgfx_shadow_mask", Cfg_bgfx_shadow_mask.Text ?? "slot-mask.png");
        _parser.SetString("bgfx_lut", Cfg_bgfx_lut.Text ?? "");
        _parser.SetString("bgfx_avi_name", Cfg_bgfx_avi_name.Text ?? "auto");

        // ── Sound ──
        _parser.SetString("sound", GetCombo(Cfg_sound, "auto"));
        _parser.SetString("samplerate", GetCombo(Cfg_samplerate, "48000"));
        _parser.SetBool("samples", Cfg_samples.IsChecked == true);
        _parser.SetInt("volume", (int)Cfg_volume.Value);
        _parser.SetBool("compressor", Cfg_compressor.IsChecked == true);
        _parser.SetInt("speaker_report", (int)(Cfg_speaker_report.Value ?? 0));
        _parser.SetInt("audio_latency", (int)(Cfg_audio_latency.Value ?? 1));

        // ── Input ──
        _parser.SetBool("coin_lockout", Cfg_coin_lockout.IsChecked == true);
        _parser.SetInt("coin_impulse", (int)(Cfg_coin_impulse.Value ?? 0));
        _parser.SetString("ctrlr", Cfg_ctrlr.Text ?? "");
        _parser.SetBool("mouse", Cfg_mouse.IsChecked == true);
        _parser.SetBool("joystick", Cfg_joystick.IsChecked == true);
        _parser.SetBool("lightgun", Cfg_lightgun.IsChecked == true);
        _parser.SetBool("multikeyboard", Cfg_multikeyboard.IsChecked == true);
        _parser.SetBool("multimouse", Cfg_multimouse.IsChecked == true);
        _parser.SetBool("steadykey", Cfg_steadykey.IsChecked == true);
        _parser.SetBool("ui_active", Cfg_ui_active.IsChecked == true);
        _parser.SetBool("offscreen_reload", Cfg_offscreen_reload.IsChecked == true);
        _parser.SetString("joystick_map", Cfg_joystick_map.Text ?? "auto");
        _parser.SetDouble("joystick_deadzone", (double)(Cfg_joystick_deadzone.Value ?? 0.15m));
        _parser.SetDouble("joystick_saturation", (double)(Cfg_joystick_saturation.Value ?? 0.85m));
        _parser.SetDouble("joystick_threshold", (double)(Cfg_joystick_threshold.Value ?? 0.3m));
        _parser.SetBool("natural", Cfg_natural.IsChecked == true);
        _parser.SetBool("joystick_contradictory", Cfg_joystick_contradictory.IsChecked == true);

        // Auto-enable devices
        _parser.SetString("paddle_device", GetCombo(Cfg_paddle_device, "keyboard"));
        _parser.SetString("adstick_device", GetCombo(Cfg_adstick_device, "keyboard"));
        _parser.SetString("pedal_device", GetCombo(Cfg_pedal_device, "keyboard"));
        _parser.SetString("dial_device", GetCombo(Cfg_dial_device, "keyboard"));
        _parser.SetString("trackball_device", GetCombo(Cfg_trackball_device, "keyboard"));
        _parser.SetString("lightgun_device", GetCombo(Cfg_lightgun_device, "keyboard"));
        _parser.SetString("positional_device", GetCombo(Cfg_positional_device, "keyboard"));
        _parser.SetString("mouse_device", GetCombo(Cfg_mouse_device, "mouse"));

        // OSD Input providers
        _parser.SetString("keyboardprovider", GetCombo(Cfg_keyboardprovider, "auto"));
        _parser.SetString("mouseprovider", GetCombo(Cfg_mouseprovider, "auto"));
        _parser.SetString("lightgunprovider", GetCombo(Cfg_lightgunprovider, "auto"));
        _parser.SetString("joystickprovider", GetCombo(Cfg_joystickprovider, "auto"));

        // ── Performance ──
        _parser.SetBool("autoframeskip", Cfg_autoframeskip.IsChecked == true);
        _parser.SetInt("frameskip", (int)(Cfg_frameskip.Value ?? 0));
        _parser.SetInt("seconds_to_run", (int)(Cfg_seconds_to_run.Value ?? 0));
        _parser.SetBool("throttle", Cfg_throttle.IsChecked == true);
        _parser.SetBool("sleep", Cfg_sleep.IsChecked == true);
        _parser.SetDouble("speed", (double)(Cfg_speed.Value ?? 1.0m));
        _parser.SetBool("refreshspeed", Cfg_refreshspeed.IsChecked == true);
        _parser.SetBool("lowlatency", Cfg_lowlatency.IsChecked == true);

        // ── State/Playback ──
        _parser.SetString("state", Cfg_state.Text ?? "");
        _parser.SetBool("autosave", Cfg_autosave.IsChecked == true);
        _parser.SetBool("rewind", Cfg_rewind.IsChecked == true);
        _parser.SetInt("rewind_capacity", (int)(Cfg_rewind_capacity.Value ?? 100));
        _parser.SetString("playback", Cfg_playback.Text ?? "");
        _parser.SetString("record", Cfg_record.Text ?? "");
        _parser.SetBool("exit_after_playback", Cfg_exit_after_playback.IsChecked == true);
        _parser.SetString("mngwrite", Cfg_mngwrite.Text ?? "");
        _parser.SetString("aviwrite", Cfg_aviwrite.Text ?? "");
        _parser.SetString("wavwrite", Cfg_wavwrite.Text ?? "");
        _parser.SetString("snapname", Cfg_snapname.Text ?? "%g/%i");
        _parser.SetString("snapsize", Cfg_snapsize.Text ?? "auto");
        _parser.SetString("snapview", Cfg_snapview.Text ?? "internal");
        _parser.SetBool("snapbilinear", Cfg_snapbilinear.IsChecked == true);
        _parser.SetString("statename", Cfg_statename.Text ?? "%g");
        _parser.SetBool("burnin", Cfg_burnin.IsChecked == true);

        // ── Misc ──
        _parser.SetBool("readconfig", Cfg_readconfig.IsChecked == true);
        _parser.SetBool("drc", Cfg_drc.IsChecked == true);
        _parser.SetBool("drc_use_c", Cfg_drc_use_c.IsChecked == true);
        _parser.SetString("bios", Cfg_bios.Text ?? "");
        _parser.SetBool("cheat", Cfg_cheat.IsChecked == true);
        _parser.SetBool("skip_gameinfo", Cfg_skip_gameinfo.IsChecked == true);
        _parser.SetBool("skip_warnings", Cfg_skip_warnings.IsChecked == true);
        _parser.SetString("uifont", Cfg_uifont.Text ?? "default");
        _parser.SetString("ui", GetCombo(Cfg_ui, "cabinet"));
        _parser.SetString("ramsize", Cfg_ramsize.Text ?? "");
        _parser.SetBool("confirm_quit", Cfg_confirm_quit.IsChecked == true);
        _parser.SetBool("ui_mouse", Cfg_ui_mouse.IsChecked == true);
        _parser.SetBool("nvram_save", Cfg_nvram_save.IsChecked == true);

        // Language
        _parser.SetString("language", Cfg_language.Text ?? "");

        // Debugging
        _parser.SetBool("verbose", Cfg_verbose.IsChecked == true);
        _parser.SetBool("log", Cfg_log.IsChecked == true);
        _parser.SetBool("oslog", Cfg_oslog.IsChecked == true);
        _parser.SetBool("debug", Cfg_debug.IsChecked == true);
        _parser.SetBool("update_in_pause", Cfg_update_in_pause.IsChecked == true);
        _parser.SetString("debugscript", Cfg_debugscript.Text ?? "");
        _parser.SetBool("debuglog", Cfg_debuglog.IsChecked == true);

        // Communication
        _parser.SetString("comm_localhost", Cfg_comm_localhost.Text ?? "0.0.0.0");
        _parser.SetString("comm_localport", Cfg_comm_localport.Text ?? "15112");
        _parser.SetString("comm_remotehost", Cfg_comm_remotehost.Text ?? "127.0.0.1");
        _parser.SetString("comm_remoteport", Cfg_comm_remoteport.Text ?? "15112");
        _parser.SetBool("comm_framesync", Cfg_comm_framesync.IsChecked == true);

        // Scripting
        _parser.SetString("autoboot_command", Cfg_autoboot_command.Text ?? "");
        _parser.SetInt("autoboot_delay", (int)(Cfg_autoboot_delay.Value ?? 0));
        _parser.SetString("autoboot_script", Cfg_autoboot_script.Text ?? "");
        _parser.SetBool("console", Cfg_console.IsChecked == true);
        _parser.SetBool("plugins", Cfg_plugins.IsChecked == true);
        _parser.SetString("plugin", Cfg_plugin.Text ?? "");
        _parser.SetString("noplugin", Cfg_noplugin.Text ?? "");

        // HTTP Server
        _parser.SetBool("http", Cfg_http.IsChecked == true);
        _parser.SetInt("http_port", (int)(Cfg_http_port.Value ?? 8080));
        _parser.SetString("http_root", Cfg_http_root.Text ?? "web");
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static void SetText(TextBox textBox, string value)
    {
        textBox.Text = value;
    }

    private static void SetCombo(ComboBox combo, string value)
    {
        // Try to find and select the matching item
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (string.Equals(combo.Items[i]?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedIndex = i;
                return;
            }
        }

        // Value not in the predefined list — add it so it's preserved on save.
        // This handles custom/newer values in existing config files.
        if (!string.IsNullOrEmpty(value))
        {
            var items = combo.ItemsSource as string[];
            if (items != null)
            {
                combo.ItemsSource = items.Append(value).ToArray();
                combo.SelectedIndex = items.Length;
                return;
            }
        }

        // If not found, select the first item (usually "auto")
        if (combo.Items.Count > 0)
            combo.SelectedIndex = 0;
    }

    private static string GetCombo(ComboBox combo, string defaultValue)
    {
        return combo.SelectedItem?.ToString() ?? defaultValue;
    }

    private void ShowStatus(string message, bool isError)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError ? StatusErrorBrush : StatusSuccessBrush;
        StatusBorder.IsVisible = true;

        // Reset and restart the auto-hide timer
        _statusTimer.Stop();
        _statusTimer.Start();
    }

    // ── Search / Filter ──────────────────────────────────────────────

    /// <summary>
    /// Builds a searchable index mapping each Section_ border to a concatenated
    /// string of all its descendant TextBlock texts. Called once after UI population.
    /// </summary>
    private void BuildSearchIndex()
    {
        _searchIndex = ConfigTabs.GetVisualDescendants()
            .OfType<Border>()
            .Where(b => b.Name != null && b.Name.StartsWith("Section_", StringComparison.Ordinal))
            .Select(section =>
            {
                string text = string.Join(" ", section.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Where(tb => tb.Text != null)
                    .Select(tb => tb.Text!));
                return (Section: section, SearchableText: text);
            })
            .ToList();
    }

    private void SearchTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_searchIndex == null) return;

        string searchText = SearchTextBox.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(searchText))
        {
            foreach (var (section, _) in _searchIndex)
                section.IsVisible = true;
            SearchResultsText.Text = "";
            return;
        }

        int matchCount = 0;

        // Support space-separated search terms (all terms must match)
        string[] terms = searchText.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var (section, searchableText) in _searchIndex)
        {
            bool matches = true;
            foreach (string term in terms)
            {
                if (!searchableText.Contains(term, StringComparison.OrdinalIgnoreCase))
                {
                    matches = false;
                    break;
                }
            }
            section.IsVisible = matches;
            if (matches) matchCount++;
        }

        var loc = LocalizationManager.Instance;
        SearchResultsText.Text = string.Format(loc["MameConfig_SearchResults"], matchCount, _searchIndex.Count);
    }

    // ── Dirty state tracking ─────────────────────────────────────────

    private void MarkDirty()
    {
        if (_isPopulating || _isDirty) return;
        _isDirty = true;
        SaveButton.IsEnabled = true;
        UnsavedIndicator.IsVisible = true;
    }

    private void ClearDirty()
    {
        _isDirty = false;
        SaveButton.IsEnabled = false;
        UnsavedIndicator.IsVisible = false;
    }

    private async Task<bool> ConfirmDiscardChanges()
    {
        var loc = LocalizationManager.Instance;
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is Window parentWindow)
        {
            var dialog = new Views.Dialogs.ConfirmDialog(
                loc["MameConfig_ConfirmDiscardTitle"],
                loc["MameConfig_ConfirmDiscardMessage"]);
            return await dialog.ShowDialog(parentWindow);
        }
        return true;
    }

    private void AttachChangeTracking()
    {
        if (_changeTrackingAttached) return;
        _changeTrackingAttached = true;

        foreach (var control in ConfigTabs.GetVisualDescendants())
        {
            switch (control)
            {
                case TextBox tb:
                    tb.TextChanged += OnControlChanged;
                    break;
                case CheckBox cb:
                    cb.IsCheckedChanged += OnControlChanged;
                    break;
                case ComboBox combo:
                    combo.SelectionChanged += OnControlChanged;
                    break;
                case Slider slider:
                    slider.PropertyChanged += OnSliderPropertyChanged;
                    break;
                case NumericUpDown nud:
                    nud.ValueChanged += OnControlChanged;
                    break;
                case ToggleSwitch ts:
                    ts.IsCheckedChanged += OnControlChanged;
                    break;
            }
        }
    }

    private void OnControlChanged(object? sender, EventArgs e) => MarkDirty();

    private void OnSliderPropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Slider.ValueProperty)
            MarkDirty();
    }
}
