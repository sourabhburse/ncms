namespace NCMS.IoT.DeviceManagement.Contracts.Permissions;

/// <summary>
/// Simplified, UI-facing permission set — every area uses only the actions that actually
/// appear as a distinct operation somewhere in the product: List, View, Add, Edit, Delete,
/// EnableDisable, Upload, Download, Copy, Abort, Terminate. No backend-shaped verbs
/// (Get/Create/Update/Manage/Validate/Publish/Deprecate/SetCompatibility/SearchDevices/...).
/// </summary>
public static class DeviceManagementPermissions
{
    public static class Devices
    {
        public const string List = "Permissions.DeviceManagement.Devices.List";
        public const string View = "Permissions.DeviceManagement.Devices.View";
    }

    public static class Inventory
    {
        public const string List = "Permissions.DeviceManagement.Inventory.List";
        public const string View = "Permissions.DeviceManagement.Inventory.View";
        public const string Add = "Permissions.DeviceManagement.Inventory.Add";
        public const string Edit = "Permissions.DeviceManagement.Inventory.Edit";
        public const string Delete = "Permissions.DeviceManagement.Inventory.Delete";
        public const string Upload = "Permissions.DeviceManagement.Inventory.Upload";
        public const string Download = "Permissions.DeviceManagement.Inventory.Download";
    }

    public static class Packages
    {
        public const string List = "Permissions.DeviceManagement.Packages.List";
        public const string View = "Permissions.DeviceManagement.Packages.View";
        public const string Add = "Permissions.DeviceManagement.Packages.Add";
        public const string Edit = "Permissions.DeviceManagement.Packages.Edit";
        public const string Delete = "Permissions.DeviceManagement.Packages.Delete";
        public const string EnableDisable = "Permissions.DeviceManagement.Packages.EnableDisable";
        public const string Upload = "Permissions.DeviceManagement.Packages.Upload";
    }

    public static class UpgradeTasks
    {
        public const string List = "Permissions.DeviceManagement.UpgradeTasks.List";
        public const string View = "Permissions.DeviceManagement.UpgradeTasks.View";
        public const string Add = "Permissions.DeviceManagement.UpgradeTasks.Add";
        public const string Abort = "Permissions.DeviceManagement.UpgradeTasks.Abort";
    }

    public static class ConfigProfiles
    {
        public const string List = "Permissions.DeviceManagement.ConfigProfiles.List";
        public const string View = "Permissions.DeviceManagement.ConfigProfiles.View";
        public const string Add = "Permissions.DeviceManagement.ConfigProfiles.Add";
        public const string Edit = "Permissions.DeviceManagement.ConfigProfiles.Edit";
        public const string Delete = "Permissions.DeviceManagement.ConfigProfiles.Delete";
        public const string EnableDisable = "Permissions.DeviceManagement.ConfigProfiles.EnableDisable";
        public const string Upload = "Permissions.DeviceManagement.ConfigProfiles.Upload";
        public const string Download = "Permissions.DeviceManagement.ConfigProfiles.Download";
        public const string Copy = "Permissions.DeviceManagement.ConfigProfiles.Copy";
    }

    public static class ConfigTasks
    {
        public const string List = "Permissions.DeviceManagement.ConfigTasks.List";
        public const string View = "Permissions.DeviceManagement.ConfigTasks.View";
        public const string Add = "Permissions.DeviceManagement.ConfigTasks.Add";
        public const string Abort = "Permissions.DeviceManagement.ConfigTasks.Abort";
        public const string Terminate = "Permissions.DeviceManagement.ConfigTasks.Terminate";
    }

    public static class ApplicationPackages
    {
        public const string List = "Permissions.DeviceManagement.ApplicationPackages.List";
        public const string View = "Permissions.DeviceManagement.ApplicationPackages.View";
        public const string Add = "Permissions.DeviceManagement.ApplicationPackages.Add";
        public const string Edit = "Permissions.DeviceManagement.ApplicationPackages.Edit";
        public const string Delete = "Permissions.DeviceManagement.ApplicationPackages.Delete";
        public const string EnableDisable = "Permissions.DeviceManagement.ApplicationPackages.EnableDisable";
        public const string Upload = "Permissions.DeviceManagement.ApplicationPackages.Upload";
    }

    public static class ApplicationTasks
    {
        public const string List = "Permissions.DeviceManagement.ApplicationTasks.List";
        public const string View = "Permissions.DeviceManagement.ApplicationTasks.View";
        public const string Add = "Permissions.DeviceManagement.ApplicationTasks.Add";
        public const string Abort = "Permissions.DeviceManagement.ApplicationTasks.Abort";
    }
}
