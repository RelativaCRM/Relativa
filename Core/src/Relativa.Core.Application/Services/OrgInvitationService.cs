using Relativa.Core.Application.Exceptions;
using FluentValidation;
using Relativa.Authentication.Domain.Interfaces;
using Relativa.Core.Application.DTOs.OrgInvitation;
using Relativa.Core.Application.Interfaces;
using Relativa.Core.Application.Utilities;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Contracts;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Application.Services;

public sealed class OrgInvitationService(
    IOrgInvitationRepository invitationRepository,
    IUserRoleOrganizationRepository orgMemberRepository,
    IOrganizationRoleRepository orgRoleRepository,
    IUserRepository userRepository,
    IValidator<InviteToOrgRequest> inviteValidator,
    IOutboxWriter? auditOutboxWriter = null) : IOrgInvitationService
{
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);

    public async Task<OrgInvitationDto> InviteAsync(int organizationId, int callerUserId, InviteToOrgRequest request, CancellationToken ct = default)
    {
        await inviteValidator.ValidateAndThrowAsync(request, ct);
        await RequireOrgPermission(callerUserId, organizationId, "invite_to_org", ct);

        var role = await ResolveInviteRoleAsync(organizationId, callerUserId, request.OrgRoleId, ct);
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        await EnsureNotExistingMemberAsync(organizationId, normalizedEmail, ct);
        await EnsureNoPendingInvitationAsync(organizationId, normalizedEmail, ct);

        var invitation = new OrganizationInvitation
        {
            OrganizationId = organizationId,
            Email = normalizedEmail,
            OrgRoleId = role.Id,
            InvitedByUserId = callerUserId,
            Token = Guid.NewGuid().ToString("N"),
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(InvitationLifetime)
        };

        await invitationRepository.AddAsync(invitation, ct);
        await EnqueueAuditAsync(
            callerUserId,
            organizationId,
            action: "organization_invitation_created",
            field: "organization_invitations",
            oldJson: null,
            newJson: new { invitation.Id, invitation.Email, RoleId = role.Id, RoleName = role.Name, invitation.Status },
            ct);

        return new OrgInvitationDto(
            invitation.Id,
            invitation.OrganizationId,
            invitation.Email,
            invitation.Organization?.Name ?? string.Empty,
            role.Name,
            role.DisplayName ?? DisplayNameHelper.Humanize(role.Name),
            invitation.Status,
            invitation.Token,
            invitation.ExpiresAt);
    }

    public async Task<List<OrgInvitationDto>> GetByOrganizationAsync(int organizationId, int callerUserId, CancellationToken ct = default)
    {
        await RequireOrgPermission(callerUserId, organizationId, "invite_to_org", ct);

        var now = DateTime.UtcNow;
        var invitations = await invitationRepository.GetByOrganizationIdAsync(organizationId, ct);
        return invitations
            .Where(i => i.Status == "Pending" && i.ExpiresAt > now)
            .Select(i => new OrgInvitationDto(
                i.Id,
                i.OrganizationId,
                i.Email,
                i.Organization?.Name ?? string.Empty,
                i.Role?.Name ?? string.Empty,
                i.Role is { } ir1 ? (ir1.DisplayName ?? DisplayNameHelper.Humanize(ir1.Name)) : string.Empty,
                i.Status,
                i.Token,
                i.ExpiresAt))
            .ToList();
    }

    public async Task CancelAsync(int organizationId, int invitationId, int callerUserId, CancellationToken ct = default)
    {
        await RequireOrgPermission(callerUserId, organizationId, "invite_to_org", ct);

        var invitation = await invitationRepository.GetByIdAsync(invitationId, ct)
            ?? throw new KeyNotFoundException("Invitation not found.");

        if (invitation.OrganizationId != organizationId)
            throw new KeyNotFoundException("Invitation not found.");

        if (invitation.Status != "Pending")
            throw new InvalidOperationException($"Invitation is no longer pending (status: {invitation.Status}).");

        invitation.Status = "Cancelled";
        await invitationRepository.UpdateAsync(invitation, ct);
        await EnqueueAuditAsync(
            callerUserId,
            organizationId,
            action: "organization_invitation_cancelled",
            field: "organization_invitations.status",
            oldJson: new { Status = "Pending", invitation.Email },
            newJson: new { Status = "Cancelled", invitation.Email },
            ct);
    }

    public async Task<OrgInvitationDto> ResendAsync(int organizationId, int invitationId, int callerUserId, CancellationToken ct = default)
    {
        await RequireOrgPermission(callerUserId, organizationId, "invite_to_org", ct);

        var invitation = await invitationRepository.GetByIdAsync(invitationId, ct)
            ?? throw new KeyNotFoundException("Invitation not found.");

        if (invitation.OrganizationId != organizationId)
            throw new KeyNotFoundException("Invitation not found.");

        if (invitation.Status != "Pending")
            throw new InvalidOperationException($"Cannot resend invitation in status '{invitation.Status}'.");

        var previousToken = invitation.Token;
        var previousExpiresAt = invitation.ExpiresAt;

        invitation.Token = Guid.NewGuid().ToString("N");
        invitation.ExpiresAt = DateTime.UtcNow.Add(InvitationLifetime);

        await invitationRepository.UpdateAsync(invitation, ct);
        await EnqueueAuditAsync(
            callerUserId,
            organizationId,
            action: "organization_invitation_resent",
            field: "organization_invitations.token",
            oldJson: new { Token = previousToken, ExpiresAt = previousExpiresAt, invitation.Email },
            newJson: new { invitation.Token, invitation.ExpiresAt, invitation.Email },
            ct);

        return new OrgInvitationDto(
            invitation.Id,
            invitation.OrganizationId,
            invitation.Email,
            invitation.Organization?.Name ?? string.Empty,
            invitation.Role?.Name ?? string.Empty,
            invitation.Role is { } ir2 ? (ir2.DisplayName ?? DisplayNameHelper.Humanize(ir2.Name)) : string.Empty,
            invitation.Status,
            invitation.Token,
            invitation.ExpiresAt);
    }

    public async Task AcceptAsync(int userId, string userEmail, string token, CancellationToken ct = default)
    {
        var invitation = await invitationRepository.GetByTokenAsync(token, ct)
            ?? throw new KeyNotFoundException("Invitation not found or has expired.");

        if (!string.Equals(invitation.Email, userEmail, StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenAccessException("This invitation was sent to a different email address.");

        if (invitation.Status != "Pending")
            throw new InvalidOperationException($"Invitation is no longer pending (status: {invitation.Status}).");

        if (invitation.ExpiresAt < DateTime.UtcNow)
        {
            invitation.Status = "Expired";
            await invitationRepository.UpdateAsync(invitation, ct);
            throw new InvalidOperationException("Invitation has expired.");
        }

        var existingMembership = await orgMemberRepository.GetAsync(userId, invitation.OrganizationId, ct);
        if (existingMembership is not null)
            throw new InvalidOperationException("You are already a member of this organization.");

        var membership = new UserRoleOrganization
        {
            UserId = userId,
            OrganizationId = invitation.OrganizationId,
            OrgRoleId = invitation.OrgRoleId,
            JoinedAt = DateTime.UtcNow,
            IsArchived = false
        };

        await orgMemberRepository.AddAsync(membership, ct);
        await EnqueueAuditAsync(
            userId,
            invitation.OrganizationId,
            action: "organization_member_added_via_invitation",
            field: "user_role_organization",
            oldJson: null,
            newJson: new { membership.UserId, membership.OrgRoleId },
            ct);

        invitation.Status = "Accepted";
        await invitationRepository.UpdateAsync(invitation, ct);
        await EnqueueAuditAsync(
            userId,
            invitation.OrganizationId,
            action: "organization_invitation_accepted",
            field: "organization_invitations.status",
            oldJson: new { Status = "Pending", invitation.Email },
            newJson: new { Status = "Accepted", invitation.Email },
            ct);
    }

    public async Task DeclineAsync(int userId, string userEmail, string token, CancellationToken ct = default)
    {
        var invitation = await invitationRepository.GetByTokenAsync(token, ct)
            ?? throw new KeyNotFoundException("Invitation not found or has expired.");

        if (!string.Equals(invitation.Email, userEmail, StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenAccessException("This invitation was sent to a different email address.");

        if (invitation.Status != "Pending")
            throw new InvalidOperationException($"Invitation is no longer pending (status: {invitation.Status}).");

        invitation.Status = "Declined";
        await invitationRepository.UpdateAsync(invitation, ct);
        await EnqueueAuditAsync(
            userId,
            invitation.OrganizationId,
            action: "organization_invitation_declined",
            field: "organization_invitations.status",
            oldJson: new { Status = "Pending", invitation.Email },
            newJson: new { Status = "Declined", invitation.Email },
            ct);
    }

    public async Task<List<OrgInvitationDto>> GetMyPendingInvitationsAsync(string userEmail, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userEmail))
            return [];

        var now = DateTime.UtcNow;
        var orgInvitations = await invitationRepository.GetByEmailAsync(userEmail, ct);
        return orgInvitations
            .Where(i => i.Status == "Pending" && i.ExpiresAt > now)
            .Select(i => new OrgInvitationDto(
                i.Id,
                i.OrganizationId,
                i.Email ?? string.Empty,
                i.Organization?.Name ?? string.Empty,
                i.Role?.Name ?? string.Empty,
                i.Role is { } ir3 ? (ir3.DisplayName ?? DisplayNameHelper.Humanize(ir3.Name)) : string.Empty,
                i.Status ?? string.Empty,
                i.Token ?? string.Empty,
                i.ExpiresAt))
            .ToList();
    }

    private async Task<OrganizationRole> ResolveInviteRoleAsync(int organizationId, int callerUserId, int? requestedRoleId, CancellationToken ct)
    {
        var systemRoles = (await orgRoleRepository.GetSystemRolesAsync(ct)) ?? [];
        var defaultRole = systemRoles
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.Id)
            .FirstOrDefault();

        if (!requestedRoleId.HasValue)
        {
            return defaultRole ?? throw new InvalidOperationException("Default system organization role not found.");
        }

        var role = await orgRoleRepository.GetByIdAsync(requestedRoleId.Value, ct)
            ?? throw new ArgumentException("The specified role does not exist.");

        if (role.OrganizationId.HasValue && role.OrganizationId.Value != organizationId)
            throw new ArgumentException("The specified role does not belong to this organization.");

        if (defaultRole is null || role.Id != defaultRole.Id)
        {
            await RequireOrgPermission(callerUserId, organizationId, "assign_org_roles", ct);
        }

        return role;
    }

    private async Task EnsureNotExistingMemberAsync(int organizationId, string normalizedEmail, CancellationToken ct)
    {
        var user = await userRepository.GetByEmailAsync(normalizedEmail, ct);
        if (user is null) return;

        var existing = await orgMemberRepository.GetAsync(user.Id, organizationId, ct);
        if (existing is not null)
            throw new InvalidOperationException("This user is already a member of the organization.");
    }

    private async Task EnsureNoPendingInvitationAsync(int organizationId, string normalizedEmail, CancellationToken ct)
    {
        var existing = await invitationRepository.GetPendingByOrgAndEmailAsync(organizationId, normalizedEmail, ct);
        if (existing is null) return;
        if (existing.ExpiresAt <= DateTime.UtcNow)
        {
            existing.Status = "Expired";
            await invitationRepository.UpdateAsync(existing, ct);
            return;
        }
        throw new InvalidOperationException("A pending invitation for this email already exists.");
    }

    private async Task EnqueueAuditAsync(int actorUserId, int organizationId, string action, string? field, object? oldJson, object? newJson, CancellationToken ct)
    {
        if (auditOutboxWriter is null) return;

        await auditOutboxWriter.EnqueueAuditAsync(
            new AuditEventContract(
                EventId: Guid.NewGuid(),
                SchemaVersion: 1,
                OccurredAtUtc: DateTimeOffset.UtcNow,
                SourceService: "core",
                ActorUserId: actorUserId,
                AuditScope: AuditRouting.ScopeOrganization,
                TargetId: organizationId,
                Action: action,
                FieldName: field,
                EntityType: null,
                OldValueJson: oldJson is null ? null : System.Text.Json.JsonSerializer.Serialize(oldJson),
                NewValueJson: newJson is null ? null : System.Text.Json.JsonSerializer.Serialize(newJson)),
            ct);
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
