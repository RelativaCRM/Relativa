namespace Relativa.Core.Domain.Entities;

public class Workspace
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public bool IsArchived { get; set; }
    public ICollection<OrganizationWorkspace> OrganizationWorkspaces { get; set; } = new List<OrganizationWorkspace>();
    public ICollection<EntityWorkspace> EntityWorkspaces { get; set; } = new List<EntityWorkspace>();
}
