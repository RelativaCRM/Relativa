namespace Relativa.Persistence.Contracts;

public static class AuditRouting
{
    public const string ExchangeName = "audit.events";

    public const string ScopeEntity = "entity";
    public const string ScopeWorkspace = "workspace";
    public const string ScopeOrganization = "organization";
    public const string ScopeUser = "user";
}

public sealed record AuditEventContract(
    Guid EventId,
    int SchemaVersion,
    DateTimeOffset OccurredAtUtc,
    string SourceService,
    int? ActorUserId,
    string AuditScope,
    int? TargetId,
    string Action,
    string? FieldName,
    string? EntityType,
    string? OldValueJson,
    string? NewValueJson
);
