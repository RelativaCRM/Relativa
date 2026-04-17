using FluentValidation;
using Relativa.Core.Application.DTOs.OrgInvitation;
using Relativa.Core.Application.Interfaces;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Application.Services;

public sealed class OrgInvitationService(
    IOrgInvitationRepository invitationRepository,
    IUserRoleOrganizationRepository orgMemberRepository,
    IOrganizationRoleRepository orgRoleRepository,
    IValidator<InviteToOrgRequest> inviteValidator) : IOrgInvitationService
{
    public async Task<OrgInvitationDto> InviteAsync(int organizationId, int callerUserId, InviteToOrgRequest request, CancellationToken ct = default)
    {
        await inviteValidator.ValidateAndThrowAsync(request, ct);
        await RequireOrgPermission(callerUserId, organizationId, "invite_to_org", ct);

        var invitation = new OrganizationInvitation
        {
            OrganizationId = organizationId,
            Email = request.Email,
            InvitedByUserId = callerUserId,
            Token = Guid.NewGuid().ToString("N"),
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        await invitationRepository.AddAsync(invitation, ct);

        return new OrgInvitationDto(
            invitation.Id,
            invitation.Email,
            invitation.Organization?.Name ?? "",
            invitation.Status,
            invitation.Token,
            invitation.ExpiresAt);
    }

    public async Task<List<OrgInvitationDto>> GetByOrganizationAsync(int organizationId, int callerUserId, CancellationToken ct = default)
    {
        await RequireOrgPermission(callerUserId, organizationId, "invite_to_org", ct);

        var invitations = await invitationRepository.GetByOrganizationIdAsync(organizationId, ct);
        return invitations
            .Where(i => i.Status == "Pending")
            .Select(i => new OrgInvitationDto(
                i.Id,
                i.Email,
                i.Organization.Name,
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

        invitation.Status = "Cancelled";
        await invitationRepository.UpdateAsync(invitation, ct);
    }

    public async Task AcceptAsync(int userId, string userEmail, string token, CancellationToken ct = default)
    {
        var invitation = await invitationRepository.GetByTokenAsync(token, ct)
            ?? throw new KeyNotFoundException("Invitation not found or has expired.");

        if (!string.Equals(invitation.Email, userEmail, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("This invitation was sent to a different email address.");

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

        var memberRole = await orgRoleRepository.GetSystemRoleByNameAsync("org_member", ct)
            ?? throw new InvalidOperationException("System org_member role not found.");

        var membership = new UserRoleOrganization
        {
            UserId = userId,
            OrganizationId = invitation.OrganizationId,
            OrgRoleId = memberRole.Id,
            JoinedAt = DateTime.UtcNow,
            IsArchived = false
        };

        await orgMemberRepository.AddAsync(membership, ct);

        invitation.Status = "Accepted";
        await invitationRepository.UpdateAsync(invitation, ct);
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
