using FluentValidation;
using Relativa.Core.Application.DTOs.WsJoinRequest;
using Relativa.Core.Application.Interfaces;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Contracts;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Application.Services;

public sealed class WsJoinRequestService(
    IWsJoinRequestRepository joinRequestRepository,
    IUserRoleWorkspaceRepository wsMemberRepository,
    IUserRoleOrganizationRepository orgMemberRepository,
    IWorkspaceRoleRepository wsRoleRepository,
    IWorkspaceRepository workspaceRepository,
    IValidator<CreateWsJoinRequestRequest> createValidator,
    IValidator<ReviewWsJoinRequestRequest> reviewValidator,
    IOutboxWriter? auditOutboxWriter = null) : IWsJoinRequestService
{
    private const string DefaultWsRoleName = "ws_member";

    public async Task<WsJoinRequestDto> SubmitAsync(int workspaceId, int userId, CreateWsJoinRequestRequest request, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);

        var workspace = await workspaceRepository.GetByIdAsync(workspaceId, ct)
            ?? throw new KeyNotFoundException("Workspace not found.");
        if (workspace.IsArchived)
            throw new InvalidOperationException("This workspace is archived.");

        _ = await orgMemberRepository.GetAsync(userId, workspace.OrganizationId, ct)
            ?? throw new UnauthorizedAccessException(
                "You must be a member of this workspace's organization before requesting to join the workspace.");

        var existingWsMembership = await wsMemberRepository.GetAsync(userId, workspaceId, ct);
        if (existingWsMembership is not null)
            throw new InvalidOperationException("You are already a member of this workspace.");

        var existingPending = await joinRequestRepository.GetPendingAsync(userId, workspaceId, ct);
        if (existingPending is not null)
            throw new InvalidOperationException("You already have a pending join request for this workspace.");

        var joinRequest = new WorkspaceJoinRequest
        {
            UserId = userId,
            WorkspaceId = workspaceId,
            Message = request.Message,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        await joinRequestRepository.AddAsync(joinRequest, ct);
        await EnqueueAuditAsync(
            userId,
            workspaceId,
            action: "workspace_join_request_submitted",
            field: "workspace_join_requests",
            oldJson: null,
            newJson: new { joinRequest.Id, joinRequest.Message },
            ct);

        return new WsJoinRequestDto(
            joinRequest.Id,
            joinRequest.UserId,
            joinRequest.User is not null ? $"{joinRequest.User.FirstName} {joinRequest.User.LastName}" : string.Empty,
            joinRequest.User?.Email ?? string.Empty,
            joinRequest.WorkspaceId,
            workspace.Name,
            joinRequest.Message,
            joinRequest.Status,
            joinRequest.CreatedAt,
            null,
            null);
    }

    public async Task<List<WsJoinRequestDto>> GetByWorkspaceAsync(int workspaceId, int callerUserId, CancellationToken ct = default)
    {
        await RequireWsPermission(callerUserId, workspaceId, "manage_ws_join_requests", ct);

        var requests = await joinRequestRepository.GetByWorkspaceIdAsync(workspaceId, ct);
        return requests
            .Where(r => r.Status == "Pending")
            .Select(r => new WsJoinRequestDto(
                r.Id,
                r.UserId,
                r.User is not null ? $"{r.User.FirstName} {r.User.LastName}" : string.Empty,
                r.User?.Email ?? string.Empty,
                r.WorkspaceId,
                r.Workspace?.Name ?? string.Empty,
                r.Message,
                r.Status,
                r.CreatedAt,
                r.ReviewedBy is not null ? $"{r.ReviewedBy.FirstName} {r.ReviewedBy.LastName}" : null,
                r.ReviewedAt))
            .ToList();
    }

    public async Task ReviewAsync(int workspaceId, int requestId, int callerUserId, ReviewWsJoinRequestRequest request, CancellationToken ct = default)
    {
        await reviewValidator.ValidateAndThrowAsync(request, ct);
        await RequireWsPermission(callerUserId, workspaceId, "manage_ws_join_requests", ct);

        var joinRequest = await joinRequestRepository.GetByIdAsync(requestId, ct)
            ?? throw new KeyNotFoundException("Join request not found.");

        if (joinRequest.WorkspaceId != workspaceId)
            throw new KeyNotFoundException("Join request not found in this workspace.");

        if (joinRequest.Status != "Pending")
            throw new InvalidOperationException($"Join request is no longer pending (status: {joinRequest.Status}).");

        if (request.Decision == "Approved")
        {
            var workspace = joinRequest.Workspace
                ?? await workspaceRepository.GetByIdAsync(workspaceId, ct)
                ?? throw new KeyNotFoundException("Workspace not found.");

            // Re-check that the requester is still an org member — their org membership could have been revoked
            // between submission and approval.
            var orgMembership = await orgMemberRepository.GetAsync(joinRequest.UserId, workspace.OrganizationId, ct);
            if (orgMembership is null)
            {
                joinRequest.Status = "Rejected";
                joinRequest.ReviewedByUserId = callerUserId;
                joinRequest.ReviewedAt = DateTime.UtcNow;
                await joinRequestRepository.UpdateAsync(joinRequest, ct);
                throw new InvalidOperationException(
                    "The requester is no longer a member of this workspace's organization. Request has been automatically rejected.");
            }

            var memberRole = await wsRoleRepository.GetSystemRoleByNameAsync(DefaultWsRoleName, ct)
                ?? throw new InvalidOperationException("System ws_member role not found.");

            var membership = new UserRoleWorkspace
            {
                UserId = joinRequest.UserId,
                WorkspaceId = workspaceId,
                WsRoleId = memberRole.Id,
                JoinedAt = DateTime.UtcNow,
                IsArchived = false
            };

            await wsMemberRepository.AddAsync(membership, ct);
            await EnqueueAuditAsync(
                callerUserId,
                workspaceId,
                action: "workspace_member_added_via_join_request",
                field: "user_role_workspace",
                oldJson: null,
                newJson: new { joinRequest.UserId, RoleId = memberRole.Id },
                ct);
        }
        else if (request.Decision != "Rejected")
        {
            throw new ArgumentException("Decision must be 'Approved' or 'Rejected'.");
        }

        var previousStatus = joinRequest.Status;
        joinRequest.Status = request.Decision;
        joinRequest.ReviewedByUserId = callerUserId;
        joinRequest.ReviewedAt = DateTime.UtcNow;
        await joinRequestRepository.UpdateAsync(joinRequest, ct);
        await EnqueueAuditAsync(
            callerUserId,
            workspaceId,
            action: "workspace_join_request_reviewed",
            field: "workspace_join_requests.status",
            oldJson: new { Status = previousStatus },
            newJson: new { Status = request.Decision, joinRequest.UserId },
            ct);
    }

    public async Task<List<WsJoinRequestDto>> GetMyRequestsAsync(int userId, CancellationToken ct = default)
    {
        var requests = await joinRequestRepository.GetByUserIdAsync(userId, ct);
        return requests
            .Select(r => new WsJoinRequestDto(
                r.Id,
                r.UserId,
                r.User is not null ? $"{r.User.FirstName} {r.User.LastName}" : string.Empty,
                r.User?.Email ?? string.Empty,
                r.WorkspaceId,
                r.Workspace?.Name ?? string.Empty,
                r.Message,
                r.Status,
                r.CreatedAt,
                r.ReviewedBy is not null ? $"{r.ReviewedBy.FirstName} {r.ReviewedBy.LastName}" : null,
                r.ReviewedAt))
            .ToList();
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

    private async Task RequireWsPermission(int userId, int workspaceId, string permission, CancellationToken ct)
    {
        var membership = await wsMemberRepository.GetAsync(userId, workspaceId, ct)
            ?? throw new UnauthorizedAccessException("You are not a member of this workspace.");

        var hasPermission = membership.Role?.RolePermissions
            .Any(rp => rp.Permission?.Name == permission) ?? false;
        if (!hasPermission)
            throw new UnauthorizedAccessException($"You do not have the '{permission}' permission in this workspace.");
    }
}
