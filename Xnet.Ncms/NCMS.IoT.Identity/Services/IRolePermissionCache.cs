namespace NCMS.IoT.Identity.Services;

/// <summary>
/// Resolves a role's granted "Permission" claims without embedding them in the auth
/// cookie/JWT. Permission checks look this up by role name (cheap after the first hit —
/// results are cached) instead of scanning claims baked into the principal at sign-in time.
/// </summary>
public interface IRolePermissionCache
{
    Task<IReadOnlySet<string>> GetPermissionsAsync(string roleName, CancellationToken ct = default);

    /// <summary>Drop the cached entry for a role — call after its permissions change so the new set applies immediately.</summary>
    void Invalidate(string roleName);
}
