using Relativa.Audit.Application.DTOs;
using Relativa.Audit.Application.Validators;

namespace Relativa.Audit.Application.Interfaces;

public interface IAuditLogReadService
{
    Task<AuditLogListResponse> GetAsync(GetAuditLogQuery q, int callerUserId, CancellationToken ct);
}
