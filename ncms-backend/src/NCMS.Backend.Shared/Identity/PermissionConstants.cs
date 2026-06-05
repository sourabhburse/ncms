using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NCMS.Backend.Shared.Constants
{
    public static class PermissionConstants
    {
        private static readonly List<NcmsPermission> _all = [];

    public const string RequiredPermissionPolicyName = "RequiredPermission";

        public static void Register(IEnumerable<NcmsPermission> additionalPermissions)
        {
            ArgumentNullException.ThrowIfNull(additionalPermissions);
            _all.AddRange(from permission in additionalPermissions
                        where !_all.Any(p => p.Name == permission.Name)
                        select permission);
        }

        public static IReadOnlyList<NcmsPermission> All => _all.AsReadOnly();
        public static IReadOnlyList<NcmsPermission> Root => [.. _all.Where(p => p.IsRoot)];
        public static IReadOnlyList<NcmsPermission> Admin => [.. _all.Where(p => !p.IsRoot)];
        public static IReadOnlyList<NcmsPermission> Basic => [.. _all.Where(p => p.IsBasic)];
    }

    public record NcmsPermission(string Description, string Action, string Resource, bool IsBasic = false, bool IsRoot = false)
    {
       public string Name => NameFor(Action, Resource); 
       public static string NameFor(string action, string resource) => $"Permissions.{action}.{resource}";
    }
}