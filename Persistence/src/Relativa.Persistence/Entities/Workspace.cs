namespace Relativa.Persistence.Entities;

public class Workspace
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int OrganizationId { get; set; }
    public int CreatedByUserId { get; set; }
    public bool IsArchived { get; set; }
    public Organization Organization { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
    public WorkspaceSettings? WorkspaceSettings { get; set; }
    public ICollection<EntityWorkspace> EntityWorkspaces { get; set; } = new List<EntityWorkspace>();
    public ICollection<UserRoleWorkspace> Members { get; set; } = new List<UserRoleWorkspace>();
    public ICollection<WorkspaceRole> Roles { get; set; } = new List<WorkspaceRole>();
}
