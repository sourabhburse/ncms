using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using NCMS.IoT.Identity.Contracts.Permissions;
using NCMS.IoT.Identity.Entities;

namespace NCMS.IoT.Identity.Data.Seeding;

/// <summary>
/// Seeds the built-in Admin/Basic roles, grants the Admin role every known permission
/// (as "Permission" role claims), and creates a default admin user on first run.
/// </summary>
public static class RoleAndAdminSeeder
{
    public const string AdminRoleName = "Admin";
    public const string BasicRoleName = "Basic";
    public const string PermissionClaimType = "Permission";

    public static async Task SeedAsync(
        RoleManager<AppRole> roleManager,
        UserManager<AppUser> userManager,
        IEnumerable<string> allPermissions,
        ILogger logger,
        CancellationToken ct = default)
    {
        var adminRole = await roleManager.FindByNameAsync(AdminRoleName);
        if (adminRole is null)
        {
            adminRole = new AppRole(AdminRoleName, "Full access to all features.");
            await roleManager.CreateAsync(adminRole);
            logger.LogInformation("Identity: seeded role '{Role}'.", AdminRoleName);
        }

        if (await roleManager.FindByNameAsync(BasicRoleName) is null)
        {
            await roleManager.CreateAsync(new AppRole(BasicRoleName, "Read-only baseline access."));
            logger.LogInformation("Identity: seeded role '{Role}'.", BasicRoleName);
        }

        var existingClaims = (await roleManager.GetClaimsAsync(adminRole))
            .Where(c => c.Type == PermissionClaimType)
            .Select(c => c.Value)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var permission in allPermissions)
        {
            if (existingClaims.Contains(permission))
                continue;

            await roleManager.AddClaimAsync(adminRole, new System.Security.Claims.Claim(PermissionClaimType, permission));
        }

        const string adminEmail = "admin@ncms.local";
        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new AppUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                IsActive = true,
                FirstName = "System",
                LastName = "Administrator"
            };

            var result = await userManager.CreateAsync(admin, "Admin@12345");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, AdminRoleName);
                logger.LogWarning(
                    "Identity: seeded default admin user '{Email}' with a well-known password — change it immediately in non-development environments.",
                    adminEmail);
            }
            else
            {
                logger.LogError(
                    "Identity: failed to seed default admin user: {Errors}",
                    string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    /// <summary>
    /// Identity's own permission constants, discovered by reflection so a new nested class/const
    /// added to <see cref="IdentityPermissions"/> is picked up automatically — no hardcoded list
    /// to keep in sync (this previously drifted: ResetPassword/AuditLogs.View were added to
    /// IdentityPermissions but never seeded because this list wasn't updated to match).
    /// </summary>
    public static IEnumerable<string> DefaultPermissions() =>
        PermissionDiscovery.GetAllPermissions(typeof(IdentityPermissions));
}
