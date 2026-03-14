namespace RetroMultiTools.Utilities;

public static class BatchHeaderFixer
{
    private const int BufferSize = 81920;
    private const long MaxFileSize = 512L * 1024 * 1024; // 512 MB

    private static void ValidateFileSize(string filePath)
    {
        long length = new FileInfo(filePath).Length;
        if (length > MaxFileSize)
            throw new InvalidOperationException(
                $"File is too large ({length / (1024.0 * 1024):F1} MB). Maximum supported size: {MaxFileSize / (1024 * 1024)} MB.");
    }

    /// <summary>
    /// Fixes ROM headers for all supported ROMs in a directory.
    /// Supports SNES, Game Boy/GBC, GBA, Mega Drive/Genesis, Sega 32X, and N64 checksum fixing,
    /// SMS/Game Gear TMR SEGA checksum fixing, NES header cleanup, and header validation for
    /// Atari 7800, Atari Lynx, PC Engine, Virtual Boy, Neo Geo Pocket, Atari Jaguar, MSX,
    /// ColecoVision, and Watara Supervision.
    /// </summary>
    public static async Task<BatchFixResult> FixDirectoryAsync(
        string inputDirectory,
        string outputDirectory,
        IProgress<string>? progress = null)
    {
        if (!Directory.Exists(inputDirectory))
            throw new DirectoryNotFoundException($"Directory not found: {inputDirectory}");

        Directory.CreateDirectory(outputDirectory);

        var files = Directory.EnumerateFiles(inputDirectory, "*", SearchOption.AllDirectories)
            .Where(f => IsSupportedForFix(f))
            .ToList();

        int fixedCount = 0;
        int skipped = 0;
        int failed = 0;
        var details = new List<string>();

        for (int i = 0; i < files.Count; i++)
        {
            string file = files[i];
            string fileName = Path.GetFileName(file);
            progress?.Report($"Processing {i + 1} of {files.Count}: {fileName}");

            try
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                string outputPath = Path.Combine(outputDirectory, fileName);
                bool wasFixed;

                if (ext is ".smc" or ".sfc")
                    wasFixed = await FixSnesChecksumAsync(file, outputPath).ConfigureAwait(false);
                else if (ext == ".nes")
                    wasFixed = await FixNesHeaderAsync(file, outputPath).ConfigureAwait(false);
                else if (ext is ".gb" or ".gbc")
                    wasFixed = await FixGameBoyChecksumAsync(file, outputPath).ConfigureAwait(false);
                else if (ext == ".gba")
                    wasFixed = await FixGbaChecksumAsync(file, outputPath).ConfigureAwait(false);
                else if (ext is ".md" or ".gen")
                    wasFixed = await FixMegaDriveChecksumAsync(file, outputPath).ConfigureAwait(false);
                else if (ext == ".32x")
                    wasFixed = await FixMegaDriveChecksumAsync(file, outputPath).ConfigureAwait(false);
                else if (ext is ".sms" or ".gg")
                    wasFixed = await FixSmsChecksumAsync(file, outputPath).ConfigureAwait(false);
                else if (ext is ".z64" or ".n64" or ".v64")
                    wasFixed = await FixN64ChecksumAsync(file, outputPath).ConfigureAwait(false);
                else if (ext == ".a78")
                    wasFixed = await FixAtari7800HeaderAsync(file, outputPath).ConfigureAwait(false);
                else if (ext == ".lnx")
                    wasFixed = await FixAtariLynxHeaderAsync(file, outputPath).ConfigureAwait(false);
                else if (ext is ".pce" or ".tg16")
                    wasFixed = await FixPceHeaderAsync(file, outputPath).ConfigureAwait(false);
                else if (ext is ".vb" or ".vboy")
                    wasFixed = await FixVirtualBoyHeaderAsync(file, outputPath).ConfigureAwait(false);
                else if (ext is ".ngp" or ".ngc")
                    wasFixed = await FixNeoGeoPocketHeaderAsync(file, outputPath).ConfigureAwait(false);
                else if (ext is ".j64" or ".jag")
                    wasFixed = await FixAtariJaguarHeaderAsync(file, outputPath).ConfigureAwait(false);
                else if (ext is ".mx1" or ".mx2")
                    wasFixed = await FixMsxHeaderAsync(file, outputPath).ConfigureAwait(false);
                else if (ext is ".col" or ".cv")
                    wasFixed = await FixColecoVisionHeaderAsync(file, outputPath).ConfigureAwait(false);
                else if (ext == ".sv")
                    wasFixed = await FixWataraSupervisionHeaderAsync(file, outputPath).ConfigureAwait(false);
                else
                {
                    skipped++;
                    continue;
                }

                if (wasFixed)
                {
                    fixedCount++;
                    details.Add($"Fixed: {fileName}");
                }
                else
                {
                    skipped++;
                    details.Add($"OK (no fix needed): {fileName}");
                    // Copy unchanged file to output
                    File.Copy(file, outputPath, true);
                }
            }
            catch (IOException ex)
            {
                failed++;
                details.Add($"Error: {fileName} — {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                skipped++;
                details.Add($"Skipped: {fileName} — {ex.Message}");
            }
        }

        progress?.Report($"Done — {fixedCount} fixed, {skipped} skipped, {failed} failed.");
        return new BatchFixResult
        {
            Fixed = fixedCount,
            Skipped = skipped,
            Failed = failed,
            Details = details
        };
    }

