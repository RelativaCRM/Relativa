namespace Relativa.Persistence.Contracts;

public sealed record MlRecalculateCompletedPayloadV1(
    Guid JobId,
    int? WorkspaceId,
    string Status,
    int ProcessedCount,
    int SucceededCount,
    int FailedCount,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    string? Error);
