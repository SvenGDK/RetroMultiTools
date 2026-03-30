using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using RetroMultiTools.Services;

namespace RetroMultiTools.Utilities.RetroArch;

/// <summary>
/// Parses and writes RetroArch configuration files (retroarch.cfg).
/// Supports the standard RetroArch CFG format: key = "value" pairs,
/// with '#' comments and blank lines preserved during round-trip editing.
/// Compatible with all RetroArch installations on Windows, macOS, and Linux (x64 + arm64).
/// </summary>
public sealed class RetroArchCfgParser
{
    /// <summary>
    /// Represents a single line in a RetroArch CFG file, preserving comments and blank lines.
    /// </summary>
    private sealed class CfgLine
    {
        /// <summary>Original raw text of the line (used for comments/blanks).</summary>
        public string RawText { get; set; } = string.Empty;

        /// <summary>Non-null when the line is a key-value setting.</summary>
        public string? Key { get; set; }

        /// <summary>The value part of a key-value line (without surrounding quotes).</summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>True when this line is a comment or blank (not a setting).</summary>
        public bool IsCommentOrBlank => Key == null;
    }

    private readonly List<CfgLine> _lines = [];
    private readonly Dictionary<string, CfgLine> _settings = new(StringComparer.Ordinal);

    /// <summary>The file path this configuration was loaded from, if any.</summary>
    public string? FilePath { get; private set; }

    /// <summary>
    /// Gets or sets a configuration value by key.
    /// Returns an empty string for unknown keys.
    /// Setting a value for an unknown key adds it to the end of the file.
    /// Values are stored without quotes; quotes are added during serialization.
    /// </summary>
    public string this[string key]
    {
        get => _settings.TryGetValue(key, out var line) ? line.Value : string.Empty;
        set
        {
            if (_settings.TryGetValue(key, out var existing))
            {
                existing.Value = value;
            }
            else
            {
                var newLine = new CfgLine { Key = key, Value = value };
                _lines.Add(newLine);
                _settings[key] = newLine;
            }
        }
    }

    /// <summary>Returns true if the given key exists in the configuration.</summary>
    public bool ContainsKey(string key) => _settings.ContainsKey(key);

    /// <summary>Returns all setting keys present in the configuration.</summary>
    public IEnumerable<string> Keys => _settings.Keys;

    /// <summary>Returns the number of settings (excluding comments/blanks).</summary>
    public int Count => _settings.Count;

    /// <summary>
    /// Parses a RetroArch CFG file from the given path.
    /// RetroArch format: key = "value" (values always quoted).
    /// Preserves comments and blank lines for round-trip fidelity.
    /// </summary>
    public static RetroArchCfgParser Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        var parser = new RetroArchCfgParser { FilePath = filePath };
        string[] lines = File.ReadAllLines(filePath);

        foreach (string rawLine in lines)
        {
            string trimmed = rawLine.TrimStart();

            // Comment or blank line
            if (trimmed.Length == 0 || trimmed[0] == '#')
            {
                parser._lines.Add(new CfgLine { RawText = rawLine });
                continue;
            }

            // RetroArch format: key = "value"
            // The key never contains '=' or spaces.
            int eqIdx = trimmed.IndexOf('=');
            if (eqIdx < 0)
            {
                // No equals sign — treat as raw/comment line to preserve it
                parser._lines.Add(new CfgLine { RawText = rawLine });
                continue;
            }

            string key = trimmed[..eqIdx].TrimEnd();
            string rawValue = trimmed[(eqIdx + 1)..].Trim();

            // Strip surrounding quotes if present
            string value;
            if (rawValue.Length >= 2 && rawValue[0] == '"' && rawValue[^1] == '"')
            {
                value = rawValue[1..^1];
            }
            else
            {
                value = rawValue;
            }

            var kvLine = new CfgLine { Key = key, Value = value };
            parser._lines.Add(kvLine);
            // Last occurrence wins (matches RetroArch behaviour)
            parser._settings[key] = kvLine;
        }

