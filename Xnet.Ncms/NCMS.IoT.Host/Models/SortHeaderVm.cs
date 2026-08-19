namespace NCMS.IoT.Host.Models;

/// <summary>
/// View model for the shared <c>_SortHeader</c> partial — a server-side sortable column
/// header rendered with Tabler's <c>table-sort</c> styling. Clicking round-trips with an
/// updated <c>sort</c> value ("key" ascending, "-key" descending) while preserving the
/// active filters carried in <see cref="RouteValues"/>.
/// </summary>
public sealed class SortHeaderVm
{
    /// <summary>Whitelisted sort key, matching the handler/specification SortMap (e.g. "serial").</summary>
    public required string Key { get; init; }

    /// <summary>Column label shown to the user.</summary>
    public required string Label { get; init; }

    /// <summary>Currently-applied sort value ("key" or "-key"), or null when unsorted.</summary>
    public string? CurrentSort { get; init; }

    /// <summary>Razor page to link to, e.g. "/Devices/Index".</summary>
    public required string PageName { get; init; }

    /// <summary>Active filters (and page size) preserved across the sort round-trip.</summary>
    public IDictionary<string, string?> RouteValues { get; init; } = new Dictionary<string, string?>();

    public bool IsActive =>
        !string.IsNullOrEmpty(CurrentSort) &&
        string.Equals(CurrentSort.TrimStart('-'), Key, StringComparison.OrdinalIgnoreCase);

    public bool IsDescending => IsActive && CurrentSort!.StartsWith('-');

    /// <summary>Next sort value: unsorted/asc → desc, desc → asc.</summary>
    public string NextSort => IsActive && !IsDescending ? $"-{Key}" : Key;

    /// <summary>Tabler sort-direction css class for the active column.</summary>
    public string DirectionClass => IsActive ? (IsDescending ? "desc" : "asc") : string.Empty;

    public IDictionary<string, string?> RouteForToggle()
    {
        var values = new Dictionary<string, string?>(RouteValues)
        {
            ["sort"] = NextSort,
            [PagerInfo.PageParam] = "1"
        };
        return values;
    }
}
