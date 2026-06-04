namespace Relativa.Core.Application.DTOs.OrgInvitation;

public sealed record OrgInvitationDto(
    int Id,
    int OrganizationId,
    string Email,
    string OrganizationName,
    string RoleName,
    string RoleDisplayName,
    string Status,
    string Token,
    DateTime ExpiresAt);
