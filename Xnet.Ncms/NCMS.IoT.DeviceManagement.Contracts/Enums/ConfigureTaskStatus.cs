namespace NCMS.IoT.DeviceManagement.Contracts.Enums;

/// <summary>
/// Lifecycle state of a configure task as a whole. Values and names are kept identical
/// to <see cref="UpgradeTaskStatus"/> so both modules present the same status everywhere.
/// </summary>
public enum ConfigureTaskStatus
{
    NotStarted = 0,  // Created; dispatcher has not yet started sending config commands
    InProgress = 1,  // At least one device is being configured
    Completed = 2,   // All devices reached a terminal state (configured or terminated)
    Cancelled = 3    // Manually cancelled by an operator before or during execution
}
