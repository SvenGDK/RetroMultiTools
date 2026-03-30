using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using RetroMultiTools.Localization;
using RetroMultiTools.Utilities.Mednafen;
using System.Globalization;

namespace RetroMultiTools.Views.Mednafen;

/// <summary>
/// Full Mednafen Configurator view that loads, edits, and saves mednafen.cfg files.
/// Supports all Mednafen configuration options and is compatible with all Mednafen
/// installations on Windows, macOS, and Linux (x64 + arm64).
/// </summary>
public partial class MednafenConfiguratorView : UserControl
{
    private static readonly IBrush StatusErrorBrush = new SolidColorBrush(Color.Parse("#F38BA8"));
    private static readonly IBrush StatusSuccessBrush = new SolidColorBrush(Color.Parse("#A6E3A1"));

    /// <summary>
    /// Maps display names to Mednafen module identifiers for per-system configuration.
    /// </summary>
    private static readonly (string Display, string Id)[] Systems =
    [
        ("Apple II/II+/IIe [apple2]", "apple2"),
        ("Atari Lynx [lynx]", "lynx"),
        ("CD-DA Player [cdplay]", "cdplay"),
        ("Demo [demo]", "demo"),
        ("GameBoy (Color) [gb]", "gb"),
        ("GameBoy Advance [gba]", "gba"),
        ("Sega Game Gear [gg]", "gg"),
        ("Sega Genesis/MegaDrive [md]", "md"),
        ("Neo Geo Pocket (Color) [ngp]", "ngp"),
        ("NES/Famicom [nes]", "nes"),
        ("PC Engine (CD) [pce]", "pce"),
        ("PC Engine (CD) Fast [pce_fast]", "pce_fast"),
        ("PC-FX [pcfx]", "pcfx"),
        ("Sony PlayStation [psx]", "psx"),
        ("Sega Arcade SCSP [sasplay]", "sasplay"),
        ("Sega Master System [sms]", "sms"),
        ("SNES/Super Famicom [snes]", "snes"),
        ("SNES/Super Famicom Fast [snes_faust]", "snes_faust"),
        ("Sega Saturn [ss]", "ss"),
        ("Sega Saturn Sound [ssfplay]", "ssfplay"),
        ("Virtual Boy [vb]", "vb"),
        ("WonderSwan [wswan]", "wswan"),
    ];

    private MednafenCfgParser? _parser;
    private List<(Border Section, string SearchableText)>? _searchIndex;
    private readonly DispatcherTimer _statusTimer;
    private bool _isDirty;
    private bool _isPopulating;
    private bool _changeTrackingAttached;

    /// <summary>
    /// The currently selected system module identifier (e.g. "psx", "nes").
    /// </summary>
    private string _currentSystem = Systems[0].Id;

    public MednafenConfiguratorView()
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
        // Video drivers
        Cfg_video_driver.ItemsSource = new[] { "default", "opengl", "softfb" };
        Cfg_video_driver.SelectedItem = "default";

        // Cursor visibility
        Cfg_video_cursorvis.ItemsSource = new[] { "hidden", "visible" };
        Cfg_video_cursorvis.SelectedItem = "hidden";

        // Deinterlacers
        Cfg_video_deinterlacer.ItemsSource = new[] { "weave", "bob", "bob_offset", "blend", "blend_rg" };
        Cfg_video_deinterlacer.SelectedItem = "weave";

        // GL format
        Cfg_video_glformat.ItemsSource = new[] { "auto", "truecolor", "hicolor", "rgb565", "rgb555" };
        Cfg_video_glformat.SelectedItem = "auto";

        // FPS font
        Cfg_fps_font.ItemsSource = new[] { "5x7", "6x9", "6x12", "6x13", "9x18" };
        Cfg_fps_font.SelectedItem = "5x7";

        // FPS position
        Cfg_fps_position.ItemsSource = new[] { "upper_left", "upper_right", "upper_center", "center" };
        Cfg_fps_position.SelectedItem = "upper_left";

        // Sound drivers
        Cfg_sound_driver.ItemsSource = new[] { "default", "alsa", "openbsd", "oss", "wasapish", "dsound", "wasapi", "sdl", "jack" };
        Cfg_sound_driver.SelectedItem = "default";

        // Sound rates
        Cfg_sound_rate.ItemsSource = new[] { "22050", "44100", "48000", "96000", "192000" };
        Cfg_sound_rate.SelectedItem = "48000";

