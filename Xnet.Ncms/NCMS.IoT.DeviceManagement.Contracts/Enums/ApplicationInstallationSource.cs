namespace NCMS.IoT.DeviceManagement.Contracts.Enums;

// How an ApplicationInstallationHistory row came to exist.
public enum ApplicationInstallationSource
{
    Deployment = 0,     // Produced by an ApplicationTaskDevice transition (ApplicationTaskDeviceId is set)
    DriftDetected = 1   // Reported by a device's periodic inventory report with no originating task
}
