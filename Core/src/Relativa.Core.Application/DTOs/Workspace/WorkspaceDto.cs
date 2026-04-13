namespace Relativa.Core.Application.DTOs.Workspace;

public sealed record WorkspaceDto(
    int Id,
    string Name,
    int MemberCount,
    string? UserRole);
