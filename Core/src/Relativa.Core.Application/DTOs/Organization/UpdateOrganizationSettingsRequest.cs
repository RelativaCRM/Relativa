namespace Relativa.Core.Application.DTOs.Organization;

public sealed record UpdateOrganizationSettingsRequest(
    string? Description,
    string JoinPolicy,
    int? DefaultOrgRoleId);
