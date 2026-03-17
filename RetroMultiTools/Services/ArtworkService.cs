using RetroMultiTools.Models;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RetroMultiTools.Services;

public static partial class ArtworkService
{
    private static readonly HttpClient HttpClient = CreateHttpClient();

    /// <summary>Marker bytes written when a URL returns 404 so we don't re-fetch.</summary>
    private static readonly byte[] NegativeCacheMarkerBytes =
        System.Text.Encoding.UTF8.GetBytes("404");

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(10)
        };
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        client.DefaultRequestHeaders.Add("User-Agent", "RetroMultiTools/1.0");
        return client;
    }

    private static readonly string CacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RetroMultiTools", "ArtworkCache");

    private static readonly string IndexCacheDirectory = Path.Combine(
        CacheDirectory, "Indices");

    private static readonly TimeSpan IndexCacheTtl = TimeSpan.FromDays(7);

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
        { RomSystem.Panasonic3DO, "The_3DO_Company_-_3DO" },
        { RomSystem.AmigaCD32, "Commodore_-_Amiga" },
        { RomSystem.SegaSaturn, "Sega_-_Saturn" },
        { RomSystem.SegaDreamcast, "Sega_-_Dreamcast" },
        { RomSystem.GameCube, "Nintendo_-_GameCube" },
        { RomSystem.Wii, "Nintendo_-_Wii" },
        { RomSystem.Arcade, "MAME" },
        { RomSystem.Atari800, "Atari_-_8-bit" },
        { RomSystem.NECPC88, "NEC_-_PC-88" },
        { RomSystem.N64DD, "Nintendo_-_Nintendo_64DD" },
        { RomSystem.NintendoDS, "Nintendo_-_Nintendo_DS" },
        { RomSystem.Nintendo3DS, "Nintendo_-_Nintendo_3DS" },
        { RomSystem.NeoGeo, "SNK_-_Neo_Geo" },
        { RomSystem.NeoGeoCD, "SNK_-_Neo_Geo_CD" },
        { RomSystem.PhilipsCDi, "Philips_-_CDi" },
        { RomSystem.FairchildChannelF, "Fairchild_-_Channel_F" },
        { RomSystem.TigerGameCom, "Tiger_-_Game.com" },
        { RomSystem.MemotechMTX, "Memotech_-_MTX" },
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

        // No exact candidate matched. Fall back to the thumbnail index: fetch
        // the list of all known thumbnails for this system and look for an entry
        // whose base name matches one of our candidates.
        progress?.Report("Searching thumbnail index...");
        var indexMatch = await FindMatchInIndexAsync(
            systemName, candidates, cancellationToken).ConfigureAwait(false);

        if (indexMatch != null)
        {
            string sanitized = SanitizeForLibretro(indexMatch);
            string encoded = Uri.EscapeDataString(sanitized);

            progress?.Report($"Searching artwork for \"{indexMatch}\"...");
            var boxArt = await TryDownloadImageAsync(
                $"{baseUrl}/Named_Boxarts/{encoded}.png", cancellationToken, progress).ConfigureAwait(false);

            if (boxArt != null)
                artwork.BoxArt = boxArt;

            progress?.Report("Fetching screenshot...");
            artwork.Snap = await TryDownloadImageAsync(
                $"{baseUrl}/Named_Snaps/{encoded}.png", cancellationToken, progress).ConfigureAwait(false);

            progress?.Report("Fetching title screen...");
            artwork.TitleScreen = await TryDownloadImageAsync(
                $"{baseUrl}/Named_Titles/{encoded}.png", cancellationToken, progress).ConfigureAwait(false);

            return artwork;
        }

        // Last resort: try fetching snaps/titles with the first candidate
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
    /// Searches the Libretro Thumbnails index for a name whose base title matches
    /// one of the candidate names. Returns the first matching index entry, or null.
    /// </summary>
    private static async Task<string?> FindMatchInIndexAsync(
        string systemName, List<string> candidates, CancellationToken cancellationToken)
    {
        var index = await FetchSystemIndexAsync(systemName, cancellationToken).ConfigureAwait(false);
        if (index == null || index.Count == 0)
            return null;

        // Build a set of base names from candidates (all parenthesized/bracketed tags stripped)
        var baseNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string candidate in candidates)
        {
            string stripped = AllParenthesizedTagRegex().Replace(candidate, "").TrimEnd();
            stripped = SquareBracketTagRegex().Replace(stripped, "").TrimEnd();
            if (!string.IsNullOrWhiteSpace(stripped))
                baseNames.Add(stripped);
        }

        // Search the index for entries whose base name matches one of ours.
        // The match requires the index entry to start with the base name followed
        // by a space-paren, paren (TOSEC-style), or end of string.
        foreach (string baseName in baseNames)
        {
            string? bestMatch = null;
            bool bestIsRevision = false;

            foreach (string entry in index)
            {
                if (!entry.StartsWith(baseName, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Ensure the match is a full title match: the character after the
                // base name must be ' (', '(' (TOSEC), or end of string.
                if (entry.Length > baseName.Length)
                {
                    string rest = entry[baseName.Length..];
                    if (!rest.StartsWith(" (", StringComparison.Ordinal) &&
                        !rest.StartsWith("(", StringComparison.Ordinal))
                        continue;
                }

                bool isRevision = entry.Contains("(Rev ", StringComparison.OrdinalIgnoreCase) ||
                                  entry.Contains("(Beta)", StringComparison.OrdinalIgnoreCase) ||
                                  entry.Contains("(Beta ", StringComparison.OrdinalIgnoreCase) ||
                                  entry.Contains("(Proto)", StringComparison.OrdinalIgnoreCase) ||
                                  entry.Contains("(Proto ", StringComparison.OrdinalIgnoreCase);

                // Prefer non-revision entries over revision entries
                if (bestMatch == null || (bestIsRevision && !isRevision))
                {
                    bestMatch = entry;
                    bestIsRevision = isRevision;
                }
            }

            if (bestMatch != null)
                return bestMatch;
        }

        return null;
    }

    /// <summary>
    /// Fetches the list of thumbnail names (boxarts) available for a system from
    /// the Libretro Thumbnails GitHub repository. Results are cached on disk for
    /// <see cref="IndexCacheTtl"/> to avoid repeated API calls.
    /// </summary>
    private static async Task<List<string>?> FetchSystemIndexAsync(
        string systemName, CancellationToken cancellationToken)
    {
        string cacheFile = Path.Combine(IndexCacheDirectory, $"{systemName}.txt");

        // Check cache first
        try
        {
            if (File.Exists(cacheFile))
            {
                var fileInfo = new FileInfo(cacheFile);
                if (DateTime.UtcNow - fileInfo.LastWriteTimeUtc < IndexCacheTtl)
                {
                    string[] lines = await File.ReadAllLinesAsync(cacheFile, cancellationToken)
                        .ConfigureAwait(false);
                    return [.. lines.Where(l => !string.IsNullOrEmpty(l))];
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* cache miss — fall through */ }

        // Fetch from GitHub Trees API
        try
        {
            string apiUrl = $"https://api.github.com/repos/libretro-thumbnails/" +
                $"{Uri.EscapeDataString(systemName)}/git/trees/master?recursive=1";

            using var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            request.Headers.Add("Accept", "application/vnd.github+json");

            using var response = await HttpClient.SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return null;

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var names = new List<string>();
            const string boxartPrefix = "Named_Boxarts/";
            const string pngSuffix = ".png";

            if (doc.RootElement.TryGetProperty("tree", out var tree))
            {
                foreach (var item in tree.EnumerateArray())
                {
                    if (!item.TryGetProperty("path", out var pathProp))
                        continue;
                    string? path = pathProp.GetString();
                    if (path == null ||
                        !path.StartsWith(boxartPrefix, StringComparison.Ordinal) ||
                        !path.EndsWith(pngSuffix, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string name = path[boxartPrefix.Length..^pngSuffix.Length];
                    if (!string.IsNullOrWhiteSpace(name))
                        names.Add(name);
                }
            }

            // Write to cache
            try
            {
                Directory.CreateDirectory(IndexCacheDirectory);
                await File.WriteAllLinesAsync(cacheFile, names, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* cache write failure is non-fatal */ }

            return names;
        }
        catch (HttpRequestException) { return null; }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is TaskCanceledException or JsonException) { return null; }
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
        // Check the local disk cache first
        string cacheKey = GetCacheKey(url);
        string cachePath = Path.Combine(CacheDirectory, cacheKey);

        try
        {
            if (File.Exists(cachePath))
            {
                byte[] cached = await File.ReadAllBytesAsync(cachePath, cancellationToken).ConfigureAwait(false);

                // A small file matching our marker indicates a cached 404
                if (cached.AsSpan().SequenceEqual(NegativeCacheMarkerBytes))
                {
                    return null;
                }

                return cached;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* cache miss — fall through to network */ }

        try
        {
            using var response = await HttpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                byte[] data = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

                // Write to cache (best-effort, don't fail the operation)
                WriteCacheFile(cachePath, data);

                return data;
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                // Negative-cache the 404 so we don't re-fetch on the next view
                WriteCacheFile(cachePath, NegativeCacheMarkerBytes);
            }
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

    /// <summary>
    /// Writes data to a cache file. Best-effort: failures are silently ignored.
    /// </summary>
    private static void WriteCacheFile(string cachePath, byte[] data)
    {
        try
        {
            Directory.CreateDirectory(CacheDirectory);
            File.WriteAllBytes(cachePath, data);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* cache write failure is non-fatal */ }
    }

    /// <summary>
    /// Generates a filesystem-safe cache key from a URL.
    /// Uses the SHA-256 hash of the URL to avoid path length and character issues.
    /// </summary>
    private static string GetCacheKey(string url)
    {
        byte[] hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(url));
        return Convert.ToHexString(hash) + ".png";
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