        return parser;
    }

    /// <summary>
    /// Creates an empty parser that can be populated and saved.
    /// </summary>
    public static RetroArchCfgParser CreateEmpty(string? filePath = null)
    {
        return new RetroArchCfgParser { FilePath = filePath };
    }

    /// <summary>
    /// Saves the configuration to the specified path (or the original path).
    /// Uses the standard RetroArch CFG format: key = "value".
    /// </summary>
    public void Save(string? outputPath = null)
    {
        string path = outputPath ?? FilePath
            ?? throw new InvalidOperationException("No file path specified for saving.");

        var sb = new StringBuilder(8192);

        foreach (var line in _lines)
        {
            if (line.IsCommentOrBlank)
            {
                sb.AppendLine(line.RawText);
            }
            else
            {
                sb.AppendLine($"{line.Key} = \"{line.Value}\"");
            }
        }

        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(path, sb.ToString());
        FilePath = path;
    }

    // ── Typed accessors ──────────────────────────────────────────────

    /// <summary>
    /// Gets a boolean value from the configuration.
    /// RetroArch stores booleans as "true" / "false".
    /// </summary>
    public bool GetBool(string key, bool defaultValue = false)
    {
        if (!_settings.TryGetValue(key, out var line))
            return defaultValue;
        return string.Equals(line.Value, "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Sets a boolean value (stored as "true" or "false").
    /// </summary>
    public void SetBool(string key, bool value)
    {
        this[key] = value ? "true" : "false";
    }

    /// <summary>
    /// Gets an integer value from the configuration.
    /// </summary>
    public int GetInt(string key, int defaultValue = 0)
    {
        if (!_settings.TryGetValue(key, out var line))
            return defaultValue;
        return int.TryParse(line.Value, CultureInfo.InvariantCulture, out int result)
            ? result : defaultValue;
    }

    /// <summary>
    /// Sets an integer value.
    /// </summary>
    public void SetInt(string key, int value)
    {
        this[key] = value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets a floating-point value from the configuration.
    /// RetroArch uses formats like "1.000000".
    /// </summary>
    public double GetDouble(string key, double defaultValue = 0.0)
    {
        if (!_settings.TryGetValue(key, out var line))
            return defaultValue;
        return double.TryParse(line.Value, CultureInfo.InvariantCulture, out double result)
            ? result : defaultValue;
    }

    /// <summary>
    /// Sets a floating-point value using RetroArch's 6-decimal-place format.
    /// </summary>
    public void SetDouble(string key, double value)
    {
        this[key] = value.ToString("F6", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets a string value from the configuration.
    /// </summary>
    public string GetString(string key, string defaultValue = "")
    {
        if (!_settings.TryGetValue(key, out var line))
            return defaultValue;
        return string.IsNullOrEmpty(line.Value) ? defaultValue : line.Value;
    }

    /// <summary>
    /// Sets a string value.
    /// </summary>
    public void SetString(string key, string value)
    {
        this[key] = value;
    }

    /// <summary>
    /// Removes a key from the configuration.
    /// </summary>
    public bool Remove(string key)
    {
        if (_settings.Remove(key))
        {
            _lines.RemoveAll(l => l.Key != null &&
                string.Equals(l.Key, key, StringComparison.Ordinal));
            return true;
        }
        return false;
    }

    // ── Static helpers for locating RetroArch configuration files ─────

    /// <summary>
    /// Detects the retroarch.cfg file path.
    /// Checks the configured RetroArch path first, then platform-specific locations.
    /// Returns null if no configuration file is found.
    /// </summary>
    public static string? FindRetroArchCfg()
    {
        // Use the existing launcher infrastructure to find the config file
        string? cfgPath = RetroArchLauncher.GetRetroArchConfigFilePath();
        if (!string.IsNullOrEmpty(cfgPath) && File.Exists(cfgPath))
            return cfgPath;

        // Also check the configured RetroArch path directory
        string configured = AppSettings.Instance.RetroArchPath;
        if (!string.IsNullOrEmpty(configured))
        {
            string resolved = RetroArchLauncher.ResolveRetroArchPath(configured);
            string? exeDir = Path.GetDirectoryName(resolved);
            if (!string.IsNullOrEmpty(exeDir))
            {
                string cfgInDir = Path.Combine(exeDir, "retroarch.cfg");
                if (File.Exists(cfgInDir))
                    return cfgInDir;
            }
        }

        // Platform-specific extra fallbacks
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return FindCfgWindows();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return FindCfgLinux();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return FindCfgMacOS();

        return null;
    }

    /// <summary>
    /// Returns the default path where a new retroarch.cfg should be created.
    /// </summary>
    public static string GetDefaultCfgPath()
    {
        string? configDir = RetroArchLauncher.GetRetroArchConfigDirectory();
        if (!string.IsNullOrEmpty(configDir))
            return Path.Combine(configDir, "retroarch.cfg");

        // Ultimate fallback
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Path.Combine(home, "retroarch.cfg");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return Path.Combine(home, "Library", "Application Support", "RetroArch", "retroarch.cfg");
        return Path.Combine(home, ".config", "retroarch", "retroarch.cfg");
    }

    /// <summary>
    /// Populates a new CFG parser with all standard RetroArch defaults.
    /// This produces a configuration equivalent to a fresh RetroArch install.
    /// </summary>
    public static RetroArchCfgParser CreateWithDefaults(string? filePath = null)
    {
        var parser = new RetroArchCfgParser { FilePath = filePath };

        void AddComment(string text) =>
            parser._lines.Add(new CfgLine { RawText = text });

        void AddSetting(string key, string value)
        {
            var line = new CfgLine { Key = key, Value = value };
            parser._lines.Add(line);
            parser._settings[key] = line;
        }

        void AddBlank() => AddComment("");

        // ── Paths ──
        AddComment("# Paths");
        AddSetting("libretro_directory", "");
        AddSetting("libretro_info_path", "");
        AddSetting("content_database_path", "");
        AddSetting("cheat_database_path", "");
        AddSetting("cursor_directory", "");
        AddSetting("input_remapping_directory", "");
        AddSetting("video_shader_dir", "");
        AddSetting("video_filter_dir", "");
        AddSetting("audio_filter_dir", "");
        AddSetting("core_assets_directory", "");
        AddSetting("assets_directory", "");
        AddSetting("dynamic_wallpapers_directory", "");
        AddSetting("thumbnails_directory", "");
        AddSetting("playlist_directory", "");
        AddSetting("joypad_autoconfig_dir", "");
        AddSetting("rgui_config_directory", "");
        AddSetting("rgui_browser_directory", "");
        AddSetting("overlay_directory", "");
        AddSetting("osk_overlay_directory", "");
        AddSetting("screenshot_directory", "");
        AddSetting("savefile_directory", "");
        AddSetting("savestate_directory", "");
        AddSetting("recording_output_directory", "");
        AddSetting("recording_config_directory", "");
        AddSetting("cache_directory", "");
        AddSetting("system_directory", "");
        AddSetting("log_dir", "");
        AddBlank();

        // ── Video ──
        AddComment("# Video");
        AddSetting("video_driver", "gl");
        AddSetting("video_context_driver", "");
        AddSetting("video_fullscreen", "false");
        AddSetting("video_windowed_fullscreen", "true");
        AddSetting("video_monitor_index", "0");
        AddSetting("video_fullscreen_x", "0");
        AddSetting("video_fullscreen_y", "0");
        AddSetting("video_window_x", "0");
        AddSetting("video_window_y", "0");
        AddSetting("video_window_auto_width_max", "0");
        AddSetting("video_window_auto_height_max", "0");
        AddSetting("video_max_swapchain_images", "3");
        AddSetting("video_adaptive_vsync", "false");
        AddSetting("video_vsync", "true");
        AddSetting("video_swap_interval", "1");
        AddSetting("video_hard_sync", "false");
        AddSetting("video_hard_sync_frames", "0");
        AddSetting("video_frame_delay", "0");
        AddSetting("video_frame_delay_auto", "false");
        AddSetting("video_black_frame_insertion", "0");
        AddSetting("video_gpu_screenshot", "true");
        AddSetting("video_smooth", "false");
        AddSetting("video_ctx_scaling", "false");
        AddSetting("video_shader_enable", "false");
        AddSetting("video_shader", "");
        AddSetting("video_force_aspect", "true");
        AddSetting("video_aspect_ratio_auto", "false");
        AddSetting("video_aspect_ratio", "-1.000000");
        AddSetting("video_scale", "3.000000");
        AddSetting("video_window_opacity", "100");
        AddSetting("video_window_show_decorations", "true");
        AddSetting("video_rotation", "0");
        AddSetting("video_threaded", "false");
        AddSetting("video_font_enable", "true");
        AddSetting("video_font_path", "");
        AddSetting("video_font_size", "32.000000");
        AddSetting("video_msg_pos_x", "0.050000");
        AddSetting("video_msg_pos_y", "0.050000");
        AddSetting("video_msg_color", "ffffff");
        AddSetting("video_crop_overscan", "true");
        AddSetting("video_allow_rotate", "true");
        AddSetting("video_shared_context", "false");
        AddSetting("video_force_srgb_disable", "false");
        AddSetting("video_notch_write_over_enable", "false");
        AddSetting("video_filter", "");
        AddSetting("video_record_threads", "2");
        AddSetting("crt_switch_resolution", "0");
        AddSetting("crt_switch_resolution_super", "2560");
        AddSetting("crt_switch_resolution_use_custom_refresh_rate", "false");
        AddBlank();

        // ── Audio ──
        AddComment("# Audio");
        AddSetting("audio_driver", "");
        AddSetting("audio_resampler", "sinc");
        AddSetting("audio_device", "");
        AddSetting("audio_output_rate", "48000");
        AddSetting("audio_dsp_plugin", "");
        AddSetting("audio_wasapi_exclusive_mode", "false");
        AddSetting("audio_wasapi_float_format", "false");
        AddSetting("audio_wasapi_sh_buffer_length", "0");
        AddSetting("audio_sync", "true");
        AddSetting("audio_rate_control", "true");
        AddSetting("audio_rate_control_delta", "0.005000");
        AddSetting("audio_max_timing_skew", "0.050000");
        AddSetting("audio_volume", "0.000000");
        AddSetting("audio_mixer_volume", "0.000000");
        AddSetting("audio_mute_enable", "false");
        AddSetting("audio_mixer_mute_enable", "false");
        AddSetting("audio_fastforward_mute", "false");
        AddSetting("audio_fastforward_speedup", "false");
        AddSetting("audio_enable", "true");
        AddSetting("audio_latency", "64");
        AddBlank();

        // ── Input ──
        AddComment("# Input");
        AddSetting("input_driver", "");
        AddSetting("input_joypad_driver", "");
        AddSetting("input_max_users", "5");
        AddSetting("input_autodetect_enable", "true");
        AddSetting("input_remap_binds_enable", "true");
        AddSetting("input_descriptor_label_show", "true");
        AddSetting("input_descriptor_hide_unbound", "false");
        AddSetting("input_axis_threshold", "0.500000");
        AddSetting("input_bind_timeout", "5");
        AddSetting("input_bind_hold", "2");
        AddSetting("input_turbo_period", "6");
        AddSetting("input_turbo_duty_cycle", "3");
        AddSetting("input_turbo_mode", "0");
        AddSetting("input_turbo_default_button", "2");
        AddSetting("input_auto_mouse_grab", "false");
        AddSetting("input_auto_game_focus", "0");
        AddSetting("input_rumble_enable", "true");
        AddSetting("input_rumble_gain", "100");
        AddSetting("input_sensors_enable", "true");
        AddSetting("input_menu_toggle_gamepad_combo", "0");
        AddSetting("input_quit_gamepad_combo", "0");
        AddSetting("input_poll_type_behavior", "2");
        AddBlank();

        // ── Input Hotkeys ──
        AddComment("# Input Hotkeys");
        AddSetting("input_enable_hotkey", "nul");
        AddSetting("input_exit_emulator", "escape");
        AddSetting("input_toggle_fullscreen", "f");
        AddSetting("input_save_state", "f2");
        AddSetting("input_load_state", "f4");
        AddSetting("input_state_slot_increase", "f7");
        AddSetting("input_state_slot_decrease", "f6");
        AddSetting("input_toggle_fast_forward", "space");
        AddSetting("input_hold_fast_forward", "l");
        AddSetting("input_toggle_slowmotion", "nul");
        AddSetting("input_hold_slowmotion", "e");
        AddSetting("input_rewind", "r");
        AddSetting("input_pause_toggle", "p");
        AddSetting("input_frame_advance", "k");
        AddSetting("input_reset", "h");
        AddSetting("input_shader_next", "m");
        AddSetting("input_shader_prev", "n");
        AddSetting("input_cheat_index_plus", "y");
        AddSetting("input_cheat_index_minus", "t");
        AddSetting("input_cheat_toggle", "u");
        AddSetting("input_screenshot", "f8");
        AddSetting("input_audio_mute", "f9");
        AddSetting("input_osk_toggle", "f12");
        AddSetting("input_fps_toggle", "nul");
        AddSetting("input_netplay_game_watch", "i");
        AddSetting("input_volume_up", "add");
        AddSetting("input_volume_down", "subtract");
        AddSetting("input_overlay_next", "nul");
        AddSetting("input_disk_eject_toggle", "nul");
        AddSetting("input_disk_next", "nul");
        AddSetting("input_disk_prev", "nul");
        AddSetting("input_grab_mouse_toggle", "f11");
        AddSetting("input_game_focus_toggle", "scroll_lock");
        AddSetting("input_menu_toggle", "f1");
        AddSetting("input_recording_toggle", "nul");
        AddSetting("input_streaming_toggle", "nul");
        AddSetting("input_ai_service", "nul");
        AddBlank();

        // ── Overlay ──
        AddComment("# Overlay");
        AddSetting("input_overlay_enable", "true");
        AddSetting("input_overlay", "");
        AddSetting("input_overlay_behind_menu", "false");
        AddSetting("input_overlay_hide_in_menu", "true");
        AddSetting("input_overlay_hide_when_gamepad_connected", "false");
        AddSetting("input_overlay_show_inputs", "2");
        AddSetting("input_overlay_show_inputs_port", "0");
        AddSetting("input_overlay_opacity", "0.700000");
        AddSetting("input_overlay_auto_scale", "true");
        AddSetting("input_overlay_scale_landscape", "1.000000");
        AddSetting("input_overlay_aspect_adjust_landscape", "0.000000");
        AddSetting("input_overlay_x_separation_landscape", "0.000000");
        AddSetting("input_overlay_y_separation_landscape", "0.000000");
        AddSetting("input_overlay_x_offset_landscape", "0.000000");
        AddSetting("input_overlay_y_offset_landscape", "0.000000");
        AddSetting("input_overlay_scale_portrait", "1.000000");
        AddSetting("input_overlay_aspect_adjust_portrait", "0.000000");
        AddSetting("input_overlay_x_separation_portrait", "0.000000");
        AddSetting("input_overlay_y_separation_portrait", "0.000000");
        AddBlank();

        // ── Core ──
        AddComment("# Core");
        AddSetting("core_updater_auto_extract", "true");
        AddSetting("core_updater_show_experimental_cores", "false");
        AddSetting("libretro_log_level", "1");
        AddSetting("core_set_supports_no_game_enable", "false");
        AddSetting("auto_remaps_enable", "true");
        AddSetting("auto_shaders_enable", "true");
        AddSetting("game_specific_options", "true");
        AddSetting("core_option_category_enable", "true");
        AddBlank();

        // ── Saving ──
        AddComment("# Saving");
        AddSetting("savestate_auto_save", "false");
        AddSetting("savestate_auto_load", "false");
        AddSetting("savestate_auto_index", "false");
        AddSetting("savestate_max_keep", "0");
        AddSetting("savestate_thumbnail_enable", "true");
        AddSetting("save_file_compression", "true");
        AddSetting("savestate_file_compression", "true");
        AddSetting("autosave_interval", "0");
        AddSetting("sort_savefiles_enable", "false");
        AddSetting("sort_savefiles_by_content_enable", "false");
        AddSetting("sort_savestates_enable", "false");
        AddSetting("sort_savestates_by_content_enable", "false");
        AddSetting("block_sram_overwrite", "false");
        AddSetting("content_runtime_log", "true");
        AddSetting("content_runtime_log_aggregate", "true");
        AddBlank();

        // ── Menu / UI ──
        AddComment("# Menu / UI");
        AddSetting("menu_driver", "ozone");
        AddSetting("menu_linear_filter", "true");
        AddSetting("menu_rgui_internal_upscale_level", "0");
        AddSetting("menu_rgui_full_width_layout", "true");
        AddSetting("menu_rgui_color_theme", "0");
        AddSetting("menu_rgui_shadows", "false");
        AddSetting("menu_rgui_particle_effect", "0");
        AddSetting("menu_rgui_thumbnail_downscaler", "0");
        AddSetting("menu_rgui_inline_thumbnails", "false");
        AddSetting("menu_rgui_swap_thumbnails", "false");
        AddSetting("menu_rgui_extended_ascii", "false");
        AddSetting("menu_rgui_background_filler_thickness_enable", "true");
        AddSetting("menu_rgui_border_filler_enable", "true");
        AddSetting("menu_rgui_border_filler_thickness_enable", "true");
        AddSetting("materialui_menu_color_theme", "0");
        AddSetting("ozone_menu_color_theme", "1");
        AddSetting("xmb_alpha_factor", "75");
        AddSetting("xmb_dark_mode", "false");
        AddSetting("menu_show_online_updater", "true");
        AddSetting("menu_show_core_updater", "true");
        AddSetting("menu_show_load_core", "true");
        AddSetting("menu_show_load_content", "true");
        AddSetting("menu_show_information", "true");
        AddSetting("menu_show_configurations", "true");
        AddSetting("menu_show_help", "true");
        AddSetting("menu_show_quit_retroarch", "true");
        AddSetting("menu_show_restart_retroarch", "true");
        AddSetting("menu_show_reboot", "true");
        AddSetting("menu_show_shutdown", "true");
        AddSetting("menu_show_sublabels", "true");
        AddSetting("menu_timedate_enable", "true");
        AddSetting("menu_timedate_style", "5");
        AddSetting("menu_battery_level_enable", "true");
        AddSetting("menu_core_enable", "true");
        AddSetting("menu_dynamic_wallpaper_enable", "true");
        AddSetting("menu_ticker_type", "0");
        AddSetting("menu_ticker_speed", "2.000000");
        AddSetting("menu_horizontal_animation", "true");
        AddSetting("menu_xmb_animation_horizontal_highlight", "0");
        AddSetting("menu_xmb_animation_move_up_down", "0");
        AddSetting("menu_xmb_animation_opening_main_menu", "0");
        AddSetting("menu_remember_selection", "1");
        AddSetting("menu_savestate_resume", "true");
        AddSetting("menu_insert_disk_resume", "true");
        AddSetting("menu_quit_on_close_content", "0");
        AddSetting("menu_screensaver_timeout", "0");
        AddSetting("menu_screensaver_animation", "1");
        AddSetting("menu_screensaver_animation_speed", "1.000000");
        AddSetting("menu_pause_libretro", "true");
        AddSetting("menu_mouse_enable", "true");
        AddSetting("menu_pointer_enable", "true");
        AddSetting("menu_swap_ok_cancel_buttons", "false");
        AddBlank();

        // ── Frame Throttle ──
        AddComment("# Frame Throttle");
        AddSetting("fastforward_ratio", "0.000000");
        AddSetting("slowmotion_ratio", "3.000000");
        AddSetting("vrr_runloop_enable", "false");
        AddSetting("menu_throttle_framerate", "true");
        AddBlank();

        // ── Rewind ──
        AddComment("# Rewind");
        AddSetting("rewind_enable", "false");
        AddSetting("rewind_buffer_size", "20");
        AddSetting("rewind_buffer_size_step", "10");
        AddSetting("rewind_granularity", "1");
        AddBlank();

        // ── Network ──
        AddComment("# Network");
        AddSetting("network_cmd_enable", "false");
        AddSetting("network_cmd_port", "55355");
        AddSetting("network_remote_enable", "false");
        AddSetting("network_remote_base_port", "55400");
        AddSetting("stdin_cmd_enable", "false");
        AddSetting("network_on_demand_thumbnails", "false");
        AddSetting("updater_buildbot_cores_url", "http://buildbot.libretro.com/nightly");
        AddSetting("updater_buildbot_assets_url", "http://buildbot.libretro.com/assets/");
        AddBlank();

        // ── Netplay ──
        AddComment("# Netplay");
        AddSetting("netplay", "false");
        AddSetting("netplay_ip_address", "");
        AddSetting("netplay_ip_port", "55435");
        AddSetting("netplay_delay_frames", "16");
        AddSetting("netplay_check_frames", "600");
        AddSetting("netplay_use_mitm_server", "false");
        AddSetting("netplay_mitm_server", "");
        AddSetting("netplay_password", "");
        AddSetting("netplay_spectate_password", "");
        AddSetting("netplay_start_as_spectator", "false");
        AddSetting("netplay_allow_slaves", "true");
        AddSetting("netplay_require_slaves", "false");
        AddSetting("netplay_stateless_mode", "false");
        AddSetting("netplay_nat_traversal", "false");
        AddSetting("netplay_share_digital", "0");
        AddSetting("netplay_share_analog", "0");
        AddSetting("netplay_request_device_p1", "false");
        AddSetting("netplay_request_device_p2", "false");
        AddSetting("netplay_request_device_p3", "false");
        AddSetting("netplay_request_device_p4", "false");
        AddSetting("netplay_request_device_p5", "false");
        AddBlank();

        // ── Achievements ──
        AddComment("# Achievements");
        AddSetting("cheevos_enable", "false");
        AddSetting("cheevos_test_unofficial", "false");
        AddSetting("cheevos_hardcore_mode_enable", "false");
        AddSetting("cheevos_leaderboards_enable", "false");
        AddSetting("cheevos_richpresence_enable", "true");
        AddSetting("cheevos_badges_enable", "true");
        AddSetting("cheevos_verbose_enable", "false");
        AddSetting("cheevos_auto_screenshot", "false");
        AddSetting("cheevos_start_active", "false");
        AddSetting("cheevos_unlock_sound_enable", "false");
        AddSetting("cheevos_appearance_anchor", "0");
        AddSetting("cheevos_appearance_padding_auto", "true");
        AddSetting("cheevos_visibility_unlock", "true");
        AddSetting("cheevos_visibility_mastery", "true");
        AddSetting("cheevos_visibility_account", "false");
        AddSetting("cheevos_visibility_summary", "0");
        AddBlank();

        // ── Recording ──
        AddComment("# Recording");
        AddSetting("record_enable", "false");
        AddSetting("record_output_dir", "");
        AddSetting("record_config_dir", "");
        AddSetting("record_use_output_dir", "false");
        AddSetting("streaming_mode", "0");
        AddBlank();

        // ── Accessibility ──
        AddComment("# Accessibility");
        AddSetting("accessibility_enable", "false");
        AddSetting("accessibility_narrator_speech_speed", "5");
        AddBlank();

        // ── AI Service ──
        AddComment("# AI Service");
        AddSetting("ai_service_enable", "false");
        AddSetting("ai_service_mode", "0");
        AddSetting("ai_service_url", "http://localhost:4404");
        AddSetting("ai_service_source_lang", "0");
        AddSetting("ai_service_target_lang", "0");
        AddSetting("ai_service_pause", "true");
        AddBlank();

        // ── Logging ──
        AddComment("# Logging");
        AddSetting("log_verbosity", "false");
        AddSetting("log_to_file", "false");
        AddSetting("log_to_file_timestamp", "false");
        AddBlank();

        // ── Notifications ──
        AddComment("# Notifications");
        AddSetting("notification_show_autoconfig", "true");
        AddSetting("notification_show_cheats_applied", "true");
        AddSetting("notification_show_patch_applied", "true");
        AddSetting("notification_show_remap_load", "true");
        AddSetting("notification_show_config_override_load", "true");
        AddSetting("notification_show_set_initial_disk", "true");
        AddSetting("notification_show_fast_forward", "true");
        AddSetting("notification_show_screenshot", "true");
        AddSetting("notification_show_screenshot_duration", "0");
        AddSetting("notification_show_screenshot_flash", "0");
        AddSetting("notification_show_refresh_rate", "false");
        AddSetting("notification_show_netplay_extra", "true");
        AddSetting("notification_show_when_menu_is_alive", "true");
        AddBlank();

        // ── Misc ──
        AddComment("# Miscellaneous");
        AddSetting("config_save_on_exit", "true");
        AddSetting("show_hidden_files", "false");
        AddSetting("game_history_size", "200");
        AddSetting("content_history_size", "200");
        AddSetting("playlist_entry_rename", "true");
        AddSetting("playlist_entry_remove", "true");
        AddSetting("playlist_sort_alphabetical", "true");
        AddSetting("playlist_use_old_format", "false");
        AddSetting("playlist_compression", "true");
        AddSetting("playlist_sublabel_runtime_type", "0");
        AddSetting("playlist_sublabel_last_played_style", "0");
        AddSetting("playlist_show_sublabels", "true");
        AddSetting("playlist_show_entry_idx", "true");
        AddSetting("playlist_fuzzy_archive_match", "false");
        AddSetting("playlist_portable_paths", "false");
        AddSetting("scan_without_core_match", "false");
        AddSetting("quit_on_close_content", "0");
        AddSetting("suspend_screensaver_enable", "true");
        AddSetting("load_dummy_on_core_shutdown", "true");
        AddSetting("fps_show", "false");
        AddSetting("fps_update_interval", "256");
        AddSetting("framecount_show", "false");
        AddSetting("memory_show", "false");
        AddSetting("statistics_show", "false");
        AddBlank();

        return parser;
    }

    // ── Private platform-specific finders ────────────────────────────

    private static string? FindCfgWindows()
    {
        // Common Windows RetroArch locations
        string[] candidates =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RetroArch", "retroarch.cfg"),
            @"C:\RetroArch-Win64\retroarch.cfg",
            @"C:\RetroArch\retroarch.cfg",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "RetroArch", "retroarch.cfg"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "RetroArch", "retroarch.cfg"),
        ];

        foreach (string path in candidates)
        {
            if (File.Exists(path))
                return path;
        }
        return null;
    }

    private static string? FindCfgLinux()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home))
            return null;

        // Standard XDG, Flatpak, Snap, and fallback locations
        string[] candidates =
        [
            Path.Combine(home, ".config", "retroarch", "retroarch.cfg"),
            Path.Combine(home, ".var", "app", "org.libretro.RetroArch", "config", "retroarch", "retroarch.cfg"),
            Path.Combine(home, "snap", "retroarch", "current", ".config", "retroarch", "retroarch.cfg"),
            Path.Combine("/etc", "retroarch.cfg"),
        ];

        foreach (string path in candidates)
        {
            if (File.Exists(path))
                return path;
        }
        return null;
    }

    private static string? FindCfgMacOS()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home))
            return null;

        string[] candidates =
        [
            Path.Combine(home, "Library", "Application Support", "RetroArch", "retroarch.cfg"),
            Path.Combine(home, ".config", "retroarch", "retroarch.cfg"),
        ];

        foreach (string path in candidates)
        {
            if (File.Exists(path))
                return path;
        }
        return null;
    }
}
