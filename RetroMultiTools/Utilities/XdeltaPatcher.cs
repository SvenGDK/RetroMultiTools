namespace RetroMultiTools.Utilities;

/// <summary>
/// Applies xDelta/VCDIFF patches (RFC 3284) to ROM files.
/// Supports the standard VCDIFF format and the xdelta3 Adler32 checksum extension.
/// Does not support secondary compression or application-defined code tables.
/// </summary>
public static class XdeltaPatcher
{
    // Maximum file size for File.ReadAllBytes() to avoid OutOfMemoryException
    private const long MaxFileSize = 512L * 1024 * 1024; // 512 MB

    private static readonly byte[] VcdiffMagic = [0xD6, 0xC3, 0xC4, 0x00];

    // Header indicator flags (RFC 3284 Section 4.1)
    private const byte VCD_DECOMPRESS = 0x01;
    private const byte VCD_CODETABLE = 0x02;
    private const byte VCD_APPHEADER = 0x04;

    // Window indicator flags (RFC 3284 Section 4.2)
    private const byte VCD_SOURCE = 0x01;
    private const byte VCD_TARGET = 0x02;
    private const byte VCD_ADLER32 = 0x04; // xdelta3 extension

    // Delta indicator flags (RFC 3284 Section 4.3)
    private const byte VCD_DATACOMP = 0x01;
    private const byte VCD_INSTCOMP = 0x02;
    private const byte VCD_ADDRCOMP = 0x04;

    // Instruction types (RFC 3284 Section 5.4)
    private const byte INST_NOOP = 0;
    private const byte INST_ADD = 1;
    private const byte INST_RUN = 2;
    private const byte INST_COPY = 3;

    // Default address cache parameters (RFC 3284 Section 5.1)
    private const int DefaultNearSize = 4;
    private const int DefaultSameSize = 3;

    /// <summary>
    /// Applies a VCDIFF/xDelta patch to a source ROM and writes the patched output.
    /// </summary>
    public static void Apply(string sourcePath, string patchPath, string outputPath)
    {
        ValidateFileSize(sourcePath, "Source");
        ValidateFileSize(patchPath, "Patch");

        byte[] source = File.ReadAllBytes(sourcePath);
        byte[] patch = File.ReadAllBytes(patchPath);

        if (patch.Length < 5 || !patch.AsSpan(0, 4).SequenceEqual(VcdiffMagic))
            throw new InvalidDataException("Not a valid VCDIFF/xDelta patch (missing VCDIFF header).");

        int pos = 4;
        byte hdrIndicator = patch[pos++];

        if ((hdrIndicator & VCD_DECOMPRESS) != 0)
        {
            // VCD_DECOMPRESS (RFC 3284 Section 4.1) indicates a secondary compressor ID
            // byte follows. xdelta3 commonly sets this flag. If the compressor ID is 0,
            // no actual secondary compression is applied.
            if (pos >= patch.Length)
                throw new InvalidDataException("Missing secondary compressor ID byte.");
            byte secondaryCompressor = patch[pos++];
            if (secondaryCompressor != 0)
                throw new NotSupportedException(
                    $"VCDIFF secondary compression (compressor ID {secondaryCompressor}) is not supported.");
        }

        if ((hdrIndicator & VCD_CODETABLE) != 0)
        {
            // Read and skip the code table length; we don't support custom tables
            int codeTableLen = ReadVarInt(patch, ref pos);
            if (pos + codeTableLen > patch.Length)
                throw new InvalidDataException("Code table data extends beyond patch file.");
            throw new NotSupportedException("Application-defined VCDIFF code tables are not supported.");
        }

        if ((hdrIndicator & VCD_APPHEADER) != 0)
        {
            int appDataLen = ReadVarInt(patch, ref pos);
            if (pos + appDataLen > patch.Length)
                throw new InvalidDataException("Application header data extends beyond patch file.");
            pos += appDataLen;
        }

        var codeTable = BuildDefaultCodeTable();

        using var output = new MemoryStream();

        while (pos < patch.Length)
        {
            DecodeWindow(patch, ref pos, source, output, codeTable);
        }

        byte[] result = output.ToArray();

        try
        {
            File.WriteAllBytes(outputPath, result);
        }
        catch
        {
            try { File.Delete(outputPath); } catch { /* best-effort cleanup */ }
            throw;
        }
    }

