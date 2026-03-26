using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace RetroMultiTools.Updater;

/// <summary>
/// External updater process for Retro Multi Tools.
/// Waits for the main application to exit, extracts the downloaded update ZIP
/// over the installation directory, then relaunches the main application.
/// </summary>
/// <remarks>
/// <c>Usage: RetroMultiTools.Updater --pid {id} --zip {path} --target {dir} --exe {name}</c>
/// </remarks>
internal static class Program
{
    private const int MaxWaitSeconds = 60;

    private static string? _logPath;

    private static int Main(string[] args)
    {
        InitializeLog();

        string? pidStr = null, zipPath = null, targetDir = null, exeName = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (i + 1 >= args.Length) break;

            switch (args[i])
            {
                case "--pid":
                    pidStr = args[i + 1];
                    i++;
                    break;
                case "--zip":
                    zipPath = args[i + 1];
                    i++;
                    break;
                case "--target":
                    targetDir = args[i + 1];
                    i++;
                    break;
                case "--exe":
                    exeName = args[i + 1];
                    i++;
                    break;
            }
        }

        if (string.IsNullOrEmpty(pidStr) || string.IsNullOrEmpty(zipPath) ||
            string.IsNullOrEmpty(targetDir) || string.IsNullOrEmpty(exeName))
        {
            Log("Usage: RetroMultiTools.Updater --pid <id> --zip <path> --target <dir> --exe <name>", error: true);
            return 1;
        }

        if (!int.TryParse(pidStr, out int pid))
        {
            Log($"Invalid PID: {pidStr}", error: true);
            return 1;
        }

        if (!File.Exists(zipPath))
        {
            Log($"Update ZIP not found: {zipPath}", error: true);
            return 1;
        }

        if (!Directory.Exists(targetDir))
        {
            Log($"Target directory not found: {targetDir}", error: true);
            return 1;
        }

        try
        {
            Log("Retro Multi Tools Updater");
            Log("========================");
            Log($"  PID:    {pid}");
            Log($"  ZIP:    {zipPath}");
            Log($"  Target: {targetDir}");
            Log($"  Exe:    {exeName}");

            // Step 1: Wait for the main application to exit
            Log($"Waiting for process {pid} to exit... ", newLine: false);
            WaitForProcessExit(pid);
            Log("done.");

            // Step 2: Extract the update ZIP
            Log("Applying update... ", newLine: false);
            ExtractUpdate(zipPath, targetDir);
            Log("done.");

            // Step 3: Clean up the downloaded ZIP
            TryDeleteFile(zipPath);

            // Step 4: Relaunch the main application
            string exePath = Path.Combine(targetDir, exeName);
            Log($"Launching {exeName}...");
            LaunchApplication(exePath);

            // Step 5: Clean up the temp directory (after launching so all log entries are captured)
            string? tempDir = Path.GetDirectoryName(zipPath);
            if (tempDir != null)
            {
                if (_logPath != null)
                {
                    TryDeleteFile(_logPath);
                    _logPath = null;
                }
                TryDeleteDirectory(tempDir);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Log($"Update failed: {ex.Message}", error: true);
            Log("You can re-download the application from:", error: true);
            Log("https://github.com/SvenGDK/RetroMultiTools/releases/latest", error: true);
            Log("", error: true);
            Log("Press any key to exit...", error: true);
            try { Console.ReadKey(intercept: true); } catch { /* non-interactive */ }
            return 1;
        }
    }

    private static void InitializeLog()
    {
        try
        {
            string logDir = Path.Combine(Path.GetTempPath(), "RetroMultiTools-Update");
            Directory.CreateDirectory(logDir);
            _logPath = Path.Combine(logDir, "updater.log");

            // Start a fresh log for each update cycle
            File.WriteAllText(_logPath, string.Empty);
        }
        catch
        {
            // Best-effort: logging is non-critical
        }
    }

    private static void Log(string message, bool error = false, bool newLine = true)
    {
        if (newLine)
        {
            if (error) Console.Error.WriteLine(message); else Console.WriteLine(message);
        }
        else
        {
            if (error) Console.Error.Write(message); else Console.Write(message);
        }

        // Append to log file for debugging failed updates (especially on Windows
        // where the console window may not be visible)
        if (_logPath != null)
        {
            try
            {
                string timestamp = DateTime.UtcNow.ToString("HH:mm:ss.fff");
                string prefix = error ? "[ERR]" : "[INF]";
                File.AppendAllText(_logPath, $"[{timestamp}] {prefix} {message}{(newLine ? Environment.NewLine : "")}");
            }
            catch
            {
                // Best-effort
            }
        }
    }

