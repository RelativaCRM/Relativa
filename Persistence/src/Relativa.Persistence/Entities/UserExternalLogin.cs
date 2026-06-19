namespace Relativa.Persistence.Entities;

public class UserExternalLogin
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Provider { get; set; } = null!;
    public string Subject { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public User User { get; set; } = null!;
}
