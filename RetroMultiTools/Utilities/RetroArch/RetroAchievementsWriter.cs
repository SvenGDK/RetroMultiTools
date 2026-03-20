using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RetroMultiTools.Utilities.RetroArch;

/// <summary>
/// Creates and edits achievement definition files compatible with RetroAchievements.org.
/// Produces the standard RACache local achievement JSON format that can be imported
/// into the RetroAchievements web toolkit or loaded by supported emulators.
/// </summary>
public static class RetroAchievementsWriter
{
    // ── Models ───────────────────────────────────────────────────────────

    /// <summary>
    /// Top-level achievement set for a single game.
    /// </summary>
    public sealed class AchievementSet
    {
        [JsonPropertyName("Title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("GameID")]
        public int GameId { get; set; }

        [JsonPropertyName("ConsoleID")]
        public int ConsoleId { get; set; }

        [JsonPropertyName("Achievements")]
        public List<Achievement> Achievements { get; set; } = [];
    }

    /// <summary>
    /// A single achievement definition.
    /// </summary>
    public sealed class Achievement
    {
        [JsonPropertyName("ID")]
        public int Id { get; set; }

        [JsonPropertyName("Title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("Description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("Points")]
        public int Points { get; set; }

        [JsonPropertyName("Author")]
        public string Author { get; set; } = string.Empty;

        [JsonPropertyName("BadgeName")]
        public string BadgeName { get; set; } = string.Empty;

        [JsonPropertyName("MemAddr")]
        public string MemAddr { get; set; } = string.Empty;

        [JsonPropertyName("Type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("Flags")]
        public int Flags { get; set; } = 5; // 5 = Unofficial (local)

        [JsonPropertyName("Created")]
        public string Created { get; set; } = string.Empty;

        [JsonPropertyName("Modified")]
        public string Modified { get; set; } = string.Empty;
    }

    /// <summary>
    /// Standard RetroAchievements console identifiers.
    /// </summary>
    public static readonly (int Id, string Name)[] Consoles =
    [
        (1, "Mega Drive / Genesis"),
        (2, "Nintendo 64"),
        (3, "SNES / Super Famicom"),
        (4, "Game Boy"),
        (5, "Game Boy Advance"),
        (6, "Game Boy Color"),
        (7, "NES / Famicom"),
        (8, "PC Engine / TurboGrafx-16"),
        (9, "Sega CD"),
        (10, "Sega 32X"),
        (11, "Master System"),
        (12, "PlayStation"),
        (13, "Atari Lynx"),
        (14, "Neo Geo Pocket"),
        (15, "Game Gear"),
        (16, "GameCube"),
        (17, "Atari Jaguar"),
        (18, "Nintendo DS"),
        (21, "PlayStation 2"),
        (24, "WonderSwan"),
        (25, "Atari 2600"),
        (27, "Arcade"),
        (28, "Virtual Boy"),
        (29, "MSX"),
        (33, "SG-1000"),
        (37, "Amstrad CPC"),
        (38, "Apple II"),
        (39, "Sega Saturn"),
        (40, "Sega Dreamcast"),
        (41, "PlayStation Portable"),
        (43, "3DO"),
        (44, "ColecoVision"),
        (45, "Intellivision"),
        (47, "PC-8000/8800"),
        (51, "Atari 7800"),
        (53, "Fairchild Channel F"),
        (56, "Neo Geo CD"),
        (57, "PC-FX"),
        (63, "Watara Supervision"),
        (69, "Mega Duck"),
        (71, "Arduboy"),
        (72, "WASM-4"),
        (73, "Arcadia 2001"),
        (76, "PC Engine CD"),
        (77, "Atari 5200"),
        (78, "Atari 800"),
    ];

    /// <summary>
    /// Standard achievement types used by RetroAchievements.org.
    /// </summary>
    public static readonly string[] AchievementTypes =
    [
        "",              // Standard (no type tag)
        "missable",      // Can be permanently missed
        "progression",   // Story / progression milestone
        "win_condition", // Game completion
    ];

    // ── Persistence ─────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Creates a new empty achievement set for the specified game.
    /// </summary>
    public static AchievementSet CreateNew(string gameTitle, int gameId, int consoleId)
    {
        return new AchievementSet
        {
            Title = gameTitle,
            GameId = gameId,
            ConsoleId = consoleId,
        };
    }

    /// <summary>
    /// Adds an achievement to the set, auto-assigning the next local ID.
    /// </summary>
    public static Achievement AddAchievement(
        AchievementSet set,
        string title,
        string description,
        int points,
        string memAddr,
        string type = "",
        string author = "",
        string badgeName = "")
    {
        int nextId = set.Achievements.Count > 0
            ? set.Achievements.Max(a => a.Id) + 1
            : 111000001; // RetroAchievements local ID range

        var achievement = new Achievement
        {
            Id = nextId,
            Title = title,
            Description = description,
            Points = points,
            MemAddr = memAddr,
            Type = type,
            Author = author,
            BadgeName = badgeName,
            Created = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            Modified = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
        };

        set.Achievements.Add(achievement);
        return achievement;
    }

    /// <summary>
    /// Removes an achievement from the set by its ID.
    /// </summary>
    public static bool RemoveAchievement(AchievementSet set, int achievementId)
    {
        return set.Achievements.RemoveAll(a => a.Id == achievementId) > 0;
    }

    /// <summary>
    /// Saves the achievement set to a JSON file compatible with RACache format.
    /// </summary>
    public static async Task SaveAsync(AchievementSet set, string filePath, CancellationToken ct = default)
    {
        string json = JsonSerializer.Serialize(set, SerializerOptions);
        await File.WriteAllTextAsync(filePath, json, Encoding.UTF8, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Loads an achievement set from a JSON file.
    /// Returns null if the file does not exist or contains invalid JSON.
    /// </summary>
    public static async Task<AchievementSet?> LoadAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            return null;

        try
        {
            string json = await File.ReadAllTextAsync(filePath, Encoding.UTF8, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<AchievementSet>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Validates the achievement set and returns a list of validation messages.
    /// An empty list means the set is valid.
    /// </summary>
    public static List<string> Validate(AchievementSet set)
    {
        var issues = new List<string>();

        if (string.IsNullOrWhiteSpace(set.Title))
            issues.Add("Game title is required.");
        if (set.GameId <= 0)
            issues.Add("Game ID must be a positive integer.");
        if (set.ConsoleId <= 0)
            issues.Add("Console ID must be a positive integer.");

        var seenIds = new HashSet<int>();
        foreach (var ach in set.Achievements)
        {
            if (!seenIds.Add(ach.Id))
                issues.Add($"Duplicate achievement ID: {ach.Id}");
            if (string.IsNullOrWhiteSpace(ach.Title))
                issues.Add($"Achievement {ach.Id}: Title is required.");
            if (ach.Points < 0 || ach.Points > 100)
                issues.Add($"Achievement '{ach.Title}': Points must be 0–100.");
            if (string.IsNullOrWhiteSpace(ach.MemAddr))
                issues.Add($"Achievement '{ach.Title}': Memory condition (MemAddr) is required.");
        }

        return issues;
    }

    /// <summary>
    /// Exports the achievement set in the RetroAchievements plain-text "local" format
    /// used by RAIntegration / RALibretro for local achievement development.
    /// </summary>
    public static string ExportAsLocalText(AchievementSet set)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"// Game: {set.Title}  (ID: {set.GameId})");
        sb.AppendLine($"// Console: {set.ConsoleId}");
        sb.AppendLine();

        foreach (var ach in set.Achievements)
        {
            // Format: ID:"MemAddr":Title:Description:::::Author:Points:::::Created:Modified:::::Type
            sb.AppendLine($"{ach.Id}:\"{ach.MemAddr}\":{ach.Title}:{ach.Description}:::::{ach.Author}:{ach.Points}:::::{ach.Created}:{ach.Modified}:::::{ach.Type}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Returns a human-readable console name for the given console ID.
    /// </summary>
    public static string GetConsoleName(int consoleId)
    {
        foreach (var (id, name) in Consoles)
        {
            if (id == consoleId)
                return name;
        }
        return $"Unknown ({consoleId})";
    }
}
