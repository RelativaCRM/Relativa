namespace Relativa.Persistence.Entities;

public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int? WorkspaceId { get; set; }
    public bool IsArchived { get; set; }
    public Workspace? Workspace { get; set; }
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<WorkspaceMember> WorkspaceMembers { get; set; } = new List<WorkspaceMember>();
}
