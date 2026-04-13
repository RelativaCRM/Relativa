namespace Relativa.Persistence.Entities;

public class Organization
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public bool IsArchived { get; set; }
    public ICollection<OrganizationWorkspace> OrganizationWorkspaces { get; set; } = new List<OrganizationWorkspace>();
}
