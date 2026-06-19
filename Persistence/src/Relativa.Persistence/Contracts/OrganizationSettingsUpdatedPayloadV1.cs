namespace Relativa.Persistence.Contracts;

public sealed record OrganizationSettingsUpdatedPayloadV1(
    int OrganizationId,
    int ActorUserId);
