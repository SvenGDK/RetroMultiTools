using RetroMultiTools.Services;

namespace RetroMultiTools.Utilities.Mame;

/// <summary>
/// Compresses and decompresses MAME CHD (Compressed Hunks of Data) files using
/// the chdman external tool. Supports disc images (CUE/BIN/ISO/GDI) for compression
/// and CHD files for decompression to raw disc images.
/// </summary>
public static class MameChdConverter
{
    /// <summary>
    /// Supported disc image extensions that can be compressed to CHD.
    /// </summary>
    public static readonly HashSet<string> CompressibleExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cue", ".iso", ".bin", ".gdi", ".3do", ".cdi", ".toc"
    };

    /// <summary>
    /// Compresses a disc image to CHD format.
    /// </summary>
    public static async Task<ChdConvertResult> CompressAsync(
        string inputPath,
        string outputPath,
        ChdCompressOptions options,
        IProgress<string>? progress = null)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Input file not found.", inputPath);

        string ext = Path.GetExtension(inputPath).ToLowerInvariant();
        string chdOutput = Path.ChangeExtension(outputPath, ".chd");

        progress?.Report($"Compressing {Path.GetFileName(inputPath)} to CHD...");

        string chdmanPath = FindChdmanOrThrow();

        string command = GetCompressCommand(ext);
        string args = BuildCompressArgs(command, inputPath, chdOutput, options);

        var result = new ChdConvertResult
        {
            InputPath = inputPath,
            OutputPath = chdOutput,
            InputSize = new FileInfo(inputPath).Length
        };

        try
        {
            await RomFormatConverter.RunExternalToolAsync(chdmanPath, args, progress).ConfigureAwait(false);
            result.Success = true;

            if (File.Exists(chdOutput))
            {
                result.OutputSize = new FileInfo(chdOutput).Length;
                result.CompressionRatio = result.InputSize > 0
                    ? (double)result.OutputSize / result.InputSize
                    : 0;
            }

            progress?.Report("Compression complete.");
        }
        catch
        {
            result.Success = false;
            try { File.Delete(chdOutput); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw;
        }

        return result;
    }

    /// <summary>
    /// Decompresses a CHD file to a raw disc image.
    /// </summary>
    public static async Task<ChdConvertResult> DecompressAsync(
        string inputPath,
        string outputPath,
        ChdDecompressOptions options,
        IProgress<string>? progress = null)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("CHD file not found.", inputPath);

        // Ensure the output path uses the correct extension for the chosen format
        string correctedOutput = EnsureDecompressExtension(outputPath, options.OutputFormat);

        progress?.Report($"Decompressing {Path.GetFileName(inputPath)}...");

        string chdmanPath = FindChdmanOrThrow();

        string args = BuildDecompressArgs(inputPath, correctedOutput, options);

        var result = new ChdConvertResult
        {
            InputPath = inputPath,
            OutputPath = correctedOutput,
            InputSize = new FileInfo(inputPath).Length
        };

        try
        {
            await RomFormatConverter.RunExternalToolAsync(chdmanPath, args, progress).ConfigureAwait(false);
            result.Success = true;

            if (File.Exists(correctedOutput))
                result.OutputSize = new FileInfo(correctedOutput).Length;

            progress?.Report("Decompression complete.");
        }
        catch
        {
            result.Success = false;
            try { File.Delete(correctedOutput); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw;
        }

        return result;
    }

    /// <summary>
    /// Compresses all disc images in a directory to CHD format.
    /// </summary>
    public static async Task<ChdBatchConvertResult> CompressBatchAsync(
        string directory,
        string outputDirectory,
        ChdCompressOptions options,
        IProgress<string>? progress = null)
    {
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException($"Directory not found: {directory}");

        Directory.CreateDirectory(outputDirectory);

        var files = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .Where(f => CompressibleExtensions.Contains(Path.GetExtension(f)))
            .ToList();

        var result = new ChdBatchConvertResult();

        for (int i = 0; i < files.Count; i++)
        {
            string file = files[i];
            string fileName = Path.GetFileName(file);
            progress?.Report($"Compressing {i + 1} of {files.Count}: {fileName}...");

            string outputName = Path.ChangeExtension(fileName, ".chd");
            string outputPath = Path.Combine(outputDirectory, outputName);

            if (File.Exists(outputPath) && !options.Overwrite)
            {
                result.Skipped++;
                continue;
            }

            try
            {
                var convertResult = await CompressAsync(file, outputPath, options, null).ConfigureAwait(false);
                if (convertResult.Success)
                    result.Converted++;
                else
                    result.Failed++;
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
            {
                result.Failed++;
            }
        }

        progress?.Report($"Batch complete — {result.Summary}");
        return result;
    }

    /// <summary>
    /// Decompresses all CHD files in a directory.
    /// </summary>
    public static async Task<ChdBatchConvertResult> DecompressBatchAsync(
        string directory,
        string outputDirectory,
        ChdDecompressOptions options,
        IProgress<string>? progress = null)
    {
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException($"Directory not found: {directory}");

        Directory.CreateDirectory(outputDirectory);

        var files = Directory.EnumerateFiles(directory, "*.chd", SearchOption.TopDirectoryOnly).ToList();
        var result = new ChdBatchConvertResult();

        for (int i = 0; i < files.Count; i++)
        {
            string file = files[i];
            string fileName = Path.GetFileName(file);
            progress?.Report($"Decompressing {i + 1} of {files.Count}: {fileName}...");

            string outputExt = GetDecompressExtension(options.OutputFormat);
            string outputName = Path.ChangeExtension(fileName, outputExt);
            string outputPath = Path.Combine(outputDirectory, outputName);

            if (File.Exists(outputPath) && !options.Overwrite)
            {
                result.Skipped++;
                continue;
            }

            try
            {
                var convertResult = await DecompressAsync(file, outputPath, options, null).ConfigureAwait(false);
                if (convertResult.Success)
                    result.Converted++;
                else
                    result.Failed++;
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
            {
                result.Failed++;
            }
        }

        progress?.Report($"Batch complete — {result.Summary}");
        return result;
    }

    /// <summary>
    /// Returns CHD file information by reading the header.
    /// </summary>
    public static async Task<ChdFileInfo> GetFileInfoAsync(string filePath, IProgress<string>? progress = null)
    {
        progress?.Report("Reading CHD header...");
        var verifyResult = await MameChdVerifier.VerifyAsync(filePath, progress).ConfigureAwait(false);

        return new ChdFileInfo
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            FileSize = verifyResult.FileSize,
            Version = verifyResult.Version,
            Compression = verifyResult.Compression,
            LogicalSize = verifyResult.LogicalSize,
            HunkSize = verifyResult.HunkSize,
            SHA1 = verifyResult.SHA1,
            IsValid = verifyResult.IsValid,
            Error = verifyResult.Error
        };
    }

    private static string GetCompressCommand(string ext)
    {
        return ext switch
        {
            ".cue" or ".toc" => "createcd",
            ".gdi" => "createcd",
            _ => "createraw"
        };
    }

    private static string BuildCompressArgs(string command, string inputPath, string outputPath, ChdCompressOptions options)
    {
        var args = new List<string>
        {
            command,
            "-i", $"\"{inputPath}\"",
            "-o", $"\"{outputPath}\""
        };

        if (command == "createraw")
        {
            int unitSize = options.UnitSize > 0 ? options.UnitSize : 2048;
            args.Add("-us");
            args.Add(unitSize.ToString());
        }

        if (options.NumProcessors > 0)
        {
            args.Add("-np");
            args.Add(options.NumProcessors.ToString());
        }

        if (!string.IsNullOrEmpty(options.Compression))
        {
            args.Add("-c");
            args.Add(options.Compression);
        }

        if (options.Force)
            args.Add("-f");

        return string.Join(" ", args);
    }

    private static string BuildDecompressArgs(string inputPath, string outputPath, ChdDecompressOptions options)
    {
        string command = options.OutputFormat switch
        {
            ChdOutputFormat.Cue => "extractcd",
            ChdOutputFormat.Gdi => "extractcd",
            _ => "extractraw"
        };

        var args = new List<string>
        {
            command,
            "-i", $"\"{inputPath}\"",
            "-o", $"\"{outputPath}\""
        };

        if (options.Force)
            args.Add("-f");

        return string.Join(" ", args);
    }

    private static string GetDecompressExtension(ChdOutputFormat format) => format switch
    {
        ChdOutputFormat.Cue => ".cue",
        ChdOutputFormat.Gdi => ".gdi",
        _ => ".bin"
    };

    private static string EnsureDecompressExtension(string outputPath, ChdOutputFormat format)
    {
        string expected = GetDecompressExtension(format);
        string current = Path.GetExtension(outputPath);
        if (!current.Equals(expected, StringComparison.OrdinalIgnoreCase))
            return Path.ChangeExtension(outputPath, expected);
        return outputPath;
    }

    /// <summary>
    /// Locates chdman or throws with a descriptive error.
    /// Checks the configured MAME installation directory first (chdman ships with MAME),
    /// then falls back to the system PATH.
    /// </summary>
    private static string FindChdmanOrThrow()
    {
        string chdmanFileName = OperatingSystem.IsWindows() ? "chdman.exe" : "chdman";

        // 1. Check next to the configured/detected MAME executable
        string mamePath = MameLauncher.GetMameExecutablePath();
        if (!string.IsNullOrEmpty(mamePath))
        {
            string? mameDir = Path.GetDirectoryName(mamePath);
            if (!string.IsNullOrEmpty(mameDir))
            {
                string chdmanInMameDir = Path.Combine(mameDir, chdmanFileName);
                if (File.Exists(chdmanInMameDir))
                    return chdmanInMameDir;
            }
        }

        // 2. Check system PATH using the shared helper
        string? fromPath = RomFormatConverter.FindTool("chdman");
        if (fromPath != null)
            return fromPath;

        throw new InvalidOperationException(
            "chdman not found. Please install MAME tools (chdman) and ensure it is available in your system PATH, or configure your MAME path in Settings.");
    }
}

