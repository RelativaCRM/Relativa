namespace Relativa.Persistence.Entities;

public class EntityType
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public bool IsStandalone { get; set; } = true;
    public ICollection<Entity> Entities { get; set; } = new List<Entity>();
    public ICollection<EntityTypeProperty> EntityTypeProperties { get; set; } = new List<EntityTypeProperty>();
    public ICollection<EntityRelationshipType> SourceRelationshipTypes { get; set; } = new List<EntityRelationshipType>();
    public ICollection<EntityRelationshipType> TargetRelationshipTypes { get; set; } = new List<EntityRelationshipType>();
}
