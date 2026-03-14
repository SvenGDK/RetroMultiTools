using RetroMultiTools.Detection;
using RetroMultiTools.Models;

namespace RetroMultiTools.Utilities;

/// <summary>
/// Verifies ROM dump integrity by checking for common issues like
/// overdumps, underdumps, bad headers, blank regions, and known bad patterns.
/// </summary>
public static class DumpVerifier
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
    /// Standard ROM sizes for various systems (in bytes).
    /// </summary>
    private static readonly Dictionary<RomSystem, long[]> ExpectedSizes = new()
    {
        [RomSystem.NES] = [16384, 24576, 32768, 40960, 49152, 65536, 131072, 262144, 524288, 1048576],
        [RomSystem.SNES] = [262144, 524288, 786432, 1048576, 1572864, 2097152, 3145728, 4194304],
        [RomSystem.N64] = [4194304, 8388608, 12582912, 16777216, 33554432, 67108864],
        [RomSystem.GameBoy] = [32768, 65536, 131072, 262144, 524288, 1048576, 2097152],
        [RomSystem.GameBoyColor] = [32768, 65536, 131072, 262144, 524288, 1048576, 2097152, 4194304],
        [RomSystem.GameBoyAdvance] = [1048576, 2097152, 4194304, 8388608, 16777216, 33554432],
        [RomSystem.VirtualBoy] = [524288, 1048576, 2097152],
        [RomSystem.SegaMasterSystem] = [32768, 65536, 131072, 262144, 524288],
        [RomSystem.MegaDrive] = [131072, 262144, 524288, 1048576, 2097152, 4194304],
        [RomSystem.Sega32X] = [1048576, 2097152, 3145728, 4194304],
        [RomSystem.GameGear] = [32768, 65536, 131072, 262144, 524288],
        [RomSystem.Atari2600] = [2048, 4096, 8192, 16384, 32768],
        [RomSystem.Atari5200] = [8192, 16384, 32768],
        [RomSystem.Atari7800] = [16384, 32768, 65536, 131072, 262144, 524288, 1048576],
        [RomSystem.AtariJaguar] = [1048576, 2097152, 4194304, 6291456],
        [RomSystem.AtariLynx] = [32768, 65536, 131072, 262144, 524288],
        [RomSystem.PCEngine] = [32768, 65536, 131072, 262144, 524288, 786432, 1048576],
        [RomSystem.NeoGeoPocket] = [524288, 1048576, 2097152, 4194304],
        [RomSystem.ColecoVision] = [8192, 16384, 24576, 32768],
        [RomSystem.Intellivision] = [4096, 8192, 16384, 32768, 65536],
        [RomSystem.WataraSupervision] = [16384, 32768, 65536, 131072],
        [RomSystem.MSX] = [8192, 16384, 32768, 65536, 131072, 262144, 524288, 1048576],
        [RomSystem.MSX2] = [8192, 16384, 32768, 65536, 131072, 262144, 524288, 1048576],
        [RomSystem.ColorComputer] = [4096, 8192, 16384, 32768],
        [RomSystem.AmstradCPC] = [194816, 204800, 40960, 65536, 131072],
        [RomSystem.Oric] = [2048, 4096, 8192, 16384, 32768, 65536],
        [RomSystem.ThomsonMO5] = [4096, 8192, 16384, 32768, 65536, 131072],
        [RomSystem.SegaCD] = [681984000, 734003200, 746586112, 838860800],
        [RomSystem.Panasonic3DO] = [681984000, 734003200, 746586112, 838860800],
        [RomSystem.AmigaCD32] = [681984000, 734003200, 746586112, 838860800],
        [RomSystem.SegaSaturn] = [681984000, 734003200, 746586112, 838860800],
        [RomSystem.SegaDreamcast] = [681984000, 734003200, 746586112, 838860800, 1073741824],
        [RomSystem.GameCube] = [1459978240],
        [RomSystem.Wii] = [4699979776, 8511160320],
        [RomSystem.Arcade] = [262144, 524288, 1048576, 2097152, 4194304, 8388608, 16777216, 33554432],
    };

    /// <summary>
    /// Verifies a single ROM dump for common issues.
    /// </summary>
    public static async Task<DumpVerificationResult> VerifyAsync(
        string romPath,
        IProgress<string>? progress = null)
    {
        if (!File.Exists(romPath))
            throw new FileNotFoundException("ROM file not found.", romPath);

        var result = new DumpVerificationResult
        {
            FilePath = romPath,
            FileName = Path.GetFileName(romPath)
        };

        progress?.Report($"Analyzing {result.FileName}...");

        // Detect ROM type
        var romInfo = await Task.Run(() => RomDetector.Detect(romPath)).ConfigureAwait(false);
        result.System = romInfo.SystemName;
        result.FileSize = romInfo.FileSize;

        var issues = new List<string>();

        // Check 1: File size validation
        progress?.Report("Checking file size...");
        CheckFileSize(romInfo, issues);

        // Check 2: Check for overdump (trailing FF/00 bytes)
        progress?.Report("Checking for overdump...");
        await CheckOverdumpAsync(romPath, romInfo.FileSize, issues).ConfigureAwait(false);

        // Check 3: Check for all-zero or all-FF content (blank dump)
        progress?.Report("Checking for blank dump...");
        await CheckBlankDumpAsync(romPath, issues).ConfigureAwait(false);

        // Check 4: Header validation
        progress?.Report("Validating header...");
        CheckHeader(romInfo, issues);

        // Check 5: Power-of-two size check (many ROMs should be power of 2)
        CheckPowerOfTwoSize(romInfo, issues);

        result.Issues = issues;
        result.IsGoodDump = issues.Count == 0;
        result.Status = result.IsGoodDump ? "Good dump ✔" : $"Potential issues found ({issues.Count}) ⚠";

        progress?.Report("Done.");
        return result;
    }

    /// <summary>
    /// Verifies all ROM dumps in a directory.
    /// </summary>
    public static async Task<List<DumpVerificationResult>> VerifyDirectoryAsync(
        string directory,
        IProgress<string>? progress = null)
    {
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException($"Directory not found: {directory}");

        var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(f => RomExtensions.Contains(Path.GetExtension(f)))
            .ToList();

        var results = new List<DumpVerificationResult>();

        for (int i = 0; i < files.Count; i++)
        {
            progress?.Report($"Verifying {i + 1} of {files.Count}: {Path.GetFileName(files[i])}");

            try
            {
                var result = await VerifyAsync(files[i], null).ConfigureAwait(false);
                results.Add(result);
            }
            catch (IOException ex)
            {
                results.Add(new DumpVerificationResult
                {
                    FilePath = files[i],
                    FileName = Path.GetFileName(files[i]),
                    IsGoodDump = false,
                    Status = $"Error: {ex.Message}",
                    Issues = [$"Could not read file: {ex.Message}"]
                });
            }
        }

        int good = results.Count(r => r.IsGoodDump);
        int bad = results.Count - good;
        progress?.Report($"Done — {good} good dumps, {bad} with potential issues.");

        return results;
    }

    private static void CheckFileSize(RomInfo romInfo, List<string> issues)
    {
        if (romInfo.FileSize == 0)
        {
            issues.Add("File is empty (0 bytes).");
            return;
        }

        if (romInfo.FileSize < 512)
        {
            issues.Add($"File is unusually small ({FileUtils.FormatFileSize(romInfo.FileSize)}).");
            return;
        }

        if (ExpectedSizes.TryGetValue(romInfo.System, out var expectedSizes))
        {
            long romDataSize = romInfo.FileSize;
            // Account for copier headers
            if ((romDataSize % 1024) == 512)
                romDataSize -= 512;
            // Account for iNES header — only subtract if header info confirms it has one
            if (romInfo.System == RomSystem.NES && romDataSize > 16 &&
                romInfo.HeaderInfo.ContainsKey("Format"))
            {
                // Check for iNES header - only subtract 16 bytes for the standard sizes comparison
                romDataSize -= 16;
            }

            bool sizeMatch = expectedSizes.Any(s => s == romDataSize);
            if (!sizeMatch)
            {
                // Check if it's close to an expected size (possible overdump)
                long closest = expectedSizes.OrderBy(s => Math.Abs(s - romDataSize)).First();
                if (romDataSize > closest && romDataSize < closest * 2)
                    issues.Add($"ROM data size ({FileUtils.FormatFileSize(romDataSize)}) is larger than expected ({FileUtils.FormatFileSize(closest)}). Possible overdump.");
                else if (romDataSize < closest)
                    issues.Add($"ROM data size ({FileUtils.FormatFileSize(romDataSize)}) is smaller than expected ({FileUtils.FormatFileSize(closest)}). Possible underdump.");
            }
        }
    }

    private static async Task CheckOverdumpAsync(string filePath, long fileSize, List<string> issues)
    {
        if (fileSize < 1024) return;

        await Task.Run(() =>
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);

            // Check last 4KB for repeated bytes
            int checkSize = (int)Math.Min(4096, fileSize / 4);
            byte[] tail = new byte[checkSize];
            fs.Seek(-checkSize, SeekOrigin.End);
            int bytesRead = fs.Read(tail, 0, checkSize);
            if (bytesRead == 0) return;

            int ffCount = tail.AsSpan(0, bytesRead).Count((byte)0xFF);
            int zeroCount = tail.AsSpan(0, bytesRead).Count((byte)0x00);

            double ffRatio = (double)ffCount / bytesRead;
            double zeroRatio = (double)zeroCount / bytesRead;

            if (ffRatio > 0.95)
                issues.Add($"Last {FileUtils.FormatFileSize(checkSize)} of file is mostly 0xFF bytes ({ffRatio:P0}). Possible overdump.");
            else if (zeroRatio > 0.95)
                issues.Add($"Last {FileUtils.FormatFileSize(checkSize)} of file is mostly 0x00 bytes ({zeroRatio:P0}). Possible overdump or padding.");
        }).ConfigureAwait(false);
    }

    private static async Task CheckBlankDumpAsync(string filePath, List<string> issues)
    {
        await Task.Run(() =>
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
            byte[] buffer = new byte[BufferSize];
            long totalBytes = 0;
            long uniformBytes = 0;
            byte? uniformValue = null;
            bool isUniform = true;

            int bytesRead;
            while ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i < bytesRead; i++)
                {
                    totalBytes++;
                    uniformValue ??= buffer[i];

                    if (buffer[i] == uniformValue)
                        uniformBytes++;
                    else
                        isUniform = false;
                }
            }

            if (isUniform && totalBytes > 0)
                issues.Add($"Entire file contains only 0x{uniformValue:X2} bytes. This is a blank/bad dump.");
            else if (totalBytes > 0)
            {
                double uniformRatio = (double)uniformBytes / totalBytes;
                if (uniformRatio > 0.99)
                    issues.Add($"File is {uniformRatio:P1} identical bytes (0x{uniformValue:X2}). Likely a bad dump.");
            }
        }).ConfigureAwait(false);
    }

    private static void CheckHeader(RomInfo romInfo, List<string> issues)
    {
        if (!romInfo.IsValid && !string.IsNullOrEmpty(romInfo.ErrorMessage))
        {
            issues.Add($"Header validation failed: {romInfo.ErrorMessage}");
        }

        // Check for missing expected header info
        if (romInfo.System == RomSystem.NES)
        {
            if (romInfo.HeaderInfo.TryGetValue("PRG ROM Size", out string? prg) && prg == "0 KB")
                issues.Add("NES header declares 0 KB PRG ROM — likely invalid.");
        }

        if (romInfo.System == RomSystem.SNES)
        {
            if (romInfo.HeaderInfo.TryGetValue("Checksum Valid", out string? valid) && valid == "No")
                issues.Add("SNES internal checksum does not match. Header may be corrupt.");
        }

        if (romInfo.System == RomSystem.GameBoyAdvance)
        {
            if (romInfo.HeaderInfo.TryGetValue("GBA Logo", out string? logo) && logo == "Invalid")
                issues.Add("GBA header logo is invalid — ROM may not boot on hardware.");
        }

        if (romInfo.System == RomSystem.ColecoVision)
        {
            if (romInfo.HeaderInfo.TryGetValue("Magic Bytes", out string? magic) && magic.StartsWith("Non-standard"))
                issues.Add("ColecoVision magic bytes are non-standard — ROM may not be a valid ColecoVision cartridge.");
        }
    }

    private static void CheckPowerOfTwoSize(RomInfo romInfo, List<string> issues)
    {
        long romDataSize = romInfo.FileSize;

        // Account for headers
        if ((romDataSize % 1024) == 512) romDataSize -= 512;
        if (romInfo.System == RomSystem.NES && romDataSize > 16 && romInfo.HeaderInfo.ContainsKey("Format"))
            romDataSize -= 16;
        if (romInfo.System == RomSystem.AtariLynx &&
            romInfo.HeaderInfo.TryGetValue("Format", out string? lynxFormat) && lynxFormat.Contains("with header"))
            romDataSize -= 64;

        if (romDataSize <= 0) return;

        // Check if power of 2 or a standard multiple
        bool isPowerOf2 = (romDataSize & (romDataSize - 1)) == 0;
        bool isStandardMultiple = romDataSize % 131072 == 0 || romDataSize % 65536 == 0 ||
                                  romDataSize % 32768 == 0 || romDataSize % 16384 == 0 ||
                                  romDataSize % 8192 == 0;

        if (!isPowerOf2 && !isStandardMultiple && romDataSize > 8192)
        {
            issues.Add($"ROM data size ({FileUtils.FormatFileSize(romDataSize)}) is not a standard size. May indicate corruption.");
        }
    }
}

public class DumpVerificationResult
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string System { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public bool IsGoodDump { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<string> Issues { get; set; } = [];
}
