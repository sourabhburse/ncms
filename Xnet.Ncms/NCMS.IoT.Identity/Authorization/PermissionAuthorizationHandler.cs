using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using NCMS.IoT.Identity.Services;

namespace NCMS.IoT.Identity.Authorization;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IRolePermissionCache _rolePermissions;

    public PermissionAuthorizationHandler(IRolePermissionCache rolePermissions) => _rolePermissions = rolePermissions;

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        foreach (var roleName in context.User.FindAll(ClaimTypes.Role).Select(c => c.Value).Distinct())
        {
            var permissions = await _rolePermissions.GetPermissionsAsync(roleName);
            if (permissions.Contains(requirement.Permission))
            {
                context.Succeed(requirement);
                return;
            }
        }
    }
}
