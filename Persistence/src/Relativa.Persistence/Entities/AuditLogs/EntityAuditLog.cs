namespace Relativa.Persistence.Entities.AuditLogs;

public class EntityAuditLog : AuditLog
{
    public int? EntityId { get; set; }
    public Entity? Entity { get; set; }
    public string? EntityType { get; set; }
}
