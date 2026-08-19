namespace NCMS.IoT.DeviceManagement.Contracts.Enums;

public enum UpgradeTaskStatus
{
    NotStarted = 0,  // Created; dispatcher has not yet started sending commands
    InProgress = 1,  // At least one UpgradeTaskDevice is in flight
    Completed = 2,   // All UpgradeTaskDevices reached a terminal state (Succeeded/Failed/TimedOut/Skipped)
    Cancelled = 3    // Manually cancelled by an operator before or during execution
}
