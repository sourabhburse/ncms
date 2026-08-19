using Mediator;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Data;

namespace NCMS.IoT.DeviceManagement.Features.V1.Firmware.Packages;

/// <summary>
/// Lists firmware packages for the management grid, including the supported
/// product model(s) each package targets (the source of upgrade eligibility).
/// </summary>
public static class ListFirmwarePackages
{
    public sealed record Query : IRequest<List<FirmwarePackageListItemDto>>;

    public sealed class Handler : IRequestHandler<Query, List<FirmwarePackageListItemDto>>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<List<FirmwarePackageListItemDto>> Handle(Query _, CancellationToken ct)
        {
            var rows = await _db.Firmwares
                .AsNoTracking()
                .OrderByDescending(p => p.UploadedAt)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Version,
                    p.Size,
                    p.UploadedAt,
                    p.IsEnabled,
                    Products = p.SupportedProducts.Select(sp => sp.Product.Name).ToList()
                })
                .ToListAsync(ct);

            return rows
                .Select(r => new FirmwarePackageListItemDto(
                    r.Id, r.Name, r.Version,
                    string.Join(", ", r.Products),
                    r.Size, r.UploadedAt, r.IsEnabled))
                .ToList();
        }
    }
}
