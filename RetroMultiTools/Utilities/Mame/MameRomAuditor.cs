using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using RetroMultiTools.Localization;

namespace RetroMultiTools.Utilities.Mame;

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
        bool searchRecursively = false,
        CancellationToken cancellationToken = default)
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
            cancellationToken.ThrowIfCancellationRequested();

            string zipFile = zipFiles[i];
            string machineName = Path.GetFileNameWithoutExtension(zipFile);

            progress?.Report(string.Format(LocalizationManager.Instance["MameAuditor_ProgressAuditing"], i + 1, zipFiles.Count, machineName));

            if (!machinesByName.TryGetValue(machineName, out var machine))
            {
                results.Add(new MachineAuditResult
                {
                    MachineName = machineName,
                    Description = machineName,
                    Status = MachineStatus.Unknown,
                    StatusDetail = LocalizationManager.Instance["MameAudit_NotInDatabase"]
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

        // Also verify CHD disk files for machines that require them.
        // CHD files are typically stored in subdirectories named after the machine.
        foreach (var auditResult in results)
        {
            if (!machinesByName.TryGetValue(auditResult.MachineName, out var machine))
                continue;

            var requiredDisks = machine.Disks
                .Where(d => string.IsNullOrEmpty(d.Merge) && d.Status != "nodump")
                .ToList();

            if (requiredDisks.Count == 0) continue;

            string machineDir = Path.Combine(romDirectory, machine.Name);
            if (!Directory.Exists(machineDir)) continue;

            foreach (var disk in requiredDisks)
            {
                string chdPath = Path.Combine(machineDir, disk.Name + ".chd");
                if (!File.Exists(chdPath))
                {
                    auditResult.Issues.Add(string.Format(LocalizationManager.Instance["MameAudit_MissingDisk"], disk.Name));
                    if (auditResult.Status == MachineStatus.Good)
                        auditResult.Status = MachineStatus.Incomplete;
                }
                else if (!string.IsNullOrEmpty(disk.SHA1))
                {
                    // Verify CHD SHA-1 against database by reading the header
                    try
                    {
                        var chdResult = await MameChdVerifier.VerifyAsync(chdPath).ConfigureAwait(false);
                        if (chdResult.IsValid && !string.IsNullOrEmpty(chdResult.SHA1) &&
                            !chdResult.SHA1.Equals(disk.SHA1, StringComparison.OrdinalIgnoreCase))
                        {
                            auditResult.Issues.Add(string.Format(LocalizationManager.Instance["MameAudit_BadDiskSha1"], disk.Name, disk.SHA1, chdResult.SHA1));
                            if (auditResult.Status == MachineStatus.Good)
                                auditResult.Status = MachineStatus.Bad;
                        }
                        else if (!chdResult.IsValid)
                        {
                            auditResult.Issues.Add(string.Format(LocalizationManager.Instance["MameAudit_InvalidDisk"], disk.Name, chdResult.Error));
                            if (auditResult.Status == MachineStatus.Good)
                                auditResult.Status = MachineStatus.Bad;
                        }
                    }
                    catch (IOException)
                    {
                        // Could not read CHD - report but don't fail the whole audit
                        auditResult.Issues.Add(string.Format(LocalizationManager.Instance["MameAudit_DiskReadError"], disk.Name));
                    }
                }
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

        progress?.Report(string.Format(LocalizationManager.Instance["MameAuditor_ProgressDone"], goodCount, incompleteCount, badCount, missingMachines.Count));

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
            result.StatusDetail = LocalizationManager.Instance["MameAudit_NoNonMerged"];
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
                    issues.Add(string.Format(LocalizationManager.Instance["MameAudit_Missing"], rom.Name));
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
                    issues.Add(string.Format(LocalizationManager.Instance["MameAudit_BadSize"], rom.Name, rom.Size, entry.Length));
                }
                else if (!string.IsNullOrEmpty(rom.CRC32) && !rom.CRC32.Equals(actualCrc, StringComparison.OrdinalIgnoreCase))
                {
                    romResult.Status = RomStatus.BadChecksum;
                    issues.Add(string.Format(LocalizationManager.Instance["MameAudit_BadCRC"], rom.Name, rom.CRC32, actualCrc));
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
                result.StatusDetail = string.Format(LocalizationManager.Instance["MameAudit_AllVerified"], requiredRoms.Count);
            }
            else if (found == 0)
            {
                result.Status = MachineStatus.Bad;
                result.StatusDetail = string.Format(LocalizationManager.Instance["MameAudit_NoRequired"], requiredRoms.Count);
            }
            else if (found < requiredRoms.Count)
            {
                result.Status = MachineStatus.Incomplete;
                result.StatusDetail = string.Format(LocalizationManager.Instance["MameAudit_RomsPresent"], found, requiredRoms.Count, issues.Count);
            }
            else
            {
                result.Status = MachineStatus.Bad;
                result.StatusDetail = string.Format(LocalizationManager.Instance["MameAudit_RomsCorrect"], correct, requiredRoms.Count, issues.Count);
            }

            result.Issues = issues;
        }
        catch (InvalidDataException)
        {
            result.Status = MachineStatus.Bad;
            result.StatusDetail = LocalizationManager.Instance["MameAudit_CorruptZip"];
        }
        catch (IOException ex)
        {
            result.Status = MachineStatus.Bad;
            result.StatusDetail = string.Format(LocalizationManager.Instance["MameAudit_ReadError"], ex.Message);
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
        string.Format(LocalizationManager.Instance["MameAuditor_SummaryFormat"], GoodCount, IncompleteCount, BadCount, TotalZips, MissingMachines.Count);
}
