namespace NCMS.IoT.Identity.Contracts.Enums;

public enum AuditEventType
{
    LoginSucceeded,
    LoginFailed,
    Logout,
    PasswordChanged,
    PasswordReset,
    UserCreated,
    UserUpdated,
    UserDeleted,
    RoleCreated,
    RoleUpdated,
    RoleDeleted,
    RolePermissionsChanged
}
