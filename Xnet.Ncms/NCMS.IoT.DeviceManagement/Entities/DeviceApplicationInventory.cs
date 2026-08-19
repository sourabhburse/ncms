using NCMS.IoT.DeviceManagement.Contracts.Enums;

namespace NCMS.IoT.DeviceManagement.Entities;

/// <summary>
/// Current software state of one (Device, ApplicationPackage) pair — one row, upserted.
///
/// Single-writer rule: only the MQTT status-reconciliation handler writes this table, in
/// the same transaction as the originating <see cref="ApplicationTaskDevice"/> transition
/// and the matching <see cref="ApplicationInstallationHistory"/> append. No other code path
/// touches it — this keeps current-state and the audit ledger from drifting apart.
/// </summary>
public sealed class DeviceApplicationInventory
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public Guid ApplicationPackageId { get; set; }
    public Guid InstalledVersionId { get; set; }
    public DeviceApplicationInventoryStatus Status { get; set; } = DeviceApplicationInventoryStatus.Installed;
    public DateTimeOffset InstalledAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public Device Device { get; set; } = default!;
    public ApplicationPackage ApplicationPackage { get; set; } = default!;
    public ApplicationPackageVersion InstalledVersion { get; set; } = default!;
}
