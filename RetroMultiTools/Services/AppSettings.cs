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
        catch (IOException) { }
        catch (JsonException) { }
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
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private sealed class SettingsData
    {
        public string? RetroArchPath { get; set; }
    }
}
