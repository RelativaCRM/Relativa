namespace Relativa.Core.Application.Interfaces;

/// <summary>
/// Workspace RBAC including organization owner bypass for all workspaces in their org(s).
/// </summary>
public interface IWorkspaceAccessEvaluator
{
    /// <summary>True when the user is <c>org_owner</c> of the organization that owns the workspace.</summary>
    Task<bool> IsOrgOwnerOfWorkspaceAsync(int userId, int workspaceId, CancellationToken ct = default);

    /// <summary>Member with role permission OR org owner of workspace's organization.</summary>
    Task<bool> HasWorkspacePermissionAsync(int userId, int workspaceId, string permissionName, CancellationToken ct = default);

    /// <summary>Throws if neither a workspace member nor org owner with access to this workspace.</summary>
    Task EnsureCanAccessWorkspaceAsync(int userId, int workspaceId, CancellationToken ct = default);

    /// <summary>
    /// Permission names for the UI: role permissions, or full workspace-superset when org_owner (via <c>ws_admin</c> system role).
    /// </summary>
    Task<IReadOnlyList<string>> GetEffectiveWorkspacePermissionNamesAsync(int userId, int workspaceId, CancellationToken ct = default);
}
