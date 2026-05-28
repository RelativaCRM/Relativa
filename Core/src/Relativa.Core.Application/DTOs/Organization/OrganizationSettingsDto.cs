namespace Relativa.Core.Application.DTOs.Organization;

public sealed record OrganizationSettingsDto(
    int OrganizationId,
    string? Description,
    string JoinPolicy,
    int? DefaultOrgRoleId,
    string? DefaultOrgRoleName);
