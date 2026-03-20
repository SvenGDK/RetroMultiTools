using RetroMultiTools.Models;
using RetroMultiTools.Services;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RetroMultiTools.Utilities.RetroArch;

/// <summary>
/// Handles finding, downloading, and launching RetroArch with the correct
/// libretro core for each supported ROM system.
/// </summary>
public static class RetroArchLauncher
{
    /// <summary>
    /// Maps each supported RomSystem to its recommended RetroArch libretro core name.
    /// Core names correspond to the library filename without platform prefix/suffix
    /// (e.g. "snes9x" → snes9x_libretro.dll/.so/.dylib).
    /// </summary>
    private static readonly Dictionary<RomSystem, string> SystemCoreMap = new()
    {
        { RomSystem.AmigaCD32,         "puae" },
        { RomSystem.AmstradCPC,        "cap32" },
        { RomSystem.Arcade,            "mame2003_plus" },
        { RomSystem.Atari2600,         "stella2014" },
        { RomSystem.Atari5200,         "atari800" },
        { RomSystem.Atari7800,         "prosystem" },
        { RomSystem.Atari800,          "atari800" },
        { RomSystem.AtariJaguar,       "virtualjaguar" },
        { RomSystem.AtariLynx,         "handy" },
        { RomSystem.ColecoVision,      "bluemsx" },
        { RomSystem.ColorComputer,     "xroar" },
        { RomSystem.FairchildChannelF, "freechaf" },
        { RomSystem.GameBoy,           "gambatte" },
        { RomSystem.GameBoyAdvance,    "mgba" },
        { RomSystem.GameBoyColor,      "gambatte" },
        { RomSystem.GameCube,          "dolphin" },
        { RomSystem.GameGear,          "genesis_plus_gx" },
        { RomSystem.Intellivision,     "freeintv" },
        { RomSystem.MegaDrive,         "genesis_plus_gx" },
        { RomSystem.MemotechMTX,       "mame2003_plus" },
        { RomSystem.MSX,               "bluemsx" },
        { RomSystem.MSX2,              "bluemsx" },
        { RomSystem.N64,               "mupen64plus_next" },
        { RomSystem.N64DD,             "mupen64plus_next" },
        { RomSystem.NECPC88,           "quasi88" },
        { RomSystem.NeoGeo,            "geolith" },
        { RomSystem.NeoGeoCD,          "neocd" },
        { RomSystem.NeoGeoPocket,      "mednafen_ngp" },
        { RomSystem.NES,               "fceumm" },
        { RomSystem.Nintendo3DS,       "citra" },
        { RomSystem.NintendoDS,        "melonds" },
        { RomSystem.Panasonic3DO,      "opera" },
        { RomSystem.PCEngine,          "mednafen_pce_fast" },
        { RomSystem.PhilipsCDi,        "same_cdi" },
        { RomSystem.Sega32X,           "picodrive" },
        { RomSystem.SegaCD,            "genesis_plus_gx" },
        { RomSystem.SegaDreamcast,     "flycast" },
        { RomSystem.SegaMasterSystem,  "genesis_plus_gx" },
        { RomSystem.SegaSaturn,        "mednafen_saturn" },
        { RomSystem.SNES,              "snes9x" },
        { RomSystem.ThomsonMO5,        "theodore" },
        { RomSystem.VirtualBoy,        "mednafen_vb" },
        { RomSystem.WataraSupervision, "potator" },
        { RomSystem.Wii,               "dolphin" },
    };

    /// <summary>
    /// Returns the recommended libretro core name for the given system,
    /// or null if no core mapping exists.
    /// </summary>
    public static string? GetCoreName(RomSystem system)
    {
        return SystemCoreMap.TryGetValue(system, out var core) ? core : null;
    }

    /// <summary>
    /// Returns a display-friendly core name for the given system (e.g. "snes9x (SNES)").
    /// </summary>
    public static string GetCoreDisplayName(RomSystem system)
    {
        if (SystemCoreMap.TryGetValue(system, out var core))
            return $"{core} ({system})";
        return "Unknown";
    }

    /// <summary>
    /// Checks whether a RetroArch executable exists at the configured or detected path.
    /// </summary>
    public static bool IsRetroArchAvailable()
    {
        string path = GetRetroArchExecutablePath();
        return !string.IsNullOrEmpty(path) && File.Exists(path);
    }

