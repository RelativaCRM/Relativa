namespace Relativa.Persistence.Entities;

public class OrganizationRolePermission
{
    public int Id { get; set; }
    public int OrgRoleId { get; set; }
    public int PermissionId { get; set; }
    public OrganizationRole Role { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}
