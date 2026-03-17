using System.Text.RegularExpressions;

namespace RetroMultiTools.Utilities;

/// <summary>
/// Identifies GoodTools labelling conventions from ROM filenames.
/// Recognizes Country Codes, Standard Codes, and GoodGen-Specific Codes
/// based on the GoodTools naming scheme.
/// </summary>
public static partial class GoodToolsIdentifier
{
    /// <summary>
    /// GoodTools country/region codes found in parentheses, e.g. (U), (E), (J), (UE).
    /// </summary>
    private static readonly Dictionary<string, string> CountryCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        { "1", "Japan & Korea" },
        { "4", "USA & Brazil (NTSC)" },
        { "A", "Australia" },
        { "B", "Non-USA (Genesis/Mega Drive)" },
        { "C", "China" },
        { "D", "Netherlands (Dutch)" },
        { "E", "Europe" },
        { "F", "France" },
        { "FC", "French-Canadian" },
        { "FN", "Finland" },
        { "G", "Germany" },
        { "GR", "Greece" },
        { "H", "Holland" },
        { "HK", "Hong Kong" },
        { "I", "Italy" },
        { "J", "Japan" },
        { "K", "Korea" },
        { "NL", "Netherlands" },
        { "NO", "Norway" },
        { "PD", "Public Domain" },
        { "R", "Russia" },
        { "S", "Spain" },
        { "SW", "Sweden" },
        { "U", "USA" },
        { "UK", "England" },
        { "Unl", "Unlicensed" },
        { "W", "World" },
        { "Ch", "China (alt)" },
        { "Unk", "Unknown Country" },
    };

    /// <summary>
    /// GoodTools standard dump-status codes found in square brackets, e.g. [!], [b1], [o2].
    /// Sorted by key length descending so longer prefixes (e.g. "T+") match before
    /// shorter ones (e.g. "T") during prefix-based lookup.
    /// </summary>
    private static readonly KeyValuePair<string, string>[] StandardCodes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "!", "Verified Good Dump" },
            { "a", "Alternate" },
            { "b", "Bad Dump" },
            { "BF", "Bung Fix" },
            { "c", "Cracked" },
            { "C", "Color" },
            { "f", "Fixed" },
            { "h", "Hack" },
            { "o", "Overdump" },
            { "p", "Pirate" },
            { "t", "Trained" },
            { "T", "Translation" },
            { "T-", "Old Translation" },
            { "T+", "Newer Translation" },
        }
        .OrderByDescending(kvp => kvp.Key.Length)
        .ToArray();

    /// <summary>
    /// GoodGen-specific codes (Sega Genesis/Mega Drive naming conventions).
    /// </summary>
    private static readonly Dictionary<string, string> GoodGenCodes = new(StringComparer.Ordinal)
    {
        { "REV", "Revision" },
        { "CCE", "Pirate (CCE Brazil)" },
        { "NFL", "Not for License" },
        { "CE", "Compact Edition" },
        { "PCB", "PC Board Rewrite" },
        { "MGD", "Multi Game Doctor Format" },
        { "NB", "Nb (No Border)" },
    };

    /// <summary>
    /// Regex that matches parenthesized tags like (U), (E), (J), (Unl), (PD), (JUE), (M5), etc.
    /// </summary>
    [GeneratedRegex(@"\(([^)]+)\)", RegexOptions.None)]
    private static partial Regex ParenthesizedTagRegex();

    /// <summary>
    /// Regex that matches square-bracket tags like [!], [b1], [o2], [T+Eng], [h1C], etc.
    /// </summary>
    [GeneratedRegex(@"\[([^\]]+)\]", RegexOptions.None)]
    private static partial Regex SquareBracketTagRegex();

    /// <summary>
    /// Identifies all GoodTools codes from a single ROM filename.
    /// </summary>
    /// <param name="fileName">The ROM filename (with or without extension).</param>
    /// <returns>A <see cref="GoodToolsResult"/> containing identified codes.</returns>
    public static GoodToolsResult Identify(string fileName)
    {
        string name = Path.GetFileNameWithoutExtension(fileName);
        var result = new GoodToolsResult { FileName = fileName };

        // Parse parenthesized tags for country codes and special codes
        foreach (Match match in ParenthesizedTagRegex().Matches(name))
        {
            string tag = match.Groups[1].Value;
            ParseParenthesizedTag(tag, result);
        }

        // Parse square-bracket tags for standard dump codes
        foreach (Match match in SquareBracketTagRegex().Matches(name))
        {
            string tag = match.Groups[1].Value;
            ParseSquareBracketTag(tag, result);
        }

        return result;
    }

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
        ".chf", ".tgc", ".mtx", ".run"
    };

    /// <summary>
    /// Identifies GoodTools codes for all ROM files in a directory.
    /// </summary>
    public static async Task<List<GoodToolsResult>> IdentifyDirectoryAsync(
        string directoryPath, IProgress<string>? progress = null)
    {
        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");

        var files = Directory.GetFiles(directoryPath)
            .Where(f => KnownExtensions.Contains(Path.GetExtension(f)))
            .ToArray();

        progress?.Report($"Found {files.Length} ROM file(s)...");

        var results = new List<GoodToolsResult>();

        await Task.Run(() =>
        {
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                progress?.Report($"Identifying {i + 1}/{files.Length}: {Path.GetFileName(file)}");

                var result = Identify(Path.GetFileName(file));
                result.FilePath = file;
                results.Add(result);
            }
        }).ConfigureAwait(false);

        progress?.Report($"Done. Identified codes in {results.Count} file(s).");
        return results;
    }

    /// <summary>
    /// Returns a compact summary string of identified GoodTools codes for display.
    /// </summary>
    public static string GetSummary(string fileName)
    {
        var result = Identify(fileName);
        return result.GetSummary();
    }

    private static void ParseParenthesizedTag(string tag, GoodToolsResult result)
    {
        // Check for exact country code matches first
        if (CountryCodes.TryGetValue(tag, out string? countryDesc))
        {
            result.CountryCodes.Add(new GoodToolsCode(tag, countryDesc, GoodToolsCodeType.Country));
            return;
        }

        // Check for combined country codes like "UE", "JU", "JUE", etc.
        // Per GoodTools conventions, only U/E/J/W are combined in multi-letter region codes.
        string tagUpper = tag.ToUpperInvariant();
        if (tagUpper.Length >= 2 && tagUpper.Length <= 4 && tagUpper.All(c => "UEJW".Contains(c)))
        {
            var countries = tagUpper.Select(c =>
                CountryCodes.TryGetValue(c.ToString(), out string? desc) ? desc : c.ToString());
            result.CountryCodes.Add(new GoodToolsCode(tag,
                string.Join(", ", countries), GoodToolsCodeType.Country));
            return;
        }

        // Check for multilanguage tag (M#)
        if (tag.StartsWith('M') && tag.Length >= 2 && int.TryParse(tag[1..], out int langCount))
        {
            result.StandardCodes.Add(new GoodToolsCode(tag,
                $"Multilanguage ({langCount} languages)", GoodToolsCodeType.Standard, inParentheses: true));
            return;
        }

        // Check for revision tag (REVxx / Rev xx)
        if (tag.StartsWith("REV", StringComparison.OrdinalIgnoreCase))
        {
            string rev = tag.Length > 3 ? tag[3..].TrimStart() : "?";
            result.GoodGenCodes.Add(new GoodToolsCode(tag,
                $"Revision {rev}", GoodToolsCodeType.GoodGen));
            return;
        }

        // Check for volume tag (Vol #)
        if (tag.StartsWith("Vol", StringComparison.OrdinalIgnoreCase))
        {
            result.StandardCodes.Add(new GoodToolsCode(tag,
                $"Volume {tag[3..].Trim()}", GoodToolsCodeType.Standard, inParentheses: true));
            return;
        }

        // Check for year (4-digit number)
        if (tag.Length == 4 && int.TryParse(tag, out int year) && year >= 1970 && year <= 2030)
        {
            result.StandardCodes.Add(new GoodToolsCode(tag,
                $"Year {year}", GoodToolsCodeType.Standard, inParentheses: true));
            return;
        }

        // Check for unknown year marker (-)
        if (tag == "-")
        {
            result.StandardCodes.Add(new GoodToolsCode(tag,
                "Unknown Year", GoodToolsCodeType.Standard, inParentheses: true));
            return;
        }

        // Check for GoodGen-specific codes
        foreach (var kvp in GoodGenCodes)
        {
            if (tag.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
            {
                string suffix = tag.Length > kvp.Key.Length ? " " + tag[kvp.Key.Length..].Trim() : "";
                result.GoodGenCodes.Add(new GoodToolsCode(tag,
                    kvp.Value + suffix, GoodToolsCodeType.GoodGen));
                return;
            }
        }
    }

    private static void ParseSquareBracketTag(string tag, GoodToolsResult result)
    {
        // Exact match for verified good dump
        if (tag == "!")
        {
            result.StandardCodes.Add(new GoodToolsCode(tag,
                "Verified Good Dump", GoodToolsCodeType.Standard));
            return;
        }

        // Parse codes with optional numeric suffix and optional sub-tag
        // e.g., [b1], [o2], [h1C], [T+Eng], [T-Chi], [f1], [a1]
        // StandardCodes is sorted by key length descending so "T+" matches before "T".
        foreach (var kvp in StandardCodes)
        {
            if (kvp.Key == "!") continue; // Already handled

            if (tag.StartsWith(kvp.Key, StringComparison.Ordinal))
            {
                string remainder = tag[kvp.Key.Length..];
                string description = kvp.Value;

                if (!string.IsNullOrEmpty(remainder))
                {
                    description += $" ({remainder})";
                }

                result.StandardCodes.Add(new GoodToolsCode(tag,
                    description, GoodToolsCodeType.Standard));
                return;
            }
        }
    }
}

