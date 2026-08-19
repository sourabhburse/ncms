using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NCMS.Persistence.Specifications;
using FirmwareEntity = NCMS.IoT.DeviceManagement.Entities.Firmware;

namespace NCMS.IoT.DeviceManagement.Features.V1.Firmware.Packages;

/// <summary>
/// Filter/sort parameters for the Firmware Packages index. <paramref name="Sort"/> uses the
/// shared "-key" convention.
/// </summary>
public sealed record FirmwarePackageFilter(
    string? Name,
    bool? Enabled,
    Guid? ProductId,
    string? Sort);

/// <summary>
/// Intermediate projection row: EF-translatable (product names as a collection subquery).
/// The handler joins <see cref="Products"/> into the DTO's display string in memory, since
/// string.Join over a navigation isn't SQL-translatable.
/// </summary>
public sealed record FirmwarePackageRow(
    Guid Id,
    string Name,
    string Version,
    IReadOnlyList<string> Products,
    long Size,
    DateTimeOffset UploadedAt,
    bool IsEnabled);

/// <summary>
/// Query composition for the Firmware Packages index: name text / status / product-support
/// filtering and whitelisted server-side sorting, projected to the intermediate row.
/// </summary>
public sealed class FirmwarePackagesSpecification : Specification<FirmwareEntity, FirmwarePackageRow>
{
    private static readonly IReadOnlyDictionary<string, Expression<Func<FirmwareEntity, object>>> SortMap =
        new Dictionary<string, Expression<Func<FirmwareEntity, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = f => f.Name,
            ["version"] = f => f.Version,
            ["size"] = f => f.Size,
            ["status"] = f => f.IsEnabled,
            ["uploaded"] = f => f.UploadedAt,
        };

    public FirmwarePackagesSpecification(FirmwarePackageFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (!string.IsNullOrWhiteSpace(filter.Name))
        {
            var term = $"%{filter.Name.Trim()}%";
            Where(f => EF.Functions.ILike(f.Name, term));
        }

        if (filter.Enabled is { } enabled)
            Where(f => f.IsEnabled == enabled);

        if (filter.ProductId is { } productId)
            Where(f => f.SupportedProducts.Any(sp => sp.ProductId == productId));

        ApplySortingOverride(
            filter.Sort,
            applyDefaultOrdering: () => OrderByDescending(f => f.UploadedAt),
            SortMap);

        Select(f => new FirmwarePackageRow(
            f.Id,
            f.Name,
            f.Version,
            f.SupportedProducts.Select(sp => sp.Product.Name).ToList(),
            f.Size,
            f.UploadedAt,
            f.IsEnabled));
    }
}
