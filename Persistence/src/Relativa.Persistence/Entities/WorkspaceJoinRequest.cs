namespace Relativa.Persistence.Entities;

public class WorkspaceJoinRequest
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int WorkspaceId { get; set; }
    public string? Message { get; set; }
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public int? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public User User { get; set; } = null!;
    public Workspace Workspace { get; set; } = null!;
    public User? ReviewedBy { get; set; }
}
