using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace RetroMultiTools.Utilities;

/// <summary>
/// Creates DAT files from a directory of ROM files by scanning files, computing checksums,
/// and outputting Logiqx XML format. Similar to CLRMamePro's Dir2Dat functionality.
/// </summary>
public static class MameDir2Dat
{
    private static readonly byte[] ChdMagic = "MComprHD"u8.ToArray();
    /// <summary>
    /// Scans a ROM directory and creates a DAT file in Logiqx XML format.
    /// </summary>
    public static async Task<Dir2DatResult> CreateDatAsync(
        string romDirectory,
        Dir2DatOptions options,
        IProgress<string>? progress = null)
    {
        if (!Directory.Exists(romDirectory))
            throw new DirectoryNotFoundException($"ROM directory not found: {romDirectory}");

        var result = new Dir2DatResult();
        var games = new List<Dir2DatGame>();

        var files = Directory.EnumerateFiles(romDirectory, "*", SearchOption.TopDirectoryOnly).ToList();

        int processed = 0;
        foreach (string file in files)
        {
            processed++;
            string fileName = Path.GetFileName(file);
            progress?.Report($"Scanning {processed} of {files.Count}: {fileName}...");

            string ext = Path.GetExtension(file).ToLowerInvariant();

            if (ext == ".zip")
            {
                var game = await Task.Run(() => ScanZipFile(file, options)).ConfigureAwait(false);
                if (game != null)
                {
                    games.Add(game);
                    result.TotalRoms += game.Roms.Count;
                }
            }
            else if (options.IncludeLooseFiles)
            {
                var game = await Task.Run(() => ScanLooseFile(file, options)).ConfigureAwait(false);
                if (game != null)
                {
                    games.Add(game);
                    result.TotalRoms += game.Roms.Count;
                }
            }
        }

        // Also scan subdirectories for CHD files
        if (options.IncludeChd)
        {
            var chdFiles = Directory.EnumerateFiles(romDirectory, "*.chd", SearchOption.AllDirectories).ToList();
            foreach (string chdFile in chdFiles)
            {
                string relativePath = Path.GetRelativePath(romDirectory, chdFile);
                string parentDir = Path.GetDirectoryName(relativePath) ?? "";
                string gameName = string.IsNullOrEmpty(parentDir) ? Path.GetFileNameWithoutExtension(chdFile) : parentDir;

                // Find or create the game entry for this CHD's parent directory
                var existingGame = games.FirstOrDefault(g =>
                    g.Name.Equals(gameName, StringComparison.OrdinalIgnoreCase));

                if (existingGame == null)
                {
                    existingGame = new Dir2DatGame { Name = gameName, Description = gameName };
                    games.Add(existingGame);
                }

                var diskInfo = await Task.Run(() => ScanChdFile(chdFile)).ConfigureAwait(false);
                if (diskInfo != null)
                    existingGame.Disks.Add(diskInfo);
            }
        }

        result.TotalGames = games.Count;
        result.Games = games;

        progress?.Report($"Done — {result.TotalGames} games, {result.TotalRoms} ROMs scanned.");

        return result;
    }

