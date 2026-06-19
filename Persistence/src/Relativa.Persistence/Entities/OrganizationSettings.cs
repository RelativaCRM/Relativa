namespace Relativa.Persistence.Entities;

public class OrganizationSettings
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public string? Description { get; set; }
    public string JoinPolicy { get; set; } = "open";
    public int? DefaultOrgRoleId { get; set; }
    public Organization Organization { get; set; } = null!;
    public OrganizationRole? DefaultOrgRole { get; set; }
}
