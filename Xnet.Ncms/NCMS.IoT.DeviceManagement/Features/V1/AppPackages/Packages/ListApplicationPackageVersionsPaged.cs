using Mediator;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Data;
using NCMS.Persistence.Pagination;
using NCMS.Persistence.Specifications;

namespace NCMS.IoT.DeviceManagement.Features.V1.AppPackages.Packages;

/// <summary>
/// Filtered, sorted, paginated flat list of every version across all packages — backs the
/// merged Application Packages index page (mirrors the Firmware Packages page). Distinct from
/// <see cref="ListApplicationPackageVersions"/> (which returns a full list for bundle/task
/// selectors and must stay non-paginated).
/// </summary>
public static class ListApplicationPackageVersionsPaged
{
    public sealed record Query(
        string? Name,
        string? Version,
        bool? Enabled,
        Guid? ProductId,
        string? Tag,
        string? Sort,
        int Page,
        int PageSize) : IRequest<PagedResponse<ApplicationPackageVersionListItemDto>>;

    public sealed class Handler : IRequestHandler<Query, PagedResponse<ApplicationPackageVersionListItemDto>>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<PagedResponse<ApplicationPackageVersionListItemDto>> Handle(Query q, CancellationToken ct)
        {
            var spec = new ApplicationPackageVersionsSpecification(
                new ApplicationVersionFilter(q.Name, q.Version, q.Enabled, q.ProductId, q.Tag, q.Sort));

            return await _db.ApplicationPackageVersions
                .ApplySpecification(spec)
                .ToPagedResponseAsync(new PagedQuery(q.Page, q.PageSize), ct);
        }
    }
}
