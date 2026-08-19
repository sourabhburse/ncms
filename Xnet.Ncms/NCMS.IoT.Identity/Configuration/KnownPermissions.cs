using NCMS.IoT.Identity.Data.Seeding;

namespace NCMS.IoT.Identity.Configuration;

/// <summary>
/// The full set of permission strings the running application knows about — Identity's own
/// (Users/Roles/RoleClaims) plus any other module's permission constants, passed in by the
/// host when constructing <see cref="IdentityModule"/> so Identity never takes a project
/// reference on another module just to seed/display its permissions.
/// </summary>
public sealed class KnownPermissions
{
    public IReadOnlyList<string> All { get; }

    public KnownPermissions(IEnumerable<string>? additionalPermissions = null)
    {
        All = RoleAndAdminSeeder.DefaultPermissions()
            .Concat(additionalPermissions ?? [])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();
    }
}
