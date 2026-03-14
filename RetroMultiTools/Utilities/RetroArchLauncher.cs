using RetroMultiTools.Models;
using RetroMultiTools.Services;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RetroMultiTools.Utilities;

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
        { RomSystem.NES,              "fceumm" },
        { RomSystem.SNES,             "snes9x" },
        { RomSystem.N64,              "mupen64plus_next" },
        { RomSystem.GameBoy,          "gambatte" },
        { RomSystem.GameBoyColor,     "gambatte" },
        { RomSystem.GameBoyAdvance,   "mgba" },
        { RomSystem.VirtualBoy,       "mednafen_vb" },
        { RomSystem.SegaMasterSystem, "genesis_plus_gx" },
        { RomSystem.MegaDrive,        "genesis_plus_gx" },
        { RomSystem.SegaCD,           "genesis_plus_gx" },
        { RomSystem.Sega32X,          "picodrive" },
        { RomSystem.GameGear,         "genesis_plus_gx" },
        { RomSystem.Atari2600,        "stella2014" },
        { RomSystem.Atari5200,        "atari800" },
        { RomSystem.Atari7800,        "prosystem" },
        { RomSystem.AtariJaguar,      "virtualjaguar" },
        { RomSystem.AtariLynx,        "handy" },
        { RomSystem.PCEngine,         "mednafen_pce_fast" },
        { RomSystem.NeoGeoPocket,     "mednafen_ngp" },
        { RomSystem.ColecoVision,     "bluemsx" },
        { RomSystem.Intellivision,    "freeintv" },
        { RomSystem.MSX,              "bluemsx" },
        { RomSystem.MSX2,             "bluemsx" },
        { RomSystem.AmstradCPC,       "cap32" },
        { RomSystem.ThomsonMO5,       "theodore" },
        { RomSystem.WataraSupervision,"potator" },
        { RomSystem.ColorComputer,    "xroar" },
        { RomSystem.Panasonic3DO,     "opera" },
        { RomSystem.AmigaCD32,        "puae" },
        { RomSystem.SegaSaturn,       "mednafen_saturn" },
        { RomSystem.SegaDreamcast,    "flycast" },
        { RomSystem.GameCube,         "dolphin" },
        { RomSystem.Wii,              "dolphin" },
        { RomSystem.Arcade,           "mame2003_plus" },
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
        if (!string.IsNullOrEmpty(configured) && File.Exists(configured))
            return configured;

        // Auto-detect from common install locations
        string? detected = DetectRetroArchPath();
        return detected ?? string.Empty;
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
                Arguments = isFlatpak
                    ? $"run org.libretro.RetroArch -L \"{corePath}\" \"{romPath}\""
                    : $"-L \"{corePath}\" \"{romPath}\"",
                UseShellExecute = false,
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                return new LaunchResult(false, "Failed to start RetroArch process.");

            return new LaunchResult(true, $"Launched with core: {GetCoreDisplayName(system)}");
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
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
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
    public sealed class LaunchResult(bool success, string message)
    {
        public bool Success { get; } = success;
        public string Message { get; } = message;
    }
}
