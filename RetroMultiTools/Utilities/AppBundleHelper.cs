using System.Runtime.InteropServices;
using System.Xml;

namespace RetroMultiTools.Utilities;

/// <summary>
/// Shared helpers for resolving macOS .app bundle paths to their executables.
/// Used by RetroArchLauncher, MameLauncher, and MednafenLauncher.
/// </summary>
public static class AppBundleHelper
{
    /// <summary>
    /// Resolves a macOS .app bundle to the executable inside Contents/MacOS/.
    /// Tries, in order:
    ///   1. Exact match for the expected executable name.
    ///   2. Case-insensitive match for the expected executable name.
    ///   3. The executable declared in the bundle's Info.plist (CFBundleExecutable).
    ///   4. The bundle directory name as executable name (e.g. "RetroArch" from "RetroArch.app").
    ///   5. The sole executable in Contents/MacOS/ when there is exactly one file.
    /// Returns null if the bundle structure is invalid or no executable is found.
    /// </summary>
    public static string? ResolveAppBundleExecutable(string bundlePath, string executableName)
    {
        string macosDir = Path.Combine(bundlePath, "Contents", "MacOS");
        if (!Directory.Exists(macosDir))
            return null;

        try
        {
            // 1. Exact match for the expected name
            string exact = Path.Combine(macosDir, executableName);
            if (File.Exists(exact))
                return exact;

            // Enumerate the directory once; used for case-insensitive match
            // and the single-file fallback below.
            string[] files = Directory.GetFiles(macosDir);

            // 2. Case-insensitive match for the expected name
            foreach (string file in files)
            {
                if (string.Equals(Path.GetFileName(file), executableName, StringComparison.OrdinalIgnoreCase))
                    return file;
            }

            // 3. Read CFBundleExecutable from Info.plist
            string? plistExe = ReadBundleExecutableName(bundlePath);
            if (!string.IsNullOrEmpty(plistExe))
            {
                string plistPath = Path.Combine(macosDir, plistExe);
                if (File.Exists(plistPath))
                    return plistPath;
            }

            // 4. Use the bundle name itself as a hint (e.g. "RetroArch" from "RetroArch.app").
            //    This handles binary plists that the XML parser cannot read and bundles
            //    whose executable name differs from the expected lowercase name.
            string bundleName = Path.GetFileNameWithoutExtension(bundlePath);
            if (!string.Equals(bundleName, executableName, StringComparison.OrdinalIgnoreCase))
            {
                string bundleNamePath = Path.Combine(macosDir, bundleName);
                if (File.Exists(bundleNamePath))
                    return bundleNamePath;

                // Case-insensitive match for the bundle name
                foreach (string file in files)
                {
                    if (string.Equals(Path.GetFileName(file), bundleName, StringComparison.OrdinalIgnoreCase))
                        return file;
                }
            }

            // 5. If there is exactly one file in Contents/MacOS/, assume it is the executable
            if (files.Length == 1)
                return files[0];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }

        return null;
    }

    /// <summary>
    /// Reads the CFBundleExecutable value from a bundle's Contents/Info.plist.
    /// Returns null if the file is missing, malformed, or the key is absent.
    /// </summary>
    private static string? ReadBundleExecutableName(string bundlePath)
    {
        string plistPath = Path.Combine(bundlePath, "Contents", "Info.plist");
        if (!File.Exists(plistPath))
            return null;

        try
        {
            var doc = new XmlDocument();
            doc.Load(plistPath);

            // Info.plist is an XML property list: <plist><dict><key>…</key><string>…</string>…
            var keys = doc.GetElementsByTagName("key");
            foreach (XmlNode key in keys)
            {
                if (key.InnerText == "CFBundleExecutable")
                {
                    // The value element immediately follows the <key> element
                    var sibling = key.NextSibling;
                    // Skip whitespace/text nodes
                    while (sibling != null && sibling.NodeType != XmlNodeType.Element)
                        sibling = sibling.NextSibling;

                    if (sibling != null && sibling.Name == "string")
                        return sibling.InnerText;
                }
            }
        }
        catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException) { }

        return null;
    }

    /// <summary>
    /// Searches a directory for a .app bundle whose executable matches the expected name.
    /// For example, given "/Applications" and "retroarch", finds "/Applications/RetroArch.app"
    /// if it contains a valid RetroArch executable.
    /// Returns the .app bundle path (not the executable path) if found, null otherwise.
    /// </summary>
    public static string? FindAppBundleInDirectory(string directory, string executableName)
    {
        if (!Directory.Exists(directory))
            return null;

        try
        {
            foreach (string entry in Directory.GetDirectories(directory, "*.app"))
            {
                string? exe = ResolveAppBundleExecutable(entry, executableName);
                if (exe != null && File.Exists(exe))
                    return entry;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }

        return null;
    }

    /// <summary>
    /// If the given path is inside a macOS .app bundle, returns the bundle root directory.
    /// For example, given "/Applications/RetroArch.app/Contents/MacOS/RetroArch",
    /// returns "/Applications/RetroArch.app". Returns null if the path is not inside a bundle.
    /// </summary>
    public static string? GetAppBundleRoot(string executablePath)
    {
        // Walk up the directory tree looking for a directory ending in .app
        string? current = Path.GetDirectoryName(executablePath);
        while (!string.IsNullOrEmpty(current))
        {
            if (current.EndsWith(".app", StringComparison.OrdinalIgnoreCase) && Directory.Exists(current))
                return current;

            string? parent = Path.GetDirectoryName(current);
            if (parent == current)
                break;
            current = parent;
        }
        return null;
    }
}