public class ChdCompressOptions
{
    public string Compression { get; set; } = "";
    public int NumProcessors { get; set; }
    public int UnitSize { get; set; } = 2048;
    public bool Force { get; set; }
    public bool Overwrite { get; set; }
}

public class ChdDecompressOptions
{
    public ChdOutputFormat OutputFormat { get; set; } = ChdOutputFormat.Bin;
    public bool Force { get; set; }
    public bool Overwrite { get; set; }
}

public enum ChdOutputFormat
{
    Bin,
    Cue,
    Gdi
}

public class ChdConvertResult
{
    public string InputPath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public long InputSize { get; set; }
    public long OutputSize { get; set; }
    public double CompressionRatio { get; set; }
    public bool Success { get; set; }

    public string Summary
    {
        get
        {
            string inputStr = FormatSize(InputSize);
            string outputStr = FormatSize(OutputSize);
            if (CompressionRatio > 0)
                return $"{inputStr} → {outputStr} ({CompressionRatio:P1} ratio)";
            return $"{inputStr} → {outputStr}";
        }
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / (double)(1L << 30):F2} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):F2} MB",
        >= 1L << 10 => $"{bytes / (double)(1L << 10):F2} KB",
        _ => $"{bytes} bytes"
    };
}

public class ChdBatchConvertResult
{
    public int Converted { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }

    public string Summary =>
        $"{Converted} converted, {Skipped} skipped, {Failed} failed";
}

public class ChdFileInfo
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public int Version { get; set; }
    public string Compression { get; set; } = string.Empty;
    public long LogicalSize { get; set; }
    public long HunkSize { get; set; }
    public string SHA1 { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public string Error { get; set; } = string.Empty;
}
