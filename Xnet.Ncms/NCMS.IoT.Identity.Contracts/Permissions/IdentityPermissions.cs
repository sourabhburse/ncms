namespace NCMS.IoT.Identity.Contracts.Permissions;

/// <summary>
/// Simplified, UI-facing permission set (List/View/Add/Edit/Delete/...). Role permission
/// management (view/save a role's granted permissions) is folded into Roles.View/Roles.Edit
/// rather than a separate "RoleClaims" group — a role's permissions are part of the role
/// itself, and a separate, easy-to-forget-to-grant permission for viewing/editing them was
/// the reason the Permissions tab could silently disappear even for an admin who could
/// otherwise fully manage roles.
/// </summary>
public static class IdentityPermissions
{
    public static class Users
    {
        public const string List = "Permissions.Identity.Users.List";
        public const string View = "Permissions.Identity.Users.View";
        public const string Add = "Permissions.Identity.Users.Add";
        public const string Edit = "Permissions.Identity.Users.Edit";
        public const string Delete = "Permissions.Identity.Users.Delete";
    }

    public static class Roles
    {
        public const string List = "Permissions.Identity.Roles.List";
        public const string View = "Permissions.Identity.Roles.View";
        public const string Add = "Permissions.Identity.Roles.Add";
        public const string Edit = "Permissions.Identity.Roles.Edit";
        public const string Delete = "Permissions.Identity.Roles.Delete";
    }

    public static class AuditLogs
    {
        public const string List = "Permissions.Identity.AuditLogs.List";
    }
}
