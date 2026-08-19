using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Entities;
using NCMS.Persistence.Specifications;

namespace NCMS.IoT.DeviceManagement.Features.V1.Inventory;

/// <summary>
/// Filter/sort parameters for <see cref="HardwareInventorySpecification"/>. The three
/// product-hierarchy ids combine as AND. <paramref name="Sort"/> uses the shared "-key"
/// convention.
/// </summary>
public sealed record HardwareInventoryFilter(
    string? Search,
    Guid? CategoryId,
    Guid? TypeId,
    Guid? ProductId,
    string? Sort);

/// <summary>
/// Query composition for the Hardware Inventory index: text/product-hierarchy filtering,
/// whitelisted server-side sort, projection to the list DTO. Global query filters are ignored
/// so inventory rows whose Product has been soft-deleted are still listed (matching prior behaviour).
/// </summary>
public sealed class HardwareInventorySpecification : Specification<HardwareInventory, HardwareInventoryDto>
{
    private static readonly IReadOnlyDictionary<string, Expression<Func<HardwareInventory, object>>> SortMap =
        new Dictionary<string, Expression<Func<HardwareInventory, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["serial"] = h => h.SerialNumber,
            ["product"] = h => h.Product.Name,
            ["status"] = h => h.Status,
            ["policy"] = h => h.IdentityPolicy,
            ["registered"] = h => h.CreatedAt,
        };

    public HardwareInventorySpecification(HardwareInventoryFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = $"%{filter.Search.Trim()}%";
            Where(h =>
                EF.Functions.ILike(h.SerialNumber, term) ||
                EF.Functions.ILike(h.Product.Name, term) ||
                EF.Functions.ILike(h.Status, term) ||
                EF.Functions.ILike(h.IdentityPolicy, term));
        }

        if (filter.CategoryId is { } categoryId)
            Where(h => h.Product.ProductType.ProductCategoryId == categoryId);
        if (filter.TypeId is { } typeId)
            Where(h => h.Product.ProductTypeId == typeId);
        if (filter.ProductId is { } productId)
            Where(h => h.ProductId == productId);

        // Show inventory even when its Product has been soft-deleted.
        IgnoreQueryFiltersEnabled();
        Include(h => h.Product);

        ApplySortingOverride(
            filter.Sort,
            applyDefaultOrdering: () => OrderByDescending(h => h.CreatedAt),
            SortMap);

        Select(h => new HardwareInventoryDto(
            h.Id,
            h.ProductId,
            h.Product.Name,
            h.SerialNumber,
            h.IsProvisioned,
            h.Status,
            h.IdentityPolicy,
            h.IdentityClaims,
            h.CreatedAt,
            h.UpdatedAt));
    }
}
