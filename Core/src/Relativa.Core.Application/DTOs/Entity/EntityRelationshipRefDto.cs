namespace Relativa.Core.Application.DTOs.Entity;

public sealed record EntityRelationshipRefDto(
    int RelationshipId,
    int RelationshipTypeId,
    string RelationshipName,
    string RelationshipDisplayName,
    int RelatedEntityId,
    string RelatedEntityTypeName,
    string RelatedEntityTypeDisplayName,
    IReadOnlyList<EntityPropertyValueDto> PreviewPropertyValues);
