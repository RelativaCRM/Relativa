namespace Relativa.Core.Application.DTOs.Invitation;

public sealed record InvitationDto(int Id, string Email, string WorkspaceName, string RoleName, string Status, string Token, DateTime ExpiresAt);
