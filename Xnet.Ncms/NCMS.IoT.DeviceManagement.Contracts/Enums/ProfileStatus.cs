namespace NCMS.IoT.DeviceManagement.Contracts.Enums;

/// <summary>
/// Whether a config profile is active and usable for new config tasks.
/// Mirrors the firmware package's enabled/disabled flag.
/// </summary>
public enum ProfileStatus
{
    Disable = 0,
    Enable = 1
}