/// <summary>
/// The type of GoodTools code.
/// </summary>
public enum GoodToolsCodeType
{
    Country,
    Standard,
    GoodGen
}

/// <summary>
/// Represents a single identified GoodTools code.
/// </summary>
public class GoodToolsCode
{
    public string Code { get; }
    public string Description { get; }
    public GoodToolsCodeType Type { get; }
    /// <summary>
    /// True when this code was found inside parentheses in the original filename,
    /// even if it is semantically a Standard code (e.g. multilanguage, year, volume).
    /// Used to render the correct bracket style in summaries.
    /// </summary>
    public bool InParentheses { get; }

    public GoodToolsCode(string code, string description, GoodToolsCodeType type, bool inParentheses = false)
    {
        Code = code;
        Description = description;
        Type = type;
        InParentheses = inParentheses;
    }

    public override string ToString() => $"{Code} = {Description}";
}

/// <summary>
/// Result of identifying GoodTools codes in a ROM filename.
/// </summary>
public class GoodToolsResult
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public List<GoodToolsCode> CountryCodes { get; set; } = [];
    public List<GoodToolsCode> StandardCodes { get; set; } = [];
    public List<GoodToolsCode> GoodGenCodes { get; set; } = [];

    /// <summary>
    /// Returns true if any GoodTools codes were identified.
    /// </summary>
    public bool HasCodes => CountryCodes.Count > 0 || StandardCodes.Count > 0 || GoodGenCodes.Count > 0;

    /// <summary>
    /// Returns all identified codes.
    /// </summary>
    public IEnumerable<GoodToolsCode> AllCodes =>
        CountryCodes.Concat(StandardCodes).Concat(GoodGenCodes);

    /// <summary>
    /// Returns a compact summary string for display in lists.
    /// </summary>
    public string GetSummary()
    {
        if (!HasCodes) return string.Empty;

        var parts = new List<string>();

        foreach (var code in CountryCodes)
            parts.Add($"({code.Code})");
        foreach (var code in StandardCodes)
            parts.Add(code.InParentheses ? $"({code.Code})" : $"[{code.Code}]");
        foreach (var code in GoodGenCodes)
            parts.Add($"({code.Code})");

        return string.Join(" ", parts);
    }

    /// <summary>
    /// Returns a detailed multi-line description of all identified codes.
    /// </summary>
    public string GetDetailedDescription()
    {
        if (!HasCodes) return "No GoodTools codes identified.";

        var sb = new System.Text.StringBuilder();

        if (CountryCodes.Count > 0)
        {
            sb.AppendLine("Country/Region Codes:");
            foreach (var code in CountryCodes)
                sb.AppendLine($"  ({code.Code}) = {code.Description}");
        }

        if (StandardCodes.Count > 0)
        {
            if (sb.Length > 0) sb.AppendLine();
            sb.AppendLine("Standard Codes:");
            foreach (var code in StandardCodes)
            {
                string bracket = code.InParentheses ? $"({code.Code})" : $"[{code.Code}]";
                sb.AppendLine($"  {bracket} = {code.Description}");
            }
        }

        if (GoodGenCodes.Count > 0)
        {
            if (sb.Length > 0) sb.AppendLine();
            sb.AppendLine("GoodGen-Specific Codes:");
            foreach (var code in GoodGenCodes)
                sb.AppendLine($"  ({code.Code}) = {code.Description}");
        }

        return sb.ToString().TrimEnd();
    }
}
