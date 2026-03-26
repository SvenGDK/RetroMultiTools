using RetroMultiTools.Localization;
using RetroMultiTools.Models;
using RetroMultiTools.Services;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RetroMultiTools.Utilities.Mame;

/// <summary>
/// Handles finding, downloading, and launching standalone MAME for Arcade ROMs.
/// Mirrors the RetroArchLauncher pattern so both emulators are first-class citizens.
/// </summary>
public static class MameLauncher
{
    /// <summary>
    /// Checks whether a MAME executable exists at the configured or detected path.
    /// </summary>
    public static bool IsMameAvailable()
    {
        string path = GetMameExecutablePath();
        return !string.IsNullOrEmpty(path) && File.Exists(path);
    }

    /// <summary>
    /// Returns the path to the MAME executable, checking the user-configured
    /// path first, then falling back to auto-detection on common install locations.
    /// </summary>
    public static string GetMameExecutablePath()
    {
        // Check user-configured path first
        string configured = AppSettings.Instance.MamePath;
        if (!string.IsNullOrEmpty(configured))
        {
            string resolved = ResolveMamePath(configured);
            if (File.Exists(resolved))
                return resolved;
        }

        // Auto-detect from common install locations
        string? detected = DetectMamePath();
        return detected ?? string.Empty;
    }

    /// <summary>
    /// Resolves a user-provided MAME path to the actual executable.
    /// On macOS, if the path points to a .app bundle, resolves to the executable inside it.
    /// On other platforms, returns the path unchanged.
    /// </summary>
    public static string ResolveMamePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            string trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (trimmed.EndsWith(".app", StringComparison.OrdinalIgnoreCase) &&
                Directory.Exists(trimmed))
            {
                string executable = Path.Combine(trimmed, "Contents", "MacOS", "mame");
                if (File.Exists(executable))
                    return executable;
            }
        }

        return path;
    }

    /// <summary>
    /// Launches an Arcade ROM in standalone MAME.
    /// MAME expects the ROM name without extension (the short "set name").
    /// Returns a result indicating success or failure with a descriptive message.
    /// </summary>
    public static LaunchResult Launch(string romPath, RomSystem system)
    {
        if (string.IsNullOrEmpty(romPath) || !File.Exists(romPath))
            return new LaunchResult(false, LocalizationManager.Instance["MameLauncher_RomNotFound"]);

        string mamePath = GetMameExecutablePath();
        if (string.IsNullOrEmpty(mamePath) || !File.Exists(mamePath))
            return new LaunchResult(false, LocalizationManager.Instance["MameLauncher_MameNotFound"]);

        try
        {
            string romDir = Path.GetDirectoryName(romPath) ?? string.Empty;
            string romName = Path.GetFileNameWithoutExtension(romPath);

            var startInfo = new ProcessStartInfo
            {
                FileName = mamePath,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(mamePath) ?? string.Empty,
            };

            // Tell MAME where to find ROMs and which ROM set to run
            startInfo.ArgumentList.Add("-rompath");
            startInfo.ArgumentList.Add(romDir);
            startInfo.ArgumentList.Add(romName);

            var process = Process.Start(startInfo);
            if (process == null)
                return new LaunchResult(false, LocalizationManager.Instance["MameLauncher_FailedToStart"]);

            try
            {
                // Update Discord Rich Presence with the current game
                DiscordRichPresence.UpdatePresence(Path.GetFileName(romPath), system);

                return new LaunchResult(true, string.Format(LocalizationManager.Instance["MameLauncher_Launched"], romName), process);
            }
            catch
            {
                process.Dispose();
                throw;
            }
        }
        catch (InvalidOperationException ex)
        {
            return new LaunchResult(false, string.Format(LocalizationManager.Instance["MameLauncher_FailedToLaunch"], ex.Message));
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return new LaunchResult(false, string.Format(LocalizationManager.Instance["MameLauncher_FailedToLaunch"], ex.Message));
        }
    }

    /// <summary>
    /// Returns whether the given ROM system is supported for launching in standalone MAME.
    /// Currently only Arcade ROMs are supported.
    /// </summary>
    public static bool IsSystemSupported(RomSystem system)
    {
        return system == RomSystem.Arcade;
    }

    /// <summary>
    /// Returns the MAME download page URL for the current platform.
    /// </summary>
    public static string GetDownloadUrl()
    {
        return "https://www.mamedev.org/release.html";
    }

    /// <summary>
    /// Opens the MAME download page in the user's default browser.
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
    /// Attempts to detect an existing MAME installation from known locations.
    /// </summary>
    private static string? DetectMamePath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return DetectMameWindows();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return DetectMameLinux();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return DetectMameMacOS();
        return null;
    }

    private static string? DetectMameWindows()
    {
        string[] possiblePaths =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MAME", "mame.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "MAME", "mame.exe"),
            @"C:\MAME\mame.exe",
            @"C:\mame\mame.exe",
            // Scoop installation
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "scoop", "apps", "mame", "current", "mame.exe"),
        ];

        foreach (string path in possiblePaths)
        {
            if (File.Exists(path))
                return path;
        }
        return null;
    }

    private static string? DetectMameLinux()
    {
        string[] possiblePaths =
        [
            "/usr/bin/mame",
            "/usr/local/bin/mame",
            "/usr/games/mame",
            "/snap/bin/mame",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "mame"),
        ];

        foreach (string path in possiblePaths)
        {
            if (File.Exists(path))
                return path;
        }

        // Try to find mame on the system PATH
        return FindOnPath("mame");
    }

    private static string? DetectMameMacOS()
    {
        string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] possiblePaths =
        [
            "/Applications/mame/mame",
            Path.Combine(homeDir, "Applications", "mame", "mame"),
            // Homebrew installations
            "/opt/homebrew/bin/mame",
            "/usr/local/bin/mame",
        ];

        foreach (string path in possiblePaths)
        {
            if (File.Exists(path))
                return path;
        }

        // Try to find mame on the system PATH
        return FindOnPath("mame");
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
    /// Result of a MAME launch attempt.
    /// </summary>
    public sealed class LaunchResult(bool success, string message, Process? process = null)
    {
        public bool Success { get; } = success;
        public string Message { get; } = message;

        /// <summary>
        /// The launched MAME process, if available.
        /// Caller is responsible for disposing when no longer needed.
        /// </summary>
        public Process? Process { get; } = process;
    }
}
