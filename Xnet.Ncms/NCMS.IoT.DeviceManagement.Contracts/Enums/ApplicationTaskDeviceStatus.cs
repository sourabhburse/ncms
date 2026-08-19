namespace NCMS.IoT.DeviceManagement.Contracts.Enums;

// Mirrors UpgradeDeviceStatus / DeviceConfigStatus.
public enum ApplicationTaskDeviceStatus
{
    Pending = 0,     // Queued — dispatcher hasn't sent the install command yet
    InProgress = 1,  // Dispatched and not yet terminal
    Succeeded = 2,   // Install succeeded
    Failed = 3,      // Install failed after exhausting its retry budget
    TimedOut = 4,    // ApplicationTask.Timeout elapsed without reaching a terminal state
    Skipped = 5      // Device was offline at dispatch time or the task was cancelled while Pending
}
