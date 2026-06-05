namespace Relativa.Persistence.Entities;

public class UserBackupCode
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string CodeHash { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public User User { get; set; } = null!;
}
