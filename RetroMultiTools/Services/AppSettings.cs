using System.Text.Json;

namespace RetroMultiTools.Services;

/// <summary>
/// Persists application settings to a JSON file in the user's AppData folder.
/// Thread-safe singleton that stores RetroArch path and other user preferences.
/// </summary>
public sealed class AppSettings
{
    private static readonly Lazy<AppSettings> _instance = new(() => new AppSettings());
    public static AppSettings Instance => _instance.Value;

    private readonly string _settingsPath;
    private readonly object _lock = new();
    private SettingsData _data;

    private AppSettings()
    {
        string appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RetroMultiTools");
        Directory.CreateDirectory(appData);
        _settingsPath = Path.Combine(appData, "settings.json");
        _data = Load();
    }

    public string RetroArchPath
    {
        get { lock (_lock) { return _data.RetroArchPath ?? string.Empty; } }
        set
        {
            lock (_lock)
            {
                _data.RetroArchPath = value;
                Save();
            }
        }
    }

    public bool DiscordRichPresenceEnabled
    {
        get { lock (_lock) { return _data.DiscordRichPresenceEnabled; } }
        set
        {
            lock (_lock)
            {
                _data.DiscordRichPresenceEnabled = value;
                Save();
            }
        }
    }

    public bool CheckForUpdatesOnStartup
    {
        get { lock (_lock) { return _data.CheckForUpdatesOnStartup; } }
        set
        {
            lock (_lock)
            {
                _data.CheckForUpdatesOnStartup = value;
                Save();
            }
        }
    }

    public bool MinimizeToTrayOnLaunch
    {
        get { lock (_lock) { return _data.MinimizeToTrayOnLaunch; } }
        set
        {
            lock (_lock)
            {
                _data.MinimizeToTrayOnLaunch = value;
                Save();
            }
        }
    }

    public bool StartInBigPictureMode
    {
        get { lock (_lock) { return _data.StartInBigPictureMode; } }
        set
        {
            lock (_lock)
            {
                _data.StartInBigPictureMode = value;
                Save();
            }
        }
    }

    public string BigPictureRomFolder
    {
        get { lock (_lock) { return _data.BigPictureRomFolder ?? string.Empty; } }
        set
        {
            lock (_lock)
            {
                _data.BigPictureRomFolder = value;
                Save();
            }
        }
    }

    /// <summary>
    /// Set of ROM file paths that the user has marked as favorites.
    /// </summary>
    public HashSet<string> Favorites
    {
        get
        {
            lock (_lock)
            {
                return new HashSet<string>(_data.Favorites ?? [], StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    public void AddFavorite(string filePath)
    {
        lock (_lock)
        {
            _data.Favorites ??= [];
            if (_data.Favorites.Add(filePath))
                Save();
        }
    }

    public void RemoveFavorite(string filePath)
    {
        lock (_lock)
        {
            if (_data.Favorites != null && _data.Favorites.Remove(filePath))
                Save();
        }
    }

    public bool IsFavorite(string filePath)
    {
        lock (_lock)
        {
            return _data.Favorites?.Contains(filePath) ?? false;
        }
    }

    /// <summary>
    /// Card scale factor for Big Picture Mode grid (0.5–2.0, default 1.0).
    /// </summary>
    public double BigPictureCardScale
    {
        get { lock (_lock) { return _data.BigPictureCardScale; } }
        set
        {
            lock (_lock)
            {
                _data.BigPictureCardScale = Math.Clamp(value, 0.5, 2.0);
                Save();
            }
        }
    }

    /// <summary>
    /// Whether native game controller (gamepad) input is enabled in Big Picture Mode.
    /// Requires SDL2 to be installed on the system.
    /// </summary>
    public bool GamepadEnabled
    {
        get { lock (_lock) { return _data.GamepadEnabled; } }
        set
        {
            lock (_lock)
            {
                _data.GamepadEnabled = value;
                Save();
            }
        }
    }

    /// <summary>
    /// Analog stick dead-zone for game controllers (0.05–0.95, default 0.25).
    /// Values below 0.05 are clamped to avoid drift.
    /// </summary>
    public double GamepadDeadZone
    {
        get { lock (_lock) { return _data.GamepadDeadZone; } }
        set
        {
            lock (_lock)
            {
                _data.GamepadDeadZone = Math.Clamp(value, 0.05, 0.95);
                Save();
            }
        }
    }

    /// <summary>
    /// Ordered list of recently launched ROM file paths (most recent first).
    /// </summary>
    public List<string> RecentlyPlayed
    {
        get
        {
            lock (_lock)
            {
                return new List<string>(_data.RecentlyPlayed ?? []);
            }
        }
    }

    /// <summary>
    /// Records a ROM launch. Moves the path to the front of the recently played list.
    /// Keeps at most 50 entries.
    /// </summary>
    public void RecordRecentlyPlayed(string filePath)
    {
        lock (_lock)
        {
            _data.RecentlyPlayed ??= [];
            _data.RecentlyPlayed.Remove(filePath);
            _data.RecentlyPlayed.Insert(0, filePath);

            const int maxRecent = 50;
            if (_data.RecentlyPlayed.Count > maxRecent)
                _data.RecentlyPlayed.RemoveRange(maxRecent, _data.RecentlyPlayed.Count - maxRecent);

            Save();
        }
    }

    /// <summary>
    /// Returns how many times a ROM has been launched.
    /// </summary>
    public int GetPlayCount(string filePath)
    {
        lock (_lock)
        {
            if (_data.PlayCounts != null &&
                _data.PlayCounts.TryGetValue(filePath, out int count))
                return count;
            return 0;
        }
    }

    /// <summary>
    /// Increments the play count for a ROM by one.
    /// </summary>
    public void IncrementPlayCount(string filePath)
    {
        lock (_lock)
        {
            _data.PlayCounts ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _data.PlayCounts.TryGetValue(filePath, out int current);
            if (current < int.MaxValue)
                _data.PlayCounts[filePath] = current + 1;
            Save();
        }
    }

    private SettingsData Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                string json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<SettingsData>(json) ?? new SettingsData();
            }
        }
        catch (IOException ex)
        {
            System.Diagnostics.Trace.WriteLine($"[AppSettings] Failed to read settings: {ex.Message}");
        }
        catch (JsonException ex)
        {
            System.Diagnostics.Trace.WriteLine($"[AppSettings] Failed to parse settings: {ex.Message}");
        }
        return new SettingsData();
    }

    private void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
            string tempPath = _settingsPath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _settingsPath, overwrite: true);
        }
        catch (IOException ex)
        {
            System.Diagnostics.Trace.WriteLine($"[AppSettings] Failed to save settings: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            System.Diagnostics.Trace.WriteLine($"[AppSettings] Permission denied saving settings: {ex.Message}");
        }
    }

    private sealed class SettingsData
    {
        public string? RetroArchPath { get; set; }
        public bool DiscordRichPresenceEnabled { get; set; }
        public bool CheckForUpdatesOnStartup { get; set; } = true;
        public bool MinimizeToTrayOnLaunch { get; set; } = true;
        public bool StartInBigPictureMode { get; set; }
        public string? BigPictureRomFolder { get; set; }
        public double BigPictureCardScale { get; set; } = 1.0;
        public bool GamepadEnabled { get; set; } = true;
        public double GamepadDeadZone { get; set; } = 0.25;
        public HashSet<string>? Favorites { get; set; }
        public List<string>? RecentlyPlayed { get; set; }
        public Dictionary<string, int>? PlayCounts { get; set; }
    }
}
