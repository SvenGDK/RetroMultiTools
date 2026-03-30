namespace RetroMultiTools.Utilities;

/// <summary>
/// Provides centralized path safety validation to prevent
/// path-traversal (zip-slip) attacks in batch file operations.
/// </summary>
public static class PathValidator
{
    /// <summary>
    /// Returns <c>true</c> when <paramref name="candidatePath"/> resolves
    /// to a location inside <paramref name="baseDirectory"/>.
    /// Use this whenever a relative path derived from user-controlled data
    /// (e.g. <see cref="System.IO.Path.GetRelativePath"/>) is combined
    /// with an output directory.
    /// </summary>
    public static bool IsPathSafe(string candidatePath, string baseDirectory)
    {
        string fullCandidate = Path.GetFullPath(candidatePath);
        string fullBase = Path.GetFullPath(baseDirectory);
        if (!fullBase.EndsWith(Path.DirectorySeparatorChar))
            fullBase += Path.DirectorySeparatorChar;

        // Use case-insensitive comparison on Windows where the file system
        // is case-insensitive; ordinal elsewhere.
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return fullCandidate.StartsWith(fullBase, comparison);
    }
}
