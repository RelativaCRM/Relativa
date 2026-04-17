namespace Relativa.Persistence.Entities;

public class WorkspaceRole
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int? WorkspaceId { get; set; }
    public bool IsArchived { get; set; }
    public Workspace? Workspace { get; set; }
    public ICollection<WorkspaceRolePermission> RolePermissions { get; set; } = new List<WorkspaceRolePermission>();
    public ICollection<UserRoleWorkspace> WorkspaceMembers { get; set; } = new List<UserRoleWorkspace>();
}
