namespace Relativa.Persistence.Entities;

public class UserRoleWorkspace
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int WorkspaceId { get; set; }
    public int WsRoleId { get; set; }
    public DateTime JoinedAt { get; set; }
    public bool IsArchived { get; set; }
    public User User { get; set; } = null!;
    public Workspace Workspace { get; set; } = null!;
    public WorkspaceRole Role { get; set; } = null!;
}
