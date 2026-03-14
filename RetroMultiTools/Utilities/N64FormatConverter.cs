namespace RetroMultiTools.Utilities;

public static class N64FormatConverter
{
    private const int BufferSize = 81920;

    public enum N64Format
    {
        BigEndian,    // .z64 — native format (80 37 12 40)
        LittleEndian, // .n64 — byte-swapped 32-bit words (40 12 37 80)
        ByteSwapped   // .v64 — byte-swapped 16-bit words (37 80 40 12)
    }

    public static N64Format? DetectFormat(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found.", filePath);

        Span<byte> header = stackalloc byte[4];
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (fs.Read(header) < 4)
            return null;

        return DetectFormat(header);
    }

    public static N64Format? DetectFormat(ReadOnlySpan<byte> header)
    {
        if (header.Length < 4)
            return null;

        if (header[0] == 0x80 && header[1] == 0x37 && header[2] == 0x12 && header[3] == 0x40)
            return N64Format.BigEndian;
        if (header[0] == 0x40 && header[1] == 0x12 && header[2] == 0x37 && header[3] == 0x80)
            return N64Format.LittleEndian;
        if (header[0] == 0x37 && header[1] == 0x80 && header[2] == 0x40 && header[3] == 0x12)
            return N64Format.ByteSwapped;

        return null;
    }

    public static async Task ConvertAsync(
        string inputPath,
        string outputPath,
        N64Format targetFormat,
        IProgress<string>? progress = null)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Input file not found.", inputPath);

        var sourceFormat = DetectFormat(inputPath)
            ?? throw new InvalidDataException("Not a recognized N64 ROM (unknown byte order).");

        if (sourceFormat == targetFormat)
        {
            progress?.Report("Source ROM is already in the target format. Copying...");
            File.Copy(inputPath, outputPath, overwrite: true);
            progress?.Report("Done.");
            return;
        }

        progress?.Report($"Converting from {FormatName(sourceFormat)} to {FormatName(targetFormat)}...");

        await Task.Run(() =>
        {
            try
            {
                using var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
                using var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize);

                byte[] buffer = new byte[BufferSize];
                int bytesRead;

                while ((bytesRead = ReadFully(input, buffer)) > 0)
                {
                    ConvertBuffer(buffer, bytesRead, sourceFormat, targetFormat);
                    output.Write(buffer, 0, bytesRead);
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

    private static void ConvertBuffer(byte[] buffer, int length, N64Format from, N64Format to)
    {
        // Convert to Big Endian first (canonical), then to target
        if (from != N64Format.BigEndian)
            NormalizeToBigEndian(buffer, length, from);

        if (to != N64Format.BigEndian)
            FromBigEndian(buffer, length, to);
    }

    /// <summary>
    /// Converts bytes from the specified N64 format to Big Endian in-place.
    /// </summary>
    public static void NormalizeToBigEndian(byte[] buffer, int length, N64Format from)
    {
        switch (from)
        {
            case N64Format.LittleEndian:
                // 32-bit word swap: [3,2,1,0] -> [0,1,2,3]
                for (int i = 0; i + 3 < length; i += 4)
                {
                    (buffer[i], buffer[i + 3]) = (buffer[i + 3], buffer[i]);
                    (buffer[i + 1], buffer[i + 2]) = (buffer[i + 2], buffer[i + 1]);
                }
                break;

            case N64Format.ByteSwapped:
                // 16-bit word swap: [1,0,3,2] -> [0,1,2,3]
                for (int i = 0; i + 1 < length; i += 2)
                {
                    (buffer[i], buffer[i + 1]) = (buffer[i + 1], buffer[i]);
                }
                break;
        }
    }

    /// <summary>
    /// Converts bytes from Big Endian to the specified N64 format in-place.
    /// </summary>
    public static void FromBigEndian(byte[] buffer, int length, N64Format to)
    {
        switch (to)
        {
            case N64Format.LittleEndian:
                // [0,1,2,3] -> [3,2,1,0]
                for (int i = 0; i + 3 < length; i += 4)
                {
                    (buffer[i], buffer[i + 3]) = (buffer[i + 3], buffer[i]);
                    (buffer[i + 1], buffer[i + 2]) = (buffer[i + 2], buffer[i + 1]);
                }
                break;

            case N64Format.ByteSwapped:
                // [0,1,2,3] -> [1,0,3,2]
                for (int i = 0; i + 1 < length; i += 2)
                {
                    (buffer[i], buffer[i + 1]) = (buffer[i + 1], buffer[i]);
                }
                break;
        }
    }

    public static string FormatName(N64Format format) => format switch
    {
        N64Format.BigEndian => "Big Endian (.z64)",
        N64Format.LittleEndian => "Little Endian (.n64)",
        N64Format.ByteSwapped => "Byte-swapped (.v64)",
        _ => "Unknown"
    };

    public static string FormatExtension(N64Format format) => format switch
    {
        N64Format.BigEndian => ".z64",
        N64Format.LittleEndian => ".n64",
        N64Format.ByteSwapped => ".v64",
        _ => ".z64"
    };

    private static int ReadFully(FileStream stream, byte[] buffer)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int read = stream.Read(buffer, totalRead, buffer.Length - totalRead);
            if (read == 0) break;
            totalRead += read;
        }
        return totalRead;
    }
}
