namespace Relativa.Core.Application.DTOs.EntityType;

/// <summary>Relationship types that point to this entity type (incoming edges).</summary>
public sealed record IncomingRelationshipDto(
    int RelationshipTypeId,
    string Name,
    string DisplayName,
    int SourceEntityTypeId,
    string SourceEntityTypeName,
    string SourceEntityTypeDisplayName,
    bool IsRequired,
    string RelationshipCardinality);
