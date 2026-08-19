namespace NCMS.IoT.DeviceManagement.Contracts.Enums;

/// <summary>
/// Per-device progress state within a configure task. Mirrors UpgradeDeviceStatus,
/// but configuration has a simpler lifecycle (no separate Failed/Skipped — all
/// non-success terminal outcomes collapse to TerminationConfig).
///
/// Values are deliberately spaced to allow future intermediate states:
///   0      → Not yet reached
///   10     → Actively being configured
///   20     → Successfully configured
///   22     → Config ended by termination (timeout / failure / abort)
/// </summary>
public enum DeviceConfigStatus
{
    NotStarted = 0,        // Queued — task created but dispatcher hasn't sent the command yet
    ConfigInProgress = 10, // Config command sent; device is applying configuration
    ConfigComplete = 20,   // Device confirmed successful configuration via MQTT response
    TerminationConfig = 22 // Ended by timeout, device-reported failure, or operator abort
}
