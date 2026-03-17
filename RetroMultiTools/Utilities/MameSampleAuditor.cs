using System.IO.Compression;

namespace RetroMultiTools.Utilities;

/// <summary>
/// Audits MAME sample audio files against a MAME XML database.
/// Verifies that sample ZIPs contain the expected WAV files for each machine.
/// Similar to CLRMamePro's Sample Auditor.
/// </summary>
public static class MameSampleAuditor
{
    /// <summary>
    /// Loads sample requirements from a MAME XML database.
    /// Extracts the &lt;sample&gt; elements from each machine definition.
    /// </summary>
    public static List<MameSampleSet> LoadSampleRequirements(string xmlPath)
    {
        if (!File.Exists(xmlPath))
            throw new FileNotFoundException("MAME XML file not found.", xmlPath);

        System.Xml.Linq.XDocument doc;
        try
        {
            var settings = new System.Xml.XmlReaderSettings
            {
                DtdProcessing = System.Xml.DtdProcessing.Ignore,
                XmlResolver = null,
                MaxCharactersFromEntities = 0
            };
            using var reader = System.Xml.XmlReader.Create(xmlPath, settings);
            doc = System.Xml.Linq.XDocument.Load(reader);
        }
        catch (System.Xml.XmlException ex)
        {
            throw new InvalidOperationException($"Invalid MAME XML file: {ex.Message}", ex);
        }

        var root = doc.Root;
        if (root == null)
            throw new InvalidOperationException("Invalid MAME XML file: no root element.");

        var sampleSets = new List<MameSampleSet>();

        var elements = root.Elements("machine").Concat(root.Elements("game"));

        foreach (var elem in elements)
        {
            string name = elem.Attribute("name")?.Value ?? "";
            if (string.IsNullOrEmpty(name)) continue;

            string sampleOf = elem.Attribute("sampleof")?.Value ?? "";
            var samples = elem.Elements("sample").ToList();
            if (samples.Count == 0) continue;

            var sampleSet = new MameSampleSet
            {
                MachineName = name,
                Description = elem.Element("description")?.Value ?? name,
                SampleOf = sampleOf
            };

            foreach (var sample in samples)
            {
                string sampleName = sample.Attribute("name")?.Value ?? "";
                if (!string.IsNullOrEmpty(sampleName))
                    sampleSet.RequiredSamples.Add(sampleName);
            }

            if (sampleSet.RequiredSamples.Count > 0)
                sampleSets.Add(sampleSet);
        }

        return sampleSets;
    }

    /// <summary>
    /// Audits a sample directory against loaded sample requirements.
    /// Scans for ZIP files and verifies they contain the expected WAV samples.
    /// </summary>
    public static async Task<SampleAuditResult> AuditDirectoryAsync(
        string sampleDirectory,
        List<MameSampleSet> sampleSets,
        IProgress<string>? progress = null,
        bool searchRecursively = false)
    {
        if (!Directory.Exists(sampleDirectory))
            throw new DirectoryNotFoundException($"Sample directory not found: {sampleDirectory}");

        var setsByName = new Dictionary<string, MameSampleSet>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in sampleSets)
            setsByName[s.MachineName] = s;

