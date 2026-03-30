using System.Runtime.InteropServices;
using System.Text;
using RetroMultiTools.Services;

namespace RetroMultiTools.Utilities.Mame;

/// <summary>
/// Parses and writes MAME INI configuration files.
/// Supports the standard MAME INI format: key-value pairs separated by whitespace,
/// with '#' comments and blank lines preserved during round-trip editing.
/// Compatible with all MAME versions on Windows, macOS, and Linux (x64 + arm64).
/// </summary>
public sealed class MameIniParser
{
    /// <summary>
    /// Represents a single line in a MAME INI file, preserving comments and blank lines.
    /// </summary>
    private sealed class IniLine
    {
        /// <summary>Original raw text of the line (used for comments/blanks).</summary>
        public string RawText { get; set; } = string.Empty;

        /// <summary>Non-null when the line is a key-value setting.</summary>
        public string? Key { get; set; }

        /// <summary>The value part of a key-value line.</summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>True when this line is a comment or blank (not a setting).</summary>
        public bool IsCommentOrBlank => Key == null;
    }

    private readonly List<IniLine> _lines = [];
    private readonly Dictionary<string, IniLine> _settings = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The file path this configuration was loaded from, if any.</summary>
    public string? FilePath { get; private set; }

    /// <summary>
    /// Gets or sets a configuration value by key.
    /// Returns an empty string for unknown keys.
    /// Setting a value for an unknown key adds it to the end of the file.
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
                var newLine = new IniLine { Key = key, Value = value };
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
    /// Parses a MAME INI file from the given path.
    /// Preserves comments and blank lines for round-trip fidelity.
    /// </summary>
    public static MameIniParser Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        var parser = new MameIniParser { FilePath = filePath };
        string[] lines = File.ReadAllLines(filePath);

        foreach (string rawLine in lines)
        {
            string trimmed = rawLine.TrimStart();

            // Comment or blank line
            if (trimmed.Length == 0 || trimmed[0] == '#')
            {
                parser._lines.Add(new IniLine { RawText = rawLine });
                continue;
            }

            // MAME INI format: key followed by whitespace then value.
            // Some values may contain spaces (e.g. paths with spaces, semicolon-separated lists).
            // The key is always a single token (no spaces).
            int spaceIdx = trimmed.IndexOfAny([' ', '\t']);
            if (spaceIdx < 0)
            {
                // Key-only line (no value) — treat value as empty string
                var kvLine = new IniLine { Key = trimmed, Value = string.Empty };
                parser._lines.Add(kvLine);
                parser._settings[trimmed] = kvLine;
            }
            else
            {
                string key = trimmed[..spaceIdx];
                string val = trimmed[(spaceIdx + 1)..].TrimStart();

                var kvLine = new IniLine { Key = key, Value = val };
                parser._lines.Add(kvLine);
                // Last occurrence wins (matches MAME behaviour)
                parser._settings[key] = kvLine;
            }
        }

