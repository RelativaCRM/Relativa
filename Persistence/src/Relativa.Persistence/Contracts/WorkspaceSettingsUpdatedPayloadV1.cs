namespace Relativa.Persistence.Contracts;

public sealed record WorkspaceSettingsUpdatedPayloadV1(
    int WorkspaceId,
    int OrganizationId,
    int ActorUserId);