        // Also index by sampleof for shared sample sets
        var sampleOfSets = new Dictionary<string, MameSampleSet>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in sampleSets.Where(s => !string.IsNullOrEmpty(s.SampleOf)))
        {
            sampleOfSets.TryAdd(s.SampleOf, s);
        }

        var searchOption = searchRecursively ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var zipFiles = Directory.EnumerateFiles(sampleDirectory, "*.zip", searchOption).ToList();

        var results = new List<SampleSetAuditResult>();
        int goodCount = 0, badCount = 0, incompleteCount = 0;

        for (int i = 0; i < zipFiles.Count; i++)
        {
            string zipFile = zipFiles[i];
            string setName = Path.GetFileNameWithoutExtension(zipFile);

            progress?.Report($"Auditing {i + 1} of {zipFiles.Count}: {setName}");

            // Find which sample set this ZIP belongs to
            MameSampleSet? sampleSet = null;
            if (setsByName.TryGetValue(setName, out var directMatch))
                sampleSet = directMatch;
            else if (sampleOfSets.TryGetValue(setName, out var sharedMatch))
                sampleSet = sharedMatch;

            if (sampleSet == null)
            {
                results.Add(new SampleSetAuditResult
                {
                    SetName = setName,
                    Status = SampleSetStatus.Unknown,
                    StatusDetail = "Not found in MAME database"
                });
                continue;
            }

            var result = await Task.Run(() => AuditSampleZip(zipFile, sampleSet)).ConfigureAwait(false);
            results.Add(result);

            switch (result.Status)
            {
                case SampleSetStatus.Good: goodCount++; break;
                case SampleSetStatus.Bad: badCount++; break;
                case SampleSetStatus.Incomplete: incompleteCount++; break;
            }
        }

        // Find missing sample sets
        var existingZips = new HashSet<string>(
            zipFiles.Select(z => Path.GetFileNameWithoutExtension(z)),
            StringComparer.OrdinalIgnoreCase);

        var missingSets = sampleSets
            .Where(s => string.IsNullOrEmpty(s.SampleOf) && !existingZips.Contains(s.MachineName))
            .Select(s => s.MachineName)
            .ToList();

        progress?.Report($"Done — {goodCount} good, {incompleteCount} incomplete, {badCount} bad, {missingSets.Count} missing.");

        return new SampleAuditResult
        {
            Results = results,
            TotalZips = zipFiles.Count,
            TotalSampleSets = sampleSets.Count,
            GoodCount = goodCount,
            BadCount = badCount,
            IncompleteCount = incompleteCount,
            MissingSets = missingSets
        };
    }

    private static SampleSetAuditResult AuditSampleZip(string zipPath, MameSampleSet sampleSet)
    {
        var result = new SampleSetAuditResult
        {
            SetName = sampleSet.MachineName,
            Description = sampleSet.Description
        };

        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            var zipEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in archive.Entries)
            {
                // Strip directory component and .wav extension for matching
                string entryName = Path.GetFileNameWithoutExtension(entry.FullName);
                zipEntries.Add(entryName);
                // Also add with extension
                zipEntries.Add(entry.FullName);
                zipEntries.Add(Path.GetFileName(entry.FullName));
            }

            int found = 0;
            var missing = new List<string>();
            var present = new List<string>();

            foreach (string sample in sampleSet.RequiredSamples)
            {
                // Match sample name as-is, or with .wav extension
                if (zipEntries.Contains(sample) ||
                    zipEntries.Contains(sample + ".wav") ||
                    zipEntries.Contains(sample + ".WAV"))
                {
                    found++;
                    present.Add(sample);
                }
                else
                {
                    missing.Add(sample);
                }
            }

            result.PresentSamples = present;
            result.MissingSamples = missing;
            result.TotalRequired = sampleSet.RequiredSamples.Count;
            result.TotalFound = found;

            if (found == sampleSet.RequiredSamples.Count)
            {
                result.Status = SampleSetStatus.Good;
                result.StatusDetail = $"All {found} samples present";
            }
            else if (found == 0)
            {
                result.Status = SampleSetStatus.Bad;
                result.StatusDetail = $"No required samples found (need {sampleSet.RequiredSamples.Count})";
            }
            else
            {
                result.Status = SampleSetStatus.Incomplete;
                result.StatusDetail = $"{found} of {sampleSet.RequiredSamples.Count} samples present, {missing.Count} missing";
            }
        }
        catch (InvalidDataException)
        {
            result.Status = SampleSetStatus.Bad;
            result.StatusDetail = "Corrupt or invalid ZIP file";
        }
        catch (IOException ex)
        {
            result.Status = SampleSetStatus.Bad;
            result.StatusDetail = $"Read error: {ex.Message}";
        }

        return result;
    }
}

public class MameSampleSet
{
    public string MachineName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SampleOf { get; set; } = string.Empty;
    public List<string> RequiredSamples { get; set; } = [];
}

public enum SampleSetStatus
{
    Good,
    Incomplete,
    Bad,
    Unknown
}

public class SampleSetAuditResult
{
    public string SetName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public SampleSetStatus Status { get; set; }
    public string StatusDetail { get; set; } = string.Empty;
    public int TotalRequired { get; set; }
    public int TotalFound { get; set; }
    public List<string> PresentSamples { get; set; } = [];
    public List<string> MissingSamples { get; set; } = [];
}

public class SampleAuditResult
{
    public List<SampleSetAuditResult> Results { get; set; } = [];
    public int TotalZips { get; set; }
    public int TotalSampleSets { get; set; }
    public int GoodCount { get; set; }
    public int BadCount { get; set; }
    public int IncompleteCount { get; set; }
    public List<string> MissingSets { get; set; } = [];

    public string Summary =>
        $"{GoodCount} good, {IncompleteCount} incomplete, {BadCount} bad out of {TotalZips} sample sets. " +
        $"{MissingSets.Count} sets missing from directory.";
}
