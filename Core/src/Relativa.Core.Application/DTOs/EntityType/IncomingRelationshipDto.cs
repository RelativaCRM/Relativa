namespace Relativa.Core.Application.DTOs.EntityType;

/// <summary>Relationship types that point to this entity type (incoming edges).</summary>
public sealed record IncomingRelationshipDto(
    int RelationshipTypeId,
    string Name,
    int SourceEntityTypeId,
    string SourceEntityTypeName,
    bool IsRequired,
    string RelationshipCardinality);
