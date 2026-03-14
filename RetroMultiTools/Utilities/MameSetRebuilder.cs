using System.IO.Compression;

namespace RetroMultiTools.Utilities;

/// <summary>
/// Rebuilds MAME ROM sets from loose/scattered ROM files into properly structured
/// ZIP archives matching a MAME XML database. Similar to CLRMamePro's Rebuilder.
/// </summary>
public static class MameSetRebuilder
{
    /// <summary>
    /// Scans a source directory for loose ROM files and builds an index by CRC32.
    /// </summary>
    public static async Task<Dictionary<string, List<SourceRom>>> IndexSourceDirectoryAsync(
        string sourceDirectory,
        IProgress<string>? progress = null)
    {
        if (!Directory.Exists(sourceDirectory))
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDirectory}");

        var index = new Dictionary<string, List<SourceRom>>(StringComparer.OrdinalIgnoreCase);

        var files = Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories).ToList();
        int count = 0;

        foreach (string file in files)
        {
            count++;
            if (count % 100 == 0)
                progress?.Report($"Indexing source files: {count} of {files.Count}...");

            string ext = Path.GetExtension(file).ToLowerInvariant();

            if (ext == ".zip")
            {
                await Task.Run(() => IndexZipContents(file, index)).ConfigureAwait(false);
            }
            else
            {
                string crc = await Task.Run(() => ComputeCrc32(file)).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(crc))
                {
                    var sourceRom = new SourceRom
                    {
                        FilePath = file,
                        FileName = Path.GetFileName(file),
                        CRC32 = crc,
                        Size = new FileInfo(file).Length,
                        IsInsideZip = false
                    };

                    if (!index.TryGetValue(crc, out var list))
                    {
                        list = [];
                        index[crc] = list;
                    }
                    list.Add(sourceRom);
                }
            }
        }

        progress?.Report($"Indexed {count} files, found {index.Count} unique CRC32 values.");
        return index;
    }

    /// <summary>
    /// Rebuilds ROM sets from indexed source files into the output directory.
    /// Creates properly structured ZIP files for each machine that can be completed.
    /// </summary>
    public static async Task<RebuildResult> RebuildAsync(
        List<MameMachine> machines,
        Dictionary<string, List<SourceRom>> sourceIndex,
        string outputDirectory,
        RebuildOptions options,
        IProgress<string>? progress = null)
    {
        if (!Directory.Exists(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        var result = new RebuildResult();
        int processed = 0;

        foreach (var machine in machines)
        {
            processed++;
            if (processed % 50 == 0)
                progress?.Report($"Processing {processed} of {machines.Count}: {machine.Name}...");

            // Get the ROMs that need to be in this set
            var requiredRoms = machine.Roms
                .Where(r => string.IsNullOrEmpty(r.Merge) && r.Status != "nodump" && !r.Optional)
                .ToList();

            if (requiredRoms.Count == 0) continue;

            string outputZip = Path.Combine(outputDirectory, machine.Name + ".zip");

            // Skip if the ZIP already exists and we're not overwriting
            if (!options.OverwriteExisting && File.Exists(outputZip))
            {
                result.SkippedCount++;
                continue;
            }

            // Check which ROMs we can find in the source index
            var foundRoms = new List<(MameRom rom, SourceRom source)>();
            var missingRoms = new List<MameRom>();

            foreach (var rom in requiredRoms)
            {
                if (!string.IsNullOrEmpty(rom.CRC32) && sourceIndex.TryGetValue(rom.CRC32, out var sources))
                {
                    // Find the best match (prefer matching size)
                    var match = sources.FirstOrDefault(s => rom.Size == 0 || s.Size == rom.Size) ?? sources[0];
                    foundRoms.Add((rom, match));
                }
                else
                {
                    missingRoms.Add(rom);
                }
            }

            // Only rebuild if we found at least some ROMs
            if (foundRoms.Count == 0) continue;

            // Decide whether to rebuild based on completeness option
            if (options.OnlyComplete && missingRoms.Count > 0) continue;

            bool success = await Task.Run(() => BuildZipFile(outputZip, foundRoms)).ConfigureAwait(false);

            if (success)
            {
                if (missingRoms.Count == 0)
                {
                    result.CompleteCount++;
                    result.RebuiltSets.Add(new RebuiltSetInfo
                    {
                        MachineName = machine.Name,
                        Description = machine.Description,
                        IsComplete = true,
                        RomsIncluded = foundRoms.Count,
                        RomsMissing = 0
                    });
                }
                else
                {
                    result.PartialCount++;
                    result.RebuiltSets.Add(new RebuiltSetInfo
                    {
                        MachineName = machine.Name,
                        Description = machine.Description,
                        IsComplete = false,
                        RomsIncluded = foundRoms.Count,
                        RomsMissing = missingRoms.Count,
                        MissingRomNames = missingRoms.Select(r => r.Name).ToList()
                    });
                }
            }
            else
            {
                result.FailedCount++;
            }
        }

        result.TotalMachines = machines.Count;
        progress?.Report($"Done — {result.CompleteCount} complete, {result.PartialCount} partial, {result.FailedCount} failed, {result.SkippedCount} skipped.");

        return result;
    }

    private static void IndexZipContents(string zipPath, Dictionary<string, List<SourceRom>> index)
    {
        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            foreach (var entry in archive.Entries)
            {
                if (entry.Length == 0) continue;

                string crc = entry.Crc32.ToString("X8");
                var sourceRom = new SourceRom
                {
                    FilePath = zipPath,
                    FileName = entry.FullName,
                    CRC32 = crc,
                    Size = entry.Length,
                    IsInsideZip = true,
                    ZipEntryName = entry.FullName
                };

                if (!index.TryGetValue(crc, out var list))
                {
                    list = [];
                    index[crc] = list;
                }
                list.Add(sourceRom);
            }
        }
        catch (InvalidDataException) { }
        catch (IOException) { }
    }

    private static bool BuildZipFile(string outputZip, List<(MameRom rom, SourceRom source)> roms)
    {
        try
        {
            using var archive = ZipFile.Open(outputZip, ZipArchiveMode.Create);
            foreach (var (rom, source) in roms)
            {
                if (source.IsInsideZip)
                {
                    using var sourceArchive = ZipFile.OpenRead(source.FilePath);
                    var sourceEntry = sourceArchive.GetEntry(source.ZipEntryName);
                    if (sourceEntry == null) continue;

                    var entry = archive.CreateEntry(rom.Name, CompressionLevel.Optimal);
                    using var entryStream = entry.Open();
                    using var sourceStream = sourceEntry.Open();
                    sourceStream.CopyTo(entryStream);
                }
                else
                {
                    var entry = archive.CreateEntry(rom.Name, CompressionLevel.Optimal);
                    using var entryStream = entry.Open();
                    using var sourceStream = new FileStream(source.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    sourceStream.CopyTo(entryStream);
                }
            }
            return true;
        }
        catch (IOException)
        {
            try { File.Delete(outputZip); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            try { File.Delete(outputZip); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            return false;
        }
    }

    private static string ComputeCrc32(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            uint crc = 0xFFFFFFFF;
            byte[] buffer = new byte[8192];
            int bytesRead;

            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i < bytesRead; i++)
                    crc = (crc >> 8) ^ Crc32Table[(crc ^ buffer[i]) & 0xFF];
            }

            return (crc ^ 0xFFFFFFFF).ToString("X8");
        }
        catch (IOException) { return ""; }
        catch (UnauthorizedAccessException) { return ""; }
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
}

public class SourceRom
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string CRC32 { get; set; } = string.Empty;
    public long Size { get; set; }
    public bool IsInsideZip { get; set; }
    public string ZipEntryName { get; set; } = string.Empty;
}

public class RebuildOptions
{
    public bool OverwriteExisting { get; set; }
    public bool OnlyComplete { get; set; }
}

public class RebuiltSetInfo
{
    public string MachineName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsComplete { get; set; }
    public int RomsIncluded { get; set; }
    public int RomsMissing { get; set; }
    public List<string> MissingRomNames { get; set; } = [];
}

public class RebuildResult
{
    public int TotalMachines { get; set; }
    public int CompleteCount { get; set; }
    public int PartialCount { get; set; }
    public int FailedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<RebuiltSetInfo> RebuiltSets { get; set; } = [];

    public string Summary =>
        $"{CompleteCount} complete, {PartialCount} partial, {FailedCount} failed, {SkippedCount} skipped out of {TotalMachines} machines.";
}
