using RetroMultiTools.Detection;
using RetroMultiTools.Models;
using System.Text;

namespace RetroMultiTools.Utilities;

public static class RomHeaderExporter
{
    public static async Task ExportSingleAsync(
        string romPath,
        string outputPath,
        IProgress<string>? progress = null)
    {
        if (!File.Exists(romPath))
            throw new FileNotFoundException("ROM file not found.", romPath);

        progress?.Report("Detecting ROM...");
        var info = await Task.Run(() => RomDetector.Detect(romPath)).ConfigureAwait(false);

        progress?.Report("Writing report...");
        var sb = new StringBuilder();
        AppendRomReport(sb, info);

        try
        {
            await File.WriteAllTextAsync(outputPath, sb.ToString()).ConfigureAwait(false);
        }
        catch
        {
            try { File.Delete(outputPath); } catch { /* best-effort cleanup */ }
            throw;
        }
        progress?.Report("Done.");
    }

    public static async Task ExportBatchAsync(
        string inputDirectory,
        string outputPath,
        IProgress<string>? progress = null)
    {
        if (!Directory.Exists(inputDirectory))
            throw new DirectoryNotFoundException($"Directory not found: {inputDirectory}");

        progress?.Report("Scanning directory...");
        var roms = await Task.Run(() => RomOrganizer.ScanDirectory(inputDirectory, progress)).ConfigureAwait(false);

        if (roms.Count == 0)
        {
            progress?.Report("No ROMs found in directory.");
            return;
        }

        string ext = Path.GetExtension(outputPath).ToLowerInvariant();
        if (ext == ".csv")
        {
            progress?.Report($"Exporting {roms.Count} ROM(s) to CSV...");
            await ExportCsvAsync(roms, outputPath).ConfigureAwait(false);
        }
        else
        {
            progress?.Report($"Exporting {roms.Count} ROM(s) to text report...");
            await ExportTextAsync(roms, outputPath).ConfigureAwait(false);
        }

        progress?.Report($"Done — exported {roms.Count} ROM(s).");
    }

    private static async Task ExportTextAsync(List<RomInfo> roms, string outputPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("╔══════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║              RetroMultiTools — ROM Header Report            ║");
        sb.AppendLine($"║  Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}                            ║");
        sb.AppendLine($"║  Total ROMs: {roms.Count,-48}║");
        sb.AppendLine("╚══════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        for (int i = 0; i < roms.Count; i++)
        {
            sb.AppendLine($"── ROM {i + 1} of {roms.Count} ──────────────────────────────────────");
            AppendRomReport(sb, roms[i]);
            sb.AppendLine();
        }

        // Summary by system
        sb.AppendLine("── Summary ─────────────────────────────────────────────────────");
        var systemGroups = roms
            .GroupBy(r => r.SystemName)
            .OrderByDescending(g => g.Count());

        foreach (var group in systemGroups)
        {
            sb.AppendLine($"  {group.Key}: {group.Count()} ROM(s)");
        }

        int validCount = roms.Count(r => r.IsValid);
        int invalidCount = roms.Count - validCount;
        sb.AppendLine($"  Valid: {validCount}, Invalid: {invalidCount}");

        try
        {
            await File.WriteAllTextAsync(outputPath, sb.ToString()).ConfigureAwait(false);
        }
        catch
        {
            try { File.Delete(outputPath); } catch { /* best-effort cleanup */ }
            throw;
        }
    }

    private static async Task ExportCsvAsync(List<RomInfo> roms, string outputPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("FileName,System,FileSize,FileSizeFormatted,IsValid,ErrorMessage,HeaderFields");

        foreach (var rom in roms)
        {
            string headerFields = rom.HeaderInfo.Count > 0
                ? string.Join("; ", rom.HeaderInfo.Select(kv => $"{kv.Key}={kv.Value}"))
                : "";

            sb.Append(CsvEscape(rom.FileName));
            sb.Append(',');
            sb.Append(CsvEscape(rom.SystemName));
            sb.Append(',');
            sb.Append(rom.FileSize);
            sb.Append(',');
            sb.Append(CsvEscape(rom.FileSizeFormatted));
            sb.Append(',');
            sb.Append(rom.IsValid);
            sb.Append(',');
            sb.Append(CsvEscape(rom.ErrorMessage ?? ""));
            sb.Append(',');
            sb.AppendLine(CsvEscape(headerFields));
        }

        try
        {
            await File.WriteAllTextAsync(outputPath, sb.ToString()).ConfigureAwait(false);
        }
        catch
        {
            try { File.Delete(outputPath); } catch { /* best-effort cleanup */ }
            throw;
        }
    }

    private static void AppendRomReport(StringBuilder sb, RomInfo info)
    {
        sb.AppendLine($"  File:     {info.FileName}");
        sb.AppendLine($"  Path:     {info.FilePath}");
        sb.AppendLine($"  System:   {info.SystemName}");
        sb.AppendLine($"  Size:     {info.FileSizeFormatted} ({info.FileSize:N0} bytes)");
        sb.AppendLine($"  Valid:    {(info.IsValid ? "Yes" : "No")}");

        if (!string.IsNullOrEmpty(info.ErrorMessage))
            sb.AppendLine($"  Error:    {info.ErrorMessage}");

        if (info.HeaderInfo.Count > 0)
        {
            sb.AppendLine("  Header:");
            foreach (var kv in info.HeaderInfo)
                sb.AppendLine($"    {kv.Key}: {kv.Value}");
        }
    }

    private static string CsvEscape(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
