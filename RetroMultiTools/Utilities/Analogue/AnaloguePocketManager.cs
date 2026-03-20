using System.Text;
using System.Text.Json;

namespace RetroMultiTools.Utilities.Analogue;

/// <summary>
/// Manages Analogue Pocket SD card operations: core listing, screenshot export,
/// save backup/restore, save state management, GB Camera photo extraction,
/// file auto-copy, and library image generation.
/// </summary>
public static class AnaloguePocketManager
{
    // ── SD Card Structure ──────────────────────────────────────────────

    private const string CoresDir = "Cores";
    private const string PlatformsDir = "Platforms";
    private const string SavesDir = "Saves";
    private const string SaveStatesDir = "Memories";
    private const string ScreenshotsDir = "Screenshots";
    private const string AssetsDir = "Assets";
    private const string LibraryImagesDir = "System/Library/Images";

    /// <summary>
    /// Validates that the given path looks like an Analogue Pocket SD card root.
    /// Checks for the presence of expected top-level directories.
    /// </summary>
    public static bool ValidateSdCard(string sdRoot)
    {
        if (string.IsNullOrWhiteSpace(sdRoot) || !Directory.Exists(sdRoot))
            return false;

        // At least one of the well-known directories must exist
        return Directory.Exists(Path.Combine(sdRoot, CoresDir))
            || Directory.Exists(Path.Combine(sdRoot, PlatformsDir))
            || Directory.Exists(Path.Combine(sdRoot, AssetsDir));
    }

    // ── Core Management ────────────────────────────────────────────────

