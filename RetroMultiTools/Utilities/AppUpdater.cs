using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RetroMultiTools.Utilities;

/// <summary>
/// Checks for application updates by querying the GitHub Releases API.
/// Downloads update ZIPs and hands off to the external updater process.
/// Uses a static HttpClient with proper connection pooling.
/// </summary>
public static class AppUpdater
{
    private static readonly HttpClient _httpClient = CreateHttpClient();

    private const string GitHubOwner = "SvenGDK";
    private const string GitHubRepo = "RetroMultiTools";
    private const string ReleasesApiUrl = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
    private const string UpdaterExeWindows = "RetroMultiTools.Updater.exe";
    private const string UpdaterExeUnix = "RetroMultiTools.Updater";

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(10)
        };
        var client = new HttpClient(handler);
        client.DefaultRequestHeaders.Add("User-Agent", $"RetroMultiTools/{GetCurrentVersion()}");
        client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
        client.Timeout = TimeSpan.FromMinutes(10);
        return client;
    }

    /// <summary>
    /// Gets the current application version from the assembly metadata.
    /// </summary>
    public static string GetCurrentVersion()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version;
        return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "0.0.0";
    }

    /// <summary>
    /// Cleans up leftover files from a previous update cycle.
    /// Call this once during application startup to remove .bak and .new
    /// files left behind by the updater process.
    /// </summary>
    public static void CleanupAfterUpdate()
    {
        try
        {
            string appDir = AppContext.BaseDirectory;

            // Swap in any .new updater files that the updater couldn't replace
            // while running. The updater defers its own exe, managed assembly,
            // dependency manifest, runtime config, and debug symbols as .new files.
            string updaterName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? UpdaterExeWindows
                : UpdaterExeUnix;
            string updaterBaseName = Path.GetFileNameWithoutExtension(updaterName);

            var updaterFiles = new[]
            {
                updaterName,
                $"{updaterBaseName}.dll",
                $"{updaterBaseName}.deps.json",
                $"{updaterBaseName}.runtimeconfig.json",
                $"{updaterBaseName}.pdb",
            };

            foreach (string fileName in updaterFiles)
            {
                string filePath = Path.Combine(appDir, fileName);
                string newFilePath = filePath + ".new";
                if (!File.Exists(newFilePath))
                    continue;

                try
                {
                    string bakPath = filePath + ".bak";
                    if (File.Exists(bakPath))
                        File.Delete(bakPath);

                    if (File.Exists(filePath))
                        File.Move(filePath, bakPath);

                    File.Move(newFilePath, filePath);

                    if (File.Exists(bakPath))
                        File.Delete(bakPath);
                }
                catch
                {
                    // Best-effort — will retry on next launch
                }
            }

            // Clean up any remaining .bak files
            foreach (string bakFile in Directory.EnumerateFiles(appDir, "*.bak"))
            {
                try { File.Delete(bakFile); }
                catch { /* best-effort */ }
            }

            // Clean up any remaining .new files from incomplete updates
            foreach (string newFile in Directory.EnumerateFiles(appDir, "*.new"))
            {
                try { File.Delete(newFile); }
                catch { /* best-effort */ }
            }

            // Clean up the update temp directory
            string tempDir = Path.Combine(Path.GetTempPath(), "RetroMultiTools-Update");
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); }
                catch { /* best-effort */ }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[AppUpdater] Cleanup after update failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks GitHub Releases for a newer version.
    /// Returns update information if available, or null if up to date.
    /// </summary>
    public static async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var release = await _httpClient.GetFromJsonAsync<GitHubRelease>(
                ReleasesApiUrl, cancellationToken).ConfigureAwait(false);

            if (release is null || string.IsNullOrEmpty(release.TagName))
                return null;

            string remoteVersionStr = release.TagName.TrimStart('v', 'V');
            if (!Version.TryParse(remoteVersionStr, out var remoteVersion))
                return null;

            string currentVersionStr = GetCurrentVersion();
            if (!Version.TryParse(currentVersionStr, out var currentVersion))
                return null;

            if (remoteVersion <= currentVersion)
                return null;

            // Find the platform-specific ZIP asset
            string? downloadUrl = null;
            string expectedAssetName = GetPlatformAssetName();
            Trace.WriteLine($"[AppUpdater] Looking for asset: '{expectedAssetName}' in release {release.TagName}");
            if (release.Assets != null && !string.IsNullOrEmpty(expectedAssetName))
            {
                var asset = release.Assets.FirstOrDefault(a =>
                    string.Equals(a.Name, expectedAssetName, StringComparison.OrdinalIgnoreCase));
                downloadUrl = asset?.BrowserDownloadUrl;

                if (downloadUrl is null)
                {
                    Trace.WriteLine($"[AppUpdater] Asset '{expectedAssetName}' not found. Available: {string.Join(", ", release.Assets.Select(a => a.Name))}");
                }
            }

            return new UpdateInfo
            {
                CurrentVersion = currentVersionStr,
                NewVersion = remoteVersionStr,
                ReleaseUrl = release.HtmlUrl ?? $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases/latest",
                ReleaseName = release.Name ?? $"v{remoteVersionStr}",
                ReleaseNotes = release.Body ?? string.Empty,
                PublishedAt = release.PublishedAt,
                DownloadUrl = downloadUrl
            };
        }
        catch (HttpRequestException ex)
        {
            Trace.WriteLine($"[AppUpdater] Network error checking for updates: {ex.Message}");
            return null;
        }
        catch (JsonException ex)
        {
            Trace.WriteLine($"[AppUpdater] Failed to parse update response: {ex.Message}");
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }

    private const int DownloadBufferSize = 81920;

    /// <summary>
    /// Downloads the update ZIP to a temporary directory and reports progress.
    /// Returns the path to the downloaded ZIP file.
    /// Cleans up partial downloads on failure and validates ZIP integrity.
    /// </summary>
    public static async Task<string> DownloadUpdateAsync(
        string downloadUrl,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(downloadUrl);

        string tempDir = Path.Combine(Path.GetTempPath(), "RetroMultiTools-Update");
        Directory.CreateDirectory(tempDir);

        string zipPath = Path.Combine(tempDir, "update.zip");

        // Delete any previous download
        if (File.Exists(zipPath))
            File.Delete(zipPath);

        try
        {
            using var response = await _httpClient.GetAsync(downloadUrl,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            long? totalBytes = response.Content.Headers.ContentLength;
            long downloadedBytes = 0;

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None,
                DownloadBufferSize, true);

            var buffer = new byte[DownloadBufferSize];
            int bytesRead;
            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                downloadedBytes += bytesRead;

                if (totalBytes.HasValue && totalBytes.Value > 0)
                {
                    int percent = (int)Math.Min(downloadedBytes * 100 / totalBytes.Value, 100);
                    progress?.Report(percent);
                }
            }
        }
        catch
        {
            // Clean up partial download on failure or cancellation
            try { if (File.Exists(zipPath)) File.Delete(zipPath); }
            catch { /* best-effort */ }
            throw;
        }

        // Validate the downloaded file is a valid ZIP archive
        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            _ = archive.Entries.Count;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            Trace.WriteLine($"[AppUpdater] Downloaded file is not a valid ZIP: {ex.Message}");
            try { File.Delete(zipPath); }
            catch { /* best-effort */ }
            throw new InvalidOperationException(
                "Downloaded update file is corrupted or not a valid ZIP archive.", ex);
        }

        progress?.Report(100);
        return zipPath;
    }

    /// <summary>
    /// Launches the external updater process to apply the update,
    /// then shuts down the current application.
    /// Returns true if the updater was launched successfully.
    /// </summary>
    public static bool LaunchUpdaterAndExit(string zipPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zipPath);

        string appDir = AppContext.BaseDirectory;
        string updaterName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? UpdaterExeWindows
            : UpdaterExeUnix;
        string updaterPath = Path.Combine(appDir, updaterName);

        if (!File.Exists(updaterPath))
        {
            Trace.WriteLine($"[AppUpdater] Updater not found at: {updaterPath}");
            return false;
        }

        string mainExeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "RetroMultiTools.exe"
            : "RetroMultiTools";

        int currentPid = Environment.ProcessId;

        // On Unix, ensure the updater is executable
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                using var chmod = Process.Start(new ProcessStartInfo
                {
                    FileName = "chmod",
                    ArgumentList = { "+x", updaterPath },
                    UseShellExecute = false
                });
                chmod?.WaitForExit(5000);
            }
            catch
            {
                // Best-effort
            }
        }

        var psi = new ProcessStartInfo
        {
            FileName = updaterPath,
            UseShellExecute = false,
            WorkingDirectory = appDir
        };
        psi.ArgumentList.Add("--pid");
        psi.ArgumentList.Add(currentPid.ToString());
        psi.ArgumentList.Add("--zip");
        psi.ArgumentList.Add(zipPath);
        psi.ArgumentList.Add("--target");
        psi.ArgumentList.Add(appDir);
        psi.ArgumentList.Add("--exe");
        psi.ArgumentList.Add(mainExeName);

        var proc = Process.Start(psi);
        if (proc is null)
        {
            Trace.WriteLine("[AppUpdater] Failed to start updater process.");
            return false;
        }

        // Dispose the handle — the updater process continues independently
        proc.Dispose();
        return true;
    }

    /// <summary>
    /// Opens the release page in the default browser.
    /// </summary>
    public static bool OpenReleasePage(string url)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };
            using var process = Process.Start(psi);
            return true;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[AppUpdater] Failed to open release page: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Returns the expected ZIP asset name for the current platform and architecture.
    /// Detects whether the current installation is self-contained and returns
    /// the appropriate asset variant.
    /// </summary>
    internal static string GetPlatformAssetName()
    {
        string os;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            os = "win";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            os = "linux";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            os = "osx";
        else
            return string.Empty;

        string arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(arch))
            return string.Empty;

        string suffix = IsSelfContainedDeployment() ? "-Selfcontained" : "";
        return $"{os}-{arch}{suffix}.zip";
    }

    /// <summary>
    /// Detects whether the current application is a self-contained deployment
    /// by checking for the presence of the native host library in the app directory.
    /// </summary>
    private static bool IsSelfContainedDeployment()
    {
        string appDir = AppContext.BaseDirectory;

        string hostFxr;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            hostFxr = "hostfxr.dll";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            hostFxr = "libhostfxr.so";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            hostFxr = "libhostfxr.dylib";
        else
            return false;

        return File.Exists(Path.Combine(appDir, hostFxr));
    }

    public sealed class UpdateInfo
    {
        public string CurrentVersion { get; set; } = "";
        public string NewVersion { get; set; } = "";
        public string ReleaseUrl { get; set; } = "";
        public string ReleaseName { get; set; } = "";
        public string ReleaseNotes { get; set; } = "";
        public DateTimeOffset? PublishedAt { get; set; }

        /// <summary>
        /// Direct download URL for the platform-specific ZIP asset.
        /// Null if no matching asset was found in the release.
        /// </summary>
        public string? DownloadUrl { get; set; }
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("published_at")]
        public DateTimeOffset? PublishedAt { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubAsset>? Assets { get; set; }
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }
    }
}
