using RetroMultiTools.Localization;
using RetroMultiTools.Models;
using RetroMultiTools.Services;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RetroMultiTools.Utilities.Mednafen;

/// <summary>
/// Handles finding, downloading, and launching standalone Mednafen for supported ROM systems.
/// Mirrors the RetroArchLauncher / MameLauncher pattern so all three emulators are first-class citizens.
/// </summary>
public static class MednafenLauncher
{
    /// <summary>
    /// Maps each supported RomSystem to the Mednafen module name used with the -force_module flag.
    /// Mednafen supports many systems natively; this map covers the ones it handles well.
    /// </summary>
    private static readonly Dictionary<RomSystem, string> SystemModuleMap = new()
    {
        { RomSystem.AtariLynx,        "lynx" },
        { RomSystem.GameBoy,          "gb" },
        { RomSystem.GameBoyAdvance,   "gba" },
        { RomSystem.GameBoyColor,     "gb" },
        { RomSystem.GameGear,         "gg" },
        { RomSystem.MegaDrive,        "md" },
        { RomSystem.NeoGeoPocket,     "ngp" },
        { RomSystem.NES,              "nes" },
        { RomSystem.PCEngine,         "pce" },
        { RomSystem.SegaMasterSystem, "sms" },
        { RomSystem.SegaSaturn,       "ss" },
        { RomSystem.SNES,             "snes" },
        { RomSystem.VirtualBoy,       "vb" },
    };

    /// <summary>
    /// Returns the Mednafen module name for the given system, or null if no mapping exists.
    /// </summary>
    public static string? GetModuleName(RomSystem system)
    {
        return SystemModuleMap.TryGetValue(system, out var module) ? module : null;
    }

    /// <summary>
    /// Returns a display-friendly module name for the given system (e.g. "pce (PCEngine)").
    /// </summary>
    public static string GetModuleDisplayName(RomSystem system)
    {
        if (SystemModuleMap.TryGetValue(system, out var module))
            return $"{module} ({system})";
        return "Unknown";
    }

    /// <summary>
    /// Checks whether a Mednafen executable exists at the configured or detected path.
    /// </summary>
    public static bool IsMednafenAvailable()
    {
        string path = GetMednafenExecutablePath();
        return !string.IsNullOrEmpty(path) && File.Exists(path);
    }

    /// <summary>
    /// Returns the path to the Mednafen executable, checking the user-configured
    /// path first, then falling back to auto-detection on common install locations.
    /// </summary>
    public static string GetMednafenExecutablePath()
    {
        // Check user-configured path first
        string configured = AppSettings.Instance.MednafenPath;
        if (!string.IsNullOrEmpty(configured))
        {
            string resolved = ResolveMednafenPath(configured);
            if (File.Exists(resolved))
                return resolved;
        }

        // Auto-detect from common install locations
        string? detected = DetectMednafenPath();
        return detected ?? string.Empty;
    }

    /// <summary>
    /// Returns the directory where Mednafen stores its configuration and data files.
    /// On Windows this is typically next to the executable; on Linux/macOS it uses
    /// the platform-specific config directories. Returns null if undetermined.
    /// </summary>
    public static string? GetMednafenConfigDirectory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string exePath = GetMednafenExecutablePath();
            if (!string.IsNullOrEmpty(exePath))
            {
                string? exeDir = Path.GetDirectoryName(exePath);
                if (!string.IsNullOrEmpty(exeDir) && Directory.Exists(exeDir))
                    return exeDir;
            }

