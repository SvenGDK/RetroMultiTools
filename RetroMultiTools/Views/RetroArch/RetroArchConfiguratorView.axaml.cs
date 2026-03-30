using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using RetroMultiTools.Localization;
using RetroMultiTools.Utilities.RetroArch;
using System.Globalization;

namespace RetroMultiTools.Views.RetroArch;

/// <summary>
/// Full RetroArch Configurator view that loads, edits, and saves retroarch.cfg files.
/// Supports all RetroArch configuration options and is compatible with all RetroArch
/// installations on Windows, macOS, and Linux (x64 + arm64).
/// </summary>
public partial class RetroArchConfiguratorView : UserControl
{
    private static readonly IBrush StatusErrorBrush = new SolidColorBrush(Color.Parse("#F38BA8"));
    private static readonly IBrush StatusSuccessBrush = new SolidColorBrush(Color.Parse("#A6E3A1"));

    private RetroArchCfgParser? _parser;
    private readonly DispatcherTimer _statusTimer;
    private List<(Border Section, string SearchableText)>? _searchIndex;
    private bool _isDirty;
    private bool _isPopulating;
    private bool _changeTrackingAttached;

    public RetroArchConfiguratorView()
    {
        InitializeComponent();
        InitializeComboBoxes();
        AttachSliderHandlers();
        SearchTextBox.TextChanged += SearchTextBox_TextChanged;

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _statusTimer.Tick += (_, _) =>
        {
            CfgStatusText.Text = "";
            _statusTimer.Stop();
        };
    }

    // ── ComboBox initialization ───────────────────────────────────────

    private void InitializeComboBoxes()
    {
        // Video driver
        Cfg_video_driver.ItemsSource = new[] { "gl", "glcore", "vulkan", "d3d11", "d3d12", "d3d10", "d3d9", "metal", "sdl2", "sdl_dingux", "gx", "gx2", "ctr", "switch", "vita2d", "null" };
        Cfg_video_driver.SelectedItem = "gl";

        // Video rotation
        Cfg_video_rotation.ItemsSource = new[] { "0 - Normal", "1 - 90°", "2 - 180°", "3 - 270°" };
        Cfg_video_rotation.SelectedIndex = 0;

        // CRT switch resolution
        Cfg_crt_switch_resolution.ItemsSource = new[] { "0 - Off", "1 - 15kHz", "2 - Super Resolution", "3 - INI Resolution" };
        Cfg_crt_switch_resolution.SelectedIndex = 0;

        // Audio driver
        Cfg_audio_driver.ItemsSource = new[] { "wasapi", "dsound", "xaudio", "pulse", "alsa", "oss", "jack", "coreaudio", "audioio", "sdl2", "tinyalsa", "roar", "null" };
        Cfg_audio_driver.SelectedItem = "pulse";

        // Audio resampler
        Cfg_audio_resampler.ItemsSource = new[] { "sinc", "CC", "nearest" };
        Cfg_audio_resampler.SelectedItem = "sinc";

        // Audio output rate
        Cfg_audio_output_rate.ItemsSource = new[] { "22050", "44100", "48000", "96000", "192000" };
        Cfg_audio_output_rate.SelectedItem = "48000";

        // Input driver
        Cfg_input_driver.ItemsSource = new[] { "x", "wayland", "sdl2", "dinput", "xinput", "udev", "linuxraw", "winraw", "cocoa", "android", "null" };
        Cfg_input_driver.SelectedItem = "x";

        // Input joypad driver
        Cfg_input_joypad_driver.ItemsSource = new[] { "sdl2", "dinput", "xinput", "udev", "linuxraw", "hid", "mfi", "android", "null" };
        Cfg_input_joypad_driver.SelectedItem = "sdl2";

        // Libretro log level
        Cfg_libretro_log_level.ItemsSource = new[] { "0 - Debug", "1 - Info", "2 - Warning", "3 - Error" };
        Cfg_libretro_log_level.SelectedIndex = 0;

        // Menu driver
        Cfg_menu_driver.ItemsSource = new[] { "ozone", "xmb", "rgui", "materialui", "null" };
        Cfg_menu_driver.SelectedItem = "ozone";
    }

