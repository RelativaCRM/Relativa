using System.Text.Json;
using Relativa.Authentication.Application.Interfaces;
using Relativa.Authentication.Infrastructure.Data;
using Relativa.Persistence.Contracts;
using Relativa.Persistence.Entities;

namespace Relativa.Authentication.Infrastructure.Services.Audit;

public sealed class AuditOutboxWriter(AuthDbContext db) : IAuditOutboxWriter
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
