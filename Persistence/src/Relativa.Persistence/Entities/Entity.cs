namespace Relativa.Persistence.Entities;

public class Entity
{
    public int Id { get; set; }
    public int EntityTypeId { get; set; }
    public int CreatedByUserId { get; set; }
    public bool IsArchived { get; set; }
    public EntityType EntityType { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public ICollection<EntityWorkspace> EntityWorkspaces { get; set; } = new List<EntityWorkspace>();
    public ICollection<EntityPropertyValue> EntityPropertyValues { get; set; } = new List<EntityPropertyValue>();
    public ICollection<EntityRelationship> SourceRelationships { get; set; } = new List<EntityRelationship>();
    public ICollection<EntityRelationship> TargetRelationships { get; set; } = new List<EntityRelationship>();
}
