namespace RetroMultiTools.Utilities;

public static class IpsPatcher
{
    private static readonly byte[] IpsHeader = [0x50, 0x41, 0x54, 0x43, 0x48]; // "PATCH"
    private static readonly byte[] IpsEof = [0x45, 0x4F, 0x46];             // "EOF"

    // Maximum file size for File.ReadAllBytes() to avoid OutOfMemoryException
    private const long MaxFileSize = 512L * 1024 * 1024; // 512 MB

    public static void Apply(string romPath, string patchPath, string outputPath)
    {
        ValidateFileSize(romPath, "ROM");
        ValidateFileSize(patchPath, "Patch");

        byte[] rom = File.ReadAllBytes(romPath);
        byte[] patch = File.ReadAllBytes(patchPath);

        if (patch.Length < 5 || !patch.AsSpan(0, 5).SequenceEqual(IpsHeader))
            throw new InvalidDataException("Not a valid IPS patch (missing PATCH header).");

        int pos = 5;
        byte[] output = new byte[rom.Length];
        Array.Copy(rom, output, rom.Length);
        int outputLen = output.Length;

        while (pos + 2 < patch.Length)
        {
            if (IsEof(patch, pos))
                break;

            if (pos + 4 >= patch.Length)
                throw new InvalidDataException("Unexpected end of IPS patch.");

            int offset = (patch[pos] << 16) | (patch[pos + 1] << 8) | patch[pos + 2];
            pos += 3;
            int size = (patch[pos] << 8) | patch[pos + 1];
            pos += 2;

            if (size == 0)
            {
                if (pos + 2 >= patch.Length)
                    throw new InvalidDataException("Truncated RLE record.");
                int rleCount = (patch[pos] << 8) | patch[pos + 1];
                pos += 2;
                byte rleByte = patch[pos++];

                int required = offset + rleCount;
                if (required > outputLen)
                {
                    Array.Resize(ref output, required);
                    outputLen = required;
                }
                Array.Fill(output, rleByte, offset, rleCount);
            }
            else
            {
                if (pos + size > patch.Length)
                    throw new InvalidDataException("Patch record exceeds patch file size.");

                int required = offset + size;
                if (required > outputLen)
                {
                    Array.Resize(ref output, required);
                    outputLen = required;
                }
                Buffer.BlockCopy(patch, pos, output, offset, size);
                pos += size;
            }
        }

        if (IsEof(patch, pos))
        {
            pos += 3;
            if (pos + 3 <= patch.Length)
            {
                int truncSize = (patch[pos] << 16) | (patch[pos + 1] << 8) | patch[pos + 2];
                if (truncSize < outputLen)
                    outputLen = truncSize;
            }
        }

        if (outputLen < output.Length)
            Array.Resize(ref output, outputLen);

        try
        {
            File.WriteAllBytes(outputPath, output);
        }
        catch
        {
            try { File.Delete(outputPath); } catch { /* best-effort cleanup */ }
            throw;
        }
    }

    private static bool IsEof(byte[] patch, int pos) =>
        pos + 2 < patch.Length &&
        patch[pos] == IpsEof[0] && patch[pos + 1] == IpsEof[1] && patch[pos + 2] == IpsEof[2];

    private static void ValidateFileSize(string filePath, string fileDescription)
    {
        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
            throw new FileNotFoundException($"{fileDescription} file not found.", filePath);
        if (fileInfo.Length > MaxFileSize)
            throw new InvalidOperationException(
                $"{fileDescription} file is too large ({fileInfo.Length / (1024 * 1024)} MB). " +
                $"Maximum supported size for IPS patching is {MaxFileSize / (1024 * 1024)} MB.");
    }
}