        return parser;
    }

    /// <summary>
    /// Creates an empty parser that can be populated and saved.
    /// </summary>
    public static MameIniParser CreateEmpty(string? filePath = null)
    {
        return new MameIniParser { FilePath = filePath };
    }

    /// <summary>
    /// Saves the configuration to the specified path (or the original path).
    /// Uses the standard MAME INI format with 26-character padded keys.
    /// </summary>
    public void Save(string? outputPath = null)
    {
        string path = outputPath ?? FilePath
            ?? throw new InvalidOperationException("No file path specified for saving.");

        var sb = new StringBuilder(4096);

        foreach (var line in _lines)
        {
            if (line.IsCommentOrBlank)
            {
                sb.AppendLine(line.RawText);
            }
            else
            {
                // MAME uses padded key names (typically 26 chars) for alignment
                string paddedKey = line.Key!.PadRight(26);
                sb.AppendLine($"{paddedKey}{line.Value}");
            }
        }

        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(path, sb.ToString());
        FilePath = path;
    }

    /// <summary>
    /// Gets a boolean value (0/1) from the configuration.
    /// </summary>
    public bool GetBool(string key, bool defaultValue = false)
    {
        if (!_settings.TryGetValue(key, out var line))
            return defaultValue;
        return line.Value == "1";
    }

    /// <summary>
    /// Sets a boolean value (stored as 0 or 1).
    /// </summary>
    public void SetBool(string key, bool value)
    {
        this[key] = value ? "1" : "0";
    }

    /// <summary>
    /// Gets an integer value from the configuration.
    /// </summary>
    public int GetInt(string key, int defaultValue = 0)
    {
        if (!_settings.TryGetValue(key, out var line))
            return defaultValue;
        return int.TryParse(line.Value, System.Globalization.CultureInfo.InvariantCulture, out int result) ? result : defaultValue;
    }

    /// <summary>
    /// Sets an integer value.
    /// </summary>
    public void SetInt(string key, int value)
    {
        this[key] = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets a floating-point value from the configuration.
    /// </summary>
    public double GetDouble(string key, double defaultValue = 0.0)
    {
        if (!_settings.TryGetValue(key, out var line))
            return defaultValue;
        return double.TryParse(line.Value, System.Globalization.CultureInfo.InvariantCulture, out double result)
            ? result : defaultValue;
    }

    /// <summary>
    /// Sets a floating-point value using invariant culture formatting.
    /// </summary>
    public void SetDouble(string key, double value)
    {
        this[key] = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
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
                string.Equals(l.Key, key, StringComparison.OrdinalIgnoreCase));
            return true;
        }
        return false;
    }

    // ── Static helpers for locating MAME configuration files ──────────

    /// <summary>
    /// Detects the MAME INI file path based on the MAME executable location.
    /// MAME looks for mame.ini in the following order:
    /// 1. The directory containing the MAME executable
    /// 2. The ini/ subdirectory of the MAME executable directory
    /// 3. Platform-specific config directories
    /// Returns null if no configuration file is found.
    /// </summary>
    public static string? FindMameIni(string? mameExePath = null)
    {
        // Try the configured MAME path first
        if (string.IsNullOrEmpty(mameExePath))
        {
            string configured = AppSettings.Instance.MamePath;
            if (!string.IsNullOrEmpty(configured))
                mameExePath = MameLauncher.ResolveMamePath(configured);
        }

        if (!string.IsNullOrEmpty(mameExePath) && File.Exists(mameExePath))
        {
            string? mameDir = Path.GetDirectoryName(mameExePath);
            if (!string.IsNullOrEmpty(mameDir))
            {
                // Check mame.ini next to executable
                string iniPath = Path.Combine(mameDir, "mame.ini");
                if (File.Exists(iniPath))
                    return iniPath;

                // Check ini/ subdirectory
                iniPath = Path.Combine(mameDir, "ini", "mame.ini");
                if (File.Exists(iniPath))
                    return iniPath;
            }
        }

        // Platform-specific config directories
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return FindMameIniWindows();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return FindMameIniLinux();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return FindMameIniMacOS();

        return null;
    }

    /// <summary>
    /// Returns the default path where a new mame.ini should be created.
    /// Prefers the directory containing the MAME executable.
    /// Falls back to platform-specific config directories.
    /// </summary>
    public static string GetDefaultIniPath(string? mameExePath = null)
    {
        if (string.IsNullOrEmpty(mameExePath))
        {
            string configured = AppSettings.Instance.MamePath;
            if (!string.IsNullOrEmpty(configured))
                mameExePath = MameLauncher.ResolveMamePath(configured);
        }

        if (!string.IsNullOrEmpty(mameExePath) && File.Exists(mameExePath))
        {
            string? mameDir = Path.GetDirectoryName(mameExePath);
            if (!string.IsNullOrEmpty(mameDir))
                return Path.Combine(mameDir, "mame.ini");
        }

        // Platform-specific fallbacks
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".mame", "mame.ini");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".mame", "mame.ini");
        }

        // Windows: default to user profile
        string profileDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(profileDir, "mame.ini");
    }

    /// <summary>
    /// Populates a new INI parser with all standard MAME defaults.
    /// This produces a configuration equivalent to running "mame -createconfig".
    /// </summary>
    public static MameIniParser CreateWithDefaults(string? filePath = null)
    {
        var parser = new MameIniParser { FilePath = filePath };

        void AddComment(string text) =>
            parser._lines.Add(new IniLine { RawText = text });

        void AddSetting(string key, string value)
        {
            var line = new IniLine { Key = key, Value = value };
            parser._lines.Add(line);
            parser._settings[key] = line;
        }

        void AddBlank() => AddComment("");

        AddComment("#");
        AddComment("# CORE CONFIGURATION OPTIONS");
        AddComment("#");
        AddSetting("readconfig", "1");
        AddBlank();

        AddComment("#");
        AddComment("# CORE SEARCH PATH OPTIONS");
        AddComment("#");
        AddSetting("homepath", ".");
        AddSetting("rompath", "roms");
        AddSetting("hashpath", "hash");
        AddSetting("samplepath", "samples");
        AddSetting("artpath", "artwork");
        AddSetting("ctrlrpath", "ctrlr");
        AddSetting("inipath", GetDefaultIniSearchPath());
        AddSetting("fontpath", ".");
        AddSetting("cheatpath", "cheat");
        AddSetting("crosshairpath", "crosshair");
        AddSetting("pluginspath", "plugins");
        AddSetting("languagepath", "language");
        AddSetting("swpath", "software");
        AddBlank();

        AddComment("#");
        AddComment("# CORE OUTPUT DIRECTORY OPTIONS");
        AddComment("#");
        AddSetting("cfg_directory", "cfg");
        AddSetting("nvram_directory", "nvram");
        AddSetting("input_directory", "inp");
        AddSetting("state_directory", "sta");
        AddSetting("snapshot_directory", "snap");
        AddSetting("diff_directory", "diff");
        AddSetting("comment_directory", "comments");
        AddSetting("share_directory", "share");
        AddBlank();

        AddComment("#");
        AddComment("# CORE STATE/PLAYBACK OPTIONS");
        AddComment("#");
        AddSetting("state", "");
        AddSetting("autosave", "0");
        AddSetting("rewind", "0");
        AddSetting("rewind_capacity", "100");
        AddSetting("playback", "");
        AddSetting("record", "");
        AddSetting("exit_after_playback", "0");
        AddSetting("mngwrite", "");
        AddSetting("aviwrite", "");
        AddSetting("wavwrite", "");
        AddSetting("snapname", "%g/%i");
        AddSetting("snapsize", "auto");
        AddSetting("snapview", "internal");
        AddSetting("snapbilinear", "1");
        AddSetting("statename", "%g");
        AddSetting("burnin", "0");
        AddBlank();

        AddComment("#");
        AddComment("# CORE PERFORMANCE OPTIONS");
        AddComment("#");
        AddSetting("autoframeskip", "0");
        AddSetting("frameskip", "0");
        AddSetting("seconds_to_run", "0");
        AddSetting("throttle", "1");
        AddSetting("sleep", "1");
        AddSetting("speed", "1.0");
        AddSetting("refreshspeed", "0");
        AddSetting("lowlatency", "0");
        AddBlank();

        AddComment("#");
        AddComment("# CORE ROTATION OPTIONS");
        AddComment("#");
        AddSetting("rotate", "1");
        AddSetting("ror", "0");
        AddSetting("rol", "0");
        AddSetting("autoror", "0");
        AddSetting("autorol", "0");
        AddSetting("flipx", "0");
        AddSetting("flipy", "0");
        AddBlank();

        AddComment("#");
        AddComment("# CORE ARTWORK OPTIONS");
        AddComment("#");
        AddSetting("artwork_crop", "0");
        AddSetting("fallback_artwork", "");
        AddSetting("override_artwork", "");
        AddBlank();

        AddComment("#");
        AddComment("# CORE SCREEN OPTIONS");
        AddComment("#");
        AddSetting("brightness", "1.0");
        AddSetting("contrast", "1.0");
        AddSetting("gamma", "1.0");
        AddSetting("pause_brightness", "0.65");
        AddSetting("effect", "none");
        AddBlank();

        AddComment("#");
        AddComment("# CORE VECTOR OPTIONS");
        AddComment("#");
        AddSetting("beam_width_min", "1.0");
        AddSetting("beam_width_max", "1.0");
        AddSetting("beam_dot_size", "1.0");
        AddSetting("beam_intensity_weight", "0");
        AddSetting("flicker", "0");
        AddBlank();

        AddComment("#");
        AddComment("# CORE SOUND OPTIONS");
        AddComment("#");
        AddSetting("samplerate", "48000");
        AddSetting("samples", "1");
        AddSetting("volume", "0");
        AddSetting("compressor", "1");
        AddSetting("speaker_report", "0");
        AddBlank();

        AddComment("#");
        AddComment("# CORE INPUT OPTIONS");
        AddComment("#");
        AddSetting("coin_lockout", "1");
        AddSetting("ctrlr", "");
        AddSetting("mouse", "0");
        AddSetting("joystick", "1");
        AddSetting("lightgun", "0");
        AddSetting("multikeyboard", "0");
        AddSetting("multimouse", "0");
        AddSetting("steadykey", "0");
        AddSetting("ui_active", "0");
        AddSetting("offscreen_reload", "0");
        AddSetting("joystick_map", "auto");
        AddSetting("joystick_deadzone", "0.15");
        AddSetting("joystick_saturation", "0.85");
        AddSetting("joystick_threshold", "0.3");
        AddSetting("natural", "0");
        AddSetting("joystick_contradictory", "0");
        AddSetting("coin_impulse", "0");
        AddBlank();

        AddComment("#");
        AddComment("# CORE INPUT AUTOMATIC ENABLE OPTIONS");
        AddComment("#");
        AddSetting("paddle_device", "keyboard");
        AddSetting("adstick_device", "keyboard");
        AddSetting("pedal_device", "keyboard");
        AddSetting("dial_device", "keyboard");
        AddSetting("trackball_device", "keyboard");
        AddSetting("lightgun_device", "keyboard");
        AddSetting("positional_device", "keyboard");
        AddSetting("mouse_device", "mouse");
        AddBlank();

        AddComment("#");
        AddComment("# CORE DEBUGGING OPTIONS");
        AddComment("#");
        AddSetting("verbose", "0");
        AddSetting("log", "0");
        AddSetting("oslog", "0");
        AddSetting("debug", "0");
        AddSetting("update_in_pause", "0");
        AddSetting("debugscript", "");
        AddSetting("debuglog", "0");
        AddBlank();

        AddComment("#");
        AddComment("# CORE COMMUNICATION OPTIONS");
        AddComment("#");
        AddSetting("comm_localhost", "0.0.0.0");
        AddSetting("comm_localport", "15112");
        AddSetting("comm_remotehost", "127.0.0.1");
        AddSetting("comm_remoteport", "15112");
        AddSetting("comm_framesync", "0");
        AddBlank();

        AddComment("#");
        AddComment("# CORE MISC OPTIONS");
        AddComment("#");
        AddSetting("drc", "1");
        AddSetting("drc_use_c", "0");
        AddSetting("bios", "");
        AddSetting("cheat", "0");
        AddSetting("skip_gameinfo", "0");
        AddSetting("skip_warnings", "0");
        AddSetting("uifont", "default");
        AddSetting("ui", "cabinet");
        AddSetting("ramsize", "");
        AddSetting("confirm_quit", "0");
        AddSetting("ui_mouse", "1");
        AddSetting("language", "");
        AddSetting("nvram_save", "1");
        AddBlank();

        AddComment("#");
        AddComment("# SCRIPTING OPTIONS");
        AddComment("#");
        AddSetting("autoboot_command", "");
        AddSetting("autoboot_delay", "0");
        AddSetting("autoboot_script", "");
        AddSetting("console", "0");
        AddSetting("plugins", "1");
        AddSetting("plugin", "");
        AddSetting("noplugin", "");
        AddBlank();

        AddComment("#");
        AddComment("# HTTP SERVER OPTIONS");
        AddComment("#");
        AddSetting("http", "0");
        AddSetting("http_port", "8080");
        AddSetting("http_root", "web");
        AddBlank();

        AddComment("#");
        AddComment("# OSD VIDEO OPTIONS");
        AddComment("#");
        AddSetting("video", "auto");
        AddSetting("numscreens", "1");
        AddSetting("window", "0");
        AddSetting("maximize", "1");
        AddSetting("waitvsync", "0");
        AddSetting("syncrefresh", "0");
        AddSetting("monitorprovider", "auto");
        AddBlank();

        AddComment("#");
        AddComment("# OSD PER-WINDOW VIDEO OPTIONS");
        AddComment("#");
        AddSetting("screen", "auto");
        AddSetting("aspect", "auto");
        AddSetting("resolution", "auto");
        AddSetting("view", "auto");
        AddSetting("screen0", "auto");
        AddSetting("aspect0", "auto");
        AddSetting("resolution0", "auto");
        AddSetting("view0", "auto");
        AddSetting("screen1", "auto");
        AddSetting("aspect1", "auto");
        AddSetting("resolution1", "auto");
        AddSetting("view1", "auto");
        AddSetting("screen2", "auto");
        AddSetting("aspect2", "auto");
        AddSetting("resolution2", "auto");
        AddSetting("view2", "auto");
        AddSetting("screen3", "auto");
        AddSetting("aspect3", "auto");
        AddSetting("resolution3", "auto");
        AddSetting("view3", "auto");
        AddBlank();

        AddComment("#");
        AddComment("# OSD FULL SCREEN OPTIONS");
        AddComment("#");
        AddSetting("switchres", "0");
        AddBlank();

        AddComment("#");
        AddComment("# OSD ACCELERATED VIDEO OPTIONS");
        AddComment("#");
        AddSetting("filter", "1");
        AddSetting("prescale", "1");
        AddBlank();

        AddComment("#");
        AddComment("# OSD SOUND OPTIONS");
        AddComment("#");
        AddSetting("sound", "auto");
        AddSetting("audio_latency", "1");
        AddBlank();

        AddComment("#");
        AddComment("# OSD INPUT OPTIONS");
        AddComment("#");
        AddSetting("keyboardprovider", "auto");
        AddSetting("mouseprovider", "auto");
        AddSetting("lightgunprovider", "auto");
        AddSetting("joystickprovider", "auto");
        AddBlank();

        AddComment("#");
        AddComment("# BGFX POST-PROCESSING OPTIONS");
        AddComment("#");
        AddSetting("bgfx_path", "bgfx");
        AddSetting("bgfx_backend", "auto");
        AddSetting("bgfx_debug", "0");
        AddSetting("bgfx_screen_chains", "default");
        AddSetting("bgfx_shadow_mask", "slot-mask.png");
        AddSetting("bgfx_lut", "");
        AddSetting("bgfx_avi_name", "auto");
        AddBlank();

        return parser;
    }

    /// <summary>
    /// Returns the default inipath value appropriate for the current platform.
    /// MAME expands $HOME on Unix but not on Windows, so we use the native
    /// convention for each OS.
    /// </summary>
    private static string GetDefaultIniSearchPath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return ".;ini;ini/presets";

        // On macOS and Linux, MAME natively uses $HOME/.mame as a config search path.
        // MAME itself expands $HOME, so this is the correct default.
        return "$HOME/.mame;.;ini";
    }

    // ── Platform-specific INI search ──────────────────────────────────

    private static string? FindMameIniWindows()
    {
        // Common Windows MAME locations
        string[] candidates =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MAME", "mame.ini"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "MAME", "mame.ini"),
            @"C:\MAME\mame.ini",
            @"C:\mame\mame.ini",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "scoop", "apps", "mame", "current", "mame.ini"),
        ];

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static string? FindMameIniLinux()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] candidates =
        [
            // XDG config directory
            Path.Combine(Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
                ?? Path.Combine(home, ".config"), "mame", "mame.ini"),
            // Traditional ~/.mame directory
            Path.Combine(home, ".mame", "mame.ini"),
            // System-wide
            "/etc/mame/mame.ini",
            // Package manager installed locations
            "/usr/share/games/mame/mame.ini",
            "/usr/local/share/mame/mame.ini",
            // Snap
            Path.Combine(home, "snap", "mame", "current", ".mame", "mame.ini"),
            // Flatpak
            Path.Combine(home, ".var", "app", "org.mamedev.MAME", "config", "mame", "mame.ini"),
        ];

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static string? FindMameIniMacOS()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] candidates =
        [
            // Standard macOS MAME config location
            Path.Combine(home, ".mame", "mame.ini"),
            // Homebrew
            "/opt/homebrew/etc/mame/mame.ini",
            "/usr/local/etc/mame/mame.ini",
            // Application Support
            Path.Combine(home, "Library", "Application Support", "mame", "mame.ini"),
        ];

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
