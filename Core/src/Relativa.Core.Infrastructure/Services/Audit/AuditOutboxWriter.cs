using System.Text.Json;
using Relativa.Core.Application.Interfaces;
using Relativa.Core.Infrastructure.Data;
using Relativa.Persistence.Contracts;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Infrastructure.Services.Audit;

public sealed class AuditOutboxWriter(RelativaDbContext db) : IAuditOutboxWriter
{
    public async Task EnqueueAsync(AuditEventContract auditEvent, CancellationToken ct = default)
    {
        var message = new AuditOutboxMessage
        {
            EventId = auditEvent.EventId,
            RoutingKey = $"audit.{auditEvent.AuditScope}",
            PayloadJson = JsonSerializer.Serialize(auditEvent),
            OccurredAtUtc = auditEvent.OccurredAtUtc,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            PublishAttempts = 0
        };

        db.AuditOutboxMessages.Add(message);
        await db.SaveChangesAsync(ct);
    }
}
