using FluentValidation;
using Relativa.Core.Application.DTOs.Role;
using Relativa.Core.Application.Interfaces;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Application.Services;

public sealed class RoleService(
    IWorkspaceRoleRepository roleRepository,
    IPermissionRepository permissionRepository,
    IUserRoleWorkspaceRepository memberRepository,
    IValidator<CreateRoleRequest> createValidator) : IRoleService
{
    public async Task<List<RoleDto>> GetByWorkspaceAsync(int workspaceId, int userId, CancellationToken ct = default)
    {
        await RequireMembership(userId, workspaceId, ct);

        var roles = await roleRepository.GetByWorkspaceIdAsync(workspaceId, ct);
        return roles
            .Where(r => !r.IsArchived)
            .Select(r => new RoleDto(
                r.Id,
                r.Name,
                r.WorkspaceId is null,
                r.RolePermissions.Select(rp => new PermissionDto(rp.Permission.Id, rp.Permission.Name)).ToList()))
            .ToList();
    }

    public async Task<RoleDto> CreateAsync(int workspaceId, int userId, CreateRoleRequest request, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);
        await RequirePermission(userId, workspaceId, "manage_ws_roles", ct);

        var permissions = await permissionRepository.GetByIdsAsync(request.PermissionIds, ct);
        if (permissions.Count != request.PermissionIds.Count)
            throw new ArgumentException("One or more permission IDs are invalid.");

        var role = new WorkspaceRole
        {
            Name = request.Name,
            WorkspaceId = workspaceId,
            IsArchived = false
        };

        await roleRepository.AddAsync(role, ct);

        foreach (var perm in permissions)
        {
            role.RolePermissions.Add(new WorkspaceRolePermission
            {
                WsRoleId = role.Id,
                PermissionId = perm.Id
            });
        }

        await roleRepository.UpdateAsync(role, ct);

        return new RoleDto(
            role.Id,
            role.Name,
            false,
            permissions.Select(p => new PermissionDto(p.Id, p.Name)).ToList());
    }

    public async Task UpdateAsync(int workspaceId, int roleId, int userId, UpdateRoleRequest request, CancellationToken ct = default)
    {
        await RequirePermission(userId, workspaceId, "manage_ws_roles", ct);

        var role = await roleRepository.GetByIdAsync(roleId, ct)
            ?? throw new KeyNotFoundException("Role not found.");

        if (role.WorkspaceId is null)
            throw new InvalidOperationException("System roles cannot be modified.");

        if (role.WorkspaceId != workspaceId)
            throw new KeyNotFoundException("Role not found in this workspace.");

        if (request.Name is not null)
            role.Name = request.Name;

        if (request.PermissionIds is not null)
        {
            var permissions = await permissionRepository.GetByIdsAsync(request.PermissionIds, ct);
            if (permissions.Count != request.PermissionIds.Count)
                throw new ArgumentException("One or more permission IDs are invalid.");

            role.RolePermissions.Clear();
            foreach (var perm in permissions)
            {
                role.RolePermissions.Add(new WorkspaceRolePermission
                {
                    WsRoleId = role.Id,
                    PermissionId = perm.Id
                });
            }
        }

        await roleRepository.UpdateAsync(role, ct);
    }

    public async Task ArchiveAsync(int workspaceId, int roleId, int userId, CancellationToken ct = default)
    {
        await RequirePermission(userId, workspaceId, "manage_ws_roles", ct);

        var role = await roleRepository.GetByIdAsync(roleId, ct)
            ?? throw new KeyNotFoundException("Role not found.");

        if (role.WorkspaceId is null)
            throw new InvalidOperationException("System roles cannot be deleted.");

        if (role.WorkspaceId != workspaceId)
            throw new KeyNotFoundException("Role not found in this workspace.");

        role.IsArchived = true;
        await roleRepository.UpdateAsync(role, ct);
    }

    public async Task<List<PermissionDto>> GetAllPermissionsAsync(CancellationToken ct = default)
    {
        var permissions = await permissionRepository.GetAllAsync(ct);
        return permissions
            .Where(p => !p.IsArchived)
            .Select(p => new PermissionDto(p.Id, p.Name))
            .ToList();
    }

    private async Task RequireMembership(int userId, int workspaceId, CancellationToken ct)
    {
        _ = await memberRepository.GetAsync(userId, workspaceId, ct)
            ?? throw new UnauthorizedAccessException("You are not a member of this workspace.");
    }

    private async Task RequirePermission(int userId, int workspaceId, string permission, CancellationToken ct)
    {
        var membership = await memberRepository.GetAsync(userId, workspaceId, ct)
            ?? throw new UnauthorizedAccessException("You are not a member of this workspace.");

        var hasPermission = membership.Role?.RolePermissions
            .Any(rp => rp.Permission?.Name == permission) ?? false;
        if (!hasPermission)
            throw new UnauthorizedAccessException($"You do not have the '{permission}' permission in this workspace.");
    }
}
