using NCMS.Persistence.Pagination;

namespace NCMS.IoT.Host.Models;

/// <summary>
/// View model for the shared <c>_Pager</c> partial. <see cref="Query"/> carries any
/// active filters so they survive page navigation (merged with the page number).
/// </summary>
public sealed class PagerInfo
{
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public required int Total { get; init; }

    /// <summary>Razor page to link to, e.g. "/Devices/Index".</summary>
    public required string PageName { get; init; }

    /// <summary>
    /// Alignment of the pager row: "center" for standard index pages,
    /// "end" (right) for dialog / modal windows.
    /// </summary>
    public string Align { get; init; } = "center";

    /// <summary>
    /// When true (default) the pager is wrapped in a <c>card-footer</c> (index pages that sit
    /// inside a card). Dialogs that render the pager outside a card set this false.
    /// </summary>
    public bool Framed { get; init; } = true;

    /// <summary>Page-size choices offered by the integrated "N / page" selector.</summary>
    public IReadOnlyList<int> PageSizeOptions { get; init; } = [];

    /// <summary>Extra route values (active filters) preserved across pages.</summary>
    public IDictionary<string, string?> Query { get; init; } = new Dictionary<string, string?>();

    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)Total / PageSize) : 0;
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
    public int FirstItem => Total == 0 ? 0 : ((Page - 1) * PageSize) + 1;
    public int LastItem => Math.Min(Page * PageSize, Total);

    // The pagination query parameter is "pageNumber", NOT "page": Razor Pages reserves "page"
    // as the route token that identifies the page itself, so passing it via asp-all-route-data /
    // Url.Page silently drops it from the generated URL. Page models bind [Name = "pageNumber"].
    public const string PageParam = "pageNumber";

    public IDictionary<string, string?> RouteFor(int page)
    {
        var values = new Dictionary<string, string?>(Query) { [PageParam] = page.ToString() };
        return values;
    }

    /// <summary>Route values that switch the page size and reset to page 1, preserving filters.</summary>
    public IDictionary<string, string?> RouteForPageSize(int size)
    {
        var values = new Dictionary<string, string?>(Query)
        {
            ["pageSize"] = size.ToString(),
            [PageParam] = "1"
        };
        return values;
    }

    /// <summary>
    /// Builds a <see cref="PagerInfo"/> from a shared <see cref="PagedResponse{T}"/>, carrying
    /// the supplied active filters so they survive page navigation.
    /// </summary>
    public static PagerInfo From<T>(
        PagedResponse<T> response,
        string pageName,
        IDictionary<string, string?>? query = null,
        string align = "center",
        IReadOnlyList<int>? pageSizeOptions = null,
        bool framed = true)
        => new()
        {
            Page = response.PageNumber,
            PageSize = response.PageSize,
            Total = (int)response.TotalCount,
            PageName = pageName,
            Query = query ?? new Dictionary<string, string?>(),
            Align = align,
            Framed = framed,
            PageSizeOptions = pageSizeOptions ?? []
        };
}
