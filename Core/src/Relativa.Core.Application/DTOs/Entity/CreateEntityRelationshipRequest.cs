namespace Relativa.Core.Application.DTOs.Entity;

public sealed record CreateEntityRelationshipRequest(
    int SourceEntityId,
    int TargetEntityId,
    int RelationshipTypeId);
