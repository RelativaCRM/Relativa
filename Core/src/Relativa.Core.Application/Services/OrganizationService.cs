using FluentValidation;
using Relativa.Core.Application.Authorization;
using Relativa.Core.Application.DTOs.Organization;
using Relativa.Core.Application.Exceptions;
using Relativa.Core.Application.Interfaces;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Contracts;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Application.Services;

public sealed class OrganizationService(
    IOrganizationRepository organizationRepository,
    IUserRoleOrganizationRepository orgMemberRepository,
    IOrganizationRoleRepository orgRoleRepository,
    IPermissionRepository permissionRepository,
    IValidator<CreateOrganizationRequest> createValidator,
    IValidator<UpdateOrganizationRequest> updateValidator,
    IOutboxWriter? auditOutboxWriter = null) : IOrganizationService
{
    public async Task<OrganizationDto> CreateAsync(int userId, CreateOrganizationRequest request, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);

        var allPermissionNames = (await permissionRepository.GetAllAsync(ct))
            .Where(p => !p.IsArchived)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);
        var ownerRole = (await orgRoleRepository.GetSystemRolesAsync(ct))
            .OrderBy(r => r.Id)
            .FirstOrDefault(r => RolePermissionEvaluator.HasAllPermissions(r, allPermissionNames))
            ?? throw new InvalidOperationException("System org owner-equivalent role not found.");

        var organization = new Organization
        {
            Name = request.Name,
            IsArchived = false
        };

        await organizationRepository.AddAsync(organization, ct);

        var membership = new UserRoleOrganization
        {
            UserId = userId,
            OrganizationId = organization.Id,
            OrgRoleId = ownerRole.Id,
            JoinedAt = DateTime.UtcNow,
            IsArchived = false
        };

        await orgMemberRepository.AddAsync(membership, ct);

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
                TargetId: organization.Id,
                Action: "organization_created",
                FieldName: null,
                EntityType: null,
                OldValueJson: null,
                NewValueJson: System.Text.Json.JsonSerializer.Serialize(new { organization.Id, organization.Name })),
            ct);
        }

        return new OrganizationDto(
            organization.Id,
            organization.Name,
            1,
            ownerRole.Name,
            ownerRole.RolePermissions.Select(rp => rp.Permission.Name).OrderBy(x => x, StringComparer.Ordinal).ToList());
    }

    public async Task<List<OrganizationDto>> GetByUserAsync(int userId, CancellationToken ct = default)
    {
        var organizations = await organizationRepository.GetByUserIdAsync(userId, ct);
        var result = new List<OrganizationDto>();

        foreach (var org in organizations)
        {
            var membership = await orgMemberRepository.GetAsync(userId, org.Id, ct);
            var memberCount = (await orgMemberRepository.GetByOrganizationIdAsync(org.Id, ct)).Count;
            result.Add(new OrganizationDto(
                org.Id,
                org.Name,
                memberCount,
                membership?.Role?.Name,
                membership?.Role?.RolePermissions
                    .Select(rp => rp.Permission?.Name)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Cast<string>()
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToList() ?? []));
        }

        return result;
    }

    public async Task<OrganizationDto> GetByIdAsync(int organizationId, int userId, CancellationToken ct = default)
    {
        await RequireOrgMembership(userId, organizationId, ct);

        var organization = await organizationRepository.GetByIdAsync(organizationId, ct)
            ?? throw new KeyNotFoundException("Organization not found.");

        var membership = await orgMemberRepository.GetAsync(userId, organizationId, ct);
        var members = await orgMemberRepository.GetByOrganizationIdAsync(organizationId, ct);

        return new OrganizationDto(
            organization.Id,
            organization.Name,
            members.Count,
            membership?.Role?.Name,
            membership?.Role?.RolePermissions
                .Select(rp => rp.Permission?.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Cast<string>()
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList() ?? []);
    }

    public async Task UpdateAsync(int organizationId, int userId, UpdateOrganizationRequest request, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(request, ct);
        await RequireOrgPermission(userId, organizationId, "manage_org_settings", ct);

        var organization = await organizationRepository.GetByIdAsync(organizationId, ct)
            ?? throw new KeyNotFoundException("Organization not found.");

        var previousName = organization.Name;
        organization.Name = request.Name;
        await organizationRepository.UpdateAsync(organization, ct);

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
                Action: "organization_updated",
                FieldName: "name",
                EntityType: null,
                OldValueJson: System.Text.Json.JsonSerializer.Serialize(new { Name = previousName }),
                NewValueJson: System.Text.Json.JsonSerializer.Serialize(new { Name = request.Name })),
            ct);
        }
    }

    public async Task<List<OrganizationSearchResultDto>> SearchAsync(string query, CancellationToken ct = default)
    {
        var hits = await organizationRepository.SearchAsync(query, ct);
        return hits
            .Select(h => new OrganizationSearchResultDto(h.Id, h.Name, h.MemberCount))
            .ToList();
    }

    public async Task<List<OrgMemberDto>> GetMembersAsync(int organizationId, int userId, CancellationToken ct = default)
    {
        await RequireOrgMembership(userId, organizationId, ct);

        var members = await orgMemberRepository.GetByOrganizationIdAsync(organizationId, ct);
        return members
            .Where(m => !m.IsArchived)
            .Select(m => new OrgMemberDto(
                m.UserId,
                m.User.FirstName,
                m.User.LastName,
                m.User.Email,
                m.Role.Name,
                m.JoinedAt))
            .ToList();
    }

    public async Task RemoveMemberAsync(int organizationId, int targetUserId, int callerUserId, CancellationToken ct = default)
    {
        if (targetUserId == callerUserId)
        {
            var selfMember = await orgMemberRepository.GetAsync(targetUserId, organizationId, ct)
                ?? throw new KeyNotFoundException("Target user is not a member of this organization.");

            await orgMemberRepository.RemoveAsync(selfMember, ct);

            if (auditOutboxWriter is not null)
            {
                await auditOutboxWriter.EnqueueAuditAsync(
                new AuditEventContract(
                    EventId: Guid.NewGuid(),
                    SchemaVersion: 1,
                    OccurredAtUtc: DateTimeOffset.UtcNow,
                    SourceService: "core",
                    ActorUserId: callerUserId,
                    AuditScope: AuditRouting.ScopeOrganization,
                    TargetId: organizationId,
                    Action: "organization_member_removed",
                    FieldName: "member",
                    EntityType: null,
                    OldValueJson: System.Text.Json.JsonSerializer.Serialize(new { UserId = targetUserId }),
                    NewValueJson: null),
                ct);
            }

            return;
        }

        await RequireOrgPermission(callerUserId, organizationId, "remove_org_members", ct);

        var callerMembership = await orgMemberRepository.GetAsync(callerUserId, organizationId, ct)
            ?? throw new UnauthorizedAccessException("You are not a member of this organization.");
        var targetMember = await orgMemberRepository.GetAsync(targetUserId, organizationId, ct)
            ?? throw new KeyNotFoundException("Target user is not a member of this organization.");

        if (callerMembership.Role!.Priority >= targetMember.Role!.Priority)
        {
            throw new ForbiddenAccessException(
                "You cannot perform this action on a member whose organization role has equal or higher authority than yours.");
        }

        await orgMemberRepository.RemoveAsync(targetMember, ct);

        if (auditOutboxWriter is not null)
        {
            await auditOutboxWriter.EnqueueAuditAsync(
            new AuditEventContract(
                EventId: Guid.NewGuid(),
                SchemaVersion: 1,
                OccurredAtUtc: DateTimeOffset.UtcNow,
                SourceService: "core",
                ActorUserId: callerUserId,
                AuditScope: AuditRouting.ScopeOrganization,
                TargetId: organizationId,
                Action: "organization_member_removed",
                FieldName: "member",
                EntityType: null,
                OldValueJson: System.Text.Json.JsonSerializer.Serialize(new { UserId = targetUserId }),
                NewValueJson: null),
            ct);
        }
    }

    public async Task ChangeMemberRoleAsync(int organizationId, int targetUserId, int callerUserId, ChangeOrgMemberRoleRequest request, CancellationToken ct = default)
    {
        await RequireOrgPermission(callerUserId, organizationId, "assign_org_roles", ct);

        var targetMember = await orgMemberRepository.GetAsync(targetUserId, organizationId, ct)
            ?? throw new KeyNotFoundException("Target user is not a member of this organization.");

        var role = await orgRoleRepository.GetByIdAsync(request.RoleId, ct)
            ?? throw new ArgumentException("The specified role does not exist.");

        if (role.OrganizationId.HasValue && role.OrganizationId.Value != organizationId)
            throw new ArgumentException("The specified role does not belong to this organization.");

        targetMember.OrgRoleId = role.Id;
        await orgMemberRepository.UpdateAsync(targetMember, ct);

        if (auditOutboxWriter is not null)
        {
            await auditOutboxWriter.EnqueueAuditAsync(
            new AuditEventContract(
                EventId: Guid.NewGuid(),
                SchemaVersion: 1,
                OccurredAtUtc: DateTimeOffset.UtcNow,
                SourceService: "core",
                ActorUserId: callerUserId,
                AuditScope: AuditRouting.ScopeOrganization,
                TargetId: organizationId,
                Action: "organization_member_role_changed",
                FieldName: "org_role_id",
                EntityType: null,
                OldValueJson: null,
                NewValueJson: System.Text.Json.JsonSerializer.Serialize(new { UserId = targetUserId, RoleId = role.Id })),
            ct);
        }
    }

    private async Task<UserRoleOrganization> RequireOrgMembership(int userId, int orgId, CancellationToken ct)
    {
        return await orgMemberRepository.GetAsync(userId, orgId, ct)
            ?? throw new UnauthorizedAccessException("You are not a member of this organization.");
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
