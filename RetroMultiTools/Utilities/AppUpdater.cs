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
    private const string ReleasesApiUrl = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases?per_page=15";
    private const string UpdaterExeWindows = "RetroMultiTools.Updater.exe";
    private const string UpdaterExeUnix = "RetroMultiTools.Updater";

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            ConnectTimeout = TimeSpan.FromSeconds(30)
        };
        var client = new HttpClient(handler);
        client.DefaultRequestHeaders.Add("User-Agent", $"RetroMultiTools/{GetCurrentVersion()}");
        client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
        // Use Timeout.InfiniteTimeSpan because downloads are streamed with
        // ResponseHeadersRead — the per-connection ConnectTimeout above
        // limits the initial connect, while body reads use the cancellation
        // token passed by the caller. A fixed HttpClient.Timeout would abort
        // large downloads that take longer than the limit to transfer.
        client.Timeout = Timeout.InfiniteTimeSpan;
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
    /// Returns <c>true</c> when the application is running inside a sandboxed
    /// environment (e.g. Flatpak, Snap) where in-app updates should be disabled
    /// because the package manager handles updates instead.
    /// </summary>
    public static bool IsRunningInSandbox()
    {
        // Flatpak sets FLATPAK_ID for every sandboxed process
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FLATPAK_ID")))
            return true;

        // Snap sets SNAP when running inside a snap package
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SNAP")))
            return true;

        return false;
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

            // Clean up any remaining .bak files (including subdirectories)
            foreach (string bakFile in Directory.EnumerateFiles(appDir, "*.bak", SearchOption.AllDirectories))
            {
                try { File.Delete(bakFile); }
                catch { /* best-effort */ }
            }

            // Clean up any remaining .new files from incomplete updates
            foreach (string newFile in Directory.EnumerateFiles(appDir, "*.new", SearchOption.AllDirectories))
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
    /// Fetches the recent releases list and picks the one with the highest
    /// semantic version, instead of relying on the /releases/latest endpoint
    /// which sorts by commit created_at date and can miss releases tagged on
    /// older commits.
    /// Returns update information if available, or null if up to date.
    /// Throws on network errors so callers can display appropriate messages.
    /// </summary>
    public static async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        // Apply a reasonable timeout for the version-check API call.
        // The global HttpClient.Timeout is infinite to support large downloads,
        // so each non-streaming call needs its own deadline.
        using var checkCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        checkCts.CancelAfter(TimeSpan.FromSeconds(30));

        var releases = await _httpClient.GetFromJsonAsync<List<GitHubRelease>>(
            ReleasesApiUrl, checkCts.Token).ConfigureAwait(false);

        if (releases is null || releases.Count == 0)
            return null;

        string currentVersionStr = GetCurrentVersion();
        if (!Version.TryParse(currentVersionStr, out var currentVersion))
            return null;

        // Normalize current version to 3 components for consistent comparison
        currentVersion = NormalizeVersion(currentVersion);

        Trace.WriteLine($"[AppUpdater] Current version: {currentVersion}");

        // Find the release with the highest version, skipping drafts, pre-releases,
        // and tags that don't parse as a valid version.
        GitHubRelease? bestRelease = null;
        Version? bestVersion = null;

        foreach (var release in releases)
        {
            if (release.Draft == true || release.Prerelease == true)
                continue;

            if (string.IsNullOrEmpty(release.TagName))
                continue;

            string versionStr = release.TagName.TrimStart('v', 'V');
            if (!Version.TryParse(versionStr, out var version))
                continue;

            version = NormalizeVersion(version);

            if (bestVersion is null || version > bestVersion)
            {
                bestVersion = version;
                bestRelease = release;
            }
        }

        if (bestRelease is null || bestVersion is null)
        {
            Trace.WriteLine("[AppUpdater] No valid releases found.");
            return null;
        }

        Trace.WriteLine($"[AppUpdater] Best remote version: {bestVersion} (tag: {bestRelease.TagName})");

        if (bestVersion <= currentVersion)
        {
            Trace.WriteLine("[AppUpdater] Already up to date.");
            return null;
        }

        string remoteVersionStr = bestRelease.TagName!.TrimStart('v', 'V');

        // Find the platform-specific ZIP asset
        string? downloadUrl = null;
        string expectedAssetName = GetPlatformAssetName();
        Trace.WriteLine($"[AppUpdater] Looking for asset: '{expectedAssetName}' in release {bestRelease.TagName}");
        if (bestRelease.Assets != null && !string.IsNullOrEmpty(expectedAssetName))
        {
            var asset = bestRelease.Assets.FirstOrDefault(a =>
                string.Equals(a.Name, expectedAssetName, StringComparison.OrdinalIgnoreCase));
            downloadUrl = asset?.BrowserDownloadUrl;

            if (downloadUrl is null)
            {
                Trace.WriteLine($"[AppUpdater] Asset '{expectedAssetName}' not found. Available: {string.Join(", ", bestRelease.Assets.Select(a => a.Name))}");
            }
        }

        return new UpdateInfo
        {
            CurrentVersion = currentVersionStr,
            NewVersion = remoteVersionStr,
            ReleaseUrl = bestRelease.HtmlUrl ?? $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases/latest",
            ReleaseName = bestRelease.Name ?? $"v{remoteVersionStr}",
            ReleaseNotes = bestRelease.Body ?? string.Empty,
            PublishedAt = bestRelease.PublishedAt,
            DownloadUrl = downloadUrl
        };
    }

    /// <summary>
    /// Normalizes a Version to at least 3 components (Major.Minor.Build) so
    /// that 2-component tags like "v4.0" compare correctly with 3-component
    /// assembly versions like "4.0.0". Without this, Version(4,0) is less
    /// than Version(4,0,0) because undefined Build (-1) &lt; 0.
    /// Preserves the Revision component when present so that tags like
    /// "v4.1.0.1" are not silently truncated.
    /// </summary>
    private static Version NormalizeVersion(Version v)
    {
        int build = Math.Max(v.Build, 0);
        // Version stores undefined components as -1; only include Revision
        // when it was actually specified in the parsed version string.
        return v.Revision >= 0
            ? new Version(v.Major, v.Minor, build, v.Revision)
            : new Version(v.Major, v.Minor, build);
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
            int lastReportedPercent = -1;

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
                    // Avoid flooding the UI with identical progress values
                    if (percent != lastReportedPercent)
                    {
                        progress?.Report(percent);
                        lastReportedPercent = percent;
                    }
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
    /// On Linux, uses xdg-open for compatibility with Flatpak and other sandboxes.
    /// </summary>
    public static bool OpenReleasePage(string url)
    {
        try
        {
            ProcessStartInfo psi;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // xdg-open works inside Flatpak (delegates to the host via portals)
                psi = new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    UseShellExecute = false
                };
                psi.ArgumentList.Add(url);
            }
            else
            {
                psi = new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
            }

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
    /// All release builds are self-contained, so no deployment-type suffix is needed.
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

        return $"{os}-{arch}.zip";
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

        [JsonPropertyName("draft")]
        public bool? Draft { get; set; }

        [JsonPropertyName("prerelease")]
        public bool? Prerelease { get; set; }

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
