using System.Security.Claims;
using NCMS.IoT.Identity.Entities;

namespace NCMS.IoT.Identity.Services;

/// <summary>
/// Builds the claim set for an authenticated <see cref="AppUser"/> — identity claims, one
/// <see cref="ClaimTypes.Role"/> per assigned role, and every "Permission"-typed role claim
/// granted through those roles. Shared by <see cref="TokenService"/> (JWT, for the API) and
/// the Host's cookie sign-in, so both authentication schemes carry the exact same
/// authorization claims and <c>ICurrentUser</c>/permission-policy checks behave identically.
/// </summary>
public interface IUserClaimsBuilder
{
    Task<List<Claim>> BuildAsync(AppUser user, CancellationToken ct = default);
}
