using Relativa.Persistence.Entities;

namespace Relativa.Core.Domain.Interfaces;

public sealed record ResolvedFilterCondition(
    int PropertyId,
    PropertyDataType DataType,
    string Op,
    string? StringValue,
    int? IntValue,
    decimal? DecimalValue,
    bool? BoolValue,
    DateOnly? DateValue);
