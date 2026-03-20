using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace RetroMultiTools.Utilities.Mame;

/// <summary>
/// Loads, edits, and saves MAME DAT files in Logiqx XML format.
/// Supports adding, removing, and modifying game and ROM entries.
/// </summary>
public static class MameDatEditor
{
    /// <summary>
    /// Loads a DAT file and returns an editable structure.
    /// </summary>
    public static DatDocument LoadDat(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("DAT file not found.", filePath);

        XDocument doc;
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };
            using var reader = XmlReader.Create(filePath, settings);
            doc = XDocument.Load(reader);
        }
        catch (XmlException ex)
        {
            throw new InvalidOperationException($"Invalid DAT file: {ex.Message}", ex);
        }

        var root = doc.Root
            ?? throw new InvalidOperationException("Invalid DAT file: no root element.");

        var datDoc = new DatDocument { FilePath = filePath };

        // Parse header
        var header = root.Element("header");
        if (header != null)
        {
            datDoc.Header = new DatHeader
            {
                Name = header.Element("name")?.Value ?? "",
                Description = header.Element("description")?.Value ?? "",
                Version = header.Element("version")?.Value ?? "",
                Author = header.Element("author")?.Value ?? "",
                Homepage = header.Element("homepage")?.Value ?? "",
                Url = header.Element("url")?.Value ?? "",
                Comment = header.Element("comment")?.Value ?? "",
                Date = header.Element("date")?.Value ?? ""
            };
        }

        // Parse games/machines
        var games = root.Elements("game").Concat(root.Elements("machine"));
        foreach (var game in games)
        {
            var gameEntry = new DatGameEntry
            {
                Name = game.Attribute("name")?.Value ?? "",
                Description = game.Element("description")?.Value ?? "",
                Year = game.Element("year")?.Value ?? "",
                Manufacturer = game.Element("manufacturer")?.Value ?? "",
                CloneOf = game.Attribute("cloneof")?.Value ?? "",
                RomOf = game.Attribute("romof")?.Value ?? "",
                SampleOf = game.Attribute("sampleof")?.Value ?? ""
            };

            foreach (var rom in game.Elements("rom"))
            {
                gameEntry.Roms.Add(new DatRomEntry
                {
                    Name = rom.Attribute("name")?.Value ?? "",
                    Size = long.TryParse(rom.Attribute("size")?.Value, out long size) ? size : 0,
                    CRC = rom.Attribute("crc")?.Value ?? "",
                    MD5 = rom.Attribute("md5")?.Value ?? "",
                    SHA1 = rom.Attribute("sha1")?.Value ?? "",
                    Status = rom.Attribute("status")?.Value ?? ""
                });
            }

            foreach (var disk in game.Elements("disk"))
            {
                gameEntry.Disks.Add(new DatDiskEntry
                {
                    Name = disk.Attribute("name")?.Value ?? "",
                    SHA1 = disk.Attribute("sha1")?.Value ?? "",
                    MD5 = disk.Attribute("md5")?.Value ?? "",
                    Status = disk.Attribute("status")?.Value ?? ""
                });
            }

            foreach (var sample in game.Elements("sample"))
            {
                gameEntry.Samples.Add(sample.Attribute("name")?.Value ?? "");
            }

            datDoc.Games.Add(gameEntry);
        }

        return datDoc;
    }

    /// <summary>
    /// Saves a DAT document to a file in Logiqx XML format.
    /// </summary>
    public static void SaveDat(DatDocument datDoc, string outputPath)
    {
        var datafile = new XElement("datafile");

        // Build header
        var headerElement = new XElement("header");
        if (!string.IsNullOrEmpty(datDoc.Header.Name))
            headerElement.Add(new XElement("name", datDoc.Header.Name));
        if (!string.IsNullOrEmpty(datDoc.Header.Description))
            headerElement.Add(new XElement("description", datDoc.Header.Description));
        if (!string.IsNullOrEmpty(datDoc.Header.Version))
            headerElement.Add(new XElement("version", datDoc.Header.Version));
        if (!string.IsNullOrEmpty(datDoc.Header.Author))
            headerElement.Add(new XElement("author", datDoc.Header.Author));
        if (!string.IsNullOrEmpty(datDoc.Header.Homepage))
            headerElement.Add(new XElement("homepage", datDoc.Header.Homepage));
        if (!string.IsNullOrEmpty(datDoc.Header.Url))
            headerElement.Add(new XElement("url", datDoc.Header.Url));
        if (!string.IsNullOrEmpty(datDoc.Header.Comment))
            headerElement.Add(new XElement("comment", datDoc.Header.Comment));
        if (!string.IsNullOrEmpty(datDoc.Header.Date))
            headerElement.Add(new XElement("date", datDoc.Header.Date));
        datafile.Add(headerElement);

        // Build game entries
        foreach (var game in datDoc.Games.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase))
        {
            var gameElement = new XElement("game",
                new XAttribute("name", game.Name));

            if (!string.IsNullOrEmpty(game.CloneOf))
                gameElement.Add(new XAttribute("cloneof", game.CloneOf));
            if (!string.IsNullOrEmpty(game.RomOf))
                gameElement.Add(new XAttribute("romof", game.RomOf));
            if (!string.IsNullOrEmpty(game.SampleOf))
                gameElement.Add(new XAttribute("sampleof", game.SampleOf));

            if (!string.IsNullOrEmpty(game.Description))
                gameElement.Add(new XElement("description", game.Description));
            if (!string.IsNullOrEmpty(game.Year))
                gameElement.Add(new XElement("year", game.Year));
            if (!string.IsNullOrEmpty(game.Manufacturer))
                gameElement.Add(new XElement("manufacturer", game.Manufacturer));

            foreach (var rom in game.Roms)
            {
                var romElement = new XElement("rom",
                    new XAttribute("name", rom.Name),
                    new XAttribute("size", rom.Size));

                if (!string.IsNullOrEmpty(rom.CRC))
                    romElement.Add(new XAttribute("crc", rom.CRC.ToLowerInvariant()));
                if (!string.IsNullOrEmpty(rom.MD5))
                    romElement.Add(new XAttribute("md5", rom.MD5.ToLowerInvariant()));
                if (!string.IsNullOrEmpty(rom.SHA1))
                    romElement.Add(new XAttribute("sha1", rom.SHA1.ToLowerInvariant()));
                if (!string.IsNullOrEmpty(rom.Status))
                    romElement.Add(new XAttribute("status", rom.Status));

                gameElement.Add(romElement);
            }

            foreach (var disk in game.Disks)
            {
                var diskElement = new XElement("disk",
                    new XAttribute("name", disk.Name));

                if (!string.IsNullOrEmpty(disk.SHA1))
                    diskElement.Add(new XAttribute("sha1", disk.SHA1.ToLowerInvariant()));
                if (!string.IsNullOrEmpty(disk.MD5))
                    diskElement.Add(new XAttribute("md5", disk.MD5.ToLowerInvariant()));
                if (!string.IsNullOrEmpty(disk.Status))
                    diskElement.Add(new XAttribute("status", disk.Status));

                gameElement.Add(diskElement);
            }

            foreach (string sample in game.Samples)
            {
                gameElement.Add(new XElement("sample", new XAttribute("name", sample)));
            }

            datafile.Add(gameElement);
        }

        var xmlDoc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XDocumentType("datafile",
                "-//Logiqx//DTD ROM Management Datafile//EN",
                "http://www.logiqx.com/Dats/datafile.dtd",
                null),
            datafile);

        try
        {
            // Write to a temporary file first to avoid corrupting the original on failure
            string tempPath = outputPath + ".tmp";
            using (var writer = new StreamWriter(tempPath, false, new UTF8Encoding(false)))
            {
                xmlDoc.Save(writer);
            }

            // Atomic-ish replace: delete original, rename temp to final
            File.Move(tempPath, outputPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Clean up the temp file if it exists, but never delete the original
            string tempPath = outputPath + ".tmp";
            try { File.Delete(tempPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw;
        }
    }

    /// <summary>
    /// Adds a new empty game entry to the document.
    /// </summary>
    public static DatGameEntry AddGame(DatDocument datDoc, string name, string description = "")
    {
        var game = new DatGameEntry
        {
            Name = name,
            Description = string.IsNullOrEmpty(description) ? name : description
        };
        datDoc.Games.Add(game);
        return game;
    }

    /// <summary>
    /// Removes a game entry from the document.
    /// </summary>
    public static bool RemoveGame(DatDocument datDoc, string gameName)
    {
        return datDoc.Games.RemoveAll(g =>
            g.Name.Equals(gameName, StringComparison.OrdinalIgnoreCase)) > 0;
    }

    /// <summary>
    /// Adds a ROM entry to a game.
    /// </summary>
    public static DatRomEntry AddRom(DatGameEntry game, string name, long size, string crc = "", string sha1 = "", string md5 = "")
    {
        var rom = new DatRomEntry
        {
            Name = name,
            Size = size,
            CRC = crc,
            SHA1 = sha1,
            MD5 = md5
        };
        game.Roms.Add(rom);
        return rom;
    }

    /// <summary>
    /// Removes a ROM entry from a game.
    /// </summary>
    public static bool RemoveRom(DatGameEntry game, string romName)
    {
        return game.Roms.RemoveAll(r =>
            r.Name.Equals(romName, StringComparison.OrdinalIgnoreCase)) > 0;
    }

    /// <summary>
    /// Returns summary statistics for a DAT document.
    /// </summary>
    public static DatDocumentStats GetStats(DatDocument datDoc)
    {
        int totalRoms = datDoc.Games.Sum(g => g.Roms.Count);
        int totalDisks = datDoc.Games.Sum(g => g.Disks.Count);
        int totalSamples = datDoc.Games.Sum(g => g.Samples.Count);
        int clonesCount = datDoc.Games.Count(g => !string.IsNullOrEmpty(g.CloneOf));

        return new DatDocumentStats
        {
            TotalGames = datDoc.Games.Count,
            TotalRoms = totalRoms,
            TotalDisks = totalDisks,
            TotalSamples = totalSamples,
            ClonesCount = clonesCount,
            ParentsCount = datDoc.Games.Count - clonesCount
        };
    }

    /// <summary>
    /// Searches games by name (case-insensitive partial match).
    /// </summary>
    public static List<DatGameEntry> SearchGames(DatDocument datDoc, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return datDoc.Games;

        return datDoc.Games
            .Where(g => g.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        g.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}

public class DatDocument
{
    public string FilePath { get; set; } = string.Empty;
    public DatHeader Header { get; set; } = new();
    public List<DatGameEntry> Games { get; set; } = [];
}

public class DatHeader
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Homepage { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
}

public class DatGameEntry
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Year { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string CloneOf { get; set; } = string.Empty;
    public string RomOf { get; set; } = string.Empty;
    public string SampleOf { get; set; } = string.Empty;
    public List<DatRomEntry> Roms { get; set; } = [];
    public List<DatDiskEntry> Disks { get; set; } = [];
    public List<string> Samples { get; set; } = [];
}

public class DatRomEntry
{
    public string Name { get; set; } = string.Empty;
    public long Size { get; set; }
    public string CRC { get; set; } = string.Empty;
    public string MD5 { get; set; } = string.Empty;
    public string SHA1 { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class DatDiskEntry
{
    public string Name { get; set; } = string.Empty;
    public string SHA1 { get; set; } = string.Empty;
    public string MD5 { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class DatDocumentStats
{
    public int TotalGames { get; set; }
    public int TotalRoms { get; set; }
    public int TotalDisks { get; set; }
    public int TotalSamples { get; set; }
    public int ClonesCount { get; set; }
    public int ParentsCount { get; set; }

    public string Summary =>
        $"{TotalGames} games ({ParentsCount} parents, {ClonesCount} clones), " +
        $"{TotalRoms} ROMs, {TotalDisks} disks, {TotalSamples} samples";
}
