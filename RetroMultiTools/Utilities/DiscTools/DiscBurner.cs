using System.Runtime.InteropServices;
using RetroMultiTools.Localization;

namespace RetroMultiTools.Utilities.DiscTools;

/// <summary>
/// Cross-platform disc burning and image creation utility.
/// Uses cdrecord/wodim on Linux, hdiutil/drutil on macOS, and
/// the IMAPI2 COM interface (via PowerShell) on Windows.
/// </summary>
public static class DiscBurner
{
    /// <summary>
    /// Known disc image extensions from retro gaming systems.
    /// </summary>
    public static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Standard disc images
        ".iso", ".bin", ".cue", ".img", ".mdf", ".mds",
        // CD-based systems (PlayStation, Sega CD, Saturn, TurboGrafx-CD, Neo Geo CD, 3DO)
        ".ccd", ".sub", ".ecm", ".pbp", ".chd",
        // Dreamcast
        ".gdi", ".cdi",
        // GameCube / Wii
        ".gcm", ".rvz", ".wbfs", ".wia", ".nkit.iso", ".gcz",
        // Xbox / Xbox 360
        ".xiso",
        // NRG (Nero), CloneCD
        ".nrg", ".toast",
    };

    /// <summary>
    /// Image formats that can actually be burnt to a physical disc using standard tools.
    /// Excludes compressed, emulator-specific, or non-standard formats (CHD, PBP, ECM, WBFS, RVZ, etc.).
    /// </summary>
    public static readonly HashSet<string> BurnableImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".iso", ".bin", ".cue", ".img",
        ".nrg", ".toast",
    };

    /// <summary>
    /// File filter patterns for all disc image file pickers.
    /// </summary>
    public static readonly string[] ImageFilterPatterns =
    [
        "*.iso", "*.bin", "*.cue", "*.img", "*.mdf", "*.mds",
        "*.ccd", "*.sub", "*.ecm", "*.pbp", "*.chd",
        "*.gdi", "*.cdi",
        "*.gcm", "*.rvz", "*.wbfs", "*.wia", "*.gcz",
        "*.xiso",
        "*.nrg", "*.toast"
    ];

    /// <summary>
    /// File filter patterns for the burn-to-disc file picker.
    /// Only includes formats that standard burn tools can handle.
    /// </summary>
    public static readonly string[] BurnableImageFilterPatterns =
    [
        "*.iso", "*.bin", "*.cue", "*.img",
        "*.nrg", "*.toast"
    ];

    /// <summary>
    /// Returns a list of available optical disc devices on the system.
    /// </summary>
    public static async Task<List<DiscDriveInfo>> DetectDrivesAsync(IProgress<string>? progress = null)
    {
        progress?.Report(LocalizationManager.Instance["DiscTools_DetectingDrives"]);
        var drives = new List<DiscDriveInfo>();

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                drives = await DetectDrivesWindowsAsync();
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                drives = await DetectDrivesLinuxAsync();
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                drives = await DetectDrivesMacAsync();
        }
        catch (Exception ex)
        {
            progress?.Report(string.Format(LocalizationManager.Instance["DiscTools_DriveDetectError"], ex.Message));
        }

        return drives;
    }

    /// <summary>
    /// Writes a disc image file to the selected optical drive.
    /// </summary>
    public static async Task<DiscOperationResult> BurnImageAsync(
        string imagePath, string deviceId, int speed, bool verify,
        IProgress<string>? progress = null, CancellationToken ct = default)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException(LocalizationManager.Instance["DiscTools_ImageNotFound"], imagePath);

        if (!ProcessHelper.IsValidDevicePath(deviceId))
            return DiscOperationResult.Failure(string.Format(
                LocalizationManager.Instance["Common_ErrorFormat"], "Invalid device identifier."));

        string ext = Path.GetExtension(imagePath);

        if (!BurnableImageExtensions.Contains(ext))
            return DiscOperationResult.Failure(string.Format(
                LocalizationManager.Instance["DiscTools_UnsupportedImageFormat"], ext));

        progress?.Report(string.Format(LocalizationManager.Instance["DiscTools_BurningImage"], Path.GetFileName(imagePath)));

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return await BurnImageWindowsAsync(imagePath, deviceId, speed, verify, progress, ct);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return await BurnImageLinuxAsync(imagePath, ext, deviceId, speed, verify, progress, ct);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return await BurnImageMacAsync(imagePath, deviceId, speed, verify, progress, ct);
        else
            return DiscOperationResult.Failure(LocalizationManager.Instance["DiscTools_UnsupportedPlatform"]);
    }

    /// <summary>
    /// Writes files and folders to a disc using a data disc layout.
    /// </summary>
    public static async Task<DiscOperationResult> BurnFilesAsync(
        string[] paths, string deviceId, string volumeLabel, int speed,
        IProgress<string>? progress = null, CancellationToken ct = default)
    {
        if (paths.Length == 0)
            return DiscOperationResult.Failure(LocalizationManager.Instance["DiscTools_NoFilesSelected"]);

        if (!ProcessHelper.IsValidDevicePath(deviceId))
            return DiscOperationResult.Failure(string.Format(
                LocalizationManager.Instance["Common_ErrorFormat"], "Invalid device identifier."));

        progress?.Report(string.Format(LocalizationManager.Instance["DiscTools_BurningFiles"], paths.Length));

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return await BurnFilesWindowsAsync(paths, deviceId, volumeLabel, speed, progress, ct);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return await BurnFilesLinuxAsync(paths, deviceId, volumeLabel, speed, progress, ct);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return await BurnFilesMacAsync(paths, deviceId, volumeLabel, speed, progress, ct);
        else
            return DiscOperationResult.Failure(LocalizationManager.Instance["DiscTools_UnsupportedPlatform"]);
    }

    /// <summary>
    /// Creates an image file (ISO) from a physical disc.
    /// </summary>
    public static async Task<DiscOperationResult> CreateImageFromDiscAsync(
        string deviceId, string outputPath, IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(outputPath))
            throw new ArgumentException(LocalizationManager.Instance["DiscTools_OutputRequired"]);

        if (!ProcessHelper.IsValidDevicePath(deviceId))
            return DiscOperationResult.Failure(string.Format(
                LocalizationManager.Instance["Common_ErrorFormat"], "Invalid device identifier."));

        progress?.Report(LocalizationManager.Instance["DiscTools_ReadingDisc"]);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return await CreateImageFromDiscWindowsAsync(deviceId, outputPath, progress, ct);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return await CreateImageFromDiscLinuxAsync(deviceId, outputPath, progress, ct);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return await CreateImageFromDiscMacAsync(deviceId, outputPath, progress, ct);
        else
            return DiscOperationResult.Failure(LocalizationManager.Instance["DiscTools_UnsupportedPlatform"]);
    }

    /// <summary>
    /// Creates an ISO image from files and folders.
    /// </summary>
    public static async Task<DiscOperationResult> CreateImageFromFilesAsync(
        string[] paths, string outputPath, string volumeLabel,
        IProgress<string>? progress = null, CancellationToken ct = default)
    {
        if (paths.Length == 0)
            return DiscOperationResult.Failure(LocalizationManager.Instance["DiscTools_NoFilesSelected"]);
        if (string.IsNullOrEmpty(outputPath))
            throw new ArgumentException(LocalizationManager.Instance["DiscTools_OutputRequired"]);

        progress?.Report(string.Format(LocalizationManager.Instance["DiscTools_CreatingImage"], Path.GetFileName(outputPath)));

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return await CreateImageFromFilesWindowsAsync(paths, outputPath, volumeLabel, progress, ct);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return await CreateImageFromFilesLinuxAsync(paths, outputPath, volumeLabel, progress, ct);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return await CreateImageFromFilesMacAsync(paths, outputPath, volumeLabel, progress, ct);
        else
            return DiscOperationResult.Failure(LocalizationManager.Instance["DiscTools_UnsupportedPlatform"]);
    }

    // ── Drive Detection ─────────────────────────────────────────────────

    private static async Task<List<DiscDriveInfo>> DetectDrivesWindowsAsync()
    {
        var drives = new List<DiscDriveInfo>();
        // Use WMI via PowerShell to enumerate optical drives
        var (exitCode, output) = await RunProcessAsync("powershell", [
            "-NoProfile", "-Command",
            "Get-CimInstance Win32_CDROMDrive | ForEach-Object { \"$($_.Drive)|$($_.Caption)|$($_.MediaLoaded)\" }"
        ]);
        if (exitCode == 0 && !string.IsNullOrWhiteSpace(output))
        {
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = line.Split('|');
                if (parts.Length >= 3)
                {
                    drives.Add(new DiscDriveInfo
                    {
                        DeviceId = parts[0],
                        Name = parts[1],
                        HasMedia = string.Equals(parts[2], "True", StringComparison.OrdinalIgnoreCase)
                    });
                }
            }
        }
        return drives;
    }

    private static async Task<List<DiscDriveInfo>> DetectDrivesLinuxAsync()
    {
        var drives = new List<DiscDriveInfo>();

        // Check /sys/class/block for sr* devices (optical drives)
        const string blockDir = "/sys/class/block";
        if (Directory.Exists(blockDir))
        {
            foreach (var dir in Directory.GetDirectories(blockDir, "sr*"))
            {
                string devName = Path.GetFileName(dir);
                string devicePath = $"/dev/{devName}";

                string model = "Optical Drive";
                string modelFile = Path.Combine(dir, "device", "model");
                if (File.Exists(modelFile))
                    model = (await File.ReadAllTextAsync(modelFile)).Trim();

                drives.Add(new DiscDriveInfo
                {
                    DeviceId = devicePath,
                    Name = model,
                    HasMedia = File.Exists(devicePath)
                });
            }
        }

        // Fallback: try wodim --devices
        if (drives.Count == 0)
        {
            var (exitCode, output) = await RunProcessAsync("wodim", ["--devices"]);
            if (exitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    // Format: "wodim: 1  dev='/dev/sr0'rwrw-- : 'HL-DT-ST' 'DVDRAM GH24NSC0'"
                    int devStart = line.IndexOf("dev='", StringComparison.Ordinal);
                    if (devStart < 0) continue;
                    devStart += 5;
                    int devEnd = line.IndexOf('\'', devStart);
                    if (devEnd < 0) continue;
                    string dev = line[devStart..devEnd];

                    int nameStart = line.LastIndexOf('\'');
                    string name = nameStart > devEnd
                        ? line[(devEnd + 1)..].Replace("'", "").Trim().TrimStart(':', ' ')
                        : "Optical Drive";

                    drives.Add(new DiscDriveInfo { DeviceId = dev, Name = name, HasMedia = true });
                }
            }
        }

        return drives;
    }

    private static async Task<List<DiscDriveInfo>> DetectDrivesMacAsync()
    {
        var drives = new List<DiscDriveInfo>();
        var (exitCode, output) = await RunProcessAsync("drutil", ["list"]);
        if (exitCode == 0 && !string.IsNullOrWhiteSpace(output))
        {
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                // drutil list output format varies; parse lines with device info
                string trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("Vendor", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Look for lines starting with a number (device index)
                // drutil list format: "  1  VENDOR   PRODUCT   FW"
                if (char.IsDigit(trimmed[0]))
                {
                    // Extract the device index from the beginning of the line
                    int spaceIdx = trimmed.IndexOf(' ');
                    string deviceIndex = spaceIdx > 0 ? trimmed[..spaceIdx] : trimmed;
                    string deviceName = spaceIdx > 0 ? trimmed[spaceIdx..].Trim() : trimmed;

                    drives.Add(new DiscDriveInfo
                    {
                        DeviceId = deviceIndex,
                        Name = deviceName,
                        HasMedia = true
                    });
                }
            }
        }

        // If no drives from drutil, try diskutil
        if (drives.Count == 0)
        {
            var (ec2, out2) = await RunProcessAsync("diskutil", ["list"]);
            if (ec2 == 0 && !string.IsNullOrWhiteSpace(out2))
            {
                foreach (var line in out2.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (line.Contains("optical", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("CD/DVD", StringComparison.OrdinalIgnoreCase))
                    {
                        drives.Add(new DiscDriveInfo
                        {
                            DeviceId = line.Trim().Split(' ')[0],
                            Name = line.Trim(),
                            HasMedia = true
                        });
                    }
                }
            }
        }

        return drives;
    }

    // ── Burn Image ──────────────────────────────────────────────────────

    private static async Task<DiscOperationResult> BurnImageWindowsAsync(
        string imagePath, string deviceId, int speed, bool verify,
        IProgress<string>? progress, CancellationToken ct)
    {
        // Use PowerShell with IMAPI2 COM for burning
        string psScript = $@"
$image = '{EscapePowerShellString(imagePath)}'
$drive = '{EscapePowerShellString(deviceId)}'
try {{
    $discMaster = New-Object -ComObject IMAPI2.MsftDiscMaster2
    $recorder = New-Object -ComObject IMAPI2.MsftDiscRecorder2
    $recorder.InitializeDiscRecorder($drive)
    $burnData = New-Object -ComObject IMAPI2.MsftDiscFormat2Data
    $burnData.Recorder = $recorder
    $burnData.ClientName = 'RetroMultiTools'
    if ({speed} -gt 0) {{ $burnData.SetWriteSpeed({speed}, $false) }}
    $stream = New-Object -ComObject ADODB.Stream
    $stream.Open()
    $stream.Type = 1
    $stream.LoadFromFile($image)
    $burnData.Write($stream)
    $stream.Close()
    Write-Output 'BURN_SUCCESS'
}} catch {{
    Write-Output ""BURN_ERROR:$($_.Exception.Message)""
}}";

        progress?.Report(LocalizationManager.Instance["DiscTools_WritingToDisc"]);
        var (exitCode, output) = await RunProcessAsync("powershell",
            ["-NoProfile", "-Command", psScript], ct);

        if (output.Contains("BURN_SUCCESS"))
        {
            if (verify)
            {
                progress?.Report(LocalizationManager.Instance["DiscTools_Verifying"]);
                var verifyResult = await VerifyBurnedDiscAsync(imagePath, deviceId, progress, ct);
                if (verifyResult != null)
                    return verifyResult;
            }
            return DiscOperationResult.Success(LocalizationManager.Instance["DiscTools_BurnComplete"]);
        }

        string error = output.Contains("BURN_ERROR:")
            ? output[(output.IndexOf("BURN_ERROR:", StringComparison.Ordinal) + 11)..]
            : output;
        return DiscOperationResult.Failure(string.Format(
            LocalizationManager.Instance["DiscTools_BurnFailed"], error.Trim()));
    }

    private static async Task<DiscOperationResult> BurnImageLinuxAsync(
        string imagePath, string ext, string deviceId, int speed, bool verify,
        IProgress<string>? progress, CancellationToken ct)
    {
        // Prefer xorrecord, fall back to wodim/cdrecord
        string burner = await FindFirstToolAsync(
            LocalizationManager.Instance["DiscTools_NoBurnTool"],
            "xorrecord", "wodim", "cdrecord");

        var args = new List<string>();

        if (speed > 0)
            args.Add($"speed={speed}");

        args.Add($"dev={deviceId}");
        args.Add("-v");
        args.Add("-dao");

        // Determine burn mode by extension
        bool isCueSheet = string.Equals(ext, ".cue", StringComparison.OrdinalIgnoreCase);
        if (isCueSheet)
            args.Add($"cuefile={imagePath}");
        else
        {
            args.Add("-data");
            args.Add(imagePath);
        }

        progress?.Report(string.Format(LocalizationManager.Instance["DiscTools_RunningTool"], Path.GetFileName(burner)));
        var (exitCode, output) = await RunProcessAsync(burner, [.. args], ct);

        if (exitCode != 0)
            return DiscOperationResult.Failure(string.Format(
                LocalizationManager.Instance["DiscTools_BurnFailed"], output.Trim()));

        // Post-burn verification: re-read the disc and compare checksum with the source image
        if (verify && !isCueSheet)
        {
            progress?.Report(LocalizationManager.Instance["DiscTools_Verifying"]);
            var verifyResult = await VerifyBurnedDiscAsync(imagePath, deviceId, progress, ct);
            if (verifyResult != null)
                return verifyResult;
        }

        return DiscOperationResult.Success(LocalizationManager.Instance["DiscTools_BurnComplete"]);
    }

    private static async Task<DiscOperationResult> BurnImageMacAsync(
        string imagePath, string deviceId, int speed, bool verify,
        IProgress<string>? progress, CancellationToken ct)
    {
        // hdiutil burn for ISO/IMG, drutil burn for other formats
        string ext = Path.GetExtension(imagePath);
        bool useHdiutil = string.Equals(ext, ".iso", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(ext, ".img", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(ext, ".dmg", StringComparison.OrdinalIgnoreCase);

        if (useHdiutil)
        {
            var args = new List<string> { "burn", imagePath };
            if (speed > 0)
                args.AddRange(["-speed", speed.ToString()]);
            if (verify)
                args.Add("-verifyburn");
            else
                args.Add("-noverifydisc");

            progress?.Report(string.Format(LocalizationManager.Instance["DiscTools_RunningTool"], "hdiutil"));
            var (exitCode, output) = await RunProcessAsync("hdiutil", [.. args], ct);

            return exitCode == 0
                ? DiscOperationResult.Success(LocalizationManager.Instance["DiscTools_BurnComplete"])
                : DiscOperationResult.Failure(string.Format(
                    LocalizationManager.Instance["DiscTools_BurnFailed"], output.Trim()));
        }
        else
        {
            // Use drutil for CUE/BIN based images
            var args = new List<string> { "burn" };
            if (!verify)
                args.Add("-noverify");
            args.Add(imagePath);

            progress?.Report(string.Format(LocalizationManager.Instance["DiscTools_RunningTool"], "drutil"));
            var (exitCode, output) = await RunProcessAsync("drutil", [.. args], ct);

            return exitCode == 0
                ? DiscOperationResult.Success(LocalizationManager.Instance["DiscTools_BurnComplete"])
                : DiscOperationResult.Failure(string.Format(
                    LocalizationManager.Instance["DiscTools_BurnFailed"], output.Trim()));
        }
    }

    // ── Burn Files/Folders ──────────────────────────────────────────────

    private static async Task<DiscOperationResult> BurnFilesWindowsAsync(
        string[] paths, string deviceId, string volumeLabel, int speed,
        IProgress<string>? progress, CancellationToken ct)
    {
        // Create an ISO first using mkisofs-style approach via PowerShell IMAPI2
        string psScript = $@"
$paths = @({string.Join(",", paths.Select(p => $"'{EscapePowerShellString(p)}'"))})
$drive = '{EscapePowerShellString(deviceId)}'
$label = '{EscapePowerShellString(volumeLabel)}'
try {{
    $discMaster = New-Object -ComObject IMAPI2.MsftDiscMaster2
    $recorder = New-Object -ComObject IMAPI2.MsftDiscRecorder2
    $recorder.InitializeDiscRecorder($drive)
    $burnData = New-Object -ComObject IMAPI2.MsftDiscFormat2Data
    $burnData.Recorder = $recorder
    $burnData.ClientName = 'RetroMultiTools'
    if ({speed} -gt 0) {{ $burnData.SetWriteSpeed({speed}, $false) }}
    $fsi = New-Object -ComObject IMAPI2FS.MsftFileSystemImage
    $fsi.VolumeName = $label
    $fsi.ChooseImageDefaultsForMediaType($burnData.CurrentMediaType)
    foreach ($p in $paths) {{
        if (Test-Path $p -PathType Container) {{
            $fsi.Root.AddTree($p, $false)
        }} else {{
            $stream = New-Object -ComObject ADODB.Stream
            $stream.Open()
            $stream.Type = 1
            $stream.LoadFromFile($p)
            $fsi.Root.AddItem((Split-Path $p -Leaf), $stream)
            $stream.Close()
        }}
    }}
    $result = $fsi.CreateResultImage()
    $burnData.Write($result.ImageStream)
    Write-Output 'BURN_SUCCESS'
}} catch {{
    Write-Output ""BURN_ERROR:$($_.Exception.Message)""
}}";

        progress?.Report(LocalizationManager.Instance["DiscTools_WritingToDisc"]);
        var (exitCode, output) = await RunProcessAsync("powershell",
            ["-NoProfile", "-Command", psScript], ct);

        if (output.Contains("BURN_SUCCESS"))
            return DiscOperationResult.Success(LocalizationManager.Instance["DiscTools_BurnComplete"]);

        string error = output.Contains("BURN_ERROR:")
            ? output[(output.IndexOf("BURN_ERROR:", StringComparison.Ordinal) + 11)..]
            : output;
        return DiscOperationResult.Failure(string.Format(
            LocalizationManager.Instance["DiscTools_BurnFailed"], error.Trim()));
    }

    private static async Task<DiscOperationResult> BurnFilesLinuxAsync(
        string[] paths, string deviceId, string volumeLabel, int speed,
        IProgress<string>? progress, CancellationToken ct)
    {
        // First create an ISO with xorrisofs/genisoimage/mkisofs, then burn
        string isoTool = await FindFirstToolAsync(
            LocalizationManager.Instance["DiscTools_NoIsoTool"],
            "xorrisofs", "genisoimage", "mkisofs");

        string tempIso = Path.Combine(Path.GetTempPath(), $"retromultitools_burn_{Guid.NewGuid():N}.iso");

        try
        {
            // Build ISO
            var isoArgs = new List<string>
            {
                "-o", tempIso,
                "-V", volumeLabel,
                "-J",       // Joliet extensions
                "-r",       // Rock Ridge extensions
                "-iso-level", "3"
            };
            isoArgs.AddRange(paths);

            progress?.Report(string.Format(LocalizationManager.Instance["DiscTools_RunningTool"], Path.GetFileName(isoTool)));
            var (isoExit, isoOutput) = await RunProcessAsync(isoTool, [.. isoArgs], ct);
            if (isoExit != 0)
                return DiscOperationResult.Failure(string.Format(
                    LocalizationManager.Instance["DiscTools_IsoCreateFailed"], isoOutput.Trim()));

            // Now burn the ISO
            return await BurnImageLinuxAsync(tempIso, ".iso", deviceId, speed, false, progress, ct);
        }
        finally
        {
            try { File.Delete(tempIso); } catch { /* best-effort cleanup */ }
        }
    }

    private static async Task<DiscOperationResult> BurnFilesMacAsync(
        string[] paths, string deviceId, string volumeLabel, int speed,
        IProgress<string>? progress, CancellationToken ct)
    {
        // Create ISO with hdiutil, then burn
        string tempIso = Path.Combine(Path.GetTempPath(), $"retromultitools_burn_{Guid.NewGuid():N}.iso");
        string tempDir = Path.Combine(Path.GetTempPath(), $"retromultitools_stage_{Guid.NewGuid():N}");

        try
        {
            // Stage files to a temp directory
            Directory.CreateDirectory(tempDir);
            foreach (var p in paths)
            {
                ct.ThrowIfCancellationRequested();
                if (Directory.Exists(p))
                    ProcessHelper.CopyDirectoryRecursive(p, Path.Combine(tempDir, Path.GetFileName(p)), ct);
                else if (File.Exists(p))
                    File.Copy(p, Path.Combine(tempDir, Path.GetFileName(p)), overwrite: true);
            }

            // Create ISO using hdiutil
            progress?.Report(string.Format(LocalizationManager.Instance["DiscTools_RunningTool"], "hdiutil"));
            var (isoExit, isoOutput) = await RunProcessAsync("hdiutil",
                ["makehybrid", "-iso", "-joliet", "-default-volume-name", volumeLabel, "-o", tempIso, tempDir], ct);

            if (isoExit != 0)
                return DiscOperationResult.Failure(string.Format(
                    LocalizationManager.Instance["DiscTools_IsoCreateFailed"], isoOutput.Trim()));

            // Burn the ISO
            return await BurnImageMacAsync(tempIso, deviceId, speed, false, progress, ct);
        }
        finally
        {
            try { File.Delete(tempIso); } catch { /* best-effort cleanup */ }
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { /* best-effort cleanup */ }
        }
    }

    // ── Create Image from Disc ──────────────────────────────────────────

    private static async Task<DiscOperationResult> CreateImageFromDiscWindowsAsync(
        string deviceId, string outputPath, IProgress<string>? progress,
        CancellationToken ct)
    {
        string psScript = $@"
$drive = '{EscapePowerShellString(deviceId)}'
$output = '{EscapePowerShellString(outputPath)}'
try {{
    $driveInfo = Get-CimInstance Win32_CDROMDrive | Where-Object {{ $_.Drive -eq $drive }} | Select-Object -First 1
    if (-not $driveInfo -or -not $driveInfo.MediaLoaded) {{
        Write-Output 'IMAGE_ERROR:No disc in drive.'
        return
    }}
    $devicePath = '\\.\' + $drive
    $source = [System.IO.File]::OpenRead($devicePath)
    $dest = [System.IO.File]::Create($output)
    $buffer = New-Object byte[] (1MB)
    $total = 0
    while (($read = $source.Read($buffer, 0, $buffer.Length)) -gt 0) {{
        $dest.Write($buffer, 0, $read)
        $total += $read
    }}
    $source.Close()
    $dest.Close()
    Write-Output ""IMAGE_SUCCESS:$total""
}} catch {{
    Write-Output ""IMAGE_ERROR:$($_.Exception.Message)""
}}";

        progress?.Report(LocalizationManager.Instance["DiscTools_ReadingDisc"]);
        var (exitCode, output) = await RunProcessAsync("powershell",
            ["-NoProfile", "-Command", psScript], ct);

        if (output.Contains("IMAGE_SUCCESS"))
        {
            string sizeStr = output[(output.IndexOf("IMAGE_SUCCESS:", StringComparison.Ordinal) + 14)..].Trim();
            return DiscOperationResult.Success(string.Format(
                LocalizationManager.Instance["DiscTools_ImageCreated"], outputPath,
                FormatSize(long.TryParse(sizeStr, out long sz) ? sz : 0)));
        }

        string error = output.Contains("IMAGE_ERROR:")
            ? output[(output.IndexOf("IMAGE_ERROR:", StringComparison.Ordinal) + 12)..]
            : output;
        return DiscOperationResult.Failure(string.Format(
            LocalizationManager.Instance["DiscTools_ImageCreateFailed"], error.Trim()));
    }

    private static async Task<DiscOperationResult> CreateImageFromDiscLinuxAsync(
        string deviceId, string outputPath, IProgress<string>? progress,
        CancellationToken ct)
    {
        // Use dd to read disc to ISO
        progress?.Report(string.Format(LocalizationManager.Instance["DiscTools_RunningTool"], "dd"));
        var (exitCode, output) = await RunProcessAsync("dd",
            [$"if={deviceId}", $"of={outputPath}", "bs=2048", "conv=noerror,sync", "status=progress"], ct);

        if (exitCode == 0)
        {
            long fileSize = File.Exists(outputPath) ? new FileInfo(outputPath).Length : 0;
            return DiscOperationResult.Success(string.Format(
                LocalizationManager.Instance["DiscTools_ImageCreated"], outputPath, FormatSize(fileSize)));
        }

        return DiscOperationResult.Failure(string.Format(
            LocalizationManager.Instance["DiscTools_ImageCreateFailed"], output.Trim()));
    }

    private static async Task<DiscOperationResult> CreateImageFromDiscMacAsync(
        string deviceId, string outputPath, IProgress<string>? progress,
        CancellationToken ct)
    {
        // Use dd on macOS; the device will be /dev/diskN
        string diskDevice = deviceId.StartsWith("/dev/", StringComparison.Ordinal) ? deviceId : $"/dev/disk{deviceId}";

        progress?.Report(string.Format(LocalizationManager.Instance["DiscTools_RunningTool"], "dd"));
        var (exitCode, output) = await RunProcessAsync("dd",
            [$"if={diskDevice}", $"of={outputPath}", "bs=2048", "conv=noerror,sync"], ct);

        if (exitCode == 0)
        {
            long fileSize = File.Exists(outputPath) ? new FileInfo(outputPath).Length : 0;
            return DiscOperationResult.Success(string.Format(
                LocalizationManager.Instance["DiscTools_ImageCreated"], outputPath, FormatSize(fileSize)));
        }

        return DiscOperationResult.Failure(string.Format(
            LocalizationManager.Instance["DiscTools_ImageCreateFailed"], output.Trim()));
    }

    // ── Create Image from Files ─────────────────────────────────────────

    private static async Task<DiscOperationResult> CreateImageFromFilesWindowsAsync(
        string[] paths, string outputPath, string volumeLabel,
        IProgress<string>? progress, CancellationToken ct)
    {
        // Use PowerShell with IMAPI2 to create an ISO image
        string psScript = $@"
$paths = @({string.Join(",", paths.Select(p => $"'{EscapePowerShellString(p)}'"))})
$output = '{EscapePowerShellString(outputPath)}'
$label = '{EscapePowerShellString(volumeLabel)}'
try {{
    $fsi = New-Object -ComObject IMAPI2FS.MsftFileSystemImage
    $fsi.VolumeName = $label
    $fsi.FileSystemsToCreate = 3  # ISO9660 + Joliet
    foreach ($p in $paths) {{
        if (Test-Path $p -PathType Container) {{
            $fsi.Root.AddTree($p, $false)
        }} else {{
            $stream = New-Object -ComObject ADODB.Stream
            $stream.Open()
            $stream.Type = 1
            $stream.LoadFromFile($p)
            $fsi.Root.AddItem((Split-Path $p -Leaf), $stream)
            $stream.Close()
        }}
    }}
    $result = $fsi.CreateResultImage()
    $resultStream = $result.ImageStream
    $fileStream = [System.IO.File]::Create($output)
    $buffer = New-Object byte[] (1MB)
    while (($read = $resultStream.Read($buffer, 0, $buffer.Length)) -gt 0) {{
        $fileStream.Write($buffer, 0, $read)
    }}
    $fileStream.Close()
    $fi = Get-Item $output
    Write-Output ""IMAGE_SUCCESS:$($fi.Length)""
}} catch {{
    Write-Output ""IMAGE_ERROR:$($_.Exception.Message)""
}}";

        progress?.Report(LocalizationManager.Instance["DiscTools_CreatingIso"]);
        var (exitCode, output) = await RunProcessAsync("powershell",
            ["-NoProfile", "-Command", psScript], ct);

        if (output.Contains("IMAGE_SUCCESS"))
        {
            string sizeStr = output[(output.IndexOf("IMAGE_SUCCESS:", StringComparison.Ordinal) + 14)..].Trim();
            return DiscOperationResult.Success(string.Format(
                LocalizationManager.Instance["DiscTools_ImageCreated"], outputPath,
                FormatSize(long.TryParse(sizeStr, out long sz) ? sz : 0)));
        }

        string error = output.Contains("IMAGE_ERROR:")
            ? output[(output.IndexOf("IMAGE_ERROR:", StringComparison.Ordinal) + 12)..]
            : output;
        return DiscOperationResult.Failure(string.Format(
            LocalizationManager.Instance["DiscTools_ImageCreateFailed"], error.Trim()));
    }

    private static async Task<DiscOperationResult> CreateImageFromFilesLinuxAsync(
        string[] paths, string outputPath, string volumeLabel,
        IProgress<string>? progress, CancellationToken ct)
    {
        string isoTool = await FindFirstToolAsync(
            LocalizationManager.Instance["DiscTools_NoIsoTool"],
            "xorrisofs", "genisoimage", "mkisofs");

        var args = new List<string>
        {
            "-o", outputPath,
            "-V", volumeLabel,
            "-J",       // Joliet extensions
            "-r",       // Rock Ridge extensions
            "-iso-level", "3"
        };
        args.AddRange(paths);

        progress?.Report(string.Format(LocalizationManager.Instance["DiscTools_RunningTool"], Path.GetFileName(isoTool)));
        var (exitCode, output) = await RunProcessAsync(isoTool, [.. args], ct);

        if (exitCode == 0)
        {
            long fileSize = File.Exists(outputPath) ? new FileInfo(outputPath).Length : 0;
            return DiscOperationResult.Success(string.Format(
                LocalizationManager.Instance["DiscTools_ImageCreated"], outputPath, FormatSize(fileSize)));
        }

        return DiscOperationResult.Failure(string.Format(
            LocalizationManager.Instance["DiscTools_ImageCreateFailed"], output.Trim()));
    }

    private static async Task<DiscOperationResult> CreateImageFromFilesMacAsync(
        string[] paths, string outputPath, string volumeLabel,
        IProgress<string>? progress, CancellationToken ct)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"retromultitools_iso_{Guid.NewGuid():N}");

        try
        {
            // Stage files
            Directory.CreateDirectory(tempDir);
            foreach (var p in paths)
            {
                ct.ThrowIfCancellationRequested();
                if (Directory.Exists(p))
                    ProcessHelper.CopyDirectoryRecursive(p, Path.Combine(tempDir, Path.GetFileName(p)), ct);
                else if (File.Exists(p))
                    File.Copy(p, Path.Combine(tempDir, Path.GetFileName(p)), overwrite: true);
            }

            progress?.Report(string.Format(LocalizationManager.Instance["DiscTools_RunningTool"], "hdiutil"));
            var (exitCode, output) = await RunProcessAsync("hdiutil",
                ["makehybrid", "-iso", "-joliet", "-default-volume-name", volumeLabel, "-o", outputPath, tempDir], ct);

            if (exitCode == 0)
            {
                long fileSize = File.Exists(outputPath) ? new FileInfo(outputPath).Length : 0;
                return DiscOperationResult.Success(string.Format(
                    LocalizationManager.Instance["DiscTools_ImageCreated"], outputPath, FormatSize(fileSize)));
            }

            return DiscOperationResult.Failure(string.Format(
                LocalizationManager.Instance["DiscTools_ImageCreateFailed"], output.Trim()));
        }
        finally
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { /* best-effort cleanup */ }
        }
    }

    // ── Verification ────────────────────────────────────────────────────

    /// <summary>
    /// Verifies a burned disc by reading it back and comparing a checksum against the source image.
    /// Returns null on success, or a DiscOperationResult on failure/skip.
    /// </summary>
    private static async Task<DiscOperationResult?> VerifyBurnedDiscAsync(
        string sourceImagePath, string deviceId, IProgress<string>? progress,
        CancellationToken ct)
    {
        try
        {
            // Compute source image checksum
            progress?.Report(LocalizationManager.Instance["DiscTools_Verifying"]);
            long sourceSize = new FileInfo(sourceImagePath).Length;

            byte[] sourceHash;
            using (var sourceStream = File.OpenRead(sourceImagePath))
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                sourceHash = await sha.ComputeHashAsync(sourceStream, ct);
            }

            // Read the same number of bytes from the disc device and compute checksum
            string discDevice;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // On Windows, read from \\.\X: device path
                discDevice = deviceId.Contains(@"\\") ? deviceId : @"\\.\" + deviceId;

                // Use PowerShell to read and hash the disc
                string verifyScript = $@"
$devicePath = '{EscapePowerShellString(discDevice)}'
$sourceSize = {sourceSize}
try {{
    $source = [System.IO.File]::OpenRead($devicePath)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $buffer = New-Object byte[] (1MB)
    $remaining = $sourceSize
    while ($remaining -gt 0) {{
        $toRead = [Math]::Min($buffer.Length, $remaining)
        $read = $source.Read($buffer, 0, $toRead)
        if ($read -eq 0) {{ break }}
        $sha.TransformBlock($buffer, 0, $read, $buffer, 0) | Out-Null
        $remaining -= $read
    }}
    $sha.TransformFinalBlock([byte[]]::new(0), 0, 0) | Out-Null
    $source.Close()
    $hash = [BitConverter]::ToString($sha.Hash).Replace('-','')
    Write-Output ""VERIFY_HASH:$hash""
}} catch {{
    Write-Output ""VERIFY_ERROR:$($_.Exception.Message)""
}}";
                var (exitCode, output) = await RunProcessAsync("powershell",
                    ["-NoProfile", "-Command", verifyScript], ct);

                if (output.Contains("VERIFY_HASH:"))
                {
                    string discHash = output[(output.IndexOf("VERIFY_HASH:", StringComparison.Ordinal) + 12)..].Trim();
                    string sourceHex = Convert.ToHexString(sourceHash);
                    if (string.Equals(discHash, sourceHex, StringComparison.OrdinalIgnoreCase))
                        return null; // verification passed
                    return DiscOperationResult.Failure(LocalizationManager.Instance["DiscTools_VerifyFailed"]);
                }

                return DiscOperationResult.Success(LocalizationManager.Instance["DiscTools_BurnCompleteVerifySkipped"]);
            }
            else
            {
                // On Linux/macOS, read from the device using dd and hash in-process.
                // This avoids shell interpolation (no bash -c with string formatting).
                discDevice = deviceId.StartsWith("/dev/", StringComparison.Ordinal) ? deviceId : $"/dev/{deviceId}";

                if (!ProcessHelper.IsValidDevicePath(discDevice))
                    return DiscOperationResult.Success(LocalizationManager.Instance["DiscTools_BurnCompleteVerifySkipped"]);

                // Use ceiling division to ensure the last partial sector is included
                long blockCount = (sourceSize + 2047) / 2048;

                // Read disc data via dd (arguments passed safely, no shell) and
                // compute the SHA-256 hash in managed code.
                using var ddProc = new System.Diagnostics.Process();
                ddProc.StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "dd",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                ddProc.StartInfo.ArgumentList.Add($"if={discDevice}");
                ddProc.StartInfo.ArgumentList.Add("bs=2048");
                ddProc.StartInfo.ArgumentList.Add($"count={blockCount}");

                ddProc.Start();
                // dd writes transfer statistics to stderr; discard it via
                // BeginErrorReadLine so the pipe buffer doesn't fill up and
                // block the process.  Only stdout (raw disc data) is needed.
                ddProc.BeginErrorReadLine();

                using var sha256 = System.Security.Cryptography.SHA256.Create();
                byte[] discHash;
                try
                {
                    discHash = await sha256.ComputeHashAsync(ddProc.StandardOutput.BaseStream, ct)
                        .ConfigureAwait(false);
                }
                finally
                {
                    try { await ddProc.WaitForExitAsync(ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { try { ddProc.Kill(true); } catch { } throw; }
                }

                string discHex = Convert.ToHexString(discHash);
                string sourceHex = Convert.ToHexString(sourceHash);
                if (string.Equals(discHex, sourceHex, StringComparison.OrdinalIgnoreCase))
                    return null; // verification passed
                return DiscOperationResult.Failure(LocalizationManager.Instance["DiscTools_VerifyFailed"]);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Verification tools may not be available or may fail for
            // permission / platform reasons.  The burn itself succeeded,
            // so report success with a note that verification was skipped.
            System.Diagnostics.Trace.WriteLine("[DiscBurner] Post-burn verification skipped due to unexpected error.");
            return DiscOperationResult.Success(LocalizationManager.Instance["DiscTools_BurnCompleteVerifySkipped"]);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Searches for the first available tool from the given list of tool names
    /// in the preferred order (first match wins).
    /// </summary>
    private static async Task<string> FindFirstToolAsync(string errorMessage, params string[] names)
    {
        foreach (var name in names)
        {
            var path = await FindToolAsync(name);
            if (path != null)
                return path;
        }
        throw new FileNotFoundException(errorMessage);
    }

    private static async Task<string?> FindToolAsync(string name)
    {
        try
        {
            string which = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where" : "which";
            var (exitCode, output) = await ProcessHelper.RunProcessAsync(which, [name]);
            if (exitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                string path = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                if (File.Exists(path))
                    return path;
            }
        }
        catch { /* tool not found */ }
        return null;
    }

    // Convenience wrappers for shared helpers
    private static Task<(int ExitCode, string Output)> RunProcessAsync(
        string fileName, string[] arguments, CancellationToken ct = default)
        => ProcessHelper.RunProcessAsync(fileName, arguments, ct);

    private static string EscapePowerShellString(string value)
        => ProcessHelper.EscapePowerShellString(value);

    private static string FormatSize(long bytes)
        => ProcessHelper.FormatSize(bytes);
}

/// <summary>
/// Information about a detected optical disc drive.
/// </summary>
public class DiscDriveInfo
{
    public string DeviceId { get; set; } = "";
    public string Name { get; set; } = "";
    public bool HasMedia { get; set; }

    public string DisplayName => string.IsNullOrEmpty(Name) ? DeviceId : $"{Name} ({DeviceId})";
}

/// <summary>
/// Result of a disc operation.
/// </summary>
public class DiscOperationResult
{
    public bool IsSuccess { get; init; }
    public string Message { get; init; } = "";

    public static DiscOperationResult Success(string message) =>
        new() { IsSuccess = true, Message = message };

    public static DiscOperationResult Failure(string message) =>
        new() { IsSuccess = false, Message = message };
}
