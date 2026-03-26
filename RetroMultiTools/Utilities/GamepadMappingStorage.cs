using RetroMultiTools.Services;

namespace RetroMultiTools.Utilities;

/// <summary>
/// Manages local storage of user-created or modified SDL2 game controller mappings.
/// Mappings are stored in <c>custom_mappings.txt</c> in the application data directory
/// using the standard SDL2 mapping string format. Each line is a complete mapping of
/// the form: <c>GUID,Name,button_mappings,platform:OS</c>.
/// </summary>
public static class GamepadMappingStorage
{
    private static readonly string _storagePath;

    static GamepadMappingStorage()
    {
        string appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RetroMultiTools");
        Directory.CreateDirectory(appData);
        _storagePath = Path.Combine(appData, "custom_mappings.txt");
    }

    /// <summary>Returns the full path to the custom mappings file.</summary>
    public static string StoragePath => _storagePath;

    /// <summary>
    /// Loads all custom mappings from the storage file.
    /// Returns an empty list if the file does not exist or is unreadable.
    /// </summary>
    public static List<string> LoadMappings()
    {
        var mappings = new List<string>();

        try
        {
            if (!File.Exists(_storagePath))
                return mappings;

            foreach (string line in File.ReadLines(_storagePath))
            {
                string trimmed = line.Trim();
                if (trimmed.Length > 0 && !trimmed.StartsWith('#'))
                    mappings.Add(trimmed);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.WriteLine(
                $"[GamepadMappingStorage] Failed to read custom mappings: {ex.Message}");
        }

        return mappings;
    }

    /// <summary>
    /// Saves or updates a mapping. If a mapping with the same GUID and platform
    /// already exists, it is replaced; otherwise the new mapping is appended.
    /// </summary>
    public static void SaveMapping(string mappingString)
    {
        string guid = ExtractGuid(mappingString);
        string platform = ExtractPlatform(mappingString);

        var existing = LoadMappings();
        bool replaced = false;

        for (int i = 0; i < existing.Count; i++)
        {
            if (string.Equals(ExtractGuid(existing[i]), guid, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ExtractPlatform(existing[i]), platform, StringComparison.OrdinalIgnoreCase))
            {
                existing[i] = mappingString;
                replaced = true;
                break;
            }
        }

        if (!replaced)
            existing.Add(mappingString);

        WriteAll(existing);
    }

    /// <summary>
    /// Removes a mapping by GUID and platform.
    /// </summary>
    public static bool RemoveMapping(string guid, string platform)
    {
        var existing = LoadMappings();
        int removed = existing.RemoveAll(m =>
            string.Equals(ExtractGuid(m), guid, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(ExtractPlatform(m), platform, StringComparison.OrdinalIgnoreCase));

        if (removed > 0)
        {
            WriteAll(existing);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Applies all stored custom mappings to the SDL2 runtime using
    /// <see cref="SDL2Interop.SDL_GameControllerAddMapping"/>.
    /// Returns the number of mappings successfully applied.
    /// </summary>
    public static int ApplyAllToSdl()
    {
        int applied = 0;
        foreach (string mapping in LoadMappings())
        {
            try
            {
                int result = SDL2Interop.SDL_GameControllerAddMapping(mapping);
                if (result >= 0)
                    applied++;
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
            {
                System.Diagnostics.Trace.WriteLine(
                    $"[GamepadMappingStorage] Failed to apply mapping: {ex.Message}");
            }
        }

        return applied;
    }

    /// <summary>
    /// Returns the number of custom mappings currently stored.
    /// </summary>
    public static int Count()
    {
        return LoadMappings().Count;
    }

    /// <summary>Extracts the GUID (first field) from an SDL mapping string.</summary>
    internal static string ExtractGuid(string mapping)
    {
        int comma = mapping.IndexOf(',');
        return comma > 0 ? mapping[..comma] : string.Empty;
    }

    /// <summary>Extracts the controller name (second field) from an SDL mapping string.</summary>
    internal static string ExtractName(string mapping)
    {
        int first = mapping.IndexOf(',');
        if (first < 0) return string.Empty;
        int second = mapping.IndexOf(',', first + 1);
        return second > first ? mapping[(first + 1)..second] : mapping[(first + 1)..];
    }

    /// <summary>Extracts the platform value from an SDL mapping string.</summary>
    internal static string ExtractPlatform(string mapping)
    {
        const string key = "platform:";
        int idx = mapping.IndexOf(key, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return string.Empty;

        int start = idx + key.Length;
        int end = mapping.IndexOf(',', start);
        return end > start ? mapping[start..end] : mapping[start..];
    }

    /// <summary>Returns the SDL2 platform string for the current OS.</summary>
    public static string GetCurrentPlatform()
    {
        if (OperatingSystem.IsWindows()) return "Windows";
        if (OperatingSystem.IsLinux()) return "Linux";
        if (OperatingSystem.IsMacOS()) return "Mac OS X";
        return "Unknown";
    }

    private static void WriteAll(List<string> mappings)
    {
        string tempPath = _storagePath + ".tmp";
        try
        {
            string header = $"# RetroMultiTools custom controller mappings{Environment.NewLine}" +
                            $"# Generated by the SDL2 Gamepad Tool{Environment.NewLine}";

            File.WriteAllText(tempPath, header + string.Join(Environment.NewLine, mappings) + Environment.NewLine);
            File.Move(tempPath, _storagePath, overwrite: true);
        }
        catch (IOException ex)
        {
            try { File.Delete(tempPath); } catch { /* best-effort cleanup */ }
            System.Diagnostics.Trace.WriteLine(
                $"[GamepadMappingStorage] Failed to write custom mappings: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            try { File.Delete(tempPath); } catch { /* best-effort cleanup */ }
            System.Diagnostics.Trace.WriteLine(
                $"[GamepadMappingStorage] Permission denied writing custom mappings: {ex.Message}");
        }
    }
}
