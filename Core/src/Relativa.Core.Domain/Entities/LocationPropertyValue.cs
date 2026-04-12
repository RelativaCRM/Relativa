namespace Relativa.Core.Domain.Entities;

public class LocationPropertyValue
{
    public int Id { get; set; }
    public string? Country { get; set; }
    public string? Region { get; set; }
    public string? State { get; set; }
    public string? City { get; set; }
    public string? Address { get; set; }
    public string? PostalCode { get; set; }
    public string? Locale { get; set; }
    public string? Timezone { get; set; }
}
