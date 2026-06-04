namespace Relativa.Core.Application.DTOs.Workspace;

public sealed record WorkspaceDto(
    int Id,
    int OrganizationId,
    string Name,
    int MemberCount,
    string? UserRole,
    string? UserRoleDisplayName,
    IReadOnlyList<string> MyPermissions);
