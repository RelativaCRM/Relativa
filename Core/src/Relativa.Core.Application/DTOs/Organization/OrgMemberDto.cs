namespace Relativa.Core.Application.DTOs.Organization;

public sealed record OrgMemberDto(int UserId, string FirstName, string LastName, string Email, string RoleName, string RoleDisplayName, DateTime JoinedAt);
