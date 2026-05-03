using FluentValidation;
using Relativa.Authentication.Application.DTOs;
using Relativa.Authentication.Application.Interfaces;
using Relativa.Core.Application.Authorization;
using Relativa.Core.Application.DTOs.Organization;
using Relativa.Core.Application.Interfaces;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Contracts;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Application.Services;

public sealed class OrganizationUserAdminService(
    IUserProvisioningService userProvisioning,
    IUserRoleOrganizationRepository orgMemberRepository,
    IOrganizationRoleRepository orgRoleRepository,
    IValidator<UpdateOrgUserProfileRequest> updateOrgUserValidator,
    IOutboxWriter? auditOutboxWriter = null) : IOrganizationUserAdminService
{
    public async Task<RegisterResponseDto> CreateOrgUserAsync(int organizationId, int callerUserId, RegisterRequestDto request, CancellationToken ct = default)
    {
        await RequireOrgPermission(callerUserId, organizationId, OrganizationPermissions.CreateOrgUsers, ct);

        var created = await userProvisioning.CreateUserAsync(request, callerUserId, ct);

        var memberRole = await orgRoleRepository.GetSystemRoleByNameAsync("org_member", ct)
            ?? throw new InvalidOperationException("System org_member role not found.");

        var membership = new UserRoleOrganization
        {
            UserId = created.Id,
            OrganizationId = organizationId,
            OrgRoleId = memberRole.Id,
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
                    ActorUserId: callerUserId,
                    AuditScope: AuditRouting.ScopeOrganization,
                    TargetId: organizationId,
                    Action: "organization_member_added_via_user_provisioning",
                    FieldName: null,
                    EntityType: null,
                    OldValueJson: null,
                    NewValueJson: System.Text.Json.JsonSerializer.Serialize(new { UserId = created.Id, RoleId = memberRole.Id })),
                ct);
        }

        return created;
    }

    public async Task<UserProfileDto> UpdateOtherUserProfileAsync(int organizationId, int targetUserId, int callerUserId, UpdateOrgUserProfileRequest request, CancellationToken ct = default)
    {
        await updateOrgUserValidator.ValidateAndThrowAsync(request, ct);
        await RequireOrgPermission(callerUserId, organizationId, OrganizationPermissions.EditOtherOrgUsersProfile, ct);

        if (targetUserId == callerUserId)
            throw new UnauthorizedAccessException("Edit your own profile via the account settings endpoint.");

        _ = await orgMemberRepository.GetAsync(targetUserId, organizationId, ct)
            ?? throw new KeyNotFoundException("Target user is not a member of this organization.");

        return await userProvisioning.UpdateUserProfileAsync(targetUserId, request.FirstName, request.LastName, callerUserId, ct);
    }

    public async Task DeleteOrgUserAsync(int organizationId, int targetUserId, int callerUserId, CancellationToken ct = default)
    {
        await RequireOrgPermission(callerUserId, organizationId, OrganizationPermissions.DeleteOrgUsers, ct);

        _ = await orgMemberRepository.GetAsync(targetUserId, organizationId, ct)
            ?? throw new KeyNotFoundException("Target user is not a member of this organization.");

        await userProvisioning.ArchiveUserAsync(targetUserId, callerUserId, ct);
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
            throw new UnauthorizedAccessException($"You do not have the '{permission}' permission in this organization.");
    }
}
