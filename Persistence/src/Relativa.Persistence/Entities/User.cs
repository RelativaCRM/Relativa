namespace Relativa.Persistence.Entities;

public class User
{
    public int Id { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public bool IsArchived { get; set; }
    public ICollection<Entity> CreatedEntities { get; set; } = new List<Entity>();
    public ICollection<UserRoleWorkspace> WorkspaceMemberships { get; set; } = new List<UserRoleWorkspace>();
    public ICollection<UserRoleOrganization> OrganizationMemberships { get; set; } = new List<UserRoleOrganization>();
}