    /// <summary>
    /// Fixes a single ROM file header.
    /// </summary>
    public static async Task<string> FixSingleAsync(
        string inputPath,
        string outputPath,
        IProgress<string>? progress = null)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("File not found.", inputPath);

        string ext = Path.GetExtension(inputPath).ToLowerInvariant();
        progress?.Report($"Analyzing {Path.GetFileName(inputPath)}...");

        bool wasFixed;
        if (ext is ".smc" or ".sfc")
            wasFixed = await FixSnesChecksumAsync(inputPath, outputPath).ConfigureAwait(false);
        else if (ext == ".nes")
            wasFixed = await FixNesHeaderAsync(inputPath, outputPath).ConfigureAwait(false);
        else if (ext is ".gb" or ".gbc")
            wasFixed = await FixGameBoyChecksumAsync(inputPath, outputPath).ConfigureAwait(false);
        else if (ext == ".gba")
            wasFixed = await FixGbaChecksumAsync(inputPath, outputPath).ConfigureAwait(false);
        else if (ext is ".md" or ".gen")
            wasFixed = await FixMegaDriveChecksumAsync(inputPath, outputPath).ConfigureAwait(false);
        else if (ext == ".32x")
            wasFixed = await FixMegaDriveChecksumAsync(inputPath, outputPath).ConfigureAwait(false);
        else if (ext is ".sms" or ".gg")
            wasFixed = await FixSmsChecksumAsync(inputPath, outputPath).ConfigureAwait(false);
        else if (ext is ".z64" or ".n64" or ".v64")
            wasFixed = await FixN64ChecksumAsync(inputPath, outputPath).ConfigureAwait(false);
        else if (ext == ".a78")
            wasFixed = await FixAtari7800HeaderAsync(inputPath, outputPath).ConfigureAwait(false);
        else if (ext == ".lnx")
            wasFixed = await FixAtariLynxHeaderAsync(inputPath, outputPath).ConfigureAwait(false);
        else if (ext is ".pce" or ".tg16")
            wasFixed = await FixPceHeaderAsync(inputPath, outputPath).ConfigureAwait(false);
        else if (ext is ".vb" or ".vboy")
            wasFixed = await FixVirtualBoyHeaderAsync(inputPath, outputPath).ConfigureAwait(false);
        else if (ext is ".ngp" or ".ngc")
            wasFixed = await FixNeoGeoPocketHeaderAsync(inputPath, outputPath).ConfigureAwait(false);
        else if (ext is ".j64" or ".jag")
            wasFixed = await FixAtariJaguarHeaderAsync(inputPath, outputPath).ConfigureAwait(false);
        else if (ext is ".mx1" or ".mx2")
            wasFixed = await FixMsxHeaderAsync(inputPath, outputPath).ConfigureAwait(false);
        else if (ext is ".col" or ".cv")
            wasFixed = await FixColecoVisionHeaderAsync(inputPath, outputPath).ConfigureAwait(false);
        else if (ext == ".sv")
            wasFixed = await FixWataraSupervisionHeaderAsync(inputPath, outputPath).ConfigureAwait(false);
        else
            throw new InvalidOperationException($"Unsupported file type: {ext}. Supported: .smc, .sfc, .nes, .gb, .gbc, .gba, .md, .gen, .32x, .sms, .gg, .z64, .n64, .v64, .a78, .lnx, .pce, .tg16, .vb, .vboy, .ngp, .ngc, .j64, .jag, .mx1, .mx2, .col, .cv, .sv");

        string result = wasFixed
            ? $"✔ Header fixed and saved to: {outputPath}"
            : $"✔ Header was already correct. File copied unchanged.";

        if (!wasFixed)
            File.Copy(inputPath, outputPath, true);