    /// <summary>
    /// Exports the scan results to a Logiqx XML DAT file.
    /// </summary>
    public static void ExportDat(Dir2DatResult result, string outputPath, Dir2DatOptions options)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XDocumentType("datafile", null, null, null),
            BuildDatafileElement(result, options));

        try
        {
            using var writer = new StreamWriter(outputPath, false, new UTF8Encoding(false));
            doc.Save(writer);
        }
        catch (IOException)
        {
            try { File.Delete(outputPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            try { File.Delete(outputPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw;
        }
    }

    private static XElement BuildDatafileElement(Dir2DatResult result, Dir2DatOptions options)
    {
        var datafile = new XElement("datafile",
            new XElement("header",
                new XElement("name", options.DatName),
                new XElement("description", options.DatDescription),
                new XElement("version", options.DatVersion),
                new XElement("author", options.DatAuthor)));

        foreach (var game in result.Games.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase))
        {
            var gameElement = new XElement("game",
                new XAttribute("name", game.Name),
                new XElement("description", game.Description));

            foreach (var rom in game.Roms)
            {
                var romElement = new XElement("rom",
                    new XAttribute("name", rom.Name),
                    new XAttribute("size", rom.Size));

                if (!string.IsNullOrEmpty(rom.CRC32))
                    romElement.Add(new XAttribute("crc", rom.CRC32.ToLowerInvariant()));
                if (!string.IsNullOrEmpty(rom.MD5))
                    romElement.Add(new XAttribute("md5", rom.MD5.ToLowerInvariant()));
                if (!string.IsNullOrEmpty(rom.SHA1))
                    romElement.Add(new XAttribute("sha1", rom.SHA1.ToLowerInvariant()));

                gameElement.Add(romElement);
            }

            foreach (var disk in game.Disks)
            {
                var diskElement = new XElement("disk",
                    new XAttribute("name", disk.Name));

                if (!string.IsNullOrEmpty(disk.SHA1))
                    diskElement.Add(new XAttribute("sha1", disk.SHA1.ToLowerInvariant()));

                gameElement.Add(diskElement);
            }

            datafile.Add(gameElement);
        }

        return datafile;
    }

    private static Dir2DatGame? ScanZipFile(string zipPath, Dir2DatOptions options)
    {
        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            string gameName = Path.GetFileNameWithoutExtension(zipPath);

            var game = new Dir2DatGame
            {
                Name = gameName,
                Description = gameName
            };

            foreach (var entry in archive.Entries)
            {
                if (entry.Length == 0 && string.IsNullOrEmpty(entry.Name)) continue;

                var romInfo = new Dir2DatRom
                {
                    Name = entry.FullName,
                    Size = entry.Length,
                    CRC32 = entry.Crc32.ToString("X8")
                };

                if (options.ComputeSHA1 && options.ComputeMD5)
                {
                    using var stream = entry.Open();
                    (romInfo.SHA1, romInfo.MD5) = ComputeSha1AndMd5(stream);
                }
                else if (options.ComputeSHA1)
                {
                    using var stream = entry.Open();
                    romInfo.SHA1 = ComputeSha1(stream);
                }
                else if (options.ComputeMD5)
                {
                    using var stream = entry.Open();
                    romInfo.MD5 = ComputeMd5(stream);
                }

                game.Roms.Add(romInfo);
            }

            return game.Roms.Count > 0 ? game : null;
        }
        catch (InvalidDataException) { return null; }
        catch (IOException) { return null; }
    }

    private static Dir2DatGame? ScanLooseFile(string filePath, Dir2DatOptions options)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            string gameName = Path.GetFileNameWithoutExtension(filePath);

            var rom = new Dir2DatRom
            {
                Name = fileInfo.Name,
                Size = fileInfo.Length
            };

            // Compute all requested hashes in a single pass to avoid reading the file multiple times
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                bool needSha1 = options.ComputeSHA1;
                bool needMd5 = options.ComputeMD5;

                if (needSha1 || needMd5)
                {
                    using var sha1Inc = needSha1 ? IncrementalHash.CreateHash(HashAlgorithmName.SHA1) : null;
                    using var md5Inc = needMd5 ? IncrementalHash.CreateHash(HashAlgorithmName.MD5) : null;

                    uint crc = 0xFFFFFFFF;
                    byte[] buffer = new byte[8192];
                    int bytesRead;
                    while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        for (int i = 0; i < bytesRead; i++)
                            crc = (crc >> 8) ^ Crc32Table[(crc ^ buffer[i]) & 0xFF];
                        sha1Inc?.AppendData(buffer, 0, bytesRead);
                        md5Inc?.AppendData(buffer, 0, bytesRead);
                    }

                    rom.CRC32 = (crc ^ 0xFFFFFFFF).ToString("X8");
                    if (sha1Inc != null) rom.SHA1 = Convert.ToHexString(sha1Inc.GetHashAndReset());
                    if (md5Inc != null) rom.MD5 = Convert.ToHexString(md5Inc.GetHashAndReset());
                }
                else
                {
                    rom.CRC32 = ComputeCrc32(stream);
                }
            }

            return new Dir2DatGame
            {
                Name = gameName,
                Description = gameName,
                Roms = [rom]
            };
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    private static Dir2DatDisk? ScanChdFile(string chdPath)
    {
        try
        {
            string name = Path.GetFileNameWithoutExtension(chdPath);

            // Read CHD header to extract SHA-1 if available
            using var stream = new FileStream(chdPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new BinaryReader(stream);

            if (stream.Length < 16) return new Dir2DatDisk { Name = name };

            byte[] magic = reader.ReadBytes(8);
            if (!magic.AsSpan().SequenceEqual(ChdMagic))
                return new Dir2DatDisk { Name = name };

            uint headerLength = ReadBigEndianUInt32(reader);
            uint version = ReadBigEndianUInt32(reader);

            string sha1 = "";
            switch (version)
            {
                case 3:
                    if (headerLength >= 120)
                    {
                        reader.ReadBytes(28); // flags+compression+hunks+logicalbytes+metaoffset
                        reader.ReadBytes(16); // md5
                        reader.ReadBytes(16); // parentmd5
                        reader.ReadBytes(4);  // hunkbytes
                        byte[] sha1Bytes = reader.ReadBytes(20);
                        sha1 = Convert.ToHexString(sha1Bytes);
                    }
                    break;
                case 4:
                    if (headerLength >= 108)
                    {
                        reader.ReadBytes(28); // flags+compression+hunks+logicalbytes+metaoffset
                        reader.ReadBytes(4);  // hunkbytes
                        byte[] sha1Bytes = reader.ReadBytes(20);
                        sha1 = Convert.ToHexString(sha1Bytes);
                    }
                    break;
                case 5:
                    if (headerLength >= 124)
                    {
                        reader.ReadBytes(16); // compressors
                        reader.ReadBytes(24); // logicalbytes+mapoffset+metaoffset
                        reader.ReadBytes(8);  // hunkbytes+unitbytes
                        reader.ReadBytes(20); // rawsha1
                        byte[] sha1Bytes = reader.ReadBytes(20);
                        sha1 = Convert.ToHexString(sha1Bytes);
                    }
                    break;
            }

            return new Dir2DatDisk { Name = name, SHA1 = sha1 };
        }
        catch (IOException) { return new Dir2DatDisk { Name = Path.GetFileNameWithoutExtension(chdPath) }; }
    }

    private static string ComputeCrc32(Stream stream)
    {
        uint crc = 0xFFFFFFFF;
        byte[] buffer = new byte[8192];
        int bytesRead;

        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < bytesRead; i++)
                crc = (crc >> 8) ^ Crc32Table[(crc ^ buffer[i]) & 0xFF];
        }

        return (crc ^ 0xFFFFFFFF).ToString("X8");
    }

    private static readonly uint[] Crc32Table = GenerateCrc32Table();

    private static uint[] GenerateCrc32Table()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint crc = i;
            for (int j = 0; j < 8; j++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
            table[i] = crc;
        }
        return table;
    }

    private static string ComputeSha1(Stream stream)
    {
        byte[] hash = SHA1.HashData(stream);
        return Convert.ToHexString(hash);
    }

    private static string ComputeMd5(Stream stream)
    {
        byte[] hash = MD5.HashData(stream);
        return Convert.ToHexString(hash);
    }

    private static (string sha1, string md5) ComputeSha1AndMd5(Stream stream)
    {
        using var sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        byte[] buffer = new byte[8192];
        int bytesRead;

        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            sha1.AppendData(buffer, 0, bytesRead);
            md5.AppendData(buffer, 0, bytesRead);
        }

        return (Convert.ToHexString(sha1.GetHashAndReset()), Convert.ToHexString(md5.GetHashAndReset()));
    }

    private static uint ReadBigEndianUInt32(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(4);
        if (bytes.Length < 4)
            throw new EndOfStreamException();
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToUInt32(bytes, 0);
    }
}

