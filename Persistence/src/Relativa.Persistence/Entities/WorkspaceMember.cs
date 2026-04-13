namespace Relativa.Persistence.Entities;

public class WorkspaceMember
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int WorkspaceId { get; set; }
    public int RoleId { get; set; }
    public DateTime JoinedAt { get; set; }
    public bool IsArchived { get; set; }
    public User User { get; set; } = null!;
    public Workspace Workspace { get; set; } = null!;
    public Role Role { get; set; } = null!;
}
