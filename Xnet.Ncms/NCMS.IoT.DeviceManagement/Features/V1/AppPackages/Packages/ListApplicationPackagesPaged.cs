using System.Linq.Expressions;
using Mediator;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Data;
using NCMS.IoT.DeviceManagement.Entities;
using NCMS.Persistence.Pagination;
using NCMS.Persistence.Specifications;

namespace NCMS.IoT.DeviceManagement.Features.V1.AppPackages.Packages;

/// <summary>Query composition for the Application Packages index: whitelisted server-side sort + projection.</summary>
public sealed class ApplicationPackagesSpecification : Specification<ApplicationPackage, ApplicationPackageDto>
{
    private static readonly IReadOnlyDictionary<string, Expression<Func<ApplicationPackage, object>>> SortMap =
        new Dictionary<string, Expression<Func<ApplicationPackage, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = p => p.Name,
            ["versions"] = p => p.Versions.Count,
            ["created"] = p => p.CreatedAt,
        };

    public ApplicationPackagesSpecification(string? sort)
    {
        ApplySortingOverride(sort, () => OrderBy(p => p.Name), SortMap);
        Select(p => new ApplicationPackageDto(p.Id, p.Name, p.Tags, p.Versions.Count, p.CreatedAt));
    }
}

/// <summary>
/// Paginated application packages for the management grid (shared pagination). Distinct from
/// <see cref="ListApplicationPackages"/> (a full list reused as a selector).
/// </summary>
public static class ListApplicationPackagesPaged
{
    public sealed record Query(string? Sort, int Page, int PageSize) : IRequest<PagedResponse<ApplicationPackageDto>>;

    public sealed class Handler : IRequestHandler<Query, PagedResponse<ApplicationPackageDto>>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<PagedResponse<ApplicationPackageDto>> Handle(Query q, CancellationToken ct) =>
            await _db.ApplicationPackages
                .ApplySpecification(new ApplicationPackagesSpecification(q.Sort))
                .ToPagedResponseAsync(new PagedQuery(q.Page, q.PageSize), ct);
    }
}
