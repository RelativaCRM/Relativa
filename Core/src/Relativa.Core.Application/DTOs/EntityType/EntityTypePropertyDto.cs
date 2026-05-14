namespace Relativa.Core.Application.DTOs.EntityType;

public sealed record EntityTypePropertyDto(
    int PropertyId,
    string Name,
    string DataType,
    bool IsRequired,
    bool IsReadonly);
