using System.Text.Json.Serialization;

namespace Relativa.Audit.Application.DTOs;

public sealed record AuditLogListResponse(
    IReadOnlyList<AuditLogEntryDto> Data,
    long Total,
    int Page,
    int PerPage,
    AuditFilterContextDto? FilterContext);

public sealed record AuditFilterContextDto(
    WorkspaceContextDto? Workspace,
    OrganizationContextDto? Organization);

public sealed record WorkspaceContextDto(
    int Id,
    string Name,
    int OrganizationId,
    string OrganizationName);

public sealed record OrganizationContextDto(
    int Id,
    string Name);

public sealed record AuditLogEntryDto(
    Guid Id,
    [property: JsonPropertyName("entity_type")] string EntityTypeCategory,
    string Action,
    string? FieldName,
    DateTimeOffset ChangedAt,
    ActorDto? Actor,
    [property: JsonPropertyName("oldValue")] object? OldValue,
    [property: JsonPropertyName("newValue")] object? NewValue,
    EntityAuditContextDto? Entity,
    WorkspaceAuditContextDto? Workspace,
    OrganizationAuditContextDto? Organization,
    UserAuditContextDto? TargetUser,
    bool? EntityDeleted,
    string? EntityTypeIdFromEvent,
    IReadOnlyList<PropertyDefinitionDto>? PropertyDefinitionsForEntityType,
    IReadOnlyList<PropertyChangeDto>? PropertyChanges);

public sealed record ActorDto(
    int? UserId,
    string? Email,
    string? FirstName,
    string? LastName);

public sealed record EntityAuditContextDto(
    int? Id,
    int? EntityTypeId,
    string? EntityTypeName,
    bool? IsArchived);

public sealed record WorkspaceAuditContextDto(
    int? Id,
    string? Name,
    int? OrganizationId,
    string? OrganizationName);

public sealed record OrganizationAuditContextDto(
    int? Id,
    string? Name);

public sealed record UserAuditContextDto(
    int? Id,
    string? Email,
    string? FirstName,
    string? LastName);

public sealed record PropertyDefinitionDto(
    int PropertyId,
    string Name,
    string DataType);

public sealed record PropertyChangeDto(
    int PropertyId,
    string PropertyName,
    string DataType,
    object? OldValue,
    object? NewValue);
