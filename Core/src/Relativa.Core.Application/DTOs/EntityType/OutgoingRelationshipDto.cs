namespace Relativa.Core.Application.DTOs.EntityType;

public sealed record OutgoingRelationshipDto(
    int RelationshipTypeId,
    string Name,
    string DisplayName,
    int TargetEntityTypeId,
    string TargetEntityTypeName,
    string TargetEntityTypeDisplayName,
    bool IsRequired,
    string RelationshipCardinality);
