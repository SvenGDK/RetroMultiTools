namespace RetroMultiTools.Utilities;

public static class RomComparer
{
    private const int BufferSize = 81920;

    public static async Task<CompareResult> CompareAsync(string filePath1, string filePath2, IProgress<string>? progress = null)
    {
        if (!File.Exists(filePath1))
            throw new FileNotFoundException("First file not found.", filePath1);
        if (!File.Exists(filePath2))
            throw new FileNotFoundException("Second file not found.", filePath2);

        long fileSize1 = new FileInfo(filePath1).Length;
        long fileSize2 = new FileInfo(filePath2).Length;

        progress?.Report("Comparing files...");

        var (firstMismatchOffset, differingByteCount) = await Task.Run(() =>
        {
            using var stream1 = new FileStream(filePath1, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
            using var stream2 = new FileStream(filePath2, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);

            byte[] buffer1 = new byte[BufferSize];
            byte[] buffer2 = new byte[BufferSize];
            long offset = 0;
            long firstMismatch = -1;
            long diffCount = 0;

            while (true)
            {
                int read1 = ReadFully(stream1, buffer1);
                int read2 = ReadFully(stream2, buffer2);

                int minRead = Math.Min(read1, read2);
                for (int i = 0; i < minRead; i++)
                {
                    if (buffer1[i] != buffer2[i])
                    {
                        diffCount++;
                        if (firstMismatch < 0)
                            firstMismatch = offset + i;
                    }
                }

                // Bytes beyond the shorter read are all differences
                int maxRead = Math.Max(read1, read2);
                diffCount += maxRead - minRead;
                if (firstMismatch < 0 && read1 != read2)
                    firstMismatch = offset + minRead;

                offset += maxRead;

                if (read1 == 0 && read2 == 0)
                    break;
            }

            return (firstMismatch, diffCount);
        }).ConfigureAwait(false);

        bool identical = fileSize1 == fileSize2 && differingByteCount == 0;

        progress?.Report("Done.");

        return new CompareResult
        {
            FileSize1 = fileSize1,
            FileSize2 = fileSize2,
            Identical = identical,
            DifferingByteCount = differingByteCount,
            FirstMismatchOffset = firstMismatchOffset
        };
    }

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

public class CompareResult
{
    public long FileSize1 { get; set; }
    public long FileSize2 { get; set; }
    public bool Identical { get; set; }
    public long DifferingByteCount { get; set; }
    public long FirstMismatchOffset { get; set; } = -1;

    public string Summary
    {
        get
        {
            if (Identical)
                return "✔ Files are identical.";

            var parts = new List<string>();
            if (FileSize1 != FileSize2)
                parts.Add($"Size difference: {FileUtils.FormatFileSize(FileSize1)} vs {FileUtils.FormatFileSize(FileSize2)}");
            parts.Add($"Differing bytes: {DifferingByteCount:N0}");
            if (FirstMismatchOffset >= 0)
                parts.Add($"First mismatch at offset: 0x{FirstMismatchOffset:X} ({FirstMismatchOffset:N0})");

            return "✘ Files differ.\n" + string.Join("\n", parts);
        }
    }
}
