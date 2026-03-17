namespace RetroMultiTools.Utilities;

public static class RomTrimmer
{
    private const int BufferSize = 81920;

    /// <summary>
    /// Analyzes a ROM file to determine how many trailing padding bytes it has.
    /// Padding bytes are 0x00 or 0xFF at the end of the file.
    /// </summary>
    public static TrimAnalysis Analyze(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found.", filePath);

        var fileInfo = new FileInfo(filePath);
        long fileSize = fileInfo.Length;
        if (fileSize == 0)
            return new TrimAnalysis { OriginalSize = 0, TrimmedSize = 0 };

        // Read from the end of the file to find the last non-padding byte
        long lastNonPadding = -1;
        byte[] buffer = new byte[BufferSize];

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);

        // Read in reverse chunks from the end
        long position = fileSize;
        bool found = false;
        while (position > 0 && !found)
        {
            int chunkSize = (int)Math.Min(BufferSize, position);
            position -= chunkSize;
            stream.Seek(position, SeekOrigin.Begin);
            int bytesRead = stream.Read(buffer, 0, chunkSize);

            for (int i = bytesRead - 1; i >= 0; i--)
            {
                if (buffer[i] != 0x00 && buffer[i] != 0xFF)
                {
                    lastNonPadding = position + i;
                    found = true;
                    break;
                }
            }
        }

        // trimmedSize is lastNonPadding + 1 (the byte at lastNonPadding is kept)
        // If all bytes are padding, keep the original file unchanged
        if (lastNonPadding < 0)
            return new TrimAnalysis { OriginalSize = fileSize, TrimmedSize = fileSize };

        long trimmedSize = lastNonPadding + 1;

        // Align to the nearest power-of-two boundary if reasonable
        // Many ROM formats expect power-of-two sizes
        long alignedSize = AlignToPowerOfTwo(trimmedSize);
        if (alignedSize > fileSize)
            alignedSize = trimmedSize; // Don't exceed file size; fall back to unaligned trim

        return new TrimAnalysis
        {
            OriginalSize = fileSize,
            TrimmedSize = alignedSize
        };
    }

    /// <summary>
    /// Trims trailing padding from a ROM file.
    /// </summary>
    public static async Task TrimAsync(
        string inputPath,
        string outputPath,
        IProgress<string>? progress = null)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Input file not found.", inputPath);

        var analysis = Analyze(inputPath);

        if (analysis.SavedBytes == 0)
        {
            progress?.Report("No padding found — file is already trimmed.");
            File.Copy(inputPath, outputPath, overwrite: true);
            return;
        }

        progress?.Report($"Trimming {FileUtils.FormatFileSize(analysis.SavedBytes)} of padding...");

        await Task.Run(() =>
        {
            try
            {
                using var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
                using var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize);

                byte[] buffer = new byte[BufferSize];
                long remaining = analysis.TrimmedSize;

                while (remaining > 0)
                {
                    int toRead = (int)Math.Min(BufferSize, remaining);
                    int bytesRead = input.Read(buffer, 0, toRead);
                    if (bytesRead == 0) break;
                    output.Write(buffer, 0, bytesRead);
                    remaining -= bytesRead;
                }
            }
            catch
            {
                try { File.Delete(outputPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                throw;
            }
        }).ConfigureAwait(false);

        progress?.Report("Done.");
    }

    /// <summary>
    /// Rounds up to the next power of two, but only if the value is at least 1024.
    /// For very small values, returns the value as-is.
    /// </summary>
    private static long AlignToPowerOfTwo(long value)
    {
        if (value <= 0) return 0;
        if (value <= 1024) return value;

        long power = 1;
        while (power > 0 && power < value)
            power <<= 1;
        return power > 0 ? power : value;
    }
}

public class TrimAnalysis
{
    public long OriginalSize { get; set; }
    public long TrimmedSize { get; set; }

    public long SavedBytes => OriginalSize - TrimmedSize;

    public string Summary
    {
        get
        {
            if (SavedBytes == 0)
                return $"No padding found — file is {FileUtils.FormatFileSize(OriginalSize)}.";

            double pct = OriginalSize > 0 ? (SavedBytes * 100.0 / OriginalSize) : 0;
            return $"{FileUtils.FormatFileSize(OriginalSize)} → {FileUtils.FormatFileSize(TrimmedSize)} " +
                   $"({FileUtils.FormatFileSize(SavedBytes)} saved, {pct:F1}%)";
        }
    }
}
