namespace Relativa.Persistence.Entities;

public class EntityTypeProperty
{
    public int EntityTypeId { get; set; }
    public int PropertyId { get; set; }
    public bool IsRequired { get; set; }
    public EntityType EntityType { get; set; } = null!;
    public Property Property { get; set; } = null!;
}
