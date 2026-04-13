namespace Relativa.Persistence.Entities;

public class WorkspaceInvitation
{
    public int Id { get; set; }
    public int WorkspaceId { get; set; }
    public string Email { get; set; } = null!;
    public int RoleId { get; set; }
    public int InvitedByUserId { get; set; }
    public string Token { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public Workspace Workspace { get; set; } = null!;
    public Role Role { get; set; } = null!;
    public User InvitedBy { get; set; } = null!;
}
