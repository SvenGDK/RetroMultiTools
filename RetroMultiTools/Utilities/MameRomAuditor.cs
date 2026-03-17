using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace RetroMultiTools.Utilities;

/// <summary>
/// Audits MAME ROM sets against a MAME XML database.
/// Verifies ZIP-packaged ROM sets for completeness, correct checksums, and size.
/// </summary>
public static class MameRomAuditor
{
    /// <summary>
    /// Loads a MAME XML database (from mame -listxml output or a Logiqx-format DAT)
    /// and returns the list of machine entries with their required ROMs.
    /// </summary>
    public static List<MameMachine> LoadMameXml(string xmlPath)
    {
        if (!File.Exists(xmlPath))
            throw new FileNotFoundException("MAME XML file not found.", xmlPath);

        XDocument doc;
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                XmlResolver = null,
                MaxCharactersFromEntities = 0
            };
            using var reader = XmlReader.Create(xmlPath, settings);
            doc = XDocument.Load(reader);
        }
        catch (XmlException ex)
        {
            throw new InvalidOperationException($"Invalid MAME XML file: {ex.Message}", ex);
        }

        var root = doc.Root;
        if (root == null)
            throw new InvalidOperationException("Invalid MAME XML file: no root element.");

        var machines = new List<MameMachine>();

        // Support both <machine> (MAME listxml) and <game> (Logiqx DAT) elements
        var elements = root.Elements("machine").Concat(root.Elements("game"));

        foreach (var elem in elements)
        {
            string name = elem.Attribute("name")?.Value ?? "";
            if (string.IsNullOrEmpty(name)) continue;

            var machine = new MameMachine
            {
                Name = name,
                Description = elem.Element("description")?.Value ?? name,
                CloneOf = elem.Attribute("cloneof")?.Value ?? "",
                RomOf = elem.Attribute("romof")?.Value ?? "",
                IsBios = string.Equals(elem.Attribute("isbios")?.Value, "yes", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(elem.Attribute("isdevice")?.Value, "yes", StringComparison.OrdinalIgnoreCase)
            };

            foreach (var rom in elem.Elements("rom"))
            {
                string status = rom.Attribute("status")?.Value ?? "good";
                string merge = rom.Attribute("merge")?.Value ?? "";

                var romEntry = new MameRom
                {
                    Name = rom.Attribute("name")?.Value ?? "",
                    Size = long.TryParse(rom.Attribute("size")?.Value, out long size) ? size : 0,
                    CRC32 = rom.Attribute("crc")?.Value?.ToUpperInvariant() ?? "",
                    SHA1 = rom.Attribute("sha1")?.Value?.ToUpperInvariant() ?? "",
                    Status = status,
                    Merge = merge,
                    Optional = string.Equals(rom.Attribute("optional")?.Value, "yes", StringComparison.OrdinalIgnoreCase)
                };

                if (!string.IsNullOrEmpty(romEntry.Name))
                    machine.Roms.Add(romEntry);
            }

            foreach (var disk in elem.Elements("disk"))
            {
                var diskEntry = new MameDisk
                {
                    Name = disk.Attribute("name")?.Value ?? "",
                    SHA1 = disk.Attribute("sha1")?.Value?.ToUpperInvariant() ?? "",
                    Merge = disk.Attribute("merge")?.Value ?? "",
                    Status = disk.Attribute("status")?.Value ?? "good"
                };

                if (!string.IsNullOrEmpty(diskEntry.Name))
                    machine.Disks.Add(diskEntry);
            }

            // Only include machines that have ROMs or disks
            if (machine.Roms.Count > 0 || machine.Disks.Count > 0)
                machines.Add(machine);
        }

        return machines;
    }

    /// <summary>
    /// Audits a ROM directory against the loaded MAME database.
    /// Scans for ZIP files and verifies contents against expected ROMs.
    /// </summary>
    public static async Task<MameAuditResult> AuditDirectoryAsync(
        string romDirectory,
        List<MameMachine> machines,
        IProgress<string>? progress = null,
        bool searchRecursively = false)
    {
        if (!Directory.Exists(romDirectory))
            throw new DirectoryNotFoundException($"ROM directory not found: {romDirectory}");

        var machinesByName = new Dictionary<string, MameMachine>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in machines)
            machinesByName[m.Name] = m;

        var searchOption = searchRecursively ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var zipFiles = Directory.EnumerateFiles(romDirectory, "*.zip", searchOption)
            .ToList();

        var results = new List<MachineAuditResult>();
        int goodCount = 0, badCount = 0, incompleteCount = 0;

        for (int i = 0; i < zipFiles.Count; i++)
        {
            string zipFile = zipFiles[i];
            string machineName = Path.GetFileNameWithoutExtension(zipFile);

            progress?.Report($"Auditing {i + 1} of {zipFiles.Count}: {machineName}");

            if (!machinesByName.TryGetValue(machineName, out var machine))
            {
                results.Add(new MachineAuditResult
                {
                    MachineName = machineName,
                    Description = machineName,
                    Status = MachineStatus.Unknown,
                    StatusDetail = "Not found in MAME database"
                });
                continue;
            }

            var result = await Task.Run(() => AuditZipFile(zipFile, machine)).ConfigureAwait(false);
            results.Add(result);

            switch (result.Status)
            {
                case MachineStatus.Good: goodCount++; break;
                case MachineStatus.Bad: badCount++; break;
                case MachineStatus.Incomplete: incompleteCount++; break;
            }
        }

        // Find machines in the database that have no corresponding ZIP
        var auditedNames = new HashSet<string>(
            zipFiles.Select(z => Path.GetFileNameWithoutExtension(z)),
            StringComparer.OrdinalIgnoreCase);

        var missingMachines = new List<string>();
        foreach (var machine in machines)
        {
            if (machine.IsBios) continue;
            if (!auditedNames.Contains(machine.Name) && machine.Roms.Any(r => string.IsNullOrEmpty(r.Merge)))
            {
                // Only count as missing if it has non-merged ROMs (i.e., not purely inheriting)
                missingMachines.Add(machine.Name);
            }
        }

        progress?.Report($"Done — {goodCount} good, {incompleteCount} incomplete, {badCount} bad, {missingMachines.Count} missing.");

        return new MameAuditResult
        {
            Results = results,
            TotalZips = zipFiles.Count,
            TotalMachines = machines.Count,
            GoodCount = goodCount,
            BadCount = badCount,
            IncompleteCount = incompleteCount,
            MissingMachines = missingMachines
        };
    }

    private static MachineAuditResult AuditZipFile(string zipPath, MameMachine machine)
    {
        var result = new MachineAuditResult
        {
            MachineName = machine.Name,
            Description = machine.Description,
            IsClone = !string.IsNullOrEmpty(machine.CloneOf),
            ParentName = machine.CloneOf
        };

        // Get the non-merged ROMs that should be in this ZIP
        var requiredRoms = machine.Roms
            .Where(r => string.IsNullOrEmpty(r.Merge) && r.Status != "nodump" && !r.Optional)
            .ToList();

        if (requiredRoms.Count == 0)
        {
            result.Status = MachineStatus.Good;
            result.StatusDetail = "No non-merged ROMs required";
            return result;
        }

        Dictionary<string, ZipArchiveEntry> zipEntries;
        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            zipEntries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in archive.Entries)
                zipEntries.TryAdd(entry.FullName, entry);

            int found = 0;
            int correct = 0;
            var issues = new List<string>();

            foreach (var rom in requiredRoms)
            {
                if (!zipEntries.TryGetValue(rom.Name, out var entry))
                {
                    issues.Add($"Missing: {rom.Name}");
                    result.RomResults.Add(new RomAuditResult
                    {
                        RomName = rom.Name,
                        ExpectedCRC32 = rom.CRC32,
                        ExpectedSize = rom.Size,
                        Status = RomStatus.Missing
                    });
                    continue;
                }

                found++;

                var romResult = new RomAuditResult
                {
                    RomName = rom.Name,
                    ExpectedCRC32 = rom.CRC32,
                    ExpectedSize = rom.Size,
                    ActualSize = entry.Length
                };

                // Verify CRC32 - ZipArchiveEntry.Crc32 provides the stored CRC
                string actualCrc = entry.Crc32.ToString("X8");
                romResult.ActualCRC32 = actualCrc;

                if (rom.Size > 0 && entry.Length != rom.Size)
                {
                    romResult.Status = RomStatus.BadSize;
                    issues.Add($"Bad size: {rom.Name} (expected {rom.Size}, got {entry.Length})");
                }
                else if (!string.IsNullOrEmpty(rom.CRC32) && !rom.CRC32.Equals(actualCrc, StringComparison.OrdinalIgnoreCase))
                {
                    romResult.Status = RomStatus.BadChecksum;
                    issues.Add($"Bad CRC: {rom.Name} (expected {rom.CRC32}, got {actualCrc})");
                }
                else
                {
                    romResult.Status = RomStatus.Good;
                    correct++;
                }

                result.RomResults.Add(romResult);
            }

            if (correct == requiredRoms.Count)
            {
                result.Status = MachineStatus.Good;
                result.StatusDetail = $"All {requiredRoms.Count} ROMs verified";
            }
            else if (found == 0)
            {
                result.Status = MachineStatus.Bad;
                result.StatusDetail = $"No required ROMs found (need {requiredRoms.Count})";
            }
            else if (found < requiredRoms.Count)
            {
                result.Status = MachineStatus.Incomplete;
                result.StatusDetail = $"{found} of {requiredRoms.Count} ROMs present, {issues.Count} issues";
            }
            else
            {
                result.Status = MachineStatus.Bad;
                result.StatusDetail = $"{correct} of {requiredRoms.Count} ROMs correct, {issues.Count} issues";
            }

            result.Issues = issues;
        }
        catch (InvalidDataException)
        {
            result.Status = MachineStatus.Bad;
            result.StatusDetail = "Corrupt or invalid ZIP file";
        }
        catch (IOException ex)
        {
            result.Status = MachineStatus.Bad;
            result.StatusDetail = $"Read error: {ex.Message}";
        }

        return result;
    }
}

