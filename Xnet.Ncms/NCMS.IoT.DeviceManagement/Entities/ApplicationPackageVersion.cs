namespace NCMS.IoT.DeviceManagement.Entities;

/// <summary>
/// One uploaded, installable artifact — the aggregate root of the software-package domain.
/// Carries lifecycle (IsEnabled), compatibility, and declared (informational-only,
/// unresolved) dependencies. Mirrors <see cref="Firmware"/>'s role and its
/// <see cref="Firmware.IsEnabled"/> lifecycle exactly — split from
/// <see cref="ApplicationPackage"/> purely to support multi-version browsing.
///
/// <see cref="PackageFormat"/> and <see cref="Metadata"/> are opaque to the domain model —
/// the device agent interprets them, the server only routes bytes. This is what lets
/// different device families use completely different underlying package managers without
/// any core schema or workflow change.
///
/// Lifecycle: single reversible <see cref="IsEnabled"/> flag, exactly like
/// <see cref="Firmware.IsEnabled"/>. Enabled versions are visible in bundle/task package
/// selectors; disabled versions are hidden (but not deleted) and become editable again.
///
/// Compatibility is Product-only — deliberately mirrors <see cref="Firmware"/>, which also
/// has no second, firmware-level compatibility layer beneath it. A firmware-version-based
/// compatibility axis was considered and rejected: a device's self-reported firmware version
/// (<c>Device.FirmwareVersion</c>) is free text, not a reference into the managed Firmware
/// Package catalog, so gating deployment on "a matching Firmware Package exists" would
/// incorrectly block devices running real, legitimate firmware NCMS simply never cataloged
/// (e.g. factory-installed builds) — the absence of a catalog entry says nothing about actual
/// compatibility.
///
/// Invariants (enforced at the application layer):
///   - Compatibility/dependencies/binary may only be edited while IsEnabled = false
///     (mirrors Firmware's "disable the package before editing it" rule)
///   - Enabling requires Sha256Checksum computed (a binary has been uploaded) and at least
///     one ApplicationPackageProductCompatibility row
///   - ApplicationTask creation may only target enabled versions
/// </summary>
public sealed class ApplicationPackageVersion
{
    public Guid Id { get; set; }
    public Guid ApplicationPackageId { get; set; }
    public string Version { get; set; } = string.Empty;

    /// <summary>Opaque format code interpreted only by the device agent (e.g. "ipk", "deb", "zip").</summary>
    public string PackageFormat { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Sha256Checksum { get; set; } = string.Empty;
    public string? Md5Checksum { get; set; }

    /// <summary>Opaque, format-specific metadata bag (jsonb). Never inspected by domain logic.</summary>
    public string? Metadata { get; set; }

    public string? ReleaseNotes { get; set; }

    /// <summary>
    /// Whether this version may be used when creating software tasks. Disabled versions are
    /// hidden from selectors but remain editable — mirrors <see cref="Firmware.IsEnabled"/>.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
    public string UploadedBy { get; set; } = string.Empty;

    // Navigation
    public ApplicationPackage ApplicationPackage { get; set; } = default!;
    public ICollection<ApplicationPackageDependency> Dependencies { get; set; } = new List<ApplicationPackageDependency>();
    public ICollection<ApplicationPackageProductCompatibility> SupportedProducts { get; set; } = new List<ApplicationPackageProductCompatibility>();
}
