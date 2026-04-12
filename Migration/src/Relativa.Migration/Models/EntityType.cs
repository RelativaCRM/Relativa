using System.Collections.Generic;
namespace Relativa.Migration.Models;
public class EntityType {
    public int Id { get; set; }
    public string TypeId { get; set; } = null!;
    public bool IsArchived { get; set; }
    public ICollection<Entity> Entities { get; set; } = new List<Entity>();
}
