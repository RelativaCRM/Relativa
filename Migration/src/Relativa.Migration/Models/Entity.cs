using System.Collections.Generic;
namespace Relativa.Migration.Models;
public class Entity {
    public int Id { get; set; }
    public int Type { get; set; }
    public bool IsArchived { get; set; }
    public EntityType EntityType { get; set; } = null!;
    public ICollection<EntityWorkspace> EntityWorkspaces { get; set; } = new List<EntityWorkspace>();
    public ICollection<DealPropertyValue> DealPropertyValues { get; set; } = new List<DealPropertyValue>();
    public EntityProperty? EntityProperty { get; set; }
}
