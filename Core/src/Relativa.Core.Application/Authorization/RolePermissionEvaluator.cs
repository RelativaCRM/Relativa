using Relativa.Persistence.Entities;

namespace Relativa.Core.Application.Authorization;

public static class RolePermissionEvaluator
{
    public static bool HasPermission(WorkspaceRole? role, string permissionName)
        => role?.RolePermissions.Any(rp => string.Equals(rp.Permission?.Name, permissionName, StringComparison.Ordinal)) == true;

    public static bool HasPermission(OrganizationRole? role, string permissionName)
        => role?.RolePermissions.Any(rp => string.Equals(rp.Permission?.Name, permissionName, StringComparison.Ordinal)) == true;

    public static bool HasAllPermissions(WorkspaceRole? role, IReadOnlyCollection<string> requiredPermissions)
    {
        if (role?.RolePermissions is null || requiredPermissions.Count == 0)
            return false;

        var rolePermissions = role.RolePermissions
            .Select(rp => rp.Permission?.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);

        return requiredPermissions.All(rolePermissions.Contains);
    }

    public static bool HasAllPermissions(OrganizationRole? role, IReadOnlyCollection<string> requiredPermissions)
    {
        if (role?.RolePermissions is null || requiredPermissions.Count == 0)
            return false;

        var rolePermissions = role.RolePermissions
            .Select(rp => rp.Permission?.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);

        return requiredPermissions.All(rolePermissions.Contains);
    }
}
