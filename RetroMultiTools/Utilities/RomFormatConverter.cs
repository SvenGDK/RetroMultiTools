namespace RetroMultiTools.Utilities;

public static class RomFormatConverter
{
    private const int BufferSize = 81920;
    private const int CopierHeaderSize = 512;

    public static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".nes", ".smc", ".sfc", ".z64", ".n64", ".v64",
        ".gb", ".gbc", ".gba", ".vb", ".vboy",
        ".sms", ".md", ".gen",
        ".bin", ".a26", ".a52", ".a78", ".pce", ".tg16",
        ".32x", ".gg", ".j64", ".jag",
        ".lnx", ".lyx",
        ".ngp", ".ngc",
        ".col", ".cv", ".int",
        ".mx1", ".mx2",
        ".sv", ".ccc",
        ".iso", ".cue", ".3do",
        ".cdi", ".gdi",
        ".chd", ".rvz", ".gcm",
        ".chf",
        ".tgc",
        ".mtx", ".run"
    };

    public enum ConversionType
    {
        AddCopierHeader,
        RemoveCopierHeader,
        NesToUnheadered,
        NesFixHeader,
        ConvertToCHD,
        ConvertToRVZ
    }

    /// <summary>
    /// Checks whether the given ROM file has a removable copier header.
    /// </summary>
    public static bool HasCopierHeader(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found.", filePath);

        long length = new FileInfo(filePath).Length;
        return length > CopierHeaderSize && (length % 1024) == CopierHeaderSize;
    }

    /// <summary>
    /// Detects available conversions for a ROM file based on its format.
    /// </summary>
    public static List<ConversionType> GetAvailableConversions(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found.", filePath);

        var conversions = new List<ConversionType>();
        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        long length = new FileInfo(filePath).Length;

        bool hasCopier = length > CopierHeaderSize && (length % 1024) == CopierHeaderSize;

        if (hasCopier)
            conversions.Add(ConversionType.RemoveCopierHeader);
        else if (ext is ".smc" or ".sfc" or ".md" or ".gen" or ".sms" or ".pce" or ".tg16"
                     or ".gb" or ".gbc" or ".gba" or ".vb" or ".vboy"
                     or ".32x" or ".gg" or ".a26" or ".a52" or ".a78"
                     or ".bin" or ".j64" or ".jag" or ".lnx" or ".lyx"
                     or ".ngp" or ".ngc" or ".col" or ".cv" or ".int"
                     or ".mx1" or ".mx2" or ".sv" or ".ccc")
            conversions.Add(ConversionType.AddCopierHeader);

        if (ext == ".nes" && length > 16)
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 16);
            byte[] header = new byte[16];
            int read = fs.Read(header, 0, 16);

            if (read >= 4 && header[0] == 0x4E && header[1] == 0x45 && header[2] == 0x53 && header[3] == 0x1A)
            {
                conversions.Add(ConversionType.NesToUnheadered);
                conversions.Add(ConversionType.NesFixHeader);
            }
        }

        // CHD conversion available for disc images and GC/Wii formats
        if (ext is ".iso" or ".cue" or ".bin" or ".3do" or ".gcm")
            conversions.Add(ConversionType.ConvertToCHD);

        // RVZ conversion available for GameCube/Wii ISO images
        if (ext is ".iso" or ".gcm")
            conversions.Add(ConversionType.ConvertToRVZ);

        return conversions;
    }

    /// <summary>
    /// Gets the display name for a conversion type.
    /// </summary>
    public static string GetConversionName(ConversionType type)
    {
        var loc = Localization.LocalizationManager.Instance;
        return type switch
        {
            ConversionType.AddCopierHeader => loc["FormatConv_AddCopierHeader"],
            ConversionType.RemoveCopierHeader => loc["FormatConv_RemoveCopierHeader"],
            ConversionType.NesToUnheadered => loc["FormatConv_NesToUnheadered"],
            ConversionType.NesFixHeader => loc["FormatConv_NesFixHeader"],
            ConversionType.ConvertToCHD => loc["FormatConv_ConvertToCHD"],
            ConversionType.ConvertToRVZ => loc["FormatConv_ConvertToRVZ"],
            _ => type.ToString()
        };
    }

    /// <summary>
    /// Converts a ROM file based on the selected conversion type.
    /// </summary>
    public static async Task ConvertAsync(
        string inputPath,
        string outputPath,
        ConversionType conversionType,
        IProgress<string>? progress = null)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Input file not found.", inputPath);

        switch (conversionType)
        {
            case ConversionType.RemoveCopierHeader:
                await RemoveHeaderAsync(inputPath, outputPath, CopierHeaderSize, "copier header", progress).ConfigureAwait(false);
                break;
            case ConversionType.AddCopierHeader:
                await AddHeaderAsync(inputPath, outputPath, CopierHeaderSize, progress).ConfigureAwait(false);
                break;
            case ConversionType.NesToUnheadered:
                await RemoveHeaderAsync(inputPath, outputPath, 16, "iNES header", progress).ConfigureAwait(false);
                break;
            case ConversionType.NesFixHeader:
                await FixNesHeaderAsync(inputPath, outputPath, progress).ConfigureAwait(false);
                break;
            case ConversionType.ConvertToCHD:
                await ConvertToChdAsync(inputPath, outputPath, progress).ConfigureAwait(false);
                break;
            case ConversionType.ConvertToRVZ:
                await ConvertToRvzAsync(inputPath, outputPath, progress).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException($"Unsupported conversion type: {conversionType}");
        }
    }

    /// <summary>
    /// Batch converts all ROM files in a directory.
    /// </summary>
    public static async Task<BatchConversionResult> ConvertBatchAsync(
        string inputDirectory,
        string outputDirectory,
        ConversionType conversionType,
        IProgress<string>? progress = null)
    {
        if (!Directory.Exists(inputDirectory))
            throw new DirectoryNotFoundException($"Directory not found: {inputDirectory}");

        Directory.CreateDirectory(outputDirectory);

        var files = Directory.EnumerateFiles(inputDirectory, "*", SearchOption.AllDirectories)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f)))
            .ToList();

        int converted = 0;
        int skipped = 0;
        int failed = 0;

        var loc = Localization.LocalizationManager.Instance;

        for (int i = 0; i < files.Count; i++)
        {
            string file = files[i];
            string relativePath = Path.GetRelativePath(inputDirectory, file);
            string fileName = Path.GetFileName(file);
            progress?.Report(string.Format(loc["FormatConv_BatchProgress"], i + 1, files.Count, fileName));

            try
            {
                var available = GetAvailableConversions(file);
                if (!available.Contains(conversionType))
                {
                    skipped++;
                    continue;
                }

                string outputPath = Path.Combine(outputDirectory, relativePath);
                string? outputDir = Path.GetDirectoryName(outputPath);
                if (outputDir != null)
                    Directory.CreateDirectory(outputDir);
                await ConvertAsync(file, outputPath, conversionType, null).ConfigureAwait(false);
                converted++;
            }
            catch (IOException)
            {
                failed++;
            }
            catch (InvalidOperationException)
            {
                skipped++;
            }
        }

        progress?.Report(string.Format(loc["FormatConv_BatchDone"], converted, skipped, failed));
        return new BatchConversionResult { Converted = converted, Skipped = skipped, Failed = failed };
    }

    private static async Task RemoveHeaderAsync(string inputPath, string outputPath, int headerSize, string headerName, IProgress<string>? progress)
    {
        long inputLength = new FileInfo(inputPath).Length;
        if (inputLength <= headerSize)
            throw new InvalidOperationException(
                string.Format(Localization.LocalizationManager.Instance["FormatConv_ErrorTooSmall"], headerName));

        progress?.Report(string.Format(Localization.LocalizationManager.Instance["FormatConv_ProgressRemoving"], headerSize, headerName));

        await Task.Run(() =>
        {
            try
            {
                using var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
                using var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize);

                input.Seek(headerSize, SeekOrigin.Begin);
                byte[] buffer = new byte[BufferSize];
                int bytesRead;
                while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
                    output.Write(buffer, 0, bytesRead);
            }
            catch
            {
                try { File.Delete(outputPath); } catch { /* best-effort cleanup */ }
                throw;
            }
        }).ConfigureAwait(false);

        progress?.Report(Localization.LocalizationManager.Instance["FormatConv_ProgressDone"]);
    }

    private static async Task AddHeaderAsync(string inputPath, string outputPath, int headerSize, IProgress<string>? progress)
    {
        progress?.Report(string.Format(Localization.LocalizationManager.Instance["FormatConv_ProgressAdding"], headerSize));

        await Task.Run(() =>
        {
            try
            {
                using var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
                using var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize);

                output.Write(new byte[headerSize], 0, headerSize);
                byte[] buffer = new byte[BufferSize];
                int bytesRead;
                while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
                    output.Write(buffer, 0, bytesRead);
            }
            catch
            {
                try { File.Delete(outputPath); } catch { /* best-effort cleanup */ }
                throw;
            }
        }).ConfigureAwait(false);

        progress?.Report(Localization.LocalizationManager.Instance["FormatConv_ProgressDone"]);
    }

    private static async Task FixNesHeaderAsync(string inputPath, string outputPath, IProgress<string>? progress)
    {
        progress?.Report(Localization.LocalizationManager.Instance["FormatConv_ProgressFixingNes"]);

        long fileSize = new FileInfo(inputPath).Length;
        if (fileSize > 64 * 1024 * 1024)
            throw new InvalidOperationException(
                string.Format(Localization.LocalizationManager.Instance["FormatConv_ErrorTooLargeNes"], fileSize / (1024.0 * 1024)));

        await Task.Run(() =>
        {
            byte[] data = File.ReadAllBytes(inputPath);

            if (data.Length < 16 || data[0] != 0x4E || data[1] != 0x45 || data[2] != 0x53 || data[3] != 0x1A)
                throw new InvalidOperationException(Localization.LocalizationManager.Instance["FormatConv_ErrorNotNes"]);

            // Clear unused header bytes (bytes 8-15 are often dirty)
            bool isNes2 = (data[7] & 0x0C) == 0x08;
            if (!isNes2)
            {
                for (int i = 8; i < 16; i++)
                    data[i] = 0;
            }

            try
            {
                File.WriteAllBytes(outputPath, data);
            }
            catch
            {
                try { File.Delete(outputPath); } catch { /* best-effort cleanup */ }
                throw;
            }
        }).ConfigureAwait(false);

        progress?.Report(Localization.LocalizationManager.Instance["FormatConv_ProgressDone"]);
    }

    /// <summary>
    /// Converts a disc image to CHD (Compressed Hunks of Data) format using chdman.
    /// Requires chdman to be installed and available in the system PATH.
    /// </summary>
    private static async Task ConvertToChdAsync(string inputPath, string outputPath, IProgress<string>? progress)
    {
        string ext = Path.GetExtension(inputPath).ToLowerInvariant();
        string chdOutput = Path.ChangeExtension(outputPath, ".chd");

        progress?.Report(Localization.LocalizationManager.Instance["FormatConv_ProgressConvertCHD"]);

        string? chdmanPath = FindTool("chdman");
        if (chdmanPath == null)
            throw new InvalidOperationException(
                Localization.LocalizationManager.Instance["FormatConv_ErrorChdmanNotFound"]);

        string args = ext == ".cue"
            ? $"createcd -i \"{inputPath}\" -o \"{chdOutput}\""
            : $"createraw -i \"{inputPath}\" -o \"{chdOutput}\" -us 2048";

        try
        {
            await RunExternalToolAsync(chdmanPath, args, progress).ConfigureAwait(false);
        }
        catch
        {
            try { File.Delete(chdOutput); } catch { /* best-effort cleanup */ }
            throw;
        }

        progress?.Report(Localization.LocalizationManager.Instance["FormatConv_ProgressDone"]);
    }

    /// <summary>
    /// Converts a disc/ROM image to RVZ (RVZ Compressed Image) format using DolphinTool.
    /// Requires DolphinTool to be installed and available in the system PATH.
    /// </summary>
    private static async Task ConvertToRvzAsync(string inputPath, string outputPath, IProgress<string>? progress)
    {
        string rvzOutput = Path.ChangeExtension(outputPath, ".rvz");

        progress?.Report(Localization.LocalizationManager.Instance["FormatConv_ProgressConvertRVZ"]);

        string? dolphinToolPath = FindTool("DolphinTool");
        if (dolphinToolPath == null)
            throw new InvalidOperationException(
                Localization.LocalizationManager.Instance["FormatConv_ErrorDolphinToolNotFound"]);

        string args = $"convert -i \"{inputPath}\" -o \"{rvzOutput}\" -f rvz -b 131072 -c zstd -l 5";

        try
        {
            await RunExternalToolAsync(dolphinToolPath, args, progress).ConfigureAwait(false);
        }
        catch
        {
            try { File.Delete(rvzOutput); } catch { /* best-effort cleanup */ }
            throw;
        }

        progress?.Report(Localization.LocalizationManager.Instance["FormatConv_ProgressDone"]);
    }

    internal static string? FindTool(string toolName)
    {
        // Try to find the tool in the system PATH
        string fileName = OperatingSystem.IsWindows() ? toolName + ".exe" : toolName;
        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (pathEnv == null)
            return null;

        char separator = OperatingSystem.IsWindows() ? ';' : ':';
        foreach (string dir in pathEnv.Split(separator, StringSplitOptions.RemoveEmptyEntries))
        {
            string fullPath = Path.Combine(dir, fileName);
            if (File.Exists(fullPath))
                return fullPath;
        }

        return null;
    }

    internal static async Task RunExternalToolAsync(string toolPath, string arguments, IProgress<string>? progress)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = toolPath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start {Path.GetFileName(toolPath)}.");

        string stdout = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        string stderr = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            string errorMsg = !string.IsNullOrWhiteSpace(stderr) ? stderr.Trim() : stdout.Trim();
            throw new InvalidOperationException(
                string.Format(Localization.LocalizationManager.Instance["FormatConv_ErrorToolFailed"],
                    Path.GetFileName(toolPath), process.ExitCode, errorMsg));
        }

        if (!string.IsNullOrWhiteSpace(stdout))
            progress?.Report(stdout.Trim());
    }
}

public class BatchConversionResult
{
    public int Converted { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }

    public string Summary =>
        string.Format(Localization.LocalizationManager.Instance["FormatConv_BatchSummary"], Converted, Skipped, Failed);
}
