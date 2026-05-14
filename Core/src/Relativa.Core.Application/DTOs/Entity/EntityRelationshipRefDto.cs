namespace Relativa.Core.Application.DTOs.Entity;

public sealed record EntityRelationshipRefDto(
    int RelationshipTypeId,
    string RelationshipName,
    int RelatedEntityId,
    string RelatedEntityTypeName,
    IReadOnlyList<EntityPropertyValueDto> PreviewPropertyValues);