    private static void DecodeWindow(byte[] patch, ref int pos, byte[] source,
        MemoryStream output, CodeTableEntry[] codeTable)
    {
        if (pos >= patch.Length)
            throw new InvalidDataException("Unexpected end of VCDIFF patch data.");

        byte winIndicator = patch[pos++];

        byte[]? srcSegment = null;
        int srcSegmentLen = 0;

        if ((winIndicator & (VCD_SOURCE | VCD_TARGET)) != 0)
        {
            srcSegmentLen = ReadVarInt(patch, ref pos);
            int srcSegmentPos = ReadVarInt(patch, ref pos);

            if ((winIndicator & VCD_SOURCE) != 0)
            {
                if ((long)srcSegmentPos + srcSegmentLen > source.Length)
                    throw new InvalidDataException("Source segment extends beyond source file.");
                srcSegment = new byte[srcSegmentLen];
                Array.Copy(source, srcSegmentPos, srcSegment, 0, srcSegmentLen);
            }
            else // VCD_TARGET: copy from already-decoded output
            {
                if ((long)srcSegmentPos + srcSegmentLen > output.Length)
                    throw new InvalidDataException("Target segment extends beyond decoded output.");
                srcSegment = new byte[srcSegmentLen];
                long savedPos = output.Position;
                output.Position = srcSegmentPos;
                int read = output.Read(srcSegment, 0, srcSegmentLen);
                if (read != srcSegmentLen)
                    throw new InvalidDataException("Could not read target segment from output.");
                output.Position = savedPos;
            }
        }

        int deltaLen = ReadVarInt(patch, ref pos);
        int deltaStart = pos;

        if (deltaStart + deltaLen > patch.Length)
            throw new InvalidDataException("Delta encoding extends beyond patch file.");

        int targetWindowLen = ReadVarInt(patch, ref pos);
        byte deltaIndicator = patch[pos++];

        if ((deltaIndicator & (VCD_DATACOMP | VCD_INSTCOMP | VCD_ADDRCOMP)) != 0)
            throw new NotSupportedException(
                "Compressed delta sections are not supported. "
                + "The patch may have been created with secondary compression enabled.");

        int dataLen = ReadVarInt(patch, ref pos);
        int instLen = ReadVarInt(patch, ref pos);
        int addrLen = ReadVarInt(patch, ref pos);

        // xdelta3 extension: Adler32 checksum stored before the section data
        int checksumPos = -1;
        if ((winIndicator & VCD_ADLER32) != 0)
        {
            checksumPos = pos;
            pos += 4; // skip the 4-byte checksum for now, validate after decoding
        }

        int dataPos = pos;
        int instPos = dataPos + dataLen;
        int addrPos = instPos + instLen;

        // Validate that all three sections fit within the delta encoding
        if (addrPos + addrLen > deltaStart + deltaLen)
            throw new InvalidDataException("Delta section lengths exceed delta encoding size.");

        byte[] target = new byte[targetWindowLen];
        int targetPos = 0;

        // Address cache (RFC 3284 Section 5.1)
        int[] nearCache = new int[DefaultNearSize];
        int[] sameCache = new int[DefaultSameSize * 256];
        int nearSlot = 0;

        int instEnd = instPos + instLen;

        while (instPos < instEnd)
        {
            byte opcode = patch[instPos++];
            ref readonly var entry = ref codeTable[opcode];

            ExecuteInstruction(entry.Type1, entry.Size1, entry.Mode1,
                patch, ref dataPos, ref instPos, ref addrPos,
                srcSegment, srcSegmentLen,
                target, ref targetPos, targetWindowLen,
                nearCache, sameCache, ref nearSlot);

            ExecuteInstruction(entry.Type2, entry.Size2, entry.Mode2,
                patch, ref dataPos, ref instPos, ref addrPos,
                srcSegment, srcSegmentLen,
                target, ref targetPos, targetWindowLen,
                nearCache, sameCache, ref nearSlot);
        }

        if (targetPos != targetWindowLen)
            throw new InvalidDataException(
                $"Target window size mismatch: expected {targetWindowLen} bytes, got {targetPos}.");

        // Verify Adler32 checksum if present (xdelta3 extension)
        if (checksumPos >= 0)
        {
            if (checksumPos + 4 > patch.Length)
                throw new InvalidDataException("Adler32 checksum extends beyond patch file.");
            uint expected = ReadUInt32BE(patch, checksumPos);
            uint actual = ComputeAdler32(target);
            if (actual != expected)
                throw new InvalidDataException(
                    $"Target window Adler32 mismatch: expected {expected:X8}, got {actual:X8}.");
        }

        output.Write(target, 0, targetWindowLen);
        pos = deltaStart + deltaLen;
    }

