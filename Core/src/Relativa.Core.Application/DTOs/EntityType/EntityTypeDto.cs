namespace Relativa.Core.Application.DTOs.EntityType;

public sealed record EntityTypeDto(
    int Id,
    string Name,
    bool IsStandalone,
    List<OutgoingRelationshipDto> OutgoingRelationships,
    List<EntityTypePropertyDto> Properties);
