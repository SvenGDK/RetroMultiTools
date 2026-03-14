namespace RetroMultiTools.Utilities;

public static class SaveFileConverter
{
    private static readonly Dictionary<string, SaveFormat> FormatMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".sav", SaveFormat.Raw },
        { ".srm", SaveFormat.Raw },
        { ".eep", SaveFormat.Raw },
        { ".fla", SaveFormat.Raw },
        { ".sra", SaveFormat.Raw },
    };

    public static SaveFileInfo Analyze(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Save file not found.", filePath);

        var info = new FileInfo(filePath);
        string ext = info.Extension.ToLowerInvariant();
        bool isPowerOfTwo = IsPowerOfTwo(info.Length);
        var format = FormatMap.GetValueOrDefault(ext, SaveFormat.Raw);

        return new SaveFileInfo
        {
            FilePath = filePath,
            FileName = info.Name,
            FileSize = info.Length,
            FileSizeFormatted = FileUtils.FormatFileSize(info.Length),
            Extension = ext,
            Format = format,
            IsPowerOfTwo = isPowerOfTwo,
            DetectedType = DetectSaveType(info.Length)
        };
    }

    private const long MaxFileSize = 256L * 1024 * 1024; // 256 MB

    public static async Task ConvertAsync(
        string inputPath, string outputPath, SaveConversion conversion,
        IProgress<string>? progress = null)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Input file not found.", inputPath);

        long fileSize = new FileInfo(inputPath).Length;
        if (fileSize > MaxFileSize)
            throw new InvalidOperationException(
                $"File is too large ({fileSize / (1024.0 * 1024):F1} MB). Maximum supported size: {MaxFileSize / (1024 * 1024)} MB.");

        byte[] data = await File.ReadAllBytesAsync(inputPath).ConfigureAwait(false);

        progress?.Report($"Applying conversion: {conversion}...");

        byte[] result = conversion switch
        {
            SaveConversion.SwapEndian16 => SwapEndian16(data),
            SaveConversion.SwapEndian32 => SwapEndian32(data),
            SaveConversion.PadToPowerOfTwo => PadToPowerOfTwo(data),
            SaveConversion.TrimTrailingZeros => TrimTrailing(data, 0x00),
            SaveConversion.TrimTrailingFF => TrimTrailing(data, 0xFF),
            SaveConversion.SrmToSav => ConvertRaw(data),
            SaveConversion.SavToSrm => ConvertRaw(data),
            _ => throw new ArgumentException($"Unknown conversion: {conversion}")
        };

        progress?.Report("Writing output file...");
        try
        {
            await File.WriteAllBytesAsync(outputPath, result).ConfigureAwait(false);
        }
        catch
        {
            try { File.Delete(outputPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw;
        }
        progress?.Report("Done.");
    }

    private static byte[] SwapEndian16(byte[] data)
    {
        byte[] result = new byte[data.Length];
        int i;
        for (i = 0; i + 1 < data.Length; i += 2)
        {
            result[i] = data[i + 1];
            result[i + 1] = data[i];
        }
        if (i < data.Length)
            result[i] = data[i];
        return result;
    }

    private static byte[] SwapEndian32(byte[] data)
    {
        byte[] result = new byte[data.Length];
        int i;
        for (i = 0; i + 3 < data.Length; i += 4)
        {
            result[i] = data[i + 3];
            result[i + 1] = data[i + 2];
            result[i + 2] = data[i + 1];
            result[i + 3] = data[i];
        }
        for (; i < data.Length; i++)
            result[i] = data[i];
        return result;
    }

    private static byte[] PadToPowerOfTwo(byte[] data)
    {
        if (data.Length == 0) return data;
        long target = 1;
        while (target < data.Length) target <<= 1;
        if (target == data.Length) return (byte[])data.Clone();

        byte[] result = new byte[target];
        Array.Copy(data, result, data.Length);
        return result;
    }

    private static byte[] TrimTrailing(byte[] data, byte padByte)
    {
        int end = data.Length;
        while (end > 0 && data[end - 1] == padByte) end--;
        if (end == data.Length) return (byte[])data.Clone();

        // Align to next power of two
        long aligned = 1;
        while (aligned < end) aligned <<= 1;
        int targetSize = (int)Math.Min(aligned, data.Length);

        byte[] result = new byte[targetSize];
        Array.Copy(data, result, Math.Min(data.Length, targetSize));
        return result;
    }

    private static byte[] ConvertRaw(byte[] data)
    {
        return (byte[])data.Clone();
    }

    private static bool IsPowerOfTwo(long value)
    {
        return value > 0 && (value & (value - 1)) == 0;
    }

    private static string DetectSaveType(long size) => size switch
    {
        512 => "EEPROM 4Kbit (512 bytes)",
        2048 => "EEPROM 16Kbit (2 KB)",
        8192 => "SRAM 64Kbit (8 KB)",
        32768 => "SRAM 256Kbit (32 KB)",
        65536 => "Flash 512Kbit (64 KB)",
        131072 => "Flash 1Mbit (128 KB)",
        _ => $"Unknown ({FileUtils.FormatFileSize(size)})"
    };
}

public enum SaveFormat
{
    Raw,
}

public enum SaveConversion
{
    SwapEndian16,
    SwapEndian32,
    PadToPowerOfTwo,
    TrimTrailingZeros,
    TrimTrailingFF,
    SrmToSav,
    SavToSrm,
}

public class SaveFileInfo
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string FileSizeFormatted { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public SaveFormat Format { get; set; }
    public bool IsPowerOfTwo { get; set; }
    public string DetectedType { get; set; } = string.Empty;
}
