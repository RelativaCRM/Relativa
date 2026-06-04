using Relativa.Core.Application.Exceptions;
using Relativa.Core.Application.DTOs.JoinRequest;
using Relativa.Core.Application.Interfaces;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Contracts;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Application.Services;

public sealed class JoinRequestService(
    IJoinRequestRepository joinRequestRepository,
    IUserRoleOrganizationRepository orgMemberRepository,
    IOrganizationRoleRepository orgRoleRepository,
    IOrganizationRepository organizationRepository,
    IOrganizationSettingsRepository organizationSettingsRepository,
    IOutboxWriter? auditOutboxWriter = null) : IJoinRequestService
{
    public async Task<JoinRequestDto> SubmitAsync(int organizationId, int userId, CreateJoinRequestRequest request, CancellationToken ct = default)
    {
        var organization = await organizationRepository.GetByIdAsync(organizationId, ct)
            ?? throw new KeyNotFoundException("Organization not found.");

        if (organization.IsArchived)
            throw new InvalidOperationException("This organization is archived.");

        var orgSettings = await organizationSettingsRepository.GetByOrganizationIdAsync(organizationId, ct);
        if (orgSettings?.JoinPolicy == "invite_only")
            throw new ForbiddenAccessException("This organization is not accepting join requests.");

        var existingMembership = await orgMemberRepository.GetAsync(userId, organizationId, ct);
        if (existingMembership is not null)
            throw new InvalidOperationException("You are already a member of this organization.");

        var existingRequest = await joinRequestRepository.GetPendingAsync(userId, organizationId, ct);
        if (existingRequest is not null)
            throw new InvalidOperationException("You already have a pending join request for this organization.");

        var joinRequest = new OrganizationJoinRequest
        {
            UserId = userId,
            OrganizationId = organizationId,
            Message = request.Message,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        await joinRequestRepository.AddAsync(joinRequest, ct);
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
                    Action: "organization_join_request_submitted",
                    FieldName: "organization_join_requests",
                    EntityType: null,
                    OldValueJson: null,
                    NewValueJson: System.Text.Json.JsonSerializer.Serialize(new { joinRequest.Id, joinRequest.Message })),
                ct);
        }

        return new JoinRequestDto(
            joinRequest.Id,
            joinRequest.UserId,
            joinRequest.User is not null ? $"{joinRequest.User.FirstName} {joinRequest.User.LastName}".Trim() : string.Empty,
            joinRequest.User?.Email ?? string.Empty,
            joinRequest.Message,
            joinRequest.Status,
            joinRequest.CreatedAt,
            null,
            null,
            organizationId,
            organization.Name);
    }

    public async Task<List<JoinRequestDto>> GetByOrganizationAsync(int organizationId, int callerUserId, CancellationToken ct = default)
    {
        await RequireOrgPermission(callerUserId, organizationId, "manage_join_requests", ct);

        var requests = await joinRequestRepository.GetByOrganizationIdAsync(organizationId, ct);
        return requests
            .Where(r => r.Status == "Pending")
            .Select(r => new JoinRequestDto(
                r.Id,
                r.UserId,
                r.User is not null ? $"{r.User.FirstName} {r.User.LastName}".Trim() : string.Empty,
                r.User?.Email ?? string.Empty,
                r.Message,
                r.Status,
                r.CreatedAt,
                r.ReviewedBy is not null ? $"{r.ReviewedBy.FirstName} {r.ReviewedBy.LastName}".Trim() : null,
                r.ReviewedAt,
                r.OrganizationId,
                r.Organization?.Name ?? string.Empty))
            .ToList();
    }

    public async Task ReviewAsync(int organizationId, int requestId, int callerUserId, ReviewJoinRequestRequest request, CancellationToken ct = default)
    {
        await RequireOrgPermission(callerUserId, organizationId, "manage_join_requests", ct);

        var joinRequest = await joinRequestRepository.GetByIdAsync(requestId, ct)
            ?? throw new KeyNotFoundException("Join request not found.");

        if (joinRequest.OrganizationId != organizationId)
            throw new KeyNotFoundException("Join request not found in this organization.");

        if (joinRequest.Status != "Pending")
            throw new InvalidOperationException($"Join request is no longer pending (status: {joinRequest.Status}).");

        if (request.Decision == "Approved")
        {
            var existingMembership = await orgMemberRepository.GetAsync(joinRequest.UserId, organizationId, ct);
            if (existingMembership is not null)
                throw new InvalidOperationException("The requester is already a member of this organization.");

            var settings = await organizationSettingsRepository.GetByOrganizationIdAsync(organizationId, ct);
            var memberRole = settings?.DefaultOrgRoleId.HasValue == true
                ? await orgRoleRepository.GetByIdAsync(settings.DefaultOrgRoleId.Value, ct)
                    ?? throw new InvalidOperationException("Configured default org role not found.")
                : ((await orgRoleRepository.GetSystemRolesAsync(ct)) ?? [])
                    .OrderByDescending(r => r.Priority)
                    .ThenBy(r => r.Id)
                    .FirstOrDefault()
                    ?? throw new InvalidOperationException("Default system organization role not found.");

            var membership = new UserRoleOrganization
            {
                UserId = joinRequest.UserId,
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
                        Action: "organization_member_added_via_join_request",
                        FieldName: "user_role_organization",
                        EntityType: null,
                        OldValueJson: null,
                        NewValueJson: System.Text.Json.JsonSerializer.Serialize(new { joinRequest.UserId, RoleId = memberRole.Id })),
                    ct);
            }
        }
        else if (request.Decision != "Rejected")
        {
            throw new ArgumentException("Decision must be 'Approved' or 'Rejected'.");
        }

        joinRequest.Status = request.Decision;
        joinRequest.ReviewedByUserId = callerUserId;
        joinRequest.ReviewedAt = DateTime.UtcNow;
        await joinRequestRepository.UpdateAsync(joinRequest, ct);
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
                    Action: "organization_join_request_reviewed",
                    FieldName: "organization_join_requests.status",
                    EntityType: null,
                    OldValueJson: System.Text.Json.JsonSerializer.Serialize(new { Status = "Pending" }),
                    NewValueJson: System.Text.Json.JsonSerializer.Serialize(new { Status = request.Decision, joinRequest.UserId })),
                ct);
        }
    }

    public async Task<List<JoinRequestDto>> GetMyRequestsAsync(int userId, CancellationToken ct = default)
    {
        var requests = await joinRequestRepository.GetByUserIdAsync(userId, ct);
        return requests
            .Select(r => new JoinRequestDto(
                r.Id,
                r.UserId,
                r.User is not null ? $"{r.User.FirstName} {r.User.LastName}".Trim() : string.Empty,
                r.User?.Email ?? string.Empty,
                r.Message,
                r.Status,
                r.CreatedAt,
                r.ReviewedBy is not null ? $"{r.ReviewedBy.FirstName} {r.ReviewedBy.LastName}".Trim() : null,
                r.ReviewedAt,
                r.OrganizationId,
                r.Organization?.Name ?? string.Empty))
            .ToList();
    }

    public async Task CancelMineAsync(int requestId, int userId, CancellationToken ct = default)
    {
        var joinRequest = await joinRequestRepository.GetByIdAsync(requestId, ct)
            ?? throw new KeyNotFoundException("Join request not found.");

        if (joinRequest.UserId != userId)
            throw new ForbiddenAccessException("You can only cancel your own join requests.");

        if (joinRequest.Status != "Pending")
            throw new InvalidOperationException($"Join request is no longer pending (status: {joinRequest.Status}).");

        joinRequest.Status = "Cancelled";
        joinRequest.ReviewedAt = DateTime.UtcNow;
        await joinRequestRepository.UpdateAsync(joinRequest, ct);

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
                    TargetId: joinRequest.OrganizationId,
                    Action: "organization_join_request_cancelled",
                    FieldName: "organization_join_requests.status",
                    EntityType: null,
                    OldValueJson: System.Text.Json.JsonSerializer.Serialize(new { Status = "Pending" }),
                    NewValueJson: System.Text.Json.JsonSerializer.Serialize(new { Status = "Cancelled", joinRequest.UserId })),
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
