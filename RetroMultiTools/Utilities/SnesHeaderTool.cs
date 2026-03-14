namespace RetroMultiTools.Utilities;

public static class SnesHeaderTool
{
    private const int CopierHeaderSize = 512;
    private const int BufferSize = 81920;

    /// <summary>
    /// Checks whether the given SNES ROM file has a 512-byte copier header.
    /// A copier header is present when the file size modulo 1024 equals 512.
    /// </summary>
    public static bool HasCopierHeader(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found.", filePath);

        long length = new FileInfo(filePath).Length;
        return length > CopierHeaderSize && (length % 1024) == CopierHeaderSize;
    }

    /// <summary>
    /// Removes the 512-byte copier header from a SNES ROM file.
    /// </summary>
    public static async Task RemoveHeaderAsync(
        string inputPath,
        string outputPath,
        IProgress<string>? progress = null)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Input file not found.", inputPath);

        long inputLength = new FileInfo(inputPath).Length;
        if (inputLength <= CopierHeaderSize)
            throw new InvalidOperationException("File is too small to contain a copier header.");

        if ((inputLength % 1024) != CopierHeaderSize)
            throw new InvalidOperationException(
                "File does not appear to have a copier header (file size mod 1024 ≠ 512).");

        progress?.Report("Removing 512-byte copier header...");

        await Task.Run(() =>
        {
            try
            {
                using var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
                using var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize);

                input.Seek(CopierHeaderSize, SeekOrigin.Begin);

                byte[] buffer = new byte[BufferSize];
                int bytesRead;
                while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
                {
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

    /// <summary>
    /// Adds a 512-byte copier header (all zeros) to a SNES ROM file.
    /// </summary>
    public static async Task AddHeaderAsync(
        string inputPath,
        string outputPath,
        IProgress<string>? progress = null)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Input file not found.", inputPath);

        long inputLength = new FileInfo(inputPath).Length;
        if ((inputLength % 1024) == CopierHeaderSize)
            throw new InvalidOperationException(
                "File already appears to have a copier header (file size mod 1024 = 512).");

        progress?.Report("Adding 512-byte copier header...");

        await Task.Run(() =>
        {
            try
            {
                using var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
                using var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize);

                // Write 512 zero bytes as the copier header
                output.Write(new byte[CopierHeaderSize], 0, CopierHeaderSize);

                byte[] buffer = new byte[BufferSize];
                int bytesRead;
                while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
                {
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
}
