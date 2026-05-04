using System.Text.Json;
using FluentValidation;
using Relativa.Core.Application.DTOs.Workspace;
using Relativa.Core.Application.Exceptions;
using Relativa.Core.Application.Interfaces;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Contracts;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Application.Services;

public sealed class WorkspaceService(
    IWorkspaceRepository workspaceRepository,
    IUserRoleWorkspaceRepository memberRepository,
    IWorkspaceRoleRepository roleRepository,
    IUserRoleOrganizationRepository orgMemberRepository,
    IValidator<CreateWorkspaceRequest> createValidator,
    IValidator<UpdateWorkspaceRequest> updateValidator,
    IOutboxWriter? auditOutboxWriter = null) : IWorkspaceService
{
    public async Task<WorkspaceDto> CreateAsync(int userId, CreateWorkspaceRequest request, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);

        var orgMembership = await orgMemberRepository.GetAsync(userId, request.OrganizationId, ct)
            ?? throw new UnauthorizedAccessException("You are not a member of this organization.");

        var hasPermission = orgMembership.Role?.RolePermissions
            .Any(rp => rp.Permission?.Name == "create_workspaces") ?? false;
        if (!hasPermission)
            throw new UnauthorizedAccessException("You do not have the 'create_workspaces' permission in this organization.");

        var adminRole = await roleRepository.GetSystemRoleByNameAsync("ws_admin", ct)
            ?? throw new InvalidOperationException("System ws_admin role not found.");

        var workspace = new Workspace
        {
            Name = request.Name,
            OrganizationId = request.OrganizationId,
            CreatedByUserId = userId,
            IsArchived = false
        };

        await workspaceRepository.AddAsync(workspace, ct);

        var member = new UserRoleWorkspace
        {
            UserId = userId,
            WorkspaceId = workspace.Id,
            WsRoleId = adminRole.Id,
            JoinedAt = DateTime.UtcNow,
            IsArchived = false
        };

        await memberRepository.AddAsync(member, ct);

        if (auditOutboxWriter is not null)
        {
            await auditOutboxWriter.EnqueueAuditAsync(
            new AuditEventContract(
                EventId: Guid.NewGuid(),
                SchemaVersion: 1,
                OccurredAtUtc: DateTimeOffset.UtcNow,
                SourceService: "core",
                ActorUserId: userId,
                AuditScope: AuditRouting.ScopeWorkspace,
                TargetId: workspace.Id,
                Action: "workspace_created",
                FieldName: null,
                EntityType: null,
                OldValueJson: null,
                NewValueJson: JsonSerializer.Serialize(new { workspace.Id, workspace.Name, workspace.OrganizationId })),
            ct);
            await PublishWorkspaceDomainAsync(
                auditOutboxWriter,
                DomainRouting.CoreWorkspaceVerbCreated,
                "created",
                workspace.Id,
                workspace.OrganizationId,
                userId,
                workspace.Name,
                ct);
        }

        return new WorkspaceDto(workspace.Id, workspace.OrganizationId, workspace.Name, 1, adminRole.Name);
    }

    public async Task<List<WorkspaceDto>> GetByUserAsync(int userId, int? organizationId, CancellationToken ct = default)
    {
        List<Workspace> workspaces;
        if (organizationId.HasValue)
        {
            var orgMembership = await orgMemberRepository.GetAsync(userId, organizationId.Value, ct);
            if (orgMembership is null)
            {
                throw new ForbiddenAccessException("You are not a member of this organization.");
            }

            workspaces = await workspaceRepository.GetByUserIdAndOrganizationIdAsync(
                userId,
                organizationId.Value,
                ct);
        }
        else
        {
            workspaces = await workspaceRepository.GetByUserIdAsync(userId, ct);
        }

        var result = new List<WorkspaceDto>();

        foreach (var ws in workspaces)
        {
            var membership = await memberRepository.GetAsync(userId, ws.Id, ct);
            var members = await memberRepository.GetByWorkspaceIdAsync(ws.Id, ct);
            var memberCount = members.Count(m => !m.IsArchived);
            result.Add(new WorkspaceDto(ws.Id, ws.OrganizationId, ws.Name, memberCount, membership?.Role?.Name));
        }

        return result;
    }

    public async Task<WorkspaceDto> GetByIdAsync(int workspaceId, int userId, CancellationToken ct = default)
    {
        await RequireMembership(userId, workspaceId, ct);

        var workspace = await workspaceRepository.GetByIdAsync(workspaceId, ct)
            ?? throw new KeyNotFoundException("Workspace not found.");

        var membership = await memberRepository.GetAsync(userId, workspaceId, ct);
        var members = await memberRepository.GetByWorkspaceIdAsync(workspaceId, ct);

        return new WorkspaceDto(
            workspace.Id,
            workspace.OrganizationId,
            workspace.Name,
            members.Count(m => !m.IsArchived),
            membership?.Role?.Name);
    }

    public async Task UpdateAsync(int workspaceId, int userId, UpdateWorkspaceRequest request, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(request, ct);
        await RequirePermission(userId, workspaceId, "manage_ws_settings", ct);

        var workspace = await workspaceRepository.GetByIdAsync(workspaceId, ct)
            ?? throw new KeyNotFoundException("Workspace not found.");

        var previousName = workspace.Name;
        workspace.Name = request.Name;
        await workspaceRepository.UpdateAsync(workspace, ct);

        if (auditOutboxWriter is not null)
        {
            await auditOutboxWriter.EnqueueAuditAsync(
            new AuditEventContract(
                EventId: Guid.NewGuid(),
                SchemaVersion: 1,
                OccurredAtUtc: DateTimeOffset.UtcNow,
                SourceService: "core",
                ActorUserId: userId,
                AuditScope: AuditRouting.ScopeWorkspace,
                TargetId: workspaceId,
                Action: "workspace_updated",
                FieldName: "name",
                EntityType: null,
                OldValueJson: JsonSerializer.Serialize(new { Name = previousName }),
                NewValueJson: JsonSerializer.Serialize(new { Name = request.Name })),
            ct);
            await PublishWorkspaceDomainAsync(
                auditOutboxWriter,
                DomainRouting.CoreWorkspaceVerbUpdated,
                "updated",
                workspace.Id,
                workspace.OrganizationId,
                userId,
                workspace.Name,
                ct);
        }
    }

    public async Task ArchiveAsync(int workspaceId, int userId, CancellationToken ct = default)
    {
        var membership = await RequireMembership(userId, workspaceId, ct);
        if (membership.Role?.Name != "ws_admin")
            throw new UnauthorizedAccessException("Only workspace admins can archive a workspace.");

        var workspace = await workspaceRepository.GetByIdAsync(workspaceId, ct)
            ?? throw new KeyNotFoundException("Workspace not found.");

        workspace.IsArchived = true;
        await workspaceRepository.UpdateAsync(workspace, ct);

        if (auditOutboxWriter is not null)
        {
            await auditOutboxWriter.EnqueueAuditAsync(
            new AuditEventContract(
                EventId: Guid.NewGuid(),
                SchemaVersion: 1,
                OccurredAtUtc: DateTimeOffset.UtcNow,
                SourceService: "core",
                ActorUserId: userId,
                AuditScope: AuditRouting.ScopeWorkspace,
                TargetId: workspaceId,
                Action: "workspace_archived",
                FieldName: "is_archived",
                EntityType: null,
                OldValueJson: JsonSerializer.Serialize(new { IsArchived = false }),
                NewValueJson: JsonSerializer.Serialize(new { IsArchived = true })),
            ct);
            await PublishWorkspaceDomainAsync(
                auditOutboxWriter,
                DomainRouting.CoreWorkspaceVerbArchived,
                "archived",
                workspace.Id,
                workspace.OrganizationId,
                userId,
                workspace.Name,
                ct);
        }
    }

    private static Task PublishWorkspaceDomainAsync(
        IOutboxWriter outboxWriter,
        string routingVerb,
        string lifecycleAction,
        int workspaceId,
        int organizationId,
        int actorUserId,
        string? workspaceName,
        CancellationToken ct)
    {
        var sagaInstanceId = Guid.NewGuid();
        var envelope = new DomainMessageEnvelope(
            SchemaVersion: MessagingSchemaVersions.V1,
            MessageId: Guid.NewGuid(),
            CorrelationId: sagaInstanceId,
            SagaInstanceId: sagaInstanceId,
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            SourceService: "core",
            PayloadTypeName: DomainPayloadTypes.WorkspaceLifecycleV1,
            PayloadJson: JsonSerializer.Serialize(new WorkspaceLifecyclePayloadV1(
                lifecycleAction,
                workspaceId,
                organizationId,
                actorUserId,
                workspaceName)));

        return outboxWriter.EnqueueDomainAsync(
            DomainRouting.RoutingKeyCoreWorkspace(routingVerb),
            envelope,
            ct);
    }

    private async Task<UserRoleWorkspace> RequireMembership(int userId, int workspaceId, CancellationToken ct)
    {
        return await memberRepository.GetAsync(userId, workspaceId, ct)
            ?? throw new UnauthorizedAccessException("You are not a member of this workspace.");
    }

    private async Task RequirePermission(int userId, int workspaceId, string permission, CancellationToken ct)
    {
        var membership = await RequireMembership(userId, workspaceId, ct);
        var hasPermission = membership.Role?.RolePermissions
            .Any(rp => rp.Permission?.Name == permission) ?? false;
        if (!hasPermission)
            throw new UnauthorizedAccessException($"You do not have the '{permission}' permission in this workspace.");
    }
}
