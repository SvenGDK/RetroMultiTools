using RetroMultiTools.Detection;
using RetroMultiTools.Models;

namespace RetroMultiTools.Utilities;

public static class RomOrganizer
{
    private static readonly HashSet<string> KnownExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".nes", ".smc", ".sfc", ".z64", ".n64", ".v64",
        ".gb", ".gbc", ".gba", ".vb", ".vboy",
        ".sms", ".md", ".gen",
        ".bin", ".iso", ".cue", ".32x", ".gg",
        ".a26", ".a52", ".a78", ".j64", ".jag",
        ".lnx", ".lyx",
        ".pce", ".tg16",
        ".ngp", ".ngc",
        ".col", ".cv", ".int",
        ".mx1", ".mx2",
        ".dsk", ".cdt", ".sna",
        ".tap",
        ".mo5", ".k7", ".fd",
        ".sv", ".ccc",
        ".3do", ".cdi", ".gdi",
        ".chd", ".rvz", ".gcm",
        ".chf",
        ".tgc",
        ".mtx", ".run",
        ".zip"
    };

    public static List<RomInfo> ScanDirectory(string path, IProgress<string>? progress = null)
    {
        var results = new List<RomInfo>();
        if (!Directory.Exists(path)) return results;

        var files = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
            .Where(f => KnownExtensions.Contains(Path.GetExtension(f)))
            .ToList();

        int total = files.Count;
        progress?.Report($"Found {total} ROM file(s). Detecting...");

        for (int i = 0; i < total; i++)
        {
            var file = files[i];
            progress?.Report($"Detecting ROM {i + 1} of {total}: {Path.GetFileName(file)}");
            results.Add(RomDetector.Detect(file));
        }

        return results;
    }

    /// <summary>
    /// Organizes ROMs by system using copy mode with no system filter.
    /// </summary>
    public static OrganizeResult OrganizeBySystem(List<RomInfo> roms, string outputDir, IProgress<string>? progress = null)
        => OrganizeBySystem(roms, outputDir, moveFiles: false, systemFilter: null, progress);

    public static OrganizeResult OrganizeBySystem(List<RomInfo> roms, string outputDir, bool moveFiles, RomSystem? systemFilter, IProgress<string>? progress = null)
    {
        Directory.CreateDirectory(outputDir);

        IReadOnlyList<RomInfo> filtered = systemFilter.HasValue
            ? roms.Where(r => r.System == systemFilter.Value).ToList()
            : roms;

        int processed = 0;
        int skipped = 0;
        int failed = 0;
        string verb = moveFiles ? "Moving" : "Copying";
        var createdFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < filtered.Count; i++)
        {
            var rom = filtered[i];
            progress?.Report($"{verb} {i + 1} of {filtered.Count}: {rom.FileName}");
            try
            {
                var systemFolder = Path.Combine(outputDir, SanitizeFolderName(rom.SystemName));
                if (createdFolders.Add(systemFolder))
                    Directory.CreateDirectory(systemFolder);

                var destPath = Path.Combine(systemFolder, rom.FileName);
                if (!File.Exists(destPath))
                {
                    if (moveFiles)
                        File.Move(rom.FilePath, destPath);
                    else
                        File.Copy(rom.FilePath, destPath);
                    processed++;
                }
                else
                {
                    skipped++;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FileNotFoundException)
            {
                failed++;
            }
        }

        return new OrganizeResult { Processed = processed, Skipped = skipped, Failed = failed, UsedMove = moveFiles };
    }

    public static string GetSystemDisplayName(RomSystem system) =>
        RomDetector.GetSystemDisplayName(system);

    /// <summary>
    /// Copies a ROM file to the specified destination directory.
    /// Returns the full destination path of the copied file.
    /// </summary>
    public static string CopyRom(string sourceFilePath, string destinationDirectory)
    {
        if (!File.Exists(sourceFilePath))
            throw new FileNotFoundException("Source ROM file not found.", sourceFilePath);

        Directory.CreateDirectory(destinationDirectory);
        string destPath = Path.Combine(destinationDirectory, Path.GetFileName(sourceFilePath));

        if (string.Equals(Path.GetFullPath(sourceFilePath), Path.GetFullPath(destPath), StringComparison.OrdinalIgnoreCase))
            throw new IOException("Source and destination are the same file.");

        File.Copy(sourceFilePath, destPath, overwrite: false);
        return destPath;
    }

    /// <summary>
    /// Moves a ROM file to the specified destination directory.
    /// Returns the full destination path of the moved file.
    /// </summary>
    public static string MoveRom(string sourceFilePath, string destinationDirectory)
    {
        if (!File.Exists(sourceFilePath))
            throw new FileNotFoundException("Source ROM file not found.", sourceFilePath);

        Directory.CreateDirectory(destinationDirectory);
        string destPath = Path.Combine(destinationDirectory, Path.GetFileName(sourceFilePath));

        if (string.Equals(Path.GetFullPath(sourceFilePath), Path.GetFullPath(destPath), StringComparison.OrdinalIgnoreCase))
            throw new IOException("Source and destination are the same file.");

        File.Move(sourceFilePath, destPath, overwrite: false);
        return destPath;
    }

    /// <summary>
    /// Deletes a ROM file permanently.
    /// </summary>
    public static void DeleteRom(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("ROM file not found.", filePath);

        File.Delete(filePath);
    }

    /// <summary>
    /// Checks if a file has a known ROM extension.
    /// </summary>
    public static bool IsKnownRomExtension(string filePath) =>
        KnownExtensions.Contains(Path.GetExtension(filePath));

    private static string SanitizeFolderName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}

public class OrganizeResult
{
    public int Processed { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public bool UsedMove { get; set; }

    public string Summary
    {
        get
        {
            string verb = UsedMove ? "moved" : "copied";
            var parts = new List<string> { $"{Processed} {verb}" };
            if (Skipped > 0)
                parts.Add($"{Skipped} skipped (already exist)");
            if (Failed > 0)
                parts.Add($"{Failed} failed");
            return string.Join(", ", parts);
        }
    }
}
