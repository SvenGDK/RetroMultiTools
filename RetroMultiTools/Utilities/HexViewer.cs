using System.Text;

namespace RetroMultiTools.Utilities;

public static class HexViewer
{
    public const int DefaultBytesPerRow = 16;
    public const int DefaultPageSize = 256; // 16 rows × 16 bytes

    public static HexViewData LoadPage(string filePath, long offset, int pageSize = DefaultPageSize)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found.", filePath);

        var fileInfo = new FileInfo(filePath);
        long fileSize = fileInfo.Length;

        if (offset < 0) offset = 0;
        if (offset >= fileSize) offset = Math.Max(0, fileSize - pageSize);

        int bytesToRead = (int)Math.Min(pageSize, fileSize - offset);
        byte[] data = new byte[bytesToRead];

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        stream.Seek(offset, SeekOrigin.Begin);
        int totalRead = 0;
        while (totalRead < bytesToRead)
        {
            int read = stream.Read(data, totalRead, bytesToRead - totalRead);
            if (read == 0) break;
            totalRead += read;
        }

        if (totalRead < bytesToRead)
            Array.Resize(ref data, totalRead);

        return new HexViewData
        {
            FilePath = filePath,
            FileSize = fileSize,
            Offset = offset,
            Data = data,
            FormattedLines = FormatHexLines(data, offset)
        };
    }

    public static List<string> FormatHexLines(byte[] data, long baseOffset, int bytesPerRow = DefaultBytesPerRow)
    {
        var lines = new List<string>();
        var sb = new StringBuilder();

        for (int i = 0; i < data.Length; i += bytesPerRow)
        {
            sb.Clear();

            // Offset column
            sb.Append($"{baseOffset + i:X8}  ");

            // Hex columns
            int count = Math.Min(bytesPerRow, data.Length - i);
            for (int j = 0; j < bytesPerRow; j++)
            {
                if (j < count)
                    sb.Append($"{data[i + j]:X2} ");
                else
                    sb.Append("   ");

                if (j == 7) sb.Append(' ');
            }

            sb.Append(" |");

            // ASCII column
            for (int j = 0; j < count; j++)
            {
                byte b = data[i + j];
                sb.Append(b is >= 0x20 and <= 0x7E ? (char)b : '.');
            }

            sb.Append('|');
            lines.Add(sb.ToString());
        }

        return lines;
    }

    public static List<long> SearchBytes(string filePath, byte[] pattern, long startOffset = 0, int maxResults = 100)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found.", filePath);
        if (pattern.Length == 0)
            return [];

        var results = new List<long>();
        const int searchBufferSize = 81920;
        byte[] buffer = new byte[searchBufferSize];

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        stream.Seek(startOffset, SeekOrigin.Begin);

        long position = startOffset;
        int carryOver = 0;
        byte[] carryBuffer = new byte[pattern.Length - 1];

        while (results.Count < maxResults)
        {
            int bytesRead = stream.Read(buffer, carryOver, searchBufferSize - carryOver);
            if (bytesRead == 0) break;

            if (carryOver > 0)
            {
                Array.Copy(carryBuffer, 0, buffer, 0, carryOver);
                bytesRead += carryOver;
            }

            int searchLen = bytesRead - pattern.Length + 1;
            for (int i = 0; i < searchLen && results.Count < maxResults; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (buffer[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                    results.Add(position + i);
            }

            // Keep carry-over bytes for cross-boundary matches
            if (bytesRead >= pattern.Length)
            {
                carryOver = pattern.Length - 1;
                Array.Copy(buffer, bytesRead - carryOver, carryBuffer, 0, carryOver);
            }
            else
            {
                carryOver = 0;
            }

            position += bytesRead - carryOver;
        }

        return results;
    }

    public static byte[] ParseHexString(string hex)
    {
        hex = hex.Replace(" ", "").Replace("-", "");
        if (hex.Length % 2 != 0)
            throw new FormatException("Hex string must have an even number of characters.");

        byte[] bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return bytes;
    }
}

public class HexViewData
{
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public long Offset { get; set; }
    public byte[] Data { get; set; } = [];
    public List<string> FormattedLines { get; set; } = [];
    public long TotalPages => (FileSize + HexViewer.DefaultPageSize - 1) / HexViewer.DefaultPageSize;
    public long CurrentPage => Offset / HexViewer.DefaultPageSize;
}
