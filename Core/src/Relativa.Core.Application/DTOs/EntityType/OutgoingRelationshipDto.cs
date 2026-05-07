namespace Relativa.Core.Application.DTOs.EntityType;

public sealed record OutgoingRelationshipDto(
    int RelationshipTypeId,
    string Name,
    int TargetEntityTypeId,
    string TargetEntityTypeName,
    bool IsRequired);
