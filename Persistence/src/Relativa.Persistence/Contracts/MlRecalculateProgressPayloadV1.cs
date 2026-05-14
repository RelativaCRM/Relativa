namespace Relativa.Persistence.Contracts;

public sealed record MlRecalculateProgressPayloadV1(
    Guid JobId,
    int? WorkspaceId,
    string Status,
    int ProcessedCount,
    int TotalCount,
    DateTimeOffset UpdatedAtUtc,
    string? Message);
