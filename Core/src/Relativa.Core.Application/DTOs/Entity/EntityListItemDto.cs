namespace Relativa.Core.Application.DTOs.Entity;

public sealed record EntityListItemDto(
    int Id,
    int EntityTypeId,
    string EntityTypeName,
    string EntityTypeDisplayName,
    List<EntityPropertyValueDto> PropertyValues);
