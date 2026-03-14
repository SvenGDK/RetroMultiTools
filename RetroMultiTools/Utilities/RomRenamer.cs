using RetroMultiTools.Detection;
using RetroMultiTools.Models;

namespace RetroMultiTools.Utilities;

public static class RomRenamer
{
    private static readonly HashSet<string> KnownExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".nes", ".smc", ".sfc", ".z64", ".n64", ".v64",
        ".gb", ".gbc", ".gba", ".vb", ".vboy",
        ".sms", ".md", ".gen",
        ".bin", ".32x", ".gg",
        ".a26", ".a52", ".a78", ".j64", ".jag",
        ".lnx", ".lyx",
        ".pce", ".tg16",
        ".ngp", ".ngc",
        ".col", ".cv", ".int",
        ".mx1", ".mx2",
        ".sv", ".ccc",
        ".iso", ".cue", ".3do",
        ".gcm"
    };

    public static RenamePreview PreviewRename(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found.", filePath);

        var info = RomDetector.Detect(filePath);
        string newName = BuildNewName(info);
        string ext = Path.GetExtension(filePath);

        return new RenamePreview
        {
            OriginalPath = filePath,
            OriginalName = Path.GetFileName(filePath),
            NewName = SanitizeFileName(newName) + ext,
            DetectedTitle = GetHeaderTitle(info),
            DetectedSystem = info.SystemName,
            WouldChange = !string.Equals(
                Path.GetFileName(filePath),
                SanitizeFileName(newName) + ext,
                StringComparison.Ordinal)
        };
    }

    public static async Task<List<RenamePreview>> PreviewBatchRenameAsync(
        string directoryPath, IProgress<string>? progress = null)
    {
        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");

        var files = Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories)
            .Where(f => KnownExtensions.Contains(Path.GetExtension(f)))
            .ToList();

        progress?.Report($"Found {files.Count} ROM file(s). Analyzing...");

        var previews = new List<RenamePreview>();
        for (int i = 0; i < files.Count; i++)
        {
            progress?.Report($"Analyzing {i + 1} of {files.Count}: {Path.GetFileName(files[i])}");
            var preview = await Task.Run(() => PreviewRename(files[i])).ConfigureAwait(false);
            previews.Add(preview);
        }

        progress?.Report("Done.");
        return previews;
    }

    public static RenameResult ApplyRename(RenamePreview preview)
    {
        if (!File.Exists(preview.OriginalPath))
            return new RenameResult { Success = false, Error = "File not found." };

        if (!preview.WouldChange)
            return new RenameResult { Success = true, Skipped = true };

        string dir = Path.GetDirectoryName(preview.OriginalPath) ?? "";
        string newPath = Path.Combine(dir, preview.NewName);

        if (File.Exists(newPath))
            return new RenameResult { Success = false, Error = "Target file already exists." };

        try
        {
            File.Move(preview.OriginalPath, newPath);
            return new RenameResult { Success = true, NewPath = newPath };
        }
        catch (IOException ex)
        {
            return new RenameResult { Success = false, Error = ex.Message };
        }
        catch (UnauthorizedAccessException ex)
        {
            return new RenameResult { Success = false, Error = ex.Message };
        }
    }

    public static int ApplyBatchRename(List<RenamePreview> previews, IProgress<string>? progress = null)
    {
        int renamed = 0;
        for (int i = 0; i < previews.Count; i++)
        {
            var preview = previews[i];
            if (!preview.WouldChange) continue;

            progress?.Report($"Renaming {i + 1} of {previews.Count}: {preview.OriginalName}");
            var result = ApplyRename(preview);
            if (result.Success && !result.Skipped)
                renamed++;
        }
        progress?.Report("Done.");
        return renamed;
    }

    private static string GetHeaderTitle(RomInfo info)
    {
        if (info.HeaderInfo.TryGetValue("Title", out var title) && !string.IsNullOrWhiteSpace(title))
            return title.Trim();
        if (info.HeaderInfo.TryGetValue("Internal Name", out var intName) && !string.IsNullOrWhiteSpace(intName))
            return intName.Trim();
        if (info.HeaderInfo.TryGetValue("Game Title", out var gameTitle) && !string.IsNullOrWhiteSpace(gameTitle))
            return gameTitle.Trim();
        return "";
    }

    private static string BuildNewName(RomInfo info)
    {
        string title = GetHeaderTitle(info);
        if (string.IsNullOrWhiteSpace(title))
            return Path.GetFileNameWithoutExtension(info.FilePath);

        string region = "";
        if (info.HeaderInfo.TryGetValue("Region", out var r) && !string.IsNullOrWhiteSpace(r))
            region = $" ({r.Trim()})";
        else if (info.HeaderInfo.TryGetValue("Destination Code", out var dc) && !string.IsNullOrWhiteSpace(dc))
            region = $" ({dc.Trim()})";

        return title + region;
    }

    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Trim();
    }
}

public class RenamePreview
{
    public string OriginalPath { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string NewName { get; set; } = string.Empty;
    public string DetectedTitle { get; set; } = string.Empty;
    public string DetectedSystem { get; set; } = string.Empty;
    public bool WouldChange { get; set; }
}

public class RenameResult
{
    public bool Success { get; set; }
    public bool Skipped { get; set; }
    public string? NewPath { get; set; }
    public string? Error { get; set; }
}