public class MameMachine
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CloneOf { get; set; } = string.Empty;
    public string RomOf { get; set; } = string.Empty;
    public bool IsBios { get; set; }
    public List<MameRom> Roms { get; set; } = [];
    public List<MameDisk> Disks { get; set; } = [];
}

public class MameRom
{
    public string Name { get; set; } = string.Empty;
    public long Size { get; set; }
    public string CRC32 { get; set; } = string.Empty;
    public string SHA1 { get; set; } = string.Empty;
    public string Status { get; set; } = "good";
    public string Merge { get; set; } = string.Empty;
    public bool Optional { get; set; }
}

public class MameDisk
{
    public string Name { get; set; } = string.Empty;
    public string SHA1 { get; set; } = string.Empty;
    public string Merge { get; set; } = string.Empty;
    public string Status { get; set; } = "good";
}

public enum MachineStatus
{
    Good,
    Incomplete,
    Bad,
    Unknown
}

public enum RomStatus
{
    Good,
    Missing,
    BadChecksum,
    BadSize
}

public class MachineAuditResult
{
    public string MachineName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public MachineStatus Status { get; set; }
    public string StatusDetail { get; set; } = string.Empty;
    public bool IsClone { get; set; }
    public string ParentName { get; set; } = string.Empty;
    public List<RomAuditResult> RomResults { get; set; } = [];
    public List<string> Issues { get; set; } = [];
}

public class RomAuditResult
{
    public string RomName { get; set; } = string.Empty;
    public RomStatus Status { get; set; }
    public string ExpectedCRC32 { get; set; } = string.Empty;
    public string ActualCRC32 { get; set; } = string.Empty;
    public long ExpectedSize { get; set; }
    public long ActualSize { get; set; }
}

public class MameAuditResult
{
    public List<MachineAuditResult> Results { get; set; } = [];
    public int TotalZips { get; set; }
    public int TotalMachines { get; set; }
    public int GoodCount { get; set; }
    public int BadCount { get; set; }
    public int IncompleteCount { get; set; }
    public List<string> MissingMachines { get; set; } = [];

    public string Summary =>
        $"{GoodCount} good, {IncompleteCount} incomplete, {BadCount} bad out of {TotalZips} ROM sets. " +
        $"{MissingMachines.Count} machines missing from directory.";
}
