namespace VideoAnalysis.Core.Storage;

/// <summary>
/// Suggests output file names/paths for a generated long-exposure (or slit-scan) image, parallel
/// to <see cref="VideoFileNamer"/>'s job for renaming source videos. Used by both Long Exposure
/// and Slit Scan, which share the same save workflow per spec.
/// </summary>
public static class LongExposureFileNamer
{
    /// <summary>Builds a suggested file name from whichever of system/body/station name are
    /// available, plus the variant, e.g. "Sol Earth Daedalus (Average).png". Callers should still
    /// let the user edit this before saving.</summary>
    public static string SuggestFileName(string systemName, string? bodyName, string? stationName, string variantDisplayName, string extension)
    {
        var parts = new List<string> { systemName };
        if (!string.IsNullOrWhiteSpace(bodyName))
        {
            parts.Add(bodyName);
        }
        if (!string.IsNullOrWhiteSpace(stationName))
        {
            parts.Add(stationName);
        }

        var baseName = string.Join(" ", parts);
        return $"{Sanitize(baseName)} ({variantDisplayName}){extension}";
    }

    /// <summary>Suggested output directory: <paramref name="outputRoot"/> with an automatic
    /// subdirectory named after the system, per spec.</summary>
    public static string SuggestDirectory(string outputRoot, string systemName)
        => Path.Combine(outputRoot, Sanitize(systemName));

    public static bool WouldOverwrite(string fullPath) => File.Exists(fullPath);

    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c)).Trim();
    }
}
