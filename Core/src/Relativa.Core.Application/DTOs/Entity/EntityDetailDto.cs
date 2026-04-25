namespace Relativa.Core.Application.DTOs.Entity;

public sealed record EntityDetailDto(
    int Id,
    int EntityTypeId,
    string EntityTypeName,
    List<EntityPropertyValueDto> PropertyValues);
