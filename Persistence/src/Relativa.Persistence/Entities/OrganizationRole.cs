namespace Relativa.Persistence.Entities;

public class OrganizationRole
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? DisplayName { get; set; }
    public int? OrganizationId { get; set; }
    /// <summary>Lower value = stronger role in the org hierarchy (<c>0</c> is strongest).</summary>
    public int Priority { get; set; }
    public bool IsArchived { get; set; }
    public Organization? Organization { get; set; }
    public ICollection<OrganizationRolePermission> RolePermissions { get; set; } = new List<OrganizationRolePermission>();
    public ICollection<UserRoleOrganization> OrganizationMembers { get; set; } = new List<UserRoleOrganization>();
}
