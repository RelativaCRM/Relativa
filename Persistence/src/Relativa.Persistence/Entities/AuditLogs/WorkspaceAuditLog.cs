namespace Relativa.Persistence.Entities.AuditLogs;

public class WorkspaceAuditLog : AuditLog
{
    public int? WorkspaceId { get; set; }
    public Workspace? Workspace { get; set; }
}
