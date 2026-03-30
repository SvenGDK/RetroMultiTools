using System.Runtime.InteropServices;
using System.Text;
using RetroMultiTools.Services;

namespace RetroMultiTools.Utilities.Mednafen;

/// <summary>
/// Parses and writes Mednafen CFG configuration files.
/// Supports the Mednafen CFG format: key-value pairs separated by a single space,
/// with ';' comments and blank lines preserved during round-trip editing.
/// Compatible with all Mednafen versions on Windows, macOS, and Linux (x64 + arm64).
/// </summary>
public sealed class MednafenCfgParser
{
    /// <summary>
    /// Represents a single line in a Mednafen CFG file, preserving comments and blank lines.
    /// </summary>
    internal sealed class CfgLine
    {
        /// <summary>Original raw text of the line. Used for output only when the line is a comment or blank.</summary>
        public string RawText { get; set; } = string.Empty;

        /// <summary>Non-null when the line is a key-value setting.</summary>
        public string? Key { get; set; }

        /// <summary>The value part of a key-value line.</summary>
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
    /// Parses a Mednafen CFG file from the given path.
    /// Preserves comments and blank lines for round-trip fidelity.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="filePath"/> is null or empty.</exception>
    /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access to the file is denied.</exception>
    public static MednafenCfgParser Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        var parser = new MednafenCfgParser { FilePath = filePath };
        string[] lines = File.ReadAllLines(filePath);

        foreach (string rawLine in lines)
        {
            string trimmed = rawLine.TrimStart();

            // Comment or blank line
            if (trimmed.Length == 0 || trimmed[0] == ';')
            {
                parser._lines.Add(new CfgLine { RawText = rawLine });
                continue;
            }

            // Mednafen CFG format: key followed by a single space then value.
            // Values may contain spaces. The key is always a single token (no spaces).
            int spaceIdx = trimmed.IndexOf(' ');
            if (spaceIdx < 0)
            {
                // Key-only line (no value) — treat value as empty string
                var kvLine = new CfgLine { Key = trimmed, Value = string.Empty };
                parser._lines.Add(kvLine);
                parser._settings[trimmed] = kvLine;
            }
            else
            {
                string key = trimmed[..spaceIdx];
                string val = trimmed[(spaceIdx + 1)..];

                var kvLine = new CfgLine { Key = key, Value = val };
                parser._lines.Add(kvLine);
                // Last occurrence wins (matches Mednafen behaviour)
                parser._settings[key] = kvLine;
            }
        }

        return parser;
    }

    /// <summary>
    /// Creates an empty parser that can be populated and saved.
    /// </summary>
    public static MednafenCfgParser CreateEmpty(string? filePath = null)
    {
        return new MednafenCfgParser { FilePath = filePath };
    }

    /// <summary>
    /// Saves the configuration to the specified path (or the original path).
    /// Uses the standard Mednafen CFG format with a single space between key and value.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no file path is available.</exception>
    /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access to the file is denied.</exception>
    /// <exception cref="NotSupportedException">Thrown when the path format is not supported.</exception>
    public void Save(string? outputPath = null)
    {
        string path = outputPath ?? FilePath
            ?? throw new InvalidOperationException("No file path specified for saving.");

        // Estimate ~40 chars per line based on typical "key value\n" config format
        var sb = new StringBuilder(_lines.Count * 40);

        foreach (var line in _lines)
        {
            if (line.IsCommentOrBlank)
            {
                sb.AppendLine(line.RawText);
            }
            else
            {
                // Mednafen uses "key value" with a single space separator (no padding)
                sb.AppendLine($"{line.Key} {line.Value}");
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
    /// Mednafen uses 0 and 1 for boolean settings.
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
    /// Sets an integer value using invariant culture formatting.
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
                string.Equals(l.Key, key, StringComparison.Ordinal));
            return true;
        }
        return false;
    }

