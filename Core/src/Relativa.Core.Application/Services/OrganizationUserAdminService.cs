using FluentValidation;
using Relativa.Authentication.Application;
using Relativa.Authentication.Application.DTOs;
using Relativa.Authentication.Application.Interfaces;
using Relativa.Authentication.Domain.Interfaces;
using Relativa.Core.Application.Authorization;
using Relativa.Core.Application.DTOs.Organization;
using Relativa.Core.Application.Exceptions;
using Relativa.Core.Application.Interfaces;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Contracts;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Application.Services;

public sealed class OrganizationUserAdminService(
    IUserProvisioningService userProvisioning,
    IUserRepository userRepository,
    IUserRoleOrganizationRepository orgMemberRepository,
    IOrganizationRoleRepository orgRoleRepository,
    IValidator<CreateOrgUserRequest> createOrgUserValidator,
    IValidator<UpdateOrgUserProfileRequest> updateOrgUserValidator,
    IOutboxWriter? auditOutboxWriter = null) : IOrganizationUserAdminService
{
    public async Task<RegisterResponseDto> CreateOrgUserAsync(int organizationId, int callerUserId, CreateOrgUserRequest request, CancellationToken ct = default)
    {
        await createOrgUserValidator.ValidateAndThrowAsync(request, ct);
        await RequireOrgPermission(callerUserId, organizationId, OrganizationPermissions.CreateOrgUsers, ct);

        var registerRequest = new RegisterRequestDto(
            request.FirstName,
            request.LastName,
            request.Email,
            request.Password);
        var created = await userProvisioning.CreateUserAsync(registerRequest, callerUserId, ct);

        var memberRole = await ResolveTargetRoleAsync(organizationId, callerUserId, request.OrgRoleId, ct);
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
        if (targetUserId == callerUserId)
            throw new ForbiddenAccessException("Archive your own account via the account settings endpoint.");

        await RequireOrgPermission(callerUserId, organizationId, OrganizationPermissions.DeleteOrgUsers, ct);

        var targetMembership = await orgMemberRepository.GetAsync(targetUserId, organizationId, ct)
            ?? throw new KeyNotFoundException("Target user is not a member of this organization.");

        var caller = await userRepository.GetByIdAsync(callerUserId, ct)
            ?? throw new KeyNotFoundException("Caller user not found.");
        var target = await userRepository.GetByIdAsync(targetUserId, ct)
            ?? throw new KeyNotFoundException("Target user not found.");
        if (!EmailDomainMatches(caller.Email, target.Email))
            throw new ForbiddenAccessException("You can archive only users with the same email domain.");

        var callerMembership = await orgMemberRepository.GetAsync(callerUserId, organizationId, ct)
            ?? throw new UnauthorizedAccessException("You are not a member of this organization.");
        if (callerMembership.Role!.Priority >= targetMembership.Role!.Priority)
        {
            throw new ForbiddenAccessException(
                "You cannot perform this action on a member whose organization role has equal or higher authority than yours.");
        }

        await userProvisioning.ArchiveUserAsync(targetUserId, callerUserId, ct);

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
                    Action: "organization_member_account_archived",
                    FieldName: "users.is_archived",
                    EntityType: null,
                    OldValueJson: System.Text.Json.JsonSerializer.Serialize(new
                    {
                        targetUserId,
                        OrgRoleId = targetMembership.OrgRoleId,
                        RoleName = targetMembership.Role?.Name
                    }),
                    NewValueJson: System.Text.Json.JsonSerializer.Serialize(new { targetUserId, Archived = true })),
                ct);
        }
    }

    private async Task<OrganizationRole> ResolveTargetRoleAsync(int organizationId, int callerUserId, int? requestedRoleId, CancellationToken ct)
    {
        var defaultRole = await orgRoleRepository.GetSystemRoleByNameAsync("org_member", ct)
            ?? throw new InvalidOperationException("System org_member role not found.");

        if (!requestedRoleId.HasValue || requestedRoleId.Value == defaultRole.Id)
            return defaultRole;

        await RequireOrgPermission(callerUserId, organizationId, OrganizationPermissions.AssignOrgRoles, ct);
        var requestedRole = await orgRoleRepository.GetByIdAsync(requestedRoleId.Value, ct)
            ?? throw new ArgumentException("The specified role does not exist.");
        if (requestedRole.IsArchived)
            throw new ArgumentException("The specified role is archived.");
        if (requestedRole.OrganizationId.HasValue && requestedRole.OrganizationId.Value != organizationId)
            throw new ArgumentException("The specified role does not belong to this organization.");
        return requestedRole;
    }

    private static bool EmailDomainMatches(string leftEmail, string rightEmail)
    {
        static string ExtractDomain(string value)
        {
            var normalized = EmailNormalizer.Normalize(value);
            var at = normalized.LastIndexOf('@');
            if (at < 0 || at == normalized.Length - 1)
                return string.Empty;
            return normalized[(at + 1)..];
        }

        return ExtractDomain(leftEmail) == ExtractDomain(rightEmail);
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