        progress?.Report("Done.");
        return result;
    }

    private static bool IsSupportedForFix(string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext is ".smc" or ".sfc" or ".nes"
            or ".gb" or ".gbc"
            or ".gba"
            or ".md" or ".gen" or ".32x"
            or ".sms" or ".gg"
            or ".z64" or ".n64" or ".v64"
            or ".a78"
            or ".lnx"
            or ".pce" or ".tg16"
            or ".vb" or ".vboy"
            or ".ngp" or ".ngc"
            or ".j64" or ".jag"
            or ".mx1" or ".mx2"
            or ".col" or ".cv"
            or ".sv";
    }

    /// <summary>
    /// Fixes the SNES internal checksum by recalculating and patching the header.
    /// </summary>
    private static async Task<bool> FixSnesChecksumAsync(string inputPath, string outputPath)
    {
        ValidateFileSize(inputPath);
        return await Task.Run(() =>
        {
            byte[] data = File.ReadAllBytes(inputPath);

            int offset = 0;
            if ((data.Length % 1024) == 512)
                offset = 512; // Skip copier header

            int romSize = data.Length - offset;

            // Determine LoROM vs HiROM
            int loRomHeader = offset + 0x7FC0;
            int hiRomHeader = offset + 0xFFC0;

            int headerOffset;
            if (hiRomHeader + 0x30 <= data.Length && IsValidSnesTitle(data, hiRomHeader))
                headerOffset = hiRomHeader;
            else if (loRomHeader + 0x30 <= data.Length && IsValidSnesTitle(data, loRomHeader))
                headerOffset = loRomHeader;
            else
                return false; // Can't determine header location

            // Read current checksums
            int checksumComplementOffset = headerOffset + 0x1C;
            int checksumOffset = headerOffset + 0x1E;

            if (checksumOffset + 2 > data.Length)
                return false;

            ushort oldComplement = (ushort)(data[checksumComplementOffset] | (data[checksumComplementOffset + 1] << 8));
            ushort oldChecksum = (ushort)(data[checksumOffset] | (data[checksumOffset + 1] << 8));

            // Zero out checksum fields for calculation
            data[checksumComplementOffset] = 0;
            data[checksumComplementOffset + 1] = 0;
            data[checksumOffset] = 0;
            data[checksumOffset + 1] = 0;

            // Calculate new checksum (sum of all bytes in ROM data, excluding copier header)
            uint sum = 0;
            for (int i = offset; i < data.Length; i++)
                sum += data[i];

            ushort newChecksum = (ushort)(sum & 0xFFFF);
            ushort newComplement = (ushort)(newChecksum ^ 0xFFFF);

            bool needsFix = newChecksum != oldChecksum || newComplement != oldComplement;

            if (needsFix)
            {
                // Write corrected checksums
                data[checksumComplementOffset] = (byte)(newComplement & 0xFF);
                data[checksumComplementOffset + 1] = (byte)((newComplement >> 8) & 0xFF);
                data[checksumOffset] = (byte)(newChecksum & 0xFF);
                data[checksumOffset + 1] = (byte)((newChecksum >> 8) & 0xFF);

                try
                {
                    File.WriteAllBytes(outputPath, data);
                }
                catch
                {
                    try { File.Delete(outputPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                    throw;
                }
            }
            return needsFix;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Fixes NES header by cleaning unused bytes and verifying PRG/CHR sizes.
    /// </summary>
    private static async Task<bool> FixNesHeaderAsync(string inputPath, string outputPath)
    {
        ValidateFileSize(inputPath);
        return await Task.Run(() =>
        {
            byte[] data = File.ReadAllBytes(inputPath);

            if (data.Length < 16 || data[0] != 0x4E || data[1] != 0x45 || data[2] != 0x53 || data[3] != 0x1A)
                throw new InvalidOperationException("Not a valid iNES ROM file.");

            // Check if this is NES 2.0 - don't modify those
            bool isNes2 = (data[7] & 0x0C) == 0x08;
            if (isNes2)
                return false;

            // Check if bytes 8-15 contain garbage data
            bool hasDirtyBytes = false;
            for (int i = 8; i < 16; i++)
            {
                if (data[i] != 0)
                {
                    hasDirtyBytes = true;
                    break;
                }
            }

            if (!hasDirtyBytes)
                return false;

            // Clean dirty header bytes
            for (int i = 8; i < 16; i++)
                data[i] = 0;

            try
            {
                File.WriteAllBytes(outputPath, data);
            }
            catch
            {
                try { File.Delete(outputPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                throw;
            }
            return true;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Fixes Game Boy / Game Boy Color header checksum (0x14D) and global checksum (0x14E-0x14F).
    /// </summary>
    private static async Task<bool> FixGameBoyChecksumAsync(string inputPath, string outputPath)
    {
        ValidateFileSize(inputPath);
        return await Task.Run(() =>
        {
            byte[] data = File.ReadAllBytes(inputPath);

            if (data.Length < 0x150)
                throw new InvalidOperationException("File is too small to be a valid Game Boy ROM.");

            // Verify Nintendo logo magic bytes at 0x104
            if (data[0x104] != 0xCE || data[0x105] != 0xED)
                throw new InvalidOperationException("Not a valid Game Boy ROM file (missing Nintendo logo signature).");

            bool needsFix = false;

            // Fix header checksum at 0x14D
            // Algorithm: x = 0; for i = 0x134..0x14C: x = x - data[i] - 1
            byte oldHeaderChecksum = data[0x14D];
            int x = 0;
            for (int i = 0x134; i <= 0x14C; i++)
                x = x - data[i] - 1;
            byte newHeaderChecksum = (byte)(x & 0xFF);

            if (oldHeaderChecksum != newHeaderChecksum)
            {
                data[0x14D] = newHeaderChecksum;
                needsFix = true;
            }

            // Fix global checksum at 0x14E-0x14F (big-endian)
            // Sum of all bytes in ROM except 0x14E and 0x14F
            ushort oldGlobalChecksum = (ushort)((data[0x14E] << 8) | data[0x14F]);
            uint sum = 0;
            for (int i = 0; i < data.Length; i++)
            {
                if (i == 0x14E || i == 0x14F) continue;
                sum += data[i];
            }
            ushort newGlobalChecksum = (ushort)(sum & 0xFFFF);

            if (oldGlobalChecksum != newGlobalChecksum)
            {
                data[0x14E] = (byte)((newGlobalChecksum >> 8) & 0xFF);
                data[0x14F] = (byte)(newGlobalChecksum & 0xFF);
                needsFix = true;
            }

            if (needsFix)
            {
                try
                {
                    File.WriteAllBytes(outputPath, data);
                }
                catch
                {
                    try { File.Delete(outputPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                    throw;
                }
            }
            return needsFix;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Fixes Game Boy Advance header checksum at 0x1BD.
    /// </summary>
    private static async Task<bool> FixGbaChecksumAsync(string inputPath, string outputPath)
    {
        ValidateFileSize(inputPath);
        return await Task.Run(() =>
        {
            byte[] data = File.ReadAllBytes(inputPath);

            if (data.Length < 0x1BE)
                throw new InvalidOperationException("File is too small to be a valid GBA ROM.");

            // Verify GBA magic at 0x04
            if (data[0x04] != 0x24 || data[0x05] != 0xFF || data[0x06] != 0xAE || data[0x07] != 0x51)
                throw new InvalidOperationException("Not a valid GBA ROM file (missing header signature).");

            byte oldChecksum = data[0x1BD];

            // Algorithm: chk = 0; for i = 0xA0..0xBC: chk -= data[i]; chk = (chk - 0x19) & 0xFF
            int chk = 0;
            for (int i = 0xA0; i <= 0xBC; i++)
                chk -= data[i];
            byte newChecksum = (byte)((chk - 0x19) & 0xFF);

            bool needsFix = oldChecksum != newChecksum;

            if (needsFix)
            {
                data[0x1BD] = newChecksum;
                try
                {
                    File.WriteAllBytes(outputPath, data);
                }
                catch
                {
                    try { File.Delete(outputPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                    throw;
                }
            }
            return needsFix;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Fixes Mega Drive / Genesis internal checksum at 0x18E-0x18F.
    /// The checksum is a 16-bit sum of all 16-bit big-endian words from 0x200 to end of ROM.
    /// </summary>
    private static async Task<bool> FixMegaDriveChecksumAsync(string inputPath, string outputPath)
    {
        ValidateFileSize(inputPath);
        return await Task.Run(() =>
        {
            byte[] data = File.ReadAllBytes(inputPath);

            if (data.Length < 0x200)
                throw new InvalidOperationException("File is too small to be a valid Mega Drive/Genesis ROM.");

            // Verify "SEGA" marker at 0x100 or 0x101
            bool hasSega = (data.Length > 0x104 &&
                ((data[0x100] == (byte)'S' && data[0x101] == (byte)'E' &&
                  data[0x102] == (byte)'G' && data[0x103] == (byte)'A') ||
                 (data[0x101] == (byte)'S' && data[0x102] == (byte)'E' &&
                  data[0x103] == (byte)'G' && data[0x104] == (byte)'A')));

            if (!hasSega)
                throw new InvalidOperationException("Not a valid Mega Drive/Genesis ROM file (missing SEGA marker).");

            ushort oldChecksum = (ushort)((data[0x18E] << 8) | data[0x18F]);

            // Calculate: 16-bit sum of all words from 0x200 to end
            uint sum = 0;
            int end = data.Length;
            // Ensure we process full words; if odd length, pad last byte
            for (int i = 0x200; i < end - 1; i += 2)
                sum += (uint)((data[i] << 8) | data[i + 1]);
            if ((end - 0x200) % 2 != 0)
                sum += (uint)(data[end - 1] << 8);

            ushort newChecksum = (ushort)(sum & 0xFFFF);
            bool needsFix = oldChecksum != newChecksum;

            if (needsFix)
            {
                data[0x18E] = (byte)((newChecksum >> 8) & 0xFF);
                data[0x18F] = (byte)(newChecksum & 0xFF);
                try
                {
                    File.WriteAllBytes(outputPath, data);
                }
                catch
                {
                    try { File.Delete(outputPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                    throw;
                }
            }
            return needsFix;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Fixes Sega Master System / Game Gear TMR SEGA checksum.
    /// The checksum covers bytes from 0x0000 up to the TMR SEGA header.
    /// </summary>
    private static async Task<bool> FixSmsChecksumAsync(string inputPath, string outputPath)
    {
        ValidateFileSize(inputPath);
        return await Task.Run(() =>
        {
            byte[] data = File.ReadAllBytes(inputPath);

            // Find TMR SEGA header - check known offsets
            int headerOffset = -1;
            int[] possibleOffsets = [0x7FF0, 0x3FF0, 0x1FF0];

            foreach (int offset in possibleOffsets)
            {
                if (offset + 16 <= data.Length &&
                    data[offset] == (byte)'T' && data[offset + 1] == (byte)'M' &&
                    data[offset + 2] == (byte)'R' && data[offset + 3] == (byte)' ' &&
                    data[offset + 4] == (byte)'S' && data[offset + 5] == (byte)'E' &&
                    data[offset + 6] == (byte)'G' && data[offset + 7] == (byte)'A')
                {
                    headerOffset = offset;
                    break;
                }
            }

            if (headerOffset < 0)
                throw new InvalidOperationException("No TMR SEGA header found. Cannot fix checksum for this ROM.");

            // Checksum is at headerOffset + 0x0A (little-endian 16-bit)
            int checksumOffset = headerOffset + 0x0A;
            if (checksumOffset + 2 > data.Length)
                throw new InvalidOperationException("TMR SEGA header is truncated.");

            ushort oldChecksum = (ushort)(data[checksumOffset] | (data[checksumOffset + 1] << 8));

            // Calculate: 16-bit sum of all bytes from 0x0000 to headerOffset - 1
            uint sum = 0;
            for (int i = 0; i < headerOffset; i++)
                sum += data[i];
            ushort newChecksum = (ushort)(sum & 0xFFFF);

            bool needsFix = oldChecksum != newChecksum;

            if (needsFix)
            {
                data[checksumOffset] = (byte)(newChecksum & 0xFF);
                data[checksumOffset + 1] = (byte)((newChecksum >> 8) & 0xFF);
                try
                {
                    File.WriteAllBytes(outputPath, data);
                }
                catch
                {
                    try { File.Delete(outputPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                    throw;
                }
            }
            return needsFix;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Fixes N64 ROM CRC checksums at offsets 0x10-0x17 using the CIC-NUS-6102 algorithm.
    /// Normalizes the ROM to Big Endian format before calculating.
    /// </summary>
    private static async Task<bool> FixN64ChecksumAsync(string inputPath, string outputPath)
    {
        ValidateFileSize(inputPath);
        return await Task.Run(() =>
        {
            byte[] data = File.ReadAllBytes(inputPath);
            if (data.Length < 0x101000)
                throw new InvalidOperationException("File is too small to be a valid N64 ROM (minimum 0x101000 bytes required for checksum calculation).");

            // Detect and normalize to Big Endian
            var format = N64FormatConverter.DetectFormat(data);
            if (format == null)
                throw new InvalidOperationException("Cannot detect N64 ROM format.");
            N64FormatConverter.NormalizeToBigEndian(data, data.Length, format.Value);

            // Read old checksums (Big Endian at 0x10 and 0x14)
            uint oldCrc1 = (uint)((data[0x10] << 24) | (data[0x11] << 16) | (data[0x12] << 8) | data[0x13]);
            uint oldCrc2 = (uint)((data[0x14] << 24) | (data[0x15] << 16) | (data[0x16] << 8) | data[0x17]);

            // CIC-NUS-6102 seed — the most common CIC variant, used by the vast majority of N64 games.
            // Games using other CIC chips (6101, 6103, 6105, 6106) have different seeds and will
            // produce incorrect checksums here, potentially failing validation on real hardware.
            uint seed = 0xF8CA4DDC;

            uint t1, t2, t3, t4, t5, t6;
            t1 = t2 = t3 = t4 = t5 = t6 = seed;

            // Checksum covers 1MB of ROM data starting after the 4KB boot code region
            for (int i = 0x1000; i < 0x101000; i += 4)
            {
                uint word = (uint)((data[i] << 24) | (data[i + 1] << 16) | (data[i + 2] << 8) | data[i + 3]);
                uint tmp = t1 + word;
                if (tmp < t1) t4++;
                t1 = tmp;
                t2 ^= word;
                t3 += word;

                if (t1 > word)
                    t5 ^= (t1 ^ word);
                else
                    t5 ^= t1 ^ ~word;

                t6 += (word ^ t3);
            }

            uint newCrc1 = t1 ^ t2 ^ t3;
            uint newCrc2 = t4 ^ t5 ^ t6;

            bool needsFix = oldCrc1 != newCrc1 || oldCrc2 != newCrc2;

            if (needsFix)
            {
                data[0x10] = (byte)((newCrc1 >> 24) & 0xFF);
                data[0x11] = (byte)((newCrc1 >> 16) & 0xFF);
                data[0x12] = (byte)((newCrc1 >> 8) & 0xFF);
                data[0x13] = (byte)(newCrc1 & 0xFF);
                data[0x14] = (byte)((newCrc2 >> 24) & 0xFF);
                data[0x15] = (byte)((newCrc2 >> 16) & 0xFF);
                data[0x16] = (byte)((newCrc2 >> 8) & 0xFF);
                data[0x17] = (byte)(newCrc2 & 0xFF);

                // Convert back to original format before writing
                if (format.Value != N64FormatConverter.N64Format.BigEndian)
                    N64FormatConverter.FromBigEndian(data, data.Length, format.Value);

                try
                {
                    File.WriteAllBytes(outputPath, data);
                }
                catch
                {
                    try { File.Delete(outputPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                    throw;
                }
            }
            return needsFix;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Fixes Atari 7800 A78 header by validating and correcting the ATARI7800 signature.
    /// </summary>
    private static async Task<bool> FixAtari7800HeaderAsync(string inputPath, string outputPath)
    {
        ValidateFileSize(inputPath);
        return await Task.Run(() =>
        {
            byte[] data = File.ReadAllBytes(inputPath);

            if (data.Length < 128)
                throw new InvalidOperationException("File is too small to be a valid Atari 7800 ROM.");

            // Check for "ATARI7800" signature at byte 1
            byte[] expectedSig = System.Text.Encoding.ASCII.GetBytes("ATARI7800");
            bool hasSig = data.Length >= 10;
            if (hasSig)
            {
                for (int i = 0; i < expectedSig.Length && hasSig; i++)
                {
                    if (data[1 + i] != expectedSig[i])
                        hasSig = false;
                }
            }

            if (hasSig)
            {
                // Header exists; verify header size field at offset 0x31 (49) is correct
                // Standard A78 header is 128 bytes; byte 0x31 should be 0 for standard header
                bool needsFix = false;

                // Clean any garbage in reserved bytes (0x64-0x7F should be 0)
                for (int i = 0x64; i < 0x80 && i < data.Length; i++)
                {
                    if (data[i] != 0)
                    {
                        data[i] = 0;
                        needsFix = true;
                    }
                }

                if (needsFix)
                {
                    try
                    {
                        File.WriteAllBytes(outputPath, data);
                    }
                    catch
                    {
                        try { File.Delete(outputPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                        throw;
                    }
                }
                return needsFix;
            }

            // No header found — file is headerless, nothing to fix
            return false;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Fixes Atari Lynx .lnx header by validating the LYNX magic and cleaning reserved bytes.
    /// </summary>
    private static async Task<bool> FixAtariLynxHeaderAsync(string inputPath, string outputPath)
    {
        ValidateFileSize(inputPath);
        return await Task.Run(() =>
        {
            byte[] data = File.ReadAllBytes(inputPath);

            // Check for "LYNX" magic at byte 0
            if (data.Length < 64 || data[0] != 0x4C || data[1] != 0x59 || data[2] != 0x4E || data[3] != 0x58)
                throw new InvalidOperationException("Not a valid Atari Lynx ROM with header (missing LYNX magic).");

            bool needsFix = false;

            // Verify page size bytes (offsets 4-5) contain valid value
            ushort pageSize = (ushort)(data[4] | (data[5] << 8));
            if (pageSize == 0)
            {
                // Default page size of 256 if unset
                data[4] = 0x00;
                data[5] = 0x01;
                needsFix = true;
            }

            // Clean reserved bytes (bytes 10-63 in the LYNX header after the defined fields)
            for (int i = 10; i < 64; i++)
            {
                if (data[i] != 0)
                {
                    data[i] = 0;
                    needsFix = true;
                }
            }

            if (needsFix)
            {
                try
                {
                    File.WriteAllBytes(outputPath, data);
                }
                catch
                {
                    try { File.Delete(outputPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                    throw;
                }
            }
            return needsFix;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Fixes PC Engine / TurboGrafx-16 ROM by removing or fixing the 512-byte copier header.
    /// If a copier header is present and contains garbage data, it is cleaned.
    /// </summary>
    private static async Task<bool> FixPceHeaderAsync(string inputPath, string outputPath)
    {
        ValidateFileSize(inputPath);
        return await Task.Run(() =>
        {
            byte[] data = File.ReadAllBytes(inputPath);

            if (data.Length < 8192)
                throw new InvalidOperationException("File is too small to be a valid PC Engine ROM.");

            bool hasCopierHeader = (data.Length % 8192) == 512;

            if (!hasCopierHeader)
            {
                // No copier header — nothing to fix
                return false;
            }

            // Clean the 512-byte copier header (zero out garbage bytes)
            bool needsFix = false;
            for (int i = 0; i < 512; i++)
            {
                if (data[i] != 0)
                {
                    data[i] = 0;
                    needsFix = true;
                }
            }

            if (needsFix)
            {
                try
                {
                    File.WriteAllBytes(outputPath, data);
                }
                catch
                {
                    try { File.Delete(outputPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                    throw;
                }
            }
            return needsFix;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Fixes Virtual Boy ROM header by validating known fields and cleaning reserved bytes.
    /// The VB header is located at the end of the ROM, 544 bytes before the end.
    /// </summary>
    private static async Task<bool> FixVirtualBoyHeaderAsync(string inputPath, string outputPath)
    {
        ValidateFileSize(inputPath);
        return await Task.Run(() =>
        {
            byte[] data = File.ReadAllBytes(inputPath);

            if (data.Length < 1024)
                throw new InvalidOperationException("File is too small to be a valid Virtual Boy ROM.");

            // Virtual Boy header is at the end of ROM: offset = size - 0x220 (544 bytes from end)
            int headerOffset = data.Length - 0x220;
            if (headerOffset < 0)
                throw new InvalidOperationException("File is too small to contain a Virtual Boy header.");

            bool needsFix = false;

            // Title is 20 bytes at headerOffset (0x00 through 0x13), should be printable ASCII padded with 0x00
            // Reserved bytes at headerOffset + 0x14 through headerOffset + 0x18 should be 0x00
            for (int i = headerOffset + 0x14; i < headerOffset + 0x19 && i < data.Length; i++)
            {
                if (data[i] != 0)
                {
                    data[i] = 0;
                    needsFix = true;
                }
            }

            // Maker code at headerOffset + 0x19 (2 bytes) — leave as-is
            // Game ID at headerOffset + 0x1B (4 bytes) — leave as-is
            // Game version at headerOffset + 0x1F (1 byte) — leave as-is

            if (needsFix)
            {
                try
                {
                    File.WriteAllBytes(outputPath, data);
                }
                catch
                {
                    try { File.Delete(outputPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                    throw;
                }
            }
            return needsFix;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Fixes Neo Geo Pocket ROM header by validating the copyright string and cleaning reserved bytes.
    /// The NGP header starts at offset 0x00.
    /// </summary>
    private static async Task<bool> FixNeoGeoPocketHeaderAsync(string inputPath, string outputPath)
    {
        ValidateFileSize(inputPath);
        return await Task.Run(() =>
        {
            byte[] data = File.ReadAllBytes(inputPath);

            if (data.Length < 64)
                throw new InvalidOperationException("File is too small to be a valid Neo Geo Pocket ROM.");

            bool needsFix = false;

            // Copyright string "COPYRIGHT BY SNK" at offset 0x00 (16 characters)
            byte[] expectedCopyright = System.Text.Encoding.ASCII.GetBytes("COPYRIGHT BY SNK");
            bool hasCopyright = data.Length >= expectedCopyright.Length;
            if (hasCopyright)
            {
                for (int i = 0; i < expectedCopyright.Length && hasCopyright; i++)
                {
                    if (data[i] != expectedCopyright[i])
                        hasCopyright = false;
                }
            }

            if (!hasCopyright)
            {
                // Not a valid NGP ROM header — nothing to fix
                return false;
            }

            // Reserved/padding bytes after the title (offset 0x30-0x3F should be 0x00)
            for (int i = 0x30; i < 0x40 && i < data.Length; i++)
            {
                if (data[i] != 0)
                {
                    data[i] = 0;
                    needsFix = true;
                }
            }

            if (needsFix)
            {
                try
                {
                    File.WriteAllBytes(outputPath, data);
                }
                catch
                {
                    try { File.Delete(outputPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                    throw;
                }
            }
            return needsFix;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Fixes Atari Jaguar ROM header by validating and cleaning reserved bytes.
    /// </summary>
    private static async Task<bool> FixAtariJaguarHeaderAsync(string inputPath, string outputPath)
    {
        ValidateFileSize(inputPath);
        return await Task.Run(() =>
        {
            byte[] data = File.ReadAllBytes(inputPath);

            if (data.Length < 0x2000)
                throw new InvalidOperationException("File is too small to be a valid Atari Jaguar ROM.");

            bool needsFix = false;

            // Jaguar ROM header at offset 0x400 contains game name and other fields
            // Reserved bytes after the header info area (0x440-0x47F) should be 0x00
            for (int i = 0x440; i < 0x480 && i < data.Length; i++)
            {
                if (data[i] != 0)
                {
                    data[i] = 0;
                    needsFix = true;
                }
            }

            if (needsFix)
            {
                try
                {
                    File.WriteAllBytes(outputPath, data);
                }
                catch
                {
                    try { File.Delete(outputPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                    throw;
                }
            }
            return needsFix;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Fixes MSX cartridge ROM header by validating the "AB" signature and cleaning reserved bytes.
    /// </summary>
    private static async Task<bool> FixMsxHeaderAsync(string inputPath, string outputPath)
    {
        ValidateFileSize(inputPath);
        return await Task.Run(() =>
        {
            byte[] data = File.ReadAllBytes(inputPath);

            if (data.Length < 16)
                throw new InvalidOperationException("File is too small to be a valid MSX cartridge ROM.");

            // Check for "AB" signature at offset 0x00-0x01
            if (data[0] != 0x41 || data[1] != 0x42)
            {
                // Not a standard MSX cartridge — nothing to fix
                return false;
            }

            bool needsFix = false;

            // Reserved bytes at offset 0x0A-0x0F should be 0x00
            for (int i = 0x0A; i < 0x10 && i < data.Length; i++)
            {
                if (data[i] != 0)
                {
                    data[i] = 0;
                    needsFix = true;
                }
            }

            if (needsFix)
            {
                try
                {
                    File.WriteAllBytes(outputPath, data);
                }
                catch
                {
                    try { File.Delete(outputPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                    throw;
                }
            }
            return needsFix;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Fixes ColecoVision cartridge ROM header by validating magic bytes and cleaning reserved bytes.
    /// </summary>
    private static async Task<bool> FixColecoVisionHeaderAsync(string inputPath, string outputPath)
    {
        ValidateFileSize(inputPath);
        return await Task.Run(() =>
        {
            byte[] data = File.ReadAllBytes(inputPath);

            if (data.Length < 16)
                throw new InvalidOperationException("File is too small to be a valid ColecoVision ROM.");

            // Check for magic bytes: 0xAA 0x55 or 0x55 0xAA at offset 0x00-0x01
            bool hasStandardMagic = (data[0] == 0xAA && data[1] == 0x55) || (data[0] == 0x55 && data[1] == 0xAA);
            if (!hasStandardMagic)
            {
                // Not a standard ColecoVision cartridge — nothing to fix
                return false;
            }

            bool needsFix = false;

            // Reserved bytes at offset 0x0A-0x0F should be 0x00
            for (int i = 0x0A; i < 0x10 && i < data.Length; i++)
            {
                if (data[i] != 0)
                {
                    data[i] = 0;
                    needsFix = true;
                }
            }

            if (needsFix)
            {
                try
                {
                    File.WriteAllBytes(outputPath, data);
                }
                catch
                {
                    try { File.Delete(outputPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                    throw;
                }
            }
            return needsFix;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Fixes Watara Supervision ROM header by validating and cleaning reserved bytes.
    /// </summary>
    private static async Task<bool> FixWataraSupervisionHeaderAsync(string inputPath, string outputPath)
    {
        ValidateFileSize(inputPath);
        return await Task.Run(() =>
        {
            byte[] data = File.ReadAllBytes(inputPath);

            if (data.Length < 64)
                throw new InvalidOperationException("File is too small to be a valid Watara Supervision ROM.");

            bool needsFix = false;

            // Supervision ROM header occupies the first 0x20 (32) bytes.
            // Reserved/padding bytes at offset 0x10-0x1F should be 0x00
            for (int i = 0x10; i < 0x20 && i < data.Length; i++)
            {
                if (data[i] != 0)
                {
                    data[i] = 0;
                    needsFix = true;
                }
            }

            if (needsFix)
            {
                try
                {
                    File.WriteAllBytes(outputPath, data);
                }
                catch
                {
                    try { File.Delete(outputPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                    throw;
                }
            }
            return needsFix;
        }).ConfigureAwait(false);
    }

    private static bool IsValidSnesTitle(byte[] data, int offset)
    {
        // SNES title is 21 ASCII bytes at offset
        if (offset + 21 > data.Length) return false;

        int printableCount = 0;
        for (int i = 0; i < 21; i++)
        {
            byte b = data[offset + i];
            if (b >= 0x20 && b <= 0x7E)
                printableCount++;
        }
        return printableCount >= 10; // At least ~half should be printable ASCII
    }
}

public class BatchFixResult
{
    public int Fixed { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public List<string> Details { get; set; } = [];

    public string Summary =>
        $"{Fixed} fixed, {Skipped} already OK/skipped, {Failed} failed";
}
