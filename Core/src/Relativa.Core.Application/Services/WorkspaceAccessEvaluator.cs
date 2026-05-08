using Relativa.Core.Application.Interfaces;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Application.Services;

public sealed class WorkspaceAccessEvaluator(
    IUserRoleWorkspaceRepository memberRepository,
    IUserRoleOrganizationRepository orgMemberRepository,
    IWorkspaceRepository workspaceRepository,
    IWorkspaceRoleRepository workspaceRoleRepository) : IWorkspaceAccessEvaluator
{
    public const string OrgOwnerRoleName = "org_owner";

    public async Task<bool> IsOrgOwnerOfWorkspaceAsync(int userId, int workspaceId, CancellationToken ct = default)
    {
        var workspace = await workspaceRepository.GetByIdAsync(workspaceId, ct);
        if (workspace is null)
            return false;

        var orgM = await orgMemberRepository.GetAsync(userId, workspace.OrganizationId, ct);
        return orgM is { Role: { } role } &&
               string.Equals(role.Name, OrgOwnerRoleName, StringComparison.Ordinal);
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
            return await GetWsAdminPermissionNamesAsync(ct);

        var m = await memberRepository.GetAsync(userId, workspaceId, ct);
        return MapRolePermissionNames(m);
    }

    private async Task<IReadOnlyList<string>> GetWsAdminPermissionNamesAsync(CancellationToken ct)
    {
        var admin = await workspaceRoleRepository.GetSystemRoleByNameAsync("ws_admin", ct)
            ?? throw new InvalidOperationException("System ws_admin role not found.");

        return admin.RolePermissions?
            .Select(rp => rp.Permission?.Name)
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .Cast<string>()
            .ToList() ?? [];
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
