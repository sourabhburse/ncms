using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Enums;

namespace NCMS.IoT.DeviceManagement.Data;

/// <summary>
/// Cross-domain "is this device already busy" check. UpgradeTaskDevice, ConfigureTaskDevice,
/// and ApplicationTaskDevice each enforce "one active task per device" only within their own
/// table (partial unique index) — nothing stops a firmware upgrade and an application
/// deployment from being dispatched to the same device concurrently, which is a real risk
/// (installing a package mid-flash). This helper is the guard: called from ApplicationTask
/// creation and ApplicationDispatcherService before dispatch, checking the *other two*
/// domains' active rows.
///
/// This is a guard clause, not a new architectural pattern — it's reused at each of those
/// call sites rather than backed by a shared "device lock" entity.
///
/// Known gap: the reverse checks (Firmware/Config creation and dispatch guarding against an
/// active ApplicationTaskDevice) are not yet wired into FirmwareDispatcherService/
/// CreateUpgradeTask or ConfigDispatcherService/CreateConfigureTask — only the application side
/// currently defers to firmware/config. Closing the loop requires touching those existing,
/// already-shipped flows and should be done as a deliberate follow-up.
/// </summary>
internal static class DeviceBusyGuard
{
    public static async Task<HashSet<Guid>> GetDevicesBusyElsewhereAsync(
        DeviceManagementDbContext db, IReadOnlyCollection<Guid> deviceIds, CancellationToken ct)
    {
        var busyInFirmware = await db.UpgradeTaskDevices
            .Where(d => deviceIds.Contains(d.DeviceId)
                && (d.Status == UpgradeDeviceStatus.Pending || d.Status == UpgradeDeviceStatus.InProgress))
            .Select(d => d.DeviceId)
            .ToListAsync(ct);

        var busyInConfig = await db.ConfigureTaskDevices
            .Where(d => deviceIds.Contains(d.DeviceId)
                && (d.Status == DeviceConfigStatus.NotStarted || d.Status == DeviceConfigStatus.ConfigInProgress))
            .Select(d => d.DeviceId)
            .ToListAsync(ct);

        return new HashSet<Guid>(busyInFirmware.Concat(busyInConfig));
    }
}
