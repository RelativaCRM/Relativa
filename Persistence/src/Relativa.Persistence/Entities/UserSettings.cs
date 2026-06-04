namespace Relativa.Persistence.Entities;

public class UserSettings
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Locale { get; set; } = "en";
    public User User { get; set; } = null!;
}
