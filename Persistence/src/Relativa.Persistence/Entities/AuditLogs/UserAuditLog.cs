namespace Relativa.Persistence.Entities.AuditLogs;

public class UserAuditLog : AuditLog
{
    public int? TargetUserId { get; set; }
    public User? TargetUser { get; set; }
}
