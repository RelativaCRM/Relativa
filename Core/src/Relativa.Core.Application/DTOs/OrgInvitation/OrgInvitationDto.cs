namespace Relativa.Core.Application.DTOs.OrgInvitation;

public sealed record OrgInvitationDto(
    int Id,
    string Email,
    string OrganizationName,
    string RoleName,
    string Status,
    string Token,
    DateTime ExpiresAt);
