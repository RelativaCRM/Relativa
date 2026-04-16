namespace Relativa.Persistence.Entities;

public class Organization
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public bool IsArchived { get; set; }
    public ICollection<UserRoleOrganization> Members { get; set; } = new List<UserRoleOrganization>();
    public ICollection<OrganizationRole> Roles { get; set; } = new List<OrganizationRole>();
    public ICollection<OrganizationJoinRequest> JoinRequests { get; set; } = new List<OrganizationJoinRequest>();
    public ICollection<OrganizationInvitation> Invitations { get; set; } = new List<OrganizationInvitation>();
    public ICollection<Workspace> Workspaces { get; set; } = new List<Workspace>();
}
