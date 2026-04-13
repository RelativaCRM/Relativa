namespace Relativa.Persistence.Entities;

public class Workspace
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int CreatedByUserId { get; set; }
    public bool IsArchived { get; set; }
    public User CreatedBy { get; set; } = null!;
    public ICollection<OrganizationWorkspace> OrganizationWorkspaces { get; set; } = new List<OrganizationWorkspace>();
    public ICollection<EntityWorkspace> EntityWorkspaces { get; set; } = new List<EntityWorkspace>();
    public ICollection<WorkspaceMember> Members { get; set; } = new List<WorkspaceMember>();
    public ICollection<WorkspaceInvitation> Invitations { get; set; } = new List<WorkspaceInvitation>();
    public ICollection<Role> Roles { get; set; } = new List<Role>();
}
