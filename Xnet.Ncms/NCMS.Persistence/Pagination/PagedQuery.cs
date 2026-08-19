namespace NCMS.Persistence.Pagination;

/// <summary>
/// Concrete <see cref="IPagedQuery"/> for handlers that need to pass paging parameters into
/// <see cref="PaginationExtensions.ToPagedResponseAsync{T}"/> without their own request type
/// implementing the interface. Page-number/size normalization (and the max-size cap) is
/// applied by <c>ToPagedResponseAsync</c>, so raw values may be passed here directly.
/// </summary>
public sealed class PagedQuery : IPagedQuery
{
    public int? PageNumber { get; set; }
    public int? PageSize { get; set; }
    public string? Sort { get; set; }

    public PagedQuery() { }

    public PagedQuery(int? pageNumber, int? pageSize, string? sort = null)
    {
        PageNumber = pageNumber;
        PageSize = pageSize;
        Sort = sort;
    }
}