public class Dir2DatOptions
{
    public string DatName { get; set; } = "Dir2Dat";
    public string DatDescription { get; set; } = "Generated by Retro Multi Tools";
    public string DatVersion { get; set; } = "1.0";
    public string DatAuthor { get; set; } = "";
    public bool ComputeSHA1 { get; set; } = true;
    public bool ComputeMD5 { get; set; }
    public bool IncludeLooseFiles { get; set; }
    public bool IncludeChd { get; set; } = true;
}

public class Dir2DatRom
{
    public string Name { get; set; } = string.Empty;
    public long Size { get; set; }
    public string CRC32 { get; set; } = string.Empty;
    public string SHA1 { get; set; } = string.Empty;
    public string MD5 { get; set; } = string.Empty;
}

public class Dir2DatDisk
{
    public string Name { get; set; } = string.Empty;
    public string SHA1 { get; set; } = string.Empty;
}

public class Dir2DatGame
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<Dir2DatRom> Roms { get; set; } = [];
    public List<Dir2DatDisk> Disks { get; set; } = [];
}

public class Dir2DatResult
{
    public int TotalGames { get; set; }
    public int TotalRoms { get; set; }
    public List<Dir2DatGame> Games { get; set; } = [];

    public string Summary =>
        $"{TotalGames} games with {TotalRoms} ROMs scanned.";
}
