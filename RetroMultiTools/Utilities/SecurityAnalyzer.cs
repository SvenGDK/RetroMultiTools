using RetroMultiTools.Detection;
using RetroMultiTools.Localization;
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
        ".chd", ".rvz", ".gcm",
        ".atr", ".xex", ".car", ".cas",
        ".d88", ".t88",
        ".ndd",
        ".nds",
        ".3ds", ".cia",
        ".neo",
        ".chf",
        ".tgc",
        ".mtx", ".run"
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
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                results.Add(new SecurityAnalysisResult
                {
                    FilePath = files[i],
                    FileName = Path.GetFileName(files[i]),
                    Features =
                    [
                        new() { Name = LocalizationManager.Instance["SecAnalyzer_AnalysisFailed"], Description = ex.Message, Category = FeatureCategory.Other }
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
            case RomSystem.AmigaCD32:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_AmigaCd32Region"],
                    Description = LocalizationManager.Instance["SecAnalyzer_AmigaCd32RegionDesc"],
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.AmstradCPC:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_AmstradCpcRegion"],
                    Description = LocalizationManager.Instance["SecAnalyzer_AmstradCpcRegionDesc"],
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.Arcade:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_ArcadeRegion"],
                    Description = LocalizationManager.Instance["SecAnalyzer_ArcadeRegionDesc"],
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.Atari2600:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_Atari2600Region"],
                    Description = LocalizationManager.Instance["SecAnalyzer_Atari2600RegionDesc"],
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.Atari5200:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_Atari5200Region"],
                    Description = LocalizationManager.Instance["SecAnalyzer_Atari5200RegionDesc"],
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.Atari7800:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_Atari7800Region"],
                    Description = LocalizationManager.Instance["SecAnalyzer_Atari7800RegionDesc"],
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.Atari800:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_Atari800Region"],
                    Description = LocalizationManager.Instance["SecAnalyzer_Atari800RegionDesc"],
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.AtariJaguar:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_AtariJaguarRegion"],
                    Description = LocalizationManager.Instance["SecAnalyzer_AtariJaguarRegionDesc"],
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.AtariLynx:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_AtariLynxRegion"],
                    Description = LocalizationManager.Instance["SecAnalyzer_AtariLynxRegionDesc"],
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.ColecoVision:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_ColecoVisionRegion"],
                    Description = LocalizationManager.Instance["SecAnalyzer_ColecoVisionRegionDesc"],
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.ColorComputer:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_Trs80Region"],
                    Description = LocalizationManager.Instance["SecAnalyzer_Trs80RegionDesc"],
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.GameBoy:
            case RomSystem.GameBoyColor:
                CheckGbRegion(romPath, features);
                break;

            case RomSystem.GameBoyAdvance:
                CheckGbaRegion(romPath, features);
                break;

            case RomSystem.GameCube:
                CheckGameCubeRegion(romInfo, features);
                break;

            case RomSystem.Intellivision:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_IntellivisionRegion"],
                    Description = LocalizationManager.Instance["SecAnalyzer_IntellivisionRegionDesc"],
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.MegaDrive:
            case RomSystem.Sega32X:
                CheckMegaDriveRegion(romPath, features);
                break;

            case RomSystem.MSX:
            case RomSystem.MSX2:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_MsxRegion"],
                    Description = LocalizationManager.Instance["SecAnalyzer_MsxRegionDesc"],
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.N64:
                CheckN64Region(romPath, features);
                break;

            case RomSystem.N64DD:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_N64ddRegion"],
                    Description = LocalizationManager.Instance["SecAnalyzer_N64ddRegionDesc"],
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.NECPC88:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_NecPc88Region"],
                    Description = LocalizationManager.Instance["SecAnalyzer_NecPc88RegionDesc"],
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.NeoGeo:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_NeoGeoRegion"],
                    Description = LocalizationManager.Instance["SecAnalyzer_NeoGeoRegionDesc"],
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.NeoGeoCD:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_NeoGeoCdRegion"],
                    Description = LocalizationManager.Instance["SecAnalyzer_NeoGeoCdRegionDesc"],
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.NeoGeoPocket:
                CheckNeoGeoPocketRegion(romInfo, features);
                break;

            case RomSystem.NES:
                // NES has no explicit region byte in iNES but PAL vs NTSC can be inferred
                if (romInfo.HeaderInfo.TryGetValue("Format", out string? format) && format == "NES 2.0")
                {
                    features.Add(new SecurityFeature
                    {
                        Name = LocalizationManager.Instance["SecAnalyzer_Nes2Timing"],
                        Description = LocalizationManager.Instance["SecAnalyzer_Nes2TimingDesc"],
                        Category = FeatureCategory.RegionLock
                    });
                }
                break;

            case RomSystem.Nintendo3DS:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_3dsRegion"],
                    Description = LocalizationManager.Instance["SecAnalyzer_3dsRegionDesc"],
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.NintendoDS:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_NdsRegion"],
                    Description = LocalizationManager.Instance["SecAnalyzer_NdsRegionDesc"],
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.Oric:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_OricRegion"],
                    Description = LocalizationManager.Instance["SecAnalyzer_OricRegionDesc"],
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.Panasonic3DO:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_3doRegion"],
                    Description = LocalizationManager.Instance["SecAnalyzer_3doRegionDesc"],
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.PCEngine:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_PceRegion"],
                    Description = LocalizationManager.Instance["SecAnalyzer_PceRegionDesc"],
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.PhilipsCDi:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_CdiRegion"],
                    Description = LocalizationManager.Instance["SecAnalyzer_CdiRegionDesc"],
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.SegaCD:
                CheckSegaCdRegion(romPath, features);
                break;

            case RomSystem.SegaDreamcast:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_DreamcastRegion"],
                    Description = LocalizationManager.Instance["SecAnalyzer_DreamcastRegionDesc"],
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.SegaMasterSystem:
            case RomSystem.GameGear:
                CheckSmsGgRegion(romInfo, features);
                break;

            case RomSystem.SegaSaturn:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_SaturnRegion"],
                    Description = LocalizationManager.Instance["SecAnalyzer_SaturnRegionDesc"],
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.SNES:
                CheckSnesRegion(romPath, romInfo, features);
                break;

            case RomSystem.ThomsonMO5:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_ThomsonRegion"],
                    Description = LocalizationManager.Instance["SecAnalyzer_ThomsonRegionDesc"],
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.VirtualBoy:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_VirtualBoyRegion"],
                    Description = LocalizationManager.Instance["SecAnalyzer_VirtualBoyRegionDesc"],
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.WataraSupervision:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_WataraRegion"],
                    Description = LocalizationManager.Instance["SecAnalyzer_WataraRegionDesc"],
                    Category = FeatureCategory.RegionLock
                });
                break;

            case RomSystem.Wii:
                CheckWiiRegion(romInfo, features);
                break;
        }
    }

    private static void CheckCopyProtection(string romPath, RomInfo romInfo, List<SecurityFeature> features)
    {
        switch (romInfo.System)
        {
            case RomSystem.AmigaCD32:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_AmigaCd32Akiko"],
                    Description = LocalizationManager.Instance["SecAnalyzer_AmigaCd32AkikoDesc"],
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.AmstradCPC:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_AmstradCpcNoCopy"],
                    Description = LocalizationManager.Instance["SecAnalyzer_AmstradCpcNoCopyDesc"],
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.Arcade:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_ArcadeBoardProt"],
                    Description = LocalizationManager.Instance["SecAnalyzer_ArcadeBoardProtDesc"],
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.Atari2600:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_Atari2600NoCopy"],
                    Description = LocalizationManager.Instance["SecAnalyzer_Atari2600NoCopyDesc"],
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.Atari5200:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_Atari5200NoCopy"],
                    Description = LocalizationManager.Instance["SecAnalyzer_Atari5200NoCopyDesc"],
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.Atari7800:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_Atari7800DigSig"],
                    Description = LocalizationManager.Instance["SecAnalyzer_Atari7800DigSigDesc"],
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.Atari800:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_Atari800NoCopy"],
                    Description = LocalizationManager.Instance["SecAnalyzer_Atari800NoCopyDesc"],
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.AtariJaguar:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_JaguarEncBoot"],
                    Description = LocalizationManager.Instance["SecAnalyzer_JaguarEncBootDesc"],
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.AtariLynx:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_LynxCartEncrypt"],
                    Description = LocalizationManager.Instance["SecAnalyzer_LynxCartEncryptDesc"],
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.ColecoVision:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_ColecoVisionBios"],
                    Description = LocalizationManager.Instance["SecAnalyzer_ColecoVisionBiosDesc"],
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.ColorComputer:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_ColorCompNoCopy"],
                    Description = LocalizationManager.Instance["SecAnalyzer_ColorCompNoCopyDesc"],
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.FairchildChannelF:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_FairchildNoCopy"],
                    Description = LocalizationManager.Instance["SecAnalyzer_FairchildNoCopyDesc"],
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.GameBoy:
            case RomSystem.GameBoyColor:
                CheckGbNintendoLogo(romPath, features);
                break;

            case RomSystem.GameBoyAdvance:
                CheckGbaNintendoLogo(romPath, features);
                break;

            case RomSystem.GameCube:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_GameCubeAuth"],
                    Description = LocalizationManager.Instance["SecAnalyzer_GameCubeAuthDesc"],
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.Intellivision:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_ExecRomHandshake"],
                    Description = LocalizationManager.Instance["SecAnalyzer_ExecRomHandshakeDesc"],
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.MegaDrive:
            case RomSystem.Sega32X:
                CheckMegaDriveTmss(romPath, features);
                break;

            case RomSystem.MemotechMTX:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_MemotechNoCopy"],
                    Description = LocalizationManager.Instance["SecAnalyzer_MemotechNoCopyDesc"],
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.MSX:
            case RomSystem.MSX2:
                CheckMsxCartridgeMarker(romPath, features);
                break;

            case RomSystem.N64:
                CheckN64CIC(romPath, features);
                break;

            case RomSystem.N64DD:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_N64ddCopyProt"],
                    Description = LocalizationManager.Instance["SecAnalyzer_N64ddCopyProtDesc"],
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.NECPC88:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_NecPc88NoCopy"],
                    Description = LocalizationManager.Instance["SecAnalyzer_NecPc88NoCopyDesc"],
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.NeoGeo:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_NeoGeoProt"],
                    Description = LocalizationManager.Instance["SecAnalyzer_NeoGeoProtDesc"],
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.NeoGeoCD:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_NeoGeoCdProt"],
                    Description = LocalizationManager.Instance["SecAnalyzer_NeoGeoCdProtDesc"],
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.NES:
                // NES 10NES lockout chip
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_10nesLockout"],
                    Description = LocalizationManager.Instance["SecAnalyzer_10nesLockoutDesc"],
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.Nintendo3DS:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_3dsEncrypt"],
                    Description = LocalizationManager.Instance["SecAnalyzer_3dsEncryptDesc"],
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.NintendoDS:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_NdsEncrypt"],
                    Description = LocalizationManager.Instance["SecAnalyzer_NdsEncryptDesc"],
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.Oric:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_OricNoCopy"],
                    Description = LocalizationManager.Instance["SecAnalyzer_OricNoCopyDesc"],
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.Panasonic3DO:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_3doDiscEncrypt"],
                    Description = LocalizationManager.Instance["SecAnalyzer_3doDiscEncryptDesc"],
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.PCEngine:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_HuCardFormFactor"],
                    Description = LocalizationManager.Instance["SecAnalyzer_HuCardFormFactorDesc"],
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.PhilipsCDi:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_CdiProt"],
                    Description = LocalizationManager.Instance["SecAnalyzer_CdiProtDesc"],
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.SegaCD:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_SegaCdDiscProt"],
                    Description = LocalizationManager.Instance["SecAnalyzer_SegaCdDiscProtDesc"],
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.SegaDreamcast:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_DreamcastGdrom"],
                    Description = LocalizationManager.Instance["SecAnalyzer_DreamcastGdromDesc"],
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.SegaMasterSystem:
            case RomSystem.GameGear:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_SmsBiosCheck"],
                    Description = LocalizationManager.Instance["SecAnalyzer_SmsBiosCheckDesc"],
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.SegaSaturn:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_SaturnDiscProt"],
                    Description = LocalizationManager.Instance["SecAnalyzer_SaturnDiscProtDesc"],
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.SNES:
                // SNES CIC lockout chip
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_CicLockout"],
                    Description = LocalizationManager.Instance["SecAnalyzer_CicLockoutDesc"],
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.ThomsonMO5:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_ThomsonNoCopy"],
                    Description = LocalizationManager.Instance["SecAnalyzer_ThomsonNoCopyDesc"],
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.TigerGameCom:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_TigerNoCopy"],
                    Description = LocalizationManager.Instance["SecAnalyzer_TigerNoCopyDesc"],
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.VirtualBoy:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_VirtualBoyLogo"],
                    Description = LocalizationManager.Instance["SecAnalyzer_VirtualBoyLogoDesc"],
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.WataraSupervision:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_WataraNoCopy"],
                    Description = LocalizationManager.Instance["SecAnalyzer_WataraNoCopyDesc"],
                    Category = FeatureCategory.CopyProtection
                });
                break;

            case RomSystem.Wii:
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_WiiDiscEncrypt"],
                    Description = LocalizationManager.Instance["SecAnalyzer_WiiDiscEncryptDesc"],
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
                    Name = LocalizationManager.Instance["SecAnalyzer_SnesChecksum"],
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
                    Name = LocalizationManager.Instance["SecAnalyzer_N64Crc"],
                    Description = string.Format(LocalizationManager.Instance["SecAnalyzer_N64CrcDynDesc"], value1, value),
                    Category = FeatureCategory.ChecksumProtection
                });
            }
        }

        if (romInfo.System == RomSystem.NES)
        {
            features.Add(new SecurityFeature
            {
                Name = LocalizationManager.Instance["SecAnalyzer_iNesHeader"],
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
                    Name = LocalizationManager.Instance["SecAnalyzer_GbaHeaderCheck"],
                    Description = string.Format(LocalizationManager.Instance["SecAnalyzer_GbaHeaderCheckDynDesc"], gbaChecksum),
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
                    Name = LocalizationManager.Instance["SecAnalyzer_MegaDriveCheck"],
                    Description = string.Format(LocalizationManager.Instance["SecAnalyzer_MegaDriveCheckDynDesc"], mdChecksum),
                    Category = FeatureCategory.ChecksumProtection
                });
            }
        }

        if (romInfo.System == RomSystem.SegaMasterSystem || romInfo.System == RomSystem.GameGear)
        {
            if (romInfo.HeaderInfo.TryGetValue("Checksum", out string? smsChecksum))
            {
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_SmsGgChecksum"],
                    Description = string.Format(LocalizationManager.Instance["SecAnalyzer_SmsGgChecksumDynDesc"], smsChecksum),
                    Category = FeatureCategory.ChecksumProtection
                });
            }
        }

        if (romInfo.System == RomSystem.NintendoDS)
        {
            if (romInfo.HeaderInfo.TryGetValue("Header CRC16", out string? ndsCrc))
            {
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_NdsHeaderCrc"],
                    Description = string.Format(LocalizationManager.Instance["SecAnalyzer_NdsHeaderCrcDynDesc"], ndsCrc),
                    Category = FeatureCategory.ChecksumProtection
                });
            }
        }

        if (romInfo.System == RomSystem.Sega32X)
        {
            if (romInfo.HeaderInfo.TryGetValue("Checksum", out string? s32xChecksum))
            {
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_Sega32xCheck"],
                    Description = string.Format(LocalizationManager.Instance["SecAnalyzer_Sega32xCheckDynDesc"], s32xChecksum),
                    Category = FeatureCategory.ChecksumProtection
                });
            }
        }

        if (romInfo.System == RomSystem.VirtualBoy)
        {
            features.Add(new SecurityFeature
            {
                Name = LocalizationManager.Instance["SecAnalyzer_VBoyHeaderInt"],
                Description = romInfo.IsValid
                    ? "Virtual Boy ROM header validated successfully."
                    : "Virtual Boy ROM header validation failed — possible corruption.",
                Category = FeatureCategory.ChecksumProtection
            });
        }

        if (romInfo.System == RomSystem.AtariLynx)
        {
            if (romInfo.HeaderInfo.TryGetValue("Format", out string? lynxFmt))
            {
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_LynxHeaderInt"],
                    Description = lynxFmt.Contains("with header")
                        ? "LYNX header present — contains ROM size and bank information required by the Lynx hardware."
                        : "No LYNX header — headerless ROM dump. Some emulators may require the header for proper operation.",
                    Category = FeatureCategory.ChecksumProtection
                });
            }
        }

        if (romInfo.System == RomSystem.Atari7800)
        {
            if (romInfo.HeaderInfo.TryGetValue("Signature", out string? a78Sig))
            {
                features.Add(new SecurityFeature
                {
                    Name = LocalizationManager.Instance["SecAnalyzer_Atari7800HeaderSig"],
                    Description = a78Sig.Contains("ATARI7800")
                        ? "ATARI7800 signature found — header contains encryption, controller, and TV-type information."
                        : "Non-standard header signature — ROM may have a modified or missing header.",
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
                            Name = LocalizationManager.Instance["SecAnalyzer_SnesRegionLock"],
                            Description = string.Format(LocalizationManager.Instance["SecAnalyzer_SnesRegionLockDynDesc"], regionByte.ToString("X2"), region),
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
                Name = LocalizationManager.Instance["SecAnalyzer_N64RegionCode"],
                Description = string.Format(LocalizationManager.Instance["SecAnalyzer_N64RegionCodeDynDesc"], region, regionByte.ToString("X2")),
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
                Name = LocalizationManager.Instance["SecAnalyzer_N64CicBoot"],
                Description = string.Format(LocalizationManager.Instance["SecAnalyzer_N64CicBootDynDesc"], hash.ToString("X8")),
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
                Name = LocalizationManager.Instance["SecAnalyzer_GameBoyRegion"],
                Description = string.Format(LocalizationManager.Instance["SecAnalyzer_GameBoyRegionDynDesc"], dest.ToString("X2"), region),
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
                Name = LocalizationManager.Instance["SecAnalyzer_GBARegionCode"],
                Description = string.Format(LocalizationManager.Instance["SecAnalyzer_GBARegionCodeDynDesc"], regionChar, region),
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
                Name = LocalizationManager.Instance["SecAnalyzer_MegaDriveRegion"],
                Description = string.Format(LocalizationManager.Instance["SecAnalyzer_MegaDriveRegionDynDesc"], regionCode, description.Trim()),
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
                Name = LocalizationManager.Instance["SecAnalyzer_NintendoLogoCheck"],
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
                Name = LocalizationManager.Instance["SecAnalyzer_GBALogoCheck"],
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
                Name = LocalizationManager.Instance["SecAnalyzer_TMSSTrademarkSecuritySystem"],
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
                Name = LocalizationManager.Instance["SecAnalyzer_SMSGameGearRegion"],
                Description = string.Format(LocalizationManager.Instance["SecAnalyzer_SMSGameGearRegionDynDesc"], region),
                Category = FeatureCategory.RegionLock
            });
        }
        else
        {
            features.Add(new SecurityFeature
            {
                Name = LocalizationManager.Instance["SecAnalyzer_SMSGameGearRegion"],
                Description = LocalizationManager.Instance["SecAnalyzer_SMSGameGearRegionDesc"],
                Category = FeatureCategory.RegionLock
            });
        }
    }

    private static void CheckNeoGeoPocketRegion(RomInfo romInfo, List<SecurityFeature> features)
    {
        features.Add(new SecurityFeature
        {
            Name = LocalizationManager.Instance["SecAnalyzer_NeoGeoPocketRegion"],
            Description = LocalizationManager.Instance["SecAnalyzer_NeoGeoPocketRegionDesc"],
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
                Name = LocalizationManager.Instance["SecAnalyzer_GameBoyHeaderChecksum"],
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
                Name = LocalizationManager.Instance["SecAnalyzer_SegaCDRegion"],
                Description = string.Format(LocalizationManager.Instance["SecAnalyzer_SegaCDRegionDynDesc"], regionCode, description.Trim()),
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
                Name = LocalizationManager.Instance["SecAnalyzer_MSXCartridgeMarker"],
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
                Name = LocalizationManager.Instance["SecAnalyzer_GameCubeRegionLock"],
                Description = string.Format(LocalizationManager.Instance["SecAnalyzer_GameCubeRegionLockDynDesc"], region),
                Category = FeatureCategory.RegionLock
            });
        }
        else
        {
            features.Add(new SecurityFeature
            {
                Name = LocalizationManager.Instance["SecAnalyzer_GameCubeRegionLock"],
                Description = LocalizationManager.Instance["SecAnalyzer_GameCubeRegionLockDesc"],
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
                Name = LocalizationManager.Instance["SecAnalyzer_WiiRegionLock"],
                Description = string.Format(LocalizationManager.Instance["SecAnalyzer_WiiRegionLockDynDesc"], region),
                Category = FeatureCategory.RegionLock
            });
        }
        else
        {
            features.Add(new SecurityFeature
            {
                Name = LocalizationManager.Instance["SecAnalyzer_WiiRegionLock"],
                Description = LocalizationManager.Instance["SecAnalyzer_WiiRegionLockDesc"],
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
