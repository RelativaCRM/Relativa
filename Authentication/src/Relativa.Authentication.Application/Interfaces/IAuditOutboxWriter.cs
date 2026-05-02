using Relativa.Persistence.Contracts;

namespace Relativa.Authentication.Application.Interfaces;

public interface IAuditOutboxWriter
{
    Task EnqueueAsync(AuditEventContract auditEvent, CancellationToken ct = default);
}
