namespace Relativa.Core.Application.DTOs.Member;

public sealed record WorkspaceMemberDto(
    int UserId,
    string FirstName,
    string LastName,
    string Email,
    string RoleName,
    string RoleDisplayName,
    DateTime JoinedAt);
