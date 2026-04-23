namespace Relativa.Core.Application.DTOs.EntityType;

public sealed record EntityTypeDto(
    int Id,
    string Name,
    List<EntityTypePropertyDto> Properties);
