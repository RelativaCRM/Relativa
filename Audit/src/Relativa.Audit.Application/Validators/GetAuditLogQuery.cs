namespace Relativa.Audit.Application.Validators;

/// <summary>
/// Query parameters for GET /audit-log.
/// <paramref name="EntityTypeCategory"/> maps query param <c>entity_type</c> (audit scope: entity|workspace|organization|user).
/// </summary>
public sealed record GetAuditLogQuery(
    string EntityTypeCategory,
    DateTimeOffset? DateFrom,
    DateTimeOffset? DateTo,
    string? Action,
    int Index,
    int PageSize,
    int? EntityId,
    string? DomainEntityType,
    int? WorkspaceId,
    int? OrganizationId,
    int? ActorUserId,
    int? TargetUserId);
