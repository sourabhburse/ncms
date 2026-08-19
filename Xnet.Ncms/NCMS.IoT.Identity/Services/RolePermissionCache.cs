using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using NCMS.IoT.Identity.Data.Seeding;
using NCMS.IoT.Identity.Entities;

namespace NCMS.IoT.Identity.Services;

public sealed class RolePermissionCache : IRolePermissionCache
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private const string KeyPrefix = "role-permissions:";

    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;

    public RolePermissionCache(IMemoryCache cache, IServiceScopeFactory scopeFactory)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
    }

    public Task<IReadOnlySet<string>> GetPermissionsAsync(string roleName, CancellationToken ct = default) =>
        _cache.GetOrCreateAsync<IReadOnlySet<string>>(KeyPrefix + roleName, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;

            using var scope = _scopeFactory.CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();

            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null)
                return new HashSet<string>();

            var claims = await roleManager.GetClaimsAsync(role);
            return claims
                .Where(c => c.Type == RoleAndAdminSeeder.PermissionClaimType)
                .Select(c => c.Value)
                .ToHashSet();
        })!;

    public void Invalidate(string roleName) => _cache.Remove(KeyPrefix + roleName);
}
