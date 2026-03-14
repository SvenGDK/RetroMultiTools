using System.Security.Cryptography;

namespace RetroMultiTools.Utilities;

public static class ChecksumCalculator
{
    private const int BufferSize = 81920;

    public static async Task<ChecksumResult> CalculateAsync(string filePath, IProgress<string>? progress = null)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found.", filePath);

        long fileSize = new FileInfo(filePath).Length;

        progress?.Report("Calculating CRC32...");
        string crc32 = await Task.Run(() => ComputeCrc32(filePath)).ConfigureAwait(false);

        progress?.Report("Calculating MD5...");
        string md5 = await ComputeHashAsync<MD5>(filePath).ConfigureAwait(false);

        progress?.Report("Calculating SHA-1...");
        string sha1 = await ComputeHashAsync<SHA1>(filePath).ConfigureAwait(false);

        progress?.Report("Calculating SHA-256...");
        string sha256 = await ComputeHashAsync<SHA256>(filePath).ConfigureAwait(false);

        progress?.Report("Done.");

        return new ChecksumResult
        {
            FilePath = filePath,
            FileSize = fileSize,
            CRC32 = crc32,
            MD5 = md5,
            SHA1 = sha1,
            SHA256 = sha256
        };
    }

    private static async Task<string> ComputeHashAsync<T>(string filePath) where T : HashAlgorithm
    {
        using var algorithm = typeof(T).Name switch
        {
            nameof(MD5) => (HashAlgorithm)MD5.Create(),
            nameof(SHA1) => (HashAlgorithm)SHA1.Create(),
            nameof(SHA256) => (HashAlgorithm)SHA256.Create(),
            _ => throw new InvalidOperationException($"Unsupported hash algorithm '{typeof(T).Name}'.")
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

public class ChecksumResult
{
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string CRC32 { get; set; } = string.Empty;
    public string MD5 { get; set; } = string.Empty;
    public string SHA1 { get; set; } = string.Empty;
    public string SHA256 { get; set; } = string.Empty;
}
