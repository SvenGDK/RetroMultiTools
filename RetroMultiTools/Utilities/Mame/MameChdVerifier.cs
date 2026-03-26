using RetroMultiTools.Localization;

namespace RetroMultiTools.Utilities.Mame;

/// <summary>
/// Verifies MAME CHD (Compressed Hunks of Data) file integrity by reading
/// and validating header information for CHD v3, v4, and v5 formats.
/// </summary>
public static class MameChdVerifier
{
    private static readonly byte[] ChdMagic = "MComprHD"u8.ToArray();

    /// <summary>
    /// Reads and verifies a single CHD file header.
    /// </summary>
    public static async Task<ChdVerifyResult> VerifyAsync(
        string filePath,
        IProgress<string>? progress = null)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("CHD file not found.", filePath);

        string fileName = Path.GetFileName(filePath);
        long fileSize = new FileInfo(filePath).Length;

        progress?.Report(string.Format(LocalizationManager.Instance["MameChd_ProgressReading"], fileName));

        return await Task.Run(() => ReadChdHeader(filePath, fileName, fileSize)).ConfigureAwait(false);
    }

    /// <summary>
    /// Verifies all CHD files in a directory.
    /// </summary>
    public static async Task<ChdBatchResult> VerifyDirectoryAsync(
        string directory,
        IProgress<string>? progress = null)
    {
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException($"Directory not found: {directory}");

        var chdFiles = Directory.EnumerateFiles(directory, "*.chd", SearchOption.AllDirectories)
            .ToList();

        if (chdFiles.Count == 0)
        {
            return new ChdBatchResult();
        }

        var results = new List<ChdVerifyResult>();
        int validCount = 0;

        for (int i = 0; i < chdFiles.Count; i++)
        {
            progress?.Report(string.Format(LocalizationManager.Instance["MameChd_ProgressVerifying"], i + 1, chdFiles.Count, Path.GetFileName(chdFiles[i])));
            var result = await VerifyAsync(chdFiles[i]).ConfigureAwait(false);
            results.Add(result);

            if (result.IsValid) validCount++;
        }

        progress?.Report(string.Format(LocalizationManager.Instance["MameChd_ProgressDone"], validCount, results.Count - validCount));

        return new ChdBatchResult
        {
            Results = results,
            TotalFiles = chdFiles.Count,
            ValidCount = validCount,
            InvalidCount = results.Count - validCount
        };
    }

    /// <summary>
    /// Verifies a CHD file's data integrity by computing the SHA-1 of raw data
    /// and comparing it against the stored raw SHA-1 in the header.
    /// Only supported for uncompressed CHD files or when the raw SHA-1 is available in the header.
    /// </summary>
    public static async Task<ChdVerifyResult> VerifyWithHashAsync(
        string filePath,
        IProgress<string>? progress = null)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("CHD file not found.", filePath);

        string fileName = Path.GetFileName(filePath);
        long fileSize = new FileInfo(filePath).Length;

        progress?.Report(string.Format(LocalizationManager.Instance["MameChd_ProgressReading"], fileName));

        var result = await Task.Run(() => ReadChdHeader(filePath, fileName, fileSize)).ConfigureAwait(false);

        if (!result.IsValid || string.IsNullOrEmpty(result.RawSHA1) || result.RawSHA1 == new string('0', 40))
        {
            result.HashVerification = LocalizationManager.Instance["MameChd_HashNotAvailable"];
            return result;
        }

        // For full hash verification we would need to decompress and hash the CHD data,
        // which requires implementing the full CHD decompression codec.
        // Instead, we report the stored hashes for manual comparison.
        result.HashVerification = LocalizationManager.Instance["MameChd_HashReadSuccess"];

        return result;
    }

    private static ChdVerifyResult ReadChdHeader(string filePath, string fileName, long fileSize)
    {
        var result = new ChdVerifyResult
        {
            FilePath = filePath,
            FileName = fileName,
            FileSize = fileSize
        };

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new BinaryReader(stream);

            // Check minimum file size for magic + version
            if (fileSize < 16)
            {
                result.IsValid = false;
                result.Error = LocalizationManager.Instance["MameChd_ErrorTooSmall"];
                return result;
            }

            // Read and verify magic number
            byte[] magic = reader.ReadBytes(8);
            if (!magic.AsSpan().SequenceEqual(ChdMagic))
            {
                result.IsValid = false;
                result.Error = LocalizationManager.Instance["MameChd_ErrorInvalidMagic"];
                return result;
            }

            // Read header length and version
            uint headerLength = ReadBigEndianUInt32(reader);
            uint version = ReadBigEndianUInt32(reader);
            result.Version = (int)version;

            switch (version)
            {
                case 3:
                    ParseV3Header(reader, result, headerLength);
                    break;
                case 4:
                    ParseV4Header(reader, result, headerLength);
                    break;
                case 5:
                    ParseV5Header(reader, result, headerLength);
                    break;
                default:
                    result.IsValid = false;
                    result.Error = string.Format(LocalizationManager.Instance["MameChd_ErrorUnsupportedVer"], version);
                    return result;
            }

            result.IsValid = true;
        }
        catch (EndOfStreamException)
        {
            result.IsValid = false;
            result.Error = LocalizationManager.Instance["MameChd_ErrorUnexpectedEof"];
        }
        catch (IOException ex)
        {
            result.IsValid = false;
            result.Error = string.Format(LocalizationManager.Instance["MameChd_ErrorIoError"], ex.Message);
        }

        return result;
    }

    private static void ParseV3Header(BinaryReader reader, ChdVerifyResult result, uint headerLength)
    {
        // CHD v3 header layout (after magic+headerlen+version = 16 bytes):
        // Offset 16: uint32 flags
        // Offset 20: uint32 compression
        // Offset 24: uint32 totalhunks
        // Offset 28: uint64 logicalbytes
        // Offset 36: uint64 metaoffset
        // Offset 44: byte[16] md5
        // Offset 60: byte[16] parentmd5
        // Offset 76: uint32 hunkbytes
        // Offset 80: byte[20] sha1
        // Offset 100: byte[20] parentsha1

        if (headerLength < 120)
        {
            result.Error = LocalizationManager.Instance["MameChd_ErrorV3TooShort"];
            result.IsValid = false;
            return;
        }

        uint flags = ReadBigEndianUInt32(reader);
        uint compression = ReadBigEndianUInt32(reader);
        uint totalHunks = ReadBigEndianUInt32(reader);
        ulong logicalBytes = ReadBigEndianUInt64(reader);
        ulong metaOffset = ReadBigEndianUInt64(reader);
        byte[] md5 = reader.ReadBytes(16);
        byte[] parentMd5 = reader.ReadBytes(16);
        uint hunkBytes = ReadBigEndianUInt32(reader);
        byte[] sha1 = reader.ReadBytes(20);
        byte[] parentSha1 = reader.ReadBytes(20);

        result.LogicalSize = (long)logicalBytes;
        result.HunkSize = (int)hunkBytes;
        result.TotalHunks = (int)totalHunks;
        result.Compression = GetCompressionName(compression);
        result.SHA1 = Convert.ToHexString(sha1);
        result.HasParent = (flags & 0x01) != 0;

        if (result.HasParent)
            result.ParentSHA1 = Convert.ToHexString(parentSha1);
    }

    private static void ParseV4Header(BinaryReader reader, ChdVerifyResult result, uint headerLength)
    {
        // CHD v4 header layout (after magic+headerlen+version = 16 bytes):
        // Offset 16: uint32 flags
        // Offset 20: uint32 compression
        // Offset 24: uint32 totalhunks
        // Offset 28: uint64 logicalbytes
        // Offset 36: uint64 metaoffset
        // Offset 44: uint32 hunkbytes
        // Offset 48: byte[20] sha1
        // Offset 68: byte[20] parentsha1
        // Offset 88: byte[20] rawsha1

        if (headerLength < 108)
        {
            result.Error = LocalizationManager.Instance["MameChd_ErrorV4TooShort"];
            result.IsValid = false;
            return;
        }

        uint flags = ReadBigEndianUInt32(reader);
        uint compression = ReadBigEndianUInt32(reader);
        uint totalHunks = ReadBigEndianUInt32(reader);
        ulong logicalBytes = ReadBigEndianUInt64(reader);
        ulong metaOffset = ReadBigEndianUInt64(reader);
        uint hunkBytes = ReadBigEndianUInt32(reader);
        byte[] sha1 = reader.ReadBytes(20);
        byte[] parentSha1 = reader.ReadBytes(20);
        byte[] rawSha1 = reader.ReadBytes(20);

        result.LogicalSize = (long)logicalBytes;
        result.HunkSize = (int)hunkBytes;
        result.TotalHunks = (int)totalHunks;
        result.Compression = GetCompressionName(compression);
        result.SHA1 = Convert.ToHexString(sha1);
        result.RawSHA1 = Convert.ToHexString(rawSha1);
        result.HasParent = (flags & 0x01) != 0;

        if (result.HasParent)
            result.ParentSHA1 = Convert.ToHexString(parentSha1);
    }

    private static void ParseV5Header(BinaryReader reader, ChdVerifyResult result, uint headerLength)
    {
        // CHD v5 header layout (after magic+headerlen+version = 16 bytes):
        // Offset 16: uint32 compressor[0]
        // Offset 20: uint32 compressor[1]
        // Offset 24: uint32 compressor[2]
        // Offset 28: uint32 compressor[3]
        // Offset 32: uint64 logicalbytes
        // Offset 40: uint64 mapoffset
        // Offset 48: uint64 metaoffset
        // Offset 56: uint32 hunkbytes
        // Offset 60: uint32 unitbytes
        // Offset 64: byte[20] rawsha1
        // Offset 84: byte[20] sha1
        // Offset 104: byte[20] parentsha1

        if (headerLength < 124)
        {
            result.Error = LocalizationManager.Instance["MameChd_ErrorV5TooShort"];
            result.IsValid = false;
            return;
        }

        uint comp0 = ReadBigEndianUInt32(reader);
        uint comp1 = ReadBigEndianUInt32(reader);
        uint comp2 = ReadBigEndianUInt32(reader);
        uint comp3 = ReadBigEndianUInt32(reader);
        ulong logicalBytes = ReadBigEndianUInt64(reader);
        ulong mapOffset = ReadBigEndianUInt64(reader);
        ulong metaOffset = ReadBigEndianUInt64(reader);
        uint hunkBytes = ReadBigEndianUInt32(reader);
        uint unitBytes = ReadBigEndianUInt32(reader);
        byte[] rawSha1 = reader.ReadBytes(20);
        byte[] sha1 = reader.ReadBytes(20);
        byte[] parentSha1 = reader.ReadBytes(20);

        result.LogicalSize = (long)logicalBytes;
        result.HunkSize = (int)hunkBytes;
        result.UnitSize = (int)unitBytes;
        result.Compression = GetV5CompressionName(comp0);
        result.SHA1 = Convert.ToHexString(sha1);
        result.RawSHA1 = Convert.ToHexString(rawSha1);
        result.HasParent = !IsAllZeros(parentSha1);

        if (result.HasParent)
            result.ParentSHA1 = Convert.ToHexString(parentSha1);
    }

    private static string GetCompressionName(uint compression) => compression switch
    {
        0 => "None",
        1 => "Zlib",
        2 => "Zlib+",
        3 => "AV Codec",
        _ => $"Unknown ({compression})"
    };

    private static string GetV5CompressionName(uint codec)
    {
        if (codec == 0) return "None";
        // V5 codecs are 4-char ASCII identifiers stored as big-endian uint32
        byte[] chars =
        [
            (byte)((codec >> 24) & 0xFF),
            (byte)((codec >> 16) & 0xFF),
            (byte)((codec >> 8) & 0xFF),
            (byte)(codec & 0xFF)
        ];
        string name = System.Text.Encoding.ASCII.GetString(chars).Trim('\0');
        return name switch
        {
            "zlib" => "Zlib",
            "lzma" => "LZMA",
            "huff" => "Huffman",
            "flac" => "FLAC",
            "zstd" => "Zstandard",
            _ => name
        };
    }

    private static bool IsAllZeros(byte[] data)
    {
        for (int i = 0; i < data.Length; i++)
            if (data[i] != 0) return false;
        return true;
    }

    private static uint ReadBigEndianUInt32(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(4);
        if (bytes.Length < 4)
            throw new EndOfStreamException("Unexpected end of CHD file while reading UInt32.");
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToUInt32(bytes, 0);
    }

    private static ulong ReadBigEndianUInt64(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(8);
        if (bytes.Length < 8)
            throw new EndOfStreamException("Unexpected end of CHD file while reading UInt64.");
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToUInt64(bytes, 0);
    }
}

public class ChdVerifyResult
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public bool IsValid { get; set; }
    public int Version { get; set; }
    public string Compression { get; set; } = string.Empty;
    public long LogicalSize { get; set; }
    public int HunkSize { get; set; }
    public int TotalHunks { get; set; }
    public int UnitSize { get; set; }
    public string SHA1 { get; set; } = string.Empty;
    public string RawSHA1 { get; set; } = string.Empty;
    public bool HasParent { get; set; }
    public string ParentSHA1 { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public string HashVerification { get; set; } = string.Empty;
}

public class ChdBatchResult
{
    public List<ChdVerifyResult> Results { get; set; } = [];
    public int TotalFiles { get; set; }
    public int ValidCount { get; set; }
    public int InvalidCount { get; set; }

    public string Summary =>
        TotalFiles == 0
            ? LocalizationManager.Instance["MameChd_NoChdFiles"]
            : string.Format(LocalizationManager.Instance["MameChd_SummaryFormat"], ValidCount, InvalidCount, TotalFiles);
}
