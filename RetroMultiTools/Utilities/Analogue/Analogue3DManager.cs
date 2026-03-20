using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RetroMultiTools.Utilities.Analogue;

/// <summary>
/// Manages the Analogue 3D SD card: per-game display/hardware settings,
/// label artwork, and Game Pak (N64 ROM) management.
/// </summary>
public static class Analogue3DManager
{
    // ── SD Card Structure ──────────────────────────────────────────────

    private const string SystemDir = "System";
    private const string SettingsDir = "System/Settings";
    private const string LibraryDir = "System/Library";
    private const string LibraryImagesDir = "System/Library/Images";
    private const string LibrarySettingsDir = "System/Library/Settings";
    private const string RomsDir = "N64";
    private const string SavesDir = "Saves";

    /// <summary>
    /// Validates that the given path looks like an Analogue 3D SD card root.
    /// </summary>
    public static bool ValidateSdCard(string sdRoot)
    {
        if (string.IsNullOrWhiteSpace(sdRoot) || !Directory.Exists(sdRoot))
            return false;

        return Directory.Exists(Path.Combine(sdRoot, SystemDir))
            || Directory.Exists(Path.Combine(sdRoot, RomsDir));
    }

    // ── Game Pak Management ────────────────────────────────────────────

    /// <summary>
    /// Represents an N64 Game Pak (ROM) on the SD card.
    /// </summary>
    public sealed class GamePakInfo
    {
        public string FileName { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string SizeFormatted => FileUtils.FormatFileSize(SizeBytes);
        public bool HasLabelArt { get; set; }
        public bool HasDisplaySettings { get; set; }
        public bool HasHardwareSettings { get; set; }
        public string InternalName { get; set; } = string.Empty;
        public string GameCode { get; set; } = string.Empty;
    }

