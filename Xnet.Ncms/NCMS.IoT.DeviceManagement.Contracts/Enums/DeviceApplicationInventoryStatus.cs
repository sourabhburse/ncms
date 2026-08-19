namespace NCMS.IoT.DeviceManagement.Contracts.Enums;

// Current-state status of one (Device, ApplicationPackage) pair.
public enum DeviceApplicationInventoryStatus
{
    Installed = 0,
    Upgrading = 1,
    Removing = 2,
    Failed = 3,
    NotInstalled = 4
}
