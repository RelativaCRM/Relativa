namespace Relativa.Core.Application.Interfaces;

/// <summary>
/// Workspace RBAC including organization super-role bypass for all workspaces in their org(s).
/// </summary>
public interface IWorkspaceAccessEvaluator
{
    /// <summary>True when the user's organization role has full permission coverage for the workspace's organization.</summary>
    Task<bool> IsOrgOwnerOfWorkspaceAsync(int userId, int workspaceId, CancellationToken ct = default);

    /// <summary>Member with role permission OR org owner of workspace's organization.</summary>
    Task<bool> HasWorkspacePermissionAsync(int userId, int workspaceId, string permissionName, CancellationToken ct = default);

    /// <summary>Throws if neither a workspace member nor org owner with access to this workspace.</summary>
    Task EnsureCanAccessWorkspaceAsync(int userId, int workspaceId, CancellationToken ct = default);

    /// <summary>
    /// Permission names for the UI: role permissions, or full workspace-superset when organization super-role applies.
    /// </summary>
    Task<IReadOnlyList<string>> GetEffectiveWorkspacePermissionNamesAsync(int userId, int workspaceId, CancellationToken ct = default);
}
