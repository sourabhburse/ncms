namespace NCMS.Persistence.Pagination;

public interface IPagedQuery
{
    /// <summary>
    /// 1-based page number. Values less than 1 are normalized to 1.
    /// </summary>
    int? PageNumber { get; set; }

    /// <summary>
    /// Requested page size. Implementations may enforce caps.
    /// </summary>
    int? PageSize { get; set; }

    /// <summary>
    /// Multi-column sort expression, for example: "Name,-CreatedOn".
    /// "-" prefix indicates descending order.
    ///
    /// Not currently applied by <see cref="PaginationExtensions.ToPagedResponseAsync{T}"/> —
    /// the source query must already be ordered by the caller. Combine with
    /// <see cref="NCMS.Persistence.Specifications.Specification{T}.ApplySortingOverride"/> if free-text
    /// sorting is actually needed.
    /// </summary>
    string? Sort { get; set; }
}
