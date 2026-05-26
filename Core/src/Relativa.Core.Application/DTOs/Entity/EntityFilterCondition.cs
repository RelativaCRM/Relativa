namespace Relativa.Core.Application.DTOs.Entity;

// Supported operators by data type:
//   String  : eq, neq, contains, startsWith
//   Int     : eq, neq, gt, lt, gte, lte
//   Decimal : eq, neq, gt, lt, gte, lte
//   Bool    : eq, neq
//   Date    : eq, neq, gt, lt, gte, lte
public sealed record EntityFilterCondition(int PropertyId, string Op, string? Value);
