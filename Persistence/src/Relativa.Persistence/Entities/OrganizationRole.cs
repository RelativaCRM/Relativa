namespace Relativa.Persistence.Entities;

public class OrganizationRole
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int? OrganizationId { get; set; }
    public bool IsArchived { get; set; }
    public Organization? Organization { get; set; }
    public ICollection<OrganizationRolePermission> RolePermissions { get; set; } = new List<OrganizationRolePermission>();
    public ICollection<UserRoleOrganization> OrganizationMembers { get; set; } = new List<UserRoleOrganization>();
}
