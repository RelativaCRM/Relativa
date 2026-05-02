namespace Relativa.Persistence.Entities;

public sealed class AuditOutboxMessage
{
    public long Id { get; set; }
    public Guid EventId { get; set; }
    public string PayloadJson { get; set; } = null!;
    public string RoutingKey { get; set; } = null!;
    public DateTimeOffset OccurredAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? PublishedAtUtc { get; set; }
    public int PublishAttempts { get; set; }
    public string? LastError { get; set; }
}
