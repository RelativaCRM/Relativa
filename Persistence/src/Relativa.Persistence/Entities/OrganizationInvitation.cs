namespace Relativa.Persistence.Entities;

public class OrganizationInvitation
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public string Email { get; set; } = null!;
    public int OrgRoleId { get; set; }
    public int InvitedByUserId { get; set; }
    public string Token { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public Organization Organization { get; set; } = null!;
    public OrganizationRole Role { get; set; } = null!;
    public User InvitedBy { get; set; } = null!;
}
