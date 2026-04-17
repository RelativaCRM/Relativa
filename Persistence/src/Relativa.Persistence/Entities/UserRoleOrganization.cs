namespace Relativa.Persistence.Entities;

public class UserRoleOrganization
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int OrganizationId { get; set; }
    public int OrgRoleId { get; set; }
    public DateTime JoinedAt { get; set; }
    public bool IsArchived { get; set; }
    public User User { get; set; } = null!;
    public Organization Organization { get; set; } = null!;
    public OrganizationRole Role { get; set; } = null!;
}
