namespace RetroMultiTools.Utilities.Mame;

/// <summary>
/// Shared CRC32 helper used by MAME utilities for checksum computation.
/// Uses the standard CRC-32/ISO-HDLC polynomial (0xEDB88320).
/// </summary>
internal static class MameCrc32
{
    internal static readonly uint[] Table = GenerateTable();

    private static uint[] GenerateTable()
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

    /// <summary>
    /// Computes the CRC32 of a stream and returns it as an 8-character uppercase hex string.
    /// </summary>
    internal static string ComputeHex(Stream stream)
    {
        uint crc = 0xFFFFFFFF;
        byte[] buffer = new byte[8192];
        int bytesRead;

        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < bytesRead; i++)
                crc = (crc >> 8) ^ Table[(crc ^ buffer[i]) & 0xFF];
        }

        return (crc ^ 0xFFFFFFFF).ToString("X8");
    }
}
