using System.Reflection;

namespace NCMS.IoT.Identity.Contracts.Permissions;

/// <summary>
/// Reflects over a permissions-constants type (e.g. <c>DeviceManagementPermissions</c>) and its
/// nested classes to collect every <c>public const string</c> value, so a host can register a
/// whole module's permissions with the Identity module without listing them by hand.
/// </summary>
public static class PermissionDiscovery
{
    public static IReadOnlyList<string> GetAllPermissions(Type permissionsRootType)
    {
        var values = new List<string>();
        CollectFrom(permissionsRootType, values);
        return values;
    }

    private static void CollectFrom(Type type, List<string> values)
    {
        values.AddRange(type
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!));

        foreach (var nested in type.GetNestedTypes(BindingFlags.Public))
            CollectFrom(nested, values);
    }
}
