using Relativa.Audit.Application.DTOs;
using Relativa.Audit.Application.Validators;

namespace Relativa.Audit.Application.Interfaces;

public interface IAuditLogReadRepository
{
    Task EnsureResourcesExistAsync(GetAuditLogQuery q, string category, CancellationToken ct);

    Task<AuditFilterContextDto?> BuildFilterContextAsync(GetAuditLogQuery q, string category, CancellationToken ct);

    Task EnsureRbacAsync(
        int callerUserId,
        string category,
        int? workspaceId,
        int? organizationId,
        CancellationToken ct);

    Task<AuditLogListResponse> GetEntityScopeAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        string? actionFilter,
        int? entityId,
        string? domainEntityType,
        int? actorUserId,
        int workspaceId,
        int skip,
        int pageSize,
        int pageIndex,
        AuditFilterContextDto? filterContext,
        CancellationToken ct);

    Task<AuditLogListResponse> GetWorkspaceScopeAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        string? actionFilter,
        int? actorUserId,
        int workspaceId,
        int skip,
        int pageSize,
        int pageIndex,
        AuditFilterContextDto? filterContext,
        CancellationToken ct);

    Task<AuditLogListResponse> GetOrganizationScopeAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        string? actionFilter,
        int? actorUserId,
        int organizationId,
        int skip,
        int pageSize,
        int pageIndex,
        AuditFilterContextDto? filterContext,
        CancellationToken ct);

    Task<AuditLogListResponse> GetUserScopeAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        string? actionFilter,
        int? actorUserId,
        int? targetUserIdFilter,
        int callerUserId,
        int skip,
        int pageSize,
        int pageIndex,
        AuditFilterContextDto? filterContext,
        CancellationToken ct);
}
