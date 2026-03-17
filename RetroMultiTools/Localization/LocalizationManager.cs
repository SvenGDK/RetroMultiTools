using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Text.Json;

namespace RetroMultiTools.Localization;

public sealed class LocalizationManager : INotifyPropertyChanged
{
    private static readonly Lazy<LocalizationManager> _instance = new(() => new LocalizationManager());
    public static LocalizationManager Instance => _instance.Value;

    private readonly ResourceManager _resourceManager;
    private CultureInfo _culture;

    public event PropertyChangedEventHandler? PropertyChanged;

    public static readonly (string DisplayName, string CultureName)[] SupportedLanguages =
    [
        ("English", "en"),
        ("Español", "es"),
        ("Français", "fr"),
        ("Deutsch", "de"),
        ("Português", "pt"),
        ("Italiano", "it"),
        ("日本語", "ja"),
        ("中文", "zh-Hans"),
        ("한국어", "ko"),
        ("Русский", "ru"),
        ("Nederlands", "nl"),
        ("Polski", "pl"),
        ("Türkçe", "tr"),
        ("العربية", "ar"),
        ("हिन्दी", "hi"),
        ("ไทย", "th"),
        ("Svenska", "sv"),
        ("Čeština", "cs"),
        ("Tiếng Việt", "vi"),
        ("Bahasa Indonesia", "id")
    ];

    private LocalizationManager()
    {
        _resourceManager = new ResourceManager(
            "RetroMultiTools.Resources.Strings",
            typeof(LocalizationManager).Assembly);

        string savedCulture = LoadLanguagePreference();
        try
        {
            _culture = new CultureInfo(savedCulture);
        }
        catch (CultureNotFoundException)
        {
            _culture = new CultureInfo("en");
        }
        CultureInfo.CurrentUICulture = _culture;
    }

    public CultureInfo Culture
    {
        get => _culture;
        set
        {
            if (_culture.Name == value.Name) return;
            _culture = value;
            CultureInfo.CurrentUICulture = value;
            SaveLanguagePreference(value.Name);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        }
    }

    public string this[string key] =>
        _resourceManager.GetString(key, _culture) ?? key;

    private static string GetSettingsFilePath()
    {
        string appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RetroMultiTools");
        Directory.CreateDirectory(appData);
        return Path.Combine(appData, "language.json");
    }

    private static string LoadLanguagePreference()
    {
        try
        {
            string path = GetSettingsFilePath();
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<LanguageSettings>(json);
                if (settings?.Language != null)
                    return settings.Language;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException) { }
        return "en";
    }

    private static void SaveLanguagePreference(string cultureName)
    {
        try
        {
            string path = GetSettingsFilePath();
            string json = JsonSerializer.Serialize(new LanguageSettings { Language = cultureName });
            File.WriteAllText(path, json);
        }
        catch (IOException) { }
    }

    private sealed class LanguageSettings
    {
        public string? Language { get; set; }
    }
}
