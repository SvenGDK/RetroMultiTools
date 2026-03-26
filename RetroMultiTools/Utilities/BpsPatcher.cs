namespace RetroMultiTools.Utilities;

public static class BpsPatcher
{
    // Maximum file size for File.ReadAllBytes() to avoid OutOfMemoryException
    private const long MaxFileSize = 512L * 1024 * 1024; // 512 MB

    public static void Apply(string sourcePath, string patchPath, string outputPath)
    {
        ValidateFileSize(sourcePath, "Source");
        ValidateFileSize(patchPath, "Patch");

        byte[] source = File.ReadAllBytes(sourcePath);
        byte[] patch = File.ReadAllBytes(patchPath);

        if (patch.Length < 4 || patch[0] != 'B' || patch[1] != 'P' || patch[2] != 'S' || patch[3] != '1')
            throw new InvalidDataException("Not a valid BPS patch (missing BPS1 magic).");

        // A valid BPS patch requires at least the 4-byte header + 12-byte footer (3 CRC32 values)
        if (patch.Length < 16)
            throw new InvalidDataException("BPS patch is too small to be valid.");

        // Validate patch CRC32 (covers all bytes except the last 4)
        uint expectedPatchCrc = ReadUInt32LE(patch, patch.Length - 4);
        uint actualPatchCrc = ComputeCrc32(patch, 0, patch.Length - 4);
        if (actualPatchCrc != expectedPatchCrc)
            throw new InvalidDataException($"Patch CRC32 mismatch: expected {expectedPatchCrc:X8}, got {actualPatchCrc:X8}.");

        // Validate source CRC32
        uint expectedSourceCrc = ReadUInt32LE(patch, patch.Length - 12);
        uint actualSourceCrc = ComputeCrc32(source, 0, source.Length);
        if (actualSourceCrc != expectedSourceCrc)
            throw new InvalidDataException($"Source CRC32 mismatch: expected {expectedSourceCrc:X8}, got {actualSourceCrc:X8}. Wrong source file?");

        int pos = 4;

        long sourceSize = ReadVarInt(patch, ref pos);
        long targetSize = ReadVarInt(patch, ref pos);
        long metaSize = ReadVarInt(patch, ref pos);

        if (sourceSize != source.Length)
            throw new InvalidDataException($"Source file size mismatch: patch expects {sourceSize} bytes, got {source.Length}.");

        if (targetSize < 0 || targetSize > int.MaxValue)
            throw new InvalidDataException($"Invalid target size: {targetSize}.");

        if (metaSize < 0 || metaSize > int.MaxValue || (long)pos + metaSize > patch.Length)
            throw new InvalidDataException($"Invalid metadata size: {metaSize}.");

        pos += (int)metaSize;

        byte[] target = new byte[targetSize];
        int sourcePos = 0;
        int targetPos = 0;
        int targetRelPos = 0;

        int patchEnd = patch.Length - 12;

        while (pos < patchEnd)
        {
            long data = ReadVarInt(patch, ref pos);
            long action = data & 3;
            long length = (data >> 2) + 1;

            switch (action)
            {
                case 0: // SourceRead: copies from source at the same position as target (parallel read)
                    if (targetPos + length > target.Length)
                        throw new InvalidDataException("SourceRead extends beyond target buffer.");
                    for (long i = 0; i < length; i++)
                        target[targetPos + i] = targetPos + i < source.Length ? source[targetPos + i] : (byte)0;
                    targetPos += (int)length;
                    break;

                case 1: // TargetRead
                    if (targetPos + length > target.Length)
                        throw new InvalidDataException("TargetRead extends beyond target buffer.");
                    if (pos + length > patchEnd)
                        throw new InvalidDataException("TargetRead extends beyond patch data.");
                    Buffer.BlockCopy(patch, pos, target, targetPos, (int)length);
                    pos += (int)length;
                    targetPos += (int)length;
                    break;

                case 2: // SourceCopy
                    {
                        long offset = ReadVarInt(patch, ref pos);
                        sourcePos += (int)((offset & 1) != 0 ? -(offset >> 1) : (offset >> 1));
                        if (sourcePos < 0)
                            throw new InvalidDataException("SourceCopy offset is out of bounds (negative position).");
                        if (targetPos + length > target.Length)
                            throw new InvalidDataException("SourceCopy extends beyond target buffer.");
                        for (long i = 0; i < length; i++)
                        {
                            target[targetPos++] = sourcePos < source.Length ? source[sourcePos] : (byte)0;
                            sourcePos++;
                        }
                        break;
                    }

                case 3: // TargetCopy
                    {
                        long offset = ReadVarInt(patch, ref pos);
                        targetRelPos += (int)((offset & 1) != 0 ? -(offset >> 1) : (offset >> 1));
                        if (targetRelPos < 0 || targetRelPos + length > target.Length)
                            throw new InvalidDataException("TargetCopy offset is out of bounds.");
                        if (targetPos + length > target.Length)
                            throw new InvalidDataException("TargetCopy extends beyond target buffer.");
                        for (long i = 0; i < length; i++)
                            target[targetPos++] = target[targetRelPos++];
                        break;
                    }
            }
        }

        // Validate target CRC32
        uint expectedTargetCrc = ReadUInt32LE(patch, patch.Length - 8);
        uint actualTargetCrc = ComputeCrc32(target, 0, target.Length);
        if (actualTargetCrc != expectedTargetCrc)
            throw new InvalidDataException($"Target CRC32 mismatch: expected {expectedTargetCrc:X8}, got {actualTargetCrc:X8}. Patch may be corrupt.");

        try
        {
            File.WriteAllBytes(outputPath, target);
        }
        catch
        {
            try { File.Delete(outputPath); } catch { /* best-effort cleanup */ }
            throw;
        }
    }

    private static long ReadVarInt(byte[] data, ref int pos)
    {
        long result = 0;
        int shift = 0;
        while (true)
        {
            if (pos >= data.Length) throw new InvalidDataException("Unexpected end of BPS data.");
            byte b = data[pos++];
            result += (long)(b & 0x7F) << shift;
            if ((b & 0x80) != 0) break;
            shift += 7;
            if (shift > 63)
                throw new InvalidDataException("BPS variable-length integer is too large.");
            result += (long)1 << shift;
        }
        return result;
    }

    private static uint ReadUInt32LE(byte[] data, int offset) =>
        (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));

    private static uint ComputeCrc32(byte[] data, int offset, int length)
    {
        uint crc = 0xFFFFFFFF;
        for (int i = offset; i < offset + length; i++)
            crc = (crc >> 8) ^ Crc32Table[(crc ^ data[i]) & 0xFF];
        return crc ^ 0xFFFFFFFF;
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

    private static void ValidateFileSize(string filePath, string fileDescription)
    {
        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
            throw new FileNotFoundException($"{fileDescription} file not found.", filePath);
        if (fileInfo.Length > MaxFileSize)
            throw new InvalidOperationException(
                $"{fileDescription} file is too large ({fileInfo.Length / (1024 * 1024)} MB). " +
                $"Maximum supported size for BPS patching is {MaxFileSize / (1024 * 1024)} MB.");
    }
}
