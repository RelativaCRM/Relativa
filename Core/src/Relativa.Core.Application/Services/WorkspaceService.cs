using FluentValidation;
using Relativa.Core.Application.DTOs.Workspace;
using Relativa.Core.Application.Interfaces;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Application.Services;

public sealed class WorkspaceService(
    IWorkspaceRepository workspaceRepository,
    IUserRoleWorkspaceRepository memberRepository,
    IWorkspaceRoleRepository roleRepository,
    IUserRoleOrganizationRepository orgMemberRepository,
    IValidator<CreateWorkspaceRequest> createValidator,
    IValidator<UpdateWorkspaceRequest> updateValidator) : IWorkspaceService
{
    public async Task<WorkspaceDto> CreateAsync(int userId, CreateWorkspaceRequest request, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);

        var orgMembership = await orgMemberRepository.GetAsync(userId, request.OrganizationId, ct)
            ?? throw new UnauthorizedAccessException("You are not a member of this organization.");

        var hasPermission = orgMembership.Role?.RolePermissions
            .Any(rp => rp.Permission?.Name == "create_workspaces") ?? false;
        if (!hasPermission)
            throw new UnauthorizedAccessException("You do not have the 'create_workspaces' permission in this organization.");

        var adminRole = await roleRepository.GetSystemRoleByNameAsync("ws_admin", ct)
            ?? throw new InvalidOperationException("System ws_admin role not found.");

        var workspace = new Workspace
        {
            Name = request.Name,
            OrganizationId = request.OrganizationId,
            CreatedByUserId = userId,
            IsArchived = false
        };

        await workspaceRepository.AddAsync(workspace, ct);

        var member = new UserRoleWorkspace
        {
            UserId = userId,
            WorkspaceId = workspace.Id,
            WsRoleId = adminRole.Id,
            JoinedAt = DateTime.UtcNow,
            IsArchived = false
        };

        await memberRepository.AddAsync(member, ct);

        return new WorkspaceDto(workspace.Id, workspace.Name, 1, adminRole.Name);
    }

    public async Task<List<WorkspaceDto>> GetByUserAsync(int userId, CancellationToken ct = default)
    {
        var workspaces = await workspaceRepository.GetByUserIdAsync(userId, ct);
        var result = new List<WorkspaceDto>();

        foreach (var ws in workspaces)
        {
            var membership = await memberRepository.GetAsync(userId, ws.Id, ct);
            var members = await memberRepository.GetByWorkspaceIdAsync(ws.Id, ct);
            var memberCount = members.Count(m => !m.IsArchived);
            result.Add(new WorkspaceDto(ws.Id, ws.Name, memberCount, membership?.Role?.Name));
        }

        return result;
    }

    public async Task<WorkspaceDto> GetByIdAsync(int workspaceId, int userId, CancellationToken ct = default)
    {
        await RequireMembership(userId, workspaceId, ct);

        var workspace = await workspaceRepository.GetByIdAsync(workspaceId, ct)
            ?? throw new KeyNotFoundException("Workspace not found.");

        var membership = await memberRepository.GetAsync(userId, workspaceId, ct);
        var members = await memberRepository.GetByWorkspaceIdAsync(workspaceId, ct);

        return new WorkspaceDto(workspace.Id, workspace.Name, members.Count(m => !m.IsArchived), membership?.Role?.Name);
    }

    public async Task UpdateAsync(int workspaceId, int userId, UpdateWorkspaceRequest request, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(request, ct);
        await RequirePermission(userId, workspaceId, "manage_ws_settings", ct);

        var workspace = await workspaceRepository.GetByIdAsync(workspaceId, ct)
            ?? throw new KeyNotFoundException("Workspace not found.");

        workspace.Name = request.Name;
        await workspaceRepository.UpdateAsync(workspace, ct);
    }

    public async Task ArchiveAsync(int workspaceId, int userId, CancellationToken ct = default)
    {
        var membership = await RequireMembership(userId, workspaceId, ct);
        if (membership.Role?.Name != "ws_admin")
            throw new UnauthorizedAccessException("Only workspace admins can archive a workspace.");

        var workspace = await workspaceRepository.GetByIdAsync(workspaceId, ct)
            ?? throw new KeyNotFoundException("Workspace not found.");

        workspace.IsArchived = true;
        await workspaceRepository.UpdateAsync(workspace, ct);
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
