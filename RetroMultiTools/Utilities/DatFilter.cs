using System.Text.RegularExpressions;
using System.Xml;

namespace RetroMultiTools.Utilities;

/// <summary>
/// Filters DAT file entries using Retool-like logic: category exclusion, region/language
/// priority, and 1G1R (One Game, One ROM) deduplication for No-Intro/Redump naming conventions.
/// </summary>
public static partial class DatFilter
{
    public static readonly string[] CommonRegions =
    [
        "USA", "Europe", "Japan", "World",
        "Korea", "Brazil", "Asia", "China",
        "Australia", "Germany", "France", "Spain",
        "Italy", "Netherlands", "Sweden", "Norway",
        "Denmark", "Finland", "Canada", "Russia",
        "Taiwan", "Hong Kong", "United Kingdom"
    ];

    public static readonly string[] CommonLanguages =
    [
        "En", "Fr", "De", "Es", "Ja", "Zh", "Ko",
        "It", "Nl", "Pt", "Sv", "No", "Da", "Fi",
        "Ru", "Pl", "Cs", "Hu", "El", "Tr", "Ro"
    ];

    [GeneratedRegex(@"\(([^)]+)\)", RegexOptions.Compiled)]
    private static partial Regex ParenGroupRegex();

    [GeneratedRegex(@"^(?:Rev\s+|v)(\d+(?:\.\d+)?[A-Za-z]?)$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex RevisionRegex();

    private static readonly HashSet<string> KnownRegions =
        new(CommonRegions, StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> KnownLanguages =
        new(CommonLanguages, StringComparer.OrdinalIgnoreCase);

    public static GameInfo ParseGameInfo(string gameName)
    {
        var info = new GameInfo();
        var matches = ParenGroupRegex().Matches(gameName);

        int firstRegionIndex = -1;

        foreach (Match m in matches)
        {
            string inner = m.Groups[1].Value.Trim();
            string innerLower = inner.ToLowerInvariant();

            if (innerLower is "beta" || innerLower.StartsWith("beta ", StringComparison.Ordinal))
            {
                info.IsBeta = true;
                continue;
            }

            if (innerLower is "proto" || innerLower.StartsWith("proto ", StringComparison.Ordinal))
            {
                info.IsPrototype = true;
                continue;
            }

            if (innerLower is "demo" || innerLower.StartsWith("demo ", StringComparison.Ordinal) ||
                innerLower.EndsWith(" demo", StringComparison.Ordinal))
            {
                info.IsDemo = true;
                continue;
            }

            if (innerLower is "sample" || innerLower.StartsWith("sample ", StringComparison.Ordinal))
            {
                info.IsSample = true;
                continue;
            }

            if (innerLower is "unl")
            {
                info.IsUnlicensed = true;
                continue;
            }

            if (innerLower is "pirate")
            {
                info.IsPirate = true;
                continue;
            }

            if (innerLower is "bios" || innerLower.StartsWith("bios ", StringComparison.Ordinal))
            {
                info.IsBIOS = true;
                continue;
            }

            if (innerLower is "program" or "test program" or "sdk")
            {
                info.IsApplication = true;
                continue;
            }

            var revMatch = RevisionRegex().Match(inner);
            if (revMatch.Success)
            {
                info.Revision = revMatch.Groups[1].Value;
                continue;
            }

            // Check for regions (may be comma-separated: "USA, Europe")
            var parts = inner.Split(',', StringSplitOptions.TrimEntries);
            bool allRegions = parts.Length > 0 && parts.All(p => KnownRegions.Contains(p));
            if (allRegions)
            {
                info.Regions.AddRange(parts);
                if (firstRegionIndex < 0)
                    firstRegionIndex = m.Index;
                continue;
            }

            // Check for languages (may be comma-separated: "En,Fr,De")
            bool allLanguages = parts.Length > 0 && parts.All(p => KnownLanguages.Contains(p));
            if (allLanguages)
            {
                info.Languages.AddRange(parts);
            }
        }

        // Base name: everything before the first region group, trimmed
        if (firstRegionIndex > 0)
            info.BaseName = gameName[..firstRegionIndex].TrimEnd();
        else
            info.BaseName = StripParenthetical(gameName);

        return info;
    }

    private static string StripParenthetical(string name)
    {
        return ParenGroupRegex().Replace(name, "").Trim();
    }

    /// <summary>
    /// Filters DAT entries by category exclusions, region/language priority, and optional 1G1R deduplication.
    /// </summary>
    public static async Task<(List<DatEntry> Filtered, DatFilterResult Stats)> FilterAsync(
        List<DatEntry> entries,
        DatFilterOptions options,
        IProgress<string>? progress = null)
    {
        return await Task.Run(() =>
        {
            var stats = new DatFilterResult { OriginalCount = entries.Count };

            progress?.Report($"Parsing {entries.Count} game names...");
            var parsed = entries.Select(e => (Entry: e, Info: ParseGameInfo(e.GameName))).ToList();

            // Step 1: Category exclusions
            progress?.Report("Applying category filters...");
            var afterCategory = parsed.Where(p => !ShouldExcludeByCategory(p.Info, options)).ToList();
            stats.ExcludedByCategory = parsed.Count - afterCategory.Count;
            progress?.Report($"Excluded {stats.ExcludedByCategory} entries by category.");

            // Step 2: 1G1R
            List<(DatEntry Entry, GameInfo Info)> afterOneGameOneRom;
            if (options.Enable1G1R)
            {
                progress?.Report("Applying 1G1R filtering...");
                afterOneGameOneRom = ApplyOneGameOneRom(afterCategory, options, progress);
                stats.ExcludedBy1G1R = afterCategory.Count - afterOneGameOneRom.Count;
                progress?.Report($"Excluded {stats.ExcludedBy1G1R} entries by 1G1R.");
            }
            else
            {
                afterOneGameOneRom = afterCategory;
            }

            var filtered = afterOneGameOneRom.Select(p => p.Entry).ToList();
            stats.FilteredCount = filtered.Count;

            progress?.Report(stats.Summary);
            return (filtered, stats);
        }).ConfigureAwait(false);
    }

    private static bool ShouldExcludeByCategory(GameInfo info, DatFilterOptions options)
    {
        if (options.ExcludeDemos && info.IsDemo) return true;
        if (options.ExcludeBetas && info.IsBeta) return true;
        if (options.ExcludePrototypes && info.IsPrototype) return true;
        if (options.ExcludeSamples && info.IsSample) return true;
        if (options.ExcludeUnlicensed && info.IsUnlicensed) return true;
        if (options.ExcludeBIOS && info.IsBIOS) return true;
        if (options.ExcludeApplications && info.IsApplication) return true;
        if (options.ExcludePirateEditions && info.IsPirate) return true;
        return false;
    }

    private static List<(DatEntry Entry, GameInfo Info)> ApplyOneGameOneRom(
        List<(DatEntry Entry, GameInfo Info)> entries,
        DatFilterOptions options,
        IProgress<string>? progress)
    {
        var groups = entries.GroupBy(e => e.Info.BaseName, StringComparer.OrdinalIgnoreCase);
        var result = new List<(DatEntry Entry, GameInfo Info)>();

        foreach (var group in groups)
        {
            var candidates = group.ToList();
            if (candidates.Count <= 1)
            {
                result.AddRange(candidates);
                continue;
            }

            var best = SelectBestVersion(candidates, options);
            result.Add(best);
        }

        return result;
    }

    private static (DatEntry Entry, GameInfo Info) SelectBestVersion(
        List<(DatEntry Entry, GameInfo Info)> candidates,
        DatFilterOptions options)
    {
        // Score each candidate: lower score is better
        var scored = candidates.Select(c =>
        {
            int regionScore = GetBestRegionScore(c.Info, options.RegionPriority);
            int languageScore = GetBestLanguageScore(c.Info, options.LanguagePriority);
            double revisionScore = GetRevisionScore(c.Info, options.PreferRevisions);
            return (Candidate: c, RegionScore: regionScore, LanguageScore: languageScore, RevisionScore: revisionScore);
        }).ToList();

        // Sort: region priority first, then language, then revision (descending for prefer higher)
        scored.Sort((a, b) =>
        {
            int cmp = a.RegionScore.CompareTo(b.RegionScore);
            if (cmp != 0) return cmp;

            cmp = a.LanguageScore.CompareTo(b.LanguageScore);
            if (cmp != 0) return cmp;

            // Higher revision is better, so reverse
            return b.RevisionScore.CompareTo(a.RevisionScore);
        });

        return scored[0].Candidate;
    }

    private static int GetBestRegionScore(GameInfo info, List<string> regionPriority)
    {
        if (info.Regions.Count == 0)
            return regionPriority.Count + 1;

        // "World" matches all regions
        bool hasWorld = info.Regions.Any(r => r.Equals("World", StringComparison.OrdinalIgnoreCase));

        int bestIndex = int.MaxValue;
        foreach (string region in info.Regions)
        {
            int idx = regionPriority.FindIndex(r => r.Equals(region, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0 && idx < bestIndex)
                bestIndex = idx;
        }

        if (hasWorld && bestIndex == int.MaxValue)
            bestIndex = 0; // "World" acts as universal fallback matching top priority

        return bestIndex == int.MaxValue ? regionPriority.Count + 1 : bestIndex;
    }

    private static int GetBestLanguageScore(GameInfo info, List<string> languagePriority)
    {
        if (info.Languages.Count == 0)
            return languagePriority.Count + 1;

        int bestIndex = int.MaxValue;
        foreach (string lang in info.Languages)
        {
            int idx = languagePriority.FindIndex(l => l.Equals(lang, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0 && idx < bestIndex)
                bestIndex = idx;
        }

        return bestIndex == int.MaxValue ? languagePriority.Count + 1 : bestIndex;
    }

    private static double GetRevisionScore(GameInfo info, bool preferRevisions)
    {
        if (string.IsNullOrEmpty(info.Revision))
            return preferRevisions ? -1.0 : 0.0; // Negative score penalizes missing revision when higher revisions are preferred

        // Try to parse numeric revision
        if (double.TryParse(info.Revision, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double numericRev))
            return numericRev;

        // Letter revisions: A=1, B=2, etc.
        if (info.Revision.Length == 1 && char.IsLetter(info.Revision[0]))
            return char.ToUpperInvariant(info.Revision[0]) - 'A' + 1;

        return 0.0;
    }

    /// <summary>
    /// Exports filtered DAT entries as a Logiqx XML DAT file.
    /// </summary>
    public static async Task ExportFilteredDat(
        List<DatEntry> entries,
        string outputPath,
        string datName,
        string datDescription,
        IProgress<string>? progress = null)
    {
        await Task.Run(() =>
        {
            progress?.Report($"Exporting {entries.Count} entries to {Path.GetFileName(outputPath)}...");

            try
            {
                var settings = new XmlWriterSettings
                {
                    Indent = true,
                    IndentChars = "  ",
                    NewLineChars = "\n",
                    NewLineHandling = NewLineHandling.Replace
                };

                using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
                using var writer = XmlWriter.Create(stream, settings);

                writer.WriteStartDocument();
                writer.WriteDocType("datafile", "-//Logiqx//DTD ROM Management Datafile//EN",
                    "http://www.logiqx.com/Dats/datafile.dtd", null);

                writer.WriteStartElement("datafile");

                // Header
                writer.WriteStartElement("header");
                writer.WriteElementString("name", datName);
                writer.WriteElementString("description", datDescription);
                writer.WriteEndElement();

                // Game entries
                foreach (var entry in entries)
                {
                    writer.WriteStartElement("game");
                    writer.WriteAttributeString("name", entry.GameName);

                    writer.WriteStartElement("rom");
                    writer.WriteAttributeString("name", entry.RomName);
                    writer.WriteAttributeString("size", entry.Size.ToString());
                    writer.WriteAttributeString("crc", entry.CRC32.ToLowerInvariant());

                    if (!string.IsNullOrEmpty(entry.MD5))
                        writer.WriteAttributeString("md5", entry.MD5.ToLowerInvariant());
                    if (!string.IsNullOrEmpty(entry.SHA1))
                        writer.WriteAttributeString("sha1", entry.SHA1.ToLowerInvariant());

                    writer.WriteEndElement(); // rom
                    writer.WriteEndElement(); // game
                }

                writer.WriteEndElement(); // datafile
                writer.WriteEndDocument();

                progress?.Report($"Exported {entries.Count} entries.");
            }
            catch (Exception) when (File.Exists(outputPath))
            {
                try { File.Delete(outputPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                throw;
            }
        }).ConfigureAwait(false);
    }
}

public class DatFilterOptions
{
    public List<string> RegionPriority { get; set; } = ["USA", "Europe", "Japan"];
    public List<string> LanguagePriority { get; set; } = ["En", "Fr", "De", "Es", "Ja"];
    public bool ExcludeDemos { get; set; }
    public bool ExcludeBetas { get; set; }
    public bool ExcludePrototypes { get; set; }
    public bool ExcludeSamples { get; set; }
    public bool ExcludeUnlicensed { get; set; }
    public bool ExcludeBIOS { get; set; }
    public bool ExcludeApplications { get; set; }
    public bool ExcludePirateEditions { get; set; }
    public bool Enable1G1R { get; set; }
    public bool PreferRevisions { get; set; }
}

public class DatFilterResult
{
    public int OriginalCount { get; set; }
    public int FilteredCount { get; set; }
    public int ExcludedByCategory { get; set; }
    public int ExcludedBy1G1R { get; set; }

    public string Summary =>
        $"{FilteredCount} of {OriginalCount} entries retained. " +
        $"Excluded: {ExcludedByCategory} by category, {ExcludedBy1G1R} by 1G1R.";
}

public class GameInfo
{
    public string BaseName { get; set; } = string.Empty;
    public List<string> Regions { get; set; } = [];
    public List<string> Languages { get; set; } = [];
    public bool IsDemo { get; set; }
    public bool IsBeta { get; set; }
    public bool IsPrototype { get; set; }
    public bool IsSample { get; set; }
    public bool IsUnlicensed { get; set; }
    public bool IsBIOS { get; set; }
    public bool IsApplication { get; set; }
    public bool IsPirate { get; set; }
    public string Revision { get; set; } = string.Empty;
}
