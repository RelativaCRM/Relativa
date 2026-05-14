namespace Relativa.Persistence.Entities;

public class EntityPropertyValue
{
    public int EntityId { get; set; }
    public int PropertyId { get; set; }
    public string? ValueString { get; set; }
    public int? ValueInt { get; set; }
    public decimal? ValueDecimal { get; set; }
    public bool? ValueBool { get; set; }
    public DateOnly? ValueDate { get; set; }
    public Entity Entity { get; set; } = null!;
    public Property Property { get; set; } = null!;
}
