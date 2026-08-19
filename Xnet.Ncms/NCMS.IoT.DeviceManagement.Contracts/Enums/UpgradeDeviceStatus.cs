namespace NCMS.IoT.DeviceManagement.Contracts.Enums;

public enum UpgradeDeviceStatus
{
    Pending = 0,     // Queued — task created but dispatcher hasn't sent the command yet
    InProgress = 1,  // Upgrade command sent; device is downloading / applying
    Succeeded = 2,   // Device confirmed successful upgrade via MQTT response
    Failed = 3,      // Device reported failure; FailureReason is populated
    TimedOut = 4,    // UpgradeTask.UpgradeTimeout elapsed without a terminal response
    Skipped = 5      // Device was offline at dispatch time or task was cancelled while Pending
}
