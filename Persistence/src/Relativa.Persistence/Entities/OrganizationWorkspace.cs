namespace Relativa.Persistence.Entities;

public class OrganizationWorkspace
{
    public int Id { get; set; }
    public int OrgId { get; set; }
    public int WorkspaceId { get; set; }
    public Organization Organization { get; set; } = null!;
    public Workspace Workspace { get; set; } = null!;
}
