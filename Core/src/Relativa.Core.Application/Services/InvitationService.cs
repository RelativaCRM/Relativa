using FluentValidation;
using Relativa.Core.Application.DTOs.Invitation;
using Relativa.Core.Application.DTOs.OrgInvitation;
using Relativa.Core.Application.Interfaces;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Application.Services;

public sealed class InvitationService(
    IWorkspaceInvitationRepository invitationRepository,
    IUserRoleWorkspaceRepository memberRepository,
    IWorkspaceRoleRepository roleRepository,
    IOrgInvitationRepository orgInvitationRepository,
    IValidator<InviteMemberRequest> inviteValidator,
    IValidator<AcceptInvitationRequest> acceptValidator) : IInvitationService
{
    public async Task<InvitationDto> InviteAsync(int workspaceId, int callerUserId, InviteMemberRequest request, CancellationToken ct = default)
    {
        await inviteValidator.ValidateAndThrowAsync(request, ct);
        await RequirePermission(callerUserId, workspaceId, "invite_to_workspace", ct);

        var role = await roleRepository.GetByIdAsync(request.RoleId, ct)
            ?? throw new ArgumentException("The specified role does not exist.");

        if (role.WorkspaceId.HasValue && role.WorkspaceId.Value != workspaceId)
            throw new ArgumentException("The specified role does not belong to this workspace.");

        var invitation = new WorkspaceInvitation
        {
            WorkspaceId = workspaceId,
            Email = request.Email,
            WsRoleId = request.RoleId,
            InvitedByUserId = callerUserId,
            Token = Guid.NewGuid().ToString("N"),
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        await invitationRepository.AddAsync(invitation, ct);

        return new InvitationDto(invitation.Id, invitation.Email, role.Name, invitation.Status, invitation.Token, invitation.ExpiresAt);
    }

    public async Task<List<InvitationDto>> GetPendingAsync(int workspaceId, int callerUserId, CancellationToken ct = default)
    {
        await RequirePermission(callerUserId, workspaceId, "invite_to_workspace", ct);

        var invitations = await invitationRepository.GetByWorkspaceIdAsync(workspaceId, ct);
        return invitations
            .Where(i => i.Status == "Pending")
            .Select(i => new InvitationDto(i.Id, i.Email, i.Role.Name, i.Status, i.Token, i.ExpiresAt))
            .ToList();
    }

    public async Task CancelAsync(int workspaceId, int invitationId, int callerUserId, CancellationToken ct = default)
    {
        await RequirePermission(callerUserId, workspaceId, "invite_to_workspace", ct);

        var invitation = await invitationRepository.GetByIdAsync(invitationId, ct)
            ?? throw new KeyNotFoundException("Invitation not found.");

        if (invitation.WorkspaceId != workspaceId)
            throw new KeyNotFoundException("Invitation not found.");

        invitation.Status = "Cancelled";
        await invitationRepository.UpdateAsync(invitation, ct);
    }

    public async Task AcceptAsync(int userId, string userEmail, AcceptInvitationRequest request, CancellationToken ct = default)
    {
        await acceptValidator.ValidateAndThrowAsync(request, ct);

        var invitation = await invitationRepository.GetByTokenAsync(request.Token, ct)
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

        var existingMembership = await memberRepository.GetAsync(userId, invitation.WorkspaceId, ct);
        if (existingMembership is not null)
            throw new InvalidOperationException("You are already a member of this workspace.");

        var member = new UserRoleWorkspace
        {
            UserId = userId,
            WorkspaceId = invitation.WorkspaceId,
            WsRoleId = invitation.WsRoleId,
            JoinedAt = DateTime.UtcNow,
            IsArchived = false
        };

        await memberRepository.AddAsync(member, ct);

        invitation.Status = "Accepted";
        await invitationRepository.UpdateAsync(invitation, ct);
    }

    public async Task<MyInvitationsDto> GetMyInvitationsAsync(int userId, string userEmail, CancellationToken ct = default)
    {
        var wsInvitations = await invitationRepository.GetByEmailAsync(userEmail, ct);
        var workspaceInvitations = wsInvitations
            .Where(i => i.Status == "Pending")
            .Select(i => new InvitationDto(i.Id, i.Email, i.Role.Name, i.Status, i.Token, i.ExpiresAt))
            .ToList();

        var orgInvitations = await orgInvitationRepository.GetByEmailAsync(userEmail, ct);
        var organizationInvitations = orgInvitations
            .Where(i => i.Status == "Pending")
            .Select(i => new OrgInvitationDto(i.Id, i.Email, i.Organization.Name, i.Status, i.Token, i.ExpiresAt))
            .ToList();

        return new MyInvitationsDto(workspaceInvitations, organizationInvitations);
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
