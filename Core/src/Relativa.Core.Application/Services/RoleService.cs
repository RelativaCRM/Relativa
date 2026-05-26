using Relativa.Core.Application.Exceptions;
using FluentValidation;
using Relativa.Core.Application.DTOs.Role;
using Relativa.Core.Application.Interfaces;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Contracts;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Application.Services;

public sealed class RoleService(
    IWorkspaceRoleRepository roleRepository,
    IPermissionRepository permissionRepository,
    IWorkspaceAccessEvaluator workspaceAccess,
    IValidator<CreateRoleRequest> createValidator,
    IOutboxWriter? auditOutboxWriter = null) : IRoleService
{
    public async Task<List<RoleDto>> GetByWorkspaceAsync(int workspaceId, int userId, CancellationToken ct = default)
    {
        await workspaceAccess.EnsureCanAccessWorkspaceAsync(userId, workspaceId, ct);

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
        if (!await workspaceAccess.HasWorkspacePermissionAsync(userId, workspaceId, "manage_ws_roles", ct))
            throw new ForbiddenAccessException("You do not have the 'manage_ws_roles' permission in this workspace.");

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
        if (auditOutboxWriter is not null)
        {
            await auditOutboxWriter.EnqueueAuditAsync(
                new AuditEventContract(
                    EventId: Guid.NewGuid(),
                    SchemaVersion: 1,
                    OccurredAtUtc: DateTimeOffset.UtcNow,
                    SourceService: "core",
                    ActorUserId: userId,
                    AuditScope: AuditRouting.ScopeWorkspace,
                    TargetId: workspaceId,
                    Action: "workspace_role_created",
                    FieldName: "workspace_roles",
                    EntityType: null,
                    OldValueJson: null,
                    NewValueJson: System.Text.Json.JsonSerializer.Serialize(new { role.Id, role.Name, request.PermissionIds })),
                ct);
        }

        return new RoleDto(
            role.Id,
            role.Name,
            false,
            permissions.Select(p => new PermissionDto(p.Id, p.Name)).ToList());
    }

    public async Task UpdateAsync(int workspaceId, int roleId, int userId, UpdateRoleRequest request, CancellationToken ct = default)
    {
        if (!await workspaceAccess.HasWorkspacePermissionAsync(userId, workspaceId, "manage_ws_roles", ct))
            throw new ForbiddenAccessException("You do not have the 'manage_ws_roles' permission in this workspace.");

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
        if (auditOutboxWriter is not null)
        {
            await auditOutboxWriter.EnqueueAuditAsync(
                new AuditEventContract(
                    EventId: Guid.NewGuid(),
                    SchemaVersion: 1,
                    OccurredAtUtc: DateTimeOffset.UtcNow,
                    SourceService: "core",
                    ActorUserId: userId,
                    AuditScope: AuditRouting.ScopeWorkspace,
                    TargetId: workspaceId,
                    Action: "workspace_role_updated",
                    FieldName: "workspace_roles",
                    EntityType: null,
                    OldValueJson: null,
                    NewValueJson: System.Text.Json.JsonSerializer.Serialize(new { role.Id, role.Name, request.PermissionIds })),
                ct);
        }
    }

    public async Task ArchiveAsync(int workspaceId, int roleId, int userId, CancellationToken ct = default)
    {
        if (!await workspaceAccess.HasWorkspacePermissionAsync(userId, workspaceId, "manage_ws_roles", ct))
            throw new ForbiddenAccessException("You do not have the 'manage_ws_roles' permission in this workspace.");

        var role = await roleRepository.GetByIdAsync(roleId, ct)
            ?? throw new KeyNotFoundException("Role not found.");

        if (role.WorkspaceId is null)
            throw new InvalidOperationException("System roles cannot be deleted.");

        if (role.WorkspaceId != workspaceId)
            throw new KeyNotFoundException("Role not found in this workspace.");

        role.IsArchived = true;
        await roleRepository.UpdateAsync(role, ct);
        if (auditOutboxWriter is not null)
        {
            await auditOutboxWriter.EnqueueAuditAsync(
                new AuditEventContract(
                    EventId: Guid.NewGuid(),
                    SchemaVersion: 1,
                    OccurredAtUtc: DateTimeOffset.UtcNow,
                    SourceService: "core",
                    ActorUserId: userId,
                    AuditScope: AuditRouting.ScopeWorkspace,
                    TargetId: workspaceId,
                    Action: "workspace_role_archived",
                    FieldName: "workspace_roles.is_archived",
                    EntityType: null,
                    OldValueJson: System.Text.Json.JsonSerializer.Serialize(new { IsArchived = false, role.Id }),
                    NewValueJson: System.Text.Json.JsonSerializer.Serialize(new { IsArchived = true, role.Id })),
                ct);
        }
    }

    public async Task<List<PermissionDto>> GetAllPermissionsAsync(CancellationToken ct = default)
    {
        var permissions = await permissionRepository.GetAllAsync(ct);
        return permissions
            .Where(p => !p.IsArchived)
            .Select(p => new PermissionDto(p.Id, p.Name))
            .ToList();
    }

}
