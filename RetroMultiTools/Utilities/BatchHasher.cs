using System.Security.Cryptography;
using System.Text;

namespace RetroMultiTools.Utilities;

public static class BatchHasher
{
    private const int BufferSize = 81920;

    private static readonly HashSet<string> KnownExtensions = new(StringComparer.OrdinalIgnoreCase)
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
        ".3do", ".chd", ".rvz", ".gcm"
    };

    public static async Task<List<BatchHashResult>> HashDirectoryAsync(
        string directoryPath, bool includeMd5, bool includeSha1, bool includeSha256,
        IProgress<string>? progress = null)
    {
        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");

        var files = Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories)
            .Where(f => KnownExtensions.Contains(Path.GetExtension(f)))
            .OrderBy(f => f)
            .ToList();

        progress?.Report($"Found {files.Count} ROM file(s). Hashing...");

        var results = new List<BatchHashResult>();
        for (int i = 0; i < files.Count; i++)
        {
            var file = files[i];
            progress?.Report($"Hashing {i + 1} of {files.Count}: {Path.GetFileName(file)}");

            var result = await HashFileAsync(file, includeMd5, includeSha1, includeSha256)
                .ConfigureAwait(false);
            results.Add(result);
        }

        progress?.Report($"Done. Hashed {results.Count} file(s).");
        return results;
    }

    public static async Task<BatchHashResult> HashFileAsync(
        string filePath, bool includeMd5, bool includeSha1, bool includeSha256)
    {
        var info = new FileInfo(filePath);
        string crc32 = await Task.Run(() => ComputeCrc32(filePath)).ConfigureAwait(false);

        string? md5 = includeMd5 ? await ComputeHashAsync<MD5>(filePath).ConfigureAwait(false) : null;
        string? sha1 = includeSha1 ? await ComputeHashAsync<SHA1>(filePath).ConfigureAwait(false) : null;
        string? sha256 = includeSha256 ? await ComputeHashAsync<SHA256>(filePath).ConfigureAwait(false) : null;

        return new BatchHashResult
        {
            FilePath = filePath,
            FileName = info.Name,
            FileSize = info.Length,
            FileSizeFormatted = FileUtils.FormatFileSize(info.Length),
            CRC32 = crc32,
            MD5 = md5,
            SHA1 = sha1,
            SHA256 = sha256
        };
    }

    public static async Task ExportResultsAsync(
        List<BatchHashResult> results, string outputPath, BatchHashExportFormat format)
    {
        var content = format switch
        {
            BatchHashExportFormat.Csv => BuildCsv(results),
            BatchHashExportFormat.Text => BuildTextReport(results),
            BatchHashExportFormat.SfvChecksum => BuildSfv(results),
            BatchHashExportFormat.Md5Sum => BuildMd5Sum(results),
            _ => throw new ArgumentException($"Unknown format: {format}")
        };

        try
        {
            await File.WriteAllTextAsync(outputPath, content).ConfigureAwait(false);
        }
        catch
        {
            try { File.Delete(outputPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw;
        }
    }

    private static string BuildCsv(List<BatchHashResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("File,Size,CRC32,MD5,SHA1,SHA256");
        foreach (var r in results)
        {
            sb.Append(CsvEscape(r.FileName));
            sb.Append(',');
            sb.Append(r.FileSize);
            sb.Append(',');
            sb.Append(r.CRC32);
            sb.Append(',');
            sb.Append(r.MD5 ?? "");
            sb.Append(',');
            sb.Append(r.SHA1 ?? "");
            sb.Append(',');
            sb.AppendLine(r.SHA256 ?? "");
        }
        return sb.ToString();
    }

    private static string CsvEscape(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    private static string BuildTextReport(List<BatchHashResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Batch Hash Report ===");
        sb.AppendLine($"Files: {results.Count}");
        sb.AppendLine();

        foreach (var r in results)
        {
            sb.AppendLine($"File: {r.FileName}");
            sb.AppendLine($"  Size:   {r.FileSizeFormatted} ({r.FileSize:N0} bytes)");
            sb.AppendLine($"  CRC32:  {r.CRC32}");
            if (r.MD5 != null) sb.AppendLine($"  MD5:    {r.MD5}");
            if (r.SHA1 != null) sb.AppendLine($"  SHA-1:  {r.SHA1}");
            if (r.SHA256 != null) sb.AppendLine($"  SHA-256: {r.SHA256}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string BuildSfv(List<BatchHashResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("; SFV checksum file");
        foreach (var r in results)
            sb.AppendLine($"{r.FileName} {r.CRC32}");
        return sb.ToString();
    }

    private static string BuildMd5Sum(List<BatchHashResult> results)
    {
        var sb = new StringBuilder();
        foreach (var r in results)
        {
            if (r.MD5 != null)
                sb.AppendLine($"{r.MD5}  {r.FileName}");
        }
        return sb.ToString();
    }

    private static async Task<string> ComputeHashAsync<T>(string filePath) where T : HashAlgorithm
    {
        using var algorithm = typeof(T).Name switch
        {
            nameof(MD5) => (HashAlgorithm)MD5.Create(),
            nameof(SHA1) => (HashAlgorithm)SHA1.Create(),
            nameof(SHA256) => (HashAlgorithm)SHA256.Create(),
            _ => throw new InvalidOperationException($"Unsupported hash: {typeof(T).Name}")
        };
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
        byte[] hash = await algorithm.ComputeHashAsync(stream).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private static string ComputeCrc32(string filePath)
    {
        uint crc = 0xFFFFFFFF;
        byte[] buffer = new byte[BufferSize];
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
        int bytesRead;
        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < bytesRead; i++)
                crc = (crc >> 8) ^ Crc32Table[(crc ^ buffer[i]) & 0xFF];
        }
        return (crc ^ 0xFFFFFFFF).ToString("X8");
    }

    private static readonly uint[] Crc32Table = GenerateCrc32Table();

    private static uint[] GenerateCrc32Table()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint crc = i;
            for (int j = 0; j < 8; j++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
            table[i] = crc;
        }
        return table;
    }
}

public enum BatchHashExportFormat
{
    Csv,
    Text,
    SfvChecksum,
    Md5Sum,
}

public class BatchHashResult
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string FileSizeFormatted { get; set; } = string.Empty;
    public string CRC32 { get; set; } = string.Empty;
    public string? MD5 { get; set; }
    public string? SHA1 { get; set; }
    public string? SHA256 { get; set; }
}
