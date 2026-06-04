namespace Relativa.Core.Application.DTOs.Entity;

public sealed record EntityDetailDto(
    int Id,
    int EntityTypeId,
    string EntityTypeName,
    string EntityTypeDisplayName,
    bool IsArchived,
    List<EntityPropertyValueDto> PropertyValues,
    List<EntityRelationshipRefDto> OutboundRelationships,
    List<EntityRelationshipRefDto> InboundRelationships);
