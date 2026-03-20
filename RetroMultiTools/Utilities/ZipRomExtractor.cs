using System.IO.Compression;

namespace RetroMultiTools.Utilities;

/// <summary>
/// Extracts ROM files from ZIP archives.
/// </summary>
public static class ZipRomExtractor
{
    private static readonly HashSet<string> RomExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".nes", ".smc", ".sfc", ".z64", ".n64", ".v64",
        ".gb", ".gbc", ".gba", ".vb", ".vboy",
        ".sms", ".md", ".gen",
        ".bin", ".iso", ".cue", ".32x", ".gg",
        ".a26", ".a52", ".a78", ".j64", ".jag",
        ".lnx", ".lyx",
        ".pce", ".tg16",
        ".ngp", ".ngc",
        ".col", ".cv", ".int",
        ".mx1", ".mx2",
        ".dsk", ".cdt", ".sna",
        ".tap",
        ".mo5", ".k7", ".fd",
        ".sv", ".ccc",
        ".3do", ".cdi", ".gdi",
        ".chd", ".rvz", ".gcm",
        ".atr", ".xex", ".car", ".cas",
        ".d88", ".t88",
        ".ndd", ".nds", ".3ds", ".cia",
        ".neo",
        ".chf",
        ".tgc",
        ".mtx", ".run"
    };

    /// <summary>
    /// Lists ROM files contained inside a ZIP archive.
    /// </summary>
    public static List<ZipRomEntry> ListRoms(string zipPath)
    {
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("ZIP file not found.", zipPath);

        var entries = new List<ZipRomEntry>();

        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            string ext = Path.GetExtension(entry.FullName);
            if (RomExtensions.Contains(ext))
            {
                entries.Add(new ZipRomEntry
                {
                    FileName = entry.FullName,
                    CompressedSize = entry.CompressedLength,
                    UncompressedSize = entry.Length
                });
            }
        }

        return entries;
    }

    /// <summary>
    /// Extracts all ROM files from a ZIP archive to an output directory.
    /// </summary>
    public static async Task<ZipExtractionResult> ExtractAsync(
        string zipPath,
        string outputDirectory,
        IProgress<string>? progress = null)
    {
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("ZIP file not found.", zipPath);

        Directory.CreateDirectory(outputDirectory);

        progress?.Report($"Opening {Path.GetFileName(zipPath)}...");

        int extracted = 0;
        int skipped = 0;
        long totalBytes = 0;

        await Task.Run(() =>
        {
            using var archive = ZipFile.OpenRead(zipPath);
            var romEntries = archive.Entries
                .Where(e => RomExtensions.Contains(Path.GetExtension(e.FullName)))
                .ToList();

            if (romEntries.Count == 0)
            {
                progress?.Report("No ROM files found in archive.");
                return;
            }

            for (int i = 0; i < romEntries.Count; i++)
            {
                var entry = romEntries[i];
                progress?.Report($"Extracting {i + 1} of {romEntries.Count}: {entry.FullName}");

                string entryFileName = Path.GetFileName(entry.FullName);
                if (string.IsNullOrEmpty(entryFileName))
                {
                    skipped++;
                    continue;
                }

                string outputPath = Path.Combine(outputDirectory, entryFileName);

                // Ensure the resolved path is within the output directory (zip slip protection)
                string fullOutputPath = Path.GetFullPath(outputPath);
                string fullOutputDir = Path.GetFullPath(outputDirectory);
                if (!fullOutputDir.EndsWith(Path.DirectorySeparatorChar))
                    fullOutputDir += Path.DirectorySeparatorChar;
                if (!fullOutputPath.StartsWith(fullOutputDir, StringComparison.Ordinal))
                {
                    skipped++;
                    continue;
                }

                try
                {
                    entry.ExtractToFile(outputPath, overwrite: true);
                    extracted++;
                    totalBytes += entry.Length;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    skipped++;
                }
            }
        }).ConfigureAwait(false);

        progress?.Report("Done.");

        return new ZipExtractionResult
        {
            Extracted = extracted,
            Skipped = skipped,
            TotalBytes = totalBytes
        };
    }

    /// <summary>
    /// Extracts ROM files from all ZIP archives in a directory.
    /// </summary>
    public static async Task<ZipExtractionResult> ExtractBatchAsync(
        string inputDirectory,
        string outputDirectory,
        IProgress<string>? progress = null)
    {
        if (!Directory.Exists(inputDirectory))
            throw new DirectoryNotFoundException($"Directory not found: {inputDirectory}");

        Directory.CreateDirectory(outputDirectory);

        var zipFiles = Directory.EnumerateFiles(inputDirectory, "*.zip", SearchOption.AllDirectories).ToList();

        if (zipFiles.Count == 0)
        {
            progress?.Report("No ZIP files found.");
            return new ZipExtractionResult();
        }

        int totalExtracted = 0;
        int totalSkipped = 0;
        long totalBytes = 0;

        for (int i = 0; i < zipFiles.Count; i++)
        {
            progress?.Report($"Processing ZIP {i + 1} of {zipFiles.Count}: {Path.GetFileName(zipFiles[i])}");

            try
            {
                var result = await ExtractAsync(zipFiles[i], outputDirectory, null).ConfigureAwait(false);
                totalExtracted += result.Extracted;
                totalSkipped += result.Skipped;
                totalBytes += result.TotalBytes;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException)
            {
                totalSkipped++;
            }
        }

        progress?.Report($"Done — {totalExtracted} ROMs extracted from {zipFiles.Count} ZIP files.");

        return new ZipExtractionResult
        {
            Extracted = totalExtracted,
            Skipped = totalSkipped,
            TotalBytes = totalBytes
        };
    }
}

public class ZipRomEntry
{
    public string FileName { get; set; } = string.Empty;
    public long CompressedSize { get; set; }
    public long UncompressedSize { get; set; }

    public string Summary =>
        $"{FileName} — {FileUtils.FormatFileSize(UncompressedSize)} ({FileUtils.FormatFileSize(CompressedSize)} compressed)";
}

public class ZipExtractionResult
{
    public int Extracted { get; set; }
    public int Skipped { get; set; }
    public long TotalBytes { get; set; }

    public string Summary =>
        $"{Extracted} ROM(s) extracted ({FileUtils.FormatFileSize(TotalBytes)}), {Skipped} skipped";
}
