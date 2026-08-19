namespace NCMS.IoT.DeviceManagement.Entities;

/// <summary>
/// Informational-only dependency declaration. Validated for existence only at publish time
/// (the referenced <see cref="ApplicationPackage"/> must exist in the catalog); never resolved,
/// expanded, or auto-installed at deployment time. Automatic dependency resolution is
/// explicitly out of scope — see the architecture decision to defer it until a real
/// multi-package interdependency case justifies the added complexity.
/// </summary>
public sealed class ApplicationPackageDependency
{
    public Guid Id { get; set; }
    public Guid ApplicationPackageVersionId { get; set; }
    public Guid DependsOnApplicationPackageId { get; set; }

    /// <summary>Free-text, shown in UI, never parsed or enforced (e.g. "&gt;= 2.0").</summary>
    public string? VersionConstraint { get; set; }

    // Navigation
    public ApplicationPackageVersion ApplicationPackageVersion { get; set; } = default!;
    public ApplicationPackage DependsOnApplicationPackage { get; set; } = default!;
}
