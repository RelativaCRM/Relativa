namespace Relativa.Persistence.Entities;

public class UserEmail
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Address { get; set; } = null!;
    public bool IsVerified { get; set; }
    public string Source { get; set; } = "manual";
    public string? VerificationToken { get; set; }
    public DateTime? VerificationTokenExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public User User { get; set; } = null!;
}
