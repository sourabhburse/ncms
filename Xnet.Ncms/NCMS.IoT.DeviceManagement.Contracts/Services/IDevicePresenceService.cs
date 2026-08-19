namespace NCMS.IoT.DeviceManagement.Contracts.Services;

public interface IDevicePresenceService
{
    Task<int> UpdatePresenceAsync(Guid deviceId, bool isOnline, CancellationToken ct = default);
    Task<int> UpdateLastSeenAsync(Guid deviceId, CancellationToken ct = default);

    /// <summary>
    /// Marks every currently-online device offline whose last heartbeat predates
    /// <paramref name="cutoff"/> (or that has never sent one). Returns the number of devices
    /// transitioned. Used by the presence reaper to detect devices that stopped heart-beating —
    /// absence of a heartbeat produces no message, so it can only be detected by this time-based sweep.
    /// </summary>
    Task<int> MarkStaleOfflineAsync(DateTimeOffset cutoff, CancellationToken ct = default);
}
