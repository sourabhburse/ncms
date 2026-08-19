using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Enums;
using NCMS.IoT.DeviceManagement.Entities;

namespace NCMS.IoT.DeviceManagement.Data;

/// <summary>
/// Applies a terminal <see cref="ApplicationTaskDevice"/> outcome to
/// <see cref="DeviceApplicationInventory"/> (current state) and appends an
/// <see cref="ApplicationInstallationHistory"/> row (audit ledger). This is the single writer
/// path for both tables — see the single-writer rule documented on
/// <see cref="DeviceApplicationInventory"/> — so it must only ever be called from the MQTT
/// status-reconciliation handler, in the same transaction as the device transition itself.
///
/// Removal semantics: on a successful Remove, the current-state row is deleted outright
/// (not soft-marked NotInstalled) — "not installed" is the absence of a row, matching
/// DeviceApplicationInventory's one-row-per-installed-package invariant. The audit trail of
/// the removal still lives in ApplicationInstallationHistory regardless.
/// </summary>
internal static class ApplicationInventorySync
{
    public static async Task ApplyDeviceOutcomeAsync(
        DeviceManagementDbContext db,
        ApplicationTaskDevice taskDevice,
        ApplicationTaskAction action,
        CancellationToken ct)
    {
        if (taskDevice.Status is not (ApplicationTaskDeviceStatus.Succeeded or ApplicationTaskDeviceStatus.Failed))
            return;

        var result = taskDevice.Status == ApplicationTaskDeviceStatus.Succeeded
            ? ApplicationInstallationResult.Succeeded
            : ApplicationInstallationResult.Failed;

        db.ApplicationInstallationHistory.Add(new ApplicationInstallationHistory
        {
            Id = Guid.NewGuid(),
            DeviceId = taskDevice.DeviceId,
            ApplicationPackageVersionId = taskDevice.ApplicationPackageVersionId,
            Action = action,
            Result = result,
            OccurredAt = DateTimeOffset.UtcNow,
            ApplicationTaskDeviceId = taskDevice.Id,
            Source = ApplicationInstallationSource.Deployment
        });

        if (result == ApplicationInstallationResult.Failed) return;

        var version = await db.ApplicationPackageVersions
            .FirstAsync(v => v.Id == taskDevice.ApplicationPackageVersionId, ct);

        if (action == ApplicationTaskAction.Remove)
        {
            var existing = await db.DeviceApplicationInventory.FirstOrDefaultAsync(
                inv => inv.DeviceId == taskDevice.DeviceId && inv.ApplicationPackageId == version.ApplicationPackageId, ct);
            if (existing is not null) db.DeviceApplicationInventory.Remove(existing);
            return;
        }

        // Install / Upgrade / Downgrade — upsert the current-state row.
        var inventory = await db.DeviceApplicationInventory.FirstOrDefaultAsync(
            inv => inv.DeviceId == taskDevice.DeviceId && inv.ApplicationPackageId == version.ApplicationPackageId, ct);

        var now = DateTimeOffset.UtcNow;
        if (inventory is null)
        {
            db.DeviceApplicationInventory.Add(new DeviceApplicationInventory
            {
                Id = Guid.NewGuid(),
                DeviceId = taskDevice.DeviceId,
                ApplicationPackageId = version.ApplicationPackageId,
                InstalledVersionId = version.Id,
                Status = DeviceApplicationInventoryStatus.Installed,
                InstalledAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            inventory.InstalledVersionId = version.Id;
            inventory.Status = DeviceApplicationInventoryStatus.Installed;
            inventory.UpdatedAt = now;
        }
    }
}
