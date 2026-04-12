namespace Relativa.Persistence.Entities;

public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public bool IsArchived { get; set; }
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public ICollection<User> Users { get; set; } = new List<User>();
}
