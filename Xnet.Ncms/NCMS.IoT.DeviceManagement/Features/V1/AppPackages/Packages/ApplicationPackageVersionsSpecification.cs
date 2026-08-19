using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Entities;
using NCMS.Persistence.Specifications;

namespace NCMS.IoT.DeviceManagement.Features.V1.AppPackages.Packages;

/// <summary>
/// Filter/sort parameters for the merged Application Packages index page — a flat list of every
/// version across all packages (mirrors the Firmware Packages page). All filters are optional:
/// <paramref name="Name"/> matches the owning package name, <paramref name="Version"/> the
/// version text, plus enabled-state and product-compatibility. <paramref name="Sort"/> uses the
/// shared "-key" convention.
/// </summary>
public sealed record ApplicationVersionFilter(
    string? Name,
    string? Version,
    bool? Enabled,
    Guid? ProductId,
    string? Tag,
    string? Sort);

/// <summary>
/// Query composition for application package versions. Two shapes share one projection:
/// the selector form (scoped by package, optionally deployable-only) used by bundle/task
/// pickers, and the page form (<see cref="ApplicationVersionFilter"/>) with text/status/product
/// filtering and whitelisted server-side sorting.
/// </summary>
public sealed class ApplicationPackageVersionsSpecification : Specification<ApplicationPackageVersion, ApplicationPackageVersionListItemDto>
{
    private static readonly Expression<Func<ApplicationPackageVersion, ApplicationPackageVersionListItemDto>> Projection =
        v => new ApplicationPackageVersionListItemDto(
            v.Id, v.ApplicationPackageId, v.ApplicationPackage.Name, v.ApplicationPackage.Tags,
            v.Version, v.PackageFormat,
            v.IsEnabled, v.SizeBytes, v.UploadedAt,
            !string.IsNullOrEmpty(v.Sha256Checksum), v.SupportedProducts.Count,
            v.SupportedProducts.Select(sp => sp.Product.Name).ToList());

    private static readonly IReadOnlyDictionary<string, Expression<Func<ApplicationPackageVersion, object>>> SortMap =
        new Dictionary<string, Expression<Func<ApplicationPackageVersion, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = v => v.ApplicationPackage.Name,
            ["version"] = v => v.Version,
            ["format"] = v => v.PackageFormat,
            ["status"] = v => v.IsEnabled,
            ["size"] = v => v.SizeBytes,
            ["uploaded"] = v => v.UploadedAt,
        };

    /// <summary>Selector form: scoped to one package (or all), optionally deployable-only, newest first.</summary>
    public ApplicationPackageVersionsSpecification(Guid? applicationPackageId, bool deployableOnly)
    {
        if (applicationPackageId is { } packageId)
            Where(v => v.ApplicationPackageId == packageId);

        if (deployableOnly)
            Where(v => v.IsEnabled);

        Include(v => v.ApplicationPackage);
        OrderByDescending(v => v.UploadedAt);
        Select(Projection);
    }

    /// <summary>Page form: filtered/sorted flat list of every version across all packages.</summary>
    public ApplicationPackageVersionsSpecification(ApplicationVersionFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (!string.IsNullOrWhiteSpace(filter.Name))
        {
            var nameTerm = $"%{filter.Name.Trim()}%";
            Where(v => EF.Functions.ILike(v.ApplicationPackage.Name, nameTerm));
        }

        if (!string.IsNullOrWhiteSpace(filter.Version))
        {
            var term = $"%{filter.Version.Trim()}%";
            Where(v => EF.Functions.ILike(v.Version, term));
        }

        if (filter.Enabled is { } enabled)
            Where(v => v.IsEnabled == enabled);

        if (filter.ProductId is { } productId)
            Where(v => v.SupportedProducts.Any(sp => sp.ProductId == productId));

        if (!string.IsNullOrWhiteSpace(filter.Tag))
        {
            var tagTerm = $"%{filter.Tag.Trim()}%";
            Where(v => v.ApplicationPackage.Tags.Any(t => EF.Functions.ILike(t, tagTerm)));
        }

        Include(v => v.ApplicationPackage);

        ApplySortingOverride(
            filter.Sort,
            applyDefaultOrdering: () => OrderByDescending(v => v.UploadedAt),
            SortMap);

        Select(Projection);
    }
}
