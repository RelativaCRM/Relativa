using Relativa.Core.Application.DTOs.Member;
using Relativa.Core.Application.Interfaces;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Application.Services;

public sealed class WorkspaceMemberService(
    IUserRoleWorkspaceRepository memberRepository,
    IWorkspaceRoleRepository roleRepository,
    IUserRoleOrganizationRepository orgMemberRepository,
    IWorkspaceRepository workspaceRepository) : IWorkspaceMemberService
{
    public async Task<List<WorkspaceMemberDto>> GetMembersAsync(int workspaceId, int userId, CancellationToken ct = default)
    {
        await RequireMembership(userId, workspaceId, ct);

        var members = await memberRepository.GetByWorkspaceIdAsync(workspaceId, ct);
        return members
            .Where(m => !m.IsArchived)
            .Select(m => new WorkspaceMemberDto(
                m.UserId,
                m.User.FirstName,
                m.User.LastName,
                m.User.Email,
                m.Role.Name,
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

        targetMember.WsRoleId = role.Id;
        await memberRepository.UpdateAsync(targetMember, ct);
    }

    public async Task RemoveAsync(int workspaceId, int targetUserId, int callerUserId, CancellationToken ct = default)
    {
        if (targetUserId != callerUserId)
            await RequirePermission(callerUserId, workspaceId, "remove_ws_members", ct);

        var member = await memberRepository.GetAsync(targetUserId, workspaceId, ct)
            ?? throw new KeyNotFoundException("Target user is not a member of this workspace.");

        await memberRepository.RemoveAsync(member, ct);
    }

    public async Task<WorkspaceMemberDto> AddMemberAsync(int workspaceId, int callerUserId, AddWorkspaceMemberRequest request, CancellationToken ct = default)
    {
        await RequirePermission(callerUserId, workspaceId, "add_ws_members", ct);

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

        var user = targetOrgMembership.User;
        return new WorkspaceMemberDto(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email,
            role.Name,
            member.JoinedAt);
    }

    private async Task<UserRoleWorkspace> RequireMembership(int userId, int workspaceId, CancellationToken ct)
    {
        return await memberRepository.GetAsync(userId, workspaceId, ct)
            ?? throw new UnauthorizedAccessException("You are not a member of this workspace.");
    }

    private async Task RequirePermission(int userId, int workspaceId, string permission, CancellationToken ct)
    {
        var membership = await RequireMembership(userId, workspaceId, ct);
        var hasPermission = membership.Role?.RolePermissions
            .Any(rp => rp.Permission?.Name == permission) ?? false;
        if (!hasPermission)
            throw new UnauthorizedAccessException($"You do not have the '{permission}' permission in this workspace.");
    }
}
