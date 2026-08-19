using Mediator;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Data;

namespace NCMS.IoT.DeviceManagement.Features.V1.Firmware.Packages;

/// <summary>
/// Full read model for the firmware package "View" screen — includes the
/// linked product model(s) that drive upgrade eligibility.
/// </summary>
public static class GetPackageDetail
{
    public sealed record Query(Guid Id) : IRequest<FirmwarePackageDetailDto?>;

    public sealed class Handler : IRequestHandler<Query, FirmwarePackageDetailDto?>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<FirmwarePackageDetailDto?> Handle(Query q, CancellationToken ct)
        {
            var p = await _db.Firmwares
                .AsNoTracking()
                .Where(f => f.Id == q.Id)
                .Select(f => new
                {
                    f.Id,
                    f.Name,
                    f.Version,
                    f.FileName,
                    f.StoragePath,
                    f.Size,
                    f.Type,
                    f.Description,
                    f.CreatedBy,
                    f.UploadedAt,
                    f.IsEnabled,
                    Products = f.SupportedProducts.Select(sp => sp.Product.Name).ToList(),
                    ProductIds = f.SupportedProducts.Select(sp => sp.ProductId).ToList()
                })
                .FirstOrDefaultAsync(ct);

            if (p is null) return null;

            return new FirmwarePackageDetailDto(
                p.Id, p.Name, p.Version, p.FileName ?? string.Empty, p.StoragePath ?? string.Empty,
                p.Size, p.Type, p.Description, p.CreatedBy,
                p.UploadedAt, p.IsEnabled, p.Products, p.ProductIds);
        }
    }
}