    /// <summary>
    /// Returns the path to the RetroArch executable, checking the user-configured
    /// path first, then falling back to auto-detection on common install locations.
    /// </summary>
    public static string GetRetroArchExecutablePath()
    {
        // Check user-configured path first
        string configured = AppSettings.Instance.RetroArchPath;
        if (!string.IsNullOrEmpty(configured))
        {
            string resolved = ResolveRetroArchPath(configured);
            if (File.Exists(resolved))
                return resolved;
        }

        // Auto-detect from common install locations
        string? detected = DetectRetroArchPath();
        return detected ?? string.Empty;
    }

    /// <summary>
    /// Returns the directory where RetroArch stores its configuration file (retroarch.cfg).
    /// Uses the executable location on Windows, or platform-specific config directories
    /// on Linux and macOS. Returns null if no config directory can be determined.
    /// </summary>
    public static string? GetRetroArchConfigDirectory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // On Windows the config is typically next to the executable
            string exePath = GetRetroArchExecutablePath();
            if (!string.IsNullOrEmpty(exePath))
            {
                string? exeDir = Path.GetDirectoryName(exePath);
                if (!string.IsNullOrEmpty(exeDir) && Directory.Exists(exeDir))
                    return exeDir;
            }

            return null;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] candidates =
            [
                Path.Combine(homeDir, ".config", "retroarch"),
                Path.Combine(homeDir, ".var", "app", "org.libretro.RetroArch", "config", "retroarch"),
                Path.Combine(homeDir, "snap", "retroarch", "current", ".config", "retroarch"),
            ];

            foreach (string dir in candidates)
            {
                if (Directory.Exists(dir))
                    return dir;
            }

            return null;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string configDir = Path.Combine(homeDir, "Library", "Application Support", "RetroArch");
            if (Directory.Exists(configDir))
                return configDir;

            return null;
        }

        return null;
    }

    /// <summary>
    /// Returns the full path to the RetroArch configuration file (retroarch.cfg).
    /// Returns the expected path even if the file does not yet exist, or null
    /// if the config directory cannot be determined.
    /// </summary>
    public static string? GetRetroArchConfigFilePath()
    {
        string? configDir = GetRetroArchConfigDirectory();
        if (configDir == null)
            return null;

        return Path.Combine(configDir, "retroarch.cfg");
    }

    /// <summary>
    /// Resolves a user-provided RetroArch path to the actual executable.
    /// On macOS, if the path points to a .app bundle, resolves to the executable inside it.
    /// On other platforms, returns the path unchanged.
    /// </summary>
    public static string ResolveRetroArchPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // Normalise: strip any trailing directory separators so .app detection works
            // regardless of whether the path was entered with or without a trailing slash.
            string trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (trimmed.EndsWith(".app", StringComparison.OrdinalIgnoreCase) &&
                Directory.Exists(trimmed))
            {
                string executable = Path.Combine(trimmed, "Contents", "MacOS", "RetroArch");
                if (File.Exists(executable))
                    return executable;
            }
        }

        return path;
    }

    /// <summary>
    /// Launches a ROM in RetroArch with the appropriate libretro core.
    /// Returns a result indicating success or failure with a descriptive message.
    /// </summary>
    public static LaunchResult Launch(string romPath, RomSystem system)
    {
        if (string.IsNullOrEmpty(romPath) || !File.Exists(romPath))
            return new LaunchResult(false, "ROM file not found.");

        string retroArchPath = GetRetroArchExecutablePath();
        if (string.IsNullOrEmpty(retroArchPath) || !File.Exists(retroArchPath))
            return new LaunchResult(false, "RetroArch not found. Please configure the path in Settings or download RetroArch.");

        if (!SystemCoreMap.TryGetValue(system, out var coreName))
            return new LaunchResult(false, $"No RetroArch core mapping found for system: {system}");

        string? corePath = FindCorePath(retroArchPath, coreName);
        if (corePath == null)
            return new LaunchResult(false, $"Core '{coreName}' not found. Please install the '{coreName}' core in RetroArch (Online Updater → Core Downloader).");

        try
        {
            bool isFlatpak = retroArchPath.EndsWith("org.libretro.RetroArch", StringComparison.Ordinal);

            var startInfo = new ProcessStartInfo
            {
                FileName = isFlatpak ? "flatpak" : retroArchPath,
                UseShellExecute = false,
            };

            if (isFlatpak)
            {
                startInfo.ArgumentList.Add("run");
                startInfo.ArgumentList.Add("org.libretro.RetroArch");
                startInfo.ArgumentList.Add("-L");
                startInfo.ArgumentList.Add(corePath);
                startInfo.ArgumentList.Add(romPath);
            }
            else
            {
                startInfo.ArgumentList.Add("-L");
                startInfo.ArgumentList.Add(corePath);
                startInfo.ArgumentList.Add(romPath);
            }

            var process = Process.Start(startInfo);
            if (process == null)
                return new LaunchResult(false, "Failed to start RetroArch process.");

            try
            {
                // Update Discord Rich Presence with the current game
                DiscordRichPresence.UpdatePresence(Path.GetFileName(romPath), system);

                return new LaunchResult(true, $"Launched with core: {GetCoreDisplayName(system)}", process);
            }
            catch
            {
                process.Dispose();
                throw;
            }
        }
        catch (InvalidOperationException ex)
        {
            return new LaunchResult(false, $"Failed to launch RetroArch: {ex.Message}");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return new LaunchResult(false, $"Failed to launch RetroArch: {ex.Message}");
        }
    }

    /// <summary>
    /// Searches for a libretro core file in the RetroArch cores directory.
    /// Returns the full path to the core library, or null if not found.
    /// </summary>
    private static string? FindCorePath(string retroArchPath, string coreName)
    {
        string suffix = GetCoreLibrarySuffix();
        string coreFileName = $"{coreName}_libretro{suffix}";

        string retroArchDir = Path.GetDirectoryName(retroArchPath) ?? string.Empty;

        // RetroArch stores cores in a "cores" subdirectory next to the executable
        string coresDir = Path.Combine(retroArchDir, "cores");
        if (Directory.Exists(coresDir))
        {
            string corePath = Path.Combine(coresDir, coreFileName);
            if (File.Exists(corePath))
                return corePath;
        }

        // Check platform-specific additional core locations
        IEnumerable<string> additionalDirs;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            additionalDirs =
            [
                "/usr/lib/libretro",
                "/usr/lib64/libretro",
                "/usr/lib/x86_64-linux-gnu/libretro",
                "/usr/local/lib/libretro",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "retroarch", "cores"),
                // Flatpak core locations
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".var", "app", "org.libretro.RetroArch", "config", "retroarch", "cores"),
                "/var/lib/flatpak/app/org.libretro.RetroArch/current/active/files/lib/retroarch/cores",
                // Snap core locations
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "snap", "retroarch", "current", ".config", "retroarch", "cores"),
            ];
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            additionalDirs =
            [
                Path.Combine(homeDir, "Library", "Application Support", "RetroArch", "cores"),
                // Inside .app bundle
                "/Applications/RetroArch.app/Contents/Resources/cores",
                Path.Combine(homeDir, "Applications", "RetroArch.app", "Contents", "Resources", "cores"),
                // Homebrew locations
                "/opt/homebrew/lib/retroarch/cores",
                "/usr/local/lib/retroarch/cores",
            ];
        }
        else
        {
            additionalDirs = [];
        }

        foreach (string dir in additionalDirs)
        {
            if (Directory.Exists(dir))
            {
                string corePath = Path.Combine(dir, coreFileName);
                if (File.Exists(corePath))
                    return corePath;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the platform-specific library file suffix for libretro cores.
    /// </summary>
    private static string GetCoreLibrarySuffix()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return ".dll";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return ".dylib";
        return ".so";
    }

    /// <summary>
    /// Attempts to detect an existing RetroArch installation from known locations.
    /// </summary>
    private static string? DetectRetroArchPath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return DetectRetroArchWindows();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return DetectRetroArchLinux();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return DetectRetroArchMacOS();
        return null;
    }

    private static string? DetectRetroArchWindows()
    {
        string[] possiblePaths =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "RetroArch", "retroarch.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "RetroArch", "retroarch.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RetroArch", "retroarch.exe"),
            @"C:\RetroArch\retroarch.exe",
            @"C:\RetroArch-Win64\retroarch.exe",
            @"C:\RetroArch-Win32\retroarch.exe",
            // Scoop installation
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "scoop", "apps", "retroarch", "current", "retroarch.exe"),
        ];

        foreach (string path in possiblePaths)
        {
            if (File.Exists(path))
                return path;
        }
        return null;
    }

    private static string? DetectRetroArchLinux()
    {
        string[] possiblePaths =
        [
            "/usr/bin/retroarch",
            "/usr/local/bin/retroarch",
            "/snap/bin/retroarch",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "retroarch"),
        ];

        foreach (string path in possiblePaths)
        {
            if (File.Exists(path))
                return path;
        }

        // Try to find retroarch on the system PATH
        string? pathFound = FindOnPath("retroarch");
        if (pathFound != null)
            return pathFound;

        // Try to find via user flatpak
        string flatpakPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share", "flatpak", "exports", "bin", "org.libretro.RetroArch");
        if (File.Exists(flatpakPath))
            return flatpakPath;

        // Try system-wide flatpak
        const string systemFlatpakPath = "/var/lib/flatpak/exports/bin/org.libretro.RetroArch";
        if (File.Exists(systemFlatpakPath))
            return systemFlatpakPath;

        // Try AppImage in common locations
        string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] appImageDirs = [
            Path.Combine(homeDir, "Applications"),
            Path.Combine(homeDir, ".local", "bin"),
            homeDir,
        ];

        foreach (string dir in appImageDirs)
        {
            if (!Directory.Exists(dir))
                continue;

            try
            {
                foreach (string file in Directory.EnumerateFiles(dir, "RetroArch*.AppImage"))
                {
                    return file;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }

        return null;
    }

    private static string? DetectRetroArchMacOS()
    {
        string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] possiblePaths =
        [
            "/Applications/RetroArch.app/Contents/MacOS/RetroArch",
            Path.Combine(homeDir, "Applications", "RetroArch.app", "Contents", "MacOS", "RetroArch"),
            // Homebrew installations
            "/opt/homebrew/bin/retroarch",
            "/usr/local/bin/retroarch",
        ];

        foreach (string path in possiblePaths)
        {
            if (File.Exists(path))
                return path;
        }

        // Try to find retroarch on the system PATH
        return FindOnPath("retroarch");
    }

    /// <summary>
    /// Searches the system PATH environment variable for the given executable name.
    /// Returns the full path if found, or null otherwise.
    /// </summary>
    private static string? FindOnPath(string executableName)
    {
        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (pathEnv == null)
            return null;

        char separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';
        string fileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? executableName + ".exe"
            : executableName;

        foreach (string dir in pathEnv.Split(separator, StringSplitOptions.RemoveEmptyEntries))
        {
            string fullPath = Path.Combine(dir, fileName);
            if (File.Exists(fullPath))
                return fullPath;
        }
        return null;
    }

    /// <summary>
    /// Returns the RetroArch download page URL for the current platform.
    /// </summary>
    public static string GetDownloadUrl()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "https://www.retroarch.com/?page=platforms";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "https://www.retroarch.com/?page=platforms";

        // Linux — users typically install via package manager
        return "https://www.retroarch.com/index.php?page=linux-instructions";
    }

    /// <summary>
    /// Opens the RetroArch download page in the user's default browser.
    /// After installation, the user can use Auto-Detect or Browse to configure the path.
    /// </summary>
    public static bool OpenDownloadPage()
    {
        try
        {
            string url = GetDownloadUrl();
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Returns whether the given ROM system is supported for launching in RetroArch.
    /// </summary>
    public static bool IsSystemSupported(RomSystem system)
    {
        return system != RomSystem.Unknown && SystemCoreMap.ContainsKey(system);
    }

    /// <summary>
    /// Result of a RetroArch launch attempt.
    /// </summary>
    public sealed class LaunchResult(bool success, string message, Process? process = null)
    {
        public bool Success { get; } = success;
        public string Message { get; } = message;

        /// <summary>
        /// The launched RetroArch process, if available.
        /// Caller is responsible for disposing when no longer needed.
        /// </summary>
        public Process? Process { get; } = process;
    }
}
