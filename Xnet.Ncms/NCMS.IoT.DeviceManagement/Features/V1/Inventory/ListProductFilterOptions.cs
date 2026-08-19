using Mediator;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Data;

namespace NCMS.IoT.DeviceManagement.Features.V1.Inventory;

/// <summary>
/// Loads the full product hierarchy (Series → Type → Model) for the cascading filter
/// dropdowns shared across the Device Management index pages. Returned once per page load;
/// the UI narrows dependent dropdowns client-side, so no per-selection round-trip is needed.
/// </summary>
public static class ListProductFilterOptions
{
    public sealed record Query : IRequest<ProductFilterOptionsDto>;

    public sealed class Handler : IRequestHandler<Query, ProductFilterOptionsDto>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<ProductFilterOptionsDto> Handle(Query _, CancellationToken ct)
        {
            var categories = await _db.ProductCategories
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new ProductCategoryOptionDto(c.Id, c.Name))
                .ToListAsync(ct);

            var types = await _db.ProductTypes
                .AsNoTracking()
                .OrderBy(t => t.Name)
                .Select(t => new ProductTypeOptionDto(t.Id, t.Name, t.ProductCategoryId))
                .ToListAsync(ct);

            var models = await _db.Products
                .AsNoTracking()
                .OrderBy(p => p.Name)
                .Select(p => new ProductModelOptionDto(p.Id, p.Name, p.ProductTypeId))
                .ToListAsync(ct);

            return new ProductFilterOptionsDto(categories, types, models);
        }
    }
}
