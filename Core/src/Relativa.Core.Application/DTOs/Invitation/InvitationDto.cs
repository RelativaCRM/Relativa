namespace Relativa.Core.Application.DTOs.Invitation;

public sealed record InvitationDto(
    int Id,
    string Email,
    string RoleName,
    string Status,
    DateTime ExpiresAt);
