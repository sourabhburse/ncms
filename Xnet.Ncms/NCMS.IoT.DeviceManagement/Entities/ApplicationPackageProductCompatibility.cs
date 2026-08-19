namespace NCMS.IoT.DeviceManagement.Entities;

/// <summary>
/// Expresses that a software package version is compatible with a specific Product model.
/// Presence-only, mirrors <see cref="FirmwareProduct"/> exactly — no version ranges.
///
/// Invariants:
///   - Product.SupportsSoftwarePackages must be true
///   - Cannot be removed while an active ApplicationTask targets devices of this product
///     with this package version
/// </summary>
public sealed class ApplicationPackageProductCompatibility
{
    public Guid ApplicationPackageVersionId { get; set; }
    public Guid ProductId { get; set; }

    // Navigation
    public ApplicationPackageVersion ApplicationPackageVersion { get; set; } = default!;
    public Product Product { get; set; } = default!;
}
