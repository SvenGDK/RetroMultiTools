using RetroMultiTools.Models;
using RetroMultiTools.Services;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RetroMultiTools.Utilities.RetroArch;

/// <summary>
/// Creates, edits and manages RetroArch playlist (.lpl) files.
/// Also supports downloading box-art / snap / title-screen thumbnails
/// from the libretro-thumbnails repository on GitHub.
/// </summary>
public static class RetroArchPlaylistCreator
{
    // ── Models ───────────────────────────────────────────────────────────

    /// <summary>
    /// RetroArch playlist (JSON .lpl format, v1.7.5+).
    /// </summary>
    public sealed class Playlist
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.5";

        [JsonPropertyName("default_core_path")]
        public string DefaultCorePath { get; set; } = string.Empty;

        [JsonPropertyName("default_core_name")]
        public string DefaultCoreName { get; set; } = string.Empty;

        [JsonPropertyName("label_display_mode")]
        public int LabelDisplayMode { get; set; }

        [JsonPropertyName("right_thumbnail_mode")]
        public int RightThumbnailMode { get; set; }

        [JsonPropertyName("left_thumbnail_mode")]
        public int LeftThumbnailMode { get; set; }

        [JsonPropertyName("sort_mode")]
        public int SortMode { get; set; }

        [JsonPropertyName("items")]
        public List<PlaylistItem> Items { get; set; } = [];
    }

    /// <summary>
    /// Single entry inside a RetroArch playlist.
    /// </summary>
    public sealed class PlaylistItem
    {
        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("core_path")]
        public string CorePath { get; set; } = "DETECT";

        [JsonPropertyName("core_name")]
        public string CoreName { get; set; } = "DETECT";

        [JsonPropertyName("crc32")]
        public string Crc32 { get; set; } = "DETECT";

        [JsonPropertyName("db_name")]
        public string DbName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Maps ROM system names to the RetroArch playlist database names
    /// used for thumbnail directories on the libretro-thumbnails server.
    /// </summary>
    private static readonly Dictionary<RomSystem, string> SystemDbMap = new()
    {
        { RomSystem.AmstradCPC,        "Amstrad - CPC" },
        { RomSystem.Arcade,            "MAME" },
        { RomSystem.Atari2600,         "Atari - 2600" },
        { RomSystem.Atari5200,         "Atari - 5200" },
        { RomSystem.Atari7800,         "Atari - 7800" },
        { RomSystem.AtariJaguar,       "Atari - Jaguar" },
        { RomSystem.AtariLynx,         "Atari - Lynx" },
        { RomSystem.ColecoVision,      "Coleco - ColecoVision" },
        { RomSystem.FairchildChannelF, "Fairchild - Channel F" },
        { RomSystem.GameBoy,           "Nintendo - Game Boy" },
        { RomSystem.GameBoyAdvance,    "Nintendo - Game Boy Advance" },
        { RomSystem.GameBoyColor,      "Nintendo - Game Boy Color" },
        { RomSystem.GameCube,          "Nintendo - GameCube" },
        { RomSystem.GameGear,          "Sega - Game Gear" },
        { RomSystem.Intellivision,     "Mattel - Intellivision" },
        { RomSystem.MegaDrive,         "Sega - Mega Drive - Genesis" },
        { RomSystem.MSX,               "Microsoft - MSX" },
        { RomSystem.MSX2,              "Microsoft - MSX2" },
        { RomSystem.N64,               "Nintendo - Nintendo 64" },
        { RomSystem.NeoGeo,            "SNK - Neo Geo" },
        { RomSystem.NeoGeoCD,          "SNK - Neo Geo CD" },
        { RomSystem.NeoGeoPocket,      "SNK - Neo Geo Pocket" },
        { RomSystem.NES,               "Nintendo - Nintendo Entertainment System" },
        { RomSystem.Nintendo3DS,       "Nintendo - Nintendo 3DS" },
        { RomSystem.NintendoDS,        "Nintendo - Nintendo DS" },
        { RomSystem.Panasonic3DO,      "The 3DO Company - 3DO" },
        { RomSystem.PCEngine,          "NEC - PC Engine - TurboGrafx 16" },
        { RomSystem.Sega32X,           "Sega - 32X" },
        { RomSystem.SegaCD,            "Sega - Mega-CD - Sega CD" },
        { RomSystem.SegaDreamcast,     "Sega - Dreamcast" },
        { RomSystem.SegaMasterSystem,  "Sega - Master System - Mark III" },
        { RomSystem.SegaSaturn,        "Sega - Saturn" },
        { RomSystem.SNES,              "Nintendo - Super Nintendo Entertainment System" },
        { RomSystem.VirtualBoy,        "Nintendo - Virtual Boy" },
        { RomSystem.Wii,               "Nintendo - Wii" },
    };

    // ── Playlist creation ───────────────────────────────────────────────

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Returns the RetroArch database name for the given ROM system, if mapped.
    /// </summary>
    public static string? GetDbName(RomSystem system)
    {
        return SystemDbMap.TryGetValue(system, out var name) ? name : null;
    }

    /// <summary>
    /// Returns all available system→DB name mappings for use in UI dropdowns.
    /// </summary>
    public static IReadOnlyList<(RomSystem System, string DbName)> GetAllDbMappings()
    {
        return SystemDbMap.Select(kvp => (kvp.Key, kvp.Value)).ToList();
    }

    /// <summary>
    /// Creates a new empty playlist with optional default core configuration.
    /// </summary>
    public static Playlist CreateNew(string defaultCorePath = "", string defaultCoreName = "")
    {
        return new Playlist
        {
            DefaultCorePath = defaultCorePath,
            DefaultCoreName = defaultCoreName,
        };
    }

    /// <summary>
    /// Scans a directory for ROM files and builds a playlist.
    /// </summary>
    public static async Task<Playlist> BuildFromDirectoryAsync(
        string romDirectory,
        RomSystem system,
        string corePath = "DETECT",
        string coreName = "DETECT",
        bool recursive = false,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        string dbName = GetDbName(system) ?? system.ToString();
        string dbFileName = $"{dbName}.lpl";

        var playlist = new Playlist
        {
            DefaultCorePath = corePath,
            DefaultCoreName = coreName,
        };

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var extensions = GetExtensionsForSystem(system);

        // Run the I/O-bound directory scan on a background thread
        var items = await Task.Run(() =>
        {
            var result = new List<PlaylistItem>();
            int count = 0;

            foreach (string file in Directory.EnumerateFiles(romDirectory, "*", searchOption))
            {
                ct.ThrowIfCancellationRequested();

                string ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
                if (!extensions.Contains(ext))
                    continue;

                string label = System.IO.Path.GetFileNameWithoutExtension(file);
                result.Add(new PlaylistItem
                {
                    Path = file,
                    Label = label,
                    CorePath = corePath,
                    CoreName = coreName,
                    DbName = dbFileName,
                });

                count++;
                progress?.Report($"Added: {label}");
            }

            progress?.Report($"Playlist complete: {count} entries");
            return result;
        }, ct).ConfigureAwait(false);

        playlist.Items.AddRange(items);
        return playlist;
    }

    /// <summary>
    /// Adds a single item to an existing playlist.
    /// </summary>
    public static void AddItem(Playlist playlist, string romPath, string label, RomSystem system,
        string corePath = "DETECT", string coreName = "DETECT")
    {
        string dbName = GetDbName(system) ?? system.ToString();
        playlist.Items.Add(new PlaylistItem
        {
            Path = romPath,
            Label = label,
            CorePath = corePath,
            CoreName = coreName,
            DbName = $"{dbName}.lpl",
        });
    }

    /// <summary>
    /// Removes a playlist item by index.
    /// </summary>
    public static bool RemoveItem(Playlist playlist, int index)
    {
        if (index < 0 || index >= playlist.Items.Count)
            return false;
        playlist.Items.RemoveAt(index);
        return true;
    }

    /// <summary>
    /// Saves a playlist in RetroArch's JSON .lpl format.
    /// </summary>
    public static async Task SaveAsync(Playlist playlist, string filePath, CancellationToken ct = default)
    {
        string? dir = System.IO.Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        string json = JsonSerializer.Serialize(playlist, SerializerOptions);
        await File.WriteAllTextAsync(filePath, json, System.Text.Encoding.UTF8, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Loads a playlist from a JSON .lpl file.
    /// </summary>
    public static async Task<Playlist?> LoadAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            return null;

        string json = await File.ReadAllTextAsync(filePath, System.Text.Encoding.UTF8, ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize<Playlist>(json, SerializerOptions);
    }

    /// <summary>
    /// Returns the recommended playlist directory inside the RetroArch configuration.
    /// On Linux and macOS the playlists live inside the config directory
    /// (e.g. ~/.config/retroarch/playlists), not next to the executable.
    /// </summary>
    public static string? GetRetroArchPlaylistDirectory()
    {
        // Prefer the config directory (correct on Linux/macOS)
        string? configDir = RetroArchLauncher.GetRetroArchConfigDirectory();
        if (configDir != null)
            return System.IO.Path.Combine(configDir, "playlists");

        // Fallback: derive from the configured executable path
        string raPath = AppSettings.Instance.RetroArchPath;
        if (string.IsNullOrEmpty(raPath))
            return null;

        string resolved = RetroArchLauncher.ResolveRetroArchPath(raPath);
        string? raDir = System.IO.Path.GetDirectoryName(resolved);
        if (raDir == null)
            return null;

        return System.IO.Path.Combine(raDir, "playlists");
    }

    // ── Thumbnail downloading ───────────────────────────────────────────

    private const string ThumbnailBaseUrl =
        "https://thumbnails.libretro.com";

    private static readonly HttpClient _httpClient = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var handler = new System.Net.Http.SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(10)
        };
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        client.DefaultRequestHeaders.Add("User-Agent", "RetroMultiTools/1.0");
        return client;
    }

    /// <summary>
    /// Thumbnail categories available in the libretro-thumbnails repository.
    /// </summary>
    public static readonly string[] ThumbnailCategories =
    [
        "Named_Boxarts",
        "Named_Snaps",
        "Named_Titles",
    ];

    /// <summary>
    /// Downloads thumbnails for all items in a playlist.
    /// Thumbnails are placed in the RetroArch thumbnails directory structure:
    /// thumbnails/{db_name}/{category}/{label}.png
    /// </summary>
    public static async Task<(int Downloaded, int Failed, int Skipped)> DownloadThumbnailsAsync(
        Playlist playlist,
        string thumbnailsBaseDir,
        string[] categories,
        bool overwrite,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        int downloaded = 0;
        int failed = 0;
        int skipped = 0;

        foreach (var item in playlist.Items)
        {
            ct.ThrowIfCancellationRequested();

            // Extract system name from db_name (remove .lpl extension)
            string systemName = item.DbName.EndsWith(".lpl", StringComparison.OrdinalIgnoreCase)
                ? item.DbName[..^4]
                : item.DbName;

            if (string.IsNullOrEmpty(systemName))
            {
                failed++;
                continue;
            }

            // Sanitize label for filename and URL
            string safeLabel = SanitizeThumbnailName(item.Label);

            foreach (string category in categories)
            {
                ct.ThrowIfCancellationRequested();

                string destDir = System.IO.Path.Combine(thumbnailsBaseDir, systemName, category);
                string destPath = System.IO.Path.Combine(destDir, $"{safeLabel}.png");

                if (!overwrite && File.Exists(destPath))
                {
                    skipped++;
                    continue;
                }

                string encodedSystem = Uri.EscapeDataString(systemName);
                string encodedCategory = Uri.EscapeDataString(category);
                string encodedLabel = Uri.EscapeDataString(safeLabel);
                string url = $"{ThumbnailBaseUrl}/{encodedSystem}/{encodedCategory}/{encodedLabel}.png";

                progress?.Report($"Downloading: {item.Label} ({category})");

                try
                {
                    using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        Directory.CreateDirectory(destDir);
                        var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                        await File.WriteAllBytesAsync(destPath, bytes, ct).ConfigureAwait(false);
                        downloaded++;
                    }
                    else
                    {
                        skipped++;
                    }
                }
                catch (HttpRequestException)
                {
                    failed++;
                }
                catch (TaskCanceledException) when (!ct.IsCancellationRequested)
                {
                    failed++;
                }
            }
        }

        return (downloaded, failed, skipped);
    }

    /// <summary>
    /// Returns the RetroArch thumbnails directory, or null if not configured.
    /// On Linux and macOS the thumbnails live inside the config directory
    /// (e.g. ~/.config/retroarch/thumbnails), not next to the executable.
    /// </summary>
    public static string? GetRetroArchThumbnailsDirectory()
    {
        // Prefer the config directory (correct on Linux/macOS)
        string? configDir = RetroArchLauncher.GetRetroArchConfigDirectory();
        if (configDir != null)
            return System.IO.Path.Combine(configDir, "thumbnails");

        // Fallback: derive from the configured executable path
        string raPath = AppSettings.Instance.RetroArchPath;
        if (string.IsNullOrEmpty(raPath))
            return null;

        string resolved = RetroArchLauncher.ResolveRetroArchPath(raPath);
        string? raDir = System.IO.Path.GetDirectoryName(resolved);
        if (raDir == null)
            return null;

        return System.IO.Path.Combine(raDir, "thumbnails");
    }

    // ── Private helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Sanitizes a ROM label for use as a thumbnail filename.
    /// RetroArch thumbnail filenames replace these characters: &amp; / : * ? " &lt; &gt; |
    /// </summary>
    private static string SanitizeThumbnailName(string label)
    {
        return label
            .Replace("&", "_")
            .Replace("/", "_")
            .Replace(":", "_")
            .Replace("*", "_")
            .Replace("?", "_")
            .Replace("\"", "_")
            .Replace("<", "_")
            .Replace(">", "_")
            .Replace("|", "_");
    }

    /// <summary>
    /// Returns typical file extensions for the given ROM system.
    /// </summary>
    private static HashSet<string> GetExtensionsForSystem(RomSystem system)
    {
        return system switch
        {
            RomSystem.NES => [".nes", ".unf", ".fds"],
            RomSystem.SNES => [".sfc", ".smc", ".fig", ".swc"],
            RomSystem.N64 => [".z64", ".n64", ".v64"],
            RomSystem.GameBoy => [".gb"],
            RomSystem.GameBoyColor => [".gbc"],
            RomSystem.GameBoyAdvance => [".gba"],
            RomSystem.NintendoDS => [".nds"],
            RomSystem.Nintendo3DS => [".3ds", ".cia"],
            RomSystem.GameCube => [".iso", ".gcm", ".ciso"],
            RomSystem.Wii => [".iso", ".wbfs", ".ciso"],
            RomSystem.VirtualBoy => [".vb"],
            RomSystem.SegaMasterSystem => [".sms"],
            RomSystem.MegaDrive => [".md", ".bin", ".gen", ".smd"],
            RomSystem.SegaCD => [".iso", ".bin", ".cue", ".chd"],
            RomSystem.Sega32X => [".32x"],
            RomSystem.GameGear => [".gg"],
            RomSystem.SegaSaturn => [".iso", ".bin", ".cue", ".chd"],
            RomSystem.SegaDreamcast => [".gdi", ".cdi", ".chd"],
            RomSystem.Atari2600 => [".a26", ".bin"],
            RomSystem.Atari5200 => [".a52", ".bin"],
            RomSystem.Atari7800 => [".a78", ".bin"],
            RomSystem.AtariJaguar => [".j64", ".jag"],
            RomSystem.AtariLynx => [".lnx"],
            RomSystem.PCEngine => [".pce"],
            RomSystem.NeoGeoPocket => [".ngp", ".ngc"],
            RomSystem.NeoGeo => [".zip"],
            RomSystem.NeoGeoCD => [".chd", ".cue"],
            RomSystem.ColecoVision => [".col"],
            RomSystem.Intellivision => [".int"],
            RomSystem.MSX or RomSystem.MSX2 => [".rom", ".mx1", ".mx2"],
            RomSystem.AmstradCPC => [".dsk", ".sna", ".cdt"],
            RomSystem.Arcade => [".zip"],
            RomSystem.Panasonic3DO => [".iso", ".chd", ".cue"],
            RomSystem.FairchildChannelF => [".bin", ".chf"],
            _ => [".zip", ".bin", ".rom"],
        };
    }
}
