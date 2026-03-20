using RetroMultiTools.Models;
using RetroMultiTools.Services;
using System.Runtime.InteropServices;
using System.Text;

namespace RetroMultiTools.Utilities.RetroArch;

/// <summary>
/// Creates OS-specific desktop shortcuts that launch a ROM in RetroArch
/// with a specified libretro core.
/// Supports Windows (.lnk via PowerShell), Linux (.desktop) and macOS (.command).
/// </summary>
public static class RetroArchShortcutCreator
{
    /// <summary>
    /// Configuration for a shortcut to be created.
    /// </summary>
    public sealed class ShortcutConfig
    {
        /// <summary>Display name for the shortcut.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Absolute path to the ROM file.</summary>
        public string RomPath { get; set; } = string.Empty;

        /// <summary>The ROM system (used to select the default core if CoreName is empty).</summary>
        public RomSystem System { get; set; }

        /// <summary>
        /// Libretro core name (e.g. "snes9x"). When empty, the default core
        /// for <see cref="System"/> is used.
        /// </summary>
        public string CoreName { get; set; } = string.Empty;

        /// <summary>Optional path to a custom icon file (.ico on Windows, .png on Linux).</summary>
        public string IconPath { get; set; } = string.Empty;

        /// <summary>
        /// Directory to write the shortcut file into.
        /// Defaults to the user's Desktop if empty.
        /// </summary>
        public string OutputDirectory { get; set; } = string.Empty;

        /// <summary>Optional RetroArch path override (uses configured path when empty).</summary>
        public string RetroArchPath { get; set; } = string.Empty;

        /// <summary>Start RetroArch in fullscreen mode.</summary>
        public bool Fullscreen { get; set; }

        /// <summary>Additional RetroArch command-line arguments.</summary>
        public string ExtraArguments { get; set; } = string.Empty;
    }

    /// <summary>
    /// Result of a shortcut creation attempt.
    /// </summary>
    public sealed class ShortcutResult(bool success, string message, string? shortcutPath = null)
    {
        public bool Success { get; } = success;
        public string Message { get; } = message;
        public string? ShortcutPath { get; } = shortcutPath;
    }

    /// <summary>
    /// Returns the available core names from the RetroArchLauncher SystemCoreMap,
    /// along with their display names, for use in UI dropdowns.
    /// </summary>
    public static IReadOnlyList<(string CoreName, string DisplayName)> GetAvailableCores()
    {
        var cores = new List<(string CoreName, string DisplayName)>();
        var seen = new HashSet<string>();

        foreach (RomSystem system in Enum.GetValues<RomSystem>())
        {
            string? coreName = RetroArchLauncher.GetCoreName(system);
            if (coreName != null && seen.Add(coreName))
            {
                cores.Add((coreName, RetroArchLauncher.GetCoreDisplayName(system)));
            }
        }

        cores.Sort((a, b) => string.Compare(a.CoreName, b.CoreName, StringComparison.OrdinalIgnoreCase));
        return cores;
    }

