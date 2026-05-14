namespace Relativa.Core.Application.DTOs.Entity;

/// <summary>
/// A single property value supplied by the client. <see cref="Value"/> is always
/// a string representation and is coerced to the correct column type by the service
/// based on the property's <c>data_type</c> (String/Int/Decimal/Bool/Date).
/// </summary>
public sealed record PropertyValueInput(int PropertyId, string? Value);