        // Input grab strategy
        Cfg_input_grab_strategy.ItemsSource = new[] { "auto", "full", "kb_auto", "mouse_auto" };
        Cfg_input_grab_strategy.SelectedItem = "full";

        // Netplay console font
        Cfg_netplay_console_font.ItemsSource = new[] { "5x7", "6x9", "6x12", "6x13", "9x18" };
        Cfg_netplay_console_font.SelectedItem = "9x18";

        // QuickTime video codecs
        Cfg_qtrecord_vcodec.ItemsSource = new[] { "raw", "cscd", "png" };
        Cfg_qtrecord_vcodec.SelectedItem = "cscd";

        // Per-system ComboBoxes
        SystemSelector.ItemsSource = Systems.Select(s => s.Display).ToArray();
        SystemSelector.SelectedIndex = 0;
        SystemSelector.SelectionChanged += SystemSelector_SelectionChanged;

        // Shader types
        Cfg_sys_shader.ItemsSource = new[] { "none", "autoip", "autoipsharper", "scale2x", "sabr", "ipsharper", "ipxnoty", "ipynotx", "ipxnotysharper", "ipynotxsharper", "goat" };
        Cfg_sys_shader.SelectedItem = "none";

        // Special scalers
        Cfg_sys_special.ItemsSource = new[] { "none", "hq2x", "hq3x", "hq4x", "scale2x", "scale3x", "scale4x", "2xsai", "super2xsai", "supereagle", "nn2x", "nn3x", "nn4x", "nny2x", "nny3x", "nny4x" };
        Cfg_sys_special.SelectedItem = "none";

        // Stretch modes
        Cfg_sys_stretch.ItemsSource = new[] { "0", "full", "aspect", "aspect_int", "aspect_mult2" };
        Cfg_sys_stretch.SelectedItem = "aspect_mult2";

        // Video interpolation
        Cfg_sys_videoip.ItemsSource = new[] { "0", "1", "x", "y" };
        Cfg_sys_videoip.SelectedItem = "0";

