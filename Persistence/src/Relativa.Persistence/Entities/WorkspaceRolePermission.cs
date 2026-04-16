namespace Relativa.Persistence.Entities;

public class WorkspaceRolePermission
{
    public int Id { get; set; }
    public int WsRoleId { get; set; }
    public int PermissionId { get; set; }
    public WorkspaceRole Role { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}
