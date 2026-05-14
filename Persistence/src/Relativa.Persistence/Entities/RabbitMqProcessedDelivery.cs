namespace Relativa.Persistence.Entities;

/// <summary>Exactly-once idempotency receipts for choreography consumers keyed by RabbitMQ envelope <see cref="Relativa.Persistence.Contracts.DomainMessageEnvelope.MessageId"/>.</summary>
public sealed class RabbitMqProcessedDelivery
{
    public Guid MessageId { get; set; }

    /// <summary>Stable consumer logical name e.g. graph.domain.v1.</summary>
    public string ConsumerGroup { get; set; } = null!;

    public DateTimeOffset ProcessedAtUtc { get; set; }
}