    private static void ExecuteInstruction(
        byte type, int size, byte mode,
        byte[] patch, ref int dataPos, ref int instPos, ref int addrPos,
        byte[]? srcSegment, int srcSegmentLen,
        byte[] target, ref int targetPos, int targetWindowLen,
        int[] nearCache, int[] sameCache, ref int nearSlot)
    {
        if (type == INST_NOOP) return;

        if (size == 0)
            size = ReadVarInt(patch, ref instPos);

        switch (type)
        {
            case INST_ADD:
                if (targetPos + size > targetWindowLen)
                    throw new InvalidDataException("ADD instruction exceeds target window size.");
                if (dataPos + size > patch.Length)
                    throw new InvalidDataException("ADD instruction exceeds data section.");
                Array.Copy(patch, dataPos, target, targetPos, size);
                dataPos += size;
                targetPos += size;
                break;

            case INST_RUN:
                if (dataPos >= patch.Length)
                    throw new InvalidDataException("RUN instruction exceeds data section.");
                byte runByte = patch[dataPos++];
                if (targetPos + size > targetWindowLen)
                    throw new InvalidDataException("RUN instruction exceeds target window size.");
                Array.Fill(target, runByte, targetPos, size);
                targetPos += size;
                break;

            case INST_COPY:
                int here = srcSegmentLen + targetPos;
                int addr = DecodeAddress(patch, ref addrPos, mode, here,
                    nearCache, sameCache, ref nearSlot);
                if (targetPos + size > targetWindowLen)
                    throw new InvalidDataException("COPY instruction exceeds target window size.");

                // Validate source is available if any addresses reference it
                if (addr < srcSegmentLen && srcSegment is null)
                    throw new InvalidDataException(
                        "COPY references source segment but no source data is available.");

                // Fast path: entire copy comes from the source segment
                if (addr >= 0 && addr + size <= srcSegmentLen)
                {
                    Array.Copy(srcSegment!, addr, target, targetPos, size);
                    targetPos += size;
                }
                else
                {
                    for (int i = 0; i < size; i++)
                    {
                        int copyAddr = addr + i;
                        if (copyAddr < srcSegmentLen)
                        {
                            target[targetPos++] = srcSegment![copyAddr];
                        }
                        else
                        {
                            // Copy from already-written target data within this window
                            int targetOffset = copyAddr - srcSegmentLen;
                            if (targetOffset >= targetWindowLen)
                                throw new InvalidDataException("COPY offset exceeds target window.");
                            target[targetPos++] = target[targetOffset];
                        }
                    }
                }
                break;

            default:
                throw new InvalidDataException($"Unknown VCDIFF instruction type: {type}.");
        }
    }

    /// <summary>
    /// Decodes a COPY address using the VCDIFF address cache (RFC 3284 Section 5.3–5.4).
    /// </summary>
    private static int DecodeAddress(byte[] data, ref int pos, byte mode, int here,
        int[] nearCache, int[] sameCache, ref int nearSlot)
    {
        int addr;

        if (mode == 0) // VCD_SELF
        {
            addr = ReadVarInt(data, ref pos);
        }
        else if (mode == 1) // VCD_HERE
        {
            addr = here - ReadVarInt(data, ref pos);
        }
        else if (mode < DefaultNearSize + 2) // NEAR cache modes
        {
            addr = nearCache[mode - 2] + ReadVarInt(data, ref pos);
        }
        else // SAME cache modes — uses a single byte, not a varint
        {
            if (pos >= data.Length)
                throw new InvalidDataException("Unexpected end of address data in SAME cache lookup.");
            int m = mode - 2 - DefaultNearSize;
            byte b = data[pos++];
            addr = sameCache[m * 256 + b];
        }

        if (addr < 0)
            throw new InvalidDataException($"Decoded COPY address is negative ({addr}).");

        // Update caches after every decode (RFC 3284 Section 5.2)
        if (DefaultNearSize > 0)
        {
            nearCache[nearSlot] = addr;
            nearSlot = (nearSlot + 1) % DefaultNearSize;
        }
        sameCache[addr % (DefaultSameSize * 256)] = addr;

        return addr;
    }

