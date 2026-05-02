using System.Text.Json;
using Relativa.Core.Infrastructure.Data;
using Relativa.Persistence.Contracts;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Infrastructure.Services.Audit;

public sealed class OutboxWriter(RelativaDbContext db) : IOutboxWriter
{
    public async Task EnqueueAuditAsync(AuditEventContract auditEvent, CancellationToken ct = default)
    {
        db.AuditOutboxMessages.Add(new AuditOutboxMessage
        {
            EventId = auditEvent.EventId,
            RoutingKey = $"audit.{auditEvent.AuditScope}",
            PayloadJson = JsonSerializer.Serialize(auditEvent),
            OccurredAtUtc = auditEvent.OccurredAtUtc,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            PublishAttempts = 0
        });

        await db.SaveChangesAsync(ct);
    }

    public async Task EnqueueDomainAsync(string routingKey, DomainMessageEnvelope envelope, CancellationToken ct = default)
    {
        db.AuditOutboxMessages.Add(new AuditOutboxMessage
        {
            EventId = envelope.MessageId,
            RoutingKey = routingKey,
            PayloadJson = JsonSerializer.Serialize(envelope),
            OccurredAtUtc = envelope.OccurredAtUtc,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            PublishAttempts = 0
        });

        await db.SaveChangesAsync(ct);
    }
}
