namespace Relativa.Persistence.Entities;

public sealed class AuditProcessedEvent
{
    public Guid EventId { get; set; }
    public DateTimeOffset ProcessedAtUtc { get; set; }
}
