namespace Relativa.Persistence.Entities;

public class EntityRelationship
{
    public int Id { get; set; }
    public int SourceEntityId { get; set; }
    public int TargetEntityId { get; set; }
    public int RelationshipTypeId { get; set; }
    public Entity SourceEntity { get; set; } = null!;
    public Entity TargetEntity { get; set; } = null!;
    public EntityRelationshipType RelationshipType { get; set; } = null!;
}