    private static void WaitForProcessExit(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            if (!process.WaitForExit(MaxWaitSeconds * 1000))
            {
                Log($"Warning: Process {pid} did not exit within {MaxWaitSeconds}s, proceeding anyway.");
            }
        }
        catch (ArgumentException)
        {
            // Process already exited
        }
        catch (InvalidOperationException)
        {
            // Process has no associated system process
        }
    }

    private static void ExtractUpdate(string zipPath, string targetDir)
    {
        // Determine the updater's own file name so we can skip replacing ourselves
        string ownFileName = Path.GetFileName(Environment.ProcessPath
            ?? (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "RetroMultiTools.Updater.exe"
                : "RetroMultiTools.Updater"));

        // Build a set of all files belonging to the running updater process.
        // The managed assembly (.dll), dependency manifest, runtime config, and
        // debug symbols are all locked while the updater runs and cannot be
        // overwritten in place (especially on Windows).
        string ownBaseName = Path.GetFileNameWithoutExtension(ownFileName);
        var ownFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ownFileName,
            $"{ownBaseName}.dll",
            $"{ownBaseName}.deps.json",
            $"{ownBaseName}.runtimeconfig.json",
            $"{ownBaseName}.pdb",
        };

        string normalizedTargetDir = Path.GetFullPath(targetDir);
        if (!normalizedTargetDir.EndsWith(Path.DirectorySeparatorChar))
            normalizedTargetDir += Path.DirectorySeparatorChar;

        using var archive = ZipFile.OpenRead(zipPath);
        int totalEntries = archive.Entries.Count(e => !string.IsNullOrEmpty(e.Name));
        int extractedCount = 0;
        int deferredCount = 0;

        foreach (var entry in archive.Entries)
        {
            // Skip directories
            if (string.IsNullOrEmpty(entry.Name))
                continue;

            string destPath = Path.GetFullPath(Path.Combine(targetDir, entry.FullName));

            // Security: ensure we don't write outside the target directory
            if (!destPath.StartsWith(normalizedTargetDir, StringComparison.OrdinalIgnoreCase))
            {
                Log($"  Skipping entry outside target: {entry.FullName}");
                continue;
            }

            // Skip replacing files that belong to the running updater process;
            // extract them as .new files to be swapped by the main app on next launch.
            if (ownFiles.Contains(entry.Name))
            {
                TryExtractAs(entry, destPath + ".new");
                deferredCount++;
                continue;
            }

            // Ensure the directory exists
            string? dir = Path.GetDirectoryName(destPath);
            if (dir != null)
                Directory.CreateDirectory(dir);

            // Extract, overwriting existing files
            RetryFileOperation(() => entry.ExtractToFile(destPath, overwrite: true));
            extractedCount++;
        }

        Log($"Extracted {extractedCount}/{totalEntries} files" +
            (deferredCount > 0 ? $" ({deferredCount} deferred for next launch)." : "."));

        // Try to swap all .new updater files into place now.
        // On Windows this typically fails for locked files; the main app
        // will finalize the swap via CleanupAfterUpdate() on next launch.
        foreach (string ownFile in ownFiles)
        {
            string filePath = Path.Combine(targetDir, ownFile);
            string newPath = filePath + ".new";
            if (!File.Exists(newPath))
                continue;

            try
            {
                string bakPath = filePath + ".bak";
                TryDeleteFile(bakPath);

                if (File.Exists(filePath))
                    File.Move(filePath, bakPath);

                File.Move(newPath, filePath);
                TryDeleteFile(bakPath);
            }
            catch
            {
                // Not critical — files will be replaced on next update cycle.
                // On Windows, the running executable/DLL cannot be renamed/deleted;
                // the .new files remain and are swapped by the main app on next launch.
            }
        }
    }

    private static void TryExtractAs(ZipArchiveEntry entry, string destPath)
    {
        try
        {
            string? dir = Path.GetDirectoryName(destPath);
            if (dir != null)
                Directory.CreateDirectory(dir);
            entry.ExtractToFile(destPath, overwrite: true);
        }
        catch
        {
            // Best-effort
        }
    }

    /// <summary>
    /// Retries a file operation a few times to handle transient locks
    /// (e.g., antivirus scanning files right after the main app exits).
    /// </summary>
    private static void RetryFileOperation(Action action, int maxRetries = 5)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                action();
                return;
            }
            catch (IOException) when (i < maxRetries - 1)
            {
                Thread.Sleep(500 * (i + 1));
            }
            catch (UnauthorizedAccessException) when (i < maxRetries - 1)
            {
                Thread.Sleep(500 * (i + 1));
            }
        }
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* best-effort cleanup */ }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path) && Directory.GetFileSystemEntries(path).Length == 0)
                Directory.Delete(path, recursive: false);
        }
        catch { /* best-effort cleanup */ }
    }

    private static void LaunchApplication(string exePath)
    {
        if (!File.Exists(exePath))
        {
            Log($"Warning: Executable not found at {exePath}");
            return;
        }

        // On Unix, ensure the file is executable
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                using var chmod = Process.Start(new ProcessStartInfo
                {
                    FileName = "chmod",
                    ArgumentList = { "+x", exePath },
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
            FileName = exePath,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(exePath) ?? "."
        };
        using var process = Process.Start(psi);
    }
}
