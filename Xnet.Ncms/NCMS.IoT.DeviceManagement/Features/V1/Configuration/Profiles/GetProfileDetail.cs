using Mediator;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Data;

namespace NCMS.IoT.DeviceManagement.Features.V1.Configuration.Profiles;

/// <summary>Full read model for the config profile "View" screen. Mirrors GetPackageDetail.</summary>
public static class GetProfileDetail
{
    public sealed record Query(Guid Id) : IRequest<ConfigProfileDetailDto?>;

    public sealed class Handler : IRequestHandler<Query, ConfigProfileDetailDto?>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<ConfigProfileDetailDto?> Handle(Query q, CancellationToken ct)
        {
            var p = await _db.ConfigProfiles
                .AsNoTracking()
                .Where(x => x.Id == q.Id)
                .Select(x => new
                {
                    x.Id, x.ProfileName, x.OriginalFilename, x.StoragePath, x.Size,
                    ProductModel = x.Product.Name, x.ProductId, x.Status, x.Remark, x.CreatedBy, x.UploadedAt
                })
                .FirstOrDefaultAsync(ct);

            if (p is null) return null;

            return new ConfigProfileDetailDto(
                p.Id, p.ProfileName, p.OriginalFilename ?? string.Empty, p.StoragePath ?? string.Empty,
                p.Size, p.ProductModel, p.ProductId, p.Status, p.Remark, p.CreatedBy, p.UploadedAt);
        }
    }
}
