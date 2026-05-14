namespace Relativa.Persistence.Contracts;

public sealed record MlRecalculateEnqueuedPayloadV1(
    Guid JobId,
    int? WorkspaceId,
    int RequestedByUserId,
    DateTimeOffset RequestedAtUtc,
    string Scope,
    IReadOnlyList<int> EntityIds,
    string Reason);
