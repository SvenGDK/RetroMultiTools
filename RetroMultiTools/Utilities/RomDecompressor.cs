using System.IO.Compression;

namespace RetroMultiTools.Utilities;

/// <summary>
/// Decompresses ROM files from common compression formats (GZip).
/// </summary>
public static class RomDecompressor
{
    private const int BufferSize = 81920;

    /// <summary>
    /// Maximum allowed decompressed size (2 GB) to prevent decompression bomb attacks.
    /// </summary>
    private const long MaxDecompressedSize = 2L * 1024 * 1024 * 1024;

    /// <summary>
    /// Checks if a file appears to be a GZip-compressed ROM.
    /// </summary>
    public static bool IsGzipCompressed(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found.", filePath);

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        byte[] magic = new byte[2];
        int read = fs.Read(magic, 0, 2);
        return read == 2 && magic[0] == 0x1F && magic[1] == 0x8B;
    }

    /// <summary>
    /// Decompresses a GZip-compressed file.
    /// </summary>
    public static async Task<DecompressionResult> DecompressAsync(
        string inputPath,
        string outputPath,
        IProgress<string>? progress = null)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Input file not found.", inputPath);

        long compressedSize = new FileInfo(inputPath).Length;

        if (!IsGzipCompressed(inputPath))
            throw new InvalidOperationException("File does not appear to be GZip compressed.");

        progress?.Report($"Decompressing {Path.GetFileName(inputPath)}...");

        long decompressedSize = 0;

        await Task.Run(() =>
        {
            try
            {
                using var inputStream = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
                using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);
                using var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize);

                byte[] buffer = new byte[BufferSize];
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
                try { File.Delete(outputPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                throw;
            }
        }).ConfigureAwait(false);

        progress?.Report("Done.");

        return new DecompressionResult
        {
            CompressedSize = compressedSize,
            DecompressedSize = decompressedSize,
            OutputPath = outputPath
        };
    }

    /// <summary>
    /// Batch decompresses all compressed ROM files in a directory.
    /// </summary>
    public static async Task<BatchDecompressionResult> DecompressBatchAsync(
        string inputDirectory,
        string outputDirectory,
        IProgress<string>? progress = null)
    {
        if (!Directory.Exists(inputDirectory))
            throw new DirectoryNotFoundException($"Directory not found: {inputDirectory}");

        Directory.CreateDirectory(outputDirectory);

        var compressedFiles = Directory.EnumerateFiles(inputDirectory, "*.gz", SearchOption.AllDirectories).ToList();

        if (compressedFiles.Count == 0)
        {
            progress?.Report("No compressed ROM files found.");
            return new BatchDecompressionResult();
        }

        int decompressed = 0;
        int skipped = 0;
        int failed = 0;

        for (int i = 0; i < compressedFiles.Count; i++)
        {
            string file = compressedFiles[i];
            string fileName = Path.GetFileName(file);
            progress?.Report($"Processing {i + 1} of {compressedFiles.Count}: {fileName}");

            try
            {
                // Remove .gz extension for output filename
                string outputName = Path.GetFileNameWithoutExtension(fileName);
                if (string.IsNullOrEmpty(outputName)) outputName = fileName + ".rom";

                string outputPath = Path.Combine(outputDirectory, outputName);

                if (!IsGzipCompressed(file))
                {
                    skipped++;
                    continue;
                }

                await DecompressAsync(file, outputPath, null).ConfigureAwait(false);
                decompressed++;
            }
            catch (IOException)
            {
                failed++;
            }
            catch (InvalidOperationException)
            {
                skipped++;
            }
            catch (InvalidDataException)
            {
                failed++;
            }
        }

        progress?.Report($"Done — {decompressed} decompressed, {skipped} skipped, {failed} failed.");

        return new BatchDecompressionResult
        {
            Decompressed = decompressed,
            Skipped = skipped,
            Failed = failed
        };
    }
}

public class DecompressionResult
{
    public long CompressedSize { get; set; }
    public long DecompressedSize { get; set; }
    public string OutputPath { get; set; } = string.Empty;

    public string Summary =>
        $"{FileUtils.FormatFileSize(CompressedSize)} → {FileUtils.FormatFileSize(DecompressedSize)}";
}

public class BatchDecompressionResult
{
    public int Decompressed { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }

    public string Summary =>
        $"{Decompressed} decompressed, {Skipped} skipped, {Failed} failed";
}
