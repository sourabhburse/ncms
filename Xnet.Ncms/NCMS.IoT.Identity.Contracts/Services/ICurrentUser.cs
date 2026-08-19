namespace NCMS.IoT.Identity.Contracts.Services;

/// <summary>
/// Read-only view of the caller's identity, resolved from the current HTTP request.
/// Lets other modules (e.g. DeviceManagement) know "who is calling" without depending
/// on the full Identity module or its DbContext.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    string? UserName { get; }
    string? Email { get; }
    IReadOnlyCollection<string> Roles { get; }

    bool HasPermission(string permission);
}
