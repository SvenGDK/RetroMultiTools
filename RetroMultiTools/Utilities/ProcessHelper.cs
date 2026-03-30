using System.Diagnostics;
using System.Text;

namespace RetroMultiTools.Utilities;

/// <summary>
/// Shared helper for running external processes, escaping shell strings,
/// and other operations used by multiple tools.
/// </summary>
internal static class ProcessHelper
{
    /// <summary>
    /// Runs a process with the given arguments, capturing combined stdout/stderr output.
    /// Uses ArgumentList (no shell interpretation) to avoid command injection.
    /// </summary>
    internal static async Task<(int ExitCode, string Output)> RunProcessAsync(
        string fileName, string[] arguments, CancellationToken ct = default)
    {
        using var proc = new Process();
        proc.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var arg in arguments)
            proc.StartInfo.ArgumentList.Add(arg);

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();
        var lockObj = new object();
        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) lock (lockObj) outputBuilder.AppendLine(e.Data);
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) lock (lockObj) errorBuilder.AppendLine(e.Data);
        };

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        try
        {
            await proc.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* best effort */ }
            throw;
        }

        string combinedOutput;
        lock (lockObj)
        {
            combinedOutput = outputBuilder.ToString();
            if (proc.ExitCode != 0 && errorBuilder.Length > 0)
                combinedOutput += "\n" + errorBuilder.ToString();
        }

        return (proc.ExitCode, combinedOutput);
    }

    /// <summary>
    /// Escapes a string for safe inclusion inside a PowerShell single-quoted string literal.
    /// In PowerShell, the only character that needs escaping inside single quotes is the
    /// single quote itself (doubled).
    /// </summary>
    internal static string EscapePowerShellString(string value)
    {
        return value.Replace("'", "''");
    }

    /// <summary>
    /// Recursively copies a directory tree to a new location.
    /// Supports cancellation and wraps individual file errors with the file name.
    /// </summary>
    internal static void CopyDirectoryRecursive(
        string source, string destination, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.GetFiles(source))
        {
            ct.ThrowIfCancellationRequested();
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(source))
            CopyDirectoryRecursive(dir, Path.Combine(destination, Path.GetFileName(dir)), ct);
    }

    /// <summary>
    /// Formats a byte count as a human-readable string (e.g. "1.50 GB").
    /// </summary>
    internal static string FormatSize(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        int idx = 0;
        double size = bytes;
        while (size >= 1024 && idx < suffixes.Length - 1)
        {
            size /= 1024;
            idx++;
        }
        return $"{size:F2} {suffixes[idx]}";
    }

    /// <summary>
    /// Validates that a device path looks like a legitimate block device.
    /// Returns true for paths like /dev/sdX, /dev/nvmeXnYpZ, /dev/diskN,
    /// /dev/srN, or Windows drive letters (D:) and disk numbers.
    /// Rejects directory traversal patterns and unusual characters.
    /// </summary>
    internal static bool IsValidDevicePath(string deviceId)
    {
        if (string.IsNullOrEmpty(deviceId))
            return false;

        // Windows drive letter (e.g. "D:")
        if (deviceId.Length == 2 && char.IsAsciiLetter(deviceId[0]) && deviceId[1] == ':')
            return true;

        // Windows disk number (e.g. "0", "1", "2")
        if (int.TryParse(deviceId, out int diskNum) && diskNum >= 0)
            return true;

        // Unix device paths must start with /dev/
        if (!deviceId.StartsWith("/dev/", StringComparison.Ordinal))
            return false;

        // Reject directory traversal patterns
        if (deviceId.Contains("..", StringComparison.Ordinal))
            return false;

        // Only allow alphanumeric characters after /dev/ (no slashes to prevent traversal)
        for (int i = 5; i < deviceId.Length; i++)
        {
            char c = deviceId[i];
            if (!char.IsAsciiLetterOrDigit(c))
                return false;
        }

        return deviceId.Length > 5;
    }
}
