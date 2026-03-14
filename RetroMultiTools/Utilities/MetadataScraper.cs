using RetroMultiTools.Detection;
using System.Text;

namespace RetroMultiTools.Utilities;

/// <summary>
/// Scrapes metadata from ROM files in bulk, extracting header information,
/// checksums, and system details into a structured report.
/// </summary>
public static class MetadataScraper
{
    private static readonly HashSet<string> RomExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".nes", ".smc", ".sfc", ".z64", ".n64", ".v64",
        ".gb", ".gbc", ".gba", ".vb", ".vboy",
        ".sms", ".md", ".gen",
        ".bin", ".32x", ".gg", ".a26", ".a52", ".a78",
        ".j64", ".jag", ".lnx", ".lyx",
        ".pce", ".tg16",
        ".ngp", ".ngc",
        ".col", ".cv", ".int",
        ".mx1", ".mx2",
        ".dsk", ".cdt", ".sna",
        ".tap",
        ".mo5", ".k7", ".fd",
        ".sv", ".ccc",
        ".iso", ".cue", ".3do",
        ".cdi", ".gdi",
        ".chd", ".rvz", ".gcm"
    };

    /// <summary>
    /// Scrapes metadata from all ROM files in a directory, including checksums.
    /// </summary>
    public static async Task<List<RomMetadata>> ScrapeDirectoryAsync(
        string directory,
        bool includeChecksums,
        IProgress<string>? progress = null)
    {
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException($"Directory not found: {directory}");

        var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(f => RomExtensions.Contains(Path.GetExtension(f)))
            .ToList();

        progress?.Report($"Found {files.Count} ROM file(s). Scraping metadata...");

        var results = new List<RomMetadata>();

        for (int i = 0; i < files.Count; i++)
        {
            string file = files[i];
            string fileName = Path.GetFileName(file);
            progress?.Report($"Scraping {i + 1} of {files.Count}: {fileName}");

            try
            {
                var metadata = await ScrapeFileAsync(file, includeChecksums).ConfigureAwait(false);
                results.Add(metadata);
            }
            catch (IOException ex)
            {
                results.Add(new RomMetadata
                {
                    FilePath = file,
                    FileName = fileName,
                    Error = ex.Message
                });
            }
        }

        progress?.Report($"Done — scraped metadata for {results.Count} ROM(s).");
        return results;
    }

    /// <summary>
    /// Scrapes metadata from a single ROM file.
    /// </summary>
    public static async Task<RomMetadata> ScrapeFileAsync(string filePath, bool includeChecksums)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("ROM file not found.", filePath);

        var romInfo = await Task.Run(() => RomDetector.Detect(filePath)).ConfigureAwait(false);

        var metadata = new RomMetadata
        {
            FilePath = filePath,
            FileName = romInfo.FileName,
            System = romInfo.SystemName,
            FileSize = romInfo.FileSize,
            FileSizeFormatted = romInfo.FileSizeFormatted,
            IsValid = romInfo.IsValid,
            HeaderInfo = new Dictionary<string, string>(romInfo.HeaderInfo)
        };

        // Extract common fields from header
        if (romInfo.HeaderInfo.TryGetValue("Title", out string? title))
            metadata.Title = title;
        else if (romInfo.HeaderInfo.TryGetValue("Internal Title", out string? internalTitle))
            metadata.Title = internalTitle;

        if (romInfo.HeaderInfo.TryGetValue("Region", out string? region))
            metadata.Region = region;

        if (romInfo.HeaderInfo.TryGetValue("Mapper", out string? mapper))
            metadata.Mapper = mapper;

        if (includeChecksums)
        {
            var checksums = await ChecksumCalculator.CalculateAsync(filePath).ConfigureAwait(false);
            metadata.CRC32 = checksums.CRC32;
            metadata.MD5 = checksums.MD5;
            metadata.SHA1 = checksums.SHA1;
        }

        return metadata;
    }

    /// <summary>
    /// Exports scraped metadata to CSV format.
    /// </summary>
    public static async Task ExportToCsvAsync(
        List<RomMetadata> metadata,
        string outputPath,
        IProgress<string>? progress = null)
    {
        progress?.Report("Exporting to CSV...");

        var sb = new StringBuilder();
        sb.AppendLine("FileName,System,Title,Region,FileSize,FileSizeFormatted,IsValid,CRC32,MD5,SHA1,Mapper,HeaderFields");

        foreach (var m in metadata)
        {
            sb.Append(CsvEscape(m.FileName));
            sb.Append(',');
            sb.Append(CsvEscape(m.System));
            sb.Append(',');
            sb.Append(CsvEscape(m.Title));
            sb.Append(',');
            sb.Append(CsvEscape(m.Region));
            sb.Append(',');
            sb.Append(m.FileSize);
            sb.Append(',');
            sb.Append(CsvEscape(m.FileSizeFormatted));
            sb.Append(',');
            sb.Append(m.IsValid);
            sb.Append(',');
            sb.Append(CsvEscape(m.CRC32));
            sb.Append(',');
            sb.Append(CsvEscape(m.MD5));
            sb.Append(',');
            sb.Append(CsvEscape(m.SHA1));
            sb.Append(',');
            sb.Append(CsvEscape(m.Mapper));
            sb.Append(',');

            string headerFields = m.HeaderInfo.Count > 0
                ? string.Join("; ", m.HeaderInfo.Select(kv => $"{kv.Key}={kv.Value}"))
                : "";
            sb.AppendLine(CsvEscape(headerFields));
        }

        try
        {
            await File.WriteAllTextAsync(outputPath, sb.ToString()).ConfigureAwait(false);
        }
        catch
        {
            try { File.Delete(outputPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw;
        }
        progress?.Report("Done.");
    }

    /// <summary>
    /// Exports scraped metadata to a formatted text report.
    /// </summary>
    public static async Task ExportToTextAsync(
        List<RomMetadata> metadata,
        string outputPath,
        IProgress<string>? progress = null)
    {
        progress?.Report("Exporting to text report...");

        var sb = new StringBuilder();
        sb.AppendLine("╔══════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║          RetroMultiTools — Bulk Metadata Report             ║");
        sb.AppendLine($"║  Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}                            ║");
        sb.AppendLine($"║  Total ROMs: {metadata.Count,-48}║");
        sb.AppendLine("╚══════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        // Group by system
        var bySystem = metadata.GroupBy(m => m.System).OrderBy(g => g.Key);
        foreach (var group in bySystem)
        {
            sb.AppendLine($"━━ {group.Key} ({group.Count()} ROMs) ━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine();

            foreach (var m in group)
            {
                sb.AppendLine($"  File:    {m.FileName}");
                if (!string.IsNullOrEmpty(m.Title))
                    sb.AppendLine($"  Title:   {m.Title}");
                if (!string.IsNullOrEmpty(m.Region))
                    sb.AppendLine($"  Region:  {m.Region}");
                sb.AppendLine($"  Size:    {m.FileSizeFormatted}");
                sb.AppendLine($"  Valid:   {(m.IsValid ? "Yes" : "No")}");

                if (!string.IsNullOrEmpty(m.CRC32))
                    sb.AppendLine($"  CRC32:   {m.CRC32}");
                if (!string.IsNullOrEmpty(m.SHA1))
                    sb.AppendLine($"  SHA-1:   {m.SHA1}");

                if (m.HeaderInfo.Count > 0)
                {
                    sb.AppendLine("  Header:");
                    foreach (var kv in m.HeaderInfo)
                        sb.AppendLine($"    {kv.Key}: {kv.Value}");
                }

                if (!string.IsNullOrEmpty(m.Error))
                    sb.AppendLine($"  Error:   {m.Error}");

                sb.AppendLine();
            }
        }

        // Summary
        sb.AppendLine("━━ Summary ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        foreach (var group in bySystem)
            sb.AppendLine($"  {group.Key}: {group.Count()}");

        int valid = metadata.Count(m => m.IsValid);
        sb.AppendLine($"  Valid: {valid}, Invalid: {metadata.Count - valid}");

        try
        {
            await File.WriteAllTextAsync(outputPath, sb.ToString()).ConfigureAwait(false);
        }
        catch
        {
            try { File.Delete(outputPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw;
        }
        progress?.Report("Done.");
    }

    private static string CsvEscape(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}

public class RomMetadata
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string System { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Mapper { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string FileSizeFormatted { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public string CRC32 { get; set; } = string.Empty;
    public string MD5 { get; set; } = string.Empty;
    public string SHA1 { get; set; } = string.Empty;
    public Dictionary<string, string> HeaderInfo { get; set; } = [];
    public string? Error { get; set; }
}