            return null;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(homeDir))
                return null;

            // Return the expected path even if the directory doesn't exist yet
            // so callers can create it on demand (e.g. on fresh installs).
            return Path.Combine(homeDir, ".mednafen");
        }

        return null;
    }

    /// <summary>
    /// Returns the full path to the Mednafen configuration file (mednafen.cfg).
    /// Returns the expected path even if the file does not yet exist, or null
    /// if the config directory cannot be determined.
    /// </summary>
    public static string? GetMednafenConfigFilePath()
    {
        string? configDir = GetMednafenConfigDirectory();
        if (configDir == null)
            return null;

        return Path.Combine(configDir, "mednafen.cfg");
    }

    /// <summary>
    /// Resolves a user-provided Mednafen path to the actual executable.
    /// Handles macOS .app bundles, directory paths containing the executable,
    /// and symlinks on all platforms.
    /// </summary>
    public static string ResolveMednafenPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        // Decode any URI percent-encoding (e.g. %20 → space) that may remain
        // from file-picker URIs on macOS.  This is idempotent for plain paths.
        path = Uri.UnescapeDataString(path);

        // Normalise: strip any trailing directory separators
        string trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // Case 1: path IS a .app bundle (e.g. /Applications/Mednafen.app)
            if (trimmed.EndsWith(".app", StringComparison.OrdinalIgnoreCase) &&
                Directory.Exists(trimmed))
            {
                string? resolved = AppBundleHelper.ResolveAppBundleExecutable(trimmed, "mednafen");
                if (resolved != null)
                    return resolved;
            }

            // Case 2: path is INSIDE a .app bundle
            // (e.g. /path/Mednafen.app/Contents/MacOS/mednafen)
            string? bundleRoot = AppBundleHelper.GetAppBundleRoot(trimmed);
            if (bundleRoot != null)
            {
                // If the exact file exists inside the bundle, use it directly
                if (File.Exists(trimmed))
                    return trimmed;

                // Otherwise resolve the bundle to its main executable
                string? resolved = AppBundleHelper.ResolveAppBundleExecutable(bundleRoot, "mednafen");
                if (resolved != null)
                    return resolved;
            }
        }

        // If the path is already an existing file, return it directly
        if (File.Exists(trimmed))
            return trimmed;

        // If the path is a directory, look for the executable inside it
        if (Directory.Exists(trimmed))
        {
            string exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "mednafen.exe" : "mednafen";

            string candidate = Path.Combine(trimmed, exeName);
            if (File.Exists(candidate))
                return candidate;

            // Check a bin/ subdirectory (common for custom prefix installs)
            candidate = Path.Combine(trimmed, "bin", exeName);
            if (File.Exists(candidate))
                return candidate;

            // On macOS, search for .app bundles within the directory.
            // This handles the case where the Avalonia folder picker returned
            // the parent directory because it could not select the .app bundle
            // directly (Avalonia limitation – see Avalonia #18080).
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string? bundle = AppBundleHelper.FindAppBundleInDirectory(trimmed, "mednafen");
                if (bundle != null)
                {
                    string? resolved = AppBundleHelper.ResolveAppBundleExecutable(bundle, "mednafen");
                    if (resolved != null)
                        return resolved;
                }
            }
        }

        return path;
    }

    /// <summary>
    /// Launches a ROM in standalone Mednafen with the appropriate system module.
    /// Returns a result indicating success or failure with a descriptive message.
    /// </summary>
    public static LaunchResult Launch(string romPath, RomSystem system)
    {
        if (string.IsNullOrEmpty(romPath) || !File.Exists(romPath))
            return new LaunchResult(false, LocalizationManager.Instance["MednafenLauncher_RomNotFound"]);

        string mednafenPath = GetMednafenExecutablePath();
        if (string.IsNullOrEmpty(mednafenPath) || !File.Exists(mednafenPath))
            return new LaunchResult(false, LocalizationManager.Instance["MednafenLauncher_MednafenNotFound"]);

        if (!SystemModuleMap.TryGetValue(system, out var moduleName))
            return new LaunchResult(false, string.Format(LocalizationManager.Instance["MednafenLauncher_NoModuleMapping"], system));

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = mednafenPath,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(mednafenPath) ?? string.Empty,
            };

            // Force the correct emulation module and pass the ROM path
            startInfo.ArgumentList.Add("-force_module");
            startInfo.ArgumentList.Add(moduleName);
            startInfo.ArgumentList.Add(romPath);

            var process = Process.Start(startInfo);
            if (process == null)
                return new LaunchResult(false, LocalizationManager.Instance["MednafenLauncher_FailedToStart"]);

            try
            {
                // Update Discord Rich Presence with the current game
                DiscordRichPresence.UpdatePresence(Path.GetFileName(romPath), system);

                return new LaunchResult(true, string.Format(LocalizationManager.Instance["MednafenLauncher_Launched"], Path.GetFileName(romPath), moduleName), process);
            }
            catch
            {
                process.Dispose();
                throw;
            }
        }
        catch (InvalidOperationException ex)
        {
            return new LaunchResult(false, string.Format(LocalizationManager.Instance["MednafenLauncher_FailedToLaunch"], ex.Message));
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return new LaunchResult(false, string.Format(LocalizationManager.Instance["MednafenLauncher_FailedToLaunch"], ex.Message));
        }
    }

    /// <summary>
    /// Returns whether the given ROM system is supported for launching in standalone Mednafen.
    /// </summary>
    public static bool IsSystemSupported(RomSystem system)
    {
        return system != RomSystem.Unknown && SystemModuleMap.ContainsKey(system);
    }

    /// <summary>
    /// Returns the Mednafen download page URL.
    /// </summary>
    public static string GetDownloadUrl()
    {
        return "https://mednafen.github.io/releases/";
    }

    /// <summary>
    /// Opens the Mednafen download page in the user's default browser.
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
    /// Attempts to detect an existing Mednafen installation from known locations.
    /// </summary>
    private static string? DetectMednafenPath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return DetectMednafenWindows();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return DetectMednafenLinux();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return DetectMednafenMacOS();
        return null;
    }

    private static string? DetectMednafenWindows()
    {
        string[] possiblePaths =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Mednafen", "mednafen.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Mednafen", "mednafen.exe"),
            @"C:\Mednafen\mednafen.exe",
            @"C:\mednafen\mednafen.exe",
            // Scoop installation
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "scoop", "apps", "mednafen", "current", "mednafen.exe"),
            // Chocolatey installation
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "chocolatey", "bin", "mednafen.exe"),
        ];

        foreach (string path in possiblePaths)
        {
            if (File.Exists(path))
                return path;
        }
        return null;
    }

    private static string? DetectMednafenLinux()
    {
        string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] possiblePaths =
        [
            "/usr/bin/mednafen",
            "/usr/local/bin/mednafen",
            "/usr/games/mednafen",
            "/snap/bin/mednafen",
            Path.Combine(homeDir, ".local", "bin", "mednafen"),
            // Nix package manager
            Path.Combine(homeDir, ".nix-profile", "bin", "mednafen"),
            "/run/current-system/sw/bin/mednafen",
        ];

        foreach (string path in possiblePaths)
        {
            if (File.Exists(path))
                return path;
        }

        // Try to find mednafen on the system PATH
        return FindOnPath("mednafen");
    }

    private static string? DetectMednafenMacOS()
    {
        string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] possiblePaths =
        [
            // Direct executable paths
            "/opt/homebrew/bin/mednafen",
            "/usr/local/bin/mednafen",
            // MacPorts installation
            "/opt/local/bin/mednafen",
            Path.Combine(homeDir, "Applications", "mednafen", "mednafen"),
            // Nix package manager
            Path.Combine(homeDir, ".nix-profile", "bin", "mednafen"),
            "/run/current-system/sw/bin/mednafen",
        ];

        foreach (string path in possiblePaths)
        {
            if (File.Exists(path))
                return path;
        }

        // Check .app bundles and resolve to the executable inside
        string[] appBundles =
        [
            "/Applications/Mednafen.app",
            "/Applications/mednafen.app",
            Path.Combine(homeDir, "Applications", "Mednafen.app"),
            Path.Combine(homeDir, "Applications", "mednafen.app"),
        ];

        foreach (string bundle in appBundles)
        {
            if (Directory.Exists(bundle))
            {
                string resolved = ResolveMednafenPath(bundle);
                if (File.Exists(resolved))
                    return resolved;
            }
        }

        // Try to find mednafen on the system PATH
        return FindOnPath("mednafen");
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
    /// Result of a Mednafen launch attempt.
    /// </summary>
    public sealed class LaunchResult(bool success, string message, Process? process = null)
    {
        public bool Success { get; } = success;
        public string Message { get; } = message;

        /// <summary>
        /// The launched Mednafen process, if available.
        /// Caller is responsible for disposing when no longer needed.
        /// </summary>
        public Process? Process { get; } = process;
    }
}
