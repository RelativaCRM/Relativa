namespace Relativa.Core.Application.DTOs.EntityType;

public sealed record EntityTypePropertyDto(
    int PropertyId,
    string Name,
    string DisplayName,
    string DataType,
    bool IsRequired,
    bool IsReadonly,
    IReadOnlyList<AllowedValueDto> AllowedValues);