    /// <summary>
    /// Creates a desktop shortcut for the given configuration.
    /// Automatically selects the correct format for the current OS.
    /// </summary>
    public static ShortcutResult CreateShortcut(ShortcutConfig config)
    {
        // ── Validation ──────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(config.Name))
            return new ShortcutResult(false, "Shortcut name is required.");

        if (string.IsNullOrWhiteSpace(config.RomPath) || !File.Exists(config.RomPath))
            return new ShortcutResult(false, "ROM file not found.");

        // Resolve RetroArch path
        string retroArchPath = !string.IsNullOrEmpty(config.RetroArchPath)
            ? RetroArchLauncher.ResolveRetroArchPath(config.RetroArchPath)
            : RetroArchLauncher.GetRetroArchExecutablePath();

        if (string.IsNullOrEmpty(retroArchPath) || !File.Exists(retroArchPath))
            return new ShortcutResult(false, "RetroArch executable not found. Please configure the path in Settings.");

        // Resolve core name
        string coreName = !string.IsNullOrEmpty(config.CoreName)
            ? config.CoreName
            : RetroArchLauncher.GetCoreName(config.System) ?? string.Empty;

        if (string.IsNullOrEmpty(coreName))
            return new ShortcutResult(false, $"No core mapping found for system: {config.System}. Please specify a core.");

        // Find core path
        string coreSuffix = GetCoreLibrarySuffix();
        string coreFileName = $"{coreName}_libretro{coreSuffix}";
        string? coresDir = FindCoresDirectory(retroArchPath);
        string? corePath = null;

        if (coresDir != null)
        {
            string candidate = Path.Combine(coresDir, coreFileName);
            if (File.Exists(candidate))
                corePath = candidate;
        }

        if (corePath == null)
            return new ShortcutResult(false, $"Core '{coreName}' not found. Please install it via the Core Downloader.");

        // Resolve output directory
        string outputDir = !string.IsNullOrEmpty(config.OutputDirectory)
            ? config.OutputDirectory
            : Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        if (string.IsNullOrEmpty(outputDir))
            return new ShortcutResult(false, "Could not determine output directory.");

        Directory.CreateDirectory(outputDir);

        // ── Create shortcut ─────────────────────────────────────────
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return CreateWindowsShortcut(config, retroArchPath, corePath, outputDir);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return CreateLinuxShortcut(config, retroArchPath, corePath, outputDir);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return CreateMacOsShortcut(config, retroArchPath, corePath, outputDir);

            return new ShortcutResult(false, "Unsupported operating system.");
        }
        catch (UnauthorizedAccessException ex)
        {
            return new ShortcutResult(false, $"Access denied: {ex.Message}");
        }
        catch (IOException ex)
        {
            return new ShortcutResult(false, $"File error: {ex.Message}");
        }
    }

    // ── Windows (.lnk via PowerShell) ───────────────────────────────────

    private static ShortcutResult CreateWindowsShortcut(
        ShortcutConfig config, string retroArchPath, string corePath, string outputDir)
    {
        string safeName = SanitizeFileName(config.Name);
        string lnkPath = Path.Combine(outputDir, $"{safeName}.lnk");
        string arguments = BuildArguments(config, corePath);

        // Use PowerShell to create a .lnk shortcut (COM-free approach)
        string psScript = $@"
$ws = New-Object -ComObject WScript.Shell
$shortcut = $ws.CreateShortcut('{EscapePowerShellString(lnkPath)}')
$shortcut.TargetPath = '{EscapePowerShellString(retroArchPath)}'
$shortcut.Arguments = '{EscapePowerShellString(arguments)}'
$shortcut.WorkingDirectory = '{EscapePowerShellString(Path.GetDirectoryName(retroArchPath) ?? "")}'
$shortcut.Description = 'Launch {EscapePowerShellString(config.Name)} in RetroArch'
{(!string.IsNullOrEmpty(config.IconPath) && File.Exists(config.IconPath)
    ? $"$shortcut.IconLocation = '{EscapePowerShellString(config.IconPath)}'"
    : $"$shortcut.IconLocation = '{EscapePowerShellString(retroArchPath)}'")}
$shortcut.Save()
";

        // Use -EncodedCommand with Base64 to avoid shell escaping issues
        string base64 = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(psScript));

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-NoProfile -NonInteractive -EncodedCommand {base64}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
        };

        using var process = System.Diagnostics.Process.Start(psi);
        if (process == null)
            return new ShortcutResult(false, "Failed to start PowerShell to create shortcut.");

        // Read stderr before WaitForExit to prevent deadlock when buffer fills
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(15000);

        if (process.ExitCode != 0)
        {
            return new ShortcutResult(false, $"PowerShell error: {stderr}");
        }

        return File.Exists(lnkPath)
            ? new ShortcutResult(true, $"Windows shortcut created: {safeName}.lnk", lnkPath)
            : new ShortcutResult(false, "Shortcut file was not created.");
    }

    // ── Linux (.desktop) ────────────────────────────────────────────────

    private static ShortcutResult CreateLinuxShortcut(
        ShortcutConfig config, string retroArchPath, string corePath, string outputDir)
    {
        string safeName = SanitizeFileName(config.Name);
        string desktopPath = Path.Combine(outputDir, $"{safeName}.desktop");
        string arguments = BuildArguments(config, corePath);

        var sb = new StringBuilder();
        sb.AppendLine("[Desktop Entry]");
        sb.AppendLine("Type=Application");
        sb.AppendLine($"Name={config.Name}");
        sb.AppendLine($"Comment=Launch {config.Name} in RetroArch");
        sb.AppendLine($"Exec={EscapeDesktopExec(retroArchPath)} {arguments}");
        sb.AppendLine($"Path={Path.GetDirectoryName(retroArchPath)}");
        sb.AppendLine("Terminal=false");
        sb.AppendLine("Categories=Game;Emulator;");

        if (!string.IsNullOrEmpty(config.IconPath) && File.Exists(config.IconPath))
            sb.AppendLine($"Icon={config.IconPath}");
        else
            sb.AppendLine($"Icon={retroArchPath}");

        File.WriteAllText(desktopPath, sb.ToString(), Encoding.UTF8);

        // Make executable
        try
        {
            using var chmod = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"+x \"{desktopPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            chmod?.WaitForExit(5000);
        }
        catch
        {
            // Non-fatal: shortcut still works, just may not be double-clickable
        }

        return new ShortcutResult(true, $"Linux shortcut created: {safeName}.desktop", desktopPath);
    }

    // ── macOS (.command) ────────────────────────────────────────────────

    private static ShortcutResult CreateMacOsShortcut(
        ShortcutConfig config, string retroArchPath, string corePath, string outputDir)
    {
        string safeName = SanitizeFileName(config.Name);
        string commandPath = Path.Combine(outputDir, $"{safeName}.command");
        string shellArgs = BuildShellArguments(config, corePath);

        var sb = new StringBuilder();
        sb.AppendLine("#!/bin/bash");
        sb.AppendLine($"# Launch {config.Name} in RetroArch");
        sb.AppendLine($"exec {EscapeShellArg(retroArchPath)} {shellArgs}");

        File.WriteAllText(commandPath, sb.ToString(), Encoding.UTF8);

        // Make executable
        try
        {
            using var chmod = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"+x \"{commandPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            chmod?.WaitForExit(5000);
        }
        catch
        {
            // Non-fatal
        }

        return new ShortcutResult(true, $"macOS shortcut created: {safeName}.command", commandPath);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static string BuildArguments(ShortcutConfig config, string corePath)
    {
        var args = new StringBuilder();

        args.Append($"-L \"{corePath}\"");

        if (config.Fullscreen)
            args.Append(" --fullscreen");

        if (!string.IsNullOrWhiteSpace(config.ExtraArguments))
        {
            args.Append(' ');
            args.Append(config.ExtraArguments);
        }

        args.Append($" \"{config.RomPath}\"");

        return args.ToString();
    }

    /// <summary>
    /// Builds shell-safe arguments for macOS .command scripts using single-quote escaping.
    /// </summary>
    private static string BuildShellArguments(ShortcutConfig config, string corePath)
    {
        var args = new StringBuilder();

        args.Append($"-L {EscapeShellArg(corePath)}");

        if (config.Fullscreen)
            args.Append(" --fullscreen");

        if (!string.IsNullOrWhiteSpace(config.ExtraArguments))
        {
            // Escape the entire extra arguments string to prevent shell injection
            args.Append(' ');
            args.Append(EscapeShellArg(config.ExtraArguments));
        }

        args.Append($" {EscapeShellArg(config.RomPath)}");

        return args.ToString();
    }

    private static readonly HashSet<char> InvalidFileNameChars = new(Path.GetInvalidFileNameChars());

    private static string SanitizeFileName(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (char c in name)
        {
            sb.Append(InvalidFileNameChars.Contains(c) ? '_' : c);
        }
        return sb.ToString().Trim();
    }

    private static string EscapePowerShellString(string value)
    {
        return value.Replace("'", "''");
    }

    /// <summary>
    /// Escapes a value for the Exec= line in a .desktop file.
    /// The desktop entry spec requires quoting the executable and escaping
    /// special characters: ", $, `, \, space.
    /// </summary>
    private static string EscapeDesktopExec(string value)
    {
        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        foreach (char c in value)
        {
            if (c is '"' or '$' or '`' or '\\')
                sb.Append('\\');
            sb.Append(c);
        }
        sb.Append('"');
        return sb.ToString();
    }

    /// <summary>
    /// Escapes a value for safe inclusion in a shell script using single-quoting.
    /// Single quotes prevent all shell expansion; the only character that needs
    /// escaping is the single quote itself (replaced with '"'"').
    /// </summary>
    private static string EscapeShellArg(string value)
    {
        return "'" + value.Replace("'", "'\"'\"'") + "'";
    }

    private static string GetCoreLibrarySuffix()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return ".dll";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return ".dylib";
        return ".so";
    }

    private static string? FindCoresDirectory(string retroArchPath)
    {
        string retroArchDir = Path.GetDirectoryName(retroArchPath) ?? string.Empty;
        string coresDir = Path.Combine(retroArchDir, "cores");
        if (Directory.Exists(coresDir))
            return coresDir;

        // Additional platform-specific locations
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] dirs =
            [
                "/usr/lib/libretro",
                "/usr/lib64/libretro",
                "/usr/lib/x86_64-linux-gnu/libretro",
                "/usr/local/lib/libretro",
                Path.Combine(homeDir, ".config", "retroarch", "cores"),
                // Flatpak core locations
                Path.Combine(homeDir,
                    ".var", "app", "org.libretro.RetroArch", "config", "retroarch", "cores"),
                "/var/lib/flatpak/app/org.libretro.RetroArch/current/active/files/lib/retroarch/cores",
                // Snap core locations
                Path.Combine(homeDir,
                    "snap", "retroarch", "current", ".config", "retroarch", "cores"),
            ];
            foreach (string dir in dirs)
            {
                if (Directory.Exists(dir))
                    return dir;
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] dirs =
            [
                Path.Combine(homeDir, "Library", "Application Support", "RetroArch", "cores"),
                "/Applications/RetroArch.app/Contents/Resources/cores",
                Path.Combine(homeDir, "Applications", "RetroArch.app", "Contents", "Resources", "cores"),
                // Homebrew locations
                "/opt/homebrew/lib/retroarch/cores",
                "/usr/local/lib/retroarch/cores",
            ];
            foreach (string dir in dirs)
            {
                if (Directory.Exists(dir))
                    return dir;
            }
        }

        return null;
    }
}
