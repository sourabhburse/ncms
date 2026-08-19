namespace NCMS.IoT.DeviceManagement.Features.V1.AppPackages.Packages;

/// <summary>
/// Normalises free-form package tags before persistence: trims whitespace, drops empties, and
/// removes case-insensitive duplicates while preserving first-seen order. Keeps the stored
/// <c>text[]</c> tidy regardless of how the client submits tags.
/// </summary>
internal static class TagNormalizer
{
    public static List<string> Normalize(IEnumerable<string>? tags)
    {
        if (tags is null) return [];

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var raw in tags)
        {
            var t = raw?.Trim();
            if (string.IsNullOrEmpty(t)) continue;
            if (seen.Add(t)) result.Add(t);
        }
        return result;
    }
}
