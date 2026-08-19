namespace NCMS.IoT.DeviceManagement.Contracts.Enums;

// Mirrors UpgradeTaskStatus / ConfigureTaskStatus.
public enum ApplicationTaskStatus
{
    NotStarted = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3
}
