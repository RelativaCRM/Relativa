namespace Relativa.Persistence.Entities;

public class OrganizationJoinRequest
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int OrganizationId { get; set; }
    public string? Message { get; set; }
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public int? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public User User { get; set; } = null!;
    public Organization Organization { get; set; } = null!;
    public User? ReviewedBy { get; set; }
}
