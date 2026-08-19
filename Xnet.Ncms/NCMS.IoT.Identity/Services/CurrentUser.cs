using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using NCMS.IoT.Identity.Contracts.Services;

namespace NCMS.IoT.Identity.Services;

public sealed class CurrentUser : ICurrentUser
{
    private readonly ClaimsPrincipal? _principal;
    private readonly IRolePermissionCache _rolePermissions;

    public CurrentUser(IHttpContextAccessor httpContextAccessor, IRolePermissionCache rolePermissions)
    {
        _principal = httpContextAccessor.HttpContext?.User;
        _rolePermissions = rolePermissions;
    }

    public bool IsAuthenticated => _principal?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId =>
        Guid.TryParse(_principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string? UserName => _principal?.FindFirstValue(ClaimTypes.Name);

    public string? Email => _principal?.FindFirstValue(ClaimTypes.Email);

    public IReadOnlyCollection<string> Roles =>
        _principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? [];

    /// <summary>
    /// Resolves the permission via the roles carried in the principal, not a "Permission"
    /// claim — the cookie/JWT intentionally only carries role names (see
    /// <see cref="UserClaimsBuilder"/>), so this looks each role's permissions up through
    /// <see cref="IRolePermissionCache"/> (cached, so this stays cheap).
    /// </summary>
    public bool HasPermission(string permission) =>
        Roles.Any(role => _rolePermissions.GetPermissionsAsync(role).GetAwaiter().GetResult().Contains(permission));
}
