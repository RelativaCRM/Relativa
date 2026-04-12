namespace Relativa.Persistence.Entities;

public class PersonalDataPropertyValue
{
    public int Id { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? PassportNumber { get; set; }
    public DateOnly? BirthDate { get; set; }
}
