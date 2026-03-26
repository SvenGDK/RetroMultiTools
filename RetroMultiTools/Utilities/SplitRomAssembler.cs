using System.Text.RegularExpressions;

namespace RetroMultiTools.Utilities;

/// <summary>
/// Reassembles split ROM files (e.g. .001, .002 or .part1, .part2) into a single file.
/// </summary>
public static class SplitRomAssembler
{
    private const int BufferSize = 81920;

    private static readonly Regex NumericSuffix = new(@"\.\d{3}$", RegexOptions.Compiled);
    private static readonly Regex PartSuffix = new(@"\.part\d+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ZipSplitSuffix = new(@"\.z\d{2}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Detects split ROM parts based on the first part file.
    /// Returns all parts in order, or an empty list if no split pattern is detected.
    /// </summary>
    public static List<string> DetectParts(string firstPartPath)
    {
        if (!File.Exists(firstPartPath))
            throw new FileNotFoundException("File not found.", firstPartPath);

        string dir = Path.GetDirectoryName(firstPartPath) ?? "";
        string fileName = Path.GetFileName(firstPartPath);

        // Detect which pattern this file matches
        if (NumericSuffix.IsMatch(fileName))
            return FindNumericParts(dir, fileName);

        if (PartSuffix.IsMatch(fileName))
            return FindPartParts(dir, fileName);

        if (ZipSplitSuffix.IsMatch(fileName))
            return FindZipSplitParts(dir, fileName);

        return [];
    }

    /// <summary>
    /// Reassembles split ROM parts into a single output file.
    /// </summary>
    public static async Task<AssemblyResult> AssembleAsync(
        string firstPartPath,
        string outputPath,
        IProgress<string>? progress = null)
    {
        var parts = DetectParts(firstPartPath);
        if (parts.Count == 0)
            throw new InvalidOperationException("No split ROM parts detected. The file does not match a known split pattern (.001/.002, .part1/.part2, .z01/.z02).");

        if (parts.Count == 1)
            throw new InvalidOperationException("Only one part found. Nothing to reassemble.");

        // Prevent data corruption if output path matches an input part
        string fullOutput = Path.GetFullPath(outputPath);
        foreach (var part in parts)
        {
            if (string.Equals(Path.GetFullPath(part), fullOutput, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Output path cannot be one of the input part files.");
        }

        progress?.Report($"Found {parts.Count} parts to reassemble...");

        string? outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            throw new DirectoryNotFoundException($"Output directory does not exist: {outputDir}");

        long totalBytes = 0;

        await Task.Run(() =>
        {
            try
            {
                using var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize);
                byte[] buffer = new byte[BufferSize];

                for (int i = 0; i < parts.Count; i++)
                {
                    string part = parts[i];
                    progress?.Report($"Writing part {i + 1} of {parts.Count}: {Path.GetFileName(part)}");

                    using var input = new FileStream(part, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
                    int bytesRead;
                    while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        output.Write(buffer, 0, bytesRead);
                        totalBytes += bytesRead;
                    }
                }
            }
            catch
            {
                try { File.Delete(outputPath); } catch { /* best-effort cleanup */ }
                throw;
            }
        }).ConfigureAwait(false);

        progress?.Report("Done.");

        return new AssemblyResult
        {
            PartsCount = parts.Count,
            TotalSize = totalBytes,
            OutputPath = outputPath
        };
    }

    private static List<string> FindNumericParts(string dir, string fileName)
    {
        // e.g. "game.bin.001" -> base = "game.bin"
        string baseName = fileName[..^4]; // remove ".001" etc.
        var parts = new List<string>();

        for (int i = 1; i <= 999; i++)
        {
            string partPath = Path.Combine(dir, $"{baseName}.{i:D3}");
            if (File.Exists(partPath))
                parts.Add(partPath);
            else
                break;
        }

        return parts;
    }

    private static List<string> FindPartParts(string dir, string fileName)
    {
        // e.g. "game.bin.part1" -> base = "game.bin"
        int lastDot = fileName.LastIndexOf(".part", StringComparison.OrdinalIgnoreCase);
        if (lastDot < 0) return [];

        string baseName = fileName[..lastDot];
        var lookup = BuildCaseInsensitiveLookup(dir);
        var parts = new List<string>();

        for (int i = 1; i <= 999; i++)
        {
            string partName = $"{baseName}.part{i}";
            if (lookup.TryGetValue(partName, out string? actualName))
                parts.Add(Path.Combine(dir, actualName));
            else
                break;
        }

        return parts;
    }

    private static List<string> FindZipSplitParts(string dir, string fileName)
    {
        // e.g. "game.z01" -> base = "game"
        string baseName = fileName[..^4]; // remove ".z01" etc.
        var lookup = BuildCaseInsensitiveLookup(dir);
        var parts = new List<string>();

        for (int i = 1; i <= 99; i++)
        {
            string partName = $"{baseName}.z{i:D2}";
            if (lookup.TryGetValue(partName, out string? actualName))
                parts.Add(Path.Combine(dir, actualName));
            else
                break;
        }

        // Also check for the final .zip part
        string zipName = $"{baseName}.zip";
        if (lookup.TryGetValue(zipName, out string? actualZipName))
            parts.Add(Path.Combine(dir, actualZipName));

        return parts;
    }

    /// <summary>
    /// Builds a case-insensitive lookup of file names in the given directory
    /// to support case-sensitive file systems (e.g. Linux ext4).
    /// </summary>
    private static Dictionary<string, string> BuildCaseInsensitiveLookup(string dir)
    {
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string filePath in Directory.EnumerateFiles(dir))
        {
            string name = Path.GetFileName(filePath);
            lookup.TryAdd(name, name);
        }
        return lookup;
    }
}

public class AssemblyResult
{
    public int PartsCount { get; set; }
    public long TotalSize { get; set; }
    public string OutputPath { get; set; } = string.Empty;

    public string Summary =>
        $"{PartsCount} parts reassembled into {FileUtils.FormatFileSize(TotalSize)}";
}
