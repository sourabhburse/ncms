using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Services;
using NCMS.IoT.DeviceManagement.Data;

namespace NCMS.IoT.DeviceManagement.Services;

internal sealed class DevicePresenceService : IDevicePresenceService
{
    private readonly DeviceManagementDbContext _db;

    public DevicePresenceService(DeviceManagementDbContext db) => _db = db;

    public Task<int> UpdatePresenceAsync(Guid deviceId, bool isOnline, CancellationToken ct = default)
        => _db.Devices
            .Where(d => d.Id == deviceId)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(d => d.IsOnline, isOnline)
                    .SetProperty(d => d.LastSeenAt, DateTimeOffset.UtcNow),
                ct);

    public Task<int> UpdateLastSeenAsync(Guid deviceId, CancellationToken ct = default)
        => _db.Devices
            .Where(d => d.Id == deviceId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(d => d.LastSeenAt, DateTimeOffset.UtcNow),
                ct);

    public Task<int> MarkStaleOfflineAsync(DateTimeOffset cutoff, CancellationToken ct = default)
        => _db.Devices
            // null LastSeenAt = provisioned but never heard from → treat as stale (offline).
            .Where(d => d.IsOnline && (d.LastSeenAt == null || d.LastSeenAt < cutoff))
            .ExecuteUpdateAsync(
                s => s.SetProperty(d => d.IsOnline, false),
                ct);
}
