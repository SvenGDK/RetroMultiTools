using RetroMultiTools.Services;

namespace RetroMultiTools.Utilities.RetroArch;

/// <summary>
/// Downloads and manages the SDL2 community game controller mapping database
/// (<c>gamecontrollerdb.txt</c>) for automatic controller recognition.
/// The database is sourced from the official SDL community repository and
/// provides RetroArch-compatible autoconfig support.
/// </summary>
public static class ControllerProfileDownloader
{
    /// <summary>
    /// URL for the community-maintained SDL game controller database.
    /// This is the same database used by SDL2, Steam, and RetroArch.
    /// </summary>
    private const string GameControllerDbUrl =
        "https://raw.githubusercontent.com/gabomdq/SDL_GameControllerDB/master/gamecontrollerdb.txt";

    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30),
        DefaultRequestHeaders =
        {
            { "User-Agent", "RetroMultiTools/1.0" }
        }
    };

    /// <summary>
    /// Minimum valid file size for gamecontrollerdb.txt in bytes.
    /// A legitimate database file is always much larger; this guards against
    /// empty or truncated downloads.
    /// </summary>
    private const long MinimumValidFileSizeBytes = 100;

    /// <summary>
    /// Downloads the latest <c>gamecontrollerdb.txt</c> from the SDL community
    /// repository and places it next to the application executable.
    /// If RetroArch is configured, also copies it to the RetroArch autoconfig directory.
    /// </summary>
    /// <returns>The number of destinations written (0 on failure, 1–2 on success).</returns>
    public static async Task<DownloadResult> DownloadProfilesAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"gamecontrollerdb_{Guid.NewGuid():N}.txt");

        try
        {
            // ── Download ────────────────────────────────────────────
            progress?.Report("Downloading controller profiles…");

            using (var response = await _httpClient.GetAsync(
                GameControllerDbUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();

                await using var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
                await fs.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            // Validate the download isn't empty / corrupt
            var fileInfo = new FileInfo(tempPath);
            if (fileInfo.Length < MinimumValidFileSizeBytes)
            {
                progress?.Report("Downloaded file appears empty or corrupt.");
                return new DownloadResult(false, 0, 0);
            }

            int mappingCount = CountMappings(tempPath);
            int destinations = 0;

            // ── Copy to app directory ───────────────────────────────
            string? exeDir = Path.GetDirectoryName(
                Environment.ProcessPath ?? AppContext.BaseDirectory);

            if (exeDir != null)
            {
                string appDbPath = Path.Combine(exeDir, "gamecontrollerdb.txt");
                try
                {
                    File.Copy(tempPath, appDbPath, overwrite: true);
                    destinations++;
                    progress?.Report($"Saved to application directory ({mappingCount} profiles).");
                }
                catch (IOException ex)
                {
                    System.Diagnostics.Trace.WriteLine(
                        $"[ControllerProfileDownloader] Could not write to app dir: {ex.Message}");
                }
                catch (UnauthorizedAccessException ex)
                {
                    System.Diagnostics.Trace.WriteLine(
                        $"[ControllerProfileDownloader] Access denied (app dir): {ex.Message}");
                }
            }

            // ── Copy to RetroArch autoconfig directory ──────────────
            string retroArchPath = AppSettings.Instance.RetroArchPath;
            if (!string.IsNullOrEmpty(retroArchPath))
            {
                string? raDir = Path.GetDirectoryName(
                    RetroArchLauncher.ResolveRetroArchPath(retroArchPath));
                if (raDir != null)
                {
                    string autoconfigDir = Path.Combine(raDir, "autoconfig");
                    try
                    {
                        Directory.CreateDirectory(autoconfigDir);
                        string raDbPath = Path.Combine(autoconfigDir, "gamecontrollerdb.txt");
                        File.Copy(tempPath, raDbPath, overwrite: true);
                        destinations++;
                        progress?.Report($"Also saved to RetroArch autoconfig directory.");
                    }
                    catch (IOException ex)
                    {
                        System.Diagnostics.Trace.WriteLine(
                            $"[ControllerProfileDownloader] Could not write to RA dir: {ex.Message}");
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        System.Diagnostics.Trace.WriteLine(
                            $"[ControllerProfileDownloader] Access denied (RA dir): {ex.Message}");
                    }
                }
            }

            if (destinations == 0)
            {
                progress?.Report("Downloaded but could not save to any directory.");
                return new DownloadResult(false, mappingCount, 0);
            }

            return new DownloadResult(true, mappingCount, destinations);
        }
        catch (HttpRequestException ex)
        {
            progress?.Report($"Download failed: {ex.Message}");
            return new DownloadResult(false, 0, 0);
        }
        catch (OperationCanceledException)
        {
            progress?.Report("Download cancelled.");
            return new DownloadResult(false, 0, 0);
        }
        catch (IOException ex)
        {
            progress?.Report($"File error: {ex.Message}");
            return new DownloadResult(false, 0, 0);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); }
                catch (IOException) { }
            }
        }
    }

    /// <summary>
    /// Returns information about the currently installed controller profiles.
    /// </summary>
    public static InstalledInfo GetInstalledInfo()
    {
        string? exeDir = Path.GetDirectoryName(
            Environment.ProcessPath ?? AppContext.BaseDirectory);

        string? appDbPath = exeDir != null
            ? Path.Combine(exeDir, "gamecontrollerdb.txt")
            : null;

        bool exists = appDbPath != null && File.Exists(appDbPath);
        int count = 0;
        DateTime? lastModified = null;

        if (exists && appDbPath != null)
        {
            count = CountMappings(appDbPath);
            lastModified = File.GetLastWriteTimeUtc(appDbPath);
        }

        return new InstalledInfo(exists, count, lastModified);
    }

    /// <summary>
    /// Counts the number of controller mapping lines in a gamecontrollerdb.txt file.
    /// A mapping line is a non-empty, non-comment line.
    /// </summary>
    private static int CountMappings(string filePath)
    {
        try
        {
            int count = 0;
            foreach (string line in File.ReadLines(filePath))
            {
                string trimmed = line.TrimStart();
                if (trimmed.Length > 0 && !trimmed.StartsWith('#'))
                    count++;
            }
            return count;
        }
        catch (IOException)
        {
            return 0;
        }
    }

    /// <summary>Result of a controller profile download operation.</summary>
    public sealed record DownloadResult(bool Success, int MappingCount, int DestinationsWritten);

    /// <summary>Information about currently installed controller profiles.</summary>
    public sealed record InstalledInfo(bool Exists, int MappingCount, DateTime? LastModifiedUtc);
}
