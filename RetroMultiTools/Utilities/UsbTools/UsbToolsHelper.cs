using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using RetroMultiTools.Localization;

namespace RetroMultiTools.Utilities.UsbTools;

/// <summary>
/// Cross-platform USB drive utility.
/// Provides USB detection, formatting, image writing/creation, and
/// file management capabilities.
/// Uses diskpart/PowerShell on Windows, udisks2/dd on Linux,
/// and diskutil/dd on macOS.
/// </summary>
public static class UsbToolsHelper
{
    /// <summary>
    /// Supported USB image extensions for writing to a USB drive.
    /// </summary>
    public static readonly HashSet<string> WritableImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".iso", ".img", ".bin", ".raw", ".image",
        ".dmg",
        ".dd", ".hdd",
        ".wbfs",
        ".xiso",
    };

    /// <summary>
    /// File filter patterns for image file pickers.
    /// </summary>
    public static readonly string[] ImageFilterPatterns =
    [
        "*.iso", "*.img", "*.bin", "*.raw", "*.image",
        "*.dmg",
        "*.dd", "*.hdd",
        "*.wbfs",
        "*.xiso"
    ];

    // ── USB Detection ───────────────────────────────────────────────────

    /// <summary>
    /// Returns a list of detected removable USB drives on the current system.
    /// </summary>
    public static async Task<List<UsbDriveInfo>> DetectDrivesAsync(IProgress<string>? progress = null)
    {
        progress?.Report(LocalizationManager.Instance["UsbTools_DetectingDrives"]);
        var drives = new List<UsbDriveInfo>();

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
            progress?.Report(string.Format(
                LocalizationManager.Instance["UsbTools_DriveDetectError"], ex.Message));
        }

        return drives;
    }

    // ── Format USB ──────────────────────────────────────────────────────

    /// <summary>
    /// Formats a USB drive with the specified file system and partition scheme.
    /// </summary>
    public static async Task<UsbOperationResult> FormatDriveAsync(
        string deviceId, string fileSystem, string partitionScheme, string volumeLabel,
        IProgress<string>? progress = null, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(deviceId))
            return UsbOperationResult.Failure(LocalizationManager.Instance["UsbTools_NoDriveSelected"]);

        if (!ProcessHelper.IsValidDevicePath(deviceId))
            return UsbOperationResult.Failure(string.Format(
                LocalizationManager.Instance["Common_ErrorFormat"], "Invalid device identifier."));

        string sanitizedLabel = SanitizeVolumeLabel(volumeLabel, fileSystem);

        progress?.Report(string.Format(
            LocalizationManager.Instance["UsbTools_Formatting"], fileSystem, partitionScheme));

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return await FormatDriveWindowsAsync(deviceId, fileSystem, partitionScheme, sanitizedLabel, progress, ct);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return await FormatDriveLinuxAsync(deviceId, fileSystem, partitionScheme, sanitizedLabel, progress, ct);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return await FormatDriveMacAsync(deviceId, fileSystem, partitionScheme, sanitizedLabel, progress, ct);
        else
            return UsbOperationResult.Failure(LocalizationManager.Instance["UsbTools_UnsupportedPlatform"]);
    }

    // ── Write Image to USB ──────────────────────────────────────────────

    /// <summary>
    /// Writes a disk image file to a USB drive.
    /// </summary>
    public static async Task<UsbOperationResult> WriteImageAsync(
        string imagePath, string deviceId,
        IProgress<string>? progress = null, CancellationToken ct = default)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException(
                LocalizationManager.Instance["UsbTools_ImageNotFound"], imagePath);

        if (string.IsNullOrEmpty(deviceId))
            return UsbOperationResult.Failure(LocalizationManager.Instance["UsbTools_NoDriveSelected"]);

        if (!ProcessHelper.IsValidDevicePath(deviceId))
            return UsbOperationResult.Failure(string.Format(
                LocalizationManager.Instance["Common_ErrorFormat"], "Invalid device identifier."));

        string ext = Path.GetExtension(imagePath);
        if (!WritableImageExtensions.Contains(ext))
            return UsbOperationResult.Failure(string.Format(
                LocalizationManager.Instance["UsbTools_UnsupportedImageFormat"], ext));

        progress?.Report(string.Format(
            LocalizationManager.Instance["UsbTools_WritingImage"],
            Path.GetFileName(imagePath)));

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return await WriteImageWindowsAsync(imagePath, deviceId, progress, ct);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return await WriteImageLinuxAsync(imagePath, deviceId, progress, ct);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return await WriteImageMacAsync(imagePath, deviceId, progress, ct);
        else
            return UsbOperationResult.Failure(LocalizationManager.Instance["UsbTools_UnsupportedPlatform"]);
    }

    // ── Create Image from USB ───────────────────────────────────────────

    /// <summary>
    /// Creates a disk image file from a USB drive.
    /// </summary>
    public static async Task<UsbOperationResult> CreateImageFromDriveAsync(
        string deviceId, string outputPath,
        IProgress<string>? progress = null, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(deviceId))
            return UsbOperationResult.Failure(LocalizationManager.Instance["UsbTools_NoDriveSelected"]);

        if (!ProcessHelper.IsValidDevicePath(deviceId))
            return UsbOperationResult.Failure(string.Format(
                LocalizationManager.Instance["Common_ErrorFormat"], "Invalid device identifier."));

        if (string.IsNullOrEmpty(outputPath))
            throw new ArgumentException(LocalizationManager.Instance["UsbTools_OutputRequired"]);

        progress?.Report(LocalizationManager.Instance["UsbTools_CreatingImage"]);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return await CreateImageWindowsAsync(deviceId, outputPath, progress, ct);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return await CreateImageLinuxAsync(deviceId, outputPath, progress, ct);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return await CreateImageMacAsync(deviceId, outputPath, progress, ct);
        else
            return UsbOperationResult.Failure(LocalizationManager.Instance["UsbTools_UnsupportedPlatform"]);
    }

    // ── Windows Detection ───────────────────────────────────────────────

    private static async Task<List<UsbDriveInfo>> DetectDrivesWindowsAsync()
    {
        var drives = new List<UsbDriveInfo>();
        // Use PowerShell with Get-Disk + Get-Partition to enumerate USB drives
        var (exitCode, output) = await RunProcessAsync("powershell", [
            "-NoProfile", "-Command",
            @"Get-Disk | Where-Object { $_.BusType -eq 'USB' } | ForEach-Object {
                $disk = $_
                $parts = Get-Partition -DiskNumber $disk.Number -ErrorAction SilentlyContinue | Where-Object { $_.DriveLetter -ne [char]0 -and $_.DriveLetter }
                $letter = if ($parts) { ($parts | Select-Object -First 1).DriveLetter + ':' } else { '' }
                $sizeGB = [math]::Round($disk.Size / 1GB, 2)
                ""$($disk.Number)|$($disk.FriendlyName)|$sizeGB|$letter""
            }"
        ]);

        if (exitCode == 0 && !string.IsNullOrWhiteSpace(output))
        {
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = line.Split('|');
                if (parts.Length >= 4)
                {
                    drives.Add(new UsbDriveInfo
                    {
                        DeviceId = parts[0].Trim(),
                        Name = parts[1].Trim(),
                        SizeBytes = ParseSizeGb(parts[2].Trim()),
                        MountPoint = parts[3].Trim()
                    });
                }
            }
        }

        return drives;
    }

    // ── Linux Detection ─────────────────────────────────────────────────

    private static async Task<List<UsbDriveInfo>> DetectDrivesLinuxAsync()
    {
        var drives = new List<UsbDriveInfo>();

        // Use lsblk with JSON output for reliable parsing of multi-word fields
        var (exitCode, output) = await RunProcessAsync("lsblk", [
            "--json", "-dnpo", "NAME,SIZE,MODEL,RM,TRAN"
        ]);

        if (exitCode != 0 || string.IsNullOrWhiteSpace(output))
            return drives;

        try
        {
            using var doc = JsonDocument.Parse(output);
            var blockDevices = doc.RootElement.GetProperty("blockdevices");

            foreach (var device in blockDevices.EnumerateArray())
            {
                string name = device.GetProperty("name").GetString() ?? "";
                string size = GetJsonStringOrNumber(device, "size");
                string model = GetJsonString(device, "model");
                bool rm = GetJsonBool(device, "rm");
                string tran = GetJsonString(device, "tran");

                // Filter for USB removable devices (whole disks only, not partitions)
                if (!(rm || tran.Equals("usb", StringComparison.OrdinalIgnoreCase)))
                    continue;
                if (name.Contains("loop") || name.Contains("sr"))
                    continue;

                // Get mount point from partitions
                string mountPoint = "";
                var (mpExit, mpOutput) = await RunProcessAsync("lsblk", [
                    "-npo", "MOUNTPOINT", name
                ]);
                if (mpExit == 0 && !string.IsNullOrWhiteSpace(mpOutput))
                {
                    mountPoint = mpOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .FirstOrDefault(m => !string.IsNullOrEmpty(m) && m != "/") ?? "";
                }

                drives.Add(new UsbDriveInfo
                {
                    DeviceId = name,
                    Name = model.Replace("\\x20", " "),
                    SizeBytes = ParseHumanSize(size),
                    MountPoint = mountPoint
                });
            }
        }
        catch (JsonException)
        {
            // Fall back to space-delimited parsing if JSON is unavailable
            return await DetectDrivesLinuxFallbackAsync();
        }

        return drives;
    }

    /// <summary>
    /// Fallback Linux drive detection using plain text lsblk output
    /// for systems where --json is not supported.
    /// </summary>
    private static async Task<List<UsbDriveInfo>> DetectDrivesLinuxFallbackAsync()
    {
        var drives = new List<UsbDriveInfo>();

        var (exitCode, output) = await RunProcessAsync("lsblk", [
            "-dnpo", "NAME,SIZE,RM,TRAN"
        ]);

        if (exitCode != 0 || string.IsNullOrWhiteSpace(output))
            return drives;

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var columns = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (columns.Length < 3) continue;

            string name = columns[0];
            string size = columns[1];
            string rm = columns[2];
            string tran = columns.Length > 3 ? columns[3] : "";

            if (!(rm == "1" || tran.Equals("usb", StringComparison.OrdinalIgnoreCase)))
                continue;
            if (name.Contains("loop") || name.Contains("sr"))
                continue;

            // Get mount point from partitions
            string mountPoint = "";
            var (mpExit, mpOutput) = await RunProcessAsync("lsblk", [
                "-npo", "MOUNTPOINT", name
            ]);
            if (mpExit == 0 && !string.IsNullOrWhiteSpace(mpOutput))
            {
                mountPoint = mpOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .FirstOrDefault(m => !string.IsNullOrEmpty(m) && m != "/") ?? "";
            }

            drives.Add(new UsbDriveInfo
            {
                DeviceId = name,
                Name = "",
                SizeBytes = ParseHumanSize(size),
                MountPoint = mountPoint
            });
        }

        return drives;
    }

    // ── macOS Detection ─────────────────────────────────────────────────

    private static async Task<List<UsbDriveInfo>> DetectDrivesMacAsync()
    {
        var drives = new List<UsbDriveInfo>();

        // Use diskutil to list external/removable physical disks
        var (exitCode, output) = await RunProcessAsync("diskutil", ["list", "external", "physical"]);

        if (exitCode == 0 && !string.IsNullOrWhiteSpace(output))
        {
            // Parse disk identifiers like /dev/disk2
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!line.StartsWith("/dev/disk", StringComparison.Ordinal)) continue;

                // Extract disk identifier
                string diskId = line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
                // Remove the scheme map suffix if present (e.g., "/dev/disk2 (external, physical):")
                if (diskId.EndsWith(':'))
                    diskId = diskId[..^1];

                // Get info for this disk
                var (infoExit, infoOutput) = await RunProcessAsync("diskutil", ["info", diskId]);
                if (infoExit != 0) continue;

                string name = "", size = "", mountPoint = "";
                foreach (var infoLine in infoOutput.Split('\n'))
                {
                    string trimmed = infoLine.Trim();
                    if (trimmed.StartsWith("Device / Media Name:", StringComparison.OrdinalIgnoreCase))
                        name = trimmed.Split(':', 2)[1].Trim();
                    else if (trimmed.StartsWith("Disk Size:", StringComparison.OrdinalIgnoreCase))
                        size = trimmed.Split(':', 2)[1].Trim();
                    else if (trimmed.StartsWith("Mount Point:", StringComparison.OrdinalIgnoreCase))
                        mountPoint = trimmed.Split(':', 2)[1].Trim();
                }

                // If no mount point on the whole disk, check partitions
                if (string.IsNullOrEmpty(mountPoint))
                {
                    mountPoint = await FindMacPartitionMountPointAsync(diskId);
                }

                long sizeBytes = 0;
                // Parse "15.7 GB (15728640000 Bytes)" format
                int byteStart = size.IndexOf('(');
                int byteEnd = size.IndexOf("Bytes", StringComparison.OrdinalIgnoreCase);
                if (byteStart >= 0 && byteEnd > byteStart)
                {
                    string bytesStr = size[(byteStart + 1)..byteEnd].Trim();
                    long.TryParse(bytesStr, out sizeBytes);
                }

                drives.Add(new UsbDriveInfo
                {
                    DeviceId = diskId,
                    Name = name,
                    SizeBytes = sizeBytes,
                    MountPoint = mountPoint
                });
            }
        }

        return drives;
    }

    /// <summary>
    /// Checks partitions of a macOS disk for mount points.
    /// The whole disk often has no mount point; the data partition does.
    /// </summary>
    private static async Task<string> FindMacPartitionMountPointAsync(string diskId)
    {
        // List partitions for the disk
        var (listExit, listOutput) = await RunProcessAsync("diskutil", ["list", diskId]);
        if (listExit != 0 || string.IsNullOrWhiteSpace(listOutput))
            return "";

        // Extract partition identifiers (e.g., disk2s1, disk2s2)
        string diskName = Path.GetFileName(diskId); // e.g. "disk2"
        foreach (var line in listOutput.Split('\n'))
        {
            string trimmed = line.Trim();
            // Partition lines contain the identifier at the end (e.g., "disk2s1")
            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            string lastPart = parts[^1];
            if (!lastPart.StartsWith(diskName + "s", StringComparison.OrdinalIgnoreCase))
                continue;

            string partId = $"/dev/{lastPart}";
            var (partInfoExit, partInfoOutput) = await RunProcessAsync("diskutil", ["info", partId]);
            if (partInfoExit != 0) continue;

            foreach (var infoLine in partInfoOutput.Split('\n'))
            {
                string infoTrimmed = infoLine.Trim();
                if (infoTrimmed.StartsWith("Mount Point:", StringComparison.OrdinalIgnoreCase))
                {
                    string mp = infoTrimmed.Split(':', 2)[1].Trim();
                    if (!string.IsNullOrEmpty(mp))
                        return mp;
                }
            }
        }

        return "";
    }

    // ── Windows Format ──────────────────────────────────────────────────

    private static async Task<UsbOperationResult> FormatDriveWindowsAsync(
        string deviceId, string fileSystem, string partitionScheme, string volumeLabel,
        IProgress<string>? progress, CancellationToken ct)
    {
        progress?.Report(LocalizationManager.Instance["UsbTools_PreparingFormat"]);

        // Build diskpart script for clean + partition + format
        string partStyle = partitionScheme.Equals("GPT", StringComparison.OrdinalIgnoreCase) ? "gpt" : "mbr";
        string escapedLabel = EscapePowerShellString(volumeLabel);
        string fs = fileSystem.ToUpperInvariant();

        string script = $@"
$disk = Get-Disk -Number {deviceId}
Clear-Disk -Number {deviceId} -RemoveData -RemoveOEM -Confirm:$false -ErrorAction SilentlyContinue
Initialize-Disk -Number {deviceId} -PartitionStyle {partStyle}
$part = New-Partition -DiskNumber {deviceId} -UseMaximumSize -AssignDriveLetter
Format-Volume -Partition $part -FileSystem {fs} -NewFileSystemLabel '{escapedLabel}' -Confirm:$false
";

        var (exitCode, output) = await RunProcessAsync("powershell", [
            "-NoProfile", "-Command", script
        ], ct);

        return exitCode == 0
            ? UsbOperationResult.Success(LocalizationManager.Instance["UsbTools_FormatComplete"])
            : UsbOperationResult.Failure(string.Format(
                LocalizationManager.Instance["UsbTools_FormatFailed"], output.Trim()));
    }

    // ── Linux Format ────────────────────────────────────────────────────

    private static async Task<UsbOperationResult> FormatDriveLinuxAsync(
        string deviceId, string fileSystem, string partitionScheme, string volumeLabel,
        IProgress<string>? progress, CancellationToken ct)
    {
        progress?.Report(LocalizationManager.Instance["UsbTools_PreparingFormat"]);

        // Unmount all partitions first (safe enumeration, no shell interpolation)
        await UnmountAllPartitionsAsync(deviceId);

        // Create partition table
        string tableType = partitionScheme.Equals("GPT", StringComparison.OrdinalIgnoreCase) ? "gpt" : "msdos";
        var (partedExit, partedOutput) = await RunProcessAsync("parted", [
            "-s", deviceId, "mklabel", tableType, "mkpart", "primary", "0%", "100%"
        ], ct);

        if (partedExit != 0)
            return UsbOperationResult.Failure(string.Format(
                LocalizationManager.Instance["UsbTools_FormatFailed"], partedOutput.Trim()));

        // Wait for kernel to re-read partition table
        await RunProcessAsync("partprobe", [deviceId]);
        await Task.Delay(1000, ct);

        // Determine partition path
        string partPath = deviceId.Contains("nvme") || deviceId.Contains("mmcblk")
            ? $"{deviceId}p1"
            : $"{deviceId}1";

        // Format the partition
        progress?.Report(string.Format(
            LocalizationManager.Instance["UsbTools_Formatting"], fileSystem, partitionScheme));

        string mkfsTool;
        string[] mkfsArgs;

        switch (fileSystem.ToUpperInvariant())
        {
            case "FAT":
                mkfsTool = "mkfs.vfat";
                mkfsArgs = ["-F", "16", "-n", volumeLabel, partPath];
                break;
            case "FAT32":
                mkfsTool = "mkfs.vfat";
                mkfsArgs = ["-F", "32", "-n", volumeLabel, partPath];
                break;
            case "EXFAT":
                mkfsTool = "mkfs.exfat";
                mkfsArgs = ["-n", volumeLabel, partPath];
                break;
            case "NTFS":
                mkfsTool = "mkfs.ntfs";
                mkfsArgs = ["-f", "-L", volumeLabel, partPath];
                break;
            default:
                return UsbOperationResult.Failure(string.Format(
                    LocalizationManager.Instance["UsbTools_UnsupportedFileSystem"], fileSystem));
        }

        var (fmtExit, fmtOutput) = await RunProcessAsync(mkfsTool, mkfsArgs, ct);

        return fmtExit == 0
            ? UsbOperationResult.Success(LocalizationManager.Instance["UsbTools_FormatComplete"])
            : UsbOperationResult.Failure(string.Format(
                LocalizationManager.Instance["UsbTools_FormatFailed"], fmtOutput.Trim()));
    }

    // ── macOS Format ────────────────────────────────────────────────────

    /// <summary>
    /// Formats a USB drive on macOS using diskutil eraseDisk.
    /// Note: macOS does not support native NTFS formatting.
    /// If NTFS is requested, ExFAT is used instead and the user is notified.
    /// </summary>
    private static async Task<UsbOperationResult> FormatDriveMacAsync(
        string deviceId, string fileSystem, string partitionScheme, string volumeLabel,
        IProgress<string>? progress, CancellationToken ct)
    {
        progress?.Report(LocalizationManager.Instance["UsbTools_PreparingFormat"]);

        // Map file system to diskutil format name
        string fsType = fileSystem.ToUpperInvariant() switch
        {
            "FAT" => "MS-DOS (FAT16)",
            "FAT32" => "MS-DOS (FAT32)",
            "EXFAT" => "ExFAT",
            "NTFS" => "ExFAT", // macOS cannot natively format NTFS; fallback to ExFAT
            _ => "MS-DOS (FAT32)"
        };

        string scheme = partitionScheme.Equals("GPT", StringComparison.OrdinalIgnoreCase)
            ? "GPTFormat" : "MBRFormat";

        // Use diskutil eraseDisk
        var (exitCode, output) = await RunProcessAsync("diskutil", [
            "eraseDisk", fsType, volumeLabel, scheme, deviceId
        ], ct);

        if (exitCode == 0)
        {
            string msg = LocalizationManager.Instance["UsbTools_FormatComplete"];
            // Warn about NTFS on macOS
            if (fileSystem.Equals("NTFS", StringComparison.OrdinalIgnoreCase))
                msg += "\n" + LocalizationManager.Instance["UsbTools_NtfsMacWarning"];
            return UsbOperationResult.Success(msg);
        }

        return UsbOperationResult.Failure(string.Format(
            LocalizationManager.Instance["UsbTools_FormatFailed"], output.Trim()));
    }

    // ── Windows Write Image ─────────────────────────────────────────────

    private static async Task<UsbOperationResult> WriteImageWindowsAsync(
        string imagePath, string deviceId,
        IProgress<string>? progress, CancellationToken ct)
    {
        progress?.Report(string.Format(
            LocalizationManager.Instance["UsbTools_WritingImage"], Path.GetFileName(imagePath)));

        // Use PowerShell with raw disk writing via .NET FileStream
        string escapedPath = EscapePowerShellString(imagePath);
        string script = $@"
$diskNumber = {deviceId}
# Clean the disk first
Clear-Disk -Number $diskNumber -RemoveData -RemoveOEM -Confirm:$false -ErrorAction SilentlyContinue
# Get physical disk path
$disk = Get-Disk -Number $diskNumber
$physicalPath = ""\\.\PhysicalDrive$diskNumber""
# Write image using dd-like approach with PowerShell
$source = [System.IO.File]::OpenRead('{escapedPath}')
$target = [System.IO.File]::Open($physicalPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
try {{
    $buffer = New-Object byte[] (1MB)
    $totalBytes = $source.Length
    $written = 0
    while (($read = $source.Read($buffer, 0, $buffer.Length)) -gt 0) {{
        $target.Write($buffer, 0, $read)
        $written += $read
        $percent = [math]::Round(($written / $totalBytes) * 100, 1)
        Write-Host ""PROGRESS:$percent""
    }}
    $target.Flush()
}} finally {{
    $source.Close()
    $target.Close()
}}
Write-Host 'DONE'
";

        var (exitCode, output) = await RunProcessAsync("powershell", [
            "-NoProfile", "-Command", script
        ], ct);

        return exitCode == 0 && output.Contains("DONE")
            ? UsbOperationResult.Success(LocalizationManager.Instance["UsbTools_WriteComplete"])
            : UsbOperationResult.Failure(string.Format(
                LocalizationManager.Instance["UsbTools_WriteFailed"], output.Trim()));
    }

    // ── Linux Write Image ───────────────────────────────────────────────

    private static async Task<UsbOperationResult> WriteImageLinuxAsync(
        string imagePath, string deviceId,
        IProgress<string>? progress, CancellationToken ct)
    {
        progress?.Report(string.Format(
            LocalizationManager.Instance["UsbTools_WritingImage"], Path.GetFileName(imagePath)));

        // Unmount device first (safe enumeration, no shell interpolation)
        await UnmountAllPartitionsAsync(deviceId);

        // Use dd to write image
        var (exitCode, output) = await RunProcessAsync("dd", [
            $"if={imagePath}",
            $"of={deviceId}",
            "bs=4M",
            "status=progress",
            "conv=fsync"
        ], ct);

        // Run sync to flush buffers
        await RunProcessAsync("sync", []);

        return exitCode == 0
            ? UsbOperationResult.Success(LocalizationManager.Instance["UsbTools_WriteComplete"])
            : UsbOperationResult.Failure(string.Format(
                LocalizationManager.Instance["UsbTools_WriteFailed"], output.Trim()));
    }

    // ── macOS Write Image ───────────────────────────────────────────────

    private static async Task<UsbOperationResult> WriteImageMacAsync(
        string imagePath, string deviceId,
        IProgress<string>? progress, CancellationToken ct)
    {
        progress?.Report(string.Format(
            LocalizationManager.Instance["UsbTools_WritingImage"], Path.GetFileName(imagePath)));

        // Unmount disk first
        await RunProcessAsync("diskutil", ["unmountDisk", deviceId]);

        // Use raw device for faster writes (replace /dev/diskN with /dev/rdiskN)
        string rawDevice = deviceId.Replace("/dev/disk", "/dev/rdisk");

        // Use dd to write image
        var (exitCode, output) = await RunProcessAsync("dd", [
            $"if={imagePath}",
            $"of={rawDevice}",
            "bs=4m"
        ], ct);

        // Eject and re-mount
        await RunProcessAsync("diskutil", ["eject", deviceId]);

        return exitCode == 0
            ? UsbOperationResult.Success(LocalizationManager.Instance["UsbTools_WriteComplete"])
            : UsbOperationResult.Failure(string.Format(
                LocalizationManager.Instance["UsbTools_WriteFailed"], output.Trim()));
    }

    // ── Windows Create Image ────────────────────────────────────────────

    private static async Task<UsbOperationResult> CreateImageWindowsAsync(
        string deviceId, string outputPath,
        IProgress<string>? progress, CancellationToken ct)
    {
        progress?.Report(LocalizationManager.Instance["UsbTools_CreatingImage"]);

        string escapedOutput = EscapePowerShellString(outputPath);
        string script = $@"
$physicalPath = ""\\.\PhysicalDrive{deviceId}""
$disk = Get-Disk -Number {deviceId}
$totalBytes = $disk.Size
$source = [System.IO.File]::Open($physicalPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
$target = [System.IO.File]::Create('{escapedOutput}')
try {{
    $buffer = New-Object byte[] (1MB)
    $read_total = 0
    while (($read = $source.Read($buffer, 0, $buffer.Length)) -gt 0) {{
        $target.Write($buffer, 0, $read)
        $read_total += $read
        if ($totalBytes -gt 0) {{
            $percent = [math]::Round(($read_total / $totalBytes) * 100, 1)
            Write-Host ""PROGRESS:$percent""
        }}
    }}
    $target.Flush()
}} finally {{
    $source.Close()
    $target.Close()
}}
Write-Host 'DONE'
";

        var (exitCode, output) = await RunProcessAsync("powershell", [
            "-NoProfile", "-Command", script
        ], ct);

        return exitCode == 0 && output.Contains("DONE")
            ? UsbOperationResult.Success(string.Format(
                LocalizationManager.Instance["UsbTools_ImageCreated"], outputPath))
            : UsbOperationResult.Failure(string.Format(
                LocalizationManager.Instance["UsbTools_ImageCreateFailed"], output.Trim()));
    }

    // ── Linux Create Image ──────────────────────────────────────────────

    private static async Task<UsbOperationResult> CreateImageLinuxAsync(
        string deviceId, string outputPath,
        IProgress<string>? progress, CancellationToken ct)
    {
        progress?.Report(LocalizationManager.Instance["UsbTools_CreatingImage"]);

        // Unmount device first (safe enumeration, no shell interpolation)
        await UnmountAllPartitionsAsync(deviceId);

        // Use dd to create image
        var (exitCode, output) = await RunProcessAsync("dd", [
            $"if={deviceId}",
            $"of={outputPath}",
            "bs=4M",
            "status=progress",
            "conv=sync,noerror"
        ], ct);

        await RunProcessAsync("sync", []);

        return exitCode == 0
            ? UsbOperationResult.Success(string.Format(
                LocalizationManager.Instance["UsbTools_ImageCreated"], outputPath))
            : UsbOperationResult.Failure(string.Format(
                LocalizationManager.Instance["UsbTools_ImageCreateFailed"], output.Trim()));
    }

    // ── macOS Create Image ──────────────────────────────────────────────

    private static async Task<UsbOperationResult> CreateImageMacAsync(
        string deviceId, string outputPath,
        IProgress<string>? progress, CancellationToken ct)
    {
        progress?.Report(LocalizationManager.Instance["UsbTools_CreatingImage"]);

        // Unmount disk
        await RunProcessAsync("diskutil", ["unmountDisk", deviceId]);

        string rawDevice = deviceId.Replace("/dev/disk", "/dev/rdisk");

        // Use dd to create image
        var (exitCode, output) = await RunProcessAsync("dd", [
            $"if={rawDevice}",
            $"of={outputPath}",
            "bs=4m"
        ], ct);

        return exitCode == 0
            ? UsbOperationResult.Success(string.Format(
                LocalizationManager.Instance["UsbTools_ImageCreated"], outputPath))
            : UsbOperationResult.Failure(string.Format(
                LocalizationManager.Instance["UsbTools_ImageCreateFailed"], output.Trim()));
    }

    // ── File Management ─────────────────────────────────────────────────

    /// <summary>
    /// Returns a list of file system entries in the given directory path on a USB drive.
    /// Individual inaccessible entries are silently skipped.
    /// </summary>
    public static List<UsbFileEntry> GetDirectoryContents(string directoryPath)
    {
        var entries = new List<UsbFileEntry>();

        if (!Directory.Exists(directoryPath))
            return entries;

        try
        {
            foreach (var dir in Directory.GetDirectories(directoryPath)
                         .OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var info = new DirectoryInfo(dir);
                    entries.Add(new UsbFileEntry
                    {
                        Name = info.Name,
                        FullPath = info.FullName,
                        IsDirectory = true,
                        Size = 0,
                        LastModified = info.LastWriteTime
                    });
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    // Skip individual inaccessible directories
                }
            }

            foreach (var file in Directory.GetFiles(directoryPath)
                         .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var info = new FileInfo(file);
                    entries.Add(new UsbFileEntry
                    {
                        Name = info.Name,
                        FullPath = info.FullName,
                        IsDirectory = false,
                        Size = info.Length,
                        LastModified = info.LastWriteTime
                    });
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    // Skip individual inaccessible files
                }
            }
        }
        catch (UnauthorizedAccessException) { /* skip inaccessible entries */ }
        catch (IOException) { /* skip I/O errors */ }

        return entries;
    }

    /// <summary>
    /// Creates a new folder at the specified path.
    /// </summary>
    public static UsbOperationResult CreateFolder(string path)
    {
        try
        {
            if (Directory.Exists(path))
                return UsbOperationResult.Failure(LocalizationManager.Instance["UsbTools_FolderExists"]);

            Directory.CreateDirectory(path);
            return UsbOperationResult.Success(LocalizationManager.Instance["UsbTools_FolderCreated"]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return UsbOperationResult.Failure(string.Format(
                LocalizationManager.Instance["Common_ErrorFormat"], ex.Message));
        }
    }

    /// <summary>
    /// Deletes a file or folder (recursive for folders).
    /// </summary>
    public static UsbOperationResult DeleteEntry(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
                return UsbOperationResult.Success(LocalizationManager.Instance["UsbTools_DeleteComplete"]);
            }

            if (File.Exists(path))
            {
                File.Delete(path);
                return UsbOperationResult.Success(LocalizationManager.Instance["UsbTools_DeleteComplete"]);
            }

            return UsbOperationResult.Failure(LocalizationManager.Instance["UsbTools_EntryNotFound"]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return UsbOperationResult.Failure(string.Format(
                LocalizationManager.Instance["Common_ErrorFormat"], ex.Message));
        }
    }

    /// <summary>
    /// Moves a file or folder to a new destination.
    /// </summary>
    public static UsbOperationResult MoveEntry(string sourcePath, string destinationDirectory)
    {
        try
        {
            string name = Path.GetFileName(sourcePath);
            string destPath = Path.Combine(destinationDirectory, name);

            if (!Directory.Exists(destinationDirectory))
                Directory.CreateDirectory(destinationDirectory);

            if (File.Exists(destPath) || Directory.Exists(destPath))
                return UsbOperationResult.Failure(LocalizationManager.Instance["UsbTools_DestinationExists"]);

            if (Directory.Exists(sourcePath))
                Directory.Move(sourcePath, destPath);
            else if (File.Exists(sourcePath))
                File.Move(sourcePath, destPath);
            else
                return UsbOperationResult.Failure(LocalizationManager.Instance["UsbTools_EntryNotFound"]);

            return UsbOperationResult.Success(LocalizationManager.Instance["UsbTools_MoveComplete"]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return UsbOperationResult.Failure(string.Format(
                LocalizationManager.Instance["Common_ErrorFormat"], ex.Message));
        }
    }

    /// <summary>
    /// Copies files to the USB drive destination directory.
    /// </summary>
    public static async Task<UsbOperationResult> AddFilesAsync(
        string[] sourcePaths, string destinationDirectory,
        IProgress<string>? progress = null, CancellationToken ct = default)
    {
        try
        {
            if (!Directory.Exists(destinationDirectory))
                Directory.CreateDirectory(destinationDirectory);

            int count = 0;
            foreach (var source in sourcePaths)
            {
                ct.ThrowIfCancellationRequested();
                string name = Path.GetFileName(source);
                string destPath = Path.Combine(destinationDirectory, name);

                progress?.Report(string.Format(
                    LocalizationManager.Instance["UsbTools_CopyingFile"], name));

                if (Directory.Exists(source))
                {
                    await Task.Run(() => ProcessHelper.CopyDirectoryRecursive(source, destPath, ct), ct);
                }
                else if (File.Exists(source))
                {
                    await Task.Run(() => File.Copy(source, destPath, overwrite: true), ct);
                }
                count++;
            }

            return UsbOperationResult.Success(string.Format(
                LocalizationManager.Instance["UsbTools_AddComplete"], count));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return UsbOperationResult.Failure(string.Format(
                LocalizationManager.Instance["Common_ErrorFormat"], ex.Message));
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static string SanitizeVolumeLabel(string label, string fileSystem)
    {
        if (string.IsNullOrWhiteSpace(label))
            return "USB_DRIVE";

        string fsUpper = fileSystem.ToUpperInvariant();

        // FAT/FAT32 labels: max 11 chars, uppercase only
        // exFAT labels: max 15 chars, mixed case allowed
        // NTFS labels: max 32 chars, mixed case allowed
        int maxLen = fsUpper switch
        {
            "FAT" or "FAT32" => 11,
            "EXFAT" => 15,
            "NTFS" => 32,
            _ => 11
        };

        bool forceUppercase = fsUpper is "FAT" or "FAT32";

        var sb = new StringBuilder(Math.Min(label.Length, maxLen));
        foreach (char c in label)
        {
            if (sb.Length >= maxLen) break;
            if (char.IsAsciiLetterOrDigit(c) || c == '_' || c == '-')
                sb.Append(forceUppercase ? char.ToUpperInvariant(c) : c);
            else if (c == ' ')
                sb.Append('_');
        }

        return sb.Length > 0 ? sb.ToString() : "USB_DRIVE";
    }

    private static long ParseSizeGb(string sizeStr)
    {
        if (double.TryParse(sizeStr, System.Globalization.CultureInfo.InvariantCulture, out double gb))
            return (long)(gb * 1024 * 1024 * 1024);
        return 0;
    }

    private static long ParseHumanSize(string sizeStr)
    {
        if (string.IsNullOrEmpty(sizeStr)) return 0;

        sizeStr = sizeStr.Trim().ToUpperInvariant();
        double multiplier = 1;

        if (sizeStr.EndsWith('T'))
        {
            multiplier = 1024.0 * 1024 * 1024 * 1024;
            sizeStr = sizeStr[..^1];
        }
        else if (sizeStr.EndsWith('G'))
        {
            multiplier = 1024.0 * 1024 * 1024;
            sizeStr = sizeStr[..^1];
        }
        else if (sizeStr.EndsWith('M'))
        {
            multiplier = 1024.0 * 1024;
            sizeStr = sizeStr[..^1];
        }
        else if (sizeStr.EndsWith('K'))
        {
            multiplier = 1024;
            sizeStr = sizeStr[..^1];
        }

        if (double.TryParse(sizeStr, System.Globalization.CultureInfo.InvariantCulture, out double val))
            return (long)(val * multiplier);

        return 0;
    }

    /// <summary>
    /// Formats a byte count as a human-readable string.
    /// </summary>
    public static string FormatSize(long bytes)
        => ProcessHelper.FormatSize(bytes);

    private static string EscapePowerShellString(string value)
        => ProcessHelper.EscapePowerShellString(value);

    /// <summary>
    /// Gets a string property from a JSON element, returning empty string if not found or null.
    /// </summary>
    private static string GetJsonString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString() ?? "";
        return "";
    }

    /// <summary>
    /// Gets a string property from a JSON element that may be a string or number.
    /// Returns the value as a string in either case.
    /// </summary>
    private static string GetJsonStringOrNumber(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop))
            return "";

        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString() ?? "",
            JsonValueKind.Number => prop.GetInt64().ToString(),
            _ => ""
        };
    }

    /// <summary>
    /// Gets a boolean property from a JSON element that may be a bool, string "1"/"0", or number.
    /// </summary>
    private static bool GetJsonBool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop))
            return false;

        return prop.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => prop.GetString() == "1",
            JsonValueKind.Number => prop.GetInt32() != 0,
            _ => false
        };
    }

    /// <summary>
    /// Unmounts all partitions of a block device on Linux without using shell
    /// interpolation.  Enumerates <c>/proc/mounts</c> for entries whose device
    /// path starts with the given base device ID and unmounts each one individually.
    /// This avoids the need for <c>sh -c "umount /dev/sdX* …"</c> which would
    /// require string interpolation inside a shell command.
    /// </summary>
    private static async Task UnmountAllPartitionsAsync(string deviceId)
    {
        try
        {
            // /proc/mounts lists all currently mounted file systems.
            // Each line has the format:  device mountpoint fstype options dump pass
            if (!File.Exists("/proc/mounts"))
            {
                // Fallback: attempt to unmount the base device itself
                await RunProcessAsync("umount", [deviceId]);
                return;
            }

            var lines = await File.ReadAllLinesAsync("/proc/mounts").ConfigureAwait(false);
            foreach (string line in lines)
            {
                string trimmed = line.TrimStart();
                // Match device paths that are the base device or a partition of it
                // e.g. /dev/sdb, /dev/sdb1, /dev/sdb2 for deviceId=/dev/sdb
                if (trimmed.StartsWith(deviceId, StringComparison.Ordinal))
                {
                    // Extract the device path (first space-delimited field)
                    int spaceIdx = trimmed.IndexOf(' ');
                    if (spaceIdx <= 0) continue;

                    string mountedDevice = trimmed[..spaceIdx];

                    // Ensure it's an exact match or a partition suffix:
                    //  - Empty suffix  → exact device match (e.g. /dev/sdb)
                    //  - All digits    → numbered partition  (e.g. /dev/sdb1)
                    //  - 'p' + digits  → NVMe/MMC partition  (e.g. /dev/nvme0n1p1)
                    string suffix = mountedDevice[deviceId.Length..];
                    if (suffix.Length > 0)
                    {
                        bool isNumericPartition = suffix.All(char.IsAsciiDigit);
                        bool isNvmePartition = suffix.Length >= 2
                            && suffix[0] == 'p'
                            && suffix.AsSpan(1).IndexOfAnyExceptInRange('0', '9') < 0;

                        if (!isNumericPartition && !isNvmePartition)
                            continue;
                    }

                    // Best-effort unmount — ignore failures
                    try { await RunProcessAsync("umount", [mountedDevice]); }
                    catch { /* best-effort */ }
                }
            }
        }
        catch
        {
            // Best-effort — ignore errors during unmount
        }
    }

    // Convenience wrappers for shared helpers
    private static Task<(int ExitCode, string Output)> RunProcessAsync(
        string fileName, string[] arguments, CancellationToken ct = default)
        => ProcessHelper.RunProcessAsync(fileName, arguments, ct);
}

/// <summary>
/// Information about a detected USB drive.
/// </summary>
public class UsbDriveInfo
{
    public string DeviceId { get; set; } = "";
    public string Name { get; set; } = "";
    public long SizeBytes { get; set; }
    public string MountPoint { get; set; } = "";

    public string DisplayName
    {
        get
        {
            string size = UsbToolsHelper.FormatSize(SizeBytes);
            string label = string.IsNullOrEmpty(Name) ? DeviceId : Name;
            return string.IsNullOrEmpty(MountPoint)
                ? $"{label} ({size})"
                : $"{label} [{MountPoint}] ({size})";
        }
    }
}

/// <summary>
/// A file or folder entry on a USB drive.
/// </summary>
public class UsbFileEntry
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public bool IsDirectory { get; set; }
    public long Size { get; set; }
    public DateTime LastModified { get; set; }
}

/// <summary>
/// Result of a USB operation.
/// </summary>
public class UsbOperationResult
{
    public bool IsSuccess { get; init; }
    public string Message { get; init; } = "";

    public static UsbOperationResult Success(string message) =>
        new() { IsSuccess = true, Message = message };

    public static UsbOperationResult Failure(string message) =>
        new() { IsSuccess = false, Message = message };
}
