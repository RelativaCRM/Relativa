namespace Relativa.Persistence.Contracts;

public sealed record WorkspaceLifecyclePayloadV1(
    string Action,
    int WorkspaceId,
    int OrganizationId,
    int ActorUserId,
    string? Name);
