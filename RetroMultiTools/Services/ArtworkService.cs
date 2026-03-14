using RetroMultiTools.Models;
using System.Text.RegularExpressions;

namespace RetroMultiTools.Services;

public static partial class ArtworkService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
        DefaultRequestHeaders =
        {
            { "User-Agent", "RetroMultiTools/1.0" }
        }
    };

    private static readonly Dictionary<RomSystem, string> LibretroSystemNames = new()
    {
        { RomSystem.NES, "Nintendo_-_Nintendo_Entertainment_System" },
        { RomSystem.SNES, "Nintendo_-_Super_Nintendo_Entertainment_System" },
        { RomSystem.N64, "Nintendo_-_Nintendo_64" },
        { RomSystem.GameBoy, "Nintendo_-_Game_Boy" },
        { RomSystem.GameBoyColor, "Nintendo_-_Game_Boy_Color" },
        { RomSystem.GameBoyAdvance, "Nintendo_-_Game_Boy_Advance" },
        { RomSystem.VirtualBoy, "Nintendo_-_Virtual_Boy" },
        { RomSystem.SegaMasterSystem, "Sega_-_Master_System_-_Mark_III" },
        { RomSystem.MegaDrive, "Sega_-_Mega_Drive_-_Genesis" },
        { RomSystem.SegaCD, "Sega_-_Mega-CD_-_Sega_CD" },
        { RomSystem.Sega32X, "Sega_-_32X" },
        { RomSystem.GameGear, "Sega_-_Game_Gear" },
        { RomSystem.Atari2600, "Atari_-_2600" },
        { RomSystem.Atari5200, "Atari_-_5200" },
        { RomSystem.Atari7800, "Atari_-_7800" },
        { RomSystem.AtariJaguar, "Atari_-_Jaguar" },
        { RomSystem.AtariLynx, "Atari_-_Lynx" },
        { RomSystem.PCEngine, "NEC_-_PC_Engine_-_TurboGrafx_16" },
        { RomSystem.NeoGeoPocket, "SNK_-_Neo_Geo_Pocket_Color" }, // Covers both Neo Geo Pocket and Pocket Color
        { RomSystem.ColecoVision, "Coleco_-_ColecoVision" },
        { RomSystem.Intellivision, "Mattel_-_Intellivision" },
        { RomSystem.MSX, "Microsoft_-_MSX" },
        { RomSystem.MSX2, "Microsoft_-_MSX2" },
        { RomSystem.AmstradCPC, "Amstrad_-_CPC" },
        { RomSystem.WataraSupervision, "Watara_-_Supervision" },
        { RomSystem.Oric, "Oric" },
        { RomSystem.ThomsonMO5, "Thomson_-_MOTO" },
        { RomSystem.ColorComputer, "Tandy_-_Color_Computer" },
        { RomSystem.GameCube, "Nintendo_-_GameCube" },
        { RomSystem.Wii, "Nintendo_-_Wii" },
        { RomSystem.Arcade, "MAME" },
    };

    private static readonly string[] CommonRegionSuffixes =
    [
        "(USA)",
        "(World)",
        "(USA, Europe)",
        "(Europe)",
        "(Japan)",
        "(Japan, USA)",
        "(Japan, Europe)",
    ];

    /// <summary>
    /// Fetches artwork for a ROM from the Libretro Thumbnails database.
    /// Tries multiple candidate game names to maximize the chance of finding a match.
    /// Returns a populated <see cref="ArtworkInfo"/> with any images that were found.
    /// </summary>
    public static async Task<ArtworkInfo> FetchArtworkAsync(
        RomInfo romInfo,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var artwork = new ArtworkInfo();

        if (romInfo.System == RomSystem.Unknown ||
            !LibretroSystemNames.TryGetValue(romInfo.System, out var systemName))
        {
            return artwork;
        }

        string baseUrl = $"https://raw.githubusercontent.com/libretro-thumbnails/{systemName}/master";

        var candidates = BuildCandidateNames(romInfo);

        // Try each candidate name: check boxart first to find the right name,
        // then fetch all artwork types for the matching name
        foreach (string candidate in candidates)
        {
            string sanitized = SanitizeForLibretro(candidate);
            string encoded = Uri.EscapeDataString(sanitized);

            progress?.Report($"Searching artwork for \"{candidate}\"...");
            var boxArt = await TryDownloadImageAsync(
                $"{baseUrl}/Named_Boxarts/{encoded}.png", cancellationToken, progress).ConfigureAwait(false);

            if (boxArt != null)
            {
                artwork.BoxArt = boxArt;

                progress?.Report("Fetching screenshot...");
                artwork.Snap = await TryDownloadImageAsync(
                    $"{baseUrl}/Named_Snaps/{encoded}.png", cancellationToken, progress).ConfigureAwait(false);

                progress?.Report("Fetching title screen...");
                artwork.TitleScreen = await TryDownloadImageAsync(
                    $"{baseUrl}/Named_Titles/{encoded}.png", cancellationToken, progress).ConfigureAwait(false);

                return artwork;
            }
        }

        // If no boxart was found with any candidate, try fetching snaps/titles
        // with the first candidate as a final attempt
        if (candidates.Count > 0)
        {
            string sanitized = SanitizeForLibretro(candidates[0]);
            string encoded = Uri.EscapeDataString(sanitized);

            progress?.Report("Fetching screenshot...");
            artwork.Snap = await TryDownloadImageAsync(
                $"{baseUrl}/Named_Snaps/{encoded}.png", cancellationToken, progress).ConfigureAwait(false);

            progress?.Report("Fetching title screen...");
            artwork.TitleScreen = await TryDownloadImageAsync(
                $"{baseUrl}/Named_Titles/{encoded}.png", cancellationToken, progress).ConfigureAwait(false);
        }

        return artwork;
    }

    /// <summary>
    /// Builds a prioritized list of candidate game names to try against the
    /// Libretro Thumbnails database. Tries exact filename first, then stripped
    /// variations, then the filename with common No-Intro region suffixes appended.
    /// </summary>
    private static List<string> BuildCandidateNames(RomInfo romInfo)
    {
        var candidates = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string fileName = Path.GetFileNameWithoutExtension(romInfo.FileName);
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            AddCandidate(candidates, seen, fileName);

            // Always strip square-bracket tags like [!], [b], [h], etc.
            // These are never part of Libretro Thumbnail filenames
            string withoutBrackets = SquareBracketTagRegex().Replace(fileName, "").TrimEnd();
            if (!string.Equals(withoutBrackets, fileName, StringComparison.Ordinal))
                AddCandidate(candidates, seen, withoutBrackets);

            // Try replacing underscores with spaces (common in user-renamed ROMs)
            string withSpaces = withoutBrackets.Replace('_', ' ');
            if (!string.Equals(withSpaces, withoutBrackets, StringComparison.Ordinal))
                AddCandidate(candidates, seen, withSpaces);

            // Check if the cleaned name contains a No-Intro region tag
            string cleanedName = (withSpaces != withoutBrackets) ? withSpaces : withoutBrackets;
            bool hasRegionTag = NoIntroRegionTagRegex().IsMatch(cleanedName);

            if (!hasRegionTag)
            {
                // Strip GoodTools-style region tags like (U), (E), (J), (UE), (JUE), etc.
                string stripped = GoodToolsRegionRegex().Replace(cleanedName, "").TrimEnd();
                if (!string.Equals(stripped, cleanedName, StringComparison.Ordinal))
                    AddCandidate(candidates, seen, stripped);

                // Strip ALL remaining parenthesized tags to get a clean base name.
                // This handles revision tags, dump codes, language codes, etc.
                string allStripped = AllParenthesizedTagRegex().Replace(stripped, "").TrimEnd();
                if (!string.Equals(allStripped, stripped, StringComparison.Ordinal))
                    AddCandidate(candidates, seen, allStripped);

                // Try appending common No-Intro region suffixes
                string baseName = allStripped;
                foreach (string suffix in CommonRegionSuffixes)
                {
                    AddCandidate(candidates, seen, $"{baseName} {suffix}");
                }
            }
            else
            {
                // Even with a No-Intro region tag, try stripping extra tags
                // like (Rev A), (SGB Enhanced), etc. for a broader match
                string baseWithRegion = ExtraParenthesizedTagRegex().Replace(cleanedName, "").TrimEnd();
                if (!string.Equals(baseWithRegion, cleanedName, StringComparison.Ordinal))
                    AddCandidate(candidates, seen, baseWithRegion);
            }
        }

        // Fall back to the parsed title from header info
        if (romInfo.HeaderInfo.TryGetValue("Title", out var title) &&
            !string.IsNullOrWhiteSpace(title))
        {
            string trimmedTitle = title.Trim();
            AddCandidate(candidates, seen, trimmedTitle);

            // Also try the header title with common region suffixes
            foreach (string suffix in CommonRegionSuffixes)
            {
                AddCandidate(candidates, seen, $"{trimmedTitle} {suffix}");
            }
        }

        return candidates;
    }

    private static void AddCandidate(List<string> candidates, HashSet<string> seen, string name)
    {
        if (seen.Add(name))
            candidates.Add(name);
    }

    /// <summary>
    /// Replaces characters that are not allowed in Libretro Thumbnail filenames with underscores.
    /// </summary>
    private static string SanitizeForLibretro(string name)
    {
        char[] invalid = ['&', '*', '/', ':', '`', '<', '>', '?', '\\', '|'];
        foreach (char c in invalid)
        {
            name = name.Replace(c, '_');
        }
        return name;
    }

    private static async Task<byte[]?> TryDownloadImageAsync(
        string url, CancellationToken cancellationToken, IProgress<string>? progress = null)
    {
        try
        {
            using var response = await HttpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            progress?.Report($"Network error fetching artwork: {ex.Message}");
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Explicit user cancellation — rethrow to stop the operation
            throw;
        }
        catch (TaskCanceledException)
        {
            progress?.Report("Artwork request timed out.");
        }

        return null;
    }

    // Matches No-Intro style region tags like (USA), (World), (Europe), (Japan), (Japan, USA), etc.
    [GeneratedRegex(@"\((?:USA|World|Europe|Japan|Korea|Asia|Australia|Brazil|Canada|China|France|Germany|Italy|Netherlands|Russia|Spain|Sweden)(?:,\s*(?:USA|World|Europe|Japan|Korea|Asia|Australia|Brazil|Canada|China|France|Germany|Italy|Netherlands|Russia|Spain|Sweden))*\)", RegexOptions.None)]
    private static partial Regex NoIntroRegionTagRegex();

    // Matches square-bracket tags like [!], [U], [E], [J], [b], [o], [h ...], [p], etc.
    [GeneratedRegex(@"\s*\[[^\]]*\]", RegexOptions.None)]
    private static partial Regex SquareBracketTagRegex();

    // Matches GoodTools-style region codes like (U), (E), (J), (W), (UE), (JU), (JUE),
    // as well as single-letter language/region codes (F), (G), (S), (I), (K), (A), (C)
    // and special codes like (Unl), (PD).
    // Multi-char combos use [UEJW]{1,4} which is intentionally liberal — GoodTools allows
    // any combination of these letters (e.g., UE, JU, JUE, JUEW) and matching invalid
    // combos like (UUUU) is harmless since they won't appear in real filenames.
    [GeneratedRegex(@"\s*\(([UEJW]{1,4}|[FGSIKAC]|Unl|PD)\)", RegexOptions.None)]
    private static partial Regex GoodToolsRegionRegex();

    // Matches any parenthesized tag, used to strip all remaining tags for fallback matching
    [GeneratedRegex(@"\s*\([^)]*\)", RegexOptions.None)]
    private static partial Regex AllParenthesizedTagRegex();

    // Matches parenthesized tags that are NOT No-Intro region tags (e.g. (Rev A), (SGB Enhanced), (En)).
    // Used to strip extra qualifiers while preserving the region tag.
    // Note: The region list intentionally mirrors NoIntroRegionTagRegex — both are [GeneratedRegex]
    // attributes which cannot reference shared constants, and the list is stable (country names).
    [GeneratedRegex(@"\s*\((?!USA|World|Europe|Japan|Korea|Asia|Australia|Brazil|Canada|China|France|Germany|Italy|Netherlands|Russia|Spain|Sweden)[^)]*\)", RegexOptions.None)]
    private static partial Regex ExtraParenthesizedTagRegex();
}
