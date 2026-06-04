namespace Relativa.Persistence.Entities;

public class Permission
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? DisplayName { get; set; }
    public bool IsArchived { get; set; }
    public ICollection<WorkspaceRolePermission> WorkspaceRolePermissions { get; set; } = new List<WorkspaceRolePermission>();
    public ICollection<OrganizationRolePermission> OrganizationRolePermissions { get; set; } = new List<OrganizationRolePermission>();
}
