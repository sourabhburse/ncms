using Microsoft.AspNetCore.Authorization;

namespace NCMS.IoT.Identity.Authorization;

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
