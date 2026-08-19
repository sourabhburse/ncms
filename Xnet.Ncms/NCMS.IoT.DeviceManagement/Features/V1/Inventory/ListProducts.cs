using Mediator;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Data;

namespace NCMS.IoT.DeviceManagement.Features.V1.Inventory;

public static class ListProducts
{
    public sealed record Query : IRequest<List<ProductSelectDto>>;

    public sealed class Handler : IRequestHandler<Query, List<ProductSelectDto>>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<List<ProductSelectDto>> Handle(Query _, CancellationToken ct) =>
            await _db.Products
                .AsNoTracking()
                .Include(p => p.ProductType)
                    .ThenInclude(t => t.ProductCategory)
                .OrderBy(p => p.ProductType.ProductCategory.Name)
                    .ThenBy(p => p.ProductType.Name)
                    .ThenBy(p => p.Name)
                .Select(p => new ProductSelectDto(
                    p.Id,
                    p.ProductType.ProductCategory.Name,
                    p.ProductType.Name,
                    p.Name))
                .ToListAsync(ct);
    }
}
