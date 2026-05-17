namespace Relativa.Persistence.Entities;

public class PropertyAllowedValue
{
    public int PropertyId { get; set; }
    public string Value { get; set; } = null!;
    public Property Property { get; set; } = null!;
}
