using System.IO.Compression;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace RetroMultiTools.Utilities;

/// <summary>
/// Unified archive manager for extracting ROMs from and creating
/// ZIP, RAR, and 7z archives.
/// </summary>
public static class ArchiveManager
{
    /// <summary>
    /// ROM file extensions recognised for extraction filtering.
    /// Shared across all archive format handlers.
    /// </summary>
    internal static readonly HashSet<string> RomExtensions = new(StringComparer.OrdinalIgnoreCase)
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
    /// All archive extensions the manager can extract from.
    /// </summary>
    private static readonly HashSet<string> ExtractableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".rar", ".7z", ".gz"
    };

    /// <summary>
    /// Maximum allowed decompressed size (2 GB) to prevent decompression bomb attacks.
    /// </summary>
    private const long MaxDecompressedSize = 2L * 1024 * 1024 * 1024;

    private const int GzipBufferSize = 81920;

    /// <summary>
    /// Archive formats that can be created.
    /// RAR is proprietary and 7z writing is not supported by SharpCompress.
    /// Kept as a list so additional formats can be added if library support improves.
    /// </summary>
    public static readonly IReadOnlyList<ArchiveFormat> CreatableFormats =
    [
        new("ZIP", ".zip")
    ];

    /// <summary>Returns true when the file has a supported archive extension.</summary>
    public static bool IsSupported(string filePath)
        => ExtractableExtensions.Contains(Path.GetExtension(filePath));

    // ── Listing ─────────────────────────────────────────────────────────

    /// <summary>
    /// Lists ROM files contained inside any supported archive.
    /// </summary>
    public static List<ArchiveEntry> ListEntries(string archivePath)
    {
        if (!File.Exists(archivePath))
            throw new FileNotFoundException("Archive file not found.", archivePath);

        string ext = Path.GetExtension(archivePath);

        if (string.Equals(ext, ".gz", StringComparison.OrdinalIgnoreCase))
            return ListEntriesGzip(archivePath);

        return string.Equals(ext, ".zip", StringComparison.OrdinalIgnoreCase)
            ? ListEntriesZip(archivePath)
            : ListEntriesSharpCompress(archivePath);
    }

    private static List<ArchiveEntry> ListEntriesGzip(string gzipPath)
    {
        var info = new FileInfo(gzipPath);
        string decompressedName = Path.GetFileNameWithoutExtension(gzipPath);
        if (string.IsNullOrEmpty(decompressedName))
            decompressedName = "decompressed_rom";

        return
        [
            new ArchiveEntry
            {
                FileName = decompressedName,
                CompressedSize = info.Length,
                UncompressedSize = -1  // GZip does not store the full uncompressed size in the header.
            }
        ];
    }

    private static List<ArchiveEntry> ListEntriesZip(string zipPath)
    {
        var entries = new List<ArchiveEntry>();
        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue;   // directory entry
            string ext = Path.GetExtension(entry.FullName);
            if (RomExtensions.Contains(ext))
            {
                entries.Add(new ArchiveEntry
                {
                    FileName = entry.FullName,
                    CompressedSize = entry.CompressedLength,
                    UncompressedSize = entry.Length
                });
            }
        }
        return entries;
    }

    private static List<ArchiveEntry> ListEntriesSharpCompress(string archivePath)
    {
        var entries = new List<ArchiveEntry>();
        using var archive = ArchiveFactory.Open(archivePath);
        foreach (var entry in archive.Entries)
        {
            if (entry.IsDirectory) continue;
            string key = entry.Key ?? "";
            string ext = Path.GetExtension(key);
            if (RomExtensions.Contains(ext))
            {
                entries.Add(new ArchiveEntry
                {
                    FileName = key,
                    CompressedSize = entry.CompressedSize,
                    UncompressedSize = entry.Size
                });
            }
        }
        return entries;
    }

    // ── Extraction ──────────────────────────────────────────────────────

    /// <summary>
    /// Extracts all ROM files from any supported archive to an output directory.
    /// </summary>
    public static async Task<ArchiveOperationResult> ExtractAsync(
        string archivePath,
        string outputDirectory,
        IProgress<string>? progress = null)
    {
        if (!File.Exists(archivePath))
            throw new FileNotFoundException("Archive file not found.", archivePath);

        Directory.CreateDirectory(outputDirectory);

        string ext = Path.GetExtension(archivePath);

        if (string.Equals(ext, ".gz", StringComparison.OrdinalIgnoreCase))
            return await ExtractGzipAsync(archivePath, outputDirectory, progress).ConfigureAwait(false);

        return string.Equals(ext, ".zip", StringComparison.OrdinalIgnoreCase)
            ? await ExtractZipAsync(archivePath, outputDirectory, progress).ConfigureAwait(false)
            : await ExtractSharpCompressAsync(archivePath, outputDirectory, progress).ConfigureAwait(false);
    }

    /// <summary>
    /// Extracts ROMs from all supported archives found in a directory.
    /// </summary>
    public static async Task<ArchiveOperationResult> ExtractBatchAsync(
        string inputDirectory,
        string outputDirectory,
        IProgress<string>? progress = null)
    {
        if (!Directory.Exists(inputDirectory))
            throw new DirectoryNotFoundException($"Directory not found: {inputDirectory}");

        Directory.CreateDirectory(outputDirectory);

        var archiveFiles = Directory.EnumerateFiles(inputDirectory, "*", SearchOption.AllDirectories)
            .Where(f => ExtractableExtensions.Contains(Path.GetExtension(f)))
            .ToList();

        if (archiveFiles.Count == 0)
        {
            progress?.Report("No archive files found.");
            return new ArchiveOperationResult();
        }

        int totalExtracted = 0;
        int totalSkipped = 0;
        long totalBytes = 0;

        for (int i = 0; i < archiveFiles.Count; i++)
        {
            progress?.Report($"Processing archive {i + 1} of {archiveFiles.Count}: {Path.GetFileName(archiveFiles[i])}");

            try
            {
                var result = await ExtractAsync(archiveFiles[i], outputDirectory, null).ConfigureAwait(false);
                totalExtracted += result.Processed;
                totalSkipped += result.Skipped;
                totalBytes += result.TotalBytes;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException)
            {
                totalSkipped++;
            }
        }

        progress?.Report($"Done — {totalExtracted} ROMs extracted from {archiveFiles.Count} archive(s).");

        return new ArchiveOperationResult
        {
            Processed = totalExtracted,
            Skipped = totalSkipped,
            TotalBytes = totalBytes
        };
    }

    private static async Task<ArchiveOperationResult> ExtractZipAsync(
        string zipPath, string outputDirectory, IProgress<string>? progress)
    {
        progress?.Report($"Opening {Path.GetFileName(zipPath)}...");

        int extracted = 0;
        int skipped = 0;
        long totalBytes = 0;

        await Task.Run(() =>
        {
            using var archive = ZipFile.OpenRead(zipPath);
            var romEntries = archive.Entries
                .Where(e => !string.IsNullOrEmpty(e.Name) && RomExtensions.Contains(Path.GetExtension(e.FullName)))
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
                if (!IsPathSafe(outputPath, outputDirectory))
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
        return new ArchiveOperationResult { Processed = extracted, Skipped = skipped, TotalBytes = totalBytes };
    }

    private static async Task<ArchiveOperationResult> ExtractSharpCompressAsync(
        string archivePath, string outputDirectory, IProgress<string>? progress)
    {
        progress?.Report($"Opening {Path.GetFileName(archivePath)}...");

        int extracted = 0;
        int skipped = 0;
        long totalBytes = 0;

        await Task.Run(() =>
        {
            using var archive = ArchiveFactory.Open(archivePath);
            var romEntries = archive.Entries
                .Where(e => !e.IsDirectory && RomExtensions.Contains(Path.GetExtension(e.Key ?? "")))
                .ToList();

            if (romEntries.Count == 0)
            {
                progress?.Report("No ROM files found in archive.");
                return;
            }

            for (int i = 0; i < romEntries.Count; i++)
            {
                var entry = romEntries[i];
                progress?.Report($"Extracting {i + 1} of {romEntries.Count}: {entry.Key}");

                string entryFileName = Path.GetFileName(entry.Key ?? "");
                if (string.IsNullOrEmpty(entryFileName))
                {
                    skipped++;
                    continue;
                }

                string outputPath = Path.Combine(outputDirectory, entryFileName);
                if (!IsPathSafe(outputPath, outputDirectory))
                {
                    skipped++;
                    continue;
                }

                try
                {
                    using var entryStream = entry.OpenEntryStream();
                    using var fileStream = File.Create(outputPath);
                    entryStream.CopyTo(fileStream);
                    extracted++;
                    totalBytes += entry.Size;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    skipped++;
                }
            }
        }).ConfigureAwait(false);

        progress?.Report("Done.");
        return new ArchiveOperationResult { Processed = extracted, Skipped = skipped, TotalBytes = totalBytes };
    }

    private static async Task<ArchiveOperationResult> ExtractGzipAsync(
        string gzipPath, string outputDirectory, IProgress<string>? progress)
    {
        string fileName = Path.GetFileName(gzipPath);
        progress?.Report($"Decompressing {fileName}...");

        string outputName = Path.GetFileNameWithoutExtension(gzipPath);
        if (string.IsNullOrEmpty(outputName)) outputName = "decompressed_rom";

        string outputPath = Path.Combine(outputDirectory, outputName);
        if (!IsPathSafe(outputPath, outputDirectory))
            throw new InvalidOperationException("Output path is outside the target directory.");

        long decompressedSize = 0;

        await Task.Run(() =>
        {
            try
            {
                using var inputStream = new FileStream(gzipPath, FileMode.Open, FileAccess.Read, FileShare.Read, GzipBufferSize);
                using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);
                using var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, GzipBufferSize);

                byte[] buffer = new byte[GzipBufferSize];
                int bytesRead;
                while ((bytesRead = gzipStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    decompressedSize += bytesRead;
                    if (decompressedSize > MaxDecompressedSize)
                        throw new InvalidOperationException($"Decompressed size exceeds maximum limit ({FileUtils.FormatFileSize(MaxDecompressedSize)}).");
                    outputStream.Write(buffer, 0, bytesRead);
                }
            }
            catch
            {
                // Clean up partial output file on failure;
                // catch-all is intentional — cleanup must not mask the original exception.
                try { File.Delete(outputPath); } catch { /* best-effort cleanup */ }
                throw;
            }
        }).ConfigureAwait(false);

        progress?.Report("Done.");
        return new ArchiveOperationResult { Processed = 1, Skipped = 0, TotalBytes = decompressedSize };
    }

    /// <summary>
    /// Checks if a file appears to be GZip-compressed (magic bytes 1F 8B).
    /// </summary>
    internal static bool IsGzipCompressed(string filePath)
    {
        if (!File.Exists(filePath))
            return false;

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        byte[] magic = new byte[2];
        int read = fs.Read(magic, 0, 2);
        return read == 2 && magic[0] == 0x1F && magic[1] == 0x8B;
    }

    // ── Creation ────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a ZIP archive from a list of input file paths.
    /// </summary>
    public static async Task<ArchiveOperationResult> CreateArchiveAsync(
        string outputPath,
        IReadOnlyList<string> inputFiles,
        IProgress<string>? progress = null)
    {
        if (inputFiles.Count == 0)
            throw new ArgumentException("No input files provided.", nameof(inputFiles));

        string ext = Path.GetExtension(outputPath);
        if (!string.Equals(ext, ".zip", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Archive format '{ext}' is not supported for creation. Only ZIP is supported.");

        string? dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        return await CreateZipAsync(outputPath, inputFiles, progress).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates ZIP archives from all ROM files in a directory.
    /// One archive per file, or a single archive containing all files.
    /// </summary>
    public static async Task<ArchiveOperationResult> CreateBatchAsync(
        string inputDirectory,
        string outputDirectory,
        bool oneArchivePerFile,
        IProgress<string>? progress = null)
    {
        if (!Directory.Exists(inputDirectory))
            throw new DirectoryNotFoundException($"Directory not found: {inputDirectory}");

        Directory.CreateDirectory(outputDirectory);

        var inputFiles = Directory.EnumerateFiles(inputDirectory, "*", SearchOption.AllDirectories)
            .Where(f => RomExtensions.Contains(Path.GetExtension(f)))
            .ToList();

        if (inputFiles.Count == 0)
        {
            progress?.Report("No ROM files found.");
            return new ArchiveOperationResult();
        }

        if (oneArchivePerFile)
        {
            int created = 0;
            int skipped = 0;
            long totalBytes = 0;

            for (int i = 0; i < inputFiles.Count; i++)
            {
                string fileName = Path.GetFileNameWithoutExtension(inputFiles[i]) + ".zip";
                string archivePath = Path.Combine(outputDirectory, fileName);

                progress?.Report($"Creating {i + 1} of {inputFiles.Count}: {fileName}");

                try
                {
                    var result = await CreateArchiveAsync(archivePath, [inputFiles[i]], null).ConfigureAwait(false);
                    created += result.Processed;
                    totalBytes += result.TotalBytes;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    skipped++;
                }
            }

            progress?.Report($"Done — {created} archive(s) created.");
            return new ArchiveOperationResult { Processed = created, Skipped = skipped, TotalBytes = totalBytes };
        }
        else
        {
            string archiveName = Path.GetFileName(inputDirectory) + ".zip";
            string archivePath = Path.Combine(outputDirectory, archiveName);

            progress?.Report($"Creating {archiveName} with {inputFiles.Count} file(s)...");
            return await CreateArchiveAsync(archivePath, inputFiles, progress).ConfigureAwait(false);
        }
    }

    private static async Task<ArchiveOperationResult> CreateZipAsync(
        string outputPath, IReadOnlyList<string> inputFiles, IProgress<string>? progress)
    {
        int added = 0;
        int skipped = 0;
        long totalBytes = 0;

        await Task.Run(() =>
        {
            using var archive = ZipFile.Open(outputPath, ZipArchiveMode.Create);

            for (int i = 0; i < inputFiles.Count; i++)
            {
                string filePath = inputFiles[i];
                string entryName = Path.GetFileName(filePath);
                progress?.Report($"Adding {i + 1} of {inputFiles.Count}: {entryName}");

                try
                {
                    var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                    using var entryStream = entry.Open();
                    using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    fileStream.CopyTo(entryStream);
                    added++;
                    totalBytes += new FileInfo(filePath).Length;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    skipped++;
                }
            }
        }).ConfigureAwait(false);

        progress?.Report("Done.");
        return new ArchiveOperationResult { Processed = added, Skipped = skipped, TotalBytes = totalBytes };
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when the resolved <paramref name="outputPath"/> is
    /// inside <paramref name="outputDirectory"/> (zip-slip / path-traversal protection).
    /// </summary>
    private static bool IsPathSafe(string outputPath, string outputDirectory)
    {
        string fullOutputPath = Path.GetFullPath(outputPath);
        string fullOutputDir = Path.GetFullPath(outputDirectory);
        if (!fullOutputDir.EndsWith(Path.DirectorySeparatorChar))
            fullOutputDir += Path.DirectorySeparatorChar;
        return fullOutputPath.StartsWith(fullOutputDir, StringComparison.Ordinal);
    }
}

/// <summary>Describes a single ROM entry inside an archive.</summary>
public sealed class ArchiveEntry
{
    public string FileName { get; set; } = string.Empty;
    public long CompressedSize { get; set; }
    public long UncompressedSize { get; set; }

    public string Summary =>
        $"{FileName} — {FileUtils.FormatFileSize(UncompressedSize)} ({FileUtils.FormatFileSize(CompressedSize)} compressed)";
}

/// <summary>Result of an extract or create operation.</summary>
public sealed class ArchiveOperationResult
{
    public int Processed { get; set; }
    public int Skipped { get; set; }
    public long TotalBytes { get; set; }

    public string Summary =>
        $"{Processed} file(s) processed ({FileUtils.FormatFileSize(TotalBytes)}), {Skipped} skipped";
}

/// <summary>Describes a creatable archive format.</summary>
public sealed record ArchiveFormat(string DisplayName, string Extension);
