namespace RetroMultiTools.Utilities;

public static class PatchCreator
{
    private const int IpsMaxSize = 0xFFFFFF; // IPS format uses 24-bit addresses, max 16,777,215 bytes

    public static async Task CreateIpsAsync(
        string originalPath, string modifiedPath, string outputPath,
        IProgress<string>? progress = null)
    {
        if (!File.Exists(originalPath))
            throw new FileNotFoundException("Original ROM file not found.", originalPath);
        if (!File.Exists(modifiedPath))
            throw new FileNotFoundException("Modified ROM file not found.", modifiedPath);

        // Validate file sizes before reading into memory
        long origSize = new FileInfo(originalPath).Length;
        long modSize = new FileInfo(modifiedPath).Length;
        if (origSize > IpsMaxSize || modSize > IpsMaxSize)
            throw new InvalidOperationException(
                "Files exceed the IPS format limit of 16 MB. Use BPS format for larger files.");

        byte[] original = await File.ReadAllBytesAsync(originalPath).ConfigureAwait(false);
        byte[] modified = await File.ReadAllBytesAsync(modifiedPath).ConfigureAwait(false);

        progress?.Report("Comparing files...");

        var records = BuildIpsRecords(original, modified, progress);

        progress?.Report($"Writing IPS patch with {records.Count} record(s)...");

        await Task.Run(() => WriteIpsFile(records, modified.Length, original.Length, outputPath))
            .ConfigureAwait(false);

        progress?.Report("Done.");
    }

    private const int MaxAnalyzeSize = 512 * 1024 * 1024; // 512 MB

    public static PatchAnalysis Analyze(string originalPath, string modifiedPath)
    {
        if (!File.Exists(originalPath))
            throw new FileNotFoundException("Original ROM file not found.", originalPath);
        if (!File.Exists(modifiedPath))
            throw new FileNotFoundException("Modified ROM file not found.", modifiedPath);

        var origInfo = new FileInfo(originalPath);
        var modInfo = new FileInfo(modifiedPath);

        if (origInfo.Length > MaxAnalyzeSize || modInfo.Length > MaxAnalyzeSize)
            throw new InvalidOperationException(
                $"Files exceed the maximum size for analysis ({MaxAnalyzeSize / (1024 * 1024)} MB).");

        byte[] original = File.ReadAllBytes(originalPath);
        byte[] modified = File.ReadAllBytes(modifiedPath);

        int diffCount = 0;
        int maxLen = Math.Max(original.Length, modified.Length);
        for (int i = 0; i < maxLen; i++)
        {
            byte origByte = i < original.Length ? original[i] : (byte)0;
            byte modByte = i < modified.Length ? modified[i] : (byte)0;
            if (origByte != modByte) diffCount++;
        }

        return new PatchAnalysis
        {
            OriginalSize = origInfo.Length,
            ModifiedSize = modInfo.Length,
            DifferingBytes = diffCount,
            IsIdentical = diffCount == 0,
            CanCreateIps = origInfo.Length <= IpsMaxSize && modInfo.Length <= IpsMaxSize
        };
    }

    private static List<IpsRecord> BuildIpsRecords(byte[] original, byte[] modified, IProgress<string>? progress)
    {
        var records = new List<IpsRecord>();
        int maxLen = Math.Max(original.Length, modified.Length);
        int i = 0;

        while (i < maxLen)
        {
            byte origByte = i < original.Length ? original[i] : (byte)0;
            byte modByte = i < modified.Length ? modified[i] : (byte)0;

            if (origByte != modByte)
            {
                int offset = i;
                var data = new List<byte>();

                while (i < maxLen)
                {
                    origByte = i < original.Length ? original[i] : (byte)0;
                    modByte = i < modified.Length ? modified[i] : (byte)0;

                    if (origByte == modByte)
                    {
                        // Look ahead to see if there are more changes nearby
                        int gapEnd = Math.Min(i + 8, maxLen);
                        bool moreChanges = false;
                        for (int j = i; j < gapEnd; j++)
                        {
                            byte ob = j < original.Length ? original[j] : (byte)0;
                            byte mb = j < modified.Length ? modified[j] : (byte)0;
                            if (ob != mb) { moreChanges = true; break; }
                        }
                        if (!moreChanges) break;
                    }

                    data.Add(modByte);
                    i++;

                    // IPS record size limit: 0xFFFF bytes
                    if (data.Count >= 0xFFFF)
                        break;
                }

                // Check for RLE compression opportunity
                if (data.Count >= 3 && data.All(b => b == data[0]))
                {
                    records.Add(new IpsRecord
                    {
                        Offset = offset,
                        IsRle = true,
                        RleSize = (ushort)data.Count,
                        RleValue = data[0]
                    });
                }
                else
                {
                    records.Add(new IpsRecord
                    {
                        Offset = offset,
                        Data = [.. data]
                    });
                }
            }
            else
            {
                i++;
            }
        }

        return records;
    }

    private static void WriteIpsFile(List<IpsRecord> records, int modifiedLength, int originalLength, string outputPath)
    {
        try
        {
            using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(stream);

            // IPS header: "PATCH"
            writer.Write("PATCH"u8);

            foreach (var record in records)
            {
                // 3-byte offset (big-endian)
                writer.Write((byte)((record.Offset >> 16) & 0xFF));
                writer.Write((byte)((record.Offset >> 8) & 0xFF));
                writer.Write((byte)(record.Offset & 0xFF));

                if (record.IsRle)
                {
                    // Size = 0 indicates RLE
                    writer.Write((byte)0);
                    writer.Write((byte)0);
                    // RLE size (2 bytes big-endian)
                    writer.Write((byte)((record.RleSize >> 8) & 0xFF));
                    writer.Write((byte)(record.RleSize & 0xFF));
                    // RLE value
                    writer.Write(record.RleValue);
                }
                else
                {
                    // Size (2 bytes big-endian)
                    ushort size = (ushort)record.Data!.Length;
                    writer.Write((byte)((size >> 8) & 0xFF));
                    writer.Write((byte)(size & 0xFF));
                    // Data
                    writer.Write(record.Data!);
                }
            }

            // IPS footer: "EOF"
            writer.Write("EOF"u8);

            // If modified file is larger, append truncation extension
            if (modifiedLength != originalLength)
            {
                // Write 3-byte truncation size for IPS patches that change file size
                writer.Write((byte)((modifiedLength >> 16) & 0xFF));
                writer.Write((byte)((modifiedLength >> 8) & 0xFF));
                writer.Write((byte)(modifiedLength & 0xFF));
            }
        }
        catch
        {
            try { File.Delete(outputPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw;
        }
    }
}

public class IpsRecord
{
    public int Offset { get; set; }
    public byte[]? Data { get; set; }
    public bool IsRle { get; set; }
    public ushort RleSize { get; set; }
    public byte RleValue { get; set; }
}

public class PatchAnalysis
{
    public long OriginalSize { get; set; }
    public long ModifiedSize { get; set; }
    public int DifferingBytes { get; set; }
    public bool IsIdentical { get; set; }
    public bool CanCreateIps { get; set; }
}
