namespace Relativa.Core.Application.DTOs.Entity;

public sealed record EntityPagedResult(
    List<EntityListItemDto> Items,
    int Total,
    int Skip,
    int Take);
