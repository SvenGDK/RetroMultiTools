using RetroMultiTools.Models;
using RetroMultiTools.Services;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace RetroMultiTools.Utilities.RetroArch;

/// <summary>
/// Downloads and updates RetroArch libretro cores from the official buildbot.
/// Supports Windows, Linux, and macOS platforms.
/// </summary>
public static class RetroArchCoreDownloader
{
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(5),
        DefaultRequestHeaders =
        {
            { "User-Agent", "RetroMultiTools/1.0" }
        }
    };

    /// <summary>
    /// Represents information about a libretro core.
    /// </summary>
    public sealed class CoreInfo
    {
        public string CoreName { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public bool IsInstalled { get; set; }
        public string? InstalledPath { get; set; }
    }

    /// <summary>
    /// Returns a list of all cores used by the application with their install status.
    /// Uses the SystemCoreMap from RetroArchLauncher. Deduplicates core names.
    /// </summary>
    public static List<CoreInfo> GetAvailableCores()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cores = new List<CoreInfo>();
        string? coresDir = GetCoresDirectory();

        foreach (RomSystem system in Enum.GetValues<RomSystem>())
        {
            string? coreName = RetroArchLauncher.GetCoreName(system);
            if (coreName == null || !seen.Add(coreName))
                continue;

            string displayName = RetroArchLauncher.GetCoreDisplayName(system);
            string coreFileName = GetCoreFileName(coreName);

            string? installedPath = null;
            bool isInstalled = false;

            if (coresDir != null)
            {
                string fullPath = Path.Combine(coresDir, coreFileName);
                if (File.Exists(fullPath))
                {
                    isInstalled = true;
                    installedPath = fullPath;
                }
            }

            cores.Add(new CoreInfo
            {
                CoreName = coreName,
                DisplayName = displayName,
                IsInstalled = isInstalled,
                InstalledPath = installedPath,
            });
        }

        return cores;
    }

    /// <summary>
    /// Returns the cores directory where downloaded cores should be placed.
    /// Creates the directory if it doesn't exist.
    /// </summary>
    public static string? GetCoresDirectory()
    {
        string retroArchPath = RetroArchLauncher.GetRetroArchExecutablePath();
        if (string.IsNullOrEmpty(retroArchPath))
            return null;

        string? retroArchDir = Path.GetDirectoryName(retroArchPath);
        if (string.IsNullOrEmpty(retroArchDir))
            return null;

        string coresDir = Path.Combine(retroArchDir, "cores");

        // On macOS, RetroArch installed as .app bundle stores cores in
        // ~/Library/Application Support/RetroArch/cores rather than inside the bundle.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && !Directory.Exists(coresDir))
        {
            string userCoresDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support", "RetroArch", "cores");

            try
            {
                Directory.CreateDirectory(userCoresDir);
                return userCoresDir;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }

        // On Linux, RetroArch installed via package manager may not have a writable
        // cores directory next to the executable; prefer the user config directory.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && !Directory.Exists(coresDir))
        {
            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            // For Flatpak installations, use the Flatpak-specific config path
            bool isFlatpak = retroArchPath.EndsWith("org.libretro.RetroArch", StringComparison.Ordinal)
                || retroArchPath.Contains("/flatpak/", StringComparison.Ordinal);

            // For Snap installations, use the Snap-specific config path
            bool isSnap = retroArchPath.Contains("/snap/", StringComparison.Ordinal);

            string userCoresDir;
            if (isFlatpak)
            {
                userCoresDir = Path.Combine(homeDir,
                    ".var", "app", "org.libretro.RetroArch", "config", "retroarch", "cores");
            }
            else if (isSnap)
            {
                userCoresDir = Path.Combine(homeDir,
                    "snap", "retroarch", "current", ".config", "retroarch", "cores");
            }
            else
            {
                userCoresDir = Path.Combine(homeDir, ".config", "retroarch", "cores");
            }

            try
            {
                Directory.CreateDirectory(userCoresDir);
                return userCoresDir;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }

        try
        {
            Directory.CreateDirectory(coresDir);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        return coresDir;
    }

    /// <summary>
    /// Downloads a single core from the buildbot, extracts it, and places it in the cores directory.
    /// </summary>
    public static async Task<bool> DownloadCoreAsync(
        string coreName,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string? coresDir = GetCoresDirectory();
        if (coresDir == null)
        {
            progress?.Report("RetroArch cores directory not found.");
            return false;
        }

        string url = GetDownloadUrl(coreName);
        string coreFileName = GetCoreFileName(coreName);
        string destinationPath = Path.Combine(coresDir, coreFileName);
        string tempZipPath = Path.Combine(Path.GetTempPath(), $"{coreName}_libretro_{Guid.NewGuid():N}.zip");

        try
        {
            progress?.Report($"Downloading {coreName}...");

            using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();

                using var fileStream = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
                await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            progress?.Report($"Extracting {coreName}...");

            using (var archive = ZipFile.OpenRead(tempZipPath))
            {
                var entry = archive.Entries.FirstOrDefault(e =>
                    e.Name.Equals(coreFileName, StringComparison.OrdinalIgnoreCase));

                if (entry == null)
                {
                    progress?.Report($"Core library '{coreFileName}' not found in archive.");
                    return false;
                }

                string tempExtractPath = destinationPath + ".tmp";
                bool extracted = false;
                try
                {
                    entry.ExtractToFile(tempExtractPath, overwrite: true);
                    File.Move(tempExtractPath, destinationPath, overwrite: true);
                    extracted = true;
                }
                finally
                {
                    if (!extracted && File.Exists(tempExtractPath))
                    {
                        try { File.Delete(tempExtractPath); }
                        catch (IOException) { }
                    }
                }
            }

            progress?.Report($"Installed {coreName} successfully.");
            return true;
        }
        catch (HttpRequestException ex)
        {
            progress?.Report($"Download failed for {coreName}: {ex.Message}");
            return false;
        }
        catch (OperationCanceledException)
        {
            progress?.Report($"Download cancelled for {coreName}.");
            return false;
        }
        catch (IOException ex)
        {
            progress?.Report($"File error for {coreName}: {ex.Message}");
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            progress?.Report($"Access denied for {coreName}: {ex.Message}");
            return false;
        }
        catch (InvalidDataException ex)
        {
            progress?.Report($"Corrupt archive for {coreName}: {ex.Message}");
            return false;
        }
        finally
        {
            if (File.Exists(tempZipPath))
            {
                try { File.Delete(tempZipPath); }
                catch (IOException) { }
            }
        }
    }

    /// <summary>
    /// Downloads all missing cores.
    /// </summary>
    public static async Task<(int downloaded, int failed)> DownloadAllMissingCoresAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var cores = GetAvailableCores();
        var missing = cores.Where(c => !c.IsInstalled).ToList();

        if (missing.Count == 0)
        {
            progress?.Report("All cores are already installed.");
            return (0, 0);
        }

        progress?.Report($"Downloading {missing.Count} missing core(s)...");

        int downloaded = 0;
        int failed = 0;

        foreach (var core in missing)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool success = await DownloadCoreAsync(core.CoreName, progress, cancellationToken).ConfigureAwait(false);
            if (success)
                downloaded++;
            else
                failed++;
        }

        progress?.Report($"Done. Downloaded: {downloaded}, Failed: {failed}.");
        return (downloaded, failed);
    }

    /// <summary>
    /// Builds the buildbot download URL for a given core name.
    /// </summary>
    private static string GetDownloadUrl(string coreName)
    {
        string coreFileName = GetCoreFileName(coreName);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return $"https://buildbot.libretro.com/nightly/windows/x86_64/latest/{coreFileName}.zip";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            string arch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                ? "arm64"
                : "x86_64";
            return $"https://buildbot.libretro.com/nightly/apple/osx/{arch}/latest/{coreFileName}.zip";
        }

        // Linux — detect ARM64 / aarch64
        string linuxArch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64
            ? "aarch64"
            : "x86_64";
        return $"https://buildbot.libretro.com/nightly/linux/{linuxArch}/latest/{coreFileName}.zip";
    }

    /// <summary>
    /// Returns the expected filename for a core library on the current platform.
    /// </summary>
    private static string GetCoreFileName(string coreName)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return $"{coreName}_libretro.dll";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return $"{coreName}_libretro.dylib";

        return $"{coreName}_libretro.so";
    }
}
