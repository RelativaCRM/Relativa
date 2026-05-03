using FluentValidation;
using Relativa.Authentication.Domain.Interfaces;
using Relativa.Core.Application.DTOs.Invitation;
using Relativa.Core.Application.DTOs.OrgInvitation;
using Relativa.Core.Application.Interfaces;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Contracts;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Application.Services;

public sealed class InvitationService(
    IWorkspaceInvitationRepository invitationRepository,
    IUserRoleWorkspaceRepository memberRepository,
    IWorkspaceRoleRepository roleRepository,
    IOrgInvitationRepository orgInvitationRepository,
    IWorkspaceRepository workspaceRepository,
    IUserRoleOrganizationRepository orgMemberRepository,
    IUserRepository userRepository,
    IValidator<InviteMemberRequest> inviteValidator,
    IValidator<AcceptInvitationRequest> acceptValidator,
    IOutboxWriter? auditOutboxWriter = null) : IInvitationService
{
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);

    public async Task<InvitationDto> InviteAsync(int workspaceId, int callerUserId, InviteMemberRequest request, CancellationToken ct = default)
    {
        await inviteValidator.ValidateAndThrowAsync(request, ct);
        await RequirePermission(callerUserId, workspaceId, "invite_to_workspace", ct);

        var role = await roleRepository.GetByIdAsync(request.RoleId, ct)
            ?? throw new ArgumentException("The specified role does not exist.");

        if (role.WorkspaceId.HasValue && role.WorkspaceId.Value != workspaceId)
            throw new ArgumentException("The specified role does not belong to this workspace.");

        var workspace = await workspaceRepository.GetByIdAsync(workspaceId, ct)
            ?? throw new KeyNotFoundException("Workspace not found.");

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        await EnsureInviteeEligibleAsync(workspace, normalizedEmail, ct);
        await EnsureNoPendingInvitationAsync(workspaceId, normalizedEmail, ct);

        var invitation = new WorkspaceInvitation
        {
            WorkspaceId = workspaceId,
            Email = normalizedEmail,
            WsRoleId = request.RoleId,
            InvitedByUserId = callerUserId,
            Token = Guid.NewGuid().ToString("N"),
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(InvitationLifetime)
        };

        await invitationRepository.AddAsync(invitation, ct);
        await EnqueueAuditAsync(
            callerUserId,
            workspaceId,
            action: "workspace_invitation_created",
            field: "workspace_invitations",
            oldJson: null,
            newJson: new { invitation.Id, invitation.Email, invitation.WsRoleId, invitation.Status },
            ct);

        var reloaded = await invitationRepository.GetByIdAsync(invitation.Id, ct) ?? invitation;
        return new InvitationDto(reloaded.Id, reloaded.Email, reloaded.Workspace?.Name ?? "", role.Name, reloaded.Status, reloaded.Token, reloaded.ExpiresAt);
    }

    public async Task<List<InvitationDto>> GetPendingAsync(int workspaceId, int callerUserId, CancellationToken ct = default)
    {
        await RequirePermission(callerUserId, workspaceId, "invite_to_workspace", ct);

        var now = DateTime.UtcNow;
        var invitations = await invitationRepository.GetByWorkspaceIdAsync(workspaceId, ct);
        return invitations
            .Where(i => i.Status == "Pending" && i.ExpiresAt > now)
            .Select(i => new InvitationDto(i.Id, i.Email, i.Workspace?.Name ?? "", i.Role.Name, i.Status, i.Token, i.ExpiresAt))
            .ToList();
    }

    public async Task CancelAsync(int workspaceId, int invitationId, int callerUserId, CancellationToken ct = default)
    {
        await RequirePermission(callerUserId, workspaceId, "invite_to_workspace", ct);

        var invitation = await invitationRepository.GetByIdAsync(invitationId, ct)
            ?? throw new KeyNotFoundException("Invitation not found.");

        if (invitation.WorkspaceId != workspaceId)
            throw new KeyNotFoundException("Invitation not found.");

        if (invitation.Status != "Pending")
            throw new InvalidOperationException($"Invitation is no longer pending (status: {invitation.Status}).");

        invitation.Status = "Cancelled";
        await invitationRepository.UpdateAsync(invitation, ct);
        await EnqueueAuditAsync(
            callerUserId,
            workspaceId,
            action: "workspace_invitation_cancelled",
            field: "workspace_invitations.status",
            oldJson: new { Status = "Pending", invitation.Email },
            newJson: new { Status = "Cancelled", invitation.Email },
            ct);
    }

    public async Task<InvitationDto> ResendAsync(int workspaceId, int invitationId, int callerUserId, CancellationToken ct = default)
    {
        await RequirePermission(callerUserId, workspaceId, "invite_to_workspace", ct);

        var invitation = await invitationRepository.GetByIdAsync(invitationId, ct)
            ?? throw new KeyNotFoundException("Invitation not found.");

        if (invitation.WorkspaceId != workspaceId)
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
            workspaceId,
            action: "workspace_invitation_resent",
            field: "workspace_invitations.token",
            oldJson: new { Token = previousToken, ExpiresAt = previousExpiresAt, invitation.Email },
            newJson: new { invitation.Token, invitation.ExpiresAt, invitation.Email },
            ct);

        return new InvitationDto(
            invitation.Id,
            invitation.Email,
            invitation.Workspace?.Name ?? "",
            invitation.Role?.Name ?? string.Empty,
            invitation.Status,
            invitation.Token,
            invitation.ExpiresAt);
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

        var workspace = invitation.Workspace
            ?? await workspaceRepository.GetByIdAsync(invitation.WorkspaceId, ct)
            ?? throw new KeyNotFoundException("Workspace not found.");

        var orgMembership = await orgMemberRepository.GetAsync(userId, workspace.OrganizationId, ct);
        if (orgMembership is null)
            throw new InvalidOperationException(
                "You must be a member of this workspace's organization before accepting the invitation.");

        var member = new UserRoleWorkspace
        {
            UserId = userId,
            WorkspaceId = invitation.WorkspaceId,
            WsRoleId = invitation.WsRoleId,
            JoinedAt = DateTime.UtcNow,
            IsArchived = false
        };

        await memberRepository.AddAsync(member, ct);
        await EnqueueAuditAsync(
            userId,
            invitation.WorkspaceId,
            action: "workspace_member_added_via_invitation",
            field: "user_role_workspace",
            oldJson: null,
            newJson: new { member.UserId, member.WsRoleId },
            ct);

        invitation.Status = "Accepted";
        await invitationRepository.UpdateAsync(invitation, ct);
        await EnqueueAuditAsync(
            userId,
            invitation.WorkspaceId,
            action: "workspace_invitation_accepted",
            field: "workspace_invitations.status",
            oldJson: new { Status = "Pending", invitation.Email },
            newJson: new { Status = "Accepted", invitation.Email },
            ct);
    }

    public async Task<MyInvitationsDto> GetMyInvitationsAsync(int userId, string userEmail, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var wsInvitations = await invitationRepository.GetByEmailAsync(userEmail, ct);
        var workspaceInvitations = wsInvitations
            .Where(i => i.Status == "Pending" && i.ExpiresAt > now)
            .Select(i => new InvitationDto(i.Id, i.Email, i.Workspace?.Name ?? "", i.Role.Name, i.Status, i.Token, i.ExpiresAt))
            .ToList();

        var orgInvitations = await orgInvitationRepository.GetByEmailAsync(userEmail, ct);
        var organizationInvitations = orgInvitations
            .Where(i => i.Status == "Pending" && i.ExpiresAt > now)
            .Select(i => new OrgInvitationDto(
                i.Id,
                i.Email,
                i.Organization.Name,
                i.Role?.Name ?? string.Empty,
                i.Status,
                i.Token,
                i.ExpiresAt))
            .ToList();

        return new MyInvitationsDto(workspaceInvitations, organizationInvitations);
    }

    private async Task EnsureInviteeEligibleAsync(Workspace workspace, string normalizedEmail, CancellationToken ct)
    {
        var user = await userRepository.GetByEmailAsync(normalizedEmail, ct);
        if (user is null)
        {
            return;
        }

        var orgMembership = await orgMemberRepository.GetAsync(user.Id, workspace.OrganizationId, ct);
        if (orgMembership is null)
        {
            throw new InvalidOperationException(
                "User must be a member of the organization before being invited to its workspaces.");
        }

        var existingWsMembership = await memberRepository.GetAsync(user.Id, workspace.Id, ct);
        if (existingWsMembership is not null)
        {
            throw new InvalidOperationException("This user is already a member of the workspace.");
        }
    }

    private async Task EnsureNoPendingInvitationAsync(int workspaceId, string normalizedEmail, CancellationToken ct)
    {
        var existing = await invitationRepository.GetPendingByWorkspaceAndEmailAsync(workspaceId, normalizedEmail, ct);
        if (existing is null) return;
        if (existing.ExpiresAt <= DateTime.UtcNow)
        {
            existing.Status = "Expired";
            await invitationRepository.UpdateAsync(existing, ct);
            return;
        }
        throw new InvalidOperationException("A pending invitation for this email already exists.");
    }

    private async Task EnqueueAuditAsync(int actorUserId, int workspaceId, string action, string? field, object? oldJson, object? newJson, CancellationToken ct)
    {
        if (auditOutboxWriter is null) return;

        await auditOutboxWriter.EnqueueAuditAsync(
            new AuditEventContract(
                EventId: Guid.NewGuid(),
                SchemaVersion: 1,
                OccurredAtUtc: DateTimeOffset.UtcNow,
                SourceService: "core",
                ActorUserId: actorUserId,
                AuditScope: AuditRouting.ScopeWorkspace,
                TargetId: workspaceId,
                Action: action,
                FieldName: field,
                EntityType: null,
                OldValueJson: oldJson is null ? null : System.Text.Json.JsonSerializer.Serialize(oldJson),
                NewValueJson: newJson is null ? null : System.Text.Json.JsonSerializer.Serialize(newJson)),
            ct);
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
