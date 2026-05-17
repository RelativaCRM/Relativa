namespace Relativa.Persistence.Entities;

public enum PropertyDataType
{
    String,
    Int,
    Decimal,
    Bool,
    Date
}

public class Property
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public PropertyDataType DataType { get; set; }
    public bool IsReadonly { get; set; }
    public int? OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public ICollection<EntityTypeProperty> EntityTypeProperties { get; set; } = new List<EntityTypeProperty>();
    public ICollection<EntityPropertyValue> EntityPropertyValues { get; set; } = new List<EntityPropertyValue>();
    public ICollection<PropertyAllowedValue> AllowedValues { get; set; } = new List<PropertyAllowedValue>();
}
