namespace Relativa.Core.Application.DTOs.Entity;

public sealed record EntityPropertyValueDto(
    int PropertyId,
    string PropertyName,
    string DataType,
    object? Value,
    bool IsReadonly);
