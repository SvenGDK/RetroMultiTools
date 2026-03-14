using RetroMultiTools.Detection;
using RetroMultiTools.Models;

namespace RetroMultiTools.Utilities;

/// <summary>
/// Analyzes ROM files for security features, copy protection, and region locking.
/// </summary>
public static class SecurityAnalyzer
{
    private const int BufferSize = 81920;

    private static readonly HashSet<string> RomExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".nes", ".smc", ".sfc", ".z64", ".n64", ".v64",
        ".gb", ".gbc", ".gba", ".vb", ".vboy",
        ".sms", ".md", ".gen",
        ".bin", ".32x", ".gg", ".a26", ".a52", ".a78",
        ".j64", ".jag", ".lnx", ".lyx",
        ".pce", ".tg16",
        ".ngp", ".ngc",
        ".col", ".cv", ".int",
        ".mx1", ".mx2",
        ".dsk", ".cdt", ".sna",
        ".tap",
        ".mo5", ".k7", ".fd",
        ".sv", ".ccc",
        ".iso", ".cue", ".3do",
        ".cdi", ".gdi",
        ".chd", ".rvz", ".gcm"
    };

    /// <summary>
    /// Analyzes a ROM file for security and DRM features.
    /// </summary>
    public static async Task<SecurityAnalysisResult> AnalyzeAsync(
        string romPath,
        IProgress<string>? progress = null)
    {
        if (!File.Exists(romPath))
            throw new FileNotFoundException("ROM file not found.", romPath);

        var result = new SecurityAnalysisResult
        {
            FilePath = romPath,
            FileName = Path.GetFileName(romPath)
        };

        progress?.Report($"Detecting ROM system for {result.FileName}...");

        var romInfo = await Task.Run(() => RomDetector.Detect(romPath)).ConfigureAwait(false);
        result.System = romInfo.SystemName;

        var features = new List<SecurityFeature>();

        progress?.Report("Checking region lock...");
        await Task.Run(() => CheckRegionLock(romPath, romInfo, features)).ConfigureAwait(false);

        progress?.Report("Checking copy protection...");
        await Task.Run(() => CheckCopyProtection(romPath, romInfo, features)).ConfigureAwait(false);

        progress?.Report("Checking checksum protection...");
        await Task.Run(() => CheckChecksumProtection(romInfo, features)).ConfigureAwait(false);

        result.Features = features;
        progress?.Report("Done.");
        return result;
    }

    /// <summary>
    /// Batch analyzes all ROM files in a directory.
    /// </summary>
    public static async Task<List<SecurityAnalysisResult>> AnalyzeBatchAsync(
        string directory,
        IProgress<string>? progress = null)
    {
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException($"Directory not found: {directory}");

        var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(f => RomExtensions.Contains(Path.GetExtension(f)))
            .ToList();

        var results = new List<SecurityAnalysisResult>();

        for (int i = 0; i < files.Count; i++)
        {
            progress?.Report($"Analyzing {i + 1} of {files.Count}: {Path.GetFileName(files[i])}");

            try
            {
                var result = await AnalyzeAsync(files[i], null).ConfigureAwait(false);
                results.Add(result);
            }
            catch (IOException ex)
            {
                results.Add(new SecurityAnalysisResult
                {
                    FilePath = files[i],
                    FileName = Path.GetFileName(files[i]),
                    Features =
                    [
                        new() { Name = "Analysis Failed", Description = ex.Message, Category = FeatureCategory.Other }
                    ]
                });
            }
        }

        progress?.Report($"Done — analyzed {results.Count} ROM(s).");
        return results;
    }

    private static void CheckRegionLock(string romPath, RomInfo romInfo, List<SecurityFeature> features)
    {
        switch (romInfo.System)
        {
            case RomSystem.NES:
                // NES has no explicit region byte in iNES but PAL vs NTSC can be inferred
                if (romInfo.HeaderInfo.TryGetValue("Format", out string? format) && format == "NES 2.0")
                {
                    features.Add(new SecurityFeature
                    {
                        Name = "NES 2.0 Timing",
                        Description = "NES 2.0 header supports region timing metadata (NTSC/PAL/Multi).",
                        Category = FeatureCategory.RegionLock
                    });
                }
                break;

            case RomSystem.SNES:
                CheckSnesRegion(romPath, romInfo, features);
                break;

            case RomSystem.N64:
                CheckN64Region(romPath, features);
                break;

            case RomSystem.GameBoy:
            case RomSystem.GameBoyColor:
                CheckGbRegion(romPath, features);
                break;

            case RomSystem.GameBoyAdvance:
                CheckGbaRegion(romPath, features);
                break;

            case RomSystem.MegaDrive:
            case RomSystem.Sega32X:
                CheckMegaDriveRegion(romPath, features);
                break;

            case RomSystem.SegaMasterSystem:
            case RomSystem.GameGear:
                CheckSmsGgRegion(romInfo, features);
                break;

            case RomSystem.PCEngine:
                features.Add(new SecurityFeature
                {
                    Name = "PC Engine Region",
                    Description = "PC Engine (Japan) and TurboGrafx-16 (USA) use different hardware pin configurations for region lockout. HuCard form factor differs between regions.",
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.NeoGeoPocket:
                CheckNeoGeoPocketRegion(romInfo, features);
                break;

            case RomSystem.VirtualBoy:
                features.Add(new SecurityFeature
                {
                    Name = "Virtual Boy Region",
                    Description = "Virtual Boy hardware is region-free; however, all commercial titles were released only in Japan and North America.",
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.Atari7800:
                features.Add(new SecurityFeature
                {
                    Name = "Atari 7800 Region",
                    Description = "Atari 7800 uses a digital signature in the cartridge header to enforce region lockout. PAL and NTSC consoles validate this signature before running the game.",
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.ColecoVision:
                features.Add(new SecurityFeature
                {
                    Name = "ColecoVision Region",
                    Description = "ColecoVision hardware is region-free. Cartridges from any region will work on any console.",
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.Intellivision:
                features.Add(new SecurityFeature
                {
                    Name = "Intellivision Region",
                    Description = "Intellivision hardware is region-free; the same cartridges are compatible with all console variants worldwide.",
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.Atari2600:
                features.Add(new SecurityFeature
                {
                    Name = "Atari 2600 Region",
                    Description = "Atari 2600 is region-free; games are NTSC or PAL specific based on programming, not hardware lockout.",
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.Atari5200:
                features.Add(new SecurityFeature
                {
                    Name = "Atari 5200 Region",
                    Description = "Atari 5200 is NTSC-only; no hardware region lock since the system was only released in North America.",
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.AtariJaguar:
                features.Add(new SecurityFeature
                {
                    Name = "Atari Jaguar Region",
                    Description = "Atari Jaguar is region-free; the Jaguar has no hardware or software region lockout mechanism.",
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.AtariLynx:
                features.Add(new SecurityFeature
                {
                    Name = "Atari Lynx Region",
                    Description = "Atari Lynx is region-free; the Lynx was designed as a worldwide product with no region restrictions.",
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.MSX:
            case RomSystem.MSX2:
                features.Add(new SecurityFeature
                {
                    Name = "MSX Region",
                    Description = "MSX standard is region-free by design; cartridges are compatible across regions. Software may have language differences.",
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.WataraSupervision:
                features.Add(new SecurityFeature
                {
                    Name = "Watara Supervision Region",
                    Description = "Watara Supervision is region-free; the handheld was sold worldwide with no regional restrictions.",
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.SegaCD:
                CheckSegaCdRegion(romPath, features);
                break;

            case RomSystem.AmstradCPC:
                features.Add(new SecurityFeature
                {
                    Name = "Amstrad CPC Region",
                    Description = "Amstrad CPC has no hardware region lock; regional differences are limited to keyboard layout and language.",
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.Oric:
                features.Add(new SecurityFeature
                {
                    Name = "Oric Region",
                    Description = "Oric has no region locking; the Oric was sold primarily in Europe with no regional restrictions.",
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.ThomsonMO5:
                features.Add(new SecurityFeature
                {
                    Name = "Thomson MO5 Region",
                    Description = "Thomson MO5 has no region locking; the Thomson MO5 was primarily a French market computer.",
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.ColorComputer:
                features.Add(new SecurityFeature
                {
                    Name = "TRS-80 Color Computer Region",
                    Description = "TRS-80 Color Computer has no region locking; the system was primarily sold in North America.",
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.Panasonic3DO:
                features.Add(new SecurityFeature
                {
                    Name = "3DO Region",
                    Description = "Panasonic 3DO is region-free by design; the 3DO specification does not include region lockout mechanisms.",
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.AmigaCD32:
                features.Add(new SecurityFeature
                {
                    Name = "Amiga CD32 Region",
                    Description = "Amiga CD32 uses PAL/NTSC region encoding; some titles may not work on consoles from a different video region.",
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.SegaSaturn:
                features.Add(new SecurityFeature
                {
                    Name = "Sega Saturn Region",
                    Description = "Sega Saturn enforces region lockout via a region code in the disc header. Games are typically locked to Japan, North America, or Europe.",
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.SegaDreamcast:
                features.Add(new SecurityFeature
                {
                    Name = "Sega Dreamcast Region",
                    Description = "Sega Dreamcast uses area symbol codes in the disc header for region locking. Games are coded for Japan, USA, and/or Europe regions.",
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.GameCube:
                CheckGameCubeRegion(romInfo, features);
                break;

            case RomSystem.Wii:
                CheckWiiRegion(romInfo, features);
                break;

            case RomSystem.Arcade:
                features.Add(new SecurityFeature
                {
                    Name = "Arcade Region",
                    Description = "Arcade boards typically have no hardware region lock; regional differences are implemented in software via DIP switch settings or separate ROM sets.",
                    Category = FeatureCategory.RegionLock
                });
                break;
        }
    }

    private static void CheckCopyProtection(string romPath, RomInfo romInfo, List<SecurityFeature> features)
    {
        switch (romInfo.System)
        {
            case RomSystem.NES:
                // NES 10NES lockout chip
                features.Add(new SecurityFeature
                {
                    Name = "10NES Lockout",
                    Description = "NES cartridges use the 10NES lockout chip for hardware-level copy protection.",
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.SNES:
                // SNES CIC lockout chip
                features.Add(new SecurityFeature
                {
                    Name = "CIC Lockout Chip",
                    Description = "SNES cartridges use a CIC (Checking Integrated Circuit) lockout chip for region and copy protection.",
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.N64:
                CheckN64CIC(romPath, features);
                break;

            case RomSystem.GameBoy:
            case RomSystem.GameBoyColor:
                CheckGbNintendoLogo(romPath, features);
                break;

            case RomSystem.GameBoyAdvance:
                CheckGbaNintendoLogo(romPath, features);
                break;

            case RomSystem.MegaDrive:
            case RomSystem.Sega32X:
                CheckMegaDriveTmss(romPath, features);
                break;

            case RomSystem.SegaMasterSystem:
            case RomSystem.GameGear:
                features.Add(new SecurityFeature
                {
                    Name = "SMS/Game Gear BIOS Check",
                    Description = "Master System and Game Gear consoles with BIOS check for the \"TMR SEGA\" header marker to validate cartridges.",
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.PCEngine:
                features.Add(new SecurityFeature
                {
                    Name = "HuCard Form Factor",
                    Description = "PC Engine uses the proprietary HuCard format as a physical copy protection mechanism.",
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.AtariLynx:
                features.Add(new SecurityFeature
                {
                    Name = "Lynx Cartridge Encryption",
                    Description = "Atari Lynx uses an encrypted boot header that the system ROM decrypts before execution.",
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.VirtualBoy:
                features.Add(new SecurityFeature
                {
                    Name = "Virtual Boy Nintendo Logo",
                    Description = "Virtual Boy validates the Nintendo logo data in the cartridge header at boot, similar to the Game Boy logo check.",
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.Atari7800:
                features.Add(new SecurityFeature
                {
                    Name = "Atari 7800 Digital Signature",
                    Description = "Atari 7800 cartridges contain a digital signature that the BIOS verifies before running the game. Without a valid signature, the console falls back to Atari 2600 mode.",
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.ColecoVision:
                features.Add(new SecurityFeature
                {
                    Name = "ColecoVision BIOS Boot Check",
                    Description = "ColecoVision BIOS checks for specific magic bytes (0xAA 0x55 or 0x55 0xAA) at the start of the cartridge ROM to validate the cartridge.",
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.Intellivision:
                features.Add(new SecurityFeature
                {
                    Name = "EXEC ROM Handshake",
                    Description = "Intellivision uses the EXEC ROM firmware to perform a handshake with the cartridge. The EXEC validates the cartridge entry point before transferring control.",
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.Atari2600:
                features.Add(new SecurityFeature
                {
                    Name = "Atari 2600 No Copy Protection",
                    Description = "No software-level copy protection. The simple ROM cartridge format has no validation mechanism.",
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.Atari5200:
                features.Add(new SecurityFeature
                {
                    Name = "Atari 5200 No Copy Protection",
                    Description = "No software-level copy protection mechanism in cartridges.",
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.AtariJaguar:
                features.Add(new SecurityFeature
                {
                    Name = "Jaguar Encrypted Boot",
                    Description = "Cartridge header contains an encrypted boot sequence validated by the Jaguar BIOS.",
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.MSX:
            case RomSystem.MSX2:
                CheckMsxCartridgeMarker(romPath, features);
                break;

            case RomSystem.WataraSupervision:
                features.Add(new SecurityFeature
                {
                    Name = "Watara Supervision No Copy Protection",
                    Description = "No software-level copy protection.",
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.SegaCD:
                features.Add(new SecurityFeature
                {
                    Name = "Sega CD Disc Protection",
                    Description = "Region-coded disc format with security ring on physical media.",
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.AmstradCPC:
                features.Add(new SecurityFeature
                {
                    Name = "Amstrad CPC No ROM Copy Protection",
                    Description = "No cartridge-level copy protection in ROM format; original media used various disk protection schemes.",
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.Oric:
                features.Add(new SecurityFeature
                {
                    Name = "Oric No ROM Copy Protection",
                    Description = "No ROM-level copy protection; original cassette tapes used custom loading routines.",
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.ThomsonMO5:
                features.Add(new SecurityFeature
                {
                    Name = "Thomson MO5 No ROM Copy Protection",
                    Description = "No ROM-level copy protection; original media used format-based copy protection.",
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.ColorComputer:
                features.Add(new SecurityFeature
                {
                    Name = "Color Computer No ROM Copy Protection",
                    Description = "No ROM-level copy protection in cartridge format.",
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.Panasonic3DO:
                features.Add(new SecurityFeature
                {
                    Name = "3DO Disc Encryption",
                    Description = "3DO uses encrypted disc headers; the console verifies disc authenticity at boot time.",
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.AmigaCD32:
                features.Add(new SecurityFeature
                {
                    Name = "Amiga CD32 AKIKO Chip",
                    Description = "Amiga CD32 uses the AKIKO chip for CD drive access and copy protection verification.",
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.SegaSaturn:
                features.Add(new SecurityFeature
                {
                    Name = "Sega Saturn Disc Protection",
                    Description = "Sega Saturn uses a security ring on the outer edge of the disc for copy protection.",
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.SegaDreamcast:
                features.Add(new SecurityFeature
                {
                    Name = "Sega Dreamcast GD-ROM Protection",
                    Description = "Sega Dreamcast uses the proprietary GD-ROM disc format which stores data in a high-density area inaccessible to standard CD drives, providing physical copy protection.",
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.GameCube:
                features.Add(new SecurityFeature
                {
                    Name = "GameCube Disc Authentication",
                    Description = "GameCube uses a proprietary mini-DVD format with Burst Cutting Area (BCA) data for disc authentication. The drive firmware validates disc authenticity before allowing reads.",
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.Wii:
                features.Add(new SecurityFeature
                {
                    Name = "Wii Disc Encryption",
                    Description = "Wii discs use AES-128-CBC encryption with per-title keys stored in an encrypted title key block. The common key is stored in the console's OTP memory. Additionally, disc authenticity is verified via a signature chain (CA → CP → Ticket).",
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.Arcade:
                features.Add(new SecurityFeature
                {
                    Name = "Arcade Board Protection",
                    Description = "Arcade boards may use various copy protection mechanisms including encrypted ROMs, custom coprocessors, battery-backed RAM, or hardware security modules depending on the manufacturer and era.",
                    Category = FeatureCategory.CopyProtection
                });
                break;
        }
    }

    private static void CheckChecksumProtection(RomInfo romInfo, List<SecurityFeature> features)
    {
        if (romInfo.System == RomSystem.SNES)
        {
            if (romInfo.HeaderInfo.TryGetValue("Checksum Valid", out string? valid))
            {
                features.Add(new SecurityFeature
                {
                    Name = "SNES Internal Checksum",
                    Description = valid == "Yes"
                        ? "Internal checksum is valid — ROM integrity verified."
                        : "Internal checksum mismatch — ROM may be modified or corrupted.",
                    Category = FeatureCategory.ChecksumProtection
                });
            }
        }

        if (romInfo.System == RomSystem.N64)
        {
            if (romInfo.HeaderInfo.TryGetValue("CRC1", out string? value1) && romInfo.HeaderInfo.TryGetValue("CRC2", out string? value))
            {
                features.Add(new SecurityFeature
                {
                    Name = "N64 CRC Checksums",
                    Description = $"Header CRC1: {value1}, CRC2: {value}. N64 boot code validates these checksums.",
                    Category = FeatureCategory.ChecksumProtection
                });
            }
        }

        if (romInfo.System == RomSystem.NES)
        {
            features.Add(new SecurityFeature
            {
                Name = "iNES Header Integrity",
                Description = romInfo.IsValid
                    ? "iNES header magic bytes validated (NES\\x1A)."
                    : "iNES header validation failed — possible corruption.",
                Category = FeatureCategory.ChecksumProtection
            });
        }

        if (romInfo.System == RomSystem.GameBoy || romInfo.System == RomSystem.GameBoyColor)
        {
            CheckGbHeaderChecksum(romInfo, features);
        }

        if (romInfo.System == RomSystem.GameBoyAdvance)
        {
            if (romInfo.HeaderInfo.TryGetValue("Header Checksum", out string? gbaChecksum))
            {
                features.Add(new SecurityFeature
                {
                    Name = "GBA Header Checksum",
                    Description = $"Header checksum: {gbaChecksum}. The GBA BIOS validates the header checksum before booting.",
                    Category = FeatureCategory.ChecksumProtection
                });
            }
        }

        if (romInfo.System == RomSystem.MegaDrive)
        {
            if (romInfo.HeaderInfo.TryGetValue("Checksum", out string? mdChecksum))
            {
                features.Add(new SecurityFeature
                {
                    Name = "Mega Drive Internal Checksum",
                    Description = $"Internal checksum: {mdChecksum}. Some Mega Drive games validate this checksum at boot.",
                    Category = FeatureCategory.ChecksumProtection
                });
            }
        }
    }

    private static void CheckSnesRegion(string romPath, RomInfo romInfo, List<SecurityFeature> features)
    {
        try
        {
            using var fs = new FileStream(romPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
            long fileLen = fs.Length;
            int copierOffset = (fileLen % 1024 == 512) ? 512 : 0;

            // Try HiROM (0xFFD9) and LoROM (0x7FD9) region byte offsets
            foreach (int baseOffset in new[] { 0xFFD9, 0x7FD9 })
            {
                int offset = copierOffset + baseOffset;
                if (offset < fileLen)
                {
                    fs.Seek(offset, SeekOrigin.Begin);
                    int regionByte = fs.ReadByte();
                    if (regionByte >= 0)
                    {
                        string region = regionByte switch
                        {
                            0x00 => "Japan",
                            0x01 => "USA/Canada",
                            0x02 => "Europe/Oceania/Asia",
                            0x03 => "Sweden/Scandinavia",
                            0x04 => "Finland",
                            0x05 => "Denmark",
                            0x06 => "France",
                            0x07 => "Netherlands",
                            0x08 => "Spain",
                            0x09 => "Germany/Austria/Switzerland",
                            0x0A => "Italy",
                            0x0B => "China/Hong Kong",
                            0x0C => "Indonesia",
                            0x0D => "South Korea",
                            _ => $"Unknown (0x{regionByte:X2})"
                        };

                        features.Add(new SecurityFeature
                        {
                            Name = "SNES Region Lock",
                            Description = $"Region code: 0x{regionByte:X2} ({region}). SNES CIC chip enforces region-based lockout.",
                            Category = FeatureCategory.RegionLock
                        });
                        return;
                    }
                }
            }
        }
        catch (IOException) { }
    }

    private static void CheckN64Region(string romPath, List<SecurityFeature> features)
    {
        try
        {
            using var fs = new FileStream(romPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
            if (fs.Length < 0x40)
                return;

            // Read the first 0x40 bytes and normalize to Big Endian
            byte[] header = new byte[0x40];
            if (fs.Read(header, 0, header.Length) < header.Length) return;

            var format = N64FormatConverter.DetectFormat(header);
            if (format == null) return;

            N64FormatConverter.NormalizeToBigEndian(header, header.Length, format.Value);

            // Region byte at 0x3E in Big Endian format
            int regionByte = header[0x3E];

            string region = (char)regionByte switch
            {
                'E' => "USA",
                'J' => "Japan",
                'P' => "Europe",
                'A' => "Asia",
                'D' => "Germany",
                'F' => "France",
                'I' => "Italy",
                'S' => "Spain",
                _ => $"Unknown ({(char)regionByte})"
            };

            features.Add(new SecurityFeature
            {
                Name = "N64 Region Code",
                Description = $"Region: {region} (0x{regionByte:X2}). N64 uses CIC chip for region lockout.",
                Category = FeatureCategory.RegionLock
            });
        }
        catch (IOException) { }
    }

    private static void CheckN64CIC(string romPath, List<SecurityFeature> features)
    {
        try
        {
            using var fs = new FileStream(romPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
            if (fs.Length < 0x1000)
                return;

            // Detect byte order from first 4 bytes
            byte[] magic = new byte[4];
            if (fs.Read(magic, 0, 4) < 4) return;

            var format = N64FormatConverter.DetectFormat(magic);
            if (format == null) return;

            // CIC boot code is at offset 0x40-0x1000 in the ROM
            fs.Seek(0x40, SeekOrigin.Begin);
            byte[] bootCode = new byte[0xFC0];
            int read = fs.Read(bootCode, 0, bootCode.Length);
            if (read < bootCode.Length) return;

            // Normalize boot code to Big Endian for consistent hashing
            N64FormatConverter.NormalizeToBigEndian(bootCode, read, format.Value);

            // Calculate a simple hash of the boot code to identify CIC type
            uint hash = 0;
            foreach (byte b in bootCode)
                hash = ((hash << 5) + hash) + b;

            features.Add(new SecurityFeature
            {
                Name = "N64 CIC Boot Code",
                Description = $"Boot code hash: 0x{hash:X8}. N64 CIC chip validates boot code for anti-piracy and region locking.",
                Category = FeatureCategory.CopyProtection
            });
        }
        catch (IOException) { }
    }

    private static void CheckGbRegion(string romPath, List<SecurityFeature> features)
    {
        try
        {
            using var fs = new FileStream(romPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
            if (fs.Length < 0x14C)
                return;

            // Destination code at 0x14A (0 = Japanese, 1 = Non-Japanese)
            fs.Seek(0x14A, SeekOrigin.Begin);
            int dest = fs.ReadByte();
            if (dest < 0) return;

            string region = dest == 0x00 ? "Japanese" : "Non-Japanese (International)";
            features.Add(new SecurityFeature
            {
                Name = "Game Boy Region",
                Description = $"Destination code: 0x{dest:X2} ({region}).",
                Category = FeatureCategory.RegionLock
            });
        }
        catch (IOException) { }
    }

    private static void CheckGbaRegion(string romPath, List<SecurityFeature> features)
    {
        try
        {
            using var fs = new FileStream(romPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
            if (fs.Length < 0xB0)
                return;

            // Game code at 0xAC-0xAF, last character indicates region
            fs.Seek(0xAC, SeekOrigin.Begin);
            byte[] code = new byte[4];
            if (fs.Read(code, 0, 4) < 4) return;

            char regionChar = (char)code[3];
            string region = regionChar switch
            {
                'E' => "USA",
                'J' => "Japan",
                'P' => "Europe",
                'D' => "Germany",
                'F' => "France",
                'I' => "Italy",
                'S' => "Spain",
                _ => $"Unknown ({regionChar})"
            };

            features.Add(new SecurityFeature
            {
                Name = "GBA Region Code",
                Description = $"Game code region: {regionChar} ({region}).",
                Category = FeatureCategory.RegionLock
            });
        }
        catch (IOException) { }
    }

    private static void CheckMegaDriveRegion(string romPath, List<SecurityFeature> features)
    {
        try
        {
            using var fs = new FileStream(romPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
            if (fs.Length < 0x1F4)
                return;

            // Region code at 0x1F0 (up to 3 characters like "JUE")
            fs.Seek(0x1F0, SeekOrigin.Begin);
            byte[] regionBytes = new byte[4];
            if (fs.Read(regionBytes, 0, 4) < 4) return;

            string regionCode = System.Text.Encoding.ASCII.GetString(regionBytes).TrimEnd('\0', ' ');
            string description = BuildRegionDescription(regionCode);

            features.Add(new SecurityFeature
            {
                Name = "Mega Drive Region",
                Description = $"Region code: \"{regionCode}\". {description.Trim()}. Mega Drive uses hardware region lockout.",
                Category = FeatureCategory.RegionLock
            });
        }
        catch (IOException) { }
    }

    private static void CheckGbNintendoLogo(string romPath, List<SecurityFeature> features)
    {
        try
        {
            using var fs = new FileStream(romPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
            if (fs.Length < 0x108)
                return;

            fs.Seek(0x104, SeekOrigin.Begin);
            byte[] logo = new byte[4];
            if (fs.Read(logo, 0, 4) < 4) return;

            bool valid = logo[0] == 0xCE && logo[1] == 0xED && logo[2] == 0x66 && logo[3] == 0x66;

            features.Add(new SecurityFeature
            {
                Name = "Nintendo Logo Check",
                Description = valid
                    ? "Nintendo logo bytes at 0x104 are valid. Game Boy boot ROM verifies this as a form of trademark protection."
                    : "Nintendo logo bytes at 0x104 are invalid. The Game Boy would refuse to boot this ROM.",
                Category = FeatureCategory.CopyProtection
            });
        }
        catch (IOException) { }
    }

    private static void CheckGbaNintendoLogo(string romPath, List<SecurityFeature> features)
    {
        try
        {
            using var fs = new FileStream(romPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
            if (fs.Length < 0x08)
                return;

            fs.Seek(0x04, SeekOrigin.Begin);
            byte[] logo = new byte[4];
            if (fs.Read(logo, 0, 4) < 4) return;

            bool valid = logo[0] == 0x24 && logo[1] == 0xFF && logo[2] == 0xAE && logo[3] == 0x51;

            features.Add(new SecurityFeature
            {
                Name = "GBA Logo Check",
                Description = valid
                    ? "GBA logo bytes at 0x04 are valid. The GBA BIOS verifies these as trademark protection."
                    : "GBA logo bytes at 0x04 are invalid. The GBA BIOS would refuse to boot this ROM.",
                Category = FeatureCategory.CopyProtection
            });
        }
        catch (IOException) { }
    }

    private static void CheckMegaDriveTmss(string romPath, List<SecurityFeature> features)
    {
        try
        {
            using var fs = new FileStream(romPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
            if (fs.Length < 0x110)
                return;

            // Check for "SEGA" marker at 0x100 (TMSS - Trademark Security System)
            fs.Seek(0x100, SeekOrigin.Begin);
            byte[] marker = new byte[4];
            if (fs.Read(marker, 0, 4) < 4) return;

            string markerStr = System.Text.Encoding.ASCII.GetString(marker);
            bool hasTmss = markerStr.StartsWith("SEGA", StringComparison.Ordinal);

            features.Add(new SecurityFeature
            {
                Name = "TMSS (Trademark Security System)",
                Description = hasTmss
                    ? "\"SEGA\" marker found at 0x100. Model 2+ Genesis/Mega Drive consoles check this for boot authorization."
                    : "\"SEGA\" marker not found at 0x100. This ROM may not boot on Model 2+ Genesis/Mega Drive hardware.",
                Category = FeatureCategory.CopyProtection
            });
        }
        catch (IOException) { }
    }

    private static void CheckSmsGgRegion(RomInfo romInfo, List<SecurityFeature> features)
    {
        if (romInfo.HeaderInfo.TryGetValue("Region", out string? region))
        {
            features.Add(new SecurityFeature
            {
                Name = "SMS/Game Gear Region",
                Description = $"Header region: {region}. Region is encoded in the TMR SEGA header and enforced by BIOS on consoles that have one.",
                Category = FeatureCategory.RegionLock
            });
        }
        else
        {
            features.Add(new SecurityFeature
            {
                Name = "SMS/Game Gear Region",
                Description = "No TMR SEGA header found — region cannot be determined from the ROM header. Early cartridges lack this marker.",
                Category = FeatureCategory.RegionLock
            });
        }
    }

    private static void CheckNeoGeoPocketRegion(RomInfo romInfo, List<SecurityFeature> features)
    {
        features.Add(new SecurityFeature
        {
            Name = "Neo Geo Pocket Region",
            Description = "Neo Geo Pocket and Pocket Color hardware is region-free. Games from any region can be played on any console.",
            Category = FeatureCategory.RegionLock
        });
    }

    private static void CheckGbHeaderChecksum(RomInfo romInfo, List<SecurityFeature> features)
    {
        try
        {
            using var fs = new FileStream(romInfo.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
            if (fs.Length < 0x14E)
                return;

            // Read bytes 0x134-0x14D for header checksum calculation
            byte[] header = new byte[0x14E];
            if (fs.Read(header, 0, header.Length) < header.Length) return;

            // Header checksum at 0x14D: sum of bytes 0x134-0x14C, complemented
            byte computed = 0;
            for (int i = 0x134; i <= 0x14C; i++)
                computed = (byte)(computed - header[i] - 1);

            byte stored = header[0x14D];
            bool valid = computed == stored;

            features.Add(new SecurityFeature
            {
                Name = "Game Boy Header Checksum",
                Description = valid
                    ? $"Header checksum at 0x14D is valid (0x{stored:X2}). The Game Boy boot ROM verifies this checksum before booting."
                    : $"Header checksum at 0x14D is invalid (stored: 0x{stored:X2}, expected: 0x{computed:X2}). The Game Boy would refuse to boot this ROM.",
                Category = FeatureCategory.ChecksumProtection
            });
        }
        catch (IOException) { }
    }

    private static void CheckSegaCdRegion(string romPath, List<SecurityFeature> features)
    {
        try
        {
            using var fs = new FileStream(romPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
            if (fs.Length < 0x204)
                return;

            // Region code at offset 0x200 in ISO format (up to 3 characters like "JUE")
            fs.Seek(0x200, SeekOrigin.Begin);
            byte[] regionBytes = new byte[4];
            if (fs.Read(regionBytes, 0, 4) < 4) return;

            string regionCode = System.Text.Encoding.ASCII.GetString(regionBytes).TrimEnd('\0', ' ');
            string description = BuildRegionDescription(regionCode);

            features.Add(new SecurityFeature
            {
                Name = "Sega CD Region",
                Description = $"Region code: \"{regionCode}\". {description.Trim()}. Sega CD uses region coding in the disc header similar to Mega Drive.",
                Category = FeatureCategory.RegionLock
            });
        }
        catch (IOException) { }
    }

    private static void CheckMsxCartridgeMarker(string romPath, List<SecurityFeature> features)
    {
        try
        {
            using var fs = new FileStream(romPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
            if (fs.Length < 2)
                return;

            byte[] marker = new byte[2];
            if (fs.Read(marker, 0, 2) < 2) return;

            // MSX cartridges use "AB" magic bytes (0x41, 0x42) as identification
            bool hasAbMarker = marker[0] == 0x41 && marker[1] == 0x42;

            features.Add(new SecurityFeature
            {
                Name = "MSX Cartridge Marker",
                Description = hasAbMarker
                    ? "\"AB\" magic bytes (0x41, 0x42) found at start. MSX BIOS validates this cartridge identification marker."
                    : "\"AB\" magic bytes not found at start. This ROM may not be recognized by the MSX BIOS.",
                Category = FeatureCategory.CopyProtection
            });
        }
        catch (IOException) { }
    }

    private static void CheckGameCubeRegion(RomInfo romInfo, List<SecurityFeature> features)
    {
        if (romInfo.HeaderInfo.TryGetValue("Region", out string? region) && !string.IsNullOrWhiteSpace(region))
        {
            features.Add(new SecurityFeature
            {
                Name = "GameCube Region Lock",
                Description = $"Region: {region}. GameCube enforces region lockout via the Game ID in the disc header. The console checks the region code and refuses to boot discs from a different region.",
                Category = FeatureCategory.RegionLock
            });
        }
        else
        {
            features.Add(new SecurityFeature
            {
                Name = "GameCube Region Lock",
                Description = "GameCube enforces hardware-level region lockout. The console verifies the disc region code at boot time.",
                Category = FeatureCategory.RegionLock
            });
        }
    }

    private static void CheckWiiRegion(RomInfo romInfo, List<SecurityFeature> features)
    {
        if (romInfo.HeaderInfo.TryGetValue("Region", out string? region) && !string.IsNullOrWhiteSpace(region))
        {
            features.Add(new SecurityFeature
            {
                Name = "Wii Region Lock",
                Description = $"Region: {region}. Wii enforces region lockout at both hardware and software levels. The System Menu checks the disc region code against the console's region setting.",
                Category = FeatureCategory.RegionLock
            });
        }
        else
        {
            features.Add(new SecurityFeature
            {
                Name = "Wii Region Lock",
                Description = "Wii enforces region lockout at both hardware and software levels. The System Menu validates the disc region before launching.",
                Category = FeatureCategory.RegionLock
            });
        }
    }

    private static string BuildRegionDescription(string regionCode)
    {
        var regions = new List<string>();
        if (regionCode.Contains('J')) regions.Add("Japan");
        if (regionCode.Contains('U')) regions.Add("USA");
        if (regionCode.Contains('E')) regions.Add("Europe");
        return regions.Count > 0
            ? "Regions: " + string.Join(", ", regions)
            : $"Region code: {regionCode}";
    }
}

public enum FeatureCategory
{
    RegionLock,
    CopyProtection,
    ChecksumProtection,
    Other
}

public class SecurityFeature
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public FeatureCategory Category { get; set; }

    public string CategoryName => Category switch
    {
        FeatureCategory.RegionLock => "Region Lock",
        FeatureCategory.CopyProtection => "Copy Protection",
        FeatureCategory.ChecksumProtection => "Checksum / Integrity",
        FeatureCategory.Other => "Other",
        _ => "Unknown"
    };
}

public class SecurityAnalysisResult
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string System { get; set; } = string.Empty;
    public List<SecurityFeature> Features { get; set; } = [];

    public string Summary =>
        $"{FileName} [{System}]: {Features.Count} security feature(s) detected";
}