        // Goat shader pattern
        Cfg_sys_shader_goat_pat.ItemsSource = new[] { "goatron", "borg", "slenderman" };
        Cfg_sys_shader_goat_pat.SelectedItem = "goatron";
    }

    private void AttachSliderHandlers()
    {
        Cfg_sound_volume.PropertyChanged += (_, e) =>
        {
            if (e.Property == Slider.ValueProperty)
                Cfg_sound_volume_label.Text = $"{(int)Cfg_sound_volume.Value}%";
        };

        // Initialize label
        Cfg_sound_volume_label.Text = "100%";
    }

    // ── File selection ────────────────────────────────────────────────

    private async void BrowseCfgButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isDirty && !await ConfirmDiscardChanges()) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var loc = LocalizationManager.Instance;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = loc["MednafenConfig_SelectCfgTitle"],
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(loc["MednafenConfig_CfgFileType"]) { Patterns = ["*.cfg"] },
                FilePickerFileTypes.All
            ]
        });

        if (files.Count == 0) return;

        string path = Uri.UnescapeDataString(files[0].Path.LocalPath);
        LoadCfgFile(path);
    }

    private async void AutoDetectCfgButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isDirty && !await ConfirmDiscardChanges()) return;

        var loc = LocalizationManager.Instance;
        string? cfgPath = MednafenCfgParser.FindMednafenCfg();

        if (cfgPath != null)
        {
            LoadCfgFile(cfgPath);
        }
        else
        {
            ShowStatus(loc["MednafenConfig_NoCfgFound"], isError: true);
        }
    }

    // ── Load / Save ──────────────────────────────────────────────────

    private void LoadCfgFile(string path)
    {
        var loc = LocalizationManager.Instance;

        try
        {
            _parser = MednafenCfgParser.Load(path);
            CfgFilePathTextBox.Text = path;
            PopulateUI();
            ConfigTabs.IsVisible = true;
            ActionPanel.IsVisible = true;
            SearchBorder.IsVisible = true;
            BuildSearchIndex();
            AttachChangeTracking();
            ClearDirty();
            ShowStatus(string.Format(loc["MednafenConfig_Loaded"], _parser.Count, path), isError: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            ShowStatus(string.Format(loc["MednafenConfig_LoadError"], ex.Message), isError: true);
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
        CollectPerSystemFromUI(_currentSystem);

        try
        {
            _parser.Save();
            ClearDirty();
            ShowStatus(string.Format(loc["MednafenConfig_Saved"], _parser.FilePath), isError: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            ShowStatus(string.Format(loc["MednafenConfig_SaveError"], ex.Message), isError: true);
        }
    }

    private async void SaveAsButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;
        if (_parser == null) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        CollectFromUI();
        CollectPerSystemFromUI(_currentSystem);

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = loc["MednafenConfig_SaveDialogTitle"],
            SuggestedFileName = "mednafen.cfg",
            FileTypeChoices =
            [
                new FilePickerFileType(loc["MednafenConfig_CfgFileType"]) { Patterns = ["*.cfg"] }
            ]
        });

        if (file == null) return;

        try
        {
            string outputPath = file.Path.LocalPath;
            _parser.Save(outputPath);
            CfgFilePathTextBox.Text = outputPath;
            ClearDirty();
            ShowStatus(string.Format(loc["MednafenConfig_Saved"], outputPath), isError: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            ShowStatus(string.Format(loc["MednafenConfig_SaveError"], ex.Message), isError: true);
        }
    }

    private async void ResetDefaultsButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is Window parentWindow)
        {
            var confirmDialog = new Views.Dialogs.ConfirmDialog(
                loc["MednafenConfig_ConfirmResetTitle"],
                loc["MednafenConfig_ConfirmResetMessage"]);
            bool confirmed = await confirmDialog.ShowDialog(parentWindow);
            if (!confirmed) return;
        }

        string? filePath = _parser?.FilePath;

        _parser = MednafenCfgParser.CreateWithDefaults(filePath);
        PopulateUI();
        BuildSearchIndex();
        ClearDirty();
        ShowStatus(loc["MednafenConfig_DefaultsRestored"], isError: false);
    }

    private async void CreateNewButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;

        if (_isDirty && !await ConfirmDiscardChanges()) return;

        string defaultPath = MednafenCfgParser.GetDefaultCfgPath();

        _parser = MednafenCfgParser.CreateWithDefaults(defaultPath);
        CfgFilePathTextBox.Text = defaultPath;
        PopulateUI();
        ConfigTabs.IsVisible = true;
        ActionPanel.IsVisible = true;
        SearchBorder.IsVisible = true;
        BuildSearchIndex();
        AttachChangeTracking();
        ClearDirty();
        ShowStatus(string.Format(loc["MednafenConfig_NewCreated"], defaultPath), isError: false);
    }

    // ── Per-system selector ──────────────────────────────────────────

    private void SystemSelector_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_parser == null || SystemSelector.SelectedIndex < 0) return;

        // Save current system settings before switching
        CollectPerSystemFromUI(_currentSystem);

        // Switch to new system
        _currentSystem = Systems[SystemSelector.SelectedIndex].Id;
        PopulatePerSystemUI(_currentSystem);
    }

    // ── UI ↔ Parser synchronization ──────────────────────────────────

    private void PopulateUI()
    {
        if (_parser == null) return;

        _isPopulating = true;

        // ── General ──
        Cfg_autosave.IsChecked = _parser.GetBool("autosave");
        Cfg_cheats.IsChecked = _parser.GetBool("cheats", true);
        Cfg_cd_image_memcache.IsChecked = _parser.GetBool("cd.image_memcache");
        Cfg_cd_m3u_disc_limit.Value = _parser.GetInt("cd.m3u.disc_limit", 25);
        Cfg_cd_m3u_recursion_limit.Value = _parser.GetInt("cd.m3u.recursion_limit", 9);
        Cfg_debugger_autostepmode.IsChecked = _parser.GetBool("debugger.autostepmode");
        Cfg_srwframes.Value = _parser.GetInt("srwframes", 600);
        Cfg_nothrottle.IsChecked = _parser.GetBool("nothrottle");

        // Speed
        Cfg_ffspeed.Value = (decimal)_parser.GetDouble("ffspeed", 4);
        Cfg_fftoggle.IsChecked = _parser.GetBool("fftoggle");
        Cfg_ffnosound.IsChecked = _parser.GetBool("ffnosound");
        Cfg_sfspeed.Value = (decimal)_parser.GetDouble("sfspeed", 0.75);
        Cfg_sftoggle.IsChecked = _parser.GetBool("sftoggle");

        // ── Video ──
        SetCombo(Cfg_video_driver, _parser.GetString("video.driver", "default"));
        Cfg_video_fs.IsChecked = _parser.GetBool("video.fs");
        Cfg_video_fs_display.Value = _parser.GetInt("video.fs.display", -1);
        Cfg_video_glvsync.IsChecked = _parser.GetBool("video.glvsync", true);
        Cfg_video_blit_timesync.IsChecked = _parser.GetBool("video.blit_timesync", true);
        Cfg_video_frameskip.IsChecked = _parser.GetBool("video.frameskip", true);
        Cfg_video_disable_composition.IsChecked = _parser.GetBool("video.disable_composition", true);
        Cfg_video_force_bbclear.IsChecked = _parser.GetBool("video.force_bbclear");
        SetCombo(Cfg_video_cursorvis, _parser.GetString("video.cursorvis", "hidden"));
        SetCombo(Cfg_video_deinterlacer, _parser.GetString("video.deinterlacer", "weave"));
        SetCombo(Cfg_video_glformat, _parser.GetString("video.glformat", "auto"));

        // FPS Display
        Cfg_fps_autoenable.IsChecked = _parser.GetBool("fps.autoenable");
        SetCombo(Cfg_fps_font, _parser.GetString("fps.font", "5x7"));
        SetCombo(Cfg_fps_position, _parser.GetString("fps.position", "upper_left"));
        Cfg_fps_scale.Value = _parser.GetInt("fps.scale", 1);
        SetText(Cfg_fps_bgcolor, _parser.GetString("fps.bgcolor", "0x80000000"));
        SetText(Cfg_fps_textcolor, _parser.GetString("fps.textcolor", "0xFFFFFFFF"));

        // OSD
        Cfg_osd_alpha_blend.IsChecked = _parser.GetBool("osd.alpha_blend", true);
        Cfg_osd_message_display_time.Value = _parser.GetInt("osd.message_display_time", 2500);
        Cfg_osd_state_display_time.Value = _parser.GetInt("osd.state_display_time", 2000);

        // ── Sound ──
        Cfg_sound.IsChecked = _parser.GetBool("sound", true);
        SetCombo(Cfg_sound_driver, _parser.GetString("sound.driver", "default"));
        SetText(Cfg_sound_device, _parser.GetString("sound.device", "default"));
        SetCombo(Cfg_sound_rate, _parser.GetString("sound.rate", "48000"));
        Cfg_sound_volume.Value = _parser.GetInt("sound.volume", 100);
        Cfg_sound_buffer_time.Value = _parser.GetInt("sound.buffer_time", 0);
        Cfg_sound_period_time.Value = _parser.GetInt("sound.period_time", 0);

        // ── Input ──
        Cfg_input_autofirefreq.Value = _parser.GetInt("input.autofirefreq", 3);
        Cfg_input_ckdelay.Value = _parser.GetInt("input.ckdelay", 0);
        SetCombo(Cfg_input_grab_strategy, _parser.GetString("input.grab.strategy", "full"));
        Cfg_input_joystick_axis_threshold.Value = _parser.GetInt("input.joystick.axis_threshold", 75);
        Cfg_input_joystick_global_focus.IsChecked = _parser.GetBool("input.joystick.global_focus", true);

        // ── Paths ──
        SetText(Cfg_filesys_path_cheat, _parser.GetString("filesys.path_cheat", "cheats"));
        SetText(Cfg_filesys_path_firmware, _parser.GetString("filesys.path_firmware", "firmware"));
        SetText(Cfg_filesys_path_movie, _parser.GetString("filesys.path_movie", "mcm"));
        SetText(Cfg_filesys_path_palette, _parser.GetString("filesys.path_palette", "palettes"));
        SetText(Cfg_filesys_path_pgconfig, _parser.GetString("filesys.path_pgconfig", "pgconfig"));
        SetText(Cfg_filesys_path_sav, _parser.GetString("filesys.path_sav", "sav"));
        SetText(Cfg_filesys_path_savbackup, _parser.GetString("filesys.path_savbackup", "b"));
        SetText(Cfg_filesys_path_snap, _parser.GetString("filesys.path_snap", "snaps"));
        SetText(Cfg_filesys_path_state, _parser.GetString("filesys.path_state", "mcs"));

        // Filename formats
        SetText(Cfg_filesys_fname_movie, _parser.GetString("filesys.fname_movie", "%f.%M%p.%x"));
        SetText(Cfg_filesys_fname_sav, _parser.GetString("filesys.fname_sav", "%f.%M%x"));
        SetText(Cfg_filesys_fname_savbackup, _parser.GetString("filesys.fname_savbackup", "%f.%m%z%p.%x"));
        SetText(Cfg_filesys_fname_snap, _parser.GetString("filesys.fname_snap", "%f-%p.%x"));
        SetText(Cfg_filesys_fname_state, _parser.GetString("filesys.fname_state", "%f.%M%X"));
        Cfg_filesys_state_comp_level.Value = _parser.GetInt("filesys.state_comp_level", 6);
        Cfg_filesys_untrusted_fip_check.IsChecked = _parser.GetBool("filesys.untrusted_fip_check", true);

        // ── Per-System (load the first system) ──
        _currentSystem = Systems[SystemSelector.SelectedIndex >= 0 ? SystemSelector.SelectedIndex : 0].Id;
        PopulatePerSystemUI(_currentSystem);

        // ── Netplay ──
        SetText(Cfg_netplay_host, _parser.GetString("netplay.host", "netplay.fobby.net"));
        Cfg_netplay_port.Value = _parser.GetInt("netplay.port", 4046);
        Cfg_netplay_localplayers.Value = _parser.GetInt("netplay.localplayers", 1);
        SetText(Cfg_netplay_nick, _parser.GetString("netplay.nick"));
        SetText(Cfg_netplay_password, _parser.GetString("netplay.password"));
        SetText(Cfg_netplay_gamekey, _parser.GetString("netplay.gamekey"));
        SetCombo(Cfg_netplay_console_font, _parser.GetString("netplay.console.font", "9x18"));
        Cfg_netplay_console_lines.Value = _parser.GetInt("netplay.console.lines", 5);
        Cfg_netplay_console_scale.Value = _parser.GetInt("netplay.console.scale", 1);

        // ── Advanced ──
        SetCombo(Cfg_qtrecord_vcodec, _parser.GetString("qtrecord.vcodec", "cscd"));
        Cfg_qtrecord_w_double_threshold.Value = _parser.GetInt("qtrecord.w_double_threshold", 384);
        Cfg_qtrecord_h_double_threshold.Value = _parser.GetInt("qtrecord.h_double_threshold", 256);
        SetText(Cfg_affinity_cd, _parser.GetString("affinity.cd", "0"));
        SetText(Cfg_affinity_emu, _parser.GetString("affinity.emu", "0"));
        SetText(Cfg_affinity_video, _parser.GetString("affinity.video", "0"));

        _isPopulating = false;
    }

    private void PopulatePerSystemUI(string sys)
    {
        if (_parser == null) return;

        bool wasPopulating = _isPopulating;
        _isPopulating = true;

        Cfg_sys_enable.IsChecked = _parser.GetBool($"{sys}.enable", true);
        Cfg_sys_forcemono.IsChecked = _parser.GetBool($"{sys}.forcemono");
        Cfg_sys_scanlines.Value = _parser.GetInt($"{sys}.scanlines", 0);
        SetCombo(Cfg_sys_shader, _parser.GetString($"{sys}.shader", "none"));
        SetCombo(Cfg_sys_special, _parser.GetString($"{sys}.special", "none"));
        SetCombo(Cfg_sys_stretch, _parser.GetString($"{sys}.stretch", "aspect_mult2"));
        SetCombo(Cfg_sys_videoip, _parser.GetString($"{sys}.videoip", "0"));
        Cfg_sys_xres.Value = _parser.GetInt($"{sys}.xres", 0);
        Cfg_sys_xscale.Value = (decimal)_parser.GetDouble($"{sys}.xscale", 4.0);
        Cfg_sys_xscalefs.Value = (decimal)_parser.GetDouble($"{sys}.xscalefs", 1.0);
        Cfg_sys_yres.Value = _parser.GetInt($"{sys}.yres", 0);
        Cfg_sys_yscale.Value = (decimal)_parser.GetDouble($"{sys}.yscale", 4.0);
        Cfg_sys_yscalefs.Value = (decimal)_parser.GetDouble($"{sys}.yscalefs", 1.0);

        // Temporal blur
        Cfg_sys_tblur.IsChecked = _parser.GetBool($"{sys}.tblur");
        Cfg_sys_tblur_accum.IsChecked = _parser.GetBool($"{sys}.tblur.accum");
        Cfg_sys_tblur_accum_amount.Value = (decimal)_parser.GetDouble($"{sys}.tblur.accum.amount", 50);

        // Goat shader
        Cfg_sys_shader_goat_fprog.IsChecked = _parser.GetBool($"{sys}.shader.goat.fprog");
        Cfg_sys_shader_goat_hdiv.Value = (decimal)_parser.GetDouble($"{sys}.shader.goat.hdiv", 0.50);
        SetCombo(Cfg_sys_shader_goat_pat, _parser.GetString($"{sys}.shader.goat.pat", "goatron"));
        Cfg_sys_shader_goat_slen.IsChecked = _parser.GetBool($"{sys}.shader.goat.slen", true);
        Cfg_sys_shader_goat_tp.Value = (decimal)_parser.GetDouble($"{sys}.shader.goat.tp", 0.50);
        Cfg_sys_shader_goat_vdiv.Value = (decimal)_parser.GetDouble($"{sys}.shader.goat.vdiv", 0.50);

        _isPopulating = wasPopulating;
    }

    private void CollectFromUI()
    {
        if (_parser == null) return;

        // ── General ──
        _parser.SetBool("autosave", Cfg_autosave.IsChecked == true);
        _parser.SetBool("cheats", Cfg_cheats.IsChecked == true);
        _parser.SetBool("cd.image_memcache", Cfg_cd_image_memcache.IsChecked == true);
        _parser.SetInt("cd.m3u.disc_limit", (int)(Cfg_cd_m3u_disc_limit.Value ?? 25));
        _parser.SetInt("cd.m3u.recursion_limit", (int)(Cfg_cd_m3u_recursion_limit.Value ?? 9));
        _parser.SetBool("debugger.autostepmode", Cfg_debugger_autostepmode.IsChecked == true);
        _parser.SetInt("srwframes", (int)(Cfg_srwframes.Value ?? 600));
        _parser.SetBool("nothrottle", Cfg_nothrottle.IsChecked == true);

        // Speed
        _parser.SetDouble("ffspeed", (double)(Cfg_ffspeed.Value ?? 4));
        _parser.SetBool("fftoggle", Cfg_fftoggle.IsChecked == true);
        _parser.SetBool("ffnosound", Cfg_ffnosound.IsChecked == true);
        _parser.SetDouble("sfspeed", (double)(Cfg_sfspeed.Value ?? 0.75m));
        _parser.SetBool("sftoggle", Cfg_sftoggle.IsChecked == true);

        // ── Video ──
        _parser.SetString("video.driver", GetCombo(Cfg_video_driver, "default"));
        _parser.SetBool("video.fs", Cfg_video_fs.IsChecked == true);
        _parser.SetInt("video.fs.display", (int)(Cfg_video_fs_display.Value ?? -1));
        _parser.SetBool("video.glvsync", Cfg_video_glvsync.IsChecked == true);
        _parser.SetBool("video.blit_timesync", Cfg_video_blit_timesync.IsChecked == true);
        _parser.SetBool("video.frameskip", Cfg_video_frameskip.IsChecked == true);
        _parser.SetBool("video.disable_composition", Cfg_video_disable_composition.IsChecked == true);
        _parser.SetBool("video.force_bbclear", Cfg_video_force_bbclear.IsChecked == true);
        _parser.SetString("video.cursorvis", GetCombo(Cfg_video_cursorvis, "hidden"));
        _parser.SetString("video.deinterlacer", GetCombo(Cfg_video_deinterlacer, "weave"));
        _parser.SetString("video.glformat", GetCombo(Cfg_video_glformat, "auto"));

        // FPS Display
        _parser.SetBool("fps.autoenable", Cfg_fps_autoenable.IsChecked == true);
        _parser.SetString("fps.font", GetCombo(Cfg_fps_font, "5x7"));
        _parser.SetString("fps.position", GetCombo(Cfg_fps_position, "upper_left"));
        _parser.SetInt("fps.scale", (int)(Cfg_fps_scale.Value ?? 1));
        _parser.SetString("fps.bgcolor", Cfg_fps_bgcolor.Text ?? "0x80000000");
        _parser.SetString("fps.textcolor", Cfg_fps_textcolor.Text ?? "0xFFFFFFFF");

        // OSD
        _parser.SetBool("osd.alpha_blend", Cfg_osd_alpha_blend.IsChecked == true);
        _parser.SetInt("osd.message_display_time", (int)(Cfg_osd_message_display_time.Value ?? 2500));
        _parser.SetInt("osd.state_display_time", (int)(Cfg_osd_state_display_time.Value ?? 2000));

        // ── Sound ──
        _parser.SetBool("sound", Cfg_sound.IsChecked == true);
        _parser.SetString("sound.driver", GetCombo(Cfg_sound_driver, "default"));
        _parser.SetString("sound.device", Cfg_sound_device.Text ?? "default");
        _parser.SetString("sound.rate", GetCombo(Cfg_sound_rate, "48000"));
        _parser.SetInt("sound.volume", (int)Cfg_sound_volume.Value);
        _parser.SetInt("sound.buffer_time", (int)(Cfg_sound_buffer_time.Value ?? 0));
        _parser.SetInt("sound.period_time", (int)(Cfg_sound_period_time.Value ?? 0));

        // ── Input ──
        _parser.SetInt("input.autofirefreq", (int)(Cfg_input_autofirefreq.Value ?? 3));
        _parser.SetInt("input.ckdelay", (int)(Cfg_input_ckdelay.Value ?? 0));
        _parser.SetString("input.grab.strategy", GetCombo(Cfg_input_grab_strategy, "full"));
        _parser.SetInt("input.joystick.axis_threshold", (int)(Cfg_input_joystick_axis_threshold.Value ?? 75));
        _parser.SetBool("input.joystick.global_focus", Cfg_input_joystick_global_focus.IsChecked == true);

        // ── Paths ──
        _parser.SetString("filesys.path_cheat", Cfg_filesys_path_cheat.Text ?? "cheats");
        _parser.SetString("filesys.path_firmware", Cfg_filesys_path_firmware.Text ?? "firmware");
        _parser.SetString("filesys.path_movie", Cfg_filesys_path_movie.Text ?? "mcm");
        _parser.SetString("filesys.path_palette", Cfg_filesys_path_palette.Text ?? "palettes");
        _parser.SetString("filesys.path_pgconfig", Cfg_filesys_path_pgconfig.Text ?? "pgconfig");
        _parser.SetString("filesys.path_sav", Cfg_filesys_path_sav.Text ?? "sav");
        _parser.SetString("filesys.path_savbackup", Cfg_filesys_path_savbackup.Text ?? "b");
        _parser.SetString("filesys.path_snap", Cfg_filesys_path_snap.Text ?? "snaps");
        _parser.SetString("filesys.path_state", Cfg_filesys_path_state.Text ?? "mcs");

        // Filename formats
        _parser.SetString("filesys.fname_movie", Cfg_filesys_fname_movie.Text ?? "%f.%M%p.%x");
        _parser.SetString("filesys.fname_sav", Cfg_filesys_fname_sav.Text ?? "%f.%M%x");
        _parser.SetString("filesys.fname_savbackup", Cfg_filesys_fname_savbackup.Text ?? "%f.%m%z%p.%x");
        _parser.SetString("filesys.fname_snap", Cfg_filesys_fname_snap.Text ?? "%f-%p.%x");
        _parser.SetString("filesys.fname_state", Cfg_filesys_fname_state.Text ?? "%f.%M%X");
        _parser.SetInt("filesys.state_comp_level", (int)(Cfg_filesys_state_comp_level.Value ?? 6));
        _parser.SetBool("filesys.untrusted_fip_check", Cfg_filesys_untrusted_fip_check.IsChecked == true);

        // ── Per-System (save current system) ──
        CollectPerSystemFromUI(_currentSystem);

        // ── Netplay ──
        _parser.SetString("netplay.host", Cfg_netplay_host.Text ?? "netplay.fobby.net");
        _parser.SetInt("netplay.port", (int)(Cfg_netplay_port.Value ?? 4046));
        _parser.SetInt("netplay.localplayers", (int)(Cfg_netplay_localplayers.Value ?? 1));
        _parser.SetString("netplay.nick", Cfg_netplay_nick.Text ?? "");
        _parser.SetString("netplay.password", Cfg_netplay_password.Text ?? "");
        _parser.SetString("netplay.gamekey", Cfg_netplay_gamekey.Text ?? "");
        _parser.SetString("netplay.console.font", GetCombo(Cfg_netplay_console_font, "9x18"));
        _parser.SetInt("netplay.console.lines", (int)(Cfg_netplay_console_lines.Value ?? 5));
        _parser.SetInt("netplay.console.scale", (int)(Cfg_netplay_console_scale.Value ?? 1));

        // ── Advanced ──
        _parser.SetString("qtrecord.vcodec", GetCombo(Cfg_qtrecord_vcodec, "cscd"));
        _parser.SetInt("qtrecord.w_double_threshold", (int)(Cfg_qtrecord_w_double_threshold.Value ?? 384));
        _parser.SetInt("qtrecord.h_double_threshold", (int)(Cfg_qtrecord_h_double_threshold.Value ?? 256));
        _parser.SetString("affinity.cd", Cfg_affinity_cd.Text ?? "0");
        _parser.SetString("affinity.emu", Cfg_affinity_emu.Text ?? "0");
        _parser.SetString("affinity.video", Cfg_affinity_video.Text ?? "0");
    }

    private void CollectPerSystemFromUI(string sys)
    {
        if (_parser == null) return;

        _parser.SetBool($"{sys}.enable", Cfg_sys_enable.IsChecked == true);
        _parser.SetBool($"{sys}.forcemono", Cfg_sys_forcemono.IsChecked == true);
        _parser.SetInt($"{sys}.scanlines", (int)(Cfg_sys_scanlines.Value ?? 0));
        _parser.SetString($"{sys}.shader", GetCombo(Cfg_sys_shader, "none"));
        _parser.SetString($"{sys}.special", GetCombo(Cfg_sys_special, "none"));
        _parser.SetString($"{sys}.stretch", GetCombo(Cfg_sys_stretch, "aspect_mult2"));
        _parser.SetString($"{sys}.videoip", GetCombo(Cfg_sys_videoip, "0"));
        _parser.SetInt($"{sys}.xres", (int)(Cfg_sys_xres.Value ?? 0));
        _parser.SetDouble($"{sys}.xscale", (double)(Cfg_sys_xscale.Value ?? 4.0m));
        _parser.SetDouble($"{sys}.xscalefs", (double)(Cfg_sys_xscalefs.Value ?? 1.0m));
        _parser.SetInt($"{sys}.yres", (int)(Cfg_sys_yres.Value ?? 0));
        _parser.SetDouble($"{sys}.yscale", (double)(Cfg_sys_yscale.Value ?? 4.0m));
        _parser.SetDouble($"{sys}.yscalefs", (double)(Cfg_sys_yscalefs.Value ?? 1.0m));

        // Temporal blur
        _parser.SetBool($"{sys}.tblur", Cfg_sys_tblur.IsChecked == true);
        _parser.SetBool($"{sys}.tblur.accum", Cfg_sys_tblur_accum.IsChecked == true);
        _parser.SetDouble($"{sys}.tblur.accum.amount", (double)(Cfg_sys_tblur_accum_amount.Value ?? 50));

        // Goat shader
        _parser.SetBool($"{sys}.shader.goat.fprog", Cfg_sys_shader_goat_fprog.IsChecked == true);
        _parser.SetDouble($"{sys}.shader.goat.hdiv", (double)(Cfg_sys_shader_goat_hdiv.Value ?? 0.50m));
        _parser.SetString($"{sys}.shader.goat.pat", GetCombo(Cfg_sys_shader_goat_pat, "goatron"));
        _parser.SetBool($"{sys}.shader.goat.slen", Cfg_sys_shader_goat_slen.IsChecked == true);
        _parser.SetDouble($"{sys}.shader.goat.tp", (double)(Cfg_sys_shader_goat_tp.Value ?? 0.50m));
        _parser.SetDouble($"{sys}.shader.goat.vdiv", (double)(Cfg_sys_shader_goat_vdiv.Value ?? 0.50m));
    }

    // ── Helper methods ───────────────────────────────────────────────

    private static void SetText(TextBox textBox, string value)
    {
        textBox.Text = value;
    }

    private static void SetCombo(ComboBox combo, string value)
    {
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (string.Equals(combo.Items[i]?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedIndex = i;
                return;
            }
        }

        // Value not in the predefined list — add it so it's preserved on save.
        // This is rare (only when the user's CFG has a custom/newer value),
        // so the array reallocation cost is acceptable.
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

        // Last resort fallback
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

        // Support space-separated search terms (all terms must match)
        string[] terms = searchText.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        int matchCount = 0;
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
        SearchResultsText.Text = string.Format(loc["MednafenConfig_SearchResults"], matchCount, _searchIndex.Count);
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
                loc["MednafenConfig_ConfirmDiscardTitle"],
                loc["MednafenConfig_ConfirmDiscardMessage"]);
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
