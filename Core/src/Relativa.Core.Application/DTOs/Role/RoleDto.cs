namespace Relativa.Core.Application.DTOs.Role;

public sealed record RoleDto(
    int Id,
    string Name,
    string DisplayName,
    bool IsSystem,
    List<PermissionDto> Permissions);
