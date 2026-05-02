using System.Text.Json;

namespace Relativa.Persistence.Entities.AuditLogs;

public abstract class AuditLog
{
    public Guid Id { get; set; }
    public string Action { get; set; } = null!;
    public int? ChangedById { get; set; }
    public User? ChangedBy { get; set; }
    public string? FieldName { get; set; }
    public JsonDocument? OldValue { get; set; }
    public JsonDocument? NewValue { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
}
