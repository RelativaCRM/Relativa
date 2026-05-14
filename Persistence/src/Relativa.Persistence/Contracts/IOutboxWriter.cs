namespace Relativa.Persistence.Contracts;

/// <summary>Transactional outbox: audit stream ( RabbitMQ routing key audit.* ) plus domain choreography (all other routing keys).</summary>
public interface IOutboxWriter
{
    Task EnqueueAuditAsync(AuditEventContract auditEvent, CancellationToken ct = default);

    /// <remarks>Enqueue a domain choreography message. Persisted alongside business data in <c>audit_outbox</c> rows using <see cref="DomainMessageEnvelope.MessageId"/>.</remarks>
    Task EnqueueDomainAsync(string routingKey, DomainMessageEnvelope envelope, CancellationToken ct = default);
}
