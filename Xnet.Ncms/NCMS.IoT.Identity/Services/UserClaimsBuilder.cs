using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using NCMS.IoT.Identity.Entities;

namespace NCMS.IoT.Identity.Services;

public sealed class UserClaimsBuilder : IUserClaimsBuilder
{
    private readonly UserManager<AppUser> _userManager;

    public UserClaimsBuilder(UserManager<AppUser> userManager) => _userManager = userManager;

    /// <summary>
    /// Builds identity + role claims only — deliberately NOT permission claims. A user in
    /// the Admin role can carry 30-50+ permissions; embedding them all in the cookie/JWT
    /// produced a Set-Cookie large enough to need chunking (multiple ~4KB cookie parts),
    /// which some reverse proxies (including a default nginx proxy_buffer_size) can't fit
    /// into their response-header buffer — causing a 502 Bad Gateway right after login.
    /// Permission checks instead resolve a role's permissions on demand via
    /// <see cref="IRolePermissionCache"/>, keeping the principal small.
    /// </summary>
    public async Task<List<Claim>> BuildAsync(AppUser user, CancellationToken ct = default)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? user.Email ?? user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? string.Empty)
        };

        var roles = await _userManager.GetRolesAsync(user);
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        return claims;
    }
}
