namespace Relativa.Persistence.Entities;

public class PropertyAllowedValue
{
    public int PropertyId { get; set; }
    public string Value { get; set; } = null!;
    public string? DisplayName { get; set; }
    public Property Property { get; set; } = null!;
}