    // ── Static helpers for locating Mednafen configuration files ──────

    /// <summary>
    /// Detects the Mednafen CFG file path.
    /// Checks the following locations in order:
    /// 1. Path from <see cref="MednafenLauncher.GetMednafenConfigFilePath"/>
    /// 2. Next to the Mednafen executable (Windows)
    /// 3. Platform-specific config directories
    /// Returns null if no configuration file is found.
    /// </summary>
    public static string? FindMednafenCfg()
    {
        // Try MednafenLauncher detection first
        string? launcherPath = MednafenLauncher.GetMednafenConfigFilePath();
        if (!string.IsNullOrEmpty(launcherPath) && File.Exists(launcherPath))
            return launcherPath;

        // Platform-specific config directories
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return FindMednafenCfgWindows();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return FindMednafenCfgLinux();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return FindMednafenCfgMacOS();

        return null;
    }

    /// <summary>
    /// Returns the default path where a new mednafen.cfg should be created.
    /// Uses <see cref="MednafenLauncher.GetMednafenConfigDirectory"/> as the primary source.
    /// Falls back to ~/.mednafen/mednafen.cfg on all platforms.
    /// </summary>
    public static string GetDefaultCfgPath()
    {
        string? configDir = MednafenLauncher.GetMednafenConfigDirectory();
        if (!string.IsNullOrEmpty(configDir))
            return Path.Combine(configDir, "mednafen.cfg");

        // Fallback: ~/.mednafen/mednafen.cfg
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".mednafen", "mednafen.cfg");
    }

    /// <summary>
    /// Populates a new CFG parser with all standard Mednafen global and per-system defaults.
    /// This produces a configuration equivalent to Mednafen's auto-generated mednafen.cfg.
    /// </summary>
    public static MednafenCfgParser CreateWithDefaults(string? filePath = null)
    {
        var parser = new MednafenCfgParser { FilePath = filePath };

        void AddComment(string text) =>
            parser._lines.Add(new CfgLine { RawText = text });

        void AddSetting(string key, string value)
        {
            var line = new CfgLine { Key = key, Value = value };
            parser._lines.Add(line);
            parser._settings[key] = line;
        }

        void AddBlank() => AddComment("");

        // ── General / Core Settings ──────────────────────────────────

        AddComment("; -- General Settings --");
        AddSetting("autosave", "0");
        AddSetting("cheats", "1");
        AddSetting("cd.image_memcache", "0");
        AddSetting("cd.m3u.disc_limit", "25");
        AddSetting("cd.m3u.recursion_limit", "9");
        AddSetting("debugger.autostepmode", "0");
        AddSetting("ffnosound", "0");
        AddSetting("ffspeed", "4");
        AddSetting("fftoggle", "0");
        AddSetting("sfspeed", "0.75");
        AddSetting("sftoggle", "0");
        AddSetting("nothrottle", "0");
        AddSetting("srwframes", "600");
        AddBlank();

        // ── Video Settings ───────────────────────────────────────────

        AddComment("; -- Video Settings --");
        AddSetting("video.driver", "default");
        AddSetting("video.fs", "0");
        AddSetting("video.fs.display", "-1");
        AddSetting("video.glvsync", "1");
        AddSetting("video.blit_timesync", "1");
        AddSetting("video.frameskip", "1");
        AddSetting("video.disable_composition", "1");
        AddSetting("video.force_bbclear", "0");
        AddSetting("video.cursorvis", "hidden");
        AddSetting("video.deinterlacer", "weave");
        AddSetting("video.glformat", "auto");
        AddBlank();

        // ── Sound Settings ───────────────────────────────────────────

        AddComment("; -- Sound Settings --");
        AddSetting("sound", "1");
        AddSetting("sound.driver", "default");
        AddSetting("sound.device", "default");
        AddSetting("sound.rate", "48000");
        AddSetting("sound.volume", "100");
        AddSetting("sound.buffer_time", "0");
        AddSetting("sound.period_time", "0");
        AddBlank();

        // ── Input Settings ───────────────────────────────────────────

        AddComment("; -- Input Settings --");
        AddSetting("input.autofirefreq", "3");
        AddSetting("input.ckdelay", "0");
        AddSetting("input.grab.strategy", "full");
        AddSetting("input.joystick.axis_threshold", "75");
        AddSetting("input.joystick.global_focus", "1");
        AddBlank();

        // ── File System / Paths ──────────────────────────────────────

        AddComment("; -- File System / Paths --");
        AddSetting("filesys.path_cheat", "cheats");
        AddSetting("filesys.path_firmware", "firmware");
        AddSetting("filesys.path_movie", "mcm");
        AddSetting("filesys.path_palette", "palettes");
        AddSetting("filesys.path_pgconfig", "pgconfig");
        AddSetting("filesys.path_sav", "sav");
        AddSetting("filesys.path_savbackup", "b");
        AddSetting("filesys.path_snap", "snaps");
        AddSetting("filesys.path_state", "mcs");
        AddSetting("filesys.fname_movie", "%f.%M%p.%x");
        AddSetting("filesys.fname_sav", "%f.%M%x");
        AddSetting("filesys.fname_savbackup", "%f.%m%z%p.%x");
        AddSetting("filesys.fname_snap", "%f-%p.%x");
        AddSetting("filesys.fname_state", "%f.%M%X");
        AddSetting("filesys.state_comp_level", "6");
        AddSetting("filesys.untrusted_fip_check", "1");
        AddBlank();

        // ── OSD / Display ────────────────────────────────────────────

        AddComment("; -- OSD / Display --");
        AddSetting("osd.alpha_blend", "1");
        AddSetting("osd.message_display_time", "2500");
        AddSetting("osd.state_display_time", "2000");
        AddBlank();

        // ── FPS Display ──────────────────────────────────────────────

        AddComment("; -- FPS Display --");
        AddSetting("fps.autoenable", "0");
        AddSetting("fps.bgcolor", "0x80000000");
        AddSetting("fps.font", "5x7");
        AddSetting("fps.position", "upper_left");
        AddSetting("fps.scale", "1");
        AddSetting("fps.textcolor", "0xFFFFFFFF");
        AddBlank();

        // ── Netplay ──────────────────────────────────────────────────

        AddComment("; -- Netplay --");
        AddSetting("netplay.host", "netplay.fobby.net");
        AddSetting("netplay.port", "4046");
        AddSetting("netplay.localplayers", "1");
        AddSetting("netplay.nick", "");
        AddSetting("netplay.password", "");
        AddSetting("netplay.gamekey", "");
        AddSetting("netplay.console.font", "9x18");
        AddSetting("netplay.console.lines", "5");
        AddSetting("netplay.console.scale", "1");
        AddBlank();

        // ── QuickTime Recording ──────────────────────────────────────

        AddComment("; -- QuickTime Recording --");
        AddSetting("qtrecord.vcodec", "cscd");
        AddSetting("qtrecord.w_double_threshold", "384");
        AddSetting("qtrecord.h_double_threshold", "256");
        AddBlank();

        // ── CPU Affinity ─────────────────────────────────────────────

        AddComment("; -- CPU Affinity --");
        AddSetting("affinity.cd", "0");
        AddSetting("affinity.emu", "0");
        AddSetting("affinity.video", "0");
        AddBlank();

        // ── Per-System Settings ──────────────────────────────────────

        string[] systems =
        [
            "apple2", "lynx", "cdplay", "demo", "gb", "gba", "gg", "md", "ngp",
            "nes", "pce", "pce_fast", "pcfx", "psx", "sasplay", "sms", "snes",
            "snes_faust", "ss", "ssfplay", "vb", "wswan"
        ];

        foreach (string sys in systems)
        {
            AddComment($"; -- {sys} --");
            AddSetting($"{sys}.enable", "1");
            AddSetting($"{sys}.forcemono", "0");
            AddSetting($"{sys}.scanlines", "0");
            AddSetting($"{sys}.shader", "none");
            AddSetting($"{sys}.shader.goat.fprog", "0");
            AddSetting($"{sys}.shader.goat.hdiv", "0.50");
            AddSetting($"{sys}.shader.goat.pat", "goatron");
            AddSetting($"{sys}.shader.goat.slen", "1");
            AddSetting($"{sys}.shader.goat.tp", "0.50");
            AddSetting($"{sys}.shader.goat.vdiv", "0.50");
            AddSetting($"{sys}.special", "none");
            AddSetting($"{sys}.stretch", "aspect_mult2");
            AddSetting($"{sys}.tblur", "0");
            AddSetting($"{sys}.tblur.accum", "0");
            AddSetting($"{sys}.tblur.accum.amount", "50");
            AddSetting($"{sys}.videoip", "0");
            AddSetting($"{sys}.xres", "0");
            AddSetting($"{sys}.xscale", "4.000000");
            AddSetting($"{sys}.xscalefs", "1.000000");
            AddSetting($"{sys}.yres", "0");
            AddSetting($"{sys}.yscale", "4.000000");
            AddSetting($"{sys}.yscalefs", "1.000000");
            AddBlank();
        }

        return parser;
    }

    // ── Platform-specific CFG search ─────────────────────────────────

    private static string? FindMednafenCfgWindows()
    {
        // On Windows, Mednafen stores config next to the executable
        string configured = AppSettings.Instance.MednafenPath;
        if (!string.IsNullOrEmpty(configured))
        {
            string resolved = MednafenLauncher.ResolveMednafenPath(configured);
            if (File.Exists(resolved))
            {
                string? exeDir = Path.GetDirectoryName(resolved);
                if (!string.IsNullOrEmpty(exeDir))
                {
                    string cfgPath = Path.Combine(exeDir, "mednafen.cfg");
                    if (File.Exists(cfgPath))
                        return cfgPath;
                }
            }
        }

        // Check common Windows install locations and user profile
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] candidates =
        [
            Path.Combine(home, ".mednafen", "mednafen.cfg"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Mednafen", "mednafen.cfg"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Mednafen", "mednafen.cfg"),
            @"C:\Mednafen\mednafen.cfg",
            @"C:\mednafen\mednafen.cfg",
            // Scoop installation
            Path.Combine(home, "scoop", "apps", "mednafen", "current", "mednafen.cfg"),
        ];

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static string? FindMednafenCfgLinux()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] candidates =
        [
            // Traditional ~/.mednafen directory
            Path.Combine(home, ".mednafen", "mednafen.cfg"),
            // XDG config directory
            Path.Combine(Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
                ?? Path.Combine(home, ".config"), "mednafen", "mednafen.cfg"),
        ];

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static string? FindMednafenCfgMacOS()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // Check next to the configured executable first (some .app bundle installs)
        string configured = AppSettings.Instance.MednafenPath;
        if (!string.IsNullOrEmpty(configured))
        {
            string resolved = MednafenLauncher.ResolveMednafenPath(configured);
            if (File.Exists(resolved))
            {
                string? exeDir = Path.GetDirectoryName(resolved);
                if (!string.IsNullOrEmpty(exeDir))
                {
                    string cfgPath = Path.Combine(exeDir, "mednafen.cfg");
                    if (File.Exists(cfgPath))
                        return cfgPath;
                }
            }
        }

        string[] candidates =
        [
            // Standard macOS Mednafen config location
            Path.Combine(home, ".mednafen", "mednafen.cfg"),
            // Library Application Support
            Path.Combine(home, "Library", "Application Support", "mednafen", "mednafen.cfg"),
        ];

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
