using Relativa.Persistence.Contracts;

namespace Relativa.Core.Application.Interfaces;

public interface IAuditOutboxWriter
{
    Task EnqueueAsync(AuditEventContract auditEvent, CancellationToken ct = default);
}
