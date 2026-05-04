namespace Relativa.Core.Application.DTOs.Organization;

public sealed record CreateOrgUserRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    int? OrgRoleId);
