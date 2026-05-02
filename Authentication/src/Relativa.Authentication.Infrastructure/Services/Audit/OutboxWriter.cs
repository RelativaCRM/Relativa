using System.Text.Json;
using Relativa.Authentication.Infrastructure.Data;
using Relativa.Persistence.Contracts;
using Relativa.Persistence.Entities;

namespace Relativa.Authentication.Infrastructure.Services.Audit;

public sealed class OutboxWriter(AuthDbContext db) : IOutboxWriter
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

    /// <inheritdoc />
    /// <remarks>Authentication service does not publish choreography domain messages today.</remarks>
    public Task EnqueueDomainAsync(string routingKey, DomainMessageEnvelope envelope, CancellationToken ct = default) =>
        Task.CompletedTask;
}
