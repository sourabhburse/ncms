using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.Host.Application.Abstractions;
using NCMS.IoT.Identity.Contracts.Permissions;

namespace NCMS.IoT.Host.Application.Navigation;

public sealed class NavigationMenuProvider : INavigationMenuProvider
{
    private static readonly IReadOnlyList<NavigationMenuItem> Items =
    [
        new("Dashboard", "/Index", "layout-dashboard"),

        NavigationMenuItem.Group("Device Management", "device-desktop",
            new("Devices",            "/Devices/Index",   "device-desktop", "/devices",   RequiredPermission: DeviceManagementPermissions.Devices.List),
            new("Hardware Inventory", "/Inventory/Index", "cpu",            "/inventory", RequiredPermission: DeviceManagementPermissions.Inventory.List)),

        NavigationMenuItem.Group("Maintenance", "tool",
            new("Firmware Packages",      "/Firmware/Packages/Index",         "box",           "/firmware/packages",       RequiredPermission: DeviceManagementPermissions.Packages.List),
            new("Application Packages",   "/AppPackages/Packages/Index",      "package",       "/apppackages/packages",    RequiredPermission: DeviceManagementPermissions.ApplicationPackages.List),
            new("Configuration Profiles", "/Configuration/Profiles/Index",    "file-settings", "/configuration/profiles",  RequiredPermission: DeviceManagementPermissions.ConfigProfiles.List),
            new("Firmware Tasks",         "/Firmware/UpgradeTasks/Index",     "refresh",       "/firmware/upgradetasks",   RequiredPermission: DeviceManagementPermissions.UpgradeTasks.List),
            new("Application Tasks",      "/AppPackages/Tasks/Index",         "cloud-upload",  "/apppackages/tasks",       RequiredPermission: DeviceManagementPermissions.ApplicationTasks.List),
            new("Configuration Tasks",    "/Configuration/ConfigTasks/Index", "settings",      "/configuration/configtasks", RequiredPermission: DeviceManagementPermissions.ConfigTasks.List)),

        NavigationMenuItem.Group("System Management", "settings-cog",
            new("User Manage",  "/SystemManagement/Users/Index",    "users",       "/systemmanagement/users",    RequiredPermission: IdentityPermissions.Users.List),
            new("Role Manage",  "/SystemManagement/Roles/Index",    "shield-lock", "/systemmanagement/roles",    RequiredPermission: IdentityPermissions.Roles.List),
            new("Audit Log",    "/SystemManagement/AuditLog/Index", "history",     "/systemmanagement/auditlog", RequiredPermission: IdentityPermissions.AuditLogs.List))
    ];

    public IReadOnlyList<NavigationMenuItem> GetItems() => Items;
}
