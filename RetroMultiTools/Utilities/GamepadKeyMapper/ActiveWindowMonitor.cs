using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RetroMultiTools.Utilities.GamepadKeyMapper;

/// <summary>
/// Monitors the currently active (foreground) application window.
/// Used by the mapping engine to switch profiles automatically based on
/// <see cref="AutoProfileRule"/> entries.
/// </summary>
internal static class ActiveWindowMonitor
{
    /// <summary>Returns the title of the currently focused window, or empty string.</summary>
    internal static string GetActiveWindowTitle()
    {
        if (OperatingSystem.IsWindows())
            return GetWindowsActiveTitle();
        if (OperatingSystem.IsLinux())
            return GetLinuxActiveTitle();
        if (OperatingSystem.IsMacOS())
            return GetMacActiveTitle();
        return string.Empty;
    }

    /// <summary>Returns the process name of the currently focused window, or empty string.</summary>
    internal static string GetActiveProcessName()
    {
        if (OperatingSystem.IsWindows())
            return GetWindowsActiveProcessName();
        if (OperatingSystem.IsLinux())
            return GetLinuxActiveProcessName();
        if (OperatingSystem.IsMacOS())
            return GetMacActiveProcessName();
        return string.Empty;
    }

    /// <summary>
    /// Checks whether the given rule matches the current foreground window.
    /// Both WindowTitleMatch and ProcessName are tested (case-insensitive contains).
    /// At least one non-empty field must match.
    /// </summary>
    internal static bool Matches(AutoProfileRule rule)
    {
        bool hasTitle = !string.IsNullOrWhiteSpace(rule.WindowTitleMatch);
        bool hasProcess = !string.IsNullOrWhiteSpace(rule.ProcessName);

        if (!hasTitle && !hasProcess) return false;

        string title = GetActiveWindowTitle();
        string proc = GetActiveProcessName();

        bool titleOk = !hasTitle ||
                       title.Contains(rule.WindowTitleMatch, StringComparison.OrdinalIgnoreCase);
        bool procOk = !hasProcess ||
                      proc.Contains(rule.ProcessName, StringComparison.OrdinalIgnoreCase);

        return titleOk && procOk;
    }

    // ════════════════════════════════════════════════════════════════════
    // Windows – user32.dll
    // ════════════════════════════════════════════════════════════════════

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, char[] lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    private static string GetWindowsActiveTitle()
    {
        try
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return string.Empty;
            char[] buffer = new char[512];
            int len = GetWindowText(hwnd, buffer, buffer.Length);
            return len > 0 ? new string(buffer, 0, len) : string.Empty;
        }
        catch (DllNotFoundException) { return string.Empty; }
    }

    private static string GetWindowsActiveProcessName()
    {
        try
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return string.Empty;
            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0) return string.Empty;
            using var proc = Process.GetProcessById((int)pid);
            return proc.ProcessName;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[ActiveWindowMonitor] GetWindowsActiveProcessName failed: {ex.Message}");
            return string.Empty;
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // Linux – xdotool / xprop
    // ════════════════════════════════════════════════════════════════════

    private static string GetLinuxActiveTitle()
    {
        return RunAndCapture("xdotool", "getactivewindow getwindowname");
    }

    private static string GetLinuxActiveProcessName()
    {
        string pidStr = RunAndCapture("xdotool", "getactivewindow getwindowpid");
        if (int.TryParse(pidStr.Trim(), out int pid) && pid > 0)
        {
            try
            {
                using var proc = Process.GetProcessById(pid);
                return proc.ProcessName;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[ActiveWindowMonitor] GetLinuxActiveProcessName failed: {ex.Message}");
            }
        }
        return string.Empty;
    }

    // ════════════════════════════════════════════════════════════════════
    // macOS – osascript
    // ════════════════════════════════════════════════════════════════════

    private static string GetMacActiveTitle()
    {
        return RunAndCapture("osascript",
            "-e \"tell application \\\"System Events\\\" to get name of first application process whose frontmost is true\"");
    }

    private static string GetMacActiveProcessName()
    {
        // On macOS the frontmost app name IS the process name
        return GetMacActiveTitle();
    }

    // ════════════════════════════════════════════════════════════════════

    private static string RunAndCapture(string fileName, string arguments)
    {
        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            proc.Start();
            string output = proc.StandardOutput.ReadToEnd().Trim();
            if (!proc.WaitForExit(2000))
            {
                try { proc.Kill(); } catch { }
            }
            return output;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            return string.Empty;
        }
    }
}
