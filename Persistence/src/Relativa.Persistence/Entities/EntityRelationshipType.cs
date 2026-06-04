namespace Relativa.Persistence.Entities;

public class EntityRelationshipType
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? DisplayName { get; set; }
    public int SourceEntityTypeId { get; set; }
    public int TargetEntityTypeId { get; set; }
    public bool IsRequired { get; set; }
    public RelationshipCardinality RelationshipCardinality { get; set; } = RelationshipCardinality.ManyToOne;
    public EntityType SourceEntityType { get; set; } = null!;
    public EntityType TargetEntityType { get; set; } = null!;
    public ICollection<EntityRelationship> EntityRelationships { get; set; } = new List<EntityRelationship>();
}
