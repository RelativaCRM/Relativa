namespace Relativa.Migration.Models;
public class EntityProperty {
    public int Id { get; set; }
    public int EntityId { get; set; }
    public int? PersonalDataPropertyId { get; set; }
    public int? LocationPropertyId { get; set; }
    public int? DealPropertyId { get; set; }
    public Entity Entity { get; set; } = null!;
    public PersonalDataPropertyValue? PersonalDataProperty { get; set; }
    public LocationPropertyValue? LocationProperty { get; set; }
    public DealPropertyValue? DealProperty { get; set; }
}
