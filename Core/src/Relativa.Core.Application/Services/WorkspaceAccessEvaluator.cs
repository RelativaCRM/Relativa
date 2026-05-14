using Relativa.Core.Application.Interfaces;
using Relativa.Core.Application.Authorization;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Application.Services;

public sealed class WorkspaceAccessEvaluator(
    IUserRoleWorkspaceRepository memberRepository,
    IUserRoleOrganizationRepository orgMemberRepository,
    IWorkspaceRepository workspaceRepository,
    IWorkspaceRoleRepository? workspaceRoleRepository = null,
    IPermissionRepository? permissionRepository = null) : IWorkspaceAccessEvaluator
{
    public const string OrgOwnerRoleName = "org_owner";

    public async Task<bool> IsOrgOwnerOfWorkspaceAsync(int userId, int workspaceId, CancellationToken ct = default)
    {
        var workspace = await workspaceRepository.GetByIdAsync(workspaceId, ct);
        if (workspace is null)
            return false;

        var orgM = await orgMemberRepository.GetAsync(userId, workspace.OrganizationId, ct);
        if (orgM?.Role is null)
            return false;

        if (permissionRepository is null &&
            string.Equals(orgM.Role.Name, OrgOwnerRoleName, StringComparison.Ordinal))
            return true;

        var allPermissionNames = await GetAllActivePermissionNamesAsync(ct);
        return RolePermissionEvaluator.HasAllPermissions(orgM.Role, allPermissionNames);
    }

    public async Task EnsureCanAccessWorkspaceAsync(int userId, int workspaceId, CancellationToken ct = default)
    {
        var m = await memberRepository.GetAsync(userId, workspaceId, ct);
        if (m is not null)
            return;
        if (await IsOrgOwnerOfWorkspaceAsync(userId, workspaceId, ct))
            return;
        throw new UnauthorizedAccessException("You are not a member of this workspace.");
    }

    public async Task<bool> HasWorkspacePermissionAsync(
        int userId,
        int workspaceId,
        string permissionName,
        CancellationToken ct = default)
    {
        await EnsureCanAccessWorkspaceAsync(userId, workspaceId, ct);

        if (await IsOrgOwnerOfWorkspaceAsync(userId, workspaceId, ct))
            return true;

        var m = await memberRepository.GetAsync(userId, workspaceId, ct);
        return m?.Role?.RolePermissions
            .Any(rp => string.Equals(rp.Permission?.Name, permissionName, StringComparison.Ordinal)) == true;
    }

    public async Task<IReadOnlyList<string>> GetEffectiveWorkspacePermissionNamesAsync(
        int userId,
        int workspaceId,
        CancellationToken ct = default)
    {
        await EnsureCanAccessWorkspaceAsync(userId, workspaceId, ct);

        if (await IsOrgOwnerOfWorkspaceAsync(userId, workspaceId, ct))
            return WorkspacePermissions.FullWorkspaceAuthority
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();

        var m = await memberRepository.GetAsync(userId, workspaceId, ct);
        return MapRolePermissionNames(m);
    }

    private async Task<IReadOnlySet<string>> GetAllActivePermissionNamesAsync(CancellationToken ct)
    {
        if (permissionRepository is null)
        {
            var fallback = new HashSet<string>(StringComparer.Ordinal)
            {
                OrganizationPermissions.ManageOrgSettings,
                OrganizationPermissions.InviteToOrg,
                OrganizationPermissions.ManageJoinRequests,
                OrganizationPermissions.RemoveOrgMembers,
                OrganizationPermissions.AssignOrgRoles,
                OrganizationPermissions.ManageOrgRoles,
                OrganizationPermissions.CreateWorkspaces,
                OrganizationPermissions.ManageOrgWorkspaceMembers,
                OrganizationPermissions.CreateOrgUsers,
                OrganizationPermissions.EditOtherOrgUsersProfile,
                OrganizationPermissions.DeleteOrgUsers
            };
            foreach (var permissionName in WorkspacePermissions.FullWorkspaceAuthority)
                fallback.Add(permissionName);
            return fallback;
        }

        var allPermissions = await permissionRepository.GetAllAsync(ct);
        return allPermissions
            .Where(p => !p.IsArchived)
            .Select(p => p.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IReadOnlyList<string> MapRolePermissionNames(UserRoleWorkspace? membership)
    {
        if (membership?.Role?.RolePermissions is null)
            return [];

        return membership.Role.RolePermissions
            .Select(rp => rp.Permission?.Name)
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .Cast<string>()
            .ToList();
    }
}
