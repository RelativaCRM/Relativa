namespace Relativa.Persistence.Contracts;

public sealed record EntityAnalysisRefreshPayloadV1(
    int EntityId,
    int EntityTypeId,
    int WorkspaceId,
    int ActorUserId,
    string Action,
    DateTimeOffset SourceUpdatedAtUtc);
