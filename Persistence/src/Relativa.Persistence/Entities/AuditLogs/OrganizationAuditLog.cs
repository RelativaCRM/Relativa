namespace Relativa.Persistence.Entities.AuditLogs;

public class OrganizationAuditLog : AuditLog
{
    public int? OrganizationId { get; set; }
    public Organization? Organization { get; set; }
}
