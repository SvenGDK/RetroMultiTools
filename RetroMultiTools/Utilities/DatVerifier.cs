using System.Xml;
using System.Xml.Linq;
using RetroMultiTools.Localization;

namespace RetroMultiTools.Utilities;

/// <summary>
/// Verifies ROM files against No-Intro and TOSEC DAT files (XML format).
/// DAT files contain known-good checksums for verified ROM dumps.
/// </summary>
public static class DatVerifier
{
    private const int BufferSize = 81920;

    /// <summary>
    /// Loads a DAT file (CLRMAMEPro XML or Logiqx XML format) and returns the list of known ROM entries.
    /// </summary>
    public static List<DatEntry> LoadDatFile(string datFilePath)
    {
        if (!File.Exists(datFilePath))
            throw new FileNotFoundException("DAT file not found.", datFilePath);

        var entries = new List<DatEntry>();

        XDocument doc;
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                XmlResolver = null
            };
            using var reader = XmlReader.Create(datFilePath, settings);
            doc = XDocument.Load(reader);
        }
        catch (XmlException ex)
        {
            throw new InvalidOperationException($"Invalid DAT file: {ex.Message}", ex);
        }
        var root = doc.Root;

        if (root == null)
            throw new InvalidOperationException("Invalid DAT file: no root element.");

        // Support both "datafile" (Logiqx) and "clrmamepro" format
        var games = root.Elements("game").Concat(root.Elements("machine"));

        foreach (var game in games)
        {
            string gameName = game.Attribute("name")?.Value ?? "";

            foreach (var rom in game.Elements("rom"))
            {
                var entry = new DatEntry
                {
                    GameName = gameName,
                    RomName = rom.Attribute("name")?.Value ?? "",
                    Size = long.TryParse(rom.Attribute("size")?.Value, out long size) ? size : 0,
                    CRC32 = rom.Attribute("crc")?.Value?.ToUpperInvariant() ?? "",
                    MD5 = rom.Attribute("md5")?.Value?.ToUpperInvariant() ?? "",
                    SHA1 = rom.Attribute("sha1")?.Value?.ToUpperInvariant() ?? ""
                };

                entries.Add(entry);
            }
        }

        return entries;
    }

    /// <summary>
    /// Verifies a single ROM file against a list of DAT entries.
    /// </summary>
    public static async Task<VerificationResult> VerifyRomAsync(
        string romPath,
        List<DatEntry> datEntries,
        IProgress<string>? progress = null)
    {
        if (!File.Exists(romPath))
            throw new FileNotFoundException("ROM file not found.", romPath);

        string fileName = Path.GetFileName(romPath);
        long fileSize = new FileInfo(romPath).Length;

        var loc = LocalizationManager.Instance;

        progress?.Report(string.Format(loc["DatVerify_CalculatingChecksums"], fileName));
        var checksums = await ChecksumCalculator.CalculateAsync(romPath, null).ConfigureAwait(false);

        progress?.Report(loc["DatVerify_SearchingDatabase"]);

        // Try matching by CRC32 first (fastest), then SHA1, then MD5
        var match = datEntries.FirstOrDefault(e =>
            !string.IsNullOrEmpty(e.CRC32) && e.CRC32.Equals(checksums.CRC32, StringComparison.OrdinalIgnoreCase));

        match ??= datEntries.FirstOrDefault(e =>
            !string.IsNullOrEmpty(e.SHA1) && e.SHA1.Equals(checksums.SHA1, StringComparison.OrdinalIgnoreCase));

        match ??= datEntries.FirstOrDefault(e =>
            !string.IsNullOrEmpty(e.MD5) && e.MD5.Equals(checksums.MD5, StringComparison.OrdinalIgnoreCase));

        var result = new VerificationResult
        {
            FilePath = romPath,
            FileName = fileName,
            FileSize = fileSize,
            CRC32 = checksums.CRC32,
            MD5 = checksums.MD5,
            SHA1 = checksums.SHA1
        };

        if (match != null)
        {
            result.IsVerified = true;
            result.DatGameName = match.GameName;
            result.DatRomName = match.RomName;
            result.Status = LocalizationManager.Instance["DatVerify_Verified"];

            // Check if size matches too
            if (match.Size > 0 && match.Size != fileSize)
                result.Status = LocalizationManager.Instance["DatVerify_SizeMismatch"];
        }
        else
        {
            result.IsVerified = false;
            result.Status = LocalizationManager.Instance["DatVerify_NotFound"];
        }

        progress?.Report(loc["DatVerify_ProgressDone"]);
        return result;
    }

    /// <summary>
    /// Verifies all ROM files in a directory against a DAT file.
    /// </summary>
    public static async Task<BatchVerificationResult> VerifyDirectoryAsync(
        string romDirectory,
        string datFilePath,
        IProgress<string>? progress = null)
    {
        if (!Directory.Exists(romDirectory))
            throw new DirectoryNotFoundException($"Directory not found: {romDirectory}");

        var loc = LocalizationManager.Instance;
        progress?.Report(loc["DatVerify_LoadingDat"]);
        var datEntries = LoadDatFile(datFilePath);
        progress?.Report(string.Format(loc["DatVerify_LoadedDatEntries"], datEntries.Count));

        return await VerifyDirectoryAsync(romDirectory, datEntries, progress).ConfigureAwait(false);
    }

    /// <summary>
    /// Verifies all ROM files in a directory against pre-loaded DAT entries.
    /// </summary>
    public static async Task<BatchVerificationResult> VerifyDirectoryAsync(
        string romDirectory,
        List<DatEntry> datEntries,
        IProgress<string>? progress = null)
    {
        if (!Directory.Exists(romDirectory))
            throw new DirectoryNotFoundException($"Directory not found: {romDirectory}");

        var romFiles = Directory.EnumerateFiles(romDirectory, "*", SearchOption.AllDirectories)
            .Where(f => IsRomExtension(Path.GetExtension(f)))
            .ToList();

        var results = new List<VerificationResult>();
        var loc = LocalizationManager.Instance;

        for (int i = 0; i < romFiles.Count; i++)
        {
            progress?.Report(string.Format(loc["DatVerify_VerifyingProgress"], i + 1, romFiles.Count, Path.GetFileName(romFiles[i])));
            var result = await VerifyRomAsync(romFiles[i], datEntries, null).ConfigureAwait(false);
            results.Add(result);
        }

        int verified = results.Count(r => r.IsVerified);
        int unverified = results.Count - verified;

        progress?.Report(string.Format(loc["DatVerify_BatchComplete"], verified, unverified));

        return new BatchVerificationResult
        {
            Results = results,
            DatEntryCount = datEntries.Count,
            TotalRoms = romFiles.Count,
            VerifiedCount = verified,
            UnverifiedCount = unverified
        };
    }

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
        ".chd", ".rvz", ".gcm"
    };

    private static bool IsRomExtension(string ext) => RomExtensions.Contains(ext);
}

public class DatEntry
{
    public string GameName { get; set; } = string.Empty;
    public string RomName { get; set; } = string.Empty;
    public long Size { get; set; }
    public string CRC32 { get; set; } = string.Empty;
    public string MD5 { get; set; } = string.Empty;
    public string SHA1 { get; set; } = string.Empty;
}

public class VerificationResult
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string CRC32 { get; set; } = string.Empty;
    public string MD5 { get; set; } = string.Empty;
    public string SHA1 { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
    public string DatGameName { get; set; } = string.Empty;
    public string DatRomName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class BatchVerificationResult
{
    public List<VerificationResult> Results { get; set; } = [];
    public int DatEntryCount { get; set; }
    public int TotalRoms { get; set; }
    public int VerifiedCount { get; set; }
    public int UnverifiedCount { get; set; }

    public string Summary =>
        string.Format(LocalizationManager.Instance["DatVerify_BatchSummary"],
            VerifiedCount, TotalRoms, DatEntryCount, UnverifiedCount);
}