    private void AttachSliderHandlers()
    {
        Cfg_audio_volume.PropertyChanged += (_, e) =>
        {
            if (e.Property == Slider.ValueProperty)
                Cfg_audio_volume_label.Text = Cfg_audio_volume.Value.ToString("F1", CultureInfo.InvariantCulture) + " dB";
        };
        Cfg_audio_mixer_volume.PropertyChanged += (_, e) =>
        {
            if (e.Property == Slider.ValueProperty)
                Cfg_audio_mixer_volume_label.Text = Cfg_audio_mixer_volume.Value.ToString("F1", CultureInfo.InvariantCulture) + " dB";
        };

        // Initialize labels
        Cfg_audio_volume_label.Text = "0.0 dB";
        Cfg_audio_mixer_volume_label.Text = "0.0 dB";
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
            Title = loc["RAConfig_SelectCfgTitle"],
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(loc["RAConfig_SelectCfgTitle"]) { Patterns = ["*.cfg"] },
                FilePickerFileTypes.All
            ]
        });

        if (files.Count == 0) return;

        string path = files[0].Path.LocalPath;
        LoadCfgFile(path);
    }

    private async void AutoDetectCfgButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isDirty && !await ConfirmDiscardChanges()) return;

        var loc = LocalizationManager.Instance;
        string? cfgPath = RetroArchCfgParser.FindRetroArchCfg();

        if (cfgPath != null)
        {
            LoadCfgFile(cfgPath);
        }
        else
        {
            ShowStatus(loc["RAConfig_NoCfgFound"], isError: true);
        }
    }

    // ── Load / Save ──────────────────────────────────────────────────

    private void LoadCfgFile(string path)
    {
        var loc = LocalizationManager.Instance;

        try
        {
            _parser = RetroArchCfgParser.Load(path);
            CfgFilePathTextBox.Text = path;
            PopulateUI();
            ConfigTabs.IsVisible = true;
            ActionButtons.IsVisible = true;
            SearchBorder.IsVisible = true;
            BuildSearchIndex();
            AttachChangeTracking();
            ClearDirty();
            ShowStatus(string.Format(loc["RAConfig_Loaded"], _parser.Count, path), isError: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            ShowStatus(string.Format(loc["RAConfig_LoadError"], ex.Message), isError: true);
            _parser = null;
            ConfigTabs.IsVisible = false;
            ActionButtons.IsVisible = false;
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
            ShowStatus(string.Format(loc["RAConfig_Saved"], _parser.FilePath), isError: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            ShowStatus(string.Format(loc["RAConfig_SaveError"], ex.Message), isError: true);
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
            Title = loc["RAConfig_SaveDialogTitle"],
            SuggestedFileName = "retroarch.cfg",
            FileTypeChoices =
            [
                new FilePickerFileType(loc["RAConfig_SelectCfgTitle"]) { Patterns = ["*.cfg"] }
            ]
        });

        if (file == null) return;

        try
        {
            string outputPath = file.Path.LocalPath;
            _parser.Save(outputPath);
            CfgFilePathTextBox.Text = outputPath;
            ClearDirty();
            ShowStatus(string.Format(loc["RAConfig_Saved"], outputPath), isError: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            ShowStatus(string.Format(loc["RAConfig_SaveError"], ex.Message), isError: true);
        }
    }

    private async void ResetDefaultsButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is Window parentWindow)
        {
            var confirmDialog = new RetroMultiTools.Views.Dialogs.ConfirmDialog(
                loc["RAConfig_ConfirmResetTitle"],
                loc["RAConfig_ConfirmResetMessage"]);
            bool confirmed = await confirmDialog.ShowDialog(parentWindow);
            if (!confirmed) return;
        }

        string? filePath = _parser?.FilePath;

        _parser = RetroArchCfgParser.CreateWithDefaults(filePath);
        PopulateUI();
        BuildSearchIndex();
        ClearDirty();
        ShowStatus(loc["RAConfig_DefaultsRestored"], isError: false);
    }

    private async void CreateNewButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var loc = LocalizationManager.Instance;

        if (_isDirty && !await ConfirmDiscardChanges()) return;

        string defaultPath = RetroArchCfgParser.GetDefaultCfgPath();

        _parser = RetroArchCfgParser.CreateWithDefaults(defaultPath);
        CfgFilePathTextBox.Text = defaultPath;
        PopulateUI();
        ConfigTabs.IsVisible = true;
        ActionButtons.IsVisible = true;
        SearchBorder.IsVisible = true;
        BuildSearchIndex();
        AttachChangeTracking();
        ClearDirty();
        ShowStatus(string.Format(loc["RAConfig_NewCreated"], defaultPath), isError: false);
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

        SearchResultsText.Text = matchCount == 1
            ? $"1 of {_searchIndex.Count} section matches"
            : $"{matchCount} of {_searchIndex.Count} sections match";
    }

    // ── UI ↔ Parser synchronization ──────────────────────────────────

    private void PopulateUI()
    {
        if (_parser == null) return;

        _isPopulating = true;

        // ── Directories / Paths ──
        SetText(Cfg_libretro_directory, _parser.GetString("libretro_directory"));
        SetText(Cfg_libretro_info_path, _parser.GetString("libretro_info_path"));
        SetText(Cfg_system_directory, _parser.GetString("system_directory"));
        SetText(Cfg_content_database_path, _parser.GetString("content_database_path"));
        SetText(Cfg_cheat_database_path, _parser.GetString("cheat_database_path"));
        SetText(Cfg_cursor_directory, _parser.GetString("cursor_directory"));
        SetText(Cfg_assets_directory, _parser.GetString("assets_directory"));
        SetText(Cfg_savefile_directory, _parser.GetString("savefile_directory"));
        SetText(Cfg_savestate_directory, _parser.GetString("savestate_directory"));
        SetText(Cfg_screenshot_directory, _parser.GetString("screenshot_directory"));
        SetText(Cfg_cache_directory, _parser.GetString("cache_directory"));
        SetText(Cfg_playlist_directory, _parser.GetString("playlist_directory"));
        SetText(Cfg_core_assets_directory, _parser.GetString("core_assets_directory"));
        SetText(Cfg_thumbnails_directory, _parser.GetString("thumbnails_directory"));
        SetText(Cfg_dynamic_wallpapers_directory, _parser.GetString("dynamic_wallpapers_directory"));
        SetText(Cfg_video_shader_dir, _parser.GetString("video_shader_dir"));
        SetText(Cfg_video_filter_dir, _parser.GetString("video_filter_dir"));
        SetText(Cfg_audio_filter_dir, _parser.GetString("audio_filter_dir"));
        SetText(Cfg_input_remapping_directory, _parser.GetString("input_remapping_directory"));
        SetText(Cfg_joypad_autoconfig_dir, _parser.GetString("joypad_autoconfig_dir"));
        SetText(Cfg_rgui_config_directory, _parser.GetString("rgui_config_directory"));
        SetText(Cfg_rgui_browser_directory, _parser.GetString("rgui_browser_directory"));
        SetText(Cfg_overlay_directory, _parser.GetString("overlay_directory"));
        SetText(Cfg_osk_overlay_directory, _parser.GetString("osk_overlay_directory"));
        SetText(Cfg_recording_output_directory, _parser.GetString("recording_output_directory"));
        SetText(Cfg_recording_config_directory, _parser.GetString("recording_config_directory"));
        SetText(Cfg_log_dir, _parser.GetString("log_dir"));

        // ── Video ──
        SetCombo(Cfg_video_driver, _parser.GetString("video_driver", "gl"));
        SetText(Cfg_video_context_driver, _parser.GetString("video_context_driver"));
        Cfg_video_fullscreen.IsChecked = _parser.GetBool("video_fullscreen");
        Cfg_video_windowed_fullscreen.IsChecked = _parser.GetBool("video_windowed_fullscreen", true);
        Cfg_video_window_show_decorations.IsChecked = _parser.GetBool("video_window_show_decorations", true);
        Cfg_video_allow_rotate.IsChecked = _parser.GetBool("video_allow_rotate", true);
        Cfg_video_vsync.IsChecked = _parser.GetBool("video_vsync", true);
        Cfg_video_adaptive_vsync.IsChecked = _parser.GetBool("video_adaptive_vsync");
        Cfg_video_hard_sync.IsChecked = _parser.GetBool("video_hard_sync");
        Cfg_video_frame_delay_auto.IsChecked = _parser.GetBool("video_frame_delay_auto");
        Cfg_video_force_aspect.IsChecked = _parser.GetBool("video_force_aspect", true);
        Cfg_video_aspect_ratio_auto.IsChecked = _parser.GetBool("video_aspect_ratio_auto");
        Cfg_video_smooth.IsChecked = _parser.GetBool("video_smooth");
        Cfg_video_crop_overscan.IsChecked = _parser.GetBool("video_crop_overscan", true);
        Cfg_video_threaded.IsChecked = _parser.GetBool("video_threaded");
        Cfg_video_shader_enable.IsChecked = _parser.GetBool("video_shader_enable");
        Cfg_video_font_enable.IsChecked = _parser.GetBool("video_font_enable", true);
        Cfg_video_gpu_screenshot.IsChecked = _parser.GetBool("video_gpu_screenshot", true);
        Cfg_video_ctx_scaling.IsChecked = _parser.GetBool("video_ctx_scaling");
        Cfg_video_shared_context.IsChecked = _parser.GetBool("video_shared_context");
        Cfg_video_force_srgb_disable.IsChecked = _parser.GetBool("video_force_srgb_disable");
        Cfg_video_notch_write_over_enable.IsChecked = _parser.GetBool("video_notch_write_over_enable");
        Cfg_crt_switch_resolution_use_custom_refresh_rate.IsChecked = _parser.GetBool("crt_switch_resolution_use_custom_refresh_rate");

        Cfg_video_monitor_index.Value = _parser.GetInt("video_monitor_index", 0);
        Cfg_video_fullscreen_x.Value = _parser.GetInt("video_fullscreen_x", 0);
        Cfg_video_fullscreen_y.Value = _parser.GetInt("video_fullscreen_y", 0);
        Cfg_video_window_opacity.Value = _parser.GetInt("video_window_opacity", 100);
        Cfg_video_swap_interval.Value = _parser.GetInt("video_swap_interval", 1);
        Cfg_video_max_swapchain_images.Value = _parser.GetInt("video_max_swapchain_images", 3);
        Cfg_video_hard_sync_frames.Value = _parser.GetInt("video_hard_sync_frames", 0);
        Cfg_video_frame_delay.Value = _parser.GetInt("video_frame_delay", 0);
        Cfg_video_scale.Value = (decimal)_parser.GetDouble("video_scale", 3.0);
        Cfg_video_black_frame_insertion.Value = _parser.GetInt("video_black_frame_insertion", 0);
        Cfg_video_font_size.Value = (decimal)_parser.GetDouble("video_font_size", 32.0);
        Cfg_video_window_x.Value = _parser.GetInt("video_window_x", 0);
        Cfg_video_window_y.Value = _parser.GetInt("video_window_y", 0);
        Cfg_video_window_auto_width_max.Value = _parser.GetInt("video_window_auto_width_max", 0);
        Cfg_video_window_auto_height_max.Value = _parser.GetInt("video_window_auto_height_max", 0);
        Cfg_video_msg_pos_x.Value = (decimal)_parser.GetDouble("video_msg_pos_x", 0.05);
        Cfg_video_msg_pos_y.Value = (decimal)_parser.GetDouble("video_msg_pos_y", 0.05);
        Cfg_video_record_threads.Value = _parser.GetInt("video_record_threads", 2);
        Cfg_crt_switch_resolution_super.Value = _parser.GetInt("crt_switch_resolution_super", 2560);

        SetText(Cfg_video_aspect_ratio, _parser.GetString("video_aspect_ratio"));
        SetText(Cfg_video_font_path, _parser.GetString("video_font_path"));
        SetText(Cfg_video_msg_color, _parser.GetString("video_msg_color"));
        SetText(Cfg_video_shader, _parser.GetString("video_shader"));
        SetText(Cfg_video_filter, _parser.GetString("video_filter"));

        SetComboByIndex(Cfg_video_rotation, _parser.GetInt("video_rotation", 0));
        SetComboByIndex(Cfg_crt_switch_resolution, _parser.GetInt("crt_switch_resolution", 0));

        // ── Audio ──
        SetCombo(Cfg_audio_driver, _parser.GetString("audio_driver", "pulse"));
        SetCombo(Cfg_audio_resampler, _parser.GetString("audio_resampler", "sinc"));
        SetCombo(Cfg_audio_output_rate, _parser.GetString("audio_output_rate", "48000"));
        SetText(Cfg_audio_device, _parser.GetString("audio_device"));
        SetText(Cfg_audio_dsp_plugin, _parser.GetString("audio_dsp_plugin"));

        Cfg_audio_enable.IsChecked = _parser.GetBool("audio_enable", true);
        Cfg_audio_sync.IsChecked = _parser.GetBool("audio_sync", true);
        Cfg_audio_rate_control.IsChecked = _parser.GetBool("audio_rate_control", true);
        Cfg_audio_mute_enable.IsChecked = _parser.GetBool("audio_mute_enable");
        Cfg_audio_mixer_mute_enable.IsChecked = _parser.GetBool("audio_mixer_mute_enable");
        Cfg_audio_fastforward_mute.IsChecked = _parser.GetBool("audio_fastforward_mute");
        Cfg_audio_wasapi_exclusive_mode.IsChecked = _parser.GetBool("audio_wasapi_exclusive_mode");
        Cfg_audio_wasapi_float_format.IsChecked = _parser.GetBool("audio_wasapi_float_format");
        Cfg_audio_fastforward_speedup.IsChecked = _parser.GetBool("audio_fastforward_speedup");

        Cfg_audio_volume.Value = _parser.GetDouble("audio_volume", 0.0);
        Cfg_audio_mixer_volume.Value = _parser.GetDouble("audio_mixer_volume", 0.0);
        Cfg_audio_latency.Value = _parser.GetInt("audio_latency", 64);
        Cfg_audio_rate_control_delta.Value = (decimal)_parser.GetDouble("audio_rate_control_delta", 0.005);
        Cfg_audio_max_timing_skew.Value = (decimal)_parser.GetDouble("audio_max_timing_skew", 0.05);
        Cfg_audio_wasapi_sh_buffer_length.Value = _parser.GetInt("audio_wasapi_sh_buffer_length", 0);

        // ── Input ──
        SetCombo(Cfg_input_driver, _parser.GetString("input_driver", "x"));
        SetCombo(Cfg_input_joypad_driver, _parser.GetString("input_joypad_driver", "sdl2"));
        Cfg_input_max_users.Value = _parser.GetInt("input_max_users", 5);
        Cfg_input_turbo_period.Value = _parser.GetInt("input_turbo_period", 6);
        Cfg_input_turbo_duty_cycle.Value = _parser.GetInt("input_turbo_duty_cycle", 3);

        Cfg_input_autodetect_enable.IsChecked = _parser.GetBool("input_autodetect_enable", true);
        Cfg_input_remap_binds_enable.IsChecked = _parser.GetBool("input_remap_binds_enable", true);
        Cfg_input_auto_mouse_grab.IsChecked = _parser.GetBool("input_auto_mouse_grab");
        Cfg_input_rumble_enable.IsChecked = _parser.GetBool("input_rumble_enable", true);
        Cfg_input_sensors_enable.IsChecked = _parser.GetBool("input_sensors_enable", true);
        Cfg_input_descriptor_label_show.IsChecked = _parser.GetBool("input_descriptor_label_show", true);
        Cfg_input_descriptor_hide_unbound.IsChecked = _parser.GetBool("input_descriptor_hide_unbound");

        Cfg_input_axis_threshold.Value = (decimal)_parser.GetDouble("input_axis_threshold", 0.5);
        Cfg_input_bind_timeout.Value = _parser.GetInt("input_bind_timeout", 5);
        Cfg_input_bind_hold.Value = _parser.GetInt("input_bind_hold", 2);
        Cfg_input_turbo_mode.Value = _parser.GetInt("input_turbo_mode", 0);
        Cfg_input_turbo_default_button.Value = _parser.GetInt("input_turbo_default_button", 2);
        Cfg_input_auto_game_focus.Value = _parser.GetInt("input_auto_game_focus", 0);
        Cfg_input_rumble_gain.Value = _parser.GetInt("input_rumble_gain", 100);
        Cfg_input_menu_toggle_gamepad_combo.Value = _parser.GetInt("input_menu_toggle_gamepad_combo", 0);
        Cfg_input_quit_gamepad_combo.Value = _parser.GetInt("input_quit_gamepad_combo", 0);
        Cfg_input_poll_type_behavior.Value = _parser.GetInt("input_poll_type_behavior", 2);

        // Hotkeys
        SetText(Cfg_input_enable_hotkey, _parser.GetString("input_enable_hotkey"));
        SetText(Cfg_input_exit_emulator, _parser.GetString("input_exit_emulator"));
        SetText(Cfg_input_toggle_fullscreen, _parser.GetString("input_toggle_fullscreen"));
        SetText(Cfg_input_save_state, _parser.GetString("input_save_state"));
        SetText(Cfg_input_load_state, _parser.GetString("input_load_state"));
        SetText(Cfg_input_toggle_fast_forward, _parser.GetString("input_toggle_fast_forward"));
        SetText(Cfg_input_rewind, _parser.GetString("input_rewind"));
        SetText(Cfg_input_pause_toggle, _parser.GetString("input_pause_toggle"));
        SetText(Cfg_input_reset, _parser.GetString("input_reset"));
        SetText(Cfg_input_screenshot, _parser.GetString("input_screenshot"));
        SetText(Cfg_input_audio_mute, _parser.GetString("input_audio_mute"));
        SetText(Cfg_input_menu_toggle, _parser.GetString("input_menu_toggle"));
        SetText(Cfg_input_state_slot_increase, _parser.GetString("input_state_slot_increase"));
        SetText(Cfg_input_state_slot_decrease, _parser.GetString("input_state_slot_decrease"));
        SetText(Cfg_input_hold_fast_forward, _parser.GetString("input_hold_fast_forward"));
        SetText(Cfg_input_toggle_slowmotion, _parser.GetString("input_toggle_slowmotion"));
        SetText(Cfg_input_hold_slowmotion, _parser.GetString("input_hold_slowmotion"));
        SetText(Cfg_input_frame_advance, _parser.GetString("input_frame_advance"));
        SetText(Cfg_input_shader_next, _parser.GetString("input_shader_next"));
        SetText(Cfg_input_shader_prev, _parser.GetString("input_shader_prev"));
        SetText(Cfg_input_cheat_index_plus, _parser.GetString("input_cheat_index_plus"));
        SetText(Cfg_input_cheat_index_minus, _parser.GetString("input_cheat_index_minus"));
        SetText(Cfg_input_cheat_toggle, _parser.GetString("input_cheat_toggle"));
        SetText(Cfg_input_osk_toggle, _parser.GetString("input_osk_toggle"));
        SetText(Cfg_input_fps_toggle, _parser.GetString("input_fps_toggle"));
        SetText(Cfg_input_netplay_game_watch, _parser.GetString("input_netplay_game_watch"));
        SetText(Cfg_input_volume_up, _parser.GetString("input_volume_up"));
        SetText(Cfg_input_volume_down, _parser.GetString("input_volume_down"));
        SetText(Cfg_input_overlay_next, _parser.GetString("input_overlay_next"));
        SetText(Cfg_input_disk_eject_toggle, _parser.GetString("input_disk_eject_toggle"));
        SetText(Cfg_input_disk_next, _parser.GetString("input_disk_next"));
        SetText(Cfg_input_disk_prev, _parser.GetString("input_disk_prev"));
        SetText(Cfg_input_grab_mouse_toggle, _parser.GetString("input_grab_mouse_toggle"));
        SetText(Cfg_input_game_focus_toggle, _parser.GetString("input_game_focus_toggle"));
        SetText(Cfg_input_recording_toggle, _parser.GetString("input_recording_toggle"));
        SetText(Cfg_input_streaming_toggle, _parser.GetString("input_streaming_toggle"));
        SetText(Cfg_input_ai_service, _parser.GetString("input_ai_service"));

        // ── Core / Saving ──
        Cfg_core_updater_auto_extract.IsChecked = _parser.GetBool("core_updater_auto_extract", true);
        Cfg_core_updater_show_experimental_cores.IsChecked = _parser.GetBool("core_updater_show_experimental_cores");
        Cfg_auto_remaps_enable.IsChecked = _parser.GetBool("auto_remaps_enable", true);
        Cfg_auto_shaders_enable.IsChecked = _parser.GetBool("auto_shaders_enable", true);
        Cfg_game_specific_options.IsChecked = _parser.GetBool("game_specific_options", true);
        Cfg_core_option_category_enable.IsChecked = _parser.GetBool("core_option_category_enable", true);
        Cfg_core_set_supports_no_game_enable.IsChecked = _parser.GetBool("core_set_supports_no_game_enable");
        SetComboByIndex(Cfg_libretro_log_level, _parser.GetInt("libretro_log_level", 1));

        Cfg_savestate_auto_save.IsChecked = _parser.GetBool("savestate_auto_save");
        Cfg_savestate_auto_load.IsChecked = _parser.GetBool("savestate_auto_load");
        Cfg_savestate_auto_index.IsChecked = _parser.GetBool("savestate_auto_index");
        Cfg_savestate_thumbnail_enable.IsChecked = _parser.GetBool("savestate_thumbnail_enable", true);
        Cfg_save_file_compression.IsChecked = _parser.GetBool("save_file_compression", true);
        Cfg_savestate_file_compression.IsChecked = _parser.GetBool("savestate_file_compression", true);
        Cfg_sort_savefiles_enable.IsChecked = _parser.GetBool("sort_savefiles_enable");
        Cfg_sort_savestates_enable.IsChecked = _parser.GetBool("sort_savestates_enable");
        Cfg_block_sram_overwrite.IsChecked = _parser.GetBool("block_sram_overwrite");
        Cfg_sort_savefiles_by_content_enable.IsChecked = _parser.GetBool("sort_savefiles_by_content_enable");
        Cfg_sort_savestates_by_content_enable.IsChecked = _parser.GetBool("sort_savestates_by_content_enable");
        Cfg_content_runtime_log.IsChecked = _parser.GetBool("content_runtime_log", true);
        Cfg_content_runtime_log_aggregate.IsChecked = _parser.GetBool("content_runtime_log_aggregate", true);
        Cfg_autosave_interval.Value = _parser.GetInt("autosave_interval", 0);
        Cfg_savestate_max_keep.Value = _parser.GetInt("savestate_max_keep", 0);

        // ── Menu / UI ──
        SetCombo(Cfg_menu_driver, _parser.GetString("menu_driver", "ozone"));
        Cfg_menu_linear_filter.IsChecked = _parser.GetBool("menu_linear_filter", true);
        Cfg_menu_pause_libretro.IsChecked = _parser.GetBool("menu_pause_libretro", true);
        Cfg_menu_mouse_enable.IsChecked = _parser.GetBool("menu_mouse_enable", true);
        Cfg_menu_show_sublabels.IsChecked = _parser.GetBool("menu_show_sublabels", true);
        Cfg_menu_timedate_enable.IsChecked = _parser.GetBool("menu_timedate_enable", true);
        Cfg_menu_battery_level_enable.IsChecked = _parser.GetBool("menu_battery_level_enable", true);
        Cfg_menu_core_enable.IsChecked = _parser.GetBool("menu_core_enable", true);
        Cfg_menu_dynamic_wallpaper_enable.IsChecked = _parser.GetBool("menu_dynamic_wallpaper_enable", true);
        Cfg_menu_show_quit_retroarch.IsChecked = _parser.GetBool("menu_show_quit_retroarch", true);
        Cfg_menu_pointer_enable.IsChecked = _parser.GetBool("menu_pointer_enable", true);
        Cfg_menu_swap_ok_cancel_buttons.IsChecked = _parser.GetBool("menu_swap_ok_cancel_buttons");
        Cfg_menu_show_online_updater.IsChecked = _parser.GetBool("menu_show_online_updater", true);
        Cfg_menu_show_core_updater.IsChecked = _parser.GetBool("menu_show_core_updater", true);
        Cfg_menu_show_load_core.IsChecked = _parser.GetBool("menu_show_load_core", true);
        Cfg_menu_show_load_content.IsChecked = _parser.GetBool("menu_show_load_content", true);
        Cfg_menu_show_information.IsChecked = _parser.GetBool("menu_show_information", true);
        Cfg_menu_show_configurations.IsChecked = _parser.GetBool("menu_show_configurations", true);
        Cfg_menu_show_help.IsChecked = _parser.GetBool("menu_show_help", true);
        Cfg_menu_show_restart_retroarch.IsChecked = _parser.GetBool("menu_show_restart_retroarch", true);
        Cfg_menu_show_reboot.IsChecked = _parser.GetBool("menu_show_reboot", true);
        Cfg_menu_show_shutdown.IsChecked = _parser.GetBool("menu_show_shutdown", true);
        Cfg_menu_horizontal_animation.IsChecked = _parser.GetBool("menu_horizontal_animation", true);
        Cfg_menu_savestate_resume.IsChecked = _parser.GetBool("menu_savestate_resume", true);
        Cfg_menu_insert_disk_resume.IsChecked = _parser.GetBool("menu_insert_disk_resume", true);

        Cfg_ozone_menu_color_theme.Value = _parser.GetInt("ozone_menu_color_theme", 1);
        Cfg_materialui_menu_color_theme.Value = _parser.GetInt("materialui_menu_color_theme", 0);
        Cfg_xmb_alpha_factor.Value = _parser.GetInt("xmb_alpha_factor", 75);
        Cfg_menu_screensaver_timeout.Value = _parser.GetInt("menu_screensaver_timeout", 0);
        Cfg_menu_timedate_style.Value = _parser.GetInt("menu_timedate_style", 5);
        Cfg_menu_ticker_type.Value = _parser.GetInt("menu_ticker_type", 0);
        Cfg_menu_ticker_speed.Value = (decimal)_parser.GetDouble("menu_ticker_speed", 2.0);
        Cfg_menu_xmb_animation_horizontal_highlight.Value = _parser.GetInt("menu_xmb_animation_horizontal_highlight", 0);
        Cfg_menu_xmb_animation_move_up_down.Value = _parser.GetInt("menu_xmb_animation_move_up_down", 0);
        Cfg_menu_xmb_animation_opening_main_menu.Value = _parser.GetInt("menu_xmb_animation_opening_main_menu", 0);
        Cfg_menu_remember_selection.Value = _parser.GetInt("menu_remember_selection", 1);
        Cfg_menu_quit_on_close_content.Value = _parser.GetInt("menu_quit_on_close_content", 0);
        Cfg_menu_screensaver_animation.Value = _parser.GetInt("menu_screensaver_animation", 1);
        Cfg_menu_screensaver_animation_speed.Value = (decimal)_parser.GetDouble("menu_screensaver_animation_speed", 1.0);

        Cfg_xmb_dark_mode.IsChecked = _parser.GetBool("xmb_dark_mode");

        // RGUI Theme
        Cfg_menu_rgui_internal_upscale_level.Value = _parser.GetInt("menu_rgui_internal_upscale_level", 0);
        Cfg_menu_rgui_full_width_layout.IsChecked = _parser.GetBool("menu_rgui_full_width_layout", true);
        Cfg_menu_rgui_color_theme.Value = _parser.GetInt("menu_rgui_color_theme", 0);
        Cfg_menu_rgui_shadows.IsChecked = _parser.GetBool("menu_rgui_shadows");
        Cfg_menu_rgui_particle_effect.Value = _parser.GetInt("menu_rgui_particle_effect", 0);
        Cfg_menu_rgui_thumbnail_downscaler.Value = _parser.GetInt("menu_rgui_thumbnail_downscaler", 0);
        Cfg_menu_rgui_inline_thumbnails.IsChecked = _parser.GetBool("menu_rgui_inline_thumbnails");
        Cfg_menu_rgui_swap_thumbnails.IsChecked = _parser.GetBool("menu_rgui_swap_thumbnails");
        Cfg_menu_rgui_extended_ascii.IsChecked = _parser.GetBool("menu_rgui_extended_ascii");
        Cfg_menu_rgui_background_filler_thickness_enable.IsChecked = _parser.GetBool("menu_rgui_background_filler_thickness_enable", true);
        Cfg_menu_rgui_border_filler_enable.IsChecked = _parser.GetBool("menu_rgui_border_filler_enable", true);
        Cfg_menu_rgui_border_filler_thickness_enable.IsChecked = _parser.GetBool("menu_rgui_border_filler_thickness_enable", true);

        // ── Network / Netplay ──
        Cfg_network_cmd_enable.IsChecked = _parser.GetBool("network_cmd_enable");
        Cfg_network_remote_enable.IsChecked = _parser.GetBool("network_remote_enable");
        Cfg_stdin_cmd_enable.IsChecked = _parser.GetBool("stdin_cmd_enable");
        Cfg_netplay.IsChecked = _parser.GetBool("netplay");
        Cfg_netplay_use_mitm_server.IsChecked = _parser.GetBool("netplay_use_mitm_server");
        Cfg_netplay_nat_traversal.IsChecked = _parser.GetBool("netplay_nat_traversal");
        Cfg_netplay_stateless_mode.IsChecked = _parser.GetBool("netplay_stateless_mode");
        Cfg_netplay_start_as_spectator.IsChecked = _parser.GetBool("netplay_start_as_spectator");
        Cfg_netplay_allow_slaves.IsChecked = _parser.GetBool("netplay_allow_slaves", true);
        Cfg_netplay_require_slaves.IsChecked = _parser.GetBool("netplay_require_slaves");
        Cfg_netplay_request_device_p1.IsChecked = _parser.GetBool("netplay_request_device_p1");
        Cfg_netplay_request_device_p2.IsChecked = _parser.GetBool("netplay_request_device_p2");
        Cfg_netplay_request_device_p3.IsChecked = _parser.GetBool("netplay_request_device_p3");
        Cfg_netplay_request_device_p4.IsChecked = _parser.GetBool("netplay_request_device_p4");
        Cfg_netplay_request_device_p5.IsChecked = _parser.GetBool("netplay_request_device_p5");
        Cfg_network_on_demand_thumbnails.IsChecked = _parser.GetBool("network_on_demand_thumbnails");

        Cfg_network_cmd_port.Value = _parser.GetInt("network_cmd_port", 55355);
        Cfg_netplay_ip_port.Value = _parser.GetInt("netplay_ip_port", 55435);
        Cfg_netplay_delay_frames.Value = _parser.GetInt("netplay_delay_frames", 16);
        Cfg_network_remote_base_port.Value = _parser.GetInt("network_remote_base_port", 55400);
        Cfg_netplay_check_frames.Value = _parser.GetInt("netplay_check_frames", 600);
        Cfg_netplay_share_digital.Value = _parser.GetInt("netplay_share_digital", 0);
        Cfg_netplay_share_analog.Value = _parser.GetInt("netplay_share_analog", 0);

        SetText(Cfg_updater_buildbot_cores_url, _parser.GetString("updater_buildbot_cores_url"));
        SetText(Cfg_updater_buildbot_assets_url, _parser.GetString("updater_buildbot_assets_url"));
        SetText(Cfg_netplay_ip_address, _parser.GetString("netplay_ip_address"));
        SetText(Cfg_netplay_mitm_server, _parser.GetString("netplay_mitm_server"));
        SetText(Cfg_netplay_password, _parser.GetString("netplay_password"));
        SetText(Cfg_netplay_spectate_password, _parser.GetString("netplay_spectate_password"));

        // ── Frame Throttle / Rewind ──
        Cfg_fastforward_ratio.Value = (decimal)_parser.GetDouble("fastforward_ratio", 0.0);
        Cfg_slowmotion_ratio.Value = (decimal)_parser.GetDouble("slowmotion_ratio", 3.0);
        Cfg_rewind_buffer_size.Value = _parser.GetInt("rewind_buffer_size", 20);
        Cfg_rewind_buffer_size_step.Value = _parser.GetInt("rewind_buffer_size_step", 10);
        Cfg_rewind_granularity.Value = _parser.GetInt("rewind_granularity", 1);
        Cfg_vrr_runloop_enable.IsChecked = _parser.GetBool("vrr_runloop_enable");
        Cfg_rewind_enable.IsChecked = _parser.GetBool("rewind_enable");
        Cfg_menu_throttle_framerate.IsChecked = _parser.GetBool("menu_throttle_framerate", true);

        // ── Achievements ──
        Cfg_cheevos_enable.IsChecked = _parser.GetBool("cheevos_enable");
        Cfg_cheevos_test_unofficial.IsChecked = _parser.GetBool("cheevos_test_unofficial");
        Cfg_cheevos_hardcore_mode_enable.IsChecked = _parser.GetBool("cheevos_hardcore_mode_enable");
        Cfg_cheevos_leaderboards_enable.IsChecked = _parser.GetBool("cheevos_leaderboards_enable");
        Cfg_cheevos_richpresence_enable.IsChecked = _parser.GetBool("cheevos_richpresence_enable", true);
        Cfg_cheevos_badges_enable.IsChecked = _parser.GetBool("cheevos_badges_enable", true);
        Cfg_cheevos_verbose_enable.IsChecked = _parser.GetBool("cheevos_verbose_enable");
        Cfg_cheevos_auto_screenshot.IsChecked = _parser.GetBool("cheevos_auto_screenshot");
        Cfg_cheevos_unlock_sound_enable.IsChecked = _parser.GetBool("cheevos_unlock_sound_enable");
        Cfg_cheevos_start_active.IsChecked = _parser.GetBool("cheevos_start_active");
        Cfg_cheevos_appearance_padding_auto.IsChecked = _parser.GetBool("cheevos_appearance_padding_auto", true);
        Cfg_cheevos_visibility_unlock.IsChecked = _parser.GetBool("cheevos_visibility_unlock", true);
        Cfg_cheevos_visibility_mastery.IsChecked = _parser.GetBool("cheevos_visibility_mastery", true);
        Cfg_cheevos_visibility_account.IsChecked = _parser.GetBool("cheevos_visibility_account");

        Cfg_cheevos_appearance_anchor.Value = _parser.GetInt("cheevos_appearance_anchor", 0);
        Cfg_cheevos_visibility_summary.Value = _parser.GetInt("cheevos_visibility_summary", 0);

        // ── Overlay ──
        Cfg_input_overlay_enable.IsChecked = _parser.GetBool("input_overlay_enable", true);
        Cfg_input_overlay_behind_menu.IsChecked = _parser.GetBool("input_overlay_behind_menu");
        Cfg_input_overlay_hide_in_menu.IsChecked = _parser.GetBool("input_overlay_hide_in_menu", true);
        Cfg_input_overlay_hide_when_gamepad_connected.IsChecked = _parser.GetBool("input_overlay_hide_when_gamepad_connected");
        Cfg_input_overlay_auto_scale.IsChecked = _parser.GetBool("input_overlay_auto_scale", true);

        SetText(Cfg_input_overlay, _parser.GetString("input_overlay"));

        Cfg_input_overlay_show_inputs.Value = _parser.GetInt("input_overlay_show_inputs", 2);
        Cfg_input_overlay_show_inputs_port.Value = _parser.GetInt("input_overlay_show_inputs_port", 0);
        Cfg_input_overlay_opacity.Value = (decimal)_parser.GetDouble("input_overlay_opacity", 0.7);
        Cfg_input_overlay_scale_landscape.Value = (decimal)_parser.GetDouble("input_overlay_scale_landscape", 1.0);
        Cfg_input_overlay_aspect_adjust_landscape.Value = (decimal)_parser.GetDouble("input_overlay_aspect_adjust_landscape", 0.0);
        Cfg_input_overlay_x_separation_landscape.Value = (decimal)_parser.GetDouble("input_overlay_x_separation_landscape", 0.0);
        Cfg_input_overlay_y_separation_landscape.Value = (decimal)_parser.GetDouble("input_overlay_y_separation_landscape", 0.0);
        Cfg_input_overlay_x_offset_landscape.Value = (decimal)_parser.GetDouble("input_overlay_x_offset_landscape", 0.0);
        Cfg_input_overlay_y_offset_landscape.Value = (decimal)_parser.GetDouble("input_overlay_y_offset_landscape", 0.0);
        Cfg_input_overlay_scale_portrait.Value = (decimal)_parser.GetDouble("input_overlay_scale_portrait", 1.0);
        Cfg_input_overlay_aspect_adjust_portrait.Value = (decimal)_parser.GetDouble("input_overlay_aspect_adjust_portrait", 0.0);
        Cfg_input_overlay_x_separation_portrait.Value = (decimal)_parser.GetDouble("input_overlay_x_separation_portrait", 0.0);
        Cfg_input_overlay_y_separation_portrait.Value = (decimal)_parser.GetDouble("input_overlay_y_separation_portrait", 0.0);

        // ── Recording ──
        Cfg_record_enable.IsChecked = _parser.GetBool("record_enable");
        Cfg_record_use_output_dir.IsChecked = _parser.GetBool("record_use_output_dir");

        SetText(Cfg_record_output_dir, _parser.GetString("record_output_dir"));
        SetText(Cfg_record_config_dir, _parser.GetString("record_config_dir"));

        Cfg_streaming_mode.Value = _parser.GetInt("streaming_mode", 0);

        // ── Accessibility ──
        Cfg_accessibility_enable.IsChecked = _parser.GetBool("accessibility_enable");
        Cfg_ai_service_enable.IsChecked = _parser.GetBool("ai_service_enable");
        Cfg_ai_service_pause.IsChecked = _parser.GetBool("ai_service_pause", true);

        Cfg_accessibility_narrator_speech_speed.Value = _parser.GetInt("accessibility_narrator_speech_speed", 5);
        Cfg_ai_service_mode.Value = _parser.GetInt("ai_service_mode", 0);
        Cfg_ai_service_source_lang.Value = _parser.GetInt("ai_service_source_lang", 0);
        Cfg_ai_service_target_lang.Value = _parser.GetInt("ai_service_target_lang", 0);

        SetText(Cfg_ai_service_url, _parser.GetString("ai_service_url"));

        // ── Notifications ──
        Cfg_notification_show_autoconfig.IsChecked = _parser.GetBool("notification_show_autoconfig", true);
        Cfg_notification_show_cheats_applied.IsChecked = _parser.GetBool("notification_show_cheats_applied", true);
        Cfg_notification_show_patch_applied.IsChecked = _parser.GetBool("notification_show_patch_applied", true);
        Cfg_notification_show_remap_load.IsChecked = _parser.GetBool("notification_show_remap_load", true);
        Cfg_notification_show_config_override_load.IsChecked = _parser.GetBool("notification_show_config_override_load", true);
        Cfg_notification_show_set_initial_disk.IsChecked = _parser.GetBool("notification_show_set_initial_disk", true);
        Cfg_notification_show_fast_forward.IsChecked = _parser.GetBool("notification_show_fast_forward", true);
        Cfg_notification_show_screenshot.IsChecked = _parser.GetBool("notification_show_screenshot", true);
        Cfg_notification_show_refresh_rate.IsChecked = _parser.GetBool("notification_show_refresh_rate");
        Cfg_notification_show_netplay_extra.IsChecked = _parser.GetBool("notification_show_netplay_extra", true);
        Cfg_notification_show_when_menu_is_alive.IsChecked = _parser.GetBool("notification_show_when_menu_is_alive", true);

        Cfg_notification_show_screenshot_duration.Value = _parser.GetInt("notification_show_screenshot_duration", 0);
        Cfg_notification_show_screenshot_flash.Value = _parser.GetInt("notification_show_screenshot_flash", 0);

        // ── Misc / Logging ──
        Cfg_log_verbosity.IsChecked = _parser.GetBool("log_verbosity");
        Cfg_log_to_file.IsChecked = _parser.GetBool("log_to_file");
        Cfg_log_to_file_timestamp.IsChecked = _parser.GetBool("log_to_file_timestamp");
        Cfg_config_save_on_exit.IsChecked = _parser.GetBool("config_save_on_exit", true);
        Cfg_show_hidden_files.IsChecked = _parser.GetBool("show_hidden_files");
        Cfg_playlist_sort_alphabetical.IsChecked = _parser.GetBool("playlist_sort_alphabetical", true);
        Cfg_playlist_compression.IsChecked = _parser.GetBool("playlist_compression", true);
        Cfg_suspend_screensaver_enable.IsChecked = _parser.GetBool("suspend_screensaver_enable", true);
        Cfg_fps_show.IsChecked = _parser.GetBool("fps_show");
        Cfg_framecount_show.IsChecked = _parser.GetBool("framecount_show");
        Cfg_statistics_show.IsChecked = _parser.GetBool("statistics_show");
        Cfg_memory_show.IsChecked = _parser.GetBool("memory_show");
        Cfg_playlist_entry_rename.IsChecked = _parser.GetBool("playlist_entry_rename", true);
        Cfg_playlist_entry_remove.IsChecked = _parser.GetBool("playlist_entry_remove", true);
        Cfg_playlist_use_old_format.IsChecked = _parser.GetBool("playlist_use_old_format");
        Cfg_playlist_show_sublabels.IsChecked = _parser.GetBool("playlist_show_sublabels", true);
        Cfg_playlist_show_entry_idx.IsChecked = _parser.GetBool("playlist_show_entry_idx", true);
        Cfg_playlist_fuzzy_archive_match.IsChecked = _parser.GetBool("playlist_fuzzy_archive_match");
        Cfg_playlist_portable_paths.IsChecked = _parser.GetBool("playlist_portable_paths");
        Cfg_scan_without_core_match.IsChecked = _parser.GetBool("scan_without_core_match");
        Cfg_load_dummy_on_core_shutdown.IsChecked = _parser.GetBool("load_dummy_on_core_shutdown", true);

        Cfg_content_history_size.Value = _parser.GetInt("content_history_size", 200);
        Cfg_fps_update_interval.Value = _parser.GetInt("fps_update_interval", 256);
        Cfg_game_history_size.Value = _parser.GetInt("game_history_size", 200);
        Cfg_playlist_sublabel_runtime_type.Value = _parser.GetInt("playlist_sublabel_runtime_type", 0);
        Cfg_playlist_sublabel_last_played_style.Value = _parser.GetInt("playlist_sublabel_last_played_style", 0);
        Cfg_quit_on_close_content.Value = _parser.GetInt("quit_on_close_content", 0);

        _isPopulating = false;
    }

    private void CollectFromUI()
    {
        if (_parser == null) return;

        // ── Directories / Paths ──
        _parser.SetString("libretro_directory", Cfg_libretro_directory.Text ?? "");
        _parser.SetString("libretro_info_path", Cfg_libretro_info_path.Text ?? "");
        _parser.SetString("system_directory", Cfg_system_directory.Text ?? "");
        _parser.SetString("content_database_path", Cfg_content_database_path.Text ?? "");
        _parser.SetString("cheat_database_path", Cfg_cheat_database_path.Text ?? "");
        _parser.SetString("cursor_directory", Cfg_cursor_directory.Text ?? "");
        _parser.SetString("assets_directory", Cfg_assets_directory.Text ?? "");
        _parser.SetString("savefile_directory", Cfg_savefile_directory.Text ?? "");
        _parser.SetString("savestate_directory", Cfg_savestate_directory.Text ?? "");
        _parser.SetString("screenshot_directory", Cfg_screenshot_directory.Text ?? "");
        _parser.SetString("cache_directory", Cfg_cache_directory.Text ?? "");
        _parser.SetString("playlist_directory", Cfg_playlist_directory.Text ?? "");
        _parser.SetString("core_assets_directory", Cfg_core_assets_directory.Text ?? "");
        _parser.SetString("thumbnails_directory", Cfg_thumbnails_directory.Text ?? "");
        _parser.SetString("dynamic_wallpapers_directory", Cfg_dynamic_wallpapers_directory.Text ?? "");
        _parser.SetString("video_shader_dir", Cfg_video_shader_dir.Text ?? "");
        _parser.SetString("video_filter_dir", Cfg_video_filter_dir.Text ?? "");
        _parser.SetString("audio_filter_dir", Cfg_audio_filter_dir.Text ?? "");
        _parser.SetString("input_remapping_directory", Cfg_input_remapping_directory.Text ?? "");
        _parser.SetString("joypad_autoconfig_dir", Cfg_joypad_autoconfig_dir.Text ?? "");
        _parser.SetString("rgui_config_directory", Cfg_rgui_config_directory.Text ?? "");
        _parser.SetString("rgui_browser_directory", Cfg_rgui_browser_directory.Text ?? "");
        _parser.SetString("overlay_directory", Cfg_overlay_directory.Text ?? "");
        _parser.SetString("osk_overlay_directory", Cfg_osk_overlay_directory.Text ?? "");
        _parser.SetString("recording_output_directory", Cfg_recording_output_directory.Text ?? "");
        _parser.SetString("recording_config_directory", Cfg_recording_config_directory.Text ?? "");
        _parser.SetString("log_dir", Cfg_log_dir.Text ?? "");

        // ── Video ──
        _parser.SetString("video_driver", GetCombo(Cfg_video_driver, "gl"));
        _parser.SetString("video_context_driver", Cfg_video_context_driver.Text ?? "");
        _parser.SetBool("video_fullscreen", Cfg_video_fullscreen.IsChecked == true);
        _parser.SetBool("video_windowed_fullscreen", Cfg_video_windowed_fullscreen.IsChecked == true);
        _parser.SetBool("video_window_show_decorations", Cfg_video_window_show_decorations.IsChecked == true);
        _parser.SetBool("video_allow_rotate", Cfg_video_allow_rotate.IsChecked == true);
        _parser.SetBool("video_vsync", Cfg_video_vsync.IsChecked == true);
        _parser.SetBool("video_adaptive_vsync", Cfg_video_adaptive_vsync.IsChecked == true);
        _parser.SetBool("video_hard_sync", Cfg_video_hard_sync.IsChecked == true);
        _parser.SetBool("video_frame_delay_auto", Cfg_video_frame_delay_auto.IsChecked == true);
        _parser.SetBool("video_force_aspect", Cfg_video_force_aspect.IsChecked == true);
        _parser.SetBool("video_aspect_ratio_auto", Cfg_video_aspect_ratio_auto.IsChecked == true);
        _parser.SetBool("video_smooth", Cfg_video_smooth.IsChecked == true);
        _parser.SetBool("video_crop_overscan", Cfg_video_crop_overscan.IsChecked == true);
        _parser.SetBool("video_threaded", Cfg_video_threaded.IsChecked == true);
        _parser.SetBool("video_shader_enable", Cfg_video_shader_enable.IsChecked == true);
        _parser.SetBool("video_font_enable", Cfg_video_font_enable.IsChecked == true);
        _parser.SetBool("video_gpu_screenshot", Cfg_video_gpu_screenshot.IsChecked == true);
        _parser.SetBool("video_ctx_scaling", Cfg_video_ctx_scaling.IsChecked == true);
        _parser.SetBool("video_shared_context", Cfg_video_shared_context.IsChecked == true);
        _parser.SetBool("video_force_srgb_disable", Cfg_video_force_srgb_disable.IsChecked == true);
        _parser.SetBool("video_notch_write_over_enable", Cfg_video_notch_write_over_enable.IsChecked == true);
        _parser.SetBool("crt_switch_resolution_use_custom_refresh_rate", Cfg_crt_switch_resolution_use_custom_refresh_rate.IsChecked == true);

        _parser.SetInt("video_monitor_index", (int)(Cfg_video_monitor_index.Value ?? 0));
        _parser.SetInt("video_fullscreen_x", (int)(Cfg_video_fullscreen_x.Value ?? 0));
        _parser.SetInt("video_fullscreen_y", (int)(Cfg_video_fullscreen_y.Value ?? 0));
        _parser.SetInt("video_window_opacity", (int)(Cfg_video_window_opacity.Value ?? 100));
        _parser.SetInt("video_swap_interval", (int)(Cfg_video_swap_interval.Value ?? 1));
        _parser.SetInt("video_max_swapchain_images", (int)(Cfg_video_max_swapchain_images.Value ?? 3));
        _parser.SetInt("video_hard_sync_frames", (int)(Cfg_video_hard_sync_frames.Value ?? 0));
        _parser.SetInt("video_frame_delay", (int)(Cfg_video_frame_delay.Value ?? 0));
        _parser.SetDouble("video_scale", (double)(Cfg_video_scale.Value ?? 3m));
        _parser.SetInt("video_black_frame_insertion", (int)(Cfg_video_black_frame_insertion.Value ?? 0));
        _parser.SetDouble("video_font_size", (double)(Cfg_video_font_size.Value ?? 32m));
        _parser.SetInt("video_window_x", (int)(Cfg_video_window_x.Value ?? 0));
        _parser.SetInt("video_window_y", (int)(Cfg_video_window_y.Value ?? 0));
        _parser.SetInt("video_window_auto_width_max", (int)(Cfg_video_window_auto_width_max.Value ?? 0));
        _parser.SetInt("video_window_auto_height_max", (int)(Cfg_video_window_auto_height_max.Value ?? 0));
        _parser.SetDouble("video_msg_pos_x", (double)(Cfg_video_msg_pos_x.Value ?? 0.05m));
        _parser.SetDouble("video_msg_pos_y", (double)(Cfg_video_msg_pos_y.Value ?? 0.05m));
        _parser.SetInt("video_record_threads", (int)(Cfg_video_record_threads.Value ?? 2));
        _parser.SetInt("crt_switch_resolution_super", (int)(Cfg_crt_switch_resolution_super.Value ?? 2560));

        _parser.SetString("video_aspect_ratio", Cfg_video_aspect_ratio.Text ?? "");
        _parser.SetString("video_font_path", Cfg_video_font_path.Text ?? "");
        _parser.SetString("video_msg_color", Cfg_video_msg_color.Text ?? "");
        _parser.SetString("video_shader", Cfg_video_shader.Text ?? "");
        _parser.SetString("video_filter", Cfg_video_filter.Text ?? "");

        _parser.SetInt("video_rotation", GetComboIndex(Cfg_video_rotation));
        _parser.SetInt("crt_switch_resolution", GetComboIndex(Cfg_crt_switch_resolution));

        // ── Audio ──
        _parser.SetString("audio_driver", GetCombo(Cfg_audio_driver, "pulse"));
        _parser.SetString("audio_resampler", GetCombo(Cfg_audio_resampler, "sinc"));
        _parser.SetString("audio_output_rate", GetCombo(Cfg_audio_output_rate, "48000"));
        _parser.SetString("audio_device", Cfg_audio_device.Text ?? "");
        _parser.SetString("audio_dsp_plugin", Cfg_audio_dsp_plugin.Text ?? "");

        _parser.SetBool("audio_enable", Cfg_audio_enable.IsChecked == true);
        _parser.SetBool("audio_sync", Cfg_audio_sync.IsChecked == true);
        _parser.SetBool("audio_rate_control", Cfg_audio_rate_control.IsChecked == true);
        _parser.SetBool("audio_mute_enable", Cfg_audio_mute_enable.IsChecked == true);
        _parser.SetBool("audio_mixer_mute_enable", Cfg_audio_mixer_mute_enable.IsChecked == true);
        _parser.SetBool("audio_fastforward_mute", Cfg_audio_fastforward_mute.IsChecked == true);
        _parser.SetBool("audio_wasapi_exclusive_mode", Cfg_audio_wasapi_exclusive_mode.IsChecked == true);
        _parser.SetBool("audio_wasapi_float_format", Cfg_audio_wasapi_float_format.IsChecked == true);
        _parser.SetBool("audio_fastforward_speedup", Cfg_audio_fastforward_speedup.IsChecked == true);

        _parser.SetDouble("audio_volume", Cfg_audio_volume.Value);
        _parser.SetDouble("audio_mixer_volume", Cfg_audio_mixer_volume.Value);
        _parser.SetInt("audio_latency", (int)(Cfg_audio_latency.Value ?? 64));
        _parser.SetDouble("audio_rate_control_delta", (double)(Cfg_audio_rate_control_delta.Value ?? 0.005m));
        _parser.SetDouble("audio_max_timing_skew", (double)(Cfg_audio_max_timing_skew.Value ?? 0.05m));
        _parser.SetInt("audio_wasapi_sh_buffer_length", (int)(Cfg_audio_wasapi_sh_buffer_length.Value ?? 0));

        // ── Input ──
        _parser.SetString("input_driver", GetCombo(Cfg_input_driver, "x"));
        _parser.SetString("input_joypad_driver", GetCombo(Cfg_input_joypad_driver, "sdl2"));
        _parser.SetInt("input_max_users", (int)(Cfg_input_max_users.Value ?? 5));
        _parser.SetInt("input_turbo_period", (int)(Cfg_input_turbo_period.Value ?? 6));
        _parser.SetInt("input_turbo_duty_cycle", (int)(Cfg_input_turbo_duty_cycle.Value ?? 3));

        _parser.SetBool("input_autodetect_enable", Cfg_input_autodetect_enable.IsChecked == true);
        _parser.SetBool("input_remap_binds_enable", Cfg_input_remap_binds_enable.IsChecked == true);
        _parser.SetBool("input_auto_mouse_grab", Cfg_input_auto_mouse_grab.IsChecked == true);
        _parser.SetBool("input_rumble_enable", Cfg_input_rumble_enable.IsChecked == true);
        _parser.SetBool("input_sensors_enable", Cfg_input_sensors_enable.IsChecked == true);
        _parser.SetBool("input_descriptor_label_show", Cfg_input_descriptor_label_show.IsChecked == true);
        _parser.SetBool("input_descriptor_hide_unbound", Cfg_input_descriptor_hide_unbound.IsChecked == true);

        _parser.SetDouble("input_axis_threshold", (double)(Cfg_input_axis_threshold.Value ?? 0.5m));
        _parser.SetInt("input_bind_timeout", (int)(Cfg_input_bind_timeout.Value ?? 5));
        _parser.SetInt("input_bind_hold", (int)(Cfg_input_bind_hold.Value ?? 2));
        _parser.SetInt("input_turbo_mode", (int)(Cfg_input_turbo_mode.Value ?? 0));
        _parser.SetInt("input_turbo_default_button", (int)(Cfg_input_turbo_default_button.Value ?? 2));
        _parser.SetInt("input_auto_game_focus", (int)(Cfg_input_auto_game_focus.Value ?? 0));
        _parser.SetInt("input_rumble_gain", (int)(Cfg_input_rumble_gain.Value ?? 100));
        _parser.SetInt("input_menu_toggle_gamepad_combo", (int)(Cfg_input_menu_toggle_gamepad_combo.Value ?? 0));
        _parser.SetInt("input_quit_gamepad_combo", (int)(Cfg_input_quit_gamepad_combo.Value ?? 0));
        _parser.SetInt("input_poll_type_behavior", (int)(Cfg_input_poll_type_behavior.Value ?? 2));

        // Hotkeys
        _parser.SetString("input_enable_hotkey", Cfg_input_enable_hotkey.Text ?? "");
        _parser.SetString("input_exit_emulator", Cfg_input_exit_emulator.Text ?? "");
        _parser.SetString("input_toggle_fullscreen", Cfg_input_toggle_fullscreen.Text ?? "");
        _parser.SetString("input_save_state", Cfg_input_save_state.Text ?? "");
        _parser.SetString("input_load_state", Cfg_input_load_state.Text ?? "");
        _parser.SetString("input_toggle_fast_forward", Cfg_input_toggle_fast_forward.Text ?? "");
        _parser.SetString("input_rewind", Cfg_input_rewind.Text ?? "");
        _parser.SetString("input_pause_toggle", Cfg_input_pause_toggle.Text ?? "");
        _parser.SetString("input_reset", Cfg_input_reset.Text ?? "");
        _parser.SetString("input_screenshot", Cfg_input_screenshot.Text ?? "");
        _parser.SetString("input_audio_mute", Cfg_input_audio_mute.Text ?? "");
        _parser.SetString("input_menu_toggle", Cfg_input_menu_toggle.Text ?? "");
        _parser.SetString("input_state_slot_increase", Cfg_input_state_slot_increase.Text ?? "");
        _parser.SetString("input_state_slot_decrease", Cfg_input_state_slot_decrease.Text ?? "");
        _parser.SetString("input_hold_fast_forward", Cfg_input_hold_fast_forward.Text ?? "");
        _parser.SetString("input_toggle_slowmotion", Cfg_input_toggle_slowmotion.Text ?? "");
        _parser.SetString("input_hold_slowmotion", Cfg_input_hold_slowmotion.Text ?? "");
        _parser.SetString("input_frame_advance", Cfg_input_frame_advance.Text ?? "");
        _parser.SetString("input_shader_next", Cfg_input_shader_next.Text ?? "");
        _parser.SetString("input_shader_prev", Cfg_input_shader_prev.Text ?? "");
        _parser.SetString("input_cheat_index_plus", Cfg_input_cheat_index_plus.Text ?? "");
        _parser.SetString("input_cheat_index_minus", Cfg_input_cheat_index_minus.Text ?? "");
        _parser.SetString("input_cheat_toggle", Cfg_input_cheat_toggle.Text ?? "");
        _parser.SetString("input_osk_toggle", Cfg_input_osk_toggle.Text ?? "");
        _parser.SetString("input_fps_toggle", Cfg_input_fps_toggle.Text ?? "");
        _parser.SetString("input_netplay_game_watch", Cfg_input_netplay_game_watch.Text ?? "");
        _parser.SetString("input_volume_up", Cfg_input_volume_up.Text ?? "");
        _parser.SetString("input_volume_down", Cfg_input_volume_down.Text ?? "");
        _parser.SetString("input_overlay_next", Cfg_input_overlay_next.Text ?? "");
        _parser.SetString("input_disk_eject_toggle", Cfg_input_disk_eject_toggle.Text ?? "");
        _parser.SetString("input_disk_next", Cfg_input_disk_next.Text ?? "");
        _parser.SetString("input_disk_prev", Cfg_input_disk_prev.Text ?? "");
        _parser.SetString("input_grab_mouse_toggle", Cfg_input_grab_mouse_toggle.Text ?? "");
        _parser.SetString("input_game_focus_toggle", Cfg_input_game_focus_toggle.Text ?? "");
        _parser.SetString("input_recording_toggle", Cfg_input_recording_toggle.Text ?? "");
        _parser.SetString("input_streaming_toggle", Cfg_input_streaming_toggle.Text ?? "");
        _parser.SetString("input_ai_service", Cfg_input_ai_service.Text ?? "");

        // ── Core / Saving ──
        _parser.SetBool("core_updater_auto_extract", Cfg_core_updater_auto_extract.IsChecked == true);
        _parser.SetBool("core_updater_show_experimental_cores", Cfg_core_updater_show_experimental_cores.IsChecked == true);
        _parser.SetBool("auto_remaps_enable", Cfg_auto_remaps_enable.IsChecked == true);
        _parser.SetBool("auto_shaders_enable", Cfg_auto_shaders_enable.IsChecked == true);
        _parser.SetBool("game_specific_options", Cfg_game_specific_options.IsChecked == true);
        _parser.SetBool("core_option_category_enable", Cfg_core_option_category_enable.IsChecked == true);
        _parser.SetBool("core_set_supports_no_game_enable", Cfg_core_set_supports_no_game_enable.IsChecked == true);
        _parser.SetInt("libretro_log_level", GetComboIndex(Cfg_libretro_log_level));

        _parser.SetBool("savestate_auto_save", Cfg_savestate_auto_save.IsChecked == true);
        _parser.SetBool("savestate_auto_load", Cfg_savestate_auto_load.IsChecked == true);
        _parser.SetBool("savestate_auto_index", Cfg_savestate_auto_index.IsChecked == true);
        _parser.SetBool("savestate_thumbnail_enable", Cfg_savestate_thumbnail_enable.IsChecked == true);
        _parser.SetBool("save_file_compression", Cfg_save_file_compression.IsChecked == true);
        _parser.SetBool("savestate_file_compression", Cfg_savestate_file_compression.IsChecked == true);
        _parser.SetBool("sort_savefiles_enable", Cfg_sort_savefiles_enable.IsChecked == true);
        _parser.SetBool("sort_savestates_enable", Cfg_sort_savestates_enable.IsChecked == true);
        _parser.SetBool("block_sram_overwrite", Cfg_block_sram_overwrite.IsChecked == true);
        _parser.SetBool("sort_savefiles_by_content_enable", Cfg_sort_savefiles_by_content_enable.IsChecked == true);
        _parser.SetBool("sort_savestates_by_content_enable", Cfg_sort_savestates_by_content_enable.IsChecked == true);
        _parser.SetBool("content_runtime_log", Cfg_content_runtime_log.IsChecked == true);
        _parser.SetBool("content_runtime_log_aggregate", Cfg_content_runtime_log_aggregate.IsChecked == true);
        _parser.SetInt("autosave_interval", (int)(Cfg_autosave_interval.Value ?? 0));
        _parser.SetInt("savestate_max_keep", (int)(Cfg_savestate_max_keep.Value ?? 0));

        // ── Menu / UI ──
        _parser.SetString("menu_driver", GetCombo(Cfg_menu_driver, "ozone"));
        _parser.SetBool("menu_linear_filter", Cfg_menu_linear_filter.IsChecked == true);
        _parser.SetBool("menu_pause_libretro", Cfg_menu_pause_libretro.IsChecked == true);
        _parser.SetBool("menu_mouse_enable", Cfg_menu_mouse_enable.IsChecked == true);
        _parser.SetBool("menu_show_sublabels", Cfg_menu_show_sublabels.IsChecked == true);
        _parser.SetBool("menu_timedate_enable", Cfg_menu_timedate_enable.IsChecked == true);
        _parser.SetBool("menu_battery_level_enable", Cfg_menu_battery_level_enable.IsChecked == true);
        _parser.SetBool("menu_core_enable", Cfg_menu_core_enable.IsChecked == true);
        _parser.SetBool("menu_dynamic_wallpaper_enable", Cfg_menu_dynamic_wallpaper_enable.IsChecked == true);
        _parser.SetBool("menu_show_quit_retroarch", Cfg_menu_show_quit_retroarch.IsChecked == true);
        _parser.SetBool("menu_pointer_enable", Cfg_menu_pointer_enable.IsChecked == true);
        _parser.SetBool("menu_swap_ok_cancel_buttons", Cfg_menu_swap_ok_cancel_buttons.IsChecked == true);
        _parser.SetBool("menu_show_online_updater", Cfg_menu_show_online_updater.IsChecked == true);
        _parser.SetBool("menu_show_core_updater", Cfg_menu_show_core_updater.IsChecked == true);
        _parser.SetBool("menu_show_load_core", Cfg_menu_show_load_core.IsChecked == true);
        _parser.SetBool("menu_show_load_content", Cfg_menu_show_load_content.IsChecked == true);
        _parser.SetBool("menu_show_information", Cfg_menu_show_information.IsChecked == true);
        _parser.SetBool("menu_show_configurations", Cfg_menu_show_configurations.IsChecked == true);
        _parser.SetBool("menu_show_help", Cfg_menu_show_help.IsChecked == true);
        _parser.SetBool("menu_show_restart_retroarch", Cfg_menu_show_restart_retroarch.IsChecked == true);
        _parser.SetBool("menu_show_reboot", Cfg_menu_show_reboot.IsChecked == true);
        _parser.SetBool("menu_show_shutdown", Cfg_menu_show_shutdown.IsChecked == true);
        _parser.SetBool("menu_horizontal_animation", Cfg_menu_horizontal_animation.IsChecked == true);
        _parser.SetBool("menu_savestate_resume", Cfg_menu_savestate_resume.IsChecked == true);
        _parser.SetBool("menu_insert_disk_resume", Cfg_menu_insert_disk_resume.IsChecked == true);

        _parser.SetInt("ozone_menu_color_theme", (int)(Cfg_ozone_menu_color_theme.Value ?? 1));
        _parser.SetInt("materialui_menu_color_theme", (int)(Cfg_materialui_menu_color_theme.Value ?? 0));
        _parser.SetInt("xmb_alpha_factor", (int)(Cfg_xmb_alpha_factor.Value ?? 75));
        _parser.SetInt("menu_screensaver_timeout", (int)(Cfg_menu_screensaver_timeout.Value ?? 0));
        _parser.SetInt("menu_timedate_style", (int)(Cfg_menu_timedate_style.Value ?? 5));
        _parser.SetInt("menu_ticker_type", (int)(Cfg_menu_ticker_type.Value ?? 0));
        _parser.SetDouble("menu_ticker_speed", (double)(Cfg_menu_ticker_speed.Value ?? 2.0m));
        _parser.SetInt("menu_xmb_animation_horizontal_highlight", (int)(Cfg_menu_xmb_animation_horizontal_highlight.Value ?? 0));
        _parser.SetInt("menu_xmb_animation_move_up_down", (int)(Cfg_menu_xmb_animation_move_up_down.Value ?? 0));
        _parser.SetInt("menu_xmb_animation_opening_main_menu", (int)(Cfg_menu_xmb_animation_opening_main_menu.Value ?? 0));
        _parser.SetInt("menu_remember_selection", (int)(Cfg_menu_remember_selection.Value ?? 1));
        _parser.SetInt("menu_quit_on_close_content", (int)(Cfg_menu_quit_on_close_content.Value ?? 0));
        _parser.SetInt("menu_screensaver_animation", (int)(Cfg_menu_screensaver_animation.Value ?? 1));
        _parser.SetDouble("menu_screensaver_animation_speed", (double)(Cfg_menu_screensaver_animation_speed.Value ?? 1.0m));

        _parser.SetBool("xmb_dark_mode", Cfg_xmb_dark_mode.IsChecked == true);

        // RGUI Theme
        _parser.SetInt("menu_rgui_internal_upscale_level", (int)(Cfg_menu_rgui_internal_upscale_level.Value ?? 0));
        _parser.SetBool("menu_rgui_full_width_layout", Cfg_menu_rgui_full_width_layout.IsChecked == true);
        _parser.SetInt("menu_rgui_color_theme", (int)(Cfg_menu_rgui_color_theme.Value ?? 0));
        _parser.SetBool("menu_rgui_shadows", Cfg_menu_rgui_shadows.IsChecked == true);
        _parser.SetInt("menu_rgui_particle_effect", (int)(Cfg_menu_rgui_particle_effect.Value ?? 0));
        _parser.SetInt("menu_rgui_thumbnail_downscaler", (int)(Cfg_menu_rgui_thumbnail_downscaler.Value ?? 0));
        _parser.SetBool("menu_rgui_inline_thumbnails", Cfg_menu_rgui_inline_thumbnails.IsChecked == true);
        _parser.SetBool("menu_rgui_swap_thumbnails", Cfg_menu_rgui_swap_thumbnails.IsChecked == true);
        _parser.SetBool("menu_rgui_extended_ascii", Cfg_menu_rgui_extended_ascii.IsChecked == true);
        _parser.SetBool("menu_rgui_background_filler_thickness_enable", Cfg_menu_rgui_background_filler_thickness_enable.IsChecked == true);
        _parser.SetBool("menu_rgui_border_filler_enable", Cfg_menu_rgui_border_filler_enable.IsChecked == true);
        _parser.SetBool("menu_rgui_border_filler_thickness_enable", Cfg_menu_rgui_border_filler_thickness_enable.IsChecked == true);

        // ── Network / Netplay ──
        _parser.SetBool("network_cmd_enable", Cfg_network_cmd_enable.IsChecked == true);
        _parser.SetBool("network_remote_enable", Cfg_network_remote_enable.IsChecked == true);
        _parser.SetBool("stdin_cmd_enable", Cfg_stdin_cmd_enable.IsChecked == true);
        _parser.SetBool("netplay", Cfg_netplay.IsChecked == true);
        _parser.SetBool("netplay_use_mitm_server", Cfg_netplay_use_mitm_server.IsChecked == true);
        _parser.SetBool("netplay_nat_traversal", Cfg_netplay_nat_traversal.IsChecked == true);
        _parser.SetBool("netplay_stateless_mode", Cfg_netplay_stateless_mode.IsChecked == true);
        _parser.SetBool("netplay_start_as_spectator", Cfg_netplay_start_as_spectator.IsChecked == true);
        _parser.SetBool("netplay_allow_slaves", Cfg_netplay_allow_slaves.IsChecked == true);
        _parser.SetBool("netplay_require_slaves", Cfg_netplay_require_slaves.IsChecked == true);
        _parser.SetBool("netplay_request_device_p1", Cfg_netplay_request_device_p1.IsChecked == true);
        _parser.SetBool("netplay_request_device_p2", Cfg_netplay_request_device_p2.IsChecked == true);
        _parser.SetBool("netplay_request_device_p3", Cfg_netplay_request_device_p3.IsChecked == true);
        _parser.SetBool("netplay_request_device_p4", Cfg_netplay_request_device_p4.IsChecked == true);
        _parser.SetBool("netplay_request_device_p5", Cfg_netplay_request_device_p5.IsChecked == true);
        _parser.SetBool("network_on_demand_thumbnails", Cfg_network_on_demand_thumbnails.IsChecked == true);

        _parser.SetInt("network_cmd_port", (int)(Cfg_network_cmd_port.Value ?? 55355));
        _parser.SetInt("netplay_ip_port", (int)(Cfg_netplay_ip_port.Value ?? 55435));
        _parser.SetInt("netplay_delay_frames", (int)(Cfg_netplay_delay_frames.Value ?? 16));
        _parser.SetInt("network_remote_base_port", (int)(Cfg_network_remote_base_port.Value ?? 55400));
        _parser.SetInt("netplay_check_frames", (int)(Cfg_netplay_check_frames.Value ?? 600));
        _parser.SetInt("netplay_share_digital", (int)(Cfg_netplay_share_digital.Value ?? 0));
        _parser.SetInt("netplay_share_analog", (int)(Cfg_netplay_share_analog.Value ?? 0));

        _parser.SetString("updater_buildbot_cores_url", Cfg_updater_buildbot_cores_url.Text ?? "");
        _parser.SetString("updater_buildbot_assets_url", Cfg_updater_buildbot_assets_url.Text ?? "");
        _parser.SetString("netplay_ip_address", Cfg_netplay_ip_address.Text ?? "");
        _parser.SetString("netplay_mitm_server", Cfg_netplay_mitm_server.Text ?? "");
        _parser.SetString("netplay_password", Cfg_netplay_password.Text ?? "");
        _parser.SetString("netplay_spectate_password", Cfg_netplay_spectate_password.Text ?? "");

        // ── Frame Throttle / Rewind ──
        _parser.SetDouble("fastforward_ratio", (double)(Cfg_fastforward_ratio.Value ?? 0m));
        _parser.SetDouble("slowmotion_ratio", (double)(Cfg_slowmotion_ratio.Value ?? 3m));
        _parser.SetInt("rewind_buffer_size", (int)(Cfg_rewind_buffer_size.Value ?? 20));
        _parser.SetInt("rewind_buffer_size_step", (int)(Cfg_rewind_buffer_size_step.Value ?? 10));
        _parser.SetInt("rewind_granularity", (int)(Cfg_rewind_granularity.Value ?? 1));
        _parser.SetBool("vrr_runloop_enable", Cfg_vrr_runloop_enable.IsChecked == true);
        _parser.SetBool("rewind_enable", Cfg_rewind_enable.IsChecked == true);
        _parser.SetBool("menu_throttle_framerate", Cfg_menu_throttle_framerate.IsChecked == true);

        // ── Achievements ──
        _parser.SetBool("cheevos_enable", Cfg_cheevos_enable.IsChecked == true);
        _parser.SetBool("cheevos_test_unofficial", Cfg_cheevos_test_unofficial.IsChecked == true);
        _parser.SetBool("cheevos_hardcore_mode_enable", Cfg_cheevos_hardcore_mode_enable.IsChecked == true);
        _parser.SetBool("cheevos_leaderboards_enable", Cfg_cheevos_leaderboards_enable.IsChecked == true);
        _parser.SetBool("cheevos_richpresence_enable", Cfg_cheevos_richpresence_enable.IsChecked == true);
        _parser.SetBool("cheevos_badges_enable", Cfg_cheevos_badges_enable.IsChecked == true);
        _parser.SetBool("cheevos_verbose_enable", Cfg_cheevos_verbose_enable.IsChecked == true);
        _parser.SetBool("cheevos_auto_screenshot", Cfg_cheevos_auto_screenshot.IsChecked == true);
        _parser.SetBool("cheevos_unlock_sound_enable", Cfg_cheevos_unlock_sound_enable.IsChecked == true);
        _parser.SetBool("cheevos_start_active", Cfg_cheevos_start_active.IsChecked == true);
        _parser.SetBool("cheevos_appearance_padding_auto", Cfg_cheevos_appearance_padding_auto.IsChecked == true);
        _parser.SetBool("cheevos_visibility_unlock", Cfg_cheevos_visibility_unlock.IsChecked == true);
        _parser.SetBool("cheevos_visibility_mastery", Cfg_cheevos_visibility_mastery.IsChecked == true);
        _parser.SetBool("cheevos_visibility_account", Cfg_cheevos_visibility_account.IsChecked == true);

        _parser.SetInt("cheevos_appearance_anchor", (int)(Cfg_cheevos_appearance_anchor.Value ?? 0));
        _parser.SetInt("cheevos_visibility_summary", (int)(Cfg_cheevos_visibility_summary.Value ?? 0));

        // ── Overlay ──
        _parser.SetBool("input_overlay_enable", Cfg_input_overlay_enable.IsChecked == true);
        _parser.SetBool("input_overlay_behind_menu", Cfg_input_overlay_behind_menu.IsChecked == true);
        _parser.SetBool("input_overlay_hide_in_menu", Cfg_input_overlay_hide_in_menu.IsChecked == true);
        _parser.SetBool("input_overlay_hide_when_gamepad_connected", Cfg_input_overlay_hide_when_gamepad_connected.IsChecked == true);
        _parser.SetBool("input_overlay_auto_scale", Cfg_input_overlay_auto_scale.IsChecked == true);

        _parser.SetString("input_overlay", Cfg_input_overlay.Text ?? "");

        _parser.SetInt("input_overlay_show_inputs", (int)(Cfg_input_overlay_show_inputs.Value ?? 2));
        _parser.SetInt("input_overlay_show_inputs_port", (int)(Cfg_input_overlay_show_inputs_port.Value ?? 0));
        _parser.SetDouble("input_overlay_opacity", (double)(Cfg_input_overlay_opacity.Value ?? 0.7m));
        _parser.SetDouble("input_overlay_scale_landscape", (double)(Cfg_input_overlay_scale_landscape.Value ?? 1.0m));
        _parser.SetDouble("input_overlay_aspect_adjust_landscape", (double)(Cfg_input_overlay_aspect_adjust_landscape.Value ?? 0.0m));
        _parser.SetDouble("input_overlay_x_separation_landscape", (double)(Cfg_input_overlay_x_separation_landscape.Value ?? 0.0m));
        _parser.SetDouble("input_overlay_y_separation_landscape", (double)(Cfg_input_overlay_y_separation_landscape.Value ?? 0.0m));
        _parser.SetDouble("input_overlay_x_offset_landscape", (double)(Cfg_input_overlay_x_offset_landscape.Value ?? 0.0m));
        _parser.SetDouble("input_overlay_y_offset_landscape", (double)(Cfg_input_overlay_y_offset_landscape.Value ?? 0.0m));
        _parser.SetDouble("input_overlay_scale_portrait", (double)(Cfg_input_overlay_scale_portrait.Value ?? 1.0m));
        _parser.SetDouble("input_overlay_aspect_adjust_portrait", (double)(Cfg_input_overlay_aspect_adjust_portrait.Value ?? 0.0m));
        _parser.SetDouble("input_overlay_x_separation_portrait", (double)(Cfg_input_overlay_x_separation_portrait.Value ?? 0.0m));
        _parser.SetDouble("input_overlay_y_separation_portrait", (double)(Cfg_input_overlay_y_separation_portrait.Value ?? 0.0m));

        // ── Recording ──
        _parser.SetBool("record_enable", Cfg_record_enable.IsChecked == true);
        _parser.SetBool("record_use_output_dir", Cfg_record_use_output_dir.IsChecked == true);

        _parser.SetString("record_output_dir", Cfg_record_output_dir.Text ?? "");
        _parser.SetString("record_config_dir", Cfg_record_config_dir.Text ?? "");

        _parser.SetInt("streaming_mode", (int)(Cfg_streaming_mode.Value ?? 0));

        // ── Accessibility ──
        _parser.SetBool("accessibility_enable", Cfg_accessibility_enable.IsChecked == true);
        _parser.SetBool("ai_service_enable", Cfg_ai_service_enable.IsChecked == true);
        _parser.SetBool("ai_service_pause", Cfg_ai_service_pause.IsChecked == true);

        _parser.SetInt("accessibility_narrator_speech_speed", (int)(Cfg_accessibility_narrator_speech_speed.Value ?? 5));
        _parser.SetInt("ai_service_mode", (int)(Cfg_ai_service_mode.Value ?? 0));
        _parser.SetInt("ai_service_source_lang", (int)(Cfg_ai_service_source_lang.Value ?? 0));
        _parser.SetInt("ai_service_target_lang", (int)(Cfg_ai_service_target_lang.Value ?? 0));

        _parser.SetString("ai_service_url", Cfg_ai_service_url.Text ?? "");

        // ── Notifications ──
        _parser.SetBool("notification_show_autoconfig", Cfg_notification_show_autoconfig.IsChecked == true);
        _parser.SetBool("notification_show_cheats_applied", Cfg_notification_show_cheats_applied.IsChecked == true);
        _parser.SetBool("notification_show_patch_applied", Cfg_notification_show_patch_applied.IsChecked == true);
        _parser.SetBool("notification_show_remap_load", Cfg_notification_show_remap_load.IsChecked == true);
        _parser.SetBool("notification_show_config_override_load", Cfg_notification_show_config_override_load.IsChecked == true);
        _parser.SetBool("notification_show_set_initial_disk", Cfg_notification_show_set_initial_disk.IsChecked == true);
        _parser.SetBool("notification_show_fast_forward", Cfg_notification_show_fast_forward.IsChecked == true);
        _parser.SetBool("notification_show_screenshot", Cfg_notification_show_screenshot.IsChecked == true);
        _parser.SetBool("notification_show_refresh_rate", Cfg_notification_show_refresh_rate.IsChecked == true);
        _parser.SetBool("notification_show_netplay_extra", Cfg_notification_show_netplay_extra.IsChecked == true);
        _parser.SetBool("notification_show_when_menu_is_alive", Cfg_notification_show_when_menu_is_alive.IsChecked == true);

        _parser.SetInt("notification_show_screenshot_duration", (int)(Cfg_notification_show_screenshot_duration.Value ?? 0));
        _parser.SetInt("notification_show_screenshot_flash", (int)(Cfg_notification_show_screenshot_flash.Value ?? 0));

        // ── Misc / Logging ──
        _parser.SetBool("log_verbosity", Cfg_log_verbosity.IsChecked == true);
        _parser.SetBool("log_to_file", Cfg_log_to_file.IsChecked == true);
        _parser.SetBool("log_to_file_timestamp", Cfg_log_to_file_timestamp.IsChecked == true);
        _parser.SetBool("config_save_on_exit", Cfg_config_save_on_exit.IsChecked == true);
        _parser.SetBool("show_hidden_files", Cfg_show_hidden_files.IsChecked == true);
        _parser.SetBool("playlist_sort_alphabetical", Cfg_playlist_sort_alphabetical.IsChecked == true);
        _parser.SetBool("playlist_compression", Cfg_playlist_compression.IsChecked == true);
        _parser.SetBool("suspend_screensaver_enable", Cfg_suspend_screensaver_enable.IsChecked == true);
        _parser.SetBool("fps_show", Cfg_fps_show.IsChecked == true);
        _parser.SetBool("framecount_show", Cfg_framecount_show.IsChecked == true);
        _parser.SetBool("statistics_show", Cfg_statistics_show.IsChecked == true);
        _parser.SetBool("memory_show", Cfg_memory_show.IsChecked == true);
        _parser.SetBool("playlist_entry_rename", Cfg_playlist_entry_rename.IsChecked == true);
        _parser.SetBool("playlist_entry_remove", Cfg_playlist_entry_remove.IsChecked == true);
        _parser.SetBool("playlist_use_old_format", Cfg_playlist_use_old_format.IsChecked == true);
        _parser.SetBool("playlist_show_sublabels", Cfg_playlist_show_sublabels.IsChecked == true);
        _parser.SetBool("playlist_show_entry_idx", Cfg_playlist_show_entry_idx.IsChecked == true);
        _parser.SetBool("playlist_fuzzy_archive_match", Cfg_playlist_fuzzy_archive_match.IsChecked == true);
        _parser.SetBool("playlist_portable_paths", Cfg_playlist_portable_paths.IsChecked == true);
        _parser.SetBool("scan_without_core_match", Cfg_scan_without_core_match.IsChecked == true);
        _parser.SetBool("load_dummy_on_core_shutdown", Cfg_load_dummy_on_core_shutdown.IsChecked == true);

        _parser.SetInt("content_history_size", (int)(Cfg_content_history_size.Value ?? 200));
        _parser.SetInt("fps_update_interval", (int)(Cfg_fps_update_interval.Value ?? 256));
        _parser.SetInt("game_history_size", (int)(Cfg_game_history_size.Value ?? 200));
        _parser.SetInt("playlist_sublabel_runtime_type", (int)(Cfg_playlist_sublabel_runtime_type.Value ?? 0));
        _parser.SetInt("playlist_sublabel_last_played_style", (int)(Cfg_playlist_sublabel_last_played_style.Value ?? 0));
        _parser.SetInt("quit_on_close_content", (int)(Cfg_quit_on_close_content.Value ?? 0));
    }

    // ── Helpers ───────────────────────────────────────────────────────

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

        // Last resort fallback
        if (combo.Items.Count > 0)
            combo.SelectedIndex = 0;
    }

    /// <summary>
    /// Sets a combo box selection by numeric index (for rotation, CRT, log level combos).
    /// </summary>
    private static void SetComboByIndex(ComboBox combo, int index)
    {
        if (index >= 0 && index < combo.Items.Count)
            combo.SelectedIndex = index;
        else if (combo.Items.Count > 0)
            combo.SelectedIndex = 0;
    }

    /// <summary>
    /// Gets the numeric index from a combo box (for rotation, CRT, log level combos).
    /// </summary>
    private static int GetComboIndex(ComboBox combo)
    {
        return combo.SelectedIndex >= 0 ? combo.SelectedIndex : 0;
    }

    private static string GetCombo(ComboBox combo, string defaultValue)
    {
        return combo.SelectedItem?.ToString() ?? defaultValue;
    }

    private void ShowStatus(string message, bool isError)
    {
        CfgStatusText.Text = message;
        CfgStatusText.Foreground = isError ? StatusErrorBrush : StatusSuccessBrush;

        // Reset and restart the auto-hide timer
        _statusTimer.Stop();
        _statusTimer.Start();
    }

    // ── Dirty state tracking ─────────────────────────────────────────

    /// <summary>
    /// Marks the configuration as having unsaved changes.
    /// </summary>
    private void MarkDirty()
    {
        if (_isPopulating || _isDirty) return;
        _isDirty = true;
        SaveButton.IsEnabled = true;
        UnsavedIndicator.IsVisible = true;
    }

    /// <summary>
    /// Clears the dirty flag and hides the unsaved-changes indicator.
    /// </summary>
    private void ClearDirty()
    {
        _isDirty = false;
        SaveButton.IsEnabled = false;
        UnsavedIndicator.IsVisible = false;
    }

    /// <summary>
    /// Shows a confirmation dialog when the user attempts to discard unsaved changes.
    /// Returns true if the user confirms, false otherwise.
    /// </summary>
    private async Task<bool> ConfirmDiscardChanges()
    {
        var loc = LocalizationManager.Instance;
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is Window parentWindow)
        {
            var dialog = new Views.Dialogs.ConfirmDialog(
                loc["RAConfig_ConfirmDiscardTitle"],
                loc["RAConfig_ConfirmDiscardMessage"]);
            return await dialog.ShowDialog(parentWindow);
        }
        return true;
    }

    /// <summary>
    /// Walks the visual tree of the configuration tabs and attaches change
    /// handlers to all interactive controls so any user edit marks the state dirty.
    /// Uses the <see cref="_changeTrackingAttached"/> guard to prevent duplicate subscriptions
    /// and the <see cref="_isPopulating"/> guard to avoid false positives during
    /// programmatic population.
    /// </summary>
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
