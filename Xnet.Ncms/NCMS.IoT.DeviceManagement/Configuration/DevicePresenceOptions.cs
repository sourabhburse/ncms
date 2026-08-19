namespace NCMS.IoT.DeviceManagement.Configuration;

/// <summary>
/// Bound from configuration section "DevicePresence". Controls the background sweep that marks
/// devices offline once their heartbeats stop arriving.
/// </summary>
public sealed class DevicePresenceOptions
{
    public const string SectionName = "DevicePresence";

    /// <summary>How often the presence reaper runs the staleness sweep (seconds). Min 5.</summary>
    public int SweepIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// A device is marked offline once it has missed this many consecutive heartbeats — i.e. when
    /// its LastSeenAt is older than (heartbeat interval × this multiplier). The heartbeat interval
    /// is taken from <c>Ztp:StatusIntervalSeconds</c> (the value devices are actually told to use),
    /// so the threshold tracks device behaviour automatically. Use ≥ 2 to tolerate jitter or a
    /// single dropped heartbeat without flapping a healthy device offline.
    /// </summary>
    public int OfflineAfterMissedHeartbeats { get; set; } = 3;
}
