using Relativa.Core.Application.Exceptions;
using FluentValidation;
using Relativa.Core.Application.DTOs.OrgRole;
using Relativa.Core.Application.DTOs.Role;
using Relativa.Core.Application.Interfaces;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Contracts;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Application.Services;

public sealed class OrgRoleService(
    IOrganizationRoleRepository orgRoleRepository,
    IPermissionRepository permissionRepository,
    IUserRoleOrganizationRepository orgMemberRepository,
    IValidator<CreateOrgRoleRequest> createValidator,
    IValidator<UpdateOrgRoleRequest> updateValidator,
    IOutboxWriter? auditOutboxWriter = null) : IOrgRoleService
{
    public async Task<List<OrgRoleDto>> GetByOrganizationAsync(int organizationId, int userId, CancellationToken ct = default)
    {
        await RequireOrgMembership(userId, organizationId, ct);

        var roles = await orgRoleRepository.GetByOrganizationIdAsync(organizationId, ct);
        return roles
            .Where(r => !r.IsArchived)
            .Select(r => new OrgRoleDto(
                r.Id,
                r.Name,
                r.OrganizationId is null,
                r.Priority,
                r.RolePermissions.Select(rp => new PermissionDto(rp.Permission.Id, rp.Permission.Name)).ToList()))
            .ToList();
    }

    public async Task<OrgRoleDto> CreateAsync(int organizationId, int userId, CreateOrgRoleRequest request, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);
        await RequireOrgPermission(userId, organizationId, "manage_org_roles", ct);

        var permissions = await permissionRepository.GetByIdsAsync(request.PermissionIds, ct);
        if (permissions.Count != request.PermissionIds.Count)
            throw new ArgumentException("One or more permission IDs are invalid.");

        var role = new OrganizationRole
        {
            Name = request.Name,
            OrganizationId = organizationId,
            Priority = request.Priority,
            IsArchived = false
        };

        await orgRoleRepository.AddAsync(role, ct);

        foreach (var perm in permissions)
        {
            role.RolePermissions.Add(new OrganizationRolePermission
            {
                OrgRoleId = role.Id,
                PermissionId = perm.Id
            });
        }

        await orgRoleRepository.UpdateAsync(role, ct);
        if (auditOutboxWriter is not null)
        {
            await auditOutboxWriter.EnqueueAuditAsync(
                new AuditEventContract(
                    EventId: Guid.NewGuid(),
                    SchemaVersion: 1,
                    OccurredAtUtc: DateTimeOffset.UtcNow,
                    SourceService: "core",
                    ActorUserId: userId,
                    AuditScope: AuditRouting.ScopeOrganization,
                    TargetId: organizationId,
                    Action: "organization_role_created",
                    FieldName: "organization_roles",
                    EntityType: null,
                    OldValueJson: null,
                    NewValueJson: System.Text.Json.JsonSerializer.Serialize(new { role.Id, role.Name, request.PermissionIds, request.Priority })),
                ct);
        }

        return new OrgRoleDto(
            role.Id,
            role.Name,
            false,
            role.Priority,
            permissions.Select(p => new PermissionDto(p.Id, p.Name)).ToList());
    }

    public async Task UpdateAsync(int organizationId, int roleId, int userId, UpdateOrgRoleRequest request, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(request, ct);
        await RequireOrgPermission(userId, organizationId, "manage_org_roles", ct);

        var role = await orgRoleRepository.GetByIdAsync(roleId, ct)
            ?? throw new KeyNotFoundException("Role not found.");

        if (role.OrganizationId is null)
            throw new InvalidOperationException("System roles cannot be modified.");

        if (role.OrganizationId != organizationId)
            throw new KeyNotFoundException("Role not found in this organization.");

        if (request.Name is not null)
            role.Name = request.Name;

        if (request.Priority.HasValue)
            role.Priority = request.Priority.Value;

        if (request.PermissionIds is not null)
        {
            var permissions = await permissionRepository.GetByIdsAsync(request.PermissionIds, ct);
            if (permissions.Count != request.PermissionIds.Count)
                throw new ArgumentException("One or more permission IDs are invalid.");

            role.RolePermissions.Clear();
            foreach (var perm in permissions)
            {
                role.RolePermissions.Add(new OrganizationRolePermission
                {
                    OrgRoleId = role.Id,
                    PermissionId = perm.Id
                });
            }
        }

        await orgRoleRepository.UpdateAsync(role, ct);
        if (auditOutboxWriter is not null)
        {
            await auditOutboxWriter.EnqueueAuditAsync(
                new AuditEventContract(
                    EventId: Guid.NewGuid(),
                    SchemaVersion: 1,
                    OccurredAtUtc: DateTimeOffset.UtcNow,
                    SourceService: "core",
                    ActorUserId: userId,
                    AuditScope: AuditRouting.ScopeOrganization,
                    TargetId: organizationId,
                    Action: "organization_role_updated",
                    FieldName: "organization_roles",
                    EntityType: null,
                    OldValueJson: null,
                    NewValueJson: System.Text.Json.JsonSerializer.Serialize(new
                    {
                        role.Id,
                        role.Name,
                        request.PermissionIds,
                        request.Priority,
                        FinalPriority = role.Priority
                    })),
                ct);
        }
    }

    public async Task ArchiveAsync(int organizationId, int roleId, int userId, CancellationToken ct = default)
    {
        await RequireOrgPermission(userId, organizationId, "manage_org_roles", ct);

        var role = await orgRoleRepository.GetByIdAsync(roleId, ct)
            ?? throw new KeyNotFoundException("Role not found.");

        if (role.OrganizationId is null)
            throw new InvalidOperationException("System roles cannot be deleted.");

        if (role.OrganizationId != organizationId)
            throw new KeyNotFoundException("Role not found in this organization.");

        role.IsArchived = true;
        await orgRoleRepository.UpdateAsync(role, ct);
        if (auditOutboxWriter is not null)
        {
            await auditOutboxWriter.EnqueueAuditAsync(
                new AuditEventContract(
                    EventId: Guid.NewGuid(),
                    SchemaVersion: 1,
                    OccurredAtUtc: DateTimeOffset.UtcNow,
                    SourceService: "core",
                    ActorUserId: userId,
                    AuditScope: AuditRouting.ScopeOrganization,
                    TargetId: organizationId,
                    Action: "organization_role_archived",
                    FieldName: "organization_roles.is_archived",
                    EntityType: null,
                    OldValueJson: System.Text.Json.JsonSerializer.Serialize(new { IsArchived = false, role.Id }),
                    NewValueJson: System.Text.Json.JsonSerializer.Serialize(new { IsArchived = true, role.Id })),
                ct);
        }
    }

    private async Task<UserRoleOrganization> RequireOrgMembership(int userId, int orgId, CancellationToken ct)
    {
        return await orgMemberRepository.GetAsync(userId, orgId, ct)
            ?? throw new ForbiddenAccessException("You are not a member of this organization.");
    }

    private async Task RequireOrgPermission(int userId, int orgId, string permission, CancellationToken ct)
    {
        var membership = await RequireOrgMembership(userId, orgId, ct);
        var hasPermission = membership.Role?.RolePermissions
            .Any(rp => rp.Permission?.Name == permission) ?? false;
        if (!hasPermission)
            throw new ForbiddenAccessException($"You do not have the '{permission}' permission in this organization.");
    }
}
