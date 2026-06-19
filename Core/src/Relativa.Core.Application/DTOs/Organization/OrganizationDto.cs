namespace Relativa.Core.Application.DTOs.Organization;

public sealed record OrganizationDto(
    int Id,
    string Name,
    int MemberCount,
    string? UserRole,
    string? UserRoleDisplayName,
    IReadOnlyList<string> MyPermissions);