    /// <summary>
    /// Reads a VCDIFF variable-length integer (RFC 3284 Section 2).
    /// Encoding: MSB of each byte except the last is 1; MSB of the last byte is 0.
    /// Value bits are big-endian (most significant 7-bit group first).
    /// </summary>
    private static int ReadVarInt(byte[] data, ref int pos)
    {
        long result = 0;
        int count = 0;
        byte b;
        do
        {
            if (pos >= data.Length)
                throw new InvalidDataException("Unexpected end of VCDIFF data while reading integer.");
            b = data[pos++];
            result = (result << 7) | (long)(b & 0x7F);
            if (++count > 5)
                throw new InvalidDataException("VCDIFF variable-length integer is too large.");
        } while ((b & 0x80) != 0);

        if (result > int.MaxValue)
            throw new InvalidDataException($"VCDIFF integer value {result} exceeds maximum supported size.");

        return (int)result;
    }

    private static uint ReadUInt32BE(byte[] data, int offset) =>
        (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);

    /// <summary>
    /// Computes the Adler-32 checksum used by xdelta3 for target window verification.
    /// </summary>
    private static uint ComputeAdler32(byte[] data)
    {
        const uint ModAdler = 65521;
        uint a = 1, b = 0;

        // Process in chunks of 5552 to defer modulo: largest n where 255*n*(n+1)/2 fits in uint32
        int index = 0;
        while (index < data.Length)
        {
            int chunk = Math.Min(data.Length - index, 5552);
            for (int i = 0; i < chunk; i++)
            {
                a += data[index + i];
                b += a;
            }
            a %= ModAdler;
            b %= ModAdler;
            index += chunk;
        }

        return (b << 16) | a;
    }

    /// <summary>
    /// Builds the default VCDIFF code table (RFC 3284 Section 5.6).
    /// Each entry encodes one or two instructions per opcode.
    /// </summary>
    private static CodeTableEntry[] BuildDefaultCodeTable()
    {
        var table = new CodeTableEntry[256];
        int idx = 0;

        // Entry 0: RUN size=0
        table[idx++] = new CodeTableEntry(INST_RUN, 0, 0, INST_NOOP, 0, 0);

        // Entries 1–18: ADD size=0..17
        for (int s = 0; s <= 17; s++)
            table[idx++] = new CodeTableEntry(INST_ADD, (byte)s, 0, INST_NOOP, 0, 0);

        // Entries 19–162: COPY with modes 0–8, sizes {0, 4–18}
        for (int mode = 0; mode <= 8; mode++)
        {
            table[idx++] = new CodeTableEntry(INST_COPY, 0, (byte)mode, INST_NOOP, 0, 0);
            for (int s = 4; s <= 18; s++)
                table[idx++] = new CodeTableEntry(INST_COPY, (byte)s, (byte)mode, INST_NOOP, 0, 0);
        }

        // Entries 163–234: ADD(1–4) + COPY(4–6) for modes 0–5
        for (int mode = 0; mode <= 5; mode++)
        {
            for (int addSize = 1; addSize <= 4; addSize++)
            {
                for (int copySize = 4; copySize <= 6; copySize++)
                    table[idx++] = new CodeTableEntry(
                        INST_ADD, (byte)addSize, 0,
                        INST_COPY, (byte)copySize, (byte)mode);
            }
        }

        // Entries 235–246: ADD(1–4) + COPY(4–6) for mode 6
        for (int addSize = 1; addSize <= 4; addSize++)
        {
            for (int copySize = 4; copySize <= 6; copySize++)
                table[idx++] = new CodeTableEntry(
                    INST_ADD, (byte)addSize, 0,
                    INST_COPY, (byte)copySize, 6);
        }

        // Entries 247–255: NOOP/NOOP (default-initialized)
        return table;
    }

    private struct CodeTableEntry(byte type1, byte size1, byte mode1, byte type2, byte size2, byte mode2)
    {
        public readonly byte Type1 = type1, Size1 = size1, Mode1 = mode1;
        public readonly byte Type2 = type2, Size2 = size2, Mode2 = mode2;
    }

    private static void ValidateFileSize(string filePath, string fileDescription)
    {
        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
            throw new FileNotFoundException($"{fileDescription} file not found.", filePath);
        if (fileInfo.Length > MaxFileSize)
            throw new InvalidOperationException(
                $"{fileDescription} file is too large ({fileInfo.Length / (1024 * 1024)} MB). " +
                $"Maximum supported size for xDelta patching is {MaxFileSize / (1024 * 1024)} MB.");
    }
}
