namespace RetroMultiTools.Utilities;

public static class DuplicateFinder
{
    private const int BufferSize = 81920;

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
        ".chd", ".rvz", ".gcm"
    };

    public static async Task<(List<DuplicateGroup> Groups, int TotalFilesScanned)> FindDuplicatesAsync(string directoryPath, IProgress<string>? progress = null)
    {
        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");

        var files = Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories)
            .Where(f => KnownExtensions.Contains(Path.GetExtension(f)))
            .ToList();

        progress?.Report($"Found {files.Count} ROM file(s). Computing checksums...");

        var hashMap = new Dictionary<string, List<string>>();

        for (int i = 0; i < files.Count; i++)
        {
            var file = files[i];
            progress?.Report($"Hashing file {i + 1} of {files.Count}: {Path.GetFileName(file)}");

            string crc = await Task.Run(() => ComputeCrc32(file)).ConfigureAwait(false);

            if (!hashMap.TryGetValue(crc, out var list))
            {
                list = [];
                hashMap[crc] = list;
            }
            list.Add(file);
        }

        progress?.Report("Identifying duplicates...");

        var duplicates = hashMap
            .Where(kv => kv.Value.Count >= 2)
            .Select(kv => new DuplicateGroup { Hash = kv.Key, FilePaths = kv.Value })
            .ToList();

        progress?.Report("Done.");
        return (duplicates, files.Count);
    }

    public static DuplicateResult BuildResult(List<DuplicateGroup> groups, int totalFilesScanned)
    {
        long wastedBytes = 0;
        foreach (var group in groups)
        {
            // All copies after the first are considered wasted
            foreach (var file in group.FilePaths.Skip(1))
            {
                if (File.Exists(file))
                    wastedBytes += new FileInfo(file).Length;
            }
        }

        return new DuplicateResult
        {
            TotalFilesScanned = totalFilesScanned,
            DuplicateGroups = groups.Count,
            WastedBytes = wastedBytes
        };
    }

    private static string ComputeCrc32(string filePath)
    {
        uint crc = 0xFFFFFFFF;
        byte[] buffer = new byte[BufferSize];
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
        int bytesRead;
        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < bytesRead; i++)
                crc = (crc >> 8) ^ Crc32Table[(crc ^ buffer[i]) & 0xFF];
        }
        return (crc ^ 0xFFFFFFFF).ToString("X8");
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

public class DuplicateGroup
{
    public string Hash { get; set; } = string.Empty;
    public List<string> FilePaths { get; set; } = [];
}

public class DuplicateResult
{
    public int TotalFilesScanned { get; set; }
    public int DuplicateGroups { get; set; }
    public long WastedBytes { get; set; }

    public string Summary =>
        $"{DuplicateGroups} duplicate group(s) found across {TotalFilesScanned} file(s), {FileUtils.FormatFileSize(WastedBytes)} wasted";
}
