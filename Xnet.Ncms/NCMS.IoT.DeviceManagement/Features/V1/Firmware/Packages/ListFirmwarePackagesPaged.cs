using Mediator;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Data;
using NCMS.Persistence.Pagination;
using NCMS.Persistence.Specifications;

namespace NCMS.IoT.DeviceManagement.Features.V1.Firmware.Packages;

/// <summary>
/// Filtered, sorted, paginated firmware packages — backs the Firmware Packages index page.
/// Distinct from <see cref="ListFirmwarePackages"/> (which returns a full list used as a
/// firmware selector and must stay non-paginated).
/// </summary>
public static class ListFirmwarePackagesPaged
{
    public sealed record Query(
        string? Name,
        bool? Enabled,
        Guid? ProductId,
        string? Sort,
        int Page,
        int PageSize) : IRequest<PagedResponse<FirmwarePackageListItemDto>>;

    public sealed class Handler : IRequestHandler<Query, PagedResponse<FirmwarePackageListItemDto>>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<PagedResponse<FirmwarePackageListItemDto>> Handle(Query q, CancellationToken ct)
        {
            var spec = new FirmwarePackagesSpecification(
                new FirmwarePackageFilter(q.Name, q.Enabled, q.ProductId, q.Sort));

            var paged = await _db.Firmwares
                .ApplySpecification(spec)
                .ToPagedResponseAsync(new PagedQuery(q.Page, q.PageSize), ct);

            // Join product names into the display string in memory (not SQL-translatable).
            var items = paged.Items
                .Select(r => new FirmwarePackageListItemDto(
                    r.Id, r.Name, r.Version, string.Join(", ", r.Products), r.Size, r.UploadedAt, r.IsEnabled))
                .ToList();

            return new PagedResponse<FirmwarePackageListItemDto>
            {
                Items = items,
                PageNumber = paged.PageNumber,
                PageSize = paged.PageSize,
                TotalCount = paged.TotalCount,
                TotalPages = paged.TotalPages
            };
        }
    }
}