    /// <summary>
    /// Represents an installed openFPGA core on the Pocket SD card.
    /// </summary>
    public sealed class CoreInfo
    {
        public string DirectoryName { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string CoreName { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string SizeFormatted => FileUtils.FormatFileSize(SizeBytes);
    }

    /// <summary>
    /// Lists all installed openFPGA cores on the SD card.
    /// </summary>
    public static async Task<List<CoreInfo>> ListCoresAsync(string sdRoot)
    {
        string coresPath = Path.Combine(sdRoot, CoresDir);
        if (!Directory.Exists(coresPath))
            return [];

        return await Task.Run(() =>
        {
            var cores = new List<CoreInfo>();
            foreach (string dir in Directory.GetDirectories(coresPath))
            {
                string dirName = Path.GetFileName(dir);
                var info = new CoreInfo
                {
                    DirectoryName = dirName,
                    FullPath = dir
                };

                // Parse author.corename from directory name
                int dotIndex = dirName.IndexOf('.', StringComparison.Ordinal);
                if (dotIndex > 0)
                {
                    info.Author = dirName[..dotIndex];
                    info.CoreName = dirName[(dotIndex + 1)..];
                }
                else
                {
                    info.CoreName = dirName;
                }

                // Try to read core.json for metadata
                string coreJson = Path.Combine(dir, "core.json");
                if (File.Exists(coreJson))
                {
                    try
                    {
                        string json = File.ReadAllText(coreJson);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        if (root.TryGetProperty("core", out var coreElem))
                        {
                            if (coreElem.TryGetProperty("metadata", out var meta))
                            {
                                if (meta.TryGetProperty("platform_ids", out var pids) && pids.GetArrayLength() > 0)
                                    info.Platform = pids[0].GetString() ?? string.Empty;
                                if (meta.TryGetProperty("version", out var ver))
                                    info.Version = ver.GetString() ?? string.Empty;
                                if (meta.TryGetProperty("description", out var desc))
                                    info.Description = desc.GetString() ?? string.Empty;
                            }
                        }
                    }
                    catch (JsonException) { }
                    catch (IOException) { }
                }

                // Calculate total size
                try
                {
                    info.SizeBytes = new DirectoryInfo(dir)
                        .EnumerateFiles("*", SearchOption.AllDirectories)
                        .Sum(f => f.Length);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }

                cores.Add(info);
            }

            return cores.OrderBy(c => c.Author).ThenBy(c => c.CoreName).ToList();
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes a core from the SD card.
    /// </summary>
    public static async Task DeleteCoreAsync(string corePath)
    {
        if (!Directory.Exists(corePath))
            throw new DirectoryNotFoundException($"Core directory not found: {corePath}");

        await Task.Run(() => Directory.Delete(corePath, recursive: true)).ConfigureAwait(false);
    }

    // ── Screenshot Export ──────────────────────────────────────────────

    /// <summary>
    /// Lists all screenshot .bmp files on the Pocket SD card.
    /// </summary>
    public static string[] ListScreenshots(string sdRoot)
    {
        string screenshotsPath = Path.Combine(sdRoot, ScreenshotsDir);
        if (!Directory.Exists(screenshotsPath))
            return [];

        return Directory.GetFiles(screenshotsPath, "*.bmp", SearchOption.AllDirectories)
            .OrderByDescending(f => new FileInfo(f).LastWriteTimeUtc)
            .ToArray();
    }

    /// <summary>
    /// Exports Pocket screenshots to a destination folder.
    /// The Pocket saves raw BMP screenshots; this copies them directly.
    /// </summary>
    public static async Task<int> ExportScreenshotsAsync(
        string sdRoot,
        string outputDir,
        IProgress<string>? progress = null)
    {
        string[] screenshots = ListScreenshots(sdRoot);
        if (screenshots.Length == 0)
            return 0;

        Directory.CreateDirectory(outputDir);

        int exported = 0;
        foreach (string bmpPath in screenshots)
        {
            string fileName = Path.GetFileNameWithoutExtension(bmpPath) + ".bmp";
            string destPath = Path.Combine(outputDir, fileName);

            progress?.Report($"Exporting {Path.GetFileName(bmpPath)}...");

            // Copy the BMP file directly — the Pocket produces standard BMP files
            await Task.Run(() => File.Copy(bmpPath, destPath, overwrite: true)).ConfigureAwait(false);
            exported++;
        }

        return exported;
    }

    // ── Save Backup & Restore ──────────────────────────────────────────

    /// <summary>
    /// Backs up all save files from the Pocket SD card to the specified backup directory.
    /// Preserves the subdirectory structure.
    /// </summary>
    public static async Task<int> BackupSavesAsync(
        string sdRoot,
        string backupDir,
        IProgress<string>? progress = null)
    {
        string savesPath = Path.Combine(sdRoot, SavesDir);
        if (!Directory.Exists(savesPath))
            return 0;

        return await Task.Run(() =>
        {
            int count = 0;
            foreach (string file in Directory.EnumerateFiles(savesPath, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(savesPath, file);
                string destPath = Path.Combine(backupDir, relativePath);

                string? destDir = Path.GetDirectoryName(destPath);
                if (destDir != null)
                    Directory.CreateDirectory(destDir);

                progress?.Report($"Backing up {relativePath}...");
                File.Copy(file, destPath, overwrite: true);
                count++;
            }
            return count;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Restores save files from a backup directory to the Pocket SD card.
    /// </summary>
    public static async Task<int> RestoreSavesAsync(
        string sdRoot,
        string backupDir,
        IProgress<string>? progress = null)
    {
        string savesPath = Path.Combine(sdRoot, SavesDir);
        if (!Directory.Exists(backupDir))
            return 0;

        return await Task.Run(() =>
        {
            int count = 0;
            foreach (string file in Directory.EnumerateFiles(backupDir, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(backupDir, file);
                string destPath = Path.Combine(savesPath, relativePath);

                string? destDir = Path.GetDirectoryName(destPath);
                if (destDir != null)
                    Directory.CreateDirectory(destDir);

                progress?.Report($"Restoring {relativePath}...");
                File.Copy(file, destPath, overwrite: true);
                count++;
            }
            return count;
        }).ConfigureAwait(false);
    }

    // ── Game Folders ───────────────────────────────────────────────────

    /// <summary>
    /// Returns the list of platform directories under Assets that contain game files.
    /// </summary>
    public static List<(string Name, string Path)> GetGameFolders(string sdRoot)
    {
        string assetsPath = Path.Combine(sdRoot, AssetsDir);
        if (!Directory.Exists(assetsPath))
            return [];

        return Directory.GetDirectories(assetsPath)
            .Select(d => (Name: Path.GetFileName(d), Path: d))
            .OrderBy(d => d.Name)
            .ToList();
    }

    // ── Save State Management ──────────────────────────────────────────

    /// <summary>
    /// Represents a save state file on the Pocket SD card.
    /// </summary>
    public sealed class SaveStateInfo
    {
        public string FileName { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string SizeFormatted => FileUtils.FormatFileSize(SizeBytes);
        public DateTime LastModified { get; set; }
    }

    /// <summary>
    /// Lists all save state files on the Pocket SD card.
    /// </summary>
    public static async Task<List<SaveStateInfo>> ListSaveStatesAsync(string sdRoot)
    {
        string statesPath = Path.Combine(sdRoot, SaveStatesDir);
        if (!Directory.Exists(statesPath))
            return [];

        return await Task.Run(() =>
        {
            var states = new List<SaveStateInfo>();
            foreach (string file in Directory.EnumerateFiles(statesPath, "*", SearchOption.AllDirectories))
            {
                var fi = new FileInfo(file);
                string relativePath = Path.GetRelativePath(statesPath, file);
                string platform = relativePath.Contains(Path.DirectorySeparatorChar)
                    ? relativePath[..relativePath.IndexOf(Path.DirectorySeparatorChar)]
                    : string.Empty;

                states.Add(new SaveStateInfo
                {
                    FileName = fi.Name,
                    FullPath = file,
                    Platform = platform,
                    SizeBytes = fi.Length,
                    LastModified = fi.LastWriteTimeUtc
                });
            }
            return states.OrderByDescending(s => s.LastModified).ToList();
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes the specified save state files.
    /// </summary>
    public static async Task<int> DeleteSaveStatesAsync(
        IEnumerable<string> filePaths,
        IProgress<string>? progress = null)
    {
        return await Task.Run(() =>
        {
            int count = 0;
            foreach (string path in filePaths)
            {
                if (File.Exists(path))
                {
                    progress?.Report($"Deleting {Path.GetFileName(path)}...");
                    File.Delete(path);
                    count++;
                }
            }
            return count;
        }).ConfigureAwait(false);
    }

    // ── GB Camera Photo Export ─────────────────────────────────────────

    /// <summary>
    /// Represents a photo extracted from a Game Boy Camera save file.
    /// </summary>
    public sealed class GbCameraPhoto
    {
        public int Index { get; set; }
        public byte[] PixelData { get; set; } = [];
        public const int Width = 128;
        public const int Height = 112;
    }

    // Game Boy Camera save file constants
    private const int GbCameraPhotoCount = 30;
    private const int GbCameraPhotoWidth = 128;
    private const int GbCameraPhotoHeight = 112;
    private const int GbCameraTileWidth = GbCameraPhotoWidth / 8;   // 16 tiles wide
    private const int GbCameraTileHeight = GbCameraPhotoHeight / 8; // 14 tiles tall
    private const int GbCameraBytesPerTile = 16; // 2bpp = 16 bytes per 8x8 tile
    private const int GbCameraPhotoSize = GbCameraTileWidth * GbCameraTileHeight * GbCameraBytesPerTile; // 3584 bytes
    private const int GbCameraPhotoDataStart = 0x2000; // Photos start at offset 0x2000 in the save file
    private const int GbCameraPhotoStride = 0x1000; // Each photo slot is 0x1000 bytes apart

    /// <summary>
    /// Extracts photos from a Game Boy Camera save (.sav) file.
    /// Returns the decoded grayscale pixel data for each photo.
    /// </summary>
    public static async Task<List<GbCameraPhoto>> ExtractGbCameraPhotosAsync(string savFilePath)
    {
        if (!File.Exists(savFilePath))
            throw new FileNotFoundException("Save file not found.", savFilePath);

        byte[] data = await File.ReadAllBytesAsync(savFilePath).ConfigureAwait(false);

        // Minimum size: header + at least one photo
        if (data.Length < GbCameraPhotoDataStart + GbCameraPhotoSize)
            throw new InvalidOperationException("File is too small to be a Game Boy Camera save file.");

        return await Task.Run(() =>
        {
            var photos = new List<GbCameraPhoto>();

            for (int i = 0; i < GbCameraPhotoCount; i++)
            {
                int offset = GbCameraPhotoDataStart + (i * GbCameraPhotoStride);
                if (offset + GbCameraPhotoSize > data.Length)
                    break;

                // Check if the photo slot is empty (all 0x00 or all 0xFF)
                bool isEmpty = true;
                for (int j = 0; j < GbCameraPhotoSize && isEmpty; j++)
                {
                    byte b = data[offset + j];
                    if (b != 0x00 && b != 0xFF)
                        isEmpty = false;
                }

                if (isEmpty)
                    continue;

                var photo = new GbCameraPhoto { Index = i };
                photo.PixelData = DecodeTileData(data, offset, GbCameraTileWidth, GbCameraTileHeight);
                photos.Add(photo);
            }

            return photos;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Decodes 2bpp tile data into a grayscale pixel array.
    /// </summary>
    private static byte[] DecodeTileData(byte[] data, int offset, int tilesWide, int tilesTall)
    {
        int pixelWidth = tilesWide * 8;
        int pixelHeight = tilesTall * 8;
        byte[] pixels = new byte[pixelWidth * pixelHeight];

        // Game Boy 2bpp palette: 0=white, 1=light gray, 2=dark gray, 3=black
        byte[] palette = [0xFF, 0xAA, 0x55, 0x00];

        int tileIndex = 0;
        for (int ty = 0; ty < tilesTall; ty++)
        {
            for (int tx = 0; tx < tilesWide; tx++)
            {
                int tileOffset = offset + tileIndex * GbCameraBytesPerTile;

                for (int row = 0; row < 8; row++)
                {
                    byte lo = data[tileOffset + row * 2];
                    byte hi = data[tileOffset + row * 2 + 1];

                    for (int col = 0; col < 8; col++)
                    {
                        int bit = 7 - col;
                        int colorIndex = ((hi >> bit) & 1) << 1 | ((lo >> bit) & 1);
                        int px = tx * 8 + col;
                        int py = ty * 8 + row;
                        pixels[py * pixelWidth + px] = palette[colorIndex];
                    }
                }

                tileIndex++;
            }
        }

        return pixels;
    }

    /// <summary>
    /// Exports GB Camera photos as BMP files to the specified output directory.
    /// </summary>
    public static async Task<int> ExportGbCameraPhotosAsync(
        string savFilePath,
        string outputDir,
        IProgress<string>? progress = null)
    {
        var photos = await ExtractGbCameraPhotosAsync(savFilePath).ConfigureAwait(false);
        if (photos.Count == 0)
            return 0;

        Directory.CreateDirectory(outputDir);

        int exported = 0;
        foreach (var photo in photos)
        {
            string fileName = $"gbcamera_photo_{photo.Index:D2}.bmp";
            string destPath = Path.Combine(outputDir, fileName);

            progress?.Report($"Exporting photo {photo.Index + 1}...");

            await Task.Run(() => WriteGrayscaleBmp(
                destPath, photo.PixelData, GbCameraPhoto.Width, GbCameraPhoto.Height
            )).ConfigureAwait(false);

            exported++;
        }

        return exported;
    }

    /// <summary>
    /// Writes a grayscale pixel array as a BMP file.
    /// </summary>
    private static void WriteGrayscaleBmp(string path, byte[] pixels, int width, int height)
    {
        // BMP with 8-bit grayscale palette
        int rowBytes = (width + 3) & ~3; // 4-byte aligned rows
        int imageSize = rowBytes * height;
        int paletteSize = 256 * 4;
        int headerSize = 14 + 40; // File header + info header
        int fileSize = headerSize + paletteSize + imageSize;

        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);

        // File header
        bw.Write((byte)'B');
        bw.Write((byte)'M');
        bw.Write(fileSize);
        bw.Write(0); // reserved
        bw.Write(headerSize + paletteSize); // pixel data offset

        // Info header (BITMAPINFOHEADER)
        bw.Write(40); // header size
        bw.Write(width);
        bw.Write(height); // positive = bottom-up
        bw.Write((short)1); // planes
        bw.Write((short)8); // bits per pixel
        bw.Write(0); // compression (BI_RGB)
        bw.Write(imageSize);
        bw.Write(2835); // X pixels per meter (~72 DPI)
        bw.Write(2835); // Y pixels per meter
        bw.Write(256); // colors used
        bw.Write(0); // important colors

        // Grayscale palette
        for (int i = 0; i < 256; i++)
        {
            bw.Write((byte)i); // B
            bw.Write((byte)i); // G
            bw.Write((byte)i); // R
            bw.Write((byte)0); // A (unused)
        }

        // Pixel data (BMP is bottom-up)
        for (int y = height - 1; y >= 0; y--)
        {
            for (int x = 0; x < width; x++)
                bw.Write(pixels[y * width + x]);

            // Padding to 4-byte boundary
            for (int p = width; p < rowBytes; p++)
                bw.Write((byte)0);
        }
    }

    // ── Auto-Copy Files ────────────────────────────────────────────────

    /// <summary>
    /// Copies all files from the source directory to the corresponding location
    /// on the Pocket SD card, matching the directory structure.
    /// </summary>
    public static async Task<int> AutoCopyFilesAsync(
        string sourceDir,
        string sdRoot,
        IProgress<string>? progress = null)
    {
        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");

        return await Task.Run(() =>
        {
            int count = 0;
            foreach (string file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(sourceDir, file);
                string destPath = Path.Combine(sdRoot, relativePath);

                string? destDir = Path.GetDirectoryName(destPath);
                if (destDir != null)
                    Directory.CreateDirectory(destDir);

                progress?.Report($"Copying {relativePath}...");
                File.Copy(file, destPath, overwrite: true);
                count++;
            }
            return count;
        }).ConfigureAwait(false);
    }

    // ── Library Image Generator ────────────────────────────────────────

    /// <summary>
    /// Generates placeholder library images for ROMs that don't have one.
    /// Creates simple BMP images with the game name as text.
    /// </summary>
    public static async Task<int> GenerateLibraryImagesAsync(
        string sdRoot,
        IProgress<string>? progress = null)
    {
        string assetsPath = Path.Combine(sdRoot, AssetsDir);
        string libraryPath = Path.Combine(sdRoot, LibraryImagesDir);

        if (!Directory.Exists(assetsPath))
            return 0;

        Directory.CreateDirectory(libraryPath);

        return await Task.Run(() =>
        {
            int generated = 0;
            HashSet<string> romExtensions = [".gba", ".gbc", ".gb", ".nes", ".sfc", ".smc",
                                       ".gen", ".md", ".sms", ".gg", ".pce",
                                       ".ngp", ".ngc", ".sg", ".col"];

            foreach (string dir in Directory.GetDirectories(assetsPath))
            {
                string commonDir = Path.Combine(dir, "common");
                if (!Directory.Exists(commonDir))
                    continue;

                foreach (string romFile in Directory.EnumerateFiles(commonDir, "*", SearchOption.AllDirectories))
                {
                    string ext = Path.GetExtension(romFile).ToLowerInvariant();
                    if (!romExtensions.Contains(ext))
                        continue;

                    string baseName = Path.GetFileNameWithoutExtension(romFile);
                    string imagePath = Path.Combine(libraryPath, baseName + ".bmp");

                    if (File.Exists(imagePath))
                        continue;

                    progress?.Report($"Generating image for {baseName}...");
                    GeneratePlaceholderImage(imagePath, baseName);
                    generated++;
                }
            }

            return generated;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Generates a simple 160×144 placeholder BMP image with the game name
    /// rendered using a minimal 8×8 font.
    /// </summary>
    private static void GeneratePlaceholderImage(string path, string label)
    {
        const int width = 160;
        const int height = 144;
        const int charW = 8;
        const int charH = 8;

        // Build a simple pixel buffer (RGB, one byte per channel)
        byte[,] pixelsR = new byte[height, width];
        byte[,] pixelsG = new byte[height, width];
        byte[,] pixelsB = new byte[height, width];

        // Fill with dark purple background
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            pixelsR[y, x] = 0x18;
            pixelsG[y, x] = 0x18;
            pixelsB[y, x] = 0x25;
        }

        // Render the label text centered using the built-in 8×8 font
        if (!string.IsNullOrEmpty(label))
        {
            // Truncate label to fit width (max chars = width / charW)
            int maxChars = width / charW;
            string text = label.Length > maxChars ? label[..maxChars] : label;

            // Center the text horizontally and vertically
            int startX = (width - text.Length * charW) / 2;
            int startY = (height - charH) / 2;

            foreach (char ch in text)
            {
                if (PlaceholderFont.TryGetValue(ch, out byte[]? rows) ||
                    PlaceholderFont.TryGetValue(char.ToUpperInvariant(ch), out rows))
                {
                    for (int row = 0; row < charH && row < rows.Length; row++)
                    {
                        for (int col = 0; col < charW; col++)
                        {
                            if ((rows[row] & (0x80 >> col)) != 0)
                            {
                                int px = startX + col;
                                int py = startY + row;
                                if (px >= 0 && px < width && py >= 0 && py < height)
                                {
                                    pixelsR[py, px] = 0xCD;
                                    pixelsG[py, px] = 0xD6;
                                    pixelsB[py, px] = 0xF4;
                                }
                            }
                        }
                    }
                }
                startX += charW;
            }
        }

        // Write as 24-bit BMP
        int rowBytes = (width * 3 + 3) & ~3;
        int imageSize = rowBytes * height;
        int headerSize = 14 + 40;
        int fileSize = headerSize + imageSize;

        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);

        // File header
        bw.Write((byte)'B');
        bw.Write((byte)'M');
        bw.Write(fileSize);
        bw.Write(0);
        bw.Write(headerSize);

        // Info header
        bw.Write(40);
        bw.Write(width);
        bw.Write(height);
        bw.Write((short)1);
        bw.Write((short)24);
        bw.Write(0);
        bw.Write(imageSize);
        bw.Write(2835);
        bw.Write(2835);
        bw.Write(0);
        bw.Write(0);

        // Pixel data (BMP is bottom-up, BGR order)
        for (int y = height - 1; y >= 0; y--)
        {
            for (int x = 0; x < width; x++)
            {
                bw.Write(pixelsB[y, x]);
                bw.Write(pixelsG[y, x]);
                bw.Write(pixelsR[y, x]);
            }
            for (int p = width * 3; p < rowBytes; p++)
                bw.Write((byte)0);
        }
    }

    /// <summary>
    /// Minimal 8×8 font for placeholder image text rendering.
    /// Covers uppercase A–Z, digits 0–9, and common punctuation.
    /// </summary>
    private static readonly Dictionary<char, byte[]> PlaceholderFont = new()
    {
        [' '] = [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00],
        ['0'] = [0x3C, 0x66, 0x6E, 0x7E, 0x76, 0x66, 0x3C, 0x00],
        ['1'] = [0x18, 0x38, 0x18, 0x18, 0x18, 0x18, 0x7E, 0x00],
        ['2'] = [0x3C, 0x66, 0x06, 0x1C, 0x30, 0x66, 0x7E, 0x00],
        ['3'] = [0x3C, 0x66, 0x06, 0x1C, 0x06, 0x66, 0x3C, 0x00],
        ['4'] = [0x0C, 0x1C, 0x3C, 0x6C, 0x7E, 0x0C, 0x0C, 0x00],
        ['5'] = [0x7E, 0x60, 0x7C, 0x06, 0x06, 0x66, 0x3C, 0x00],
        ['6'] = [0x1C, 0x30, 0x60, 0x7C, 0x66, 0x66, 0x3C, 0x00],
        ['7'] = [0x7E, 0x06, 0x0C, 0x18, 0x30, 0x30, 0x30, 0x00],
        ['8'] = [0x3C, 0x66, 0x66, 0x3C, 0x66, 0x66, 0x3C, 0x00],
        ['9'] = [0x3C, 0x66, 0x66, 0x3E, 0x06, 0x0C, 0x38, 0x00],
        ['A'] = [0x18, 0x3C, 0x66, 0x66, 0x7E, 0x66, 0x66, 0x00],
        ['B'] = [0x7C, 0x66, 0x66, 0x7C, 0x66, 0x66, 0x7C, 0x00],
        ['C'] = [0x3C, 0x66, 0x60, 0x60, 0x60, 0x66, 0x3C, 0x00],
        ['D'] = [0x78, 0x6C, 0x66, 0x66, 0x66, 0x6C, 0x78, 0x00],
        ['E'] = [0x7E, 0x60, 0x60, 0x7C, 0x60, 0x60, 0x7E, 0x00],
        ['F'] = [0x7E, 0x60, 0x60, 0x7C, 0x60, 0x60, 0x60, 0x00],
        ['G'] = [0x3C, 0x66, 0x60, 0x6E, 0x66, 0x66, 0x3E, 0x00],
        ['H'] = [0x66, 0x66, 0x66, 0x7E, 0x66, 0x66, 0x66, 0x00],
        ['I'] = [0x3C, 0x18, 0x18, 0x18, 0x18, 0x18, 0x3C, 0x00],
        ['J'] = [0x06, 0x06, 0x06, 0x06, 0x06, 0x66, 0x3C, 0x00],
        ['K'] = [0x66, 0x6C, 0x78, 0x70, 0x78, 0x6C, 0x66, 0x00],
        ['L'] = [0x60, 0x60, 0x60, 0x60, 0x60, 0x60, 0x7E, 0x00],
        ['M'] = [0x63, 0x77, 0x7F, 0x6B, 0x63, 0x63, 0x63, 0x00],
        ['N'] = [0x66, 0x76, 0x7E, 0x7E, 0x6E, 0x66, 0x66, 0x00],
        ['O'] = [0x3C, 0x66, 0x66, 0x66, 0x66, 0x66, 0x3C, 0x00],
        ['P'] = [0x7C, 0x66, 0x66, 0x7C, 0x60, 0x60, 0x60, 0x00],
        ['Q'] = [0x3C, 0x66, 0x66, 0x66, 0x6A, 0x6C, 0x36, 0x00],
        ['R'] = [0x7C, 0x66, 0x66, 0x7C, 0x6C, 0x66, 0x66, 0x00],
        ['S'] = [0x3C, 0x66, 0x60, 0x3C, 0x06, 0x66, 0x3C, 0x00],
        ['T'] = [0x7E, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x00],
        ['U'] = [0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x3C, 0x00],
        ['V'] = [0x66, 0x66, 0x66, 0x66, 0x66, 0x3C, 0x18, 0x00],
        ['W'] = [0x63, 0x63, 0x63, 0x6B, 0x7F, 0x77, 0x63, 0x00],
        ['X'] = [0x66, 0x66, 0x3C, 0x18, 0x3C, 0x66, 0x66, 0x00],
        ['Y'] = [0x66, 0x66, 0x66, 0x3C, 0x18, 0x18, 0x18, 0x00],
        ['Z'] = [0x7E, 0x06, 0x0C, 0x18, 0x30, 0x60, 0x7E, 0x00],
        ['-'] = [0x00, 0x00, 0x00, 0x7E, 0x00, 0x00, 0x00, 0x00],
        ['_'] = [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x7E, 0x00],
        ['.'] = [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x18, 0x00],
        ['('] = [0x0C, 0x18, 0x30, 0x30, 0x30, 0x18, 0x0C, 0x00],
        [')'] = [0x30, 0x18, 0x0C, 0x0C, 0x0C, 0x18, 0x30, 0x00],
    };
}
