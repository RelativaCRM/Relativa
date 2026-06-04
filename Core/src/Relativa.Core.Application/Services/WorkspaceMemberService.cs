using Relativa.Core.Application.Exceptions;
using Relativa.Core.Application.DTOs.Member;
using Relativa.Core.Application.Authorization;
using Relativa.Core.Application.Interfaces;
using Relativa.Core.Application.Utilities;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Contracts;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Application.Services;

public sealed class WorkspaceMemberService(
    IUserRoleWorkspaceRepository memberRepository,
    IWorkspaceRoleRepository roleRepository,
    IUserRoleOrganizationRepository orgMemberRepository,
    IWorkspaceRepository workspaceRepository,
    IWorkspaceAccessEvaluator workspaceAccess,
    IOutboxWriter? auditOutboxWriter = null) : IWorkspaceMemberService
{
    private const string ManageOrgWorkspaceMembers = "manage_org_workspace_members";
    public async Task<List<WorkspaceMemberDto>> GetMembersAsync(int workspaceId, int userId, CancellationToken ct = default)
    {
        await workspaceAccess.EnsureCanAccessWorkspaceAsync(userId, workspaceId, ct);

        var members = await memberRepository.GetByWorkspaceIdAsync(workspaceId, ct);
        return members
            .Where(m => !m.IsArchived)
            .Select(m => new WorkspaceMemberDto(
                m.UserId,
                m.User.FirstName,
                m.User.LastName,
                m.User.Email,
                m.Role.Name,
                m.Role.DisplayName ?? DisplayNameHelper.Humanize(m.Role.Name),
                m.JoinedAt))
            .ToList();
    }

    public async Task UpdateRoleAsync(int workspaceId, int targetUserId, int callerUserId, UpdateMemberRoleRequest request, CancellationToken ct = default)
    {
        await RequirePermission(callerUserId, workspaceId, "assign_ws_roles", ct);

        var targetMember = await memberRepository.GetAsync(targetUserId, workspaceId, ct)
            ?? throw new KeyNotFoundException("Target user is not a member of this workspace.");

        var role = await roleRepository.GetByIdAsync(request.RoleId, ct)
            ?? throw new ArgumentException("The specified role does not exist.");

        if (role.WorkspaceId.HasValue && role.WorkspaceId.Value != workspaceId)
            throw new ArgumentException("The specified role does not belong to this workspace.");

        var targetHasFullAuthority = RolePermissionEvaluator.HasAllPermissions(
            targetMember.Role,
            WorkspacePermissions.FullWorkspaceAuthority);
        var targetIsWsAdminFallback = string.Equals(targetMember.Role?.Name, "ws_admin", StringComparison.Ordinal);
        var newRoleHasFullAuthority = RolePermissionEvaluator.HasAllPermissions(
            role,
            WorkspacePermissions.FullWorkspaceAuthority);
        var newRoleIsWsAdminFallback = string.Equals(role.Name, "ws_admin", StringComparison.Ordinal);
        if ((targetHasFullAuthority || targetIsWsAdminFallback) &&
            !(newRoleHasFullAuthority || newRoleIsWsAdminFallback))
        {
            var allMembers = await memberRepository.GetByWorkspaceIdAsync(workspaceId, ct);
            var fullAuthorityCount = allMembers.Count(m =>
                !m.IsArchived &&
                RolePermissionEvaluator.HasAllPermissions(m.Role, WorkspacePermissions.FullWorkspaceAuthority));
            if (fullAuthorityCount == 0)
            {
                fullAuthorityCount = allMembers.Count(m =>
                    !m.IsArchived &&
                    string.Equals(m.Role?.Name, "ws_admin", StringComparison.Ordinal));
            }
            if (fullAuthorityCount <= 1)
            {
                throw new InvalidOperationException("Cannot demote the last workspace admin.");
            }
        }

        targetMember.WsRoleId = role.Id;
        await memberRepository.UpdateAsync(targetMember, ct);
        if (auditOutboxWriter is not null)
        {
            await auditOutboxWriter.EnqueueAuditAsync(
                new AuditEventContract(
                    EventId: Guid.NewGuid(),
                    SchemaVersion: 1,
                    OccurredAtUtc: DateTimeOffset.UtcNow,
                    SourceService: "core",
                    ActorUserId: callerUserId,
                    AuditScope: AuditRouting.ScopeWorkspace,
                    TargetId: workspaceId,
                    Action: "workspace_member_role_changed",
                    FieldName: "user_role_workspace.ws_role_id",
                    EntityType: null,
                    OldValueJson: null,
                    NewValueJson: System.Text.Json.JsonSerializer.Serialize(new { targetUserId, RoleId = role.Id })),
                ct);
        }
    }

    public async Task RemoveAsync(int workspaceId, int targetUserId, int callerUserId, CancellationToken ct = default)
    {
        if (targetUserId != callerUserId)
            await RequireRemoveMemberPermissionAsync(callerUserId, workspaceId, ct);

        var member = await memberRepository.GetAsync(targetUserId, workspaceId, ct)
            ?? throw new KeyNotFoundException("Target user is not a member of this workspace.");

        await memberRepository.RemoveAsync(member, ct);
        if (auditOutboxWriter is not null)
        {
            await auditOutboxWriter.EnqueueAuditAsync(
                new AuditEventContract(
                    EventId: Guid.NewGuid(),
                    SchemaVersion: 1,
                    OccurredAtUtc: DateTimeOffset.UtcNow,
                    SourceService: "core",
                    ActorUserId: callerUserId,
                    AuditScope: AuditRouting.ScopeWorkspace,
                    TargetId: workspaceId,
                    Action: "workspace_member_removed",
                    FieldName: "user_role_workspace",
                    EntityType: null,
                    OldValueJson: System.Text.Json.JsonSerializer.Serialize(new { targetUserId }),
                    NewValueJson: null),
                ct);
        }
    }

    public async Task<WorkspaceMemberDto> AddMemberAsync(int workspaceId, int callerUserId, AddWorkspaceMemberRequest request, CancellationToken ct = default)
    {
        await RequireAddMemberPermissionAsync(callerUserId, workspaceId, ct);

        var workspace = await workspaceRepository.GetByIdAsync(workspaceId, ct)
            ?? throw new KeyNotFoundException("Workspace not found.");

        var targetOrgMembership = await orgMemberRepository.GetAsync(request.UserId, workspace.OrganizationId, ct)
            ?? throw new ArgumentException("The target user is not a member of this organization.");

        var existingMembership = await memberRepository.GetAsync(request.UserId, workspaceId, ct);
        if (existingMembership is not null)
            throw new InvalidOperationException("User is already a member of this workspace.");

        var role = await roleRepository.GetByIdAsync(request.RoleId, ct)
            ?? throw new ArgumentException("The specified role does not exist.");

        if (role.WorkspaceId.HasValue && role.WorkspaceId.Value != workspaceId)
            throw new ArgumentException("The specified role does not belong to this workspace.");

        var member = new UserRoleWorkspace
        {
            UserId = request.UserId,
            WorkspaceId = workspaceId,
            WsRoleId = role.Id,
            JoinedAt = DateTime.UtcNow,
            IsArchived = false
        };

        await memberRepository.AddAsync(member, ct);
        if (auditOutboxWriter is not null)
        {
            await auditOutboxWriter.EnqueueAuditAsync(
                new AuditEventContract(
                    EventId: Guid.NewGuid(),
                    SchemaVersion: 1,
                    OccurredAtUtc: DateTimeOffset.UtcNow,
                    SourceService: "core",
                    ActorUserId: callerUserId,
                    AuditScope: AuditRouting.ScopeWorkspace,
                    TargetId: workspaceId,
                    Action: "workspace_member_added",
                    FieldName: "user_role_workspace",
                    EntityType: null,
                    OldValueJson: null,
                    NewValueJson: System.Text.Json.JsonSerializer.Serialize(new { member.UserId, member.WsRoleId })),
                ct);
        }

        var user = targetOrgMembership.User;
        return new WorkspaceMemberDto(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email,
            role.Name,
            role.DisplayName ?? DisplayNameHelper.Humanize(role.Name),
            member.JoinedAt);
    }

    private async Task RequirePermission(int userId, int workspaceId, string permission, CancellationToken ct)
    {
        if (!await workspaceAccess.HasWorkspacePermissionAsync(userId, workspaceId, permission, ct))
            throw new ForbiddenAccessException($"You do not have the '{permission}' permission in this workspace.");
    }

    private async Task<bool> WorkspaceMemberHasOrOrgOwnerPermissionAsync(int userId, int workspaceId, string permission,
        CancellationToken ct)
    {
        if (await workspaceAccess.IsOrgOwnerOfWorkspaceAsync(userId, workspaceId, ct))
            return true;

        var m = await memberRepository.GetAsync(userId, workspaceId, ct);
        return m?.Role?.RolePermissions
            .Any(rp => string.Equals(rp.Permission?.Name, permission, StringComparison.Ordinal)) == true;
    }

    private async Task<bool> IsOrgWorkspaceMembersManagerAsync(int userId, int workspaceId, CancellationToken ct)
    {
        var workspace = await workspaceRepository.GetByIdAsync(workspaceId, ct);
        if (workspace is null)
            return false;

        var orgMembership = await orgMemberRepository.GetAsync(userId, workspace.OrganizationId, ct);
        return orgMembership?.Role?.RolePermissions
            .Any(rp => string.Equals(rp.Permission?.Name, ManageOrgWorkspaceMembers, StringComparison.Ordinal)) == true;
    }

    private async Task RequireAddMemberPermissionAsync(int callerUserId, int workspaceId, CancellationToken ct)
    {
        if (await WorkspaceMemberHasOrOrgOwnerPermissionAsync(callerUserId, workspaceId, "add_ws_members", ct))
            return;

        if (await IsOrgWorkspaceMembersManagerAsync(callerUserId, workspaceId, ct))
            return;

        var workspace = await workspaceRepository.GetByIdAsync(workspaceId, ct)
            ?? throw new KeyNotFoundException("Workspace not found.");

        _ = await orgMemberRepository.GetAsync(callerUserId, workspace.OrganizationId, ct)
            ?? throw new ForbiddenAccessException("You are not a member of this organization.");

        throw new ForbiddenAccessException("You do not have the 'add_ws_members' permission in this workspace.");
    }

    private async Task RequireRemoveMemberPermissionAsync(int callerUserId, int workspaceId, CancellationToken ct)
    {
        if (await WorkspaceMemberHasOrOrgOwnerPermissionAsync(callerUserId, workspaceId, "remove_ws_members", ct))
            return;

        if (await IsOrgWorkspaceMembersManagerAsync(callerUserId, workspaceId, ct))
            return;

        var workspace = await workspaceRepository.GetByIdAsync(workspaceId, ct)
            ?? throw new KeyNotFoundException("Workspace not found.");

        _ = await orgMemberRepository.GetAsync(callerUserId, workspace.OrganizationId, ct)
            ?? throw new ForbiddenAccessException("You are not a member of this organization.");

        throw new ForbiddenAccessException("You do not have the 'remove_ws_members' permission in this workspace.");
    }
}