    /// <summary>
    /// Lists all N64 Game Pak ROMs on the SD card.
    /// </summary>
    public static async Task<List<GamePakInfo>> ListGamePaksAsync(string sdRoot)
    {
        string romsPath = Path.Combine(sdRoot, RomsDir);
        if (!Directory.Exists(romsPath))
            return [];

        string imagesPath = Path.Combine(sdRoot, LibraryImagesDir);
        string settingsPath = Path.Combine(sdRoot, LibrarySettingsDir);

        return await Task.Run(() =>
        {
            var paks = new List<GamePakInfo>();
            HashSet<string> romExtensions = [".z64", ".n64", ".v64"];

            foreach (string file in Directory.EnumerateFiles(romsPath, "*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (!romExtensions.Contains(ext))
                    continue;

                var fi = new FileInfo(file);
                string baseName = Path.GetFileNameWithoutExtension(file);

                var pak = new GamePakInfo
                {
                    FileName = fi.Name,
                    FullPath = file,
                    SizeBytes = fi.Length,
                    HasLabelArt = Directory.Exists(imagesPath) &&
                        File.Exists(Path.Combine(imagesPath, baseName + ".png")),
                    HasDisplaySettings = Directory.Exists(settingsPath) &&
                        File.Exists(Path.Combine(settingsPath, baseName + ".display.json")),
                    HasHardwareSettings = Directory.Exists(settingsPath) &&
                        File.Exists(Path.Combine(settingsPath, baseName + ".hardware.json"))
                };

                // Try reading N64 ROM header for internal name and game code
                try
                {
                    using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                    byte[] header = new byte[64];
                    int read = fs.Read(header, 0, 64);

                    if (read >= 64)
                    {
                        // Detect byte order and swap if needed
                        byte[] normalized = NormalizeN64Header(header);

                        // Internal name at offset 0x20, 20 bytes
                        pak.InternalName = Encoding.ASCII
                            .GetString(normalized, 0x20, 20)
                            .TrimEnd('\0', ' ');

                        // Game code at offset 0x3B, 4 bytes
                        if (normalized.Length >= 0x3F)
                        {
                            pak.GameCode = Encoding.ASCII
                                .GetString(normalized, 0x3B, 4)
                                .TrimEnd('\0', ' ');
                        }
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }

                paks.Add(pak);
            }

            return paks.OrderBy(p => p.FileName).ToList();
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Normalizes an N64 ROM header to big-endian (z64) format.
    /// </summary>
    private static byte[] NormalizeN64Header(byte[] header)
    {
        if (header.Length < 4) return header;

        // Check magic bytes to determine byte order
        uint magic = (uint)(header[0] << 24 | header[1] << 16 | header[2] << 8 | header[3]);

        switch (magic)
        {
            case 0x80371240: // z64 (big-endian) — already correct
                return header;

            case 0x37804012: // v64 (byte-swapped)
            {
                byte[] swapped = new byte[header.Length];
                for (int i = 0; i < header.Length - 1; i += 2)
                {
                    swapped[i] = header[i + 1];
                    swapped[i + 1] = header[i];
                }
                return swapped;
            }

            case 0x40123780: // n64 (little-endian)
            {
                byte[] reversed = new byte[header.Length];
                for (int i = 0; i < header.Length - 3; i += 4)
                {
                    reversed[i] = header[i + 3];
                    reversed[i + 1] = header[i + 2];
                    reversed[i + 2] = header[i + 1];
                    reversed[i + 3] = header[i];
                }
                return reversed;
            }

            default:
                return header;
        }
    }

    /// <summary>
    /// Deletes a Game Pak ROM and its associated settings/artwork from the SD card.
    /// </summary>
    public static async Task DeleteGamePakAsync(string sdRoot, GamePakInfo pak)
    {
        await Task.Run(() =>
        {
            if (File.Exists(pak.FullPath))
                File.Delete(pak.FullPath);

            string baseName = Path.GetFileNameWithoutExtension(pak.FileName);
            string imagesPath = Path.Combine(sdRoot, LibraryImagesDir);
            string settingsPath = Path.Combine(sdRoot, LibrarySettingsDir);

            TryDeleteFile(Path.Combine(imagesPath, baseName + ".png"));
            TryDeleteFile(Path.Combine(settingsPath, baseName + ".display.json"));
            TryDeleteFile(Path.Combine(settingsPath, baseName + ".hardware.json"));
        }).ConfigureAwait(false);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    // ── Per-Game Settings ──────────────────────────────────────────────

    /// <summary>
    /// Per-game display settings for the Analogue 3D.
    /// </summary>
    public sealed class DisplaySettings
    {
        [JsonPropertyName("resolution")]
        public string Resolution { get; set; } = "auto";

        [JsonPropertyName("crop_overscan")]
        public bool CropOverscan { get; set; }

        [JsonPropertyName("smoothing")]
        public string Smoothing { get; set; } = "off";

        [JsonPropertyName("aspect_ratio")]
        public string AspectRatio { get; set; } = "auto";
    }

    /// <summary>
    /// Per-game hardware settings for the Analogue 3D.
    /// </summary>
    public sealed class HardwareSettings
    {
        [JsonPropertyName("expansion_pak")]
        public bool ExpansionPak { get; set; } = true;

        [JsonPropertyName("controller_pak")]
        public string ControllerPak { get; set; } = "auto";

        [JsonPropertyName("rumble_pak")]
        public bool RumblePak { get; set; } = true;

        [JsonPropertyName("cpu_overclock")]
        public bool CpuOverclock { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Loads per-game display settings for the specified ROM.
    /// Returns defaults if no settings file exists or cannot be read.
    /// </summary>
    public static async Task<DisplaySettings> LoadDisplaySettingsAsync(string sdRoot, string romFileName)
    {
        string baseName = Path.GetFileNameWithoutExtension(romFileName);
        string settingsFile = Path.Combine(sdRoot, LibrarySettingsDir, baseName + ".display.json");

        if (!File.Exists(settingsFile))
            return new DisplaySettings();

        try
        {
            string json = await File.ReadAllTextAsync(settingsFile).ConfigureAwait(false);
            return JsonSerializer.Deserialize<DisplaySettings>(json, JsonOptions) ?? new DisplaySettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new DisplaySettings();
        }
    }

    /// <summary>
    /// Saves per-game display settings for the specified ROM.
    /// Uses atomic write (temp file + rename) to prevent corruption.
    /// </summary>
    public static async Task SaveDisplaySettingsAsync(string sdRoot, string romFileName, DisplaySettings settings)
    {
        string baseName = Path.GetFileNameWithoutExtension(romFileName);
        string settingsDir = Path.Combine(sdRoot, LibrarySettingsDir);
        Directory.CreateDirectory(settingsDir);

        string settingsFile = Path.Combine(settingsDir, baseName + ".display.json");
        string tempFile = settingsFile + ".tmp";
        string json = JsonSerializer.Serialize(settings, JsonOptions);
        await File.WriteAllTextAsync(tempFile, json).ConfigureAwait(false);
        File.Move(tempFile, settingsFile, overwrite: true);
    }

    /// <summary>
    /// Loads per-game hardware settings for the specified ROM.
    /// Returns defaults if no settings file exists or cannot be read.
    /// </summary>
    public static async Task<HardwareSettings> LoadHardwareSettingsAsync(string sdRoot, string romFileName)
    {
        string baseName = Path.GetFileNameWithoutExtension(romFileName);
        string settingsFile = Path.Combine(sdRoot, LibrarySettingsDir, baseName + ".hardware.json");

        if (!File.Exists(settingsFile))
            return new HardwareSettings();

        try
        {
            string json = await File.ReadAllTextAsync(settingsFile).ConfigureAwait(false);
            return JsonSerializer.Deserialize<HardwareSettings>(json, JsonOptions) ?? new HardwareSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new HardwareSettings();
        }
    }

    /// <summary>
    /// Saves per-game hardware settings for the specified ROM.
    /// Uses atomic write (temp file + rename) to prevent corruption.
    /// </summary>
    public static async Task SaveHardwareSettingsAsync(string sdRoot, string romFileName, HardwareSettings settings)
    {
        string baseName = Path.GetFileNameWithoutExtension(romFileName);
        string settingsDir = Path.Combine(sdRoot, LibrarySettingsDir);
        Directory.CreateDirectory(settingsDir);

        string settingsFile = Path.Combine(settingsDir, baseName + ".hardware.json");
        string tempFile = settingsFile + ".tmp";
        string json = JsonSerializer.Serialize(settings, JsonOptions);
        await File.WriteAllTextAsync(tempFile, json).ConfigureAwait(false);
        File.Move(tempFile, settingsFile, overwrite: true);
    }

    // ── Label Artwork ──────────────────────────────────────────────────

    /// <summary>
    /// Copies a label artwork image to the library images directory.
    /// </summary>
    public static async Task SetLabelArtworkAsync(string sdRoot, string romFileName, string imagePath)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException("Image file not found.", imagePath);

        string baseName = Path.GetFileNameWithoutExtension(romFileName);
        string imagesDir = Path.Combine(sdRoot, LibraryImagesDir);
        Directory.CreateDirectory(imagesDir);

        string destPath = Path.Combine(imagesDir, baseName + ".png");
        await Task.Run(() => File.Copy(imagePath, destPath, overwrite: true)).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes the label artwork for the specified ROM.
    /// </summary>
    public static async Task RemoveLabelArtworkAsync(string sdRoot, string romFileName)
    {
        string baseName = Path.GetFileNameWithoutExtension(romFileName);
        string imagePath = Path.Combine(sdRoot, LibraryImagesDir, baseName + ".png");

        await Task.Run(() =>
        {
            if (File.Exists(imagePath))
                File.Delete(imagePath);
        }).ConfigureAwait(false);
    }
}
