using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Entities;
using NCMS.Persistence.Specifications;

namespace NCMS.IoT.DeviceManagement.Features.V1.Devices;

/// <summary>
/// Filter/sort parameters for <see cref="DevicesSpecification"/>. The three product-hierarchy
/// ids combine as AND, so any partial selection (Series only, Series+Type, or full Model) works.
/// <paramref name="Sort"/> uses the shared "-key" convention ("-lastSeen" = descending).
/// </summary>
public sealed record DeviceFilter(
    string? Search,
    Guid? CategoryId,
    Guid? TypeId,
    Guid? ProductId,
    string? Sort);

/// <summary>
/// Query composition for the Devices index: server-side text/product-hierarchy filtering,
/// whitelisted server-side sorting, and projection straight to the list DTO.
/// </summary>
public sealed class DevicesSpecification : Specification<Device, DeviceListItemDto>
{
    // Whitelisted, reflection-free sort keys → strongly-typed selectors. Keys match the
    // data-sort identifiers rendered by the Advanced Table headers.
    private static readonly IReadOnlyDictionary<string, Expression<Func<Device, object>>> SortMap =
        new Dictionary<string, Expression<Func<Device, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["serial"] = d => d.HardwareInventory.SerialNumber,
            ["firmware"] = d => d.FirmwareVersion!,
            ["agent"] = d => d.AgentVersion!,
            ["status"] = d => d.IsOnline,
            ["lastSeen"] = d => d.LastSeenAt!,
            ["activated"] = d => d.ActivatedAt,
        };

    public DevicesSpecification(DeviceFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = $"%{filter.Search.Trim()}%";
            Where(d =>
                EF.Functions.ILike(d.HardwareInventory.SerialNumber, term) ||
                (d.Name != null && EF.Functions.ILike(d.Name, term)) ||
                (d.WanIpAddress != null && EF.Functions.ILike(d.WanIpAddress, term)) ||
                EF.Functions.ILike(d.Status, term));
        }

        // Product hierarchy: each provided level narrows further (combined via AND).
        if (filter.CategoryId is { } categoryId)
            Where(d => d.HardwareInventory.Product.ProductType.ProductCategoryId == categoryId);
        if (filter.TypeId is { } typeId)
            Where(d => d.HardwareInventory.Product.ProductTypeId == typeId);
        if (filter.ProductId is { } productId)
            Where(d => d.HardwareInventory.ProductId == productId);

        Include(d => d.HardwareInventory);

        ApplySortingOverride(
            filter.Sort,
            applyDefaultOrdering: () =>
            {
                OrderByDescending(d => d.LastSeenAt!);
                ThenByDescending(d => d.ActivatedAt);
            },
            SortMap);

        Select(d => new DeviceListItemDto(
            d.Id,
            d.HardwareInventory.SerialNumber,
            d.HardwareModel,
            d.Name,
            d.FirmwareVersion,
            d.AgentVersion,
            d.IsOnline,
            d.Status,
            d.WanIpAddress,
            d.MacAddresses,
            d.LastSeenAt,
            d.ActivatedAt,
            d.CreatedAt,
            d.UpdatedAt));
    }
}
