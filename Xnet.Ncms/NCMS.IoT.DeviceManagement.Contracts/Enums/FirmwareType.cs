namespace NCMS.IoT.DeviceManagement.Contracts.Enums;

public enum FirmwareType
{
    Full = 0,        // Complete firmware image — replaces everything on the device
    Patch = 1,       // Incremental delta — applied on top of a known base version
    Bootloader = 2,  // Bootloader only — replaces the boot stage, not the OS/application
    Application = 3  // Application layer only — OS/drivers untouched
}
