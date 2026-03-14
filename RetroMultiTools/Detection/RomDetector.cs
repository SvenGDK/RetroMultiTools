using RetroMultiTools.Models;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Detection;

public static class RomDetector
{
    private static readonly Dictionary<string, RomSystem> ExtensionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".nes", RomSystem.NES },
        { ".smc", RomSystem.SNES },
        { ".sfc", RomSystem.SNES },
        { ".z64", RomSystem.N64 },
        { ".n64", RomSystem.N64 },
        { ".v64", RomSystem.N64 },
        { ".gb",  RomSystem.GameBoy },
        { ".gbc", RomSystem.GameBoyColor },
        { ".gba", RomSystem.GameBoyAdvance },
        { ".vb",  RomSystem.VirtualBoy },
        { ".vboy", RomSystem.VirtualBoy },
        { ".sms", RomSystem.SegaMasterSystem },
        { ".md",  RomSystem.MegaDrive },
        { ".gen", RomSystem.MegaDrive },
        { ".32x", RomSystem.Sega32X },
        { ".gg",  RomSystem.GameGear },
        { ".a26", RomSystem.Atari2600 },
        { ".a52", RomSystem.Atari5200 },
        { ".a78", RomSystem.Atari7800 },
        { ".j64", RomSystem.AtariJaguar },
        { ".jag", RomSystem.AtariJaguar },
        { ".lnx", RomSystem.AtariLynx },
        { ".lyx", RomSystem.AtariLynx },
        { ".pce", RomSystem.PCEngine },
        { ".tg16", RomSystem.PCEngine },
        { ".ngp", RomSystem.NeoGeoPocket },
        { ".ngc", RomSystem.NeoGeoPocket },
        { ".col", RomSystem.ColecoVision },
        { ".cv",  RomSystem.ColecoVision },
        { ".int", RomSystem.Intellivision },
        { ".mx1", RomSystem.MSX },
        { ".mx2", RomSystem.MSX2 },
        { ".dsk", RomSystem.AmstradCPC }, // Note: .dsk is also used by MSX and Oric; defaults to Amstrad CPC
        { ".cdt", RomSystem.AmstradCPC },
        { ".sna", RomSystem.AmstradCPC },
        { ".tap", RomSystem.Oric },
        { ".mo5", RomSystem.ThomsonMO5 },
        { ".k7",  RomSystem.ThomsonMO5 },
        { ".fd",  RomSystem.ThomsonMO5 },
        { ".sv",  RomSystem.WataraSupervision },
        { ".ccc", RomSystem.ColorComputer },
        { ".3do", RomSystem.Panasonic3DO },
        { ".cdi", RomSystem.SegaDreamcast },
        { ".gdi", RomSystem.SegaDreamcast },
        { ".gcm", RomSystem.GameCube },
    };

    public static RomInfo Detect(string filePath)
    {
        var info = new RomInfo
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath)
        };

        try
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                info.IsValid = false;
                info.ErrorMessage = "File not found.";
                return info;
            }

            info.FileSize = fileInfo.Length;
            info.FileSizeFormatted = FileUtils.FormatFileSize(fileInfo.Length);

            var ext = Path.GetExtension(filePath);

            if (string.Equals(ext, ".bin", StringComparison.OrdinalIgnoreCase))
            {
                DetectBin(info, filePath);
                return info;
            }
            if (string.Equals(ext, ".iso", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".cue", StringComparison.OrdinalIgnoreCase))
            {
                var discSystem = DetectDiscSystem(filePath);
                info.System = discSystem;
                info.SystemName = GetSystemDisplayName(discSystem);
                ParseHeader(info, filePath);
                return info;
            }

            if (ExtensionMap.TryGetValue(ext, out var system))
            {
                info.System = system;
                info.SystemName = GetSystemDisplayName(system);
                ParseHeader(info, filePath);
            }
            else
            {
                info.System = RomSystem.Unknown;
                info.SystemName = "Unknown";
                info.IsValid = false;
                info.ErrorMessage = "Unrecognized file extension.";
            }
        }
        catch (IOException ex)
        {
            info.IsValid = false;
            info.ErrorMessage = ex.Message;
        }
        catch (UnauthorizedAccessException ex)
        {
            info.IsValid = false;
            info.ErrorMessage = ex.Message;
        }

        return info;
    }

    private static RomSystem DetectDiscSystem(string filePath)
    {
        string ext = Path.GetExtension(filePath);

        // For .cue files, try to find and inspect the associated .bin/.iso data track
        if (string.Equals(ext, ".cue", StringComparison.OrdinalIgnoreCase))
        {
            string? dataFile = FindCueDataFile(filePath);
            if (dataFile != null && File.Exists(dataFile))
                return DetectDiscSystemFromImage(dataFile);
            return RomSystem.SegaCD; // fallback
        }

        return DetectDiscSystemFromImage(filePath);
    }

    private static string? FindCueDataFile(string cuePath)
    {
        try
        {
            string? dir = Path.GetDirectoryName(cuePath);
            foreach (string line in File.ReadLines(cuePath))
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("FILE", StringComparison.OrdinalIgnoreCase))
                {
                    int firstQuote = trimmed.IndexOf('"');
                    int lastQuote = trimmed.LastIndexOf('"');
                    if (firstQuote >= 0 && lastQuote > firstQuote)
                    {
                        string fileName = trimmed.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
                        return dir != null ? Path.Combine(dir, fileName) : fileName;
                    }
                }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return null;
    }

    private static RomSystem DetectDiscSystemFromImage(string filePath)
    {
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (fs.Length < 256)
                return RomSystem.SegaCD;

            byte[] header = new byte[(int)Math.Min(0x200L, fs.Length)];
            int read = fs.Read(header, 0, header.Length);

            // Check for Sega Saturn: "SEGA SEGASATURN" at offset 0x00
            if (read >= 16)
            {
                string id = System.Text.Encoding.ASCII.GetString(header, 0, 16);
                if (id.Contains("SEGASATURN", StringComparison.Ordinal))
                    return RomSystem.SegaSaturn;
            }

            // Check for Sega Dreamcast: "SEGA SEGAKATANA" or "SEGA DREAMCAST" at offset 0x00
            if (read >= 16)
            {
                string id = System.Text.Encoding.ASCII.GetString(header, 0, 16);
                if (id.Contains("SEGAKATANA", StringComparison.Ordinal) ||
                    id.Contains("DREAMCAST", StringComparison.Ordinal))
                    return RomSystem.SegaDreamcast;
            }

            // Check for Sega CD: "SEGADISC" or "SEGADISK" or "SEGA" at offset 0x00
            if (read >= 16)
            {
                string id = System.Text.Encoding.ASCII.GetString(header, 0, 16);
                if (id.Contains("SEGADISC", StringComparison.Ordinal) ||
                    id.Contains("SEGADISK", StringComparison.Ordinal) ||
                    id.StartsWith("SEGA", StringComparison.Ordinal))
                    return RomSystem.SegaCD;
            }

            // Check for Panasonic 3DO: look for "\x01\x5A\x5A\x5A\x5A\x5A\x01" at start
            if (read >= 7 && header[0] == 0x01 && header[1] == 0x5A && header[2] == 0x5A
                && header[3] == 0x5A && header[4] == 0x5A && header[5] == 0x5A && header[6] == 0x01)
                return RomSystem.Panasonic3DO;

            if (read >= 0x50)
            {
                string id = System.Text.Encoding.ASCII.GetString(header, 0x28, Math.Min(24, read - 0x28));
                if (id.Contains("CD-ROM", StringComparison.Ordinal))
                    return RomSystem.Panasonic3DO;
            }

            // Check for Amiga CD32: "AMIGACD32" marker or "CDTV" in early sectors
            if (read >= 0x50)
            {
                string block = System.Text.Encoding.ASCII.GetString(header, 0, Math.Min(read, 0x50));
                if (block.Contains("AMIGACD", StringComparison.Ordinal))
                    return RomSystem.AmigaCD32;
            }

            // Check for Nintendo Wii: magic word 0x5D1C9EA3 at offset 0x18
            if (read >= 0x1C)
            {
                if (header[0x18] == 0x5D && header[0x19] == 0x1C && header[0x1A] == 0x9E && header[0x1B] == 0xA3)
                    return RomSystem.Wii;
            }

            // Check for Nintendo GameCube: magic word 0xC2339F3D at offset 0x1C
            if (read >= 0x20)
            {
                if (header[0x1C] == 0xC2 && header[0x1D] == 0x33 && header[0x1E] == 0x9F && header[0x1F] == 0x3D)
                    return RomSystem.GameCube;
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        return RomSystem.SegaCD; // fallback for unrecognized disc images
    }

    private static void DetectBin(RomInfo info, string filePath)
    {
        try
        {
            using var fs = File.OpenRead(filePath);
            byte[] header = new byte[(int)Math.Min(512L, fs.Length)];
            int read = fs.Read(header, 0, header.Length);

            if (read >= 0x110)
            {
                string segaMarkerAt0x100 = System.Text.Encoding.ASCII.GetString(header, 0x100, Math.Min(4, read - 0x100));
                string segaMarkerAt0x101 = read >= 0x105 ? System.Text.Encoding.ASCII.GetString(header, 0x101, Math.Min(4, read - 0x101)) : "";
                if (segaMarkerAt0x100.StartsWith("SEGA", StringComparison.Ordinal) || segaMarkerAt0x101.StartsWith("SEGA", StringComparison.Ordinal))
                {
                    info.System = RomSystem.MegaDrive;
                    info.SystemName = GetSystemDisplayName(RomSystem.MegaDrive);
                    ParseMegaDriveHeader(info, header);
                    info.IsValid = true;
                    return;
                }
            }

            info.System = RomSystem.Atari2600;
            info.SystemName = GetSystemDisplayName(RomSystem.Atari2600);
            info.IsValid = true;
        }
        catch (IOException ex)
        {
            info.IsValid = false;
            info.ErrorMessage = ex.Message;
        }
        catch (UnauthorizedAccessException ex)
        {
            info.IsValid = false;
            info.ErrorMessage = ex.Message;
        }
    }

    private static void ParseHeader(RomInfo info, string filePath)
    {
        try
        {
            using var fs = File.OpenRead(filePath);
            byte[] header = new byte[(int)Math.Min(0x200L, fs.Length)];
            int read = fs.Read(header, 0, header.Length);

            switch (info.System)
            {
                case RomSystem.NES:
                    ParseNesHeader(info, header, read);
                    break;
                case RomSystem.SNES:
                    ParseSnesHeader(info, fs);
                    break;
                case RomSystem.N64:
                    ParseN64Header(info, header, read);
                    break;
                case RomSystem.GameBoy:
                case RomSystem.GameBoyColor:
                    ParseGbHeader(info, header, read);
                    break;
                case RomSystem.GameBoyAdvance:
                    ParseGbaHeader(info, header, read);
                    break;
                case RomSystem.VirtualBoy:
                    ParseVirtualBoyHeader(info, fs);
                    break;
                case RomSystem.SegaMasterSystem:
                case RomSystem.GameGear:
                    ParseSmsGgHeader(info, fs);
                    break;
                case RomSystem.MegaDrive:
                    ParseMegaDriveHeader(info, header);
                    info.IsValid = true;
                    break;
                case RomSystem.Sega32X:
                    ParseSega32XHeader(info, header, read);
                    break;
                case RomSystem.Atari7800:
                    ParseAtari7800Header(info, header, read);
                    break;
                case RomSystem.AtariLynx:
                    ParseAtariLynxHeader(info, header, read);
                    break;
                case RomSystem.PCEngine:
                    ParsePceHeader(info, fs);
                    break;
                case RomSystem.NeoGeoPocket:
                    ParseNeoGeoPocketHeader(info, header, read);
                    break;
                case RomSystem.ColecoVision:
                    ParseColecoVisionHeader(info, header, read);
                    break;
                case RomSystem.Intellivision:
                    ParseIntellivisionHeader(info, header, read);
                    break;
                case RomSystem.Atari2600:
                    ParseAtari2600Header(info, fs);
                    break;
                case RomSystem.Atari5200:
                    ParseAtari5200Header(info, header, read);
                    break;
                case RomSystem.AtariJaguar:
                    ParseAtariJaguarHeader(info, header, read);
                    break;
                case RomSystem.WataraSupervision:
                    ParseWataraSupervisionHeader(info, fs);
                    break;
                case RomSystem.MSX:
                case RomSystem.MSX2:
                    ParseMsxHeader(info, header, read);
                    break;
                case RomSystem.SegaCD:
                    ParseSegaCdHeader(info, fs);
                    break;
                case RomSystem.AmstradCPC:
                    ParseAmstradCpcHeader(info, fs);
                    break;
                case RomSystem.Oric:
                    ParseOricHeader(info, header, read);
                    break;
                case RomSystem.ThomsonMO5:
                    ParseThomsonMO5Header(info, header, read);
                    break;
                case RomSystem.ColorComputer:
                    ParseColorComputerHeader(info, header, read);
                    break;
                case RomSystem.Panasonic3DO:
                    ParsePanasonic3DOHeader(info, fs);
                    break;
                case RomSystem.AmigaCD32:
                    ParseAmigaCD32Header(info, fs);
                    break;
                case RomSystem.SegaSaturn:
                    ParseSegaSaturnHeader(info, fs);
                    break;
                case RomSystem.SegaDreamcast:
                    ParseSegaDreamcastHeader(info, fs);
                    break;
                case RomSystem.GameCube:
                    ParseGameCubeHeader(info, fs);
                    break;
                case RomSystem.Wii:
                    ParseWiiHeader(info, fs);
                    break;
                default:
                    info.IsValid = true;
                    break;
            }
        }
        catch (IOException ex)
        {
            info.IsValid = false;
            info.ErrorMessage = ex.Message;
        }
        catch (UnauthorizedAccessException ex)
        {
            info.IsValid = false;
            info.ErrorMessage = ex.Message;
        }
    }

    private static void ParseNesHeader(RomInfo info, byte[] header, int read)
    {
        if (read < 16 || header[0] != 0x4E || header[1] != 0x45 || header[2] != 0x53 || header[3] != 0x1A)
        {
            info.IsValid = false;
            info.ErrorMessage = "Invalid iNES header magic.";
            return;
        }

        bool nes20 = (header[7] & 0x0C) == 0x08;
        info.HeaderInfo["Format"] = nes20 ? "NES 2.0" : "iNES";
        info.HeaderInfo["PRG ROM Banks"] = header[4].ToString();
        info.HeaderInfo["PRG ROM Size"] = $"{header[4] * 16} KB";
        info.HeaderInfo["CHR ROM Banks"] = header[5].ToString();
        info.HeaderInfo["CHR ROM Size"] = header[5] == 0 ? "CHR RAM" : $"{header[5] * 8} KB";
        int mapper = (header[6] >> 4) | (header[7] & 0xF0);
        info.HeaderInfo["Mapper"] = mapper.ToString();
        info.HeaderInfo["Mirroring"] = (header[6] & 0x01) == 0 ? "Horizontal" : "Vertical";
        info.HeaderInfo["Battery"] = (header[6] & 0x02) != 0 ? "Yes" : "No";
        info.HeaderInfo["Trainer"] = (header[6] & 0x04) != 0 ? "Yes" : "No";
        info.IsValid = true;
    }

    private static void ParseSnesHeader(RomInfo info, FileStream fs)
    {
        try
        {
            long fileLen = fs.Length;

            // Determine if a 512-byte copier header is present
            int copierOffset = (fileLen % 1024 == 512) ? 512 : 0;
            if (copierOffset > 0)
                info.HeaderInfo["Copier Header"] = "Present (512 bytes)";

            // Try LoROM (0x7FC0) and HiROM (0xFFC0) header offsets
            int loRomOffset = copierOffset + 0x7FC0;
            int hiRomOffset = copierOffset + 0xFFC0;

            byte[]? loRomHeader = TryReadSnesHeader(fs, loRomOffset);
            byte[]? hiRomHeader = TryReadSnesHeader(fs, hiRomOffset);

            byte[]? chosenHeader = null;
            string mapMode = "Unknown";

            if (hiRomHeader != null && IsValidSnesHeader(hiRomHeader))
            {
                chosenHeader = hiRomHeader;
                mapMode = "HiROM";
            }
            else if (loRomHeader != null && IsValidSnesHeader(loRomHeader))
            {
                chosenHeader = loRomHeader;
                mapMode = "LoROM";
            }

            if (chosenHeader != null)
            {
                string title = System.Text.Encoding.ASCII.GetString(chosenHeader, 0, 21).TrimEnd('\0', ' ');
                info.HeaderInfo["Title"] = title;
                info.HeaderInfo["Map Mode"] = mapMode;

                // Report whether the internal checksum is valid
                int checksum = (chosenHeader[0x1F] << 8) | chosenHeader[0x1E];
                int complement = (chosenHeader[0x1D] << 8) | chosenHeader[0x1C];
                info.HeaderInfo["Checksum Valid"] = (checksum ^ complement) == 0xFFFF ? "Yes" : "No";

                byte romMakeup = chosenHeader[0x15];
                info.HeaderInfo["ROM Makeup"] = $"0x{romMakeup:X2}";
                info.HeaderInfo["Fast ROM"] = (romMakeup & 0x10) != 0 ? "Yes" : "No";

                byte romType = chosenHeader[0x16];
                info.HeaderInfo["Chipset"] = $"0x{romType:X2}";

                byte romSizeCode = chosenHeader[0x17];
                int romSizeKb = romSizeCode < 24 ? (1 << romSizeCode) : 0;
                info.HeaderInfo["ROM Size"] = romSizeKb > 0 ? $"{romSizeKb} KB" : "Unknown";

                byte ramSizeCode = chosenHeader[0x18];
                int ramSizeKb = ramSizeCode is > 0 and < 24 ? (1 << ramSizeCode) : 0;
                info.HeaderInfo["RAM Size"] = ramSizeKb > 0 ? $"{ramSizeKb} KB" : "None";
            }
            else
            {
                info.HeaderInfo["Format"] = "SNES ROM (header not detected)";
            }

            info.IsValid = true;
        }
        catch (IOException ex)
        {
            info.HeaderInfo["Format"] = "SNES ROM";
            info.ErrorMessage = ex.Message;
        }
    }

    private static byte[]? TryReadSnesHeader(FileStream fs, int offset)
    {
        if (offset < 0 || offset + 0x20 > fs.Length)
            return null;

        fs.Seek(offset, SeekOrigin.Begin);
        byte[] buf = new byte[0x20];
        int read = fs.Read(buf, 0, buf.Length);
        return read == 0x20 ? buf : null;
    }

    private static bool IsValidSnesHeader(byte[] h)
    {
        // Validate checksum complement: bytes 0x1C-0x1D (complement) + 0x1E-0x1F (checksum) should equal 0xFFFF
        int checksum = (h[0x1F] << 8) | h[0x1E];
        int complement = (h[0x1D] << 8) | h[0x1C];
        if ((checksum ^ complement) != 0xFFFF)
            return false;

        // Verify the title field contains mostly printable ASCII
        int printable = 0;
        for (int i = 0; i < 21; i++)
        {
            if (h[i] >= 0x20 && h[i] <= 0x7E)
                printable++;
        }
        return printable >= 10;
    }

    private static void ParseN64Header(RomInfo info, byte[] header, int read)
    {
        if (read < 4)
        {
            info.IsValid = false;
            info.ErrorMessage = "File too small to parse N64 header.";
            return;
        }

        var format = N64FormatConverter.DetectFormat(header);
        string endian = format switch
        {
            N64FormatConverter.N64Format.BigEndian => "Big Endian (.z64)",
            N64FormatConverter.N64Format.LittleEndian => "Little Endian (.n64)",
            N64FormatConverter.N64Format.ByteSwapped => "Mid Endian (.v64)",
            _ => "Unknown"
        };

        info.HeaderInfo["Byte Order"] = endian;

        // Normalize header bytes to Big Endian for correct field parsing
        if (format != null)
            N64FormatConverter.NormalizeToBigEndian(header, read, format.Value);

        if (read >= 0x18) // need bytes 0x10-0x17 for CRC1 and CRC2
        {
            uint crc1 = (uint)((header[0x10] << 24) | (header[0x11] << 16) | (header[0x12] << 8) | header[0x13]);
            uint crc2 = (uint)((header[0x14] << 24) | (header[0x15] << 16) | (header[0x16] << 8) | header[0x17]);
            info.HeaderInfo["CRC1"] = crc1.ToString("X8");
            info.HeaderInfo["CRC2"] = crc2.ToString("X8");
        }

        if (read >= 0x34) // title field spans 0x20..0x33 (20 bytes)
        {
            string title = System.Text.Encoding.ASCII.GetString(header, 0x20, 20).TrimEnd('\0', ' ');
            info.HeaderInfo["Title"] = title;
        }

        info.IsValid = format != null;
        if (!info.IsValid) info.ErrorMessage = "Unrecognized N64 ROM byte order.";
    }

    private static void ParseGbHeader(RomInfo info, byte[] header, int read)
    {
        if (read >= 0x108)
        {
            bool validLogo = header[0x104] == 0xCE && header[0x105] == 0xED &&
                             header[0x106] == 0x66 && header[0x107] == 0x66;
            if (!validLogo)
            {
                info.IsValid = false;
                info.ErrorMessage = "Nintendo logo bytes not found at 0x104.";
                return;
            }
        }

        if (read >= 0x149)
        {
            string title = System.Text.Encoding.ASCII.GetString(header, 0x134, 15).TrimEnd('\0', ' ');
            info.HeaderInfo["Title"] = title;

            byte cartType = header[0x147];
            info.HeaderInfo["Cartridge Type"] = $"0x{cartType:X2}";

            byte romSizeCode = header[0x148];
            int romSizeKb = 32 << romSizeCode;
            info.HeaderInfo["ROM Size"] = $"{romSizeKb} KB";

            byte cgbFlag = header[0x143];
            if (info.System == RomSystem.GameBoyColor || (cgbFlag & 0x80) != 0)
            {
                info.System = RomSystem.GameBoyColor;
                info.SystemName = GetSystemDisplayName(RomSystem.GameBoyColor);
                info.HeaderInfo["CGB Flag"] = $"0x{cgbFlag:X2}";
            }
        }

        info.IsValid = true;
    }

    private static void ParseGbaHeader(RomInfo info, byte[] header, int read)
    {
        if (read >= 0x0C)
        {
            bool validLogo = header[0x04] == 0x24 && header[0x05] == 0xFF &&
                             header[0x06] == 0xAE && header[0x07] == 0x51;
            info.HeaderInfo["GBA Logo"] = validLogo ? "Valid" : "Invalid";

            if (!validLogo)
            {
                info.IsValid = false;
                info.ErrorMessage = "GBA header logo not found.";
                return;
            }
        }

        if (read >= 0xB0)
        {
            string title = System.Text.Encoding.ASCII.GetString(header, 0xA0, 12).TrimEnd('\0', ' ');
            string gameCode = System.Text.Encoding.ASCII.GetString(header, 0xAC, 4).TrimEnd('\0', ' ');
            info.HeaderInfo["Title"] = title;
            info.HeaderInfo["Game Code"] = gameCode;
        }

        info.IsValid = true;
    }

    private static void ParseMegaDriveHeader(RomInfo info, byte[] header)
    {
        if (header.Length >= 0x120)
        {
            string domestic = System.Text.Encoding.ASCII.GetString(header, 0x120, Math.Min(48, header.Length - 0x120)).TrimEnd('\0', ' ');
            info.HeaderInfo["Domestic Title"] = domestic;
        }
        if (header.Length >= 0x150)
        {
            string overseas = System.Text.Encoding.ASCII.GetString(header, 0x150, Math.Min(48, header.Length - 0x150)).TrimEnd('\0', ' ');
            info.HeaderInfo["Overseas Title"] = overseas;
        }
    }

    private static void ParseAtari7800Header(RomInfo info, byte[] header, int read)
    {
        if (read >= 10)
        {
            string magic = System.Text.Encoding.ASCII.GetString(header, 1, Math.Min(9, read - 1));
            if (magic.Contains("ATARI7800"))
            {
                info.HeaderInfo["Format"] = "Atari 7800";
                info.IsValid = true;
                return;
            }
        }
        // File has .a78 extension but no ATARI7800 header signature — likely still valid
        // as many Atari 7800 ROMs lack the magic header bytes
        info.HeaderInfo["Format"] = "Atari 7800 (no header signature)";
        info.IsValid = true;
    }

    private static void ParseAtariLynxHeader(RomInfo info, byte[] header, int read)
    {
        // .lnx files may have a 64-byte "LYNX" header
        if (read >= 64 && header[0] == 0x4C && header[1] == 0x59 && header[2] == 0x4E && header[3] == 0x58) // "LYNX"
        {
            info.HeaderInfo["Format"] = "Atari Lynx (.lnx with header)";
            if (read >= 10)
            {
                int bankSize0 = header[6] | (header[7] << 8);
                int bankSize1 = header[8] | (header[9] << 8);
                if (bankSize0 > 0) info.HeaderInfo["Bank 0 Size"] = $"{bankSize0} bytes";
                if (bankSize1 > 0) info.HeaderInfo["Bank 1 Size"] = $"{bankSize1} bytes";
            }
            info.IsValid = true;
            return;
        }
        // .lyx files or headerless .lnx — assume valid by extension
        info.HeaderInfo["Format"] = "Atari Lynx (no header)";
        info.IsValid = true;
    }

    private static void ParseVirtualBoyHeader(RomInfo info, FileStream fs)
    {
        // Virtual Boy header is at the end of the ROM, offset -544 from EOF (0xFFFFFDE0 mapped)
        // The title is 20 bytes at ROM end - 544, publisher code at -524, game code at -522
        long fileLen = fs.Length;
        if (fileLen < 1024)
        {
            info.IsValid = true;
            return;
        }

        int headerOffset = (int)(fileLen - 544);
        if (headerOffset < 0)
        {
            info.IsValid = true;
            return;
        }

        byte[] hdr = new byte[32];
        fs.Seek(headerOffset, SeekOrigin.Begin);
        int read = fs.Read(hdr, 0, hdr.Length);
        if (read >= 20)
        {
            string title = System.Text.Encoding.ASCII.GetString(hdr, 0, 20).TrimEnd('\0', ' ');
            if (!string.IsNullOrWhiteSpace(title))
                info.HeaderInfo["Title"] = title;
        }
        if (read >= 26)
        {
            string gameCode = System.Text.Encoding.ASCII.GetString(hdr, 22, 4).TrimEnd('\0', ' ');
            if (!string.IsNullOrWhiteSpace(gameCode))
                info.HeaderInfo["Game Code"] = gameCode;
        }
        if (read >= 27)
        {
            byte version = hdr[26];
            info.HeaderInfo["Version"] = $"1.{version}";
        }

        info.IsValid = true;
    }

    private static void ParseSmsGgHeader(RomInfo info, FileStream fs)
    {
        // Sega Master System / Game Gear ROMs have a "TMR SEGA" marker
        // typically at 0x7FF0 (8 KB boundary) or 0x1FF0 or 0x3FF0
        long fileLen = fs.Length;
        int[] offsets = [0x7FF0, 0x3FF0, 0x1FF0];

        foreach (int offset in offsets)
        {
            if (offset + 16 > fileLen) continue;

            fs.Seek(offset, SeekOrigin.Begin);
            byte[] marker = new byte[16];
            if (fs.Read(marker, 0, 16) < 16) continue;

            string sig = System.Text.Encoding.ASCII.GetString(marker, 0, 8);
            if (sig == "TMR SEGA")
            {
                info.HeaderInfo["Signature"] = "TMR SEGA";

                // Byte 0x0F contains region (upper nibble) and ROM size code (lower nibble)
                byte regionSizeByte = marker[0x0F];
                int regionCode = (regionSizeByte >> 4) & 0x0F;
                string region = regionCode switch
                {
                    3 => "SMS Japan",
                    4 => "SMS Export",
                    5 => "Game Gear Japan",
                    6 => "Game Gear Export",
                    7 => "Game Gear International",
                    _ => $"Unknown (0x{regionCode:X1})"
                };
                info.HeaderInfo["Region"] = region;

                // Auto-detect Game Gear vs SMS from the region code
                if (regionCode >= 5 && regionCode <= 7 && info.System == RomSystem.SegaMasterSystem)
                {
                    info.System = RomSystem.GameGear;
                    info.SystemName = GetSystemDisplayName(RomSystem.GameGear);
                }
                else if (regionCode >= 3 && regionCode <= 4 && info.System == RomSystem.GameGear)
                {
                    info.System = RomSystem.SegaMasterSystem;
                    info.SystemName = GetSystemDisplayName(RomSystem.SegaMasterSystem);
                }

                // Checksum at bytes 0x0A-0x0B
                int checksum = marker[0x0A] | (marker[0x0B] << 8);
                info.HeaderInfo["Header Checksum"] = $"0x{checksum:X4}";

                // Product code from bytes 0x0C-0x0E (BCD encoded)
                int productLow = marker[0x0C] | (marker[0x0D] << 8);
                int productHigh = (marker[0x0E] >> 4) & 0x0F;
                info.HeaderInfo["Product Code"] = $"{productHigh}{productLow:X4}";

                // ROM size code from lower nibble of byte 0x0F
                int sizeCode = regionSizeByte & 0x0F;
                string sizeDesc = sizeCode switch
                {
                    0x0A => "8 KB",
                    0x0B => "16 KB",
                    0x0C => "32 KB",
                    0x0D => "48 KB",
                    0x0E => "64 KB",
                    0x0F => "128 KB",
                    0x00 => "256 KB",
                    0x01 => "512 KB",
                    0x02 => "1 MB",
                    _ => $"Unknown (0x{sizeCode:X1})"
                };
                info.HeaderInfo["Declared ROM Size"] = sizeDesc;

                info.IsValid = true;
                return;
            }
        }

        // No TMR SEGA marker found — many early SMS/GG ROMs lack it
        info.HeaderInfo["Signature"] = "None (no TMR SEGA marker)";
        info.IsValid = true;
    }

    private static void ParseSega32XHeader(RomInfo info, byte[] header, int read)
    {
        // 32X ROMs share the Mega Drive header format with additional 32X markers
        if (read >= 0x110)
        {
            string segaMarker = System.Text.Encoding.ASCII.GetString(header, 0x100, Math.Min(16, read - 0x100)).TrimEnd('\0', ' ');
            if (segaMarker.StartsWith("SEGA", StringComparison.Ordinal))
                info.HeaderInfo["Signature"] = segaMarker;
        }

        if (read >= 0x120)
        {
            string domestic = System.Text.Encoding.ASCII.GetString(header, 0x120, Math.Min(48, read - 0x120)).TrimEnd('\0', ' ');
            if (!string.IsNullOrWhiteSpace(domestic))
                info.HeaderInfo["Domestic Title"] = domestic;
        }
        if (read >= 0x150)
        {
            string overseas = System.Text.Encoding.ASCII.GetString(header, 0x150, Math.Min(48, read - 0x150)).TrimEnd('\0', ' ');
            if (!string.IsNullOrWhiteSpace(overseas))
                info.HeaderInfo["Overseas Title"] = overseas;
        }

        info.IsValid = true;
    }

    private static void ParsePceHeader(RomInfo info, FileStream fs)
    {
        // PC Engine / TurboGrafx-16 ROMs may have a 512-byte copier header
        long fileLen = fs.Length;
        bool hasCopierHeader = (fileLen % 8192) == 512;
        if (hasCopierHeader)
            info.HeaderInfo["Copier Header"] = "Present (512 bytes)";

        long romSize = hasCopierHeader ? fileLen - 512 : fileLen;
        info.HeaderInfo["ROM Size"] = FileUtils.FormatFileSize(romSize);

        info.IsValid = true;
    }

    private static void ParseNeoGeoPocketHeader(RomInfo info, byte[] header, int read)
    {
        // Neo Geo Pocket / Pocket Color header structure:
        // 0x00-0x0B: Copyright string (e.g., "COPYRIGHT BY SNK")
        // 0x0C-0x0F: License code
        // 0x10-0x1F: Game ID, version, etc.
        // 0x24-0x2F: Title (12 bytes)
        if (read < 0x30)
        {
            info.IsValid = true;
            return;
        }

        string copyright = System.Text.Encoding.ASCII.GetString(header, 0x00, Math.Min(12, read)).TrimEnd('\0', ' ');
        if (copyright.Contains("SNK"))
            info.HeaderInfo["Copyright"] = copyright;

        // Game name at offset 0x24 (12 bytes)
        if (read >= 0x30)
        {
            string title = System.Text.Encoding.ASCII.GetString(header, 0x24, 12).TrimEnd('\0', ' ');
            if (!string.IsNullOrWhiteSpace(title))
                info.HeaderInfo["Title"] = title;
        }

        // System mode at 0x23: 0x00 = Neo Geo Pocket, 0x10 = Neo Geo Pocket Color
        if (read >= 0x24)
        {
            byte mode = header[0x23];
            string modeStr = mode switch
            {
                0x00 => "Neo Geo Pocket (B&W)",
                0x10 => "Neo Geo Pocket Color",
                _ => $"Unknown (0x{mode:X2})"
            };
            info.HeaderInfo["System Mode"] = modeStr;
        }

        info.IsValid = true;
    }

    private static void ParseColecoVisionHeader(RomInfo info, byte[] header, int read)
    {
        // ColecoVision ROMs typically start with bytes 0xAA and 0x55 (or 0x55 and 0xAA)
        if (read >= 2)
        {
            bool validMagic = (header[0] == 0xAA && header[1] == 0x55) ||
                              (header[0] == 0x55 && header[1] == 0xAA);
            info.HeaderInfo["Magic Bytes"] = validMagic
                ? $"Valid (0x{header[0]:X2} 0x{header[1]:X2})"
                : $"Non-standard (0x{header[0]:X2} 0x{header[1]:X2})";
        }

        info.IsValid = true;
    }

    private static void ParseIntellivisionHeader(RomInfo info, byte[] header, int read)
    {
        // Intellivision ROMs typically have specific values at the start
        // The first two words (big-endian) are often recognizable
        if (read >= 4)
        {
            ushort word0 = (ushort)((header[0] << 8) | header[1]);
            info.HeaderInfo["Entry Point"] = $"0x{word0:X4}";
        }

        info.IsValid = true;
    }

    private static void ParseAtari2600Header(RomInfo info, FileStream fs)
    {
        // Atari 2600 ROMs have no standard header; infer bankswitching scheme from file size
        long size = fs.Length;
        string bankswitch = size switch
        {
            2048 => "2K (no bankswitching)",
            4096 => "4K (no bankswitching)",
            8192 => "F8 (8K bankswitching)",
            12288 => "FA (CBS RAM Plus, 12K)",
            16384 => "F6 (16K bankswitching)",
            32768 => "F4 (32K bankswitching)",
            65536 => "EF (64K bankswitching)",
            _ => $"Non-standard ({size} bytes)"
        };

        info.HeaderInfo["Bankswitching"] = bankswitch;
        info.HeaderInfo["ROM Size"] = FileUtils.FormatFileSize(size);
        info.IsValid = true;
    }

    private static void ParseAtari5200Header(RomInfo info, byte[] header, int read)
    {
        // Atari 5200 cartridges store the start address at the last 2 bytes of the ROM.
        // The first bytes often hold the cartridge code.
        if (read >= 4)
        {
            info.HeaderInfo["First Bytes"] = $"0x{header[0]:X2} 0x{header[1]:X2} 0x{header[2]:X2} 0x{header[3]:X2}";
        }

        info.IsValid = true;
    }

    private static void ParseAtariJaguarHeader(RomInfo info, byte[] header, int read)
    {
        // Atari Jaguar ROMs may contain identification strings at offsets 0x400+
        // Check for the common ROM header structure
        if (read >= 0x24)
        {
            // Jaguar ROM header has entry point and other data
            uint entryPoint = (uint)((header[0] << 24) | (header[1] << 16) | (header[2] << 8) | header[3]);
            info.HeaderInfo["Entry Point"] = $"0x{entryPoint:X8}";
        }

        info.IsValid = true;
    }

    private static void ParseWataraSupervisionHeader(RomInfo info, FileStream fs)
    {
        // Watara Supervision has minimal header; infer type from size
        long size = fs.Length;
        string cartType = size switch
        {
            <= 16384 => "16K cartridge",
            <= 32768 => "32K cartridge",
            <= 65536 => "64K cartridge",
            _ => "Extended cartridge"
        };

        info.HeaderInfo["Cartridge Type"] = cartType;
        info.HeaderInfo["ROM Size"] = FileUtils.FormatFileSize(size);
        info.IsValid = true;
    }

    private static void ParseMsxHeader(RomInfo info, byte[] header, int read)
    {
        // MSX ROM cartridges start with "AB" (0x41 0x42) at offset 0
        if (read >= 2)
        {
            bool hasMagic = header[0] == 0x41 && header[1] == 0x42;
            info.HeaderInfo["Cartridge Marker"] = hasMagic ? "Valid (AB)" : $"Non-standard (0x{header[0]:X2} 0x{header[1]:X2})";

            if (hasMagic && read >= 8)
            {
                ushort initAddr = (ushort)(header[2] | (header[3] << 8));
                ushort statementAddr = (ushort)(header[4] | (header[5] << 8));
                ushort deviceAddr = (ushort)(header[6] | (header[7] << 8));
                info.HeaderInfo["Init Address"] = $"0x{initAddr:X4}";
                if (statementAddr != 0)
                    info.HeaderInfo["BASIC Statement"] = $"0x{statementAddr:X4}";
                if (deviceAddr != 0)
                    info.HeaderInfo["Device Handler"] = $"0x{deviceAddr:X4}";
            }
        }

        info.IsValid = true;
    }

    private static void ParseSegaCdHeader(RomInfo info, FileStream fs)
    {
        // Sega CD ISO images contain an IP.BIN (Initial Program) at the start
        // with system header information at offset 0x00
        long fileLen = fs.Length;
        if (fileLen < 0x200)
        {
            info.IsValid = true;
            return;
        }

        byte[] header = new byte[0x200];
        fs.Seek(0, SeekOrigin.Begin);
        int read = fs.Read(header, 0, header.Length);

        if (read >= 0x10)
        {
            string systemId = System.Text.Encoding.ASCII.GetString(header, 0x00, 16).TrimEnd('\0', ' ');
            if (systemId.Contains("SEGA") || systemId.Contains("SEGADISC") || systemId.Contains("SEGADISK"))
                info.HeaderInfo["System ID"] = systemId;
        }

        if (read >= 0x30)
        {
            string volumeId = System.Text.Encoding.ASCII.GetString(header, 0x10, 16).TrimEnd('\0', ' ');
            if (!string.IsNullOrWhiteSpace(volumeId))
                info.HeaderInfo["Volume ID"] = volumeId;
        }

        if (read >= 0x120)
        {
            string domestic = System.Text.Encoding.ASCII.GetString(header, 0x120, Math.Min(48, read - 0x120)).TrimEnd('\0', ' ');
            if (!string.IsNullOrWhiteSpace(domestic))
                info.HeaderInfo["Domestic Title"] = domestic;
        }

        if (read >= 0x150)
        {
            string overseas = System.Text.Encoding.ASCII.GetString(header, 0x150, Math.Min(48, read - 0x150)).TrimEnd('\0', ' ');
            if (!string.IsNullOrWhiteSpace(overseas))
                info.HeaderInfo["Overseas Title"] = overseas;
        }

        if (read >= 0x1F0)
        {
            string region = System.Text.Encoding.ASCII.GetString(header, 0x1F0, Math.Min(16, read - 0x1F0)).TrimEnd('\0', ' ');
            if (!string.IsNullOrWhiteSpace(region))
                info.HeaderInfo["Region"] = region;
        }

        info.IsValid = true;
    }

    private static void ParseAmstradCpcHeader(RomInfo info, FileStream fs)
    {
        // Amstrad CPC DSK format has a Disc Information Block header
        long fileLen = fs.Length;
        if (fileLen < 0x100)
        {
            info.IsValid = true;
            return;
        }

        byte[] header = new byte[0x100];
        fs.Seek(0, SeekOrigin.Begin);
        int read = fs.Read(header, 0, header.Length);

        if (read >= 0x30)
        {
            string magic = System.Text.Encoding.ASCII.GetString(header, 0, Math.Min(34, read)).TrimEnd('\0', ' ');
            if (magic.Contains("MV - CPC") || magic.Contains("EXTENDED CPC DSK"))
            {
                info.HeaderInfo["Format"] = magic.Contains("EXTENDED") ? "Extended DSK" : "Standard DSK";

                string creator = System.Text.Encoding.ASCII.GetString(header, 0x22, Math.Min(14, read - 0x22)).TrimEnd('\0', ' ');
                if (!string.IsNullOrWhiteSpace(creator))
                    info.HeaderInfo["Creator"] = creator;

                if (read >= 0x32)
                {
                    byte tracks = header[0x30];
                    byte sides = header[0x31];
                    info.HeaderInfo["Tracks"] = tracks.ToString();
                    info.HeaderInfo["Sides"] = sides.ToString();
                }
            }
            else
            {
                info.HeaderInfo["Format"] = "Amstrad CPC (non-DSK)";
            }
        }

        info.IsValid = true;
    }

    private static void ParseOricHeader(RomInfo info, byte[] header, int read)
    {
        // Oric TAP format: sync bytes followed by header with type, start, end, and filename
        if (read >= 13)
        {
            // Look for header after sync bytes (0x16 repeated, then 0x24)
            int headerStart = -1;
            for (int i = 0; i < read - 9; i++)
            {
                if (header[i] == 0x24)
                {
                    headerStart = i;
                    break;
                }
            }

            if (headerStart >= 0 && headerStart + 9 <= read)
            {
                int pos = headerStart + 1;
                if (pos + 8 <= read)
                {
                    byte fileType = header[pos + 2];
                    info.HeaderInfo["File Type"] = fileType == 0x00 ? "BASIC" : "Machine Code";

                    ushort endAddr = (ushort)((header[pos + 4] << 8) | header[pos + 5]);
                    ushort startAddr = (ushort)((header[pos + 6] << 8) | header[pos + 7]);
                    info.HeaderInfo["Start Address"] = $"0x{startAddr:X4}";
                    info.HeaderInfo["End Address"] = $"0x{endAddr:X4}";

                    // Filename follows at pos + 9 (null-terminated)
                    if (pos + 9 < read)
                    {
                        int nameEnd = pos + 9;
                        while (nameEnd < read && header[nameEnd] != 0x00)
                            nameEnd++;
                        if (nameEnd > pos + 9)
                        {
                            string name = System.Text.Encoding.ASCII.GetString(header, pos + 9, nameEnd - (pos + 9)).TrimEnd('\0', ' ');
                            if (!string.IsNullOrWhiteSpace(name))
                                info.HeaderInfo["Program Name"] = name;
                        }
                    }
                }
            }
            else
            {
                info.HeaderInfo["Format"] = "Oric TAP";
            }
        }

        info.IsValid = true;
    }

    private static void ParseThomsonMO5Header(RomInfo info, byte[] header, int read)
    {
        // Thomson MO5 .k7 cassette format and .mo5 cartridge format
        if (read >= 5)
        {
            // K7 cassettes may start with a leader tone (0x01 repeated) then 0x3C5A
            bool isK7 = false;
            for (int i = 0; i < read - 1; i++)
            {
                if (header[i] == 0x3C && i + 1 < read && header[i + 1] == 0x5A)
                {
                    isK7 = true;
                    info.HeaderInfo["Format"] = "K7 Cassette";
                    break;
                }
            }

            if (!isK7)
            {
                info.HeaderInfo["Format"] = "Thomson MO5 ROM";
            }
        }

        info.HeaderInfo["ROM Size"] = FileUtils.FormatFileSize(read);
        info.IsValid = true;
    }

    private static void ParseColorComputerHeader(RomInfo info, byte[] header, int read)
    {
        // Color Computer .ccc cartridge: first two bytes are often the entry vector
        if (read >= 5)
        {
            // CoCo ROM cartridges start execution at the address pointed to by 0xFFFE
            // but the cartridge data itself starts with meaningful bytes
            byte startHi = header[0];
            byte startLo = header[1];
            ushort startVector = (ushort)((startHi << 8) | startLo);
            info.HeaderInfo["Start Vector"] = $"0x{startVector:X4}";

            info.HeaderInfo["ROM Size"] = FileUtils.FormatFileSize(read);
        }

        info.IsValid = true;
    }

    private static void ParsePanasonic3DOHeader(RomInfo info, FileStream fs)
    {
        long fileLen = fs.Length;
        if (fileLen < 0x100)
        {
            info.IsValid = true;
            return;
        }

        byte[] header = new byte[0x100];
        fs.Seek(0, SeekOrigin.Begin);
        int read = fs.Read(header, 0, header.Length);

        if (read >= 7 && header[0] == 0x01 && header[1] == 0x5A && header[2] == 0x5A
            && header[3] == 0x5A && header[4] == 0x5A && header[5] == 0x5A && header[6] == 0x01)
        {
            info.HeaderInfo["Format"] = "3DO Disc Image";
        }

        if (read >= 0x50)
        {
            string volumeId = System.Text.Encoding.ASCII.GetString(header, 0x28, Math.Min(32, read - 0x28)).TrimEnd('\0', ' ');
            if (!string.IsNullOrWhiteSpace(volumeId))
                info.HeaderInfo["Volume ID"] = volumeId;
        }

        info.IsValid = true;
    }

    private static void ParseAmigaCD32Header(RomInfo info, FileStream fs)
    {
        long fileLen = fs.Length;
        if (fileLen < 0x100)
        {
            info.IsValid = true;
            return;
        }

        byte[] header = new byte[0x100];
        fs.Seek(0, SeekOrigin.Begin);
        int read = fs.Read(header, 0, header.Length);

        if (read >= 0x50)
        {
            string block = System.Text.Encoding.ASCII.GetString(header, 0, Math.Min(read, 0x50)).TrimEnd('\0', ' ');
            if (block.Contains("AMIGACD", StringComparison.Ordinal))
                info.HeaderInfo["Format"] = "Amiga CD32 Disc Image";
        }

        info.IsValid = true;
    }

    private static void ParseSegaSaturnHeader(RomInfo info, FileStream fs)
    {
        long fileLen = fs.Length;
        if (fileLen < 0x100)
        {
            info.IsValid = true;
            return;
        }

        byte[] header = new byte[0x100];
        fs.Seek(0, SeekOrigin.Begin);
        int read = fs.Read(header, 0, header.Length);

        if (read >= 16)
        {
            string systemId = System.Text.Encoding.ASCII.GetString(header, 0x00, 16).TrimEnd('\0', ' ');
            if (!string.IsNullOrWhiteSpace(systemId))
                info.HeaderInfo["System ID"] = systemId;
        }

        if (read >= 0x70)
        {
            string title = System.Text.Encoding.ASCII.GetString(header, 0x60, Math.Min(16, read - 0x60)).TrimEnd('\0', ' ');
            if (!string.IsNullOrWhiteSpace(title))
                info.HeaderInfo["Title"] = title;
        }

        if (read >= 0x50)
        {
            string region = System.Text.Encoding.ASCII.GetString(header, 0x40, Math.Min(16, read - 0x40)).TrimEnd('\0', ' ');
            if (!string.IsNullOrWhiteSpace(region))
                info.HeaderInfo["Region"] = region;
        }

        if (read >= 0x30)
        {
            string productNo = System.Text.Encoding.ASCII.GetString(header, 0x20, Math.Min(10, read - 0x20)).TrimEnd('\0', ' ');
            if (!string.IsNullOrWhiteSpace(productNo))
                info.HeaderInfo["Product Number"] = productNo;
        }

        info.IsValid = true;
    }

    private static void ParseSegaDreamcastHeader(RomInfo info, FileStream fs)
    {
        // Dreamcast disc IP.BIN header:
        // 0x00-0x0F: Hardware ID ("SEGA SEGAKATANA " or "SEGA DREAMCAST  ")
        // 0x10-0x1F: Maker ID
        // 0x20-0x2F: Device Information
        // 0x30-0x3F: Area Symbols (region codes)
        // 0x40-0x4F: Peripherals
        // 0x50-0x59: Product Number
        // 0x5A-0x5F: Product Version
        // 0x60-0x6F: Release Date
        // 0x70-0x7F: Boot Filename
        // 0x80-0xFF: Game Title
        long fileLen = fs.Length;
        if (fileLen < 0x100)
        {
            info.IsValid = true;
            return;
        }

        byte[] header = new byte[0x100];
        fs.Seek(0, SeekOrigin.Begin);
        int read = fs.Read(header, 0, header.Length);

        if (read >= 0x10)
        {
            string hardwareId = System.Text.Encoding.ASCII.GetString(header, 0x00, 16).TrimEnd('\0', ' ');
            if (!string.IsNullOrWhiteSpace(hardwareId))
                info.HeaderInfo["Hardware ID"] = hardwareId;
        }

        if (read >= 0x20)
        {
            string makerId = System.Text.Encoding.ASCII.GetString(header, 0x10, 16).TrimEnd('\0', ' ');
            if (!string.IsNullOrWhiteSpace(makerId))
                info.HeaderInfo["Maker ID"] = makerId;
        }

        if (read >= 0x40)
        {
            string areaSymbols = System.Text.Encoding.ASCII.GetString(header, 0x30, 16).TrimEnd('\0', ' ');
            if (!string.IsNullOrWhiteSpace(areaSymbols))
                info.HeaderInfo["Region"] = areaSymbols;
        }

        if (read >= 0x5A)
        {
            string productNo = System.Text.Encoding.ASCII.GetString(header, 0x50, 10).TrimEnd('\0', ' ');
            if (!string.IsNullOrWhiteSpace(productNo))
                info.HeaderInfo["Product Number"] = productNo;
        }

        if (read >= 0x60)
        {
            string version = System.Text.Encoding.ASCII.GetString(header, 0x5A, 6).TrimEnd('\0', ' ');
            if (!string.IsNullOrWhiteSpace(version))
                info.HeaderInfo["Version"] = version;
        }

        if (read >= 0x70)
        {
            string releaseDate = System.Text.Encoding.ASCII.GetString(header, 0x60, 16).TrimEnd('\0', ' ');
            if (!string.IsNullOrWhiteSpace(releaseDate))
                info.HeaderInfo["Release Date"] = releaseDate;
        }

        if (read >= 0x100)
        {
            string title = System.Text.Encoding.ASCII.GetString(header, 0x80, Math.Min(128, read - 0x80)).TrimEnd('\0', ' ');
            int nullIdx = title.IndexOf('\0');
            if (nullIdx >= 0)
                title = title[..nullIdx];
            if (!string.IsNullOrWhiteSpace(title))
                info.HeaderInfo["Title"] = title;
        }

        info.IsValid = true;
    }

    private static void ParseGameCubeHeader(RomInfo info, FileStream fs)
    {
        // GameCube disc header: 0x00 = Game ID (6 bytes), 0x20 = Title (0x3E0 bytes),
        // 0x1C = Magic word (0xC2339F3D), region at Game ID byte 3
        long fileLen = fs.Length;
        if (fileLen < 0x400)
        {
            info.IsValid = true;
            return;
        }

        byte[] header = new byte[0x400];
        fs.Seek(0, SeekOrigin.Begin);
        int read = fs.Read(header, 0, header.Length);

        if (read >= 6)
        {
            string gameId = System.Text.Encoding.ASCII.GetString(header, 0x00, 6).TrimEnd('\0', ' ');
            if (!string.IsNullOrWhiteSpace(gameId))
                info.HeaderInfo["Game ID"] = gameId;

            // Region code is the 4th character of the Game ID
            if (gameId.Length >= 4)
            {
                char regionChar = gameId[3];
                string region = regionChar switch
                {
                    'E' => "USA",
                    'P' => "Europe",
                    'J' => "Japan",
                    'K' => "Korea",
                    'W' => "Taiwan",
                    _ => $"Unknown ({regionChar})"
                };
                info.HeaderInfo["Region"] = region;
            }
        }

        if (read >= 0x20)
        {
            // Verify magic word at 0x1C
            bool hasMagic = header[0x1C] == 0xC2 && header[0x1D] == 0x33
                         && header[0x1E] == 0x9F && header[0x1F] == 0x3D;
            info.HeaderInfo["Format"] = hasMagic ? "GameCube Disc Image" : "GameCube Disc Image (no magic)";
        }

        if (read >= 0x60)
        {
            string title = System.Text.Encoding.ASCII.GetString(header, 0x20, Math.Min(0x3E0, read - 0x20)).TrimEnd('\0', ' ');
            // Truncate at first null if the title has embedded nulls
            int nullIdx = title.IndexOf('\0');
            if (nullIdx >= 0)
                title = title[..nullIdx];
            if (!string.IsNullOrWhiteSpace(title))
                info.HeaderInfo["Title"] = title;
        }

        info.IsValid = true;
    }

    private static void ParseWiiHeader(RomInfo info, FileStream fs)
    {
        // Wii disc header: 0x00 = Game ID (6 bytes), 0x18 = Magic word (0x5D1C9EA3),
        // 0x20 = Title (0x60 bytes), region at Game ID byte 3
        long fileLen = fs.Length;
        if (fileLen < 0x100)
        {
            info.IsValid = true;
            return;
        }

        byte[] header = new byte[0x100];
        fs.Seek(0, SeekOrigin.Begin);
        int read = fs.Read(header, 0, header.Length);

        if (read >= 6)
        {
            string gameId = System.Text.Encoding.ASCII.GetString(header, 0x00, 6).TrimEnd('\0', ' ');
            if (!string.IsNullOrWhiteSpace(gameId))
                info.HeaderInfo["Game ID"] = gameId;

            // Region code is the 4th character of the Game ID
            if (gameId.Length >= 4)
            {
                char regionChar = gameId[3];
                string region = regionChar switch
                {
                    'E' => "USA",
                    'P' => "Europe",
                    'J' => "Japan",
                    'K' => "Korea",
                    'W' => "Taiwan",
                    _ => $"Unknown ({regionChar})"
                };
                info.HeaderInfo["Region"] = region;
            }
        }

        if (read >= 0x1C)
        {
            // Verify magic word at 0x18
            bool hasMagic = header[0x18] == 0x5D && header[0x19] == 0x1C
                         && header[0x1A] == 0x9E && header[0x1B] == 0xA3;
            info.HeaderInfo["Format"] = hasMagic ? "Wii Disc Image" : "Wii Disc Image (no magic)";
        }

        if (read >= 0x80)
        {
            string title = System.Text.Encoding.ASCII.GetString(header, 0x20, Math.Min(0x60, read - 0x20)).TrimEnd('\0', ' ');
            int nullIdx = title.IndexOf('\0');
            if (nullIdx >= 0)
                title = title[..nullIdx];
            if (!string.IsNullOrWhiteSpace(title))
                info.HeaderInfo["Title"] = title;
        }

        info.IsValid = true;
    }

    public static string GetSystemDisplayName(RomSystem system) => system switch
    {
        RomSystem.NES => "Nintendo Entertainment System",
        RomSystem.SNES => "Super Nintendo",
        RomSystem.N64 => "Nintendo 64",
        RomSystem.GameBoy => "Game Boy",
        RomSystem.GameBoyColor => "Game Boy Color",
        RomSystem.GameBoyAdvance => "Game Boy Advance",
        RomSystem.VirtualBoy => "Nintendo Virtual Boy",
        RomSystem.SegaMasterSystem => "Sega Master System",
        RomSystem.MegaDrive => "Sega Mega Drive / Genesis",
        RomSystem.SegaCD => "Sega CD",
        RomSystem.Sega32X => "Sega 32X",
        RomSystem.GameGear => "Sega Game Gear",
        RomSystem.Atari2600 => "Atari 2600",
        RomSystem.Atari5200 => "Atari 5200",
        RomSystem.Atari7800 => "Atari 7800",
        RomSystem.AtariJaguar => "Atari Jaguar",
        RomSystem.AtariLynx => "Atari Lynx",
        RomSystem.PCEngine => "PC Engine / TurboGrafx-16",
        RomSystem.NeoGeoPocket => "SNK Neo Geo Pocket / Pocket Color",
        RomSystem.ColecoVision => "Coleco ColecoVision",
        RomSystem.Intellivision => "Mattel Intellivision",
        RomSystem.MSX => "MSX",
        RomSystem.MSX2 => "MSX2",
        RomSystem.AmstradCPC => "Amstrad CPC",
        RomSystem.Oric => "Oric / Atmos / TeleStrat",
        RomSystem.ThomsonMO5 => "Thomson MO5",
        RomSystem.WataraSupervision => "Watara Supervision",
        RomSystem.ColorComputer => "Radio Shack Color Computer",
        RomSystem.Panasonic3DO => "Panasonic 3DO",
        RomSystem.AmigaCD32 => "Amiga CD32",
        RomSystem.SegaSaturn => "Sega Saturn",
        RomSystem.SegaDreamcast => "Sega Dreamcast",
        RomSystem.GameCube => "Nintendo GameCube",
        RomSystem.Wii => "Nintendo Wii",
        RomSystem.Arcade => "Arcade (MAME)",
        _ => "Unknown"
    };
}
